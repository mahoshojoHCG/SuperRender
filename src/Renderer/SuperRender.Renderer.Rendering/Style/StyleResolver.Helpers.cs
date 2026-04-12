using SuperRender.Document;
using SuperRender.Document.Css;

namespace SuperRender.Renderer.Rendering.Style;

public sealed partial class StyleResolver
{
    private float ResolveLength(CssValue value, ComputedStyle? parentStyle)
    {
        if (value.Raw.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return float.NaN;

        if (value.Raw == "0")
            return 0;

        if (value.Type == CssValueType.Calc && value.CalcExpr != null)
        {
            var context = new CalcContext
            {
                FontSize = parentStyle?.FontSize ?? 16,
                ContainingBlockSize = 0,
                ViewportWidth = _viewportWidth,
                ViewportHeight = _viewportHeight
            };
            return (float)value.CalcExpr.Evaluate(context);
        }

        return value.Type switch
        {
            CssValueType.Length => value.Unit?.ToLowerInvariant() switch
            {
                "px" => (float)value.NumericValue,
                "em" => (float)value.NumericValue * (parentStyle?.FontSize ?? PropertyDefaults.DefaultFontSize),
                "rem" => (float)value.NumericValue * PropertyDefaults.DefaultFontSize,
                "vw" => (float)(value.NumericValue * _viewportWidth / 100),
                "vh" => (float)(value.NumericValue * _viewportHeight / 100),
                "vmin" => (float)(value.NumericValue * Math.Min(_viewportWidth, _viewportHeight) / 100),
                "vmax" => (float)(value.NumericValue * Math.Max(_viewportWidth, _viewportHeight) / 100),
                _ => (float)value.NumericValue
            },
            CssValueType.Number => (float)value.NumericValue,
            CssValueType.Percentage => (float)value.NumericValue,
            _ => float.NaN
        };
    }

    private static float ResolveFontSize(CssValue value, ComputedStyle? parentStyle)
    {
        var parentFontSize = parentStyle?.FontSize ?? PropertyDefaults.DefaultFontSize;

        return value.Type switch
        {
            CssValueType.Length => value.Unit?.ToLowerInvariant() switch
            {
                "px" => (float)value.NumericValue,
                "em" => (float)value.NumericValue * parentFontSize,
                "rem" => (float)value.NumericValue * PropertyDefaults.DefaultFontSize,
                "pt" => (float)value.NumericValue * 1.333f,
                _ => (float)value.NumericValue
            },
            CssValueType.Percentage => parentFontSize * (float)value.NumericValue / 100f,
            CssValueType.Number => (float)value.NumericValue,
            CssValueType.Keyword => value.Raw.ToLowerInvariant() switch
            {
                "small" => 13f,
                "medium" => PropertyDefaults.DefaultFontSize,
                "large" => 18f,
                "x-large" => 24f,
                "xx-large" => 32f,
                "smaller" => parentFontSize * 0.833f,
                "larger" => parentFontSize * 1.2f,
                _ => parentFontSize
            },
            _ => parentFontSize
        };
    }

    /// <summary>
    /// Resolves a border-radius value. Percentage values are stored as negative
    /// values (e.g., 50% → -50) to signal percentage-based resolution during painting.
    /// </summary>
    private float ResolveBorderRadius(CssValue value, ComputedStyle? parentStyle)
    {
        if (value.Type == CssValueType.Percentage)
            return -(float)value.NumericValue; // negative = percentage marker
        return ResolveLength(value, parentStyle);
    }

    private static Color ResolveColor(CssValue value, ComputedStyle? contextStyle = null)
    {
        if (value.ColorValue.HasValue)
            return value.ColorValue.Value;

        if (value.Raw.Equals("currentcolor", StringComparison.OrdinalIgnoreCase) ||
            value.Raw.Equals("currentColor", StringComparison.Ordinal))
        {
            return contextStyle?.Color ?? Color.Black;
        }

        if (value.Type == CssValueType.Color)
            return Color.FromHex(value.Raw);

        if (Color.TryFromName(value.Raw, out var named))
            return named;

        if (value.Raw.StartsWith('#'))
            return Color.FromHex(value.Raw);

        return Color.Black;
    }

    private static int ResolveFontWeight(CssValue value, ComputedStyle? parentStyle)
    {
        var raw = value.Raw.ToLowerInvariant();
        return raw switch
        {
            "normal" => 400,
            "bold" => 700,
            "bolder" => Math.Min((parentStyle?.FontWeight ?? 400) + 300, 900),
            "lighter" => Math.Max((parentStyle?.FontWeight ?? 400) - 100, 100),
            _ => value.Type == CssValueType.Number
                ? Math.Clamp((int)value.NumericValue, 100, 900)
                : parentStyle?.FontWeight ?? 400
        };
    }

    private static TextDecorationLine ResolveTextDecorationLine(CssValue value)
    {
        var raw = value.Raw.ToLowerInvariant().Trim();
        if (raw == "none")
            return TextDecorationLine.None;

        var result = TextDecorationLine.None;
        foreach (var part in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            result |= part switch
            {
                "underline" => TextDecorationLine.Underline,
                "overline" => TextDecorationLine.Overline,
                "line-through" => TextDecorationLine.LineThrough,
                _ => TextDecorationLine.None
            };
        }
        return result;
    }
}
