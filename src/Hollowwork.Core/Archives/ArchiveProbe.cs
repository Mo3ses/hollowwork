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

        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Archive not found.", path);
        }

        return new ArchiveInfo(Path.GetFullPath(path), DetectKind(path), info.Length);
    }

    public static ArchiveKind DetectKind(string path)
    {
        using var stream = File.OpenRead(path);

        Span<byte> magic = stackalloc byte[4];
        var read = stream.Read(magic);
        if (read == 4 && magic.SequenceEqual("GGPK"u8))
        {
            return ArchiveKind.Ggpk;
        }

        var name = Path.GetFileName(path);
        return name.Equals("_.index.bin", StringComparison.OrdinalIgnoreCase)
            ? ArchiveKind.BundleIndex
            : ArchiveKind.Unknown;
    }
}
