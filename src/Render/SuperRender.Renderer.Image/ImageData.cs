namespace SuperRender.Renderer.Image;

/// <summary>
/// Decoded image data in RGBA format.
/// </summary>
public sealed class ImageData
{
    public required int Width { get; init; }
    public required int Height { get; init; }

    /// <summary>
    /// RGBA pixel data, 4 bytes per pixel, row-major order (top-to-bottom, left-to-right).
    /// Length = Width * Height * 4.
    /// </summary>
    public required byte[] Pixels { get; init; }
}
