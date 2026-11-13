using Hollowwork.Core.Oodle;
using LibBundle3;
using LibBundle3.Records;

namespace Hollowwork.Core.Archives;

/// <summary>
/// Steam bundle archive (Bundles2). Requires a user-provided Oodle library for
/// reading or writing compressed bundles.
/// </summary>
public sealed class BundleArchiveSession : IArchiveSession
{
    private readonly LibBundle3.Index _index;
    private string[]? _paths;

    public BundleArchiveSession(string path, string? oodleLibraryPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        OodleLibrary.Configure(oodleLibraryPath);
        OodleLibrary.EnsureAvailable();

        var (indexPath, bundlesDirectory) = Resolve(path);
        FilePath = indexPath;
        BundlesDirectory = bundlesDirectory;

        try
        {
            _index = new LibBundle3.Index(indexPath, parsePaths: true, new DriveBundleFactory(bundlesDirectory));
        }
        catch (DllNotFoundException exception)
        {
            throw new OodleLibraryNotFoundException(
                "Oodle library failed to load. Set HOLLOWWORK_OODLE or place liboo2corelinux64.so.9 next to the executable.",
                exception);
        }
    }

    public string FilePath { get; }

    public string BundlesDirectory { get; }

    public ArchiveKind Kind => ArchiveKind.BundleIndex;

    public IReadOnlyList<string> ListFiles(string root, IReadOnlyCollection<string> extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        _paths ??= _index.Files.Values
            .Select(record => record.Path)
            .Where(path => !string.IsNullOrEmpty(path))
            .ToArray()!;

        var normalizedRoot = Normalize(root);
        var results = new List<string>();

        foreach (var path in _paths)
        {
            if (normalizedRoot.Length > 0
                && !path.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var extension = Path.GetExtension(path);
            foreach (var candidate in extensions)
            {
                if (string.Equals(extension, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(path);
                    break;
                }
            }
        }

        return results;
    }

    public bool TryRead(string path, out byte[] content)
    {
        var normalized = Normalize(path);
        if (_index.TryGetFile(normalized, out var record))
        {
            content = record.Read().ToArray();
            return true;
        }

        content = [];
        return false;
    }

    public void Write(string path, ReadOnlySpan<byte> content)
    {
        var normalized = Normalize(path);
        if (!_index.TryGetFile(normalized, out var record))
        {
            throw new FileNotFoundException($"Archive entry not found: {path}", path);
        }

        record.Write(content, saveIndex: false);
    }

    public void Flush() => _index.Save();

    public void Dispose() => _index.Dispose();

    private static (string IndexPath, string BundlesDirectory) Resolve(string path)
    {
        if (Directory.Exists(path))
        {
            var index = Path.Combine(path, "_.index.bin");
            if (!File.Exists(index))
            {
                throw new FileNotFoundException("_.index.bin not found in the Bundles2 directory.", index);
            }

            return (Path.GetFullPath(index), Path.GetFullPath(path));
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Unable to resolve the bundle directory.", nameof(path));

        return (fullPath, directory);
    }

    private static string Normalize(string path) => path.Replace('\\', '/').Trim('/');
}
