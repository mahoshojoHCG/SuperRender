namespace SuperRender.Renderer.Image;

/// <summary>
/// Entry point for image decoding. Detects format and delegates to the appropriate decoder.
/// Supports PNG, BMP, and baseline JPEG in pure C# with no external dependencies.
/// </summary>
public static class ImageDecoder
{
    /// <summary>
    /// Decodes image bytes into RGBA pixel data. Returns null if the format is unrecognized or invalid.
    /// </summary>
    public static ImageData? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4) return null;

        if (PngDecoder.IsPng(data)) return PngDecoder.Decode(data);
        if (JpegDecoder.IsJpeg(data)) return JpegDecoder.Decode(data);
        if (BmpDecoder.IsBmp(data)) return BmpDecoder.Decode(data);

        return null;
    }

    /// <summary>
    /// Decodes image bytes from a byte array.
    /// </summary>
    public static ImageData? Decode(byte[] data) => Decode(data.AsSpan());
}
