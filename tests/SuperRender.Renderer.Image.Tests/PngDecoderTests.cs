using System.Buffers.Binary;
using System.IO.Compression;
using Xunit;

namespace SuperRender.Renderer.Image.Tests;

public class PngDecoderTests
{
    #region Helper: BuildPng

    /// <summary>
    /// Builds a valid PNG byte array programmatically.
    /// rawPixelData contains the unfiltered pixel data (without filter bytes).
    /// A None (0) filter byte is prepended to each scanline by default.
    /// </summary>
    private static byte[] BuildPng(int width, int height, byte colorType, byte bitDepth,
        byte[] rawPixelData, byte[]? palette = null, byte[]? transparency = null,
        byte[][]? filteredScanlines = null)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // PNG Signature
        bw.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        // IHDR
        WriteChunk(bw, "IHDR", BuildIhdr(width, height, bitDepth, colorType));

        // PLTE (optional)
        if (palette != null)
            WriteChunk(bw, "PLTE", palette);

        // tRNS (optional)
        if (transparency != null)
            WriteChunk(bw, "tRNS", transparency);

        // IDAT
        byte[] compressedData = CompressFilteredScanlines(width, height, colorType, bitDepth,
            rawPixelData, filteredScanlines);
        WriteChunk(bw, "IDAT", compressedData);

        // IEND
        WriteChunk(bw, "IEND", []);

        return ms.ToArray();
    }

    private static byte[] BuildIhdr(int width, int height, byte bitDepth, byte colorType)
    {
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), (uint)height);
        ihdr[8] = bitDepth;
        ihdr[9] = colorType;
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter
        ihdr[12] = 0; // interlace (none)
        return ihdr;
    }

    private static byte[] CompressFilteredScanlines(int width, int height, byte colorType,
        byte bitDepth, byte[] rawPixelData, byte[][]? filteredScanlines)
    {
        int channels = colorType switch
        {
            0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 1
        };
        int bitsPerPixel = channels * bitDepth;
        int scanlineBytes = (width * bitsPerPixel + 7) / 8;

        byte[] uncompressed;

        if (filteredScanlines != null)
        {
            // Use pre-built filtered scanlines (filter byte already included)
            using var scanMs = new MemoryStream();
            foreach (var line in filteredScanlines)
                scanMs.Write(line);
            uncompressed = scanMs.ToArray();
        }
        else
        {
            // Prepend filter byte 0 (None) to each scanline
            uncompressed = new byte[height * (1 + scanlineBytes)];
            for (int y = 0; y < height; y++)
            {
                int dstOffset = y * (1 + scanlineBytes);
                uncompressed[dstOffset] = 0; // None filter
                Array.Copy(rawPixelData, y * scanlineBytes, uncompressed, dstOffset + 1, scanlineBytes);
            }
        }

        // Zlib: 2-byte header + deflate data
        using var output = new MemoryStream();
        output.WriteByte(0x78); // CMF
        output.WriteByte(0x01); // FLG
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(uncompressed);
        }
        return output.ToArray();
    }

    private static void WriteChunk(BinaryWriter bw, string type, byte[] data)
    {
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);

        // Length (big-endian)
        var lenBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lenBytes, (uint)data.Length);
        bw.Write(lenBytes);

        // Type
        bw.Write(typeBytes);

        // Data
        bw.Write(data);

        // CRC32 over type + data
        byte[] crcInput = new byte[4 + data.Length];
        Array.Copy(typeBytes, 0, crcInput, 0, 4);
        Array.Copy(data, 0, crcInput, 4, data.Length);
        uint crc = Crc32(crcInput);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        bw.Write(crcBytes);
    }

    /// <summary>
    /// Standard CRC-32 as used by PNG.
    /// </summary>
    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return crc ^ 0xFFFFFFFF;
    }

    #endregion

    [Fact]
    public void IsPng_ValidSignature_ReturnsTrue()
    {
        byte[] data = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0];
        Assert.True(PngDecoder.IsPng(data));
    }

    [Fact]
    public void IsPng_InvalidSignature_ReturnsFalse()
    {
        byte[] data = [0x42, 0x4D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]; // BMP header
        Assert.False(PngDecoder.IsPng(data));
    }

    [Fact]
    public void Decode_1x1RedPixel_CorrectRgba()
    {
        // 1x1 red pixel, color type 2 (RGB), bit depth 8
        // Raw pixel data: R=255, G=0, B=0
        byte[] rawPixels = [255, 0, 0];
        byte[] png = BuildPng(1, 1, colorType: 2, bitDepth: 8, rawPixels);

        ImageData? result = PngDecoder.Decode(png);

        Assert.NotNull(result);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
        Assert.Equal(4, result.Pixels.Length);
        Assert.Equal(255, result.Pixels[0]); // R
        Assert.Equal(0, result.Pixels[1]);   // G
        Assert.Equal(0, result.Pixels[2]);   // B
        Assert.Equal(255, result.Pixels[3]); // A (expanded)
    }

    [Fact]
    public void Decode_1x1RedPixelRgba_CorrectRgba()
    {
        // 1x1 red pixel, color type 6 (RGBA), bit depth 8
        byte[] rawPixels = [255, 0, 0, 128];
        byte[] png = BuildPng(1, 1, colorType: 6, bitDepth: 8, rawPixels);

        ImageData? result = PngDecoder.Decode(png);

        Assert.NotNull(result);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
        Assert.Equal(4, result.Pixels.Length);
        Assert.Equal(255, result.Pixels[0]); // R
        Assert.Equal(0, result.Pixels[1]);   // G
        Assert.Equal(0, result.Pixels[2]);   // B
        Assert.Equal(128, result.Pixels[3]); // A
    }

    [Fact]
    public void Decode_GrayscalePixel_ExpandsToRgba()
    {
        // 1x1 white pixel, color type 0 (Grayscale), bit depth 8
        byte[] rawPixels = [255];
        byte[] png = BuildPng(1, 1, colorType: 0, bitDepth: 8, rawPixels);

        ImageData? result = PngDecoder.Decode(png);

        Assert.NotNull(result);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
        Assert.Equal(4, result.Pixels.Length);
        Assert.Equal(255, result.Pixels[0]); // R
        Assert.Equal(255, result.Pixels[1]); // G
        Assert.Equal(255, result.Pixels[2]); // B
        Assert.Equal(255, result.Pixels[3]); // A
    }

    [Fact]
    public void Decode_InvalidData_ReturnsNull()
    {
        byte[] data = [1, 2, 3, 4, 5];
        Assert.Null(PngDecoder.Decode(data));
    }

    [Fact]
    public void Decode_2x2WithSubFilter_Correct()
    {
        // 2x2 image, color type 2 (RGB), bit depth 8
        // Pixel layout:
        //   Row 0: (10, 20, 30), (40, 50, 60)
        //   Row 1: (70, 80, 90), (100, 110, 120)
        //
        // Sub filter (type 1): each byte = raw - left neighbor (bytesPerPixel=3)
        // Row 0 filtered: filter=1, then [10,20,30, (40-10),(50-20),(60-30)] = [10,20,30, 30,30,30]
        // Row 1 filtered: filter=1, then [70,80,90, (100-70),(110-80),(120-90)] = [70,80,90, 30,30,30]

        byte[][] filteredScanlines =
        [
            [1, 10, 20, 30, 30, 30, 30],  // filter=1 (Sub)
            [1, 70, 80, 90, 30, 30, 30]   // filter=1 (Sub)
        ];

        // rawPixelData is not used when filteredScanlines is provided
        byte[] png = BuildPng(2, 2, colorType: 2, bitDepth: 8, [], filteredScanlines: filteredScanlines);

        ImageData? result = PngDecoder.Decode(png);

        Assert.NotNull(result);
        Assert.Equal(2, result.Width);
        Assert.Equal(2, result.Height);

        // Row 0, pixel 0: R=10, G=20, B=30, A=255
        Assert.Equal(10, result.Pixels[0]);
        Assert.Equal(20, result.Pixels[1]);
        Assert.Equal(30, result.Pixels[2]);
        Assert.Equal(255, result.Pixels[3]);

        // Row 0, pixel 1: R=40, G=50, B=60, A=255
        Assert.Equal(40, result.Pixels[4]);
        Assert.Equal(50, result.Pixels[5]);
        Assert.Equal(60, result.Pixels[6]);
        Assert.Equal(255, result.Pixels[7]);

        // Row 1, pixel 0: R=70, G=80, B=90, A=255
        Assert.Equal(70, result.Pixels[8]);
        Assert.Equal(80, result.Pixels[9]);
        Assert.Equal(90, result.Pixels[10]);
        Assert.Equal(255, result.Pixels[11]);

        // Row 1, pixel 1: R=100, G=110, B=120, A=255
        Assert.Equal(100, result.Pixels[12]);
        Assert.Equal(110, result.Pixels[13]);
        Assert.Equal(120, result.Pixels[14]);
        Assert.Equal(255, result.Pixels[15]);
    }

    [Fact]
    public void Decode_2x2WithUpFilter_Correct()
    {
        // 2x2 image, color type 2 (RGB), bit depth 8
        // Pixel layout:
        //   Row 0: (10, 20, 30), (40, 50, 60)
        //   Row 1: (15, 25, 35), (45, 55, 65)
        //
        // Row 0: filter=0 (None), raw bytes
        // Row 1: filter=2 (Up), each byte = raw - above
        //   (15-10),(25-20),(35-30),(45-40),(55-50),(65-60) = 5,5,5,5,5,5

        byte[][] filteredScanlines =
        [
            [0, 10, 20, 30, 40, 50, 60],  // filter=0 (None)
            [2, 5, 5, 5, 5, 5, 5]         // filter=2 (Up)
        ];

        byte[] png = BuildPng(2, 2, colorType: 2, bitDepth: 8, [], filteredScanlines: filteredScanlines);

        ImageData? result = PngDecoder.Decode(png);

        Assert.NotNull(result);
        Assert.Equal(2, result.Width);
        Assert.Equal(2, result.Height);

        // Row 0, pixel 0
        Assert.Equal(10, result.Pixels[0]);
        Assert.Equal(20, result.Pixels[1]);
        Assert.Equal(30, result.Pixels[2]);
        Assert.Equal(255, result.Pixels[3]);

        // Row 0, pixel 1
        Assert.Equal(40, result.Pixels[4]);
        Assert.Equal(50, result.Pixels[5]);
        Assert.Equal(60, result.Pixels[6]);
        Assert.Equal(255, result.Pixels[7]);

        // Row 1, pixel 0: 10+5=15, 20+5=25, 30+5=35
        Assert.Equal(15, result.Pixels[8]);
        Assert.Equal(25, result.Pixels[9]);
        Assert.Equal(35, result.Pixels[10]);
        Assert.Equal(255, result.Pixels[11]);

        // Row 1, pixel 1: 40+5=45, 50+5=55, 60+5=65
        Assert.Equal(45, result.Pixels[12]);
        Assert.Equal(55, result.Pixels[13]);
        Assert.Equal(65, result.Pixels[14]);
        Assert.Equal(255, result.Pixels[15]);
    }

    [Fact]
    public void Decode_TruncatedIdat_ReturnsNull()
    {
        // Build a valid PNG but truncate the IDAT data
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // Signature
        bw.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        // IHDR
        byte[] ihdr = BuildIhdr(2, 2, 8, 2);
        WriteChunk(bw, "IHDR", ihdr);

        // Truncated IDAT - just the zlib header and a few bytes, not valid deflate data
        byte[] truncatedIdat = [0x78, 0x01, 0x00];
        WriteChunk(bw, "IDAT", truncatedIdat);

        // IEND
        WriteChunk(bw, "IEND", []);

        byte[] png = ms.ToArray();
        Assert.Null(PngDecoder.Decode(png));
    }

    [Fact]
    public void Decode_IndexedColor_UsesPlte()
    {
        // 2x2 indexed color (type 3), bit depth 8
        // Palette: index 0 = red (255,0,0), index 1 = green (0,255,0)
        byte[] palette = [255, 0, 0, 0, 255, 0];

        // Raw pixel data: row 0 = [0, 1], row 1 = [1, 0]
        byte[] rawPixels = [0, 1, 1, 0];

        byte[] png = BuildPng(2, 2, colorType: 3, bitDepth: 8, rawPixels, palette: palette);

        ImageData? result = PngDecoder.Decode(png);

        Assert.NotNull(result);
        Assert.Equal(2, result.Width);
        Assert.Equal(2, result.Height);

        // Row 0, pixel 0: index 0 = red
        Assert.Equal(255, result.Pixels[0]);
        Assert.Equal(0, result.Pixels[1]);
        Assert.Equal(0, result.Pixels[2]);
        Assert.Equal(255, result.Pixels[3]);

        // Row 0, pixel 1: index 1 = green
        Assert.Equal(0, result.Pixels[4]);
        Assert.Equal(255, result.Pixels[5]);
        Assert.Equal(0, result.Pixels[6]);
        Assert.Equal(255, result.Pixels[7]);

        // Row 1, pixel 0: index 1 = green
        Assert.Equal(0, result.Pixels[8]);
        Assert.Equal(255, result.Pixels[9]);
        Assert.Equal(0, result.Pixels[10]);
        Assert.Equal(255, result.Pixels[11]);

        // Row 1, pixel 1: index 0 = red
        Assert.Equal(255, result.Pixels[12]);
        Assert.Equal(0, result.Pixels[13]);
        Assert.Equal(0, result.Pixels[14]);
        Assert.Equal(255, result.Pixels[15]);
    }
}
