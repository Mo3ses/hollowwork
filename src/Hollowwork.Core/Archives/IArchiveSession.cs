namespace Hollowwork.Core.Archives;

/// <summary>
/// Read/write facade over a game archive. Implementations own the underlying format;
/// the patch engine only sees paths and bytes.
/// </summary>
public interface IArchiveSession : IDisposable
{
    string FilePath { get; }

    ArchiveKind Kind { get; }

    /// <summary>Lists file paths under <paramref name="root"/> whose extension is in <paramref name="extensions"/>.</summary>
    IReadOnlyList<string> ListFiles(string root, IReadOnlyCollection<string> extensions);

    bool TryRead(string path, out byte[] content);

    void Write(string path, ReadOnlySpan<byte> content);

    void Flush();
}
