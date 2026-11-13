namespace Hollowwork.Core.Archives;

public sealed record ArchiveSessionOptions(string? OodleLibraryPath = null);

public static class ArchiveSessionFactory
{
    /// <summary>
    /// Opens a GGPK file or a Bundles2 directory/index. Never writes until
    /// <see cref="IArchiveSession.Write"/> is called explicitly.
    /// </summary>
    public static IArchiveSession Open(string path, ArchiveSessionOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var kind = ArchiveProbe.DetectKind(path);
        return kind switch
        {
            ArchiveKind.Ggpk => new GgpkArchiveSession(path),
            ArchiveKind.BundleIndex => new BundleArchiveSession(path, options?.OodleLibraryPath),
            _ => throw new NotSupportedException($"Unsupported archive format: {path}"),
        };
    }
}
