using LibGGPK3;
using LibGGPK3.Records;

namespace Hollowwork.Core.Archives;

/// <summary>
/// GGPK session backed by LibGGPK3. Opening the file grants write access, so callers
/// must create a backup before writing.
/// </summary>
public sealed class GgpkArchiveSession : IArchiveSession
{
    private readonly GGPK _ggpk;

    public GgpkArchiveSession(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("GGPK not found.", filePath);
        }

        FilePath = Path.GetFullPath(filePath);
        _ggpk = new GGPK(FilePath);
    }

    public string FilePath { get; }

    public ArchiveKind Kind => ArchiveKind.Ggpk;

    public IReadOnlyList<string> ListFiles(string root, IReadOnlyCollection<string> extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        var normalizedRoot = NormalizePath(root);
        var directory = ResolveDirectory(normalizedRoot);
        if (directory is null)
        {
            return [];
        }

        var results = new List<string>();
        foreach (var (_, relative) in TreeNode.RecurseFiles(directory))
        {
            var path = Combine(normalizedRoot, relative);
            if (HasExtension(path, extensions))
            {
                results.Add(path);
            }
        }

        return results;
    }

    public bool TryRead(string path, out byte[] content)
    {
        if (FindFile(path) is { } file)
        {
            content = file.Read();
            return true;
        }

        content = [];
        return false;
    }

    public void Write(string path, ReadOnlySpan<byte> content)
    {
        var file = FindFile(path) ?? throw new FileNotFoundException($"Archive entry not found: {path}", path);
        file.Write(content, hash: null);
    }

    public void Flush() => _ggpk.Flush();

    public void Dispose() => _ggpk.Dispose();

    private DirectoryRecord? ResolveDirectory(string normalizedRoot)
    {
        if (normalizedRoot.Length == 0)
        {
            return _ggpk.Root;
        }

        return _ggpk.Root.TryFindNode(normalizedRoot, out var node) && node is DirectoryRecord directory
            ? directory
            : null;
    }

    private FileRecord? FindFile(string path)
    {
        var normalized = NormalizePath(path);
        return normalized.Length == 0
            ? null
            : _ggpk.Root.TryFindNode(normalized, out var node) ? node as FileRecord : null;
    }

    private static bool HasExtension(string path, IReadOnlyCollection<string> extensions)
    {
        var extension = Path.GetExtension(path);
        foreach (var candidate in extensions)
        {
            if (string.Equals(extension, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Combine(string root, string relative)
    {
        var child = NormalizePath(relative);
        if (root.Length == 0)
        {
            return child;
        }

        return child.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase)
            ? child
            : $"{root}/{child}";
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').Trim('/');
}
