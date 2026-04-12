using SuperRender.Document.Css;

namespace SuperRender.Renderer.Rendering.Style;

public sealed partial class StyleResolver
{
    private static bool ApplyColorProperty(ComputedStyle style, string prop, CssValue value, ComputedStyle? parentStyle)
    {
        switch (prop)
        {
            case CssPropertyNames.Color:
                style.Color = ResolveColor(value, parentStyle);
                break;
            case CssPropertyNames.BackgroundColor:
                style.BackgroundColor = ResolveColor(value, style);
                break;

            case CssPropertyNames.BorderColor:
                var bc = ResolveColor(value, style);
                style.BorderTopColor = bc;
                style.BorderRightColor = bc;
                style.BorderBottomColor = bc;
                style.BorderLeftColor = bc;
                break;
            case CssPropertyNames.BorderTopColor:
                style.BorderTopColor = ResolveColor(value, style);
                break;
            case CssPropertyNames.BorderRightColor:
                style.BorderRightColor = ResolveColor(value, style);
                break;
            case CssPropertyNames.BorderBottomColor:
                style.BorderBottomColor = ResolveColor(value, style);
                break;
            case CssPropertyNames.BorderLeftColor:
                style.BorderLeftColor = ResolveColor(value, style);
                break;

            case CssPropertyNames.TextDecorationColor:
                style.TextDecorationColor = ResolveColor(value, style);
                break;

            // P1: Opacity
            case CssPropertyNames.Opacity:
                if (value.Type is CssValueType.Number or CssValueType.Percentage)
                    style.Opacity = (float)Math.Clamp(value.NumericValue, 0, 1);
                break;

            default:
                return false;
        }
        return true;
    }
}
