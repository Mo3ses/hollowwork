using System.Text;

namespace Hollowwork.Core.Text;

public enum TextEncodingKind
{
    Utf8,
    Utf8Bom,
    Utf16Le,
    Utf16Be,
}

/// <summary>Decode/encode text files preserving their original encoding and BOM.</summary>
public static class TextContent
{
    private static readonly UTF8Encoding Utf8Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static bool TryDecode(byte[] bytes, out string text, out TextEncodingKind encoding)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            encoding = TextEncodingKind.Utf8Bom;
            return TryDecodeWith(Utf8Strict, bytes, 3, out text);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encoding = TextEncodingKind.Utf16Le;
            return TryDecodeWith(Encoding.Unicode, bytes, 2, out text);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encoding = TextEncodingKind.Utf16Be;
            return TryDecodeWith(Encoding.BigEndianUnicode, bytes, 2, out text);
        }

        encoding = TextEncodingKind.Utf8;
        return TryDecodeWith(Utf8Strict, bytes, 0, out text);
    }

    public static byte[] Encode(string text, TextEncodingKind encoding) => encoding switch
    {
        TextEncodingKind.Utf8Bom => [0xEF, 0xBB, 0xBF, .. Utf8Strict.GetBytes(text)],
        TextEncodingKind.Utf16Le => [0xFF, 0xFE, .. Encoding.Unicode.GetBytes(text)],
        TextEncodingKind.Utf16Be => [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(text)],
        _ => Utf8Strict.GetBytes(text),
    };

    private static bool TryDecodeWith(Encoding encoding, byte[] bytes, int offset, out string text)
    {
        try
        {
            text = encoding.GetString(bytes, offset, bytes.Length - offset);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }
}
