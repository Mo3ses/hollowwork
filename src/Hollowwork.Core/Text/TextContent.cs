using System.Text;

namespace Hollowwork.Core.Text;

/// <summary>UTF-8 decode/encode that preserves a leading BOM and rejects binary files.</summary>
public static class TextContent
{
    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static bool TryDecode(byte[] bytes, out string text, out bool hasBom)
    {
        hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var offset = hasBom ? 3 : 0;

        try
        {
            text = Strict.GetString(bytes, offset, bytes.Length - offset);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            hasBom = false;
            return false;
        }
    }

    public static byte[] Encode(string text, bool hasBom)
    {
        var body = Strict.GetBytes(text);
        if (!hasBom)
        {
            return body;
        }

        var result = new byte[body.Length + 3];
        result[0] = 0xEF;
        result[1] = 0xBB;
        result[2] = 0xBF;
        body.CopyTo(result, 3);
        return result;
    }
}
