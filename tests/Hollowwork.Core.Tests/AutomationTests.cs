using Hollowwork.Automation;

namespace Hollowwork.Core.Tests;

public class AutomationTests
{
    [Fact]
    public void Pattern_scanner_finds_bytes()
    {
        var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
        var pattern = new byte[] { 3, 4, 5 };

        Assert.Equal(3, PatternScanner.IndexOf(data, pattern));
        Assert.Equal(-1, PatternScanner.IndexOf(data, new byte[] { 9 }));

        var large = new byte[0x200000];
        pattern.CopyTo(large.AsSpan(0x1F49B4));
        Assert.Equal(0x1F49B4, PatternScanner.IndexOf(large, pattern));
    }

    [Fact]
    public void Loads_the_poe1_profile()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "automation", "poe1.json");
        var profile = AutomationProfile.Load(path);

        Assert.Equal("poe1", profile.Game);
        Assert.Equal("PathOfExileSteam.exe", profile.ProcessName);
        Assert.Equal(8, profile.GameRootAdjust);
        Assert.Equal(0x88, profile.InGameStateOffset);
        Assert.Equal(0x218, profile.DataOffset);
        Assert.Equal(0x980, profile.PlayerOffset);
        Assert.Equal(0x1A8, profile.Life.CurHp);
        Assert.Equal(0x240, profile.Life.CurEs);
        Assert.True(profile.PatternBytes().Length >= 8);
    }

    [Fact]
    public void Reader_follows_the_configured_pointer_chain()
    {
        var profile = AutomationProfile.Load(
            Path.Combine(AppContext.BaseDirectory, "automation", "poe1.json"));
        var memory = new FakeMemory(0x100000, 0x10000);
        var match = 0x100100UL;
        var pattern = profile.PatternBytes();
        memory.Write(match, pattern);
        memory.WriteInt32(match + (ulong)pattern.Length, 0x200);

        var reference = match + 0x200;
        memory.WritePtr(reference + (ulong)profile.GameRootPointerOffset, 0x105000);
        var gameRoot = 0x105000UL + (ulong)profile.GameRootAdjust;
        memory.WritePtr(gameRoot + (ulong)profile.InGameStateOffset, 0x106000);
        memory.WriteByte(0x106000 + 0x209, 4);
        memory.WritePtr(0x106000 + (ulong)profile.DataOffset, 0x107000);
        memory.WritePtr(0x107000 + (ulong)profile.PlayerOffset, 0x108000);

        memory.WritePtr(0x108000 + (ulong)profile.Entity.DetailsOffset, 0x109000);
        memory.WritePtr(0x109000 + (ulong)profile.Entity.LookupOffset, 0x10A000);
        memory.WriteArray(0x10A000 + (ulong)profile.Entity.ComponentMapOffset, 0x10B000, 0x10B010);
        memory.WriteArray(0x108000 + (ulong)profile.Entity.ComponentsOffset, 0x10C000, 0x10C008);
        memory.WritePtr(0x10B000, 0x10D000);
        memory.WriteInt32(0x10B000 + 8, 0);
        memory.Write(0x10D000, "Life\0"u8.ToArray());
        memory.WritePtr(0x10C000, 0x10E000);

        memory.WriteInt32(0x10E000 + (ulong)profile.Life.CurHp, 40);
        memory.WriteInt32(0x10E000 + (ulong)profile.Life.MaxHp, 100);
        memory.WriteInt32(0x10E000 + (ulong)profile.Life.CurEs, 25);
        memory.WriteInt32(0x10E000 + (ulong)profile.Life.MaxEs, 50);
        memory.WriteInt32(0x10E000 + (ulong)profile.Life.CurMp, 30);
        memory.WriteInt32(0x10E000 + (ulong)profile.Life.MaxMp, 100);

        var probe = new byte[memory.ImageSize];
        Assert.True(memory.TryRead(memory.BaseAddress, probe));
        Assert.Equal(0x100, PatternScanner.IndexOf(probe, pattern));
        using var reader = new Poe1Reader(memory, profile);
        Assert.True(reader.TryAttach(out var error), error);
        var vitals = reader.ReadVitals();

        Assert.NotNull(vitals);
        Assert.Equal(40, vitals.Value.Hp);
        Assert.Equal(100, vitals.Value.MaxHp);
        Assert.Equal(25, vitals.Value.Es);
        Assert.Equal(30, vitals.Value.Mp);
    }

    [Fact]
    public void Should_press_respects_threshold_and_cooldown()
    {
        var now = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(AutoFlaskEngine.ShouldPress(30, 100, 0.40, now, DateTime.MinValue, 1000));
        Assert.False(AutoFlaskEngine.ShouldPress(50, 100, 0.40, now, DateTime.MinValue, 1000));
        Assert.False(AutoFlaskEngine.ShouldPress(30, 100, 0.40, now, now, 1000));
        Assert.True(AutoFlaskEngine.ShouldPress(30, 100, 0.40, now, now.AddMilliseconds(-1200), 1000));
    }

    [Fact]
    public void Vitals_report_percentages_and_reject_impossible_values()
    {
        var vitals = new PlayerVitals(40, 100, 25, 50, 30, 100);

        Assert.Equal(40, vitals.HpPercent, 3);
        Assert.Equal(50, vitals.EsPercent, 3);
        Assert.Equal(30, vitals.MpPercent, 3);
        Assert.True(vitals.IsSane);
        Assert.False(new PlayerVitals(300, 100, 0, 0, 0, 0).IsSane);
        Assert.False(new PlayerVitals(1, 0, 0, 0, 0, 0).IsSane);
    }

    private sealed class FakeMemory : IMemoryReader
    {
        private readonly byte[] _bytes;

        public FakeMemory(ulong baseAddress, int size)
        {
            BaseAddress = baseAddress;
            ImageSize = size;
            _bytes = new byte[size];
        }

        public int ProcessId => 1;

        public ulong BaseAddress { get; }

        public int ImageSize { get; }

        public bool TryRead(ulong address, Span<byte> buffer)
        {
            if (address < BaseAddress)
            {
                return false;
            }

            var offset = address - BaseAddress;
            if (offset + (ulong)buffer.Length > (ulong)_bytes.Length)
            {
                return false;
            }

            _bytes.AsSpan((int)offset, buffer.Length).CopyTo(buffer);
            return true;
        }

        public void Write(ulong address, ReadOnlySpan<byte> data)
        {
            var offset = (int)(address - BaseAddress);
            data.CopyTo(_bytes.AsSpan(offset));
        }

        public void WriteByte(ulong address, byte value) => _bytes[(int)(address - BaseAddress)] = value;

        public void WriteInt32(ulong address, int value) =>
            BitConverter.TryWriteBytes(_bytes.AsSpan((int)(address - BaseAddress)), value);

        public void WritePtr(ulong address, ulong value) =>
            BitConverter.TryWriteBytes(_bytes.AsSpan((int)(address - BaseAddress)), value);

        public void WriteArray(ulong address, ulong head, ulong tail)
        {
            WritePtr(address, head);
            WritePtr(address + 8, tail);
        }

        public void Dispose()
        {
        }
    }

    [Theory]
    [InlineData("2026/11/16 09:31:22 123456789 abc [INFO Client 123] : You have entered Celestial Hideout.", "Celestial Hideout")]
    [InlineData("2026/11/16 09:31:22 1 a [INFO Client 1] : You have entered The Sarn Encampment.", "The Sarn Encampment")]
    public void Parses_the_area_from_a_log_line(string line, string expected)
    {
        Assert.Equal(expected, AreaWatcher.ParseArea(line));
    }

    [Theory]
    [InlineData("2026/11/16 09:31:22 1 a [INFO Client 1] : Connecting to instance server at 127.0.0.1:6112.")]
    [InlineData("2026/11/16 09:31:22 1 a [INFO Client 1] : AFK mode is now ON.")]
    public void Ignores_lines_that_are_not_an_area_entry(string line)
    {
        Assert.Null(AreaWatcher.ParseArea(line));
    }

    [Theory]
    [InlineData("Celestial Hideout", AreaKind.Hideout)]
    [InlineData("Lioneye's Watch", AreaKind.Town)]
    [InlineData("The Rogue Harbour", AreaKind.Town)]
    [InlineData("Cemetery Map", AreaKind.Map)]
    [InlineData(null, AreaKind.Unknown)]
    public void Classifies_areas(string? area, AreaKind expected)
    {
        Assert.Equal(expected, AreaWatcher.Classify(area));
    }

    [Fact]
    public void Follows_zone_changes_in_the_log()
    {
        var path = Path.GetTempFileName();
        try
        {
            using var watcher = new AreaWatcher(path);
            watcher.Poll();
            Assert.True(watcher.IsAvailable);
            Assert.Null(watcher.Area);

            File.AppendAllText(path, "[INFO Client 1] : You have entered Cemetery Map.\n");
            watcher.Poll();

            Assert.Equal("Cemetery Map", watcher.Area);
            Assert.Equal(AreaKind.Map, watcher.Kind);
            Assert.False(watcher.IsTransitioning);

            File.AppendAllText(path, "[INFO Client 1] : Connecting to instance server at 127.0.0.1:6112.\n");
            watcher.Poll();
            Assert.True(watcher.IsTransitioning);

            File.AppendAllText(path, "[INFO Client 1] : You have entered Celestial Hideout.\n");
            watcher.Poll();
            Assert.False(watcher.IsTransitioning);
            Assert.Equal(AreaKind.Hideout, watcher.Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Restarts_cleanly_when_the_log_is_truncated()
    {
        var path = Path.GetTempFileName();
        try
        {
            using var watcher = new AreaWatcher(path);
            watcher.Poll();

            File.AppendAllText(path, "[INFO Client 1] : You have entered Cemetery Map.\n");
            watcher.Poll();
            Assert.Equal("Cemetery Map", watcher.Area);

            // The game restarts and starts a fresh log.
            File.WriteAllText(path, "[INFO Client 1] : You have entered The Sarn Encampment.\n");
            watcher.Poll();

            Assert.Equal("The Sarn Encampment", watcher.Area);
            Assert.Equal(AreaKind.Town, watcher.Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(5, 848, "5% · 42")]
    [InlineData(40, 1450, "40% · 580")]
    [InlineData(50, 0, "50%")]
    public void Describes_a_threshold_in_points(double percent, int max, string expected)
    {
        Assert.Equal(expected, PlayerVitals.DescribeThreshold(percent, max));
    }

    [Fact]
    public void Parses_a_key_list()
    {
        Assert.Equal(["1", "2"], FlaskChannel.Parse("1, 2;"));
        Assert.Equal(["q", "F1"], FlaskChannel.Parse("q F1"));
        Assert.Empty(FlaskChannel.Parse("   "));
    }

    [Fact]
    public void Deduplicates_keys()
    {
        Assert.Equal(["1", "2"], FlaskChannel.Parse("1,1,2, 2"));
    }

    [Fact]
    public void Rotates_keys_and_respects_the_cooldown()
    {
        var channel = new FlaskChannel("1,2");
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal("1", channel.Press(start, 1000));
        Assert.Null(channel.Press(start.AddMilliseconds(500), 1000));
        Assert.Equal("2", channel.Press(start.AddMilliseconds(1000), 1000));
        Assert.Equal("1", channel.Press(start.AddMilliseconds(2000), 1000));
    }

    [Fact]
    public void Empty_channel_never_presses()
    {
        var channel = new FlaskChannel(null);

        Assert.True(channel.IsEmpty);
        Assert.Null(channel.Press(DateTime.UtcNow, 0));
    }
}
