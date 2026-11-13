namespace Hollowwork.Core.Archives;

public enum ArchiveKind
{
    Unknown = 0,
    Ggpk = 1,
    BundleIndex = 2,
}

public sealed record ArchiveInfo(string Path, ArchiveKind Kind, long SizeBytes);

/// <summary>
/// Cheap, read-only archive detection. Full parsing is delegated to LibGGPK3
/// once a session is opened; this type never writes to the archive.
/// </summary>
public static class ArchiveProbe
{
    public static ArchiveInfo Inspect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (Directory.Exists(path))
        {
            var index = System.IO.Path.Combine(path, "_.index.bin");
            if (!File.Exists(index))
            {
                throw new FileNotFoundException("_.index.bin not found in the Bundles2 directory.", index);
            }

            return new ArchiveInfo(System.IO.Path.GetFullPath(index), ArchiveKind.BundleIndex, new FileInfo(index).Length);
        }

        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Archive not found.", path);
        }

        return new ArchiveInfo(System.IO.Path.GetFullPath(path), DetectKind(path), info.Length);
    }

    public static ArchiveKind DetectKind(string path)
    {
        if (Directory.Exists(path))
        {
            return File.Exists(System.IO.Path.Combine(path, "_.index.bin"))
                ? ArchiveKind.BundleIndex
                : ArchiveKind.Unknown;
        }

        using var stream = File.OpenRead(path);

        Span<byte> magic = stackalloc byte[4];
        var read = stream.Read(magic);
        if (read == 4 && magic.SequenceEqual("GGPK"u8))
        {
            return ArchiveKind.Ggpk;
        }

        var name = System.IO.Path.GetFileName(path);
        return name.Equals("_.index.bin", StringComparison.OrdinalIgnoreCase)
            ? ArchiveKind.BundleIndex
            : ArchiveKind.Unknown;
    }
}
