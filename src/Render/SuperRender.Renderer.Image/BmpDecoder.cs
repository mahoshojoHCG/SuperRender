using System.Buffers.Binary;

namespace SuperRender.Renderer.Image;

/// <summary>
/// Pure C# BMP image decoder. Supports 24-bit and 32-bit uncompressed BMPs.
/// </summary>
public static class BmpDecoder
{
    public static bool IsBmp(ReadOnlySpan<byte> data)
        => data.Length >= 2 && data[0] == (byte)'B' && data[1] == (byte)'M';

    public static ImageData? Decode(ReadOnlySpan<byte> data)
    {
        if (!IsBmp(data) || data.Length < 54) return null;

        // BMP file header (14 bytes)
        int dataOffset = BinaryPrimitives.ReadInt32LittleEndian(data[10..]);

        // DIB header (BITMAPINFOHEADER = 40 bytes starting at offset 14)
        int headerSize = BinaryPrimitives.ReadInt32LittleEndian(data[14..]);
        if (headerSize < 40) return null;

        int width = BinaryPrimitives.ReadInt32LittleEndian(data[18..]);
        int height = BinaryPrimitives.ReadInt32LittleEndian(data[22..]);
        int bitsPerPixel = BinaryPrimitives.ReadInt16LittleEndian(data[28..]);
        int compression = BinaryPrimitives.ReadInt32LittleEndian(data[30..]);

        // Only support uncompressed (0) and BITFIELDS (3) for 32-bit
        if (compression != 0 && compression != 3) return null;
        if (bitsPerPixel != 24 && bitsPerPixel != 32) return null;
        if (width <= 0 || width > 65535) return null;

        // Height can be negative (top-down) or positive (bottom-up)
        bool topDown = height < 0;
        int absHeight = Math.Abs(height);
        if (absHeight <= 0 || absHeight > 65535) return null;

        int bytesPerPixel = bitsPerPixel / 8;
        int rowStride = (width * bytesPerPixel + 3) & ~3; // rows padded to 4-byte boundary

        if (dataOffset + rowStride * absHeight > data.Length) return null;

        var pixels = new byte[width * absHeight * 4];

        for (int y = 0; y < absHeight; y++)
        {
            int srcRow = topDown ? y : (absHeight - 1 - y);
            int srcOffset = dataOffset + srcRow * rowStride;
            int dstOffset = y * width * 4;

            for (int x = 0; x < width; x++)
            {
                int si = srcOffset + x * bytesPerPixel;
                int di = dstOffset + x * 4;

                byte b = data[si];
                byte g = data[si + 1];
                byte r = data[si + 2];
                byte a = bytesPerPixel == 4 ? data[si + 3] : (byte)255;

                pixels[di] = r;
                pixels[di + 1] = g;
                pixels[di + 2] = b;
                pixels[di + 3] = a;
            }
        }

        return new ImageData { Width = width, Height = absHeight, Pixels = pixels };
    }
}
