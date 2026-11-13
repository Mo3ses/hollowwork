using System.Text.RegularExpressions;

namespace Hollowwork.Patches;

public static class GlobMatcher
{
    public static bool IsMatch(string path, IEnumerable<string>? patterns)
    {
        if (patterns is null)
        {
            return false;
        }

        foreach (var pattern in patterns)
        {
            if (Matches(path, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Matches(string path, string pattern)
    {
        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(path, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
