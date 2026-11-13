using Hollowwork.Core.Protection;
using Hollowwork.Core.Text;

namespace Hollowwork.Core.Tests;

public class TextContentTests
{
    [Fact]
    public void Decodes_utf8_without_bom()
    {
        Assert.True(TextContent.TryDecode("fog"u8.ToArray(), out var text, out var hasBom));
        Assert.Equal("fog", text);
        Assert.False(hasBom);
    }

    [Fact]
    public void Detects_bom_and_strips_it_from_text()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat("fog"u8.ToArray()).ToArray();
        Assert.True(TextContent.TryDecode(bytes, out var text, out var hasBom));
        Assert.Equal("fog", text);
        Assert.True(hasBom);
    }

    [Fact]
    public void Rejects_binary()
    {
        Assert.False(TextContent.TryDecode([0xFF, 0xFE, 0x00], out _, out _));
    }

    [Fact]
    public void Encoding_round_trips_bom()
    {
        var bytes = TextContent.Encode("fog", hasBom: true);
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
