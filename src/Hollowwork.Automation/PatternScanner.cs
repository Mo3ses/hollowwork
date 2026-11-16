namespace Hollowwork.Automation;

public static class PatternScanner
{
    public static int IndexOf(ReadOnlySpan<byte> data, ReadOnlySpan<byte> pattern)
    {
        if (pattern.Length == 0 || data.Length < pattern.Length)
        {
            return -1;
        }

        var first = pattern[0];
        var last = data.Length - pattern.Length;
        for (var index = 0; index <= last; index++)
        {
            if (data[index] == first &&
                data.Slice(index, pattern.Length).SequenceEqual(pattern))
            {
                return index;
            }
        }

        return -1;
    }
}
