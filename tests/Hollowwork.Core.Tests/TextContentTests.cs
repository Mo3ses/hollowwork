using Hollowwork.Core.Protection;
using Hollowwork.Core.Text;

namespace Hollowwork.Core.Tests;

public class TextContentTests
{
    [Fact]
    public void Decodes_utf8_without_bom()
    {
        Assert.True(TextContent.TryDecode("fog"u8.ToArray(), out var text, out var encoding));
        Assert.Equal("fog", text);
        Assert.Equal(TextEncodingKind.Utf8, encoding);
    }

    [Fact]
    public void Detects_utf8_bom()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat("fog"u8.ToArray()).ToArray();
        Assert.True(TextContent.TryDecode(bytes, out var text, out var encoding));
        Assert.Equal("fog", text);
        Assert.Equal(TextEncodingKind.Utf8Bom, encoding);
    }

    [Fact]
    public void Detects_utf16le_bom()
    {
        var bytes = new byte[] { 0xFF, 0xFE }.Concat(System.Text.Encoding.Unicode.GetBytes("fog")).ToArray();
        Assert.True(TextContent.TryDecode(bytes, out var text, out var encoding));
        Assert.Equal("fog", text);
        Assert.Equal(TextEncodingKind.Utf16Le, encoding);
    }

    [Fact]
    public void Rejects_binary()
    {
        Assert.False(TextContent.TryDecode([0x80, 0x81, 0x82], out _, out _));
    }

    [Fact]
    public void Encoding_round_trips_utf16le_bom()
    {
        var bytes = TextContent.Encode("fog", TextEncodingKind.Utf16Le);
        Assert.Equal(new byte[] { 0xFF, 0xFE }, bytes.Take(2));
        Assert.Equal("fog", System.Text.Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
    }

    [Fact]
    public void Encoding_round_trips_utf8_bom()
    {
        var bytes = TextContent.Encode("fog", TextEncodingKind.Utf8Bom);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
    }
}

public class PathProtectionTests
{
    [Theory]
    [InlineData("metadata/environmentsettings/keep/a.env", "metadata/environmentsettings/keep", true)]
    [InlineData("metadata/environmentsettings/keep", "metadata/environmentsettings/keep", true)]
    [InlineData("metadata/environmentsettings/keeper/a.env", "metadata/environmentsettings/keep", false)]
    [InlineData("metadata/other/a.env", "metadata/environmentsettings/keep", false)]
    public void Matches_prefix_boundaries(string path, string root, bool expected)
    {
        Assert.Equal(expected, PathProtection.IsProtected(path, [root]));
    }
}
