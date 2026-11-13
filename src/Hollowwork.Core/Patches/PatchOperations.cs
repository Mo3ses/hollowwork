using System.Text.RegularExpressions;
using Hollowwork.Patches;

namespace Hollowwork.Core.Patches;

public sealed record OperationResult(string Text, int Changes);

/// <summary>Pure text operations. No I/O, no archive knowledge.</summary>
public static class PatchOperations
{
    public static OperationResult Apply(string text, PatchOperation operation) => operation.Type switch
    {
        "replace" => Replace(text, operation.From, operation.To),
        "replaceExtension" => Replace(text, operation.From, operation.To),
        "setBool" => SetBool(text, operation.From, operation.To),
        "stripBlock" => StripBlock(text, operation.From),
        "removeLines" => RemoveLines(text, operation.From),
        _ => throw new PatchValidationException($"Unknown operation '{operation.Type}'."),
    };

    public static OperationResult Replace(string text, string? from, string? to)
    {
        if (string.IsNullOrEmpty(from))
        {
            throw new PatchValidationException("replace requires 'from'.");
        }

        to ??= string.Empty;
        var count = CountOccurrences(text, from);
        return count == 0
            ? new OperationResult(text, 0)
            : new OperationResult(text.Replace(from, to, StringComparison.Ordinal), count);
    }

    public static OperationResult SetBool(string text, string? key, string? value)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new PatchValidationException("setBool requires 'from' as the key.");
        }

        if (value is not ("true" or "false"))
        {
            throw new PatchValidationException("setBool requires 'to' to be 'true' or 'false'.");
        }

        var pattern = $"(\"{Regex.Escape(key)}\"\\s*:\\s*)(true|false)";
        var count = 0;
        var result = Regex.Replace(text, pattern, match =>
        {
            if (match.Groups[2].Value == value)
            {
                return match.Value;
            }

            count++;
            return match.Groups[1].Value + value;
        });

        return new OperationResult(result, count);
    }

    public static OperationResult StripBlock(string text, string? blockName)
    {
        if (string.IsNullOrWhiteSpace(blockName))
        {
            throw new PatchValidationException("stripBlock requires 'from' as the block name.");
        }

        var count = 0;
        var searchFrom = 0;

        while (true)
        {
            var open = FindBlockOpen(text, blockName, searchFrom);
            if (open < 0)
            {
                break;
            }

            var close = FindMatchingBrace(text, open);
            if (close < 0)
            {
                break;
            }

            var inner = text[(open + 1)..close];
            if (inner.Length > 0)
            {
                count++;
                text = text[..(open + 1)] + text[close..];
            }

            searchFrom = open + 1;
        }

        return new OperationResult(text, count);
    }

    public static OperationResult RemoveLines(string text, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            throw new PatchValidationException("removeLines requires 'from' as a regex.");
        }

        var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(2));
        var lines = text.Split('\n');
        var kept = new List<string>(lines.Length);
        var removed = 0;

        foreach (var line in lines)
        {
            if (regex.IsMatch(line.TrimEnd('\r')))
            {
                removed++;
                continue;
            }

            kept.Add(line);
        }

        return removed == 0
            ? new OperationResult(text, 0)
            : new OperationResult(string.Join('\n', kept), removed);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static int FindBlockOpen(string text, string name, int start)
    {
        var index = start;
        while ((index = text.IndexOf(name, index, StringComparison.Ordinal)) >= 0)
        {
            var after = index + name.Length;
            var leftOk = index == 0 || !IsIdentifierChar(text[index - 1]);
            var rightOk = after >= text.Length || !IsIdentifierChar(text[after]);

            if (leftOk && rightOk)
            {
                var cursor = after;
                while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
                {
                    cursor++;
                }

                if (cursor < text.Length && text[cursor] == '{')
                {
                    return cursor;
                }
            }

            index = after;
        }

        return -1;
    }

    private static int FindMatchingBrace(string text, int openIndex)
    {
        var depth = 0;
        var quote = '\0';
        var escaped = false;

        for (var i = openIndex; i < text.Length; i++)
        {
            var c = text[i];

            if (quote != '\0')
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            switch (c)
            {
                case '"' or '\'':
                    quote = c;
                    break;
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }

                    break;
            }
        }

        return -1;
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
