namespace SuperRender.Gpu;

public sealed class FontAtlas : IDisposable
{
    public int AtlasWidth { get; }
    public int AtlasHeight { get; }
    public byte[] PixelData { get; }
    public Dictionary<char, GlyphInfo> Glyphs { get; }

    public FontAtlas()
    {
        AtlasWidth = FontAtlasGenerator.AtlasWidth;
        AtlasHeight = FontAtlasGenerator.AtlasHeight;
        PixelData = FontAtlasGenerator.GenerateAtlas(out var glyphs);
        Glyphs = glyphs;
    }

    public void Dispose()
    {
        // No unmanaged resources to release; kept for future GPU resource cleanup.
    }
}
