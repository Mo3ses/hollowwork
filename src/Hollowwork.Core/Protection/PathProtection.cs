namespace Hollowwork.Core.Protection;

/// <summary>Keeps configured path prefixes out of every patch.</summary>
public static class PathProtection
{
    public static string Normalize(string path) =>
        path.Replace('\\', '/').Trim('/').ToLowerInvariant();

    public static bool IsProtected(string path, IEnumerable<string> protectedRoots)
    {
        var normalized = Normalize(path);
        foreach (var root in protectedRoots)
        {
            var prefix = Normalize(root);
            if (prefix.Length == 0)
            {
                continue;
            }

            if (normalized.Equals(prefix, StringComparison.Ordinal)
                || normalized.StartsWith(prefix + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
