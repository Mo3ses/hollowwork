using Hollowwork.Core.Backup;

namespace Hollowwork.Core.Tests;

public class ArchiveBackupTests
{
    [Fact]
    public void Creates_verified_copy_and_manifest()
    {
        var root = Path.Combine(Path.GetTempPath(), $"hollowwork-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var archive = Path.Combine(root, "Content.ggpk");
            File.WriteAllBytes(archive, "GGPK"u8.ToArray().Concat(new byte[128]).ToArray());

            var manifest = ArchiveBackup.Create(archive, Path.Combine(root, "backups"));

            Assert.True(File.Exists(manifest.BackupPath));
            Assert.Equal(ArchiveBackup.HashFile(archive), manifest.Sha256);
            Assert.Equal(new FileInfo(archive).Length, manifest.SizeBytes);
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(manifest.BackupPath)!, "manifest.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Restore_copies_the_backup_back()
    {
        var root = Path.Combine(Path.GetTempPath(), $"hollowwork-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var archive = Path.Combine(root, "Content.ggpk");
            var original = "GGPK"u8.ToArray().Concat(new byte[64]).ToArray();
            File.WriteAllBytes(archive, original);

            var manifest = ArchiveBackup.Create(archive, Path.Combine(root, "backups"));
            File.WriteAllBytes(archive, "corrupted"u8.ToArray());

            ArchiveBackup.Restore(manifest, archive);

            Assert.Equal(original, File.ReadAllBytes(archive));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Refuses_missing_archive()
    {
        var root = Path.Combine(Path.GetTempPath(), $"hollowwork-backup-{Guid.NewGuid():N}");
        try
        {
            Assert.Throws<FileNotFoundException>(() =>
                ArchiveBackup.Create(Path.Combine(root, "missing.ggpk"), root));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
