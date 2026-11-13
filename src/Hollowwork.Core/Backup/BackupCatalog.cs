using System.Text.Json;

namespace Hollowwork.Core.Backup;

public sealed record BackupEntry(
    string Directory,
    string Kind,
    string Source,
    DateTimeOffset CreatedAt,
    long SizeBytes)
{
    public string Display => $"{CreatedAt:yyyy-MM-dd HH:mm:ss} · {Kind} · {SizeBytes / 1024d / 1024d:F1} MB";
}

/// <summary>Reads and restores the backup folders created by Hollowwork.</summary>
public static class BackupCatalog
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<BackupEntry> List(string backupRoot)
    {
        if (!Directory.Exists(backupRoot))
        {
            return [];
        }

        var entries = new List<BackupEntry>();
        foreach (var directory in Directory.EnumerateDirectories(backupRoot))
        {
            var manifestPath = Path.Combine(directory, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
                var root = document.RootElement;

                if (TryGet(root, "kind", out var kind)
                    && string.Equals(kind.GetString(), "bundles2", StringComparison.OrdinalIgnoreCase))
                {
                    var manifest = JsonSerializer.Deserialize<BundleBackupManifest>(root.GetRawText(), Options)!;
                    entries.Add(new BackupEntry(directory, "bundles2", manifest.Source, manifest.CreatedAt, SizeOf(directory)));
                }
                else if (TryGet(root, "sourcePath", out var sourcePath))
                {
                    var manifest = JsonSerializer.Deserialize<BackupManifest>(root.GetRawText(), Options)!;
                    var source = sourcePath.GetString() ?? "";
                    var legacyKind = Path.GetFileName(source).Equals("_.index.bin", StringComparison.OrdinalIgnoreCase)
                        ? "index-only"
                        : "ggpk";
                    entries.Add(new BackupEntry(directory, legacyKind, source, manifest.CreatedAt, manifest.SizeBytes));
                }
            }
            catch
            {
                // Ignore malformed or foreign folders.
            }
        }

        return entries.OrderByDescending(entry => entry.CreatedAt).ToList();
    }

    public static string Restore(BackupEntry entry, string target)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Kind == "bundles2")
        {
            return BundleArchiveBackup.Restore(entry.Directory, target);
        }

        if (entry.Kind == "index-only")
        {
            throw new InvalidOperationException(
                "Legacy index-only backup: it does not include LibGGPK3 and cannot be restored safely.");
        }

        var manifest = JsonSerializer.Deserialize<BackupManifest>(
            File.ReadAllText(Path.Combine(entry.Directory, "manifest.json")), Options)
            ?? throw new InvalidDataException("Invalid backup manifest.");

        var backupFile = Path.Combine(entry.Directory, Path.GetFileName(manifest.BackupPath));
        ArchiveBackup.Restore(manifest with { BackupPath = backupFile }, target);
        return string.Empty;
    }

    private static bool TryGet(JsonElement root, string name, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static long SizeOf(string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length);
}
