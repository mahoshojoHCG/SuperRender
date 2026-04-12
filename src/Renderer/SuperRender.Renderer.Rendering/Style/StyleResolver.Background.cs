using SuperRender.Document.Css;

namespace SuperRender.Renderer.Rendering.Style;

public sealed partial class StyleResolver
{
    private bool ApplyBackgroundProperty(ComputedStyle style, string prop, CssValue value, ComputedStyle? parentStyle)
    {
        switch (prop)
        {
            case CssPropertyNames.BackgroundImage:
                if (value.Gradient != null)
                {
                    style.BackgroundImage = value.Gradient;
                }
                else if (value.Raw.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
                {
                    style.BackgroundImageUrl = ExtractUrl(value.Raw);
                }
                else if (value.Raw.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    style.BackgroundImage = null;
                    style.BackgroundImageUrl = null;
                }
                break;

            case CssPropertyNames.BackgroundRepeat:
                style.BackgroundRepeat = value.Raw.ToLowerInvariant();
                break;

            case CssPropertyNames.BackgroundPositionX:
                if (value.Type is CssValueType.Length or CssValueType.Number)
                    style.BackgroundPositionX = ResolveLength(value, parentStyle);
                else if (value.Type == CssValueType.Percentage)
                    style.BackgroundPositionX = (float)value.NumericValue;
                break;

            case CssPropertyNames.BackgroundPositionY:
                if (value.Type is CssValueType.Length or CssValueType.Number)
                    style.BackgroundPositionY = ResolveLength(value, parentStyle);
                else if (value.Type == CssValueType.Percentage)
                    style.BackgroundPositionY = (float)value.NumericValue;
                break;

            case CssPropertyNames.BackgroundSize:
                style.BackgroundSize = value.Raw.ToLowerInvariant();
                break;

            case CssPropertyNames.BackgroundAttachment:
                style.BackgroundAttachment = value.Raw.ToLowerInvariant();
                break;

            case CssPropertyNames.BackgroundOrigin:
                style.BackgroundOrigin = value.Raw.ToLowerInvariant();
                break;

            case CssPropertyNames.BackgroundClip:
                style.BackgroundClip = value.Raw.ToLowerInvariant();
                break;

            case CssPropertyNames.BoxShadow:
                if (value.Raw.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    style.BoxShadows = null;
                }
                else
                {
                    style.BoxShadows = CssParser.ParseBoxShadowValue(value.Raw);
                }
                break;

            case CssPropertyNames.OutlineWidth:
                if (value.Type is CssValueType.Length or CssValueType.Number)
                    style.OutlineWidth = ResolveLength(value, parentStyle);
                else if (value.Raw.Equals("thin", StringComparison.OrdinalIgnoreCase))
                    style.OutlineWidth = 1f;
                else if (value.Raw.Equals("medium", StringComparison.OrdinalIgnoreCase))
                    style.OutlineWidth = 3f;
                else if (value.Raw.Equals("thick", StringComparison.OrdinalIgnoreCase))
                    style.OutlineWidth = 5f;
                break;

            case CssPropertyNames.OutlineColor:
                style.OutlineColor = ResolveColor(value, style);
                break;

            case CssPropertyNames.OutlineStyle:
                style.OutlineStyle = value.Raw.ToLowerInvariant();
                break;

            case CssPropertyNames.OutlineOffset:
                if (value.Type is CssValueType.Length or CssValueType.Number)
                    style.OutlineOffset = ResolveLength(value, parentStyle);
                break;

            case CssPropertyNames.Outline:
                // outline shorthand: width style color (any order)
                ApplyOutlineShorthand(style, value, parentStyle);
                break;

            default:
                return false;
        }
        return true;
    }

    private void ApplyOutlineShorthand(ComputedStyle style, CssValue value, ComputedStyle? parentStyle)
    {
        var raw = value.Raw;
        if (raw.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            style.OutlineStyle = "none";
            style.OutlineWidth = 0;
            return;
        }

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var outlineStyles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "none", "hidden", "dotted", "dashed", "solid", "double", "groove", "ridge", "inset", "outset", "auto"
        };

        foreach (var part in parts)
        {
            var partVal = CssParser.ParseValueText(part);
            if (partVal.Type is CssValueType.Length or CssValueType.Number)
            {
                style.OutlineWidth = ResolveLength(partVal, parentStyle);
            }
            else if (partVal.Type == CssValueType.Color || partVal.ColorValue.HasValue
                     || Document.Color.TryFromName(part, out _))
            {
                style.OutlineColor = ResolveColor(partVal, style);
            }
            else if (outlineStyles.Contains(part))
            {
                style.OutlineStyle = part.ToLowerInvariant();
            }
        }
    }

    private static string? ExtractUrl(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("url(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(')'))
        {
            var url = trimmed.Substring(4, trimmed.Length - 5).Trim();
            if (url.Length >= 2 && ((url[0] == '"' && url[^1] == '"') || (url[0] == '\'' && url[^1] == '\'')))
                url = url[1..^1];
            return url;
        }
        return null;
    }
}
