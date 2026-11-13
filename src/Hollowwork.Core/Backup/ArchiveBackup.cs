using System.Security.Cryptography;
using System.Text.Json;

namespace Hollowwork.Core.Backup;

public sealed record BackupManifest(
    string SourcePath,
    string BackupPath,
    string Sha256,
    long SizeBytes,
    DateTimeOffset CreatedAt);

/// <summary>Hash-verified copy of an archive, created before any write.</summary>
public static class ArchiveBackup
{
    private const string ManifestName = "manifest.json";

    public static BackupManifest Create(string archivePath, string backupRoot, DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupRoot);

        var source = Path.GetFullPath(archivePath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("Archive not found.", source);
        }

        var createdAt = now ?? DateTimeOffset.UtcNow;
        var folder = Path.Combine(backupRoot, createdAt.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);

        var destination = Path.Combine(folder, Path.GetFileName(source));
        File.Copy(source, destination, overwrite: false);

        var sourceHash = HashFile(source);
        var destinationHash = HashFile(destination);
        if (!string.Equals(sourceHash, destinationHash, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(destination);
            throw new IOException("Backup verification failed: source and copy hashes differ.");
        }

        var manifest = new BackupManifest(source, destination, sourceHash, new FileInfo(source).Length, createdAt);
        File.WriteAllText(Path.Combine(folder, ManifestName), JsonSerializer.Serialize(manifest, JsonOptions));

        return manifest;
    }

    public static void Restore(BackupManifest manifest, string archivePath)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        if (!File.Exists(manifest.BackupPath))
        {
            throw new FileNotFoundException("Backup file not found.", manifest.BackupPath);
        }

        var backupHash = HashFile(manifest.BackupPath);
        if (!string.Equals(backupHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Backup is corrupted: hash does not match the manifest.");
        }

        File.Copy(manifest.BackupPath, archivePath, overwrite: true);
    }

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
