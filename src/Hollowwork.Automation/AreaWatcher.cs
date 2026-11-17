using System.Text;

namespace Hollowwork.Automation;

public enum AreaKind
{
    Unknown,
    Town,
    Hideout,
    Map,
}

/// <summary>What the automation needs to know about the current zone.</summary>
public interface IAreaProbe
{
    AreaKind Kind { get; }

    string? Area { get; }

    bool IsTransitioning { get; }

    bool IsAvailable { get; }

    string? Source { get; }

    string? Error { get; }

    void Poll();
}

/// <summary>
/// Follows <c>Client.txt</c> for the zone the player is in. The game appends
/// <c>: You have entered &lt;area&gt;.</c> on every zone change, which survives game patches
/// without new memory offsets.
/// </summary>
public sealed class AreaWatcher : IAreaProbe, IDisposable
{
    // Town and hub areas where pressing a flask has no purpose.
    private static readonly HashSet<string> Towns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Lioneye's Watch",
        "The Forest Encampment",
        "The Sarn Encampment",
        "Highgate",
        "The Overseer's Tower",
        "The Bridge Encampment",
        "Oriath Docks",
        "Karui Shores",
        "Aspirants' Plaza",
        "The Rogue Harbour",
    };

    private const string EnteredMarker = "] : You have entered ";
    private const string ConnectingMarker = "Connecting to instance server";
    private const string DisconnectedMarker = "You have been disconnected";
    private const int HeadLength = 64;

    private readonly string? _configuredPath;
    private readonly StringBuilder _pending = new();
    private FileStream? _stream;
    private string? _path;
    private string _head = string.Empty;
    private long _position;

    public AreaWatcher(string? clientLogPath = null)
    {
        _configuredPath = string.IsNullOrWhiteSpace(clientLogPath) ? null : clientLogPath;
    }

    public AreaKind Kind { get; private set; } = AreaKind.Unknown;

    public string? Area { get; private set; }

    public bool IsTransitioning { get; private set; }

    public bool IsAvailable => _stream is not null;

    public string? Source { get; private set; }

    public string? Error { get; private set; }

    /// <summary>Path used by the watcher: the configured one or the discovered one.</summary>
    public string? ResolvedPath => Source;

    public void Poll()
    {
        if (_stream is null && !Open(fromEnd: true))
        {
            return;
        }

        try
        {
            // FileStream caches its own length, and the game may replace the file while we hold
            // it. Ask the filesystem, then compare the first bytes: a shrunken or rewritten log
            // means a new session, so start over.
            var length = new FileInfo(_path!).Length;
            if (length < _position || !HeadMatches())
            {
                if (!Open(fromEnd: false))
                {
                    return;
                }

                length = new FileInfo(_path!).Length;
            }

            if (length == _position)
            {
                return;
            }

            _stream.Seek(_position, SeekOrigin.Begin);
            var buffer = new byte[length - _position];
            var read = _stream.Read(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                return;
            }

            _position += read;
            _pending.Append(Encoding.UTF8.GetString(buffer, 0, read));
            ConsumeLines();
        }
        catch (FileNotFoundException)
        {
            _stream?.Dispose();
            _stream = null;
            _position = 0;
            _pending.Clear();
        }
        catch (IOException exception)
        {
            Error = exception.Message;
        }
        catch (UnauthorizedAccessException exception)
        {
            Error = exception.Message;
        }
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _stream = null;
    }

    /// <summary>Area name from a <c>Client.txt</c> line, or null when the line is not an entry.</summary>
    public static string? ParseArea(string line)
    {
        var index = line.IndexOf(EnteredMarker, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var rest = line[(index + EnteredMarker.Length)..].TrimEnd();
        if (!rest.EndsWith('.'))
        {
            return null;
        }

        var area = rest[..^1].Trim();
        return area.Length == 0 ? null : area;
    }

    public static AreaKind Classify(string? area)
    {
        if (string.IsNullOrWhiteSpace(area))
        {
            return AreaKind.Unknown;
        }

        if (area.EndsWith("Hideout", StringComparison.OrdinalIgnoreCase))
        {
            return AreaKind.Hideout;
        }

        return Towns.Contains(area) ? AreaKind.Town : AreaKind.Map;
    }

    /// <summary>First existing <c>Client.txt</c> for a Steam/Proton or Wine install.</summary>
    public static string? Discover()
    {
        foreach (var candidate in Candidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> Candidates()
    {
        const string suffix = "drive_c/users";
        const string tail = "Documents/My Games/Path of Exile/logs/Client.txt";
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        foreach (var library in SteamLibraries(home))
        {
            // The client writes its log next to the game files, inside the install directory.
            yield return Path.Combine(library, "steamapps", "common", "Path of Exile", "logs", "Client.txt");

            var users = Path.Combine(library, "steamapps", "compatdata", "238960", "pfx", suffix);
            if (!Directory.Exists(users))
            {
                continue;
            }

            foreach (var user in Directory.EnumerateDirectories(users))
            {
                yield return Path.Combine(user, tail);
            }
        }

        var wineUsers = Path.Combine(home, ".wine", suffix);
        if (Directory.Exists(wineUsers))
        {
            foreach (var user in Directory.EnumerateDirectories(wineUsers))
            {
                yield return Path.Combine(user, tail);
            }
        }

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrEmpty(documents))
        {
            yield return Path.Combine(documents, "My Games", "Path of Exile", "logs", "Client.txt");
        }
    }

    private static IEnumerable<string> SteamLibraries(string home)
    {
        var roots = new[]
        {
            Path.Combine(home, ".steam", "steam"),
            Path.Combine(home, ".local", "share", "Steam"),
            Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"),
        };

        foreach (var root in roots)
        {
            yield return root;

            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
            {
                continue;
            }

            string text;
            try
            {
                text = File.ReadAllText(vdf);
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var library in ReadLibraryPaths(text))
            {
                yield return library;
            }
        }
    }

    private static IEnumerable<string> ReadLibraryPaths(string vdf)
    {
        const string key = "\"path\"";
        var index = 0;

        while (true)
        {
            index = vdf.IndexOf(key, index, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                yield break;
            }

            index += key.Length;
            var start = vdf.IndexOf('"', index);
            if (start < 0)
            {
                yield break;
            }

            var end = vdf.IndexOf('"', start + 1);
            if (end < 0)
            {
                yield break;
            }

            var path = vdf[(start + 1)..end].Replace("\\\\", "\\", StringComparison.Ordinal);
            if (path.Length > 0)
            {
                yield return path;
            }

            index = end + 1;
        }
    }

    private bool Open(bool fromEnd)
    {
        var path = _configuredPath ?? Discover();
        if (path is null)
        {
            Error = "Client.txt not found. Set the path or area pause stays off.";
            return false;
        }

        _stream?.Dispose();

        try
        {
            _stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                FileOptions.None);

            // Only new lines matter: start at the end so a restart does not replay history.
            _path = path;
            _pending.Clear();
            _head = ReadHead();
            _position = fromEnd ? _stream.Length : 0;
            Source = path;
            Error = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Error = exception.Message;
            return false;
        }
    }

    private string ReadHead()
    {
        if (_stream is null)
        {
            return string.Empty;
        }

        var count = (int)Math.Min(HeadLength, _stream.Length);
        if (count == 0)
        {
            return string.Empty;
        }

        var buffer = new byte[count];
        _stream.Seek(0, SeekOrigin.Begin);
        var read = _stream.Read(buffer, 0, count);
        return Encoding.UTF8.GetString(buffer, 0, read);
    }

    private bool HeadMatches()
    {
        if (_stream is null)
        {
            return false;
        }

        var count = (int)Math.Min(HeadLength, new FileInfo(_path!).Length);
        if (count == 0)
        {
            return _head.Length == 0;
        }

        var buffer = new byte[count];
        _stream.Seek(0, SeekOrigin.Begin);
        var read = _stream.Read(buffer, 0, count);
        return Encoding.UTF8.GetString(buffer, 0, read) == _head;
    }

    private void ConsumeLines()
    {
        while (true)
        {
            var text = _pending.ToString();
            var newline = text.IndexOf('\n', StringComparison.Ordinal);
            if (newline < 0)
            {
                return;
            }

            var line = text[..newline].TrimEnd('\r');
            _pending.Remove(0, newline + 1);
            ProcessLine(line);
        }
    }

    private void ProcessLine(string line)
    {
        var area = ParseArea(line);
        if (area is not null)
        {
            Area = area;
            Kind = Classify(area);
            IsTransitioning = false;
            return;
        }

        if (line.Contains(ConnectingMarker, StringComparison.Ordinal) ||
            line.Contains(DisconnectedMarker, StringComparison.Ordinal))
        {
            IsTransitioning = true;
        }
    }
}
