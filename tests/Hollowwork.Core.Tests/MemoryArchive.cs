using System.Text;
using Hollowwork.Core.Archives;

namespace Hollowwork.Core.Tests;

internal sealed class MemoryArchive : IArchiveSession
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string FilePath { get; } = "memory.ggpk";

    public ArchiveKind Kind => ArchiveKind.Ggpk;

    public bool Disposed { get; private set; }

    public bool Flushed { get; private set; }

    public IReadOnlyList<string> ListFiles(string root, IReadOnlyCollection<string> extensions)
    {
        var prefix = root.Replace('\\', '/').Trim('/');
        return Files.Keys
            .Where(path => prefix.Length == 0 || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
            .Where(path => extensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
            .Order()
            .ToList();
    }

    public bool TryRead(string path, out byte[] content)
    {
        if (Files.TryGetValue(path, out var bytes))
        {
            content = bytes;
            return true;
        }

        content = [];
        return false;
    }

    public void Write(string path, ReadOnlySpan<byte> content)
    {
        Files[path] = content.ToArray();
    }

    public void Flush() => Flushed = true;

    public void Dispose() => Disposed = true;

    public void AddText(string path, string text) => Files[path] = Encoding.UTF8.GetBytes(text);
}
