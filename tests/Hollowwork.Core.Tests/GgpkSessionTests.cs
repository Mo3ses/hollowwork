using System.Text;
using Hollowwork.Core.Archives;
using Hollowwork.Core.Backup;
using Hollowwork.Core.Patches;
using Hollowwork.Patches;

namespace Hollowwork.Core.Tests;

public class GgpkSessionTests
{
    [Fact]
    public void Reads_lists_and_writes_a_real_ggpk()
    {
        var root = NewTempDirectory();
        try
        {
            var path = Path.Combine(root, "Content.ggpk");
            GgpkFixture.Create(path, new Dictionary<string, byte[]>
            {
                ["metadata/environmentsettings/a.env"] = "fog"u8.ToArray(),
                ["art/textures/b.dds"] = [1, 2, 3, 4],
            });

            using (var session = new GgpkArchiveSession(path))
            {
                Assert.Equal(ArchiveKind.Ggpk, session.Kind);
                Assert.Equal(["metadata/environmentsettings/a.env"], session.ListFiles("metadata/environmentsettings", [".env"]));
                Assert.True(session.TryRead("metadata/environmentsettings/a.env", out var bytes));
                Assert.Equal("fog", Encoding.UTF8.GetString(bytes));

                session.Write("metadata/environmentsettings/a.env", "xog"u8);
                session.Flush();
            }

            using (var reopened = new GgpkArchiveSession(path))
            {
                Assert.True(reopened.TryRead("metadata/environmentsettings/a.env", out var bytes));
                Assert.Equal("xog", Encoding.UTF8.GetString(bytes));
                Assert.True(reopened.TryRead("art/textures/b.dds", out var texture));
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, texture);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Applies_a_patch_to_a_real_ggpk_with_backup()
    {
        var root = NewTempDirectory();
        try
        {
            var path = Path.Combine(root, "Content.ggpk");
            GgpkFixture.Create(path, new Dictionary<string, byte[]>
            {
                ["metadata/environmentsettings/a.env"] = "{ \"fog\": true }"u8.ToArray(),
            });

            var patch = new PatchDefinition(
                "poe1.fog-off",
                "Fog off",
                "test",
                "poe1",
                [new PatchTarget("metadata/environmentsettings", [".env"])],
                [new PatchOperation("replace", "fog", "xog")]);

            var backup = ArchiveBackup.Create(path, Path.Combine(root, "backups"));

            using (var session = new GgpkArchiveSession(path))
            {
                var plan = new PatchPlanner().Plan(session, [patch]);
                Assert.Single(plan.Files);
                new PatchApplier().Apply(session, plan);
            }

            using (var reopened = new GgpkArchiveSession(path))
            {
                Assert.True(reopened.TryRead("metadata/environmentsettings/a.env", out var bytes));
                Assert.Equal("{ \"xog\": true }", Encoding.UTF8.GetString(bytes));
            }

            ArchiveBackup.Restore(backup, path);
            using (var restored = new GgpkArchiveSession(path))
            {
                Assert.True(restored.TryRead("metadata/environmentsettings/a.env", out var bytes));
                Assert.Equal("{ \"fog\": true }", Encoding.UTF8.GetString(bytes));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hollowwork-ggpk-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
