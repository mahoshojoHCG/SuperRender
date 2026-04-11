using SuperRender.Core.Layout;

namespace SuperRender.Gpu;

public sealed class BitmapFontTextMeasurer : ITextMeasurer
{
    private readonly FontAtlas _fontAtlas;

    public BitmapFontTextMeasurer(FontAtlas fontAtlas)
    {
        _fontAtlas = fontAtlas;
    }

    /// <summary>
    /// Measures text width using the regular glyph set (backward compatible).
    /// </summary>
    public float MeasureWidth(string text, float fontSize)
    {
        _fontAtlas.EnsureGlyphs(text, GlyphVariant.Regular);
        return MeasureWithGlyphs(text, fontSize, _fontAtlas.Glyphs);
    }

    /// <summary>
    /// Measures text width using the correct glyph set based on font family and weight.
    /// </summary>
    public float MeasureWidth(string text, float fontSize, string fontFamily, int fontWeight)
    {
        var (glyphs, variant) = SelectGlyphs(fontFamily, fontWeight);
        _fontAtlas.EnsureGlyphs(text, variant);
        return MeasureWithGlyphs(text, fontSize, glyphs);
    }

    public float GetLineHeight(float fontSize, float lineHeightMultiplier)
        => fontSize * lineHeightMultiplier;

    public float GetAscent(float fontSize)
        => FontAtlasGenerator.Ascent * (fontSize / FontAtlasGenerator.AtlasRenderSize);

    private static float MeasureWithGlyphs(string text, float fontSize, Dictionary<char, GlyphInfo> glyphs)
    {
        float scale = fontSize / FontAtlasGenerator.AtlasRenderSize;
        float width = 0f;

        foreach (char c in text)
        {
            if (glyphs.TryGetValue(c, out var glyph))
                width += glyph.AdvanceX * scale;
            else if (glyphs.TryGetValue('?', out var fallback))
                width += fallback.AdvanceX * scale;
        }

        return width;
    }

    private (Dictionary<char, GlyphInfo> glyphs, GlyphVariant variant)
        SelectGlyphs(string fontFamily, int fontWeight)
    {
        if (!string.IsNullOrEmpty(fontFamily) && IsMonospaceFamily(fontFamily))
            return (_fontAtlas.MonospaceGlyphs, GlyphVariant.Monospace);
        if (fontWeight >= 700)
            return (_fontAtlas.BoldGlyphs, GlyphVariant.Bold);
        return (_fontAtlas.Glyphs, GlyphVariant.Regular);
    }

    private static bool IsMonospaceFamily(string family)
    {
        return family.Equals("monospace", StringComparison.OrdinalIgnoreCase)
            || family.Equals("ui-monospace", StringComparison.OrdinalIgnoreCase)
            || family.Contains("Mono", StringComparison.OrdinalIgnoreCase)
            || family.Contains("Courier", StringComparison.OrdinalIgnoreCase)
            || family.Contains("Consolas", StringComparison.OrdinalIgnoreCase)
            || family.Contains("Menlo", StringComparison.OrdinalIgnoreCase);
    }
}
