using SuperRender.Core.Layout;

namespace SuperRender.Demo;

public sealed class BitmapFontTextMeasurer : ITextMeasurer
{
    private readonly FontAtlas _fontAtlas;

    public BitmapFontTextMeasurer(FontAtlas fontAtlas)
    {
        _fontAtlas = fontAtlas;
    }

    /// <summary>
    /// Measures the total width of <paramref name="text"/> at the given
    /// <paramref name="fontSize"/> by summing the AdvanceX of every glyph,
    /// scaled by <c>fontSize / BaseFontSize</c>.
    /// </summary>
    public float MeasureWidth(string text, float fontSize)
    {
        float scale = fontSize / FontAtlasGenerator.BaseFontSize;
        float width = 0f;

        foreach (char c in text)
        {
            if (_fontAtlas.Glyphs.TryGetValue(c, out var glyph))
                width += glyph.AdvanceX * scale;
            else if (_fontAtlas.Glyphs.TryGetValue('?', out var fallback))
                width += fallback.AdvanceX * scale;
        }

        return width;
    }

    public float GetLineHeight(float fontSize, float lineHeightMultiplier)
        => fontSize * lineHeightMultiplier;

    public float GetAscent(float fontSize)
        => FontAtlasGenerator.Ascent * (fontSize / FontAtlasGenerator.BaseFontSize);
}
