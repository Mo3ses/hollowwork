namespace Hollowwork.Automation;

/// <summary>
/// One flask slot per key, in a comma-separated list. Each press takes the next key in order, so
/// two life flasks on <c>1,2</c> alternate instead of spending the same one twice.
/// </summary>
/// <remarks>
/// Charges are not read: the client ignores a press when the flask is empty, and the next key in
/// the list still gets its turn.
/// </remarks>
public sealed class FlaskChannel
{
    private static readonly char[] Separators = [',', ';', ' ', '\t'];

    private readonly string[] _keys;
    private int _cursor;

    public FlaskChannel(string? specification)
    {
        _keys = Parse(specification);
    }

    public bool IsEmpty => _keys.Length == 0;

    public int Count => _keys.Length;

    /// <summary>Last time any key of this channel was pressed.</summary>
    public DateTime LastPress { get; private set; } = DateTime.MinValue;

    public static string[] Parse(string? specification)
    {
        if (string.IsNullOrWhiteSpace(specification))
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var keys = new List<string>();
        foreach (var part in specification.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var key = part.Trim();
            if (key.Length > 0 && seen.Add(key))
            {
                keys.Add(key);
            }
        }

        return [.. keys];
    }

    /// <summary>Next key to press, or null while the cooldown has not elapsed.</summary>
    public string? Press(DateTime now, int cooldownMs)
    {
        if (_keys.Length == 0 || (now - LastPress).TotalMilliseconds < Math.Max(0, cooldownMs))
        {
            return null;
        }

        var key = _keys[_cursor];
        _cursor = (_cursor + 1) % _keys.Length;
        LastPress = now;
        return key;
    }
}
