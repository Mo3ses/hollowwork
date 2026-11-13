using Hollowwork.Patches;

namespace Hollowwork.Core.Tests;

public class ShaderColorTests
{
    [Fact]
    public void Parses_rgba_hex()
    {
        Assert.True(ShaderColor.TryParse("#7FE0D080", out var r, out var g, out var b, out var a));
        Assert.Equal(0x7F, r);
        Assert.Equal(0xE0, g);
        Assert.Equal(0xD0, b);
        Assert.Equal(0x80, a);
    }

    [Fact]
    public void Expands_rgb_hex_with_opaque_alpha()
    {
        Assert.True(ShaderColor.TryParse("#00FF00", out _, out _, out _, out var a));
        Assert.Equal(255, a);
    }

    [Fact]
    public void Formats_float4()
    {
        Assert.Equal("0.498f, 0.878f, 0.816f, 0.502f", ShaderColor.ToFloat4("#7FE0D080"));
    }

    [Fact]
    public void Rejects_invalid()
    {
        Assert.False(ShaderColor.TryParse("cyan", out _, out _, out _, out _));
    }
}
