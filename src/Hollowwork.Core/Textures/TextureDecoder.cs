using Pfim;

namespace Hollowwork.Core.Textures;

public sealed record DecodedTexture(int Width, int Height, byte[] Bgra);

public static class TextureDecoder
{
    public static bool TryDecodeDds(byte[] bytes, out DecodedTexture texture)
    {
        texture = null!;
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var dds = Dds.Create(stream, new PfimConfig());
            if (dds.Width <= 0 || dds.Height <= 0)
            {
                return false;
            }

            var bgra = ToBgra(dds);
            if (bgra is null)
            {
                return false;
            }

            texture = new DecodedTexture(dds.Width, dds.Height, bgra);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[]? ToBgra(Dds dds)
    {
        var width = dds.Width;
        var height = dds.Height;
        var source = dds.Data;
        var stride = dds.Stride;

        switch (dds.Format)
        {
            case ImageFormat.Rgba32:
            {
                var result = new byte[width * height * 4];
                for (var y = 0; y < height; y++)
                {
                    Buffer.BlockCopy(source, y * stride, result, y * width * 4, width * 4);
                }

                return result;
            }
            case ImageFormat.Rgb24:
            {
                var result = new byte[width * height * 4];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var src = y * stride + x * 3;
                        var dst = (y * width + x) * 4;
                        result[dst] = source[src + 2];
                        result[dst + 1] = source[src + 1];
                        result[dst + 2] = source[src];
                        result[dst + 3] = 255;
                    }
                }

                return result;
            }
            default:
                return null;
        }
    }
}
