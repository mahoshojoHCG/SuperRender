namespace SuperRender.Core.Layout;

public interface ITextMeasurer
{
    float MeasureWidth(string text, float fontSize);
    float MeasureWidth(string text, float fontSize, string fontFamily, int fontWeight);
    float GetLineHeight(float fontSize, float lineHeightMultiplier);
    float GetAscent(float fontSize);
}

public sealed class MonospaceTextMeasurer : ITextMeasurer
{
    private readonly float _charWidthRatio;

    public MonospaceTextMeasurer(float charWidthRatio = 0.6f)
    {
        _charWidthRatio = charWidthRatio;
    }

    public float MeasureWidth(string text, float fontSize)
        => text.Length * fontSize * _charWidthRatio;

    public float MeasureWidth(string text, float fontSize, string fontFamily, int fontWeight)
        => text.Length * fontSize * _charWidthRatio;

    public float GetLineHeight(float fontSize, float lineHeightMultiplier)
        => fontSize * lineHeightMultiplier;

    public float GetAscent(float fontSize)
        => fontSize * 0.8f;
}
