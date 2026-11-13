using System.Security.Cryptography;
using System.Text.Json;

namespace Hollowwork.Core.Backup;

public sealed record BundleBackupManifest(
    string Kind,
    string Source,
    DateTimeOffset CreatedAt,
    string IndexSha256,
    IReadOnlyDictionary<string, string> Files);

/// <summary>
/// Backups for Bundles2: the index plus the custom <c>LibGGPK3/</c> bundles it points to.
/// Restore is hash-verified and snapshots the current state before replacing anything.
/// </summary>
public static class BundleArchiveBackup
{
    private const string ManifestName = "manifest.json";
    private const string CustomBundleDirectory = "LibGGPK3";

    public static string Create(string bundlesDirectory, string backupRoot, DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundlesDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupRoot);

        var source = Path.GetFullPath(bundlesDirectory);
        var index = Path.Combine(source, "_.index.bin");
        if (!File.Exists(index))
        {
            throw new FileNotFoundException("_.index.bin not found.", index);
        }

        var createdAt = now ?? DateTimeOffset.UtcNow;
        var folder = Path.Combine(backupRoot, $"{createdAt:yyyyMMdd-HHmmss-fff}-bundles");
        Directory.CreateDirectory(folder);

        File.Copy(index, Path.Combine(folder, "_.index.bin"), overwrite: false);

        var custom = Path.Combine(source, CustomBundleDirectory);
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Directory.Exists(custom))
        {
            foreach (var file in Directory.EnumerateFiles(custom, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file).Replace('\\', '/');
                var destination = Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: false);
                files[relative] = HashFile(destination);
            }
        }

        var manifest = new BundleBackupManifest(
            "bundles2",
            source,
            createdAt,
            HashFile(Path.Combine(folder, "_.index.bin")),
            files);

        File.WriteAllText(Path.Combine(folder, ManifestName), JsonSerializer.Serialize(manifest, JsonOptions));
        return folder;
    }

    /// <summary>Restores a backup and returns the snapshot directory of the previous state.</summary>
    public static string Restore(string backupDirectory, string bundlesDirectory)
    {
        var manifestPath = Path.Combine(backupDirectory, ManifestName);
        var manifest = JsonSerializer.Deserialize<BundleBackupManifest>(File.ReadAllText(manifestPath), JsonOptions)
            ?? throw new InvalidDataException("Invalid bundle backup manifest.");

        if (!string.Equals(manifest.Kind, "bundles2", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unexpected backup kind '{manifest.Kind}'.");
        }

        var indexCopy = Path.Combine(backupDirectory, "_.index.bin");
        if (!File.Exists(indexCopy) || HashFile(indexCopy) != manifest.IndexSha256)
        {
            throw new InvalidDataException("Backup index is missing or corrupted.");
        }

        foreach (var (relative, hash) in manifest.Files)
        {
            var file = Path.Combine(backupDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file) || HashFile(file) != hash)
            {
                throw new InvalidDataException($"Backup file is missing or corrupted: {relative}");
            }
        }

        var target = Path.GetFullPath(bundlesDirectory);
        var snapshot = Path.Combine(
            backupDirectory,
            $"pre-restore-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}");
        Directory.CreateDirectory(snapshot);

        var currentIndex = Path.Combine(target, "_.index.bin");
        if (File.Exists(currentIndex))
        {
            File.Copy(currentIndex, Path.Combine(snapshot, "_.index.bin"), overwrite: true);
        }

        var currentCustom = Path.Combine(target, CustomBundleDirectory);
        if (Directory.Exists(currentCustom))
        {
            Directory.Move(currentCustom, Path.Combine(snapshot, CustomBundleDirectory));
        }

        File.Copy(indexCopy, currentIndex, overwrite: true);

        var backupCustom = Path.Combine(backupDirectory, CustomBundleDirectory);
        if (Directory.Exists(backupCustom))
        {
            CopyDirectory(backupCustom, currentCustom);
        }

        return snapshot;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };
}
