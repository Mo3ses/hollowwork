using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;

namespace Hollowwork.Automation;

public sealed class Poe1Reader : IDisposable
{
    private const int MaxComponents = 64;
    private const int ComponentMapEntrySize = 16;

    private readonly IMemoryReader _memory;
    private readonly AutomationProfile _profile;
    private ulong _gameRoot;
    private ulong _inGameState;
    private ulong _data;
    private ulong _player;
    private ulong _life;

    public Poe1Reader(IMemoryReader memory, AutomationProfile profile)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    public int ProcessId => _memory.ProcessId;

    public ulong BaseAddress => _memory.BaseAddress;

    public bool IsAttached => _life != 0;

    /// <summary>False once the game process exits; the reader cannot recover without reopening.</summary>
    public bool IsProcessAlive
    {
        get
        {
            try
            {
                using var process = Process.GetProcessById(ProcessId);
                return !process.HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    public static bool TryOpen(
        AutomationProfile profile,
        out Poe1Reader? reader,
        out string error)
    {
        reader = null;
        try
        {
            var memory = ProcessMemoryFactory.Open(profile);

            // Attach is best effort: at the login screen the player does not exist yet, and the
            // engine retries on its own loop once an area is entered.
            var candidate = new Poe1Reader(memory, profile);
            candidate.TryAttach(out error);
            reader = candidate;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public bool TryAttach(out string error)
    {
        error = string.Empty;
        _gameRoot = 0;
        _inGameState = 0;
        _data = 0;
        _player = 0;
        _life = 0;

        var image = new byte[_memory.ImageSize];
        if (!_memory.TryRead(_memory.BaseAddress, image))
        {
            error = "Could not read the game image from memory.";
            return false;
        }

        var pattern = _profile.PatternBytes();
        var search = 0;
        var lastError = "GameRoot pattern not found. The game may have patched; update automation/poe1.json.";
        while (search < image.Length)
        {
            var index = PatternScanner.IndexOf(image.AsSpan(search), pattern);
            if (index < 0)
            {
                break;
            }

            var match = search + index;
            if (TryResolveAt(match, out var candidateError))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(candidateError))
            {
                lastError = $"GameRoot found, but the in-game player could not be resolved ({candidateError}). Enter an area and try again.";
            }

            search = match + 1;
        }

        error = lastError;
        return false;
    }

    public PlayerVitals? ReadVitals()
    {
        if (_life == 0)
        {
            return null;
        }

        var vitals = ReadLife(_life);
        if (vitals is { IsSane: true })
        {
            return vitals;
        }

        _life = 0;
        _player = 0;
        _data = 0;
        _inGameState = 0;
        _gameRoot = 0;
        return null;
    }

    public void Dispose() => _memory.Dispose();

    private bool TryResolveAt(int match, out string error)
    {
        error = "GameRoot found, but InGameState/player is not ready. Log in and enter an area.";
        try
        {
            var matchAddress = _memory.BaseAddress + (ulong)match;
            var pointerOffset = ReadInt32(matchAddress + (ulong)_profile.PatternBytes().Length);
            var referenceAddress = matchAddress + (ulong)(long)pointerOffset;
            var rootPointer = ReadPtr(referenceAddress + (ulong)_profile.GameRootPointerOffset);

            foreach (var adjust in new[] { _profile.GameRootAdjust, 0 })
            {
                var gameRoot = rootPointer + (ulong)adjust;
                if (!TryResolveChain(gameRoot, out var inGameState, out var data, out var player, out var life))
                {
                    continue;
                }

                _gameRoot = gameRoot;
                _inGameState = inGameState;
                _data = data;
                _player = player;
                _life = life;
                return true;
            }
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }

        return false;
    }

    private bool TryResolveChain(
        ulong gameRoot,
        out ulong inGameState,
        out ulong data,
        out ulong player,
        out ulong life)
    {
        inGameState = 0;
        data = 0;
        player = 0;
        life = 0;

        if (!TryReadPtr(gameRoot + (ulong)_profile.InGameStateOffset, out inGameState) ||
            inGameState == 0 ||
            !TryReadByte(inGameState + 0x209, out var kind) ||
            kind != 4)
        {
            return false;
        }

        if (!TryReadPtr(inGameState + (ulong)_profile.DataOffset, out data) ||
            data == 0 ||
            !TryReadPtr(data + (ulong)_profile.PlayerOffset, out player) ||
            player == 0)
        {
            return false;
        }

        return TryResolveLife(player, out life);
    }

    private bool TryResolveLife(ulong player, out ulong life)
    {
        life = 0;

        if (!TryReadPtr(player + (ulong)_profile.Entity.DetailsOffset, out var details) ||
            details == 0 ||
            !TryReadPtr(details + (ulong)_profile.Entity.LookupOffset, out var lookup) ||
            lookup == 0)
        {
            return false;
        }

        if (!TryReadArray(lookup + (ulong)_profile.Entity.ComponentMapOffset, out var mapHead, out var mapTail) ||
            !TryReadArray(player + (ulong)_profile.Entity.ComponentsOffset, out var componentHead, out var componentTail))
        {
            return false;
        }

        if (mapTail <= mapHead || componentTail <= componentHead)
        {
            return false;
        }

        var mapCount = (mapTail - mapHead) / ComponentMapEntrySize;
        var componentCount = (componentTail - componentHead) / 8;
        if (mapCount == 0 || mapCount > MaxComponents || componentCount == 0 || componentCount > MaxComponents)
        {
            return false;
        }

        for (var index = 0UL; index < mapCount; index++)
        {
            var entry = mapHead + index * ComponentMapEntrySize;
            if (!TryReadPtr(entry, out var namePointer) ||
                namePointer == 0 ||
                !TryReadInt32(entry + 8, out var componentIndex) ||
                componentIndex < 0 ||
                (ulong)componentIndex >= componentCount)
            {
                continue;
            }

            if (!TryReadString(namePointer, out var name) ||
                !string.Equals(name, "Life", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryReadPtr(componentHead + (ulong)componentIndex * 8, out life) || life == 0)
            {
                return false;
            }

            var vitals = ReadLife(life);
            return vitals is { IsSane: true };
        }

        return false;
    }

    private PlayerVitals? ReadLife(ulong life)
    {
        if (!TryReadInt32(life + (ulong)_profile.Life.CurHp, out var hp) ||
            !TryReadInt32(life + (ulong)_profile.Life.MaxHp, out var maxHp) ||
            !TryReadInt32(life + (ulong)_profile.Life.CurMp, out var mp) ||
            !TryReadInt32(life + (ulong)_profile.Life.MaxMp, out var maxMp) ||
            !TryReadInt32(life + (ulong)_profile.Life.CurEs, out var es) ||
            !TryReadInt32(life + (ulong)_profile.Life.MaxEs, out var maxEs))
        {
            return null;
        }

        var vitals = new PlayerVitals(hp, maxHp, es, maxEs, mp, maxMp);
        return vitals.IsSane ? vitals : null;
    }

    private bool TryReadArray(ulong address, out ulong head, out ulong tail)
    {
        head = 0;
        tail = 0;
        return TryReadPtr(address, out head) && TryReadPtr(address + 8, out tail);
    }

    private bool TryReadString(ulong address, out string value)
    {
        value = string.Empty;
        Span<byte> buffer = stackalloc byte[64];
        if (!_memory.TryRead(address, buffer))
        {
            return false;
        }

        var end = buffer.IndexOf((byte)0);
        if (end <= 0)
        {
            return false;
        }

        value = Encoding.ASCII.GetString(buffer[..end]);
        return true;
    }

    private ulong ReadPtr(ulong address)
    {
        if (!TryReadPtr(address, out var value))
        {
            throw new InvalidOperationException($"Failed to read pointer at 0x{address:X}.");
        }

        return value;
    }

    private int ReadInt32(ulong address)
    {
        if (!TryReadInt32(address, out var value))
        {
            throw new InvalidOperationException($"Failed to read int32 at 0x{address:X}.");
        }

        return value;
    }

    private bool TryReadPtr(ulong address, out ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        if (!_memory.TryRead(address, buffer))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        return value != 0;
    }

    private bool TryReadInt32(ulong address, out int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        if (!_memory.TryRead(address, buffer))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(buffer);
        return true;
    }

    private bool TryReadByte(ulong address, out byte value)
    {
        Span<byte> buffer = stackalloc byte[1];
        if (!_memory.TryRead(address, buffer))
        {
            value = 0;
            return false;
        }

        value = buffer[0];
        return true;
    }
}
