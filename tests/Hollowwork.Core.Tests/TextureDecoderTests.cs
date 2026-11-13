using Hollowwork.Core.Textures;

namespace Hollowwork.Core.Tests;

public class TextureDecoderTests
{
    [Fact]
    public void Decodes_a_minimal_uncompressed_dds()
    {
        var dds = BuildDds(0x11, 0x22, 0x33, 0x44);

        Assert.True(TextureDecoder.TryDecodeDds(dds, out var texture));
        Assert.Equal(1, texture.Width);
        Assert.Equal(1, texture.Height);
        Assert.Equal(new byte[] { 0x33, 0x22, 0x11, 0x44 }, texture.Bgra);
    }

    [Fact]
    public void Rejects_garbage()
    {
        Assert.False(TextureDecoder.TryDecodeDds([1, 2, 3, 4], out _));
    }

    private static byte[] BuildDds(byte r, byte g, byte b, byte a)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(0x20534444u); // "DDS "
        writer.Write(124u);
        writer.Write(0x100Fu); // caps | height | width | pitch | pixelformat
        writer.Write(1u); // height
        writer.Write(1u); // width
        writer.Write(4u); // pitch
        writer.Write(0u); // depth
        writer.Write(0u); // mipmaps
        for (var i = 0; i < 11; i++)
        {
            writer.Write(0u);
        }

        writer.Write(32u); // pixel format size
        writer.Write(0x41u); // RGB | ALPHAPIXELS
        writer.Write(0u); // fourcc
        writer.Write(32u); // bit count
        writer.Write(0x000000FFu); // r
        writer.Write(0x0000FF00u); // g
        writer.Write(0x00FF0000u); // b
        writer.Write(0xFF000000u); // a

        writer.Write(0x1000u); // caps: texture
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);

        writer.Write(r);
        writer.Write(g);
        writer.Write(b);
        writer.Write(a);
        writer.Flush();
        return stream.ToArray();
    }
}
