using Hollowwork.Core.Backup;

namespace Hollowwork.Core.Tests;

public class BundleArchiveBackupTests
{
    [Fact]
    public void Creates_and_restores_index_and_custom_bundles()
    {
        var root = NewTempDirectory();
        try
        {
            var bundles = Path.Combine(root, "Bundles2");
            var custom = Path.Combine(bundles, "LibGGPK3");
            Directory.CreateDirectory(custom);
            File.WriteAllBytes(Path.Combine(bundles, "_.index.bin"), "index-v1"u8.ToArray());
            File.WriteAllBytes(Path.Combine(custom, "0.bundle.bin"), "bundle-v1"u8.ToArray());

            var backup = BundleArchiveBackup.Create(bundles, Path.Combine(root, "backups"));

            File.WriteAllBytes(Path.Combine(bundles, "_.index.bin"), "index-v2"u8.ToArray());
            File.WriteAllBytes(Path.Combine(custom, "0.bundle.bin"), "bundle-v2"u8.ToArray());

            var snapshot = BundleArchiveBackup.Restore(backup, bundles);

            Assert.Equal("index-v1"u8.ToArray(), File.ReadAllBytes(Path.Combine(bundles, "_.index.bin")));
            Assert.Equal("bundle-v1"u8.ToArray(), File.ReadAllBytes(Path.Combine(custom, "0.bundle.bin")));
            Assert.True(File.Exists(Path.Combine(snapshot, "_.index.bin")));
            Assert.Equal("bundle-v2"u8.ToArray(), File.ReadAllBytes(Path.Combine(snapshot, "LibGGPK3", "0.bundle.bin")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Catalog_lists_bundle_backups()
    {
        var root = NewTempDirectory();
        try
        {
            var bundles = Path.Combine(root, "Bundles2");
            Directory.CreateDirectory(bundles);
            File.WriteAllBytes(Path.Combine(bundles, "_.index.bin"), "index"u8.ToArray());

            var backupRoot = Path.Combine(root, "backups");
            BundleArchiveBackup.Create(bundles, backupRoot);

            var entries = BackupCatalog.List(backupRoot);

            var entry = Assert.Single(entries);
            Assert.Equal("bundles2", entry.Kind);
            Assert.Equal(Path.GetFullPath(bundles), entry.Source);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hollowwork-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
