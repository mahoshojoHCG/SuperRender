using Xunit;

namespace SuperRender.Renderer.Image.Tests;

public class JpegDecoderTests
{
    [Fact]
    public void IsJpeg_ValidSignature_ReturnsTrue()
    {
        byte[] data = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
        Assert.True(JpegDecoder.IsJpeg(data));
    }

    [Fact]
    public void IsJpeg_InvalidSignature_ReturnsFalse()
    {
        // PNG signature
        byte[] data = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.False(JpegDecoder.IsJpeg(data));
    }

    [Fact]
    public void IsJpeg_EmptyData_ReturnsFalse()
    {
        Assert.False(JpegDecoder.IsJpeg(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void IsJpeg_SingleByte_ReturnsFalse()
    {
        byte[] data = [0xFF];
        Assert.False(JpegDecoder.IsJpeg(data));
    }

    [Fact]
    public void Decode_InvalidData_ReturnsNull()
    {
        byte[] data = [0x00, 0x01, 0x02, 0x03];
        Assert.Null(JpegDecoder.Decode(data));
    }

    [Fact]
    public void Decode_TruncatedData_ReturnsNull()
    {
        // Valid SOI but nothing else
        byte[] data = [0xFF, 0xD8, 0xFF];
        Assert.Null(JpegDecoder.Decode(data));
    }

    [Fact]
    public void Decode_ProgressiveJpeg_ReturnsNull()
    {
        // SOI + SOF2 (progressive) marker
        byte[] data =
        [
            0xFF, 0xD8, // SOI
            0xFF, 0xC2, // SOF2 - progressive
            0x00, 0x0B, // length = 11
            0x08,       // precision
            0x00, 0x08, // height = 8
            0x00, 0x08, // width = 8
            0x01,       // 1 component
            0x01, 0x11, 0x00 // component 1, H=1 V=1, QT=0
        ];
        Assert.Null(JpegDecoder.Decode(data));
    }

    [Fact]
    public void Decode_MinimalBaseline_ValidDimensions()
    {
        byte[] jpeg = BuildMinimalGrayscaleJpeg();
        var result = JpegDecoder.Decode(jpeg);

        Assert.NotNull(result);
        Assert.Equal(8, result.Width);
        Assert.Equal(8, result.Height);
        Assert.Equal(8 * 8 * 4, result.Pixels.Length);
    }

    [Fact]
    public void Decode_MinimalBaseline_AllPixelsHaveFullAlpha()
    {
        byte[] jpeg = BuildMinimalGrayscaleJpeg();
        var result = JpegDecoder.Decode(jpeg);

        Assert.NotNull(result);
        for (int i = 0; i < result.Width * result.Height; i++)
        {
            Assert.Equal(255, result.Pixels[i * 4 + 3]);
        }
    }

    [Fact]
    public void ZigZag_Table_Has64Entries()
    {
        Assert.Equal(64, JpegDecoder.ZigZag.Length);
    }

    [Fact]
    public void ZigZag_Table_ContainsAllIndices()
    {
        // Each index 0-63 should appear exactly once
        var sorted = JpegDecoder.ZigZag.Order().ToArray();
        for (int i = 0; i < 64; i++)
        {
            Assert.Equal(i, sorted[i]);
        }
    }

    [Fact]
    public void YCbCrToRgb_PureWhite_Correct()
    {
        var (r, g, b) = JpegDecoder.YCbCrToRgb(255, 128, 128);
        Assert.Equal(255, r);
        Assert.Equal(255, g);
        Assert.Equal(255, b);
    }

    [Fact]
    public void YCbCrToRgb_PureBlack_Correct()
    {
        var (r, g, b) = JpegDecoder.YCbCrToRgb(0, 128, 128);
        Assert.Equal(0, r);
        Assert.Equal(0, g);
        Assert.Equal(0, b);
    }

    [Fact]
    public void YCbCrToRgb_MidGray_Correct()
    {
        var (r, g, b) = JpegDecoder.YCbCrToRgb(128, 128, 128);
        Assert.Equal(128, r);
        Assert.Equal(128, g);
        Assert.Equal(128, b);
    }

    [Fact]
    public void YCbCrToRgb_Clamps_OverflowValues()
    {
        // Y=255, Cb=0, Cr=255 would produce out-of-range values
        var (r, g, b) = JpegDecoder.YCbCrToRgb(255, 0, 255);
        Assert.InRange(r, 0, 255);
        Assert.InRange(g, 0, 255);
        Assert.InRange(b, 0, 255);
    }

    /// <summary>
    /// Builds a minimal valid baseline JPEG encoding an 8x8 grayscale image where all pixels
    /// are mid-gray (128). This produces a trivial entropy stream: DC category 0 + AC EOB.
    /// </summary>
    private static byte[] BuildMinimalGrayscaleJpeg()
    {
        using var ms = new MemoryStream();

        // SOI
        ms.Write([0xFF, 0xD8]);

        // DQT - quantization table 0, all values = 1 (identity quantization)
        ms.Write([0xFF, 0xDB]); // marker
        ms.Write([0x00, 0x43]); // length = 67
        ms.WriteByte(0x00);     // 8-bit precision, table 0
        for (int i = 0; i < 64; i++)
            ms.WriteByte(0x01); // all quant values = 1

        // SOF0 - baseline, 8x8, 1 component (grayscale)
        ms.Write([0xFF, 0xC0]); // marker
        ms.Write([0x00, 0x0B]); // length = 11
        ms.WriteByte(0x08);     // 8-bit precision
        ms.Write([0x00, 0x08]); // height = 8
        ms.Write([0x00, 0x08]); // width = 8
        ms.WriteByte(0x01);     // 1 component
        ms.WriteByte(0x01);     // component ID = 1
        ms.WriteByte(0x11);     // H=1, V=1
        ms.WriteByte(0x00);     // quant table 0

        // DHT - DC table 0: one code of length 1 for symbol 0 (category 0)
        WriteDHT(ms, 0x00, [1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0x00]);

        // DHT - AC table 0: one code of length 1 for symbol 0x00 (EOB)
        WriteDHT(ms, 0x10, [1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0x00]);

        // SOS
        ms.Write([0xFF, 0xDA]); // marker
        ms.Write([0x00, 0x08]); // length = 8
        ms.WriteByte(0x01);     // 1 component
        ms.WriteByte(0x01);     // component selector = 1
        ms.WriteByte(0x00);     // DC table 0, AC table 0
        ms.WriteByte(0x00);     // spectral selection start
        ms.WriteByte(0x3F);     // spectral selection end (63)
        ms.WriteByte(0x00);     // successive approximation

        // Entropy-coded data:
        // For a uniform mid-gray 8x8 block (all pixels = 128):
        //   After level shift: all values = 0
        //   DCT: all coefficients = 0
        //   DC: category 0 -> Huffman code "0" (1 bit)
        //   AC: EOB -> Huffman code "0" (1 bit)
        // Total: 2 bits = 00, pad remaining 6 bits with 1s -> 0x3F
        ms.WriteByte(0x3F);

        // EOI
        ms.Write([0xFF, 0xD9]);

        return ms.ToArray();
    }

    private static void WriteDHT(MemoryStream ms, byte tableInfo, byte[] counts, byte[] symbols)
    {
        ms.Write([0xFF, 0xC4]); // DHT marker
        int length = 2 + 1 + 16 + symbols.Length;
        ms.WriteByte((byte)(length >> 8));
        ms.WriteByte((byte)(length & 0xFF));
        ms.WriteByte(tableInfo);
        ms.Write(counts);
        ms.Write(symbols);
    }
}
