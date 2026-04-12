using SuperRender.Document.Css;
using SuperRender.Renderer.Rendering.Layout;

namespace SuperRender.Renderer.Rendering.Style;

public sealed partial class StyleResolver
{
    private bool ApplyBoxModelProperty(ComputedStyle style, string prop, CssValue value, ComputedStyle? parentStyle)
    {
        switch (prop)
        {
            case CssPropertyNames.Width:
                if (value.Type == CssValueType.Calc && value.CalcExpr != null)
                {
                    style.WidthCalc = value.CalcExpr;
                    style.Width = float.NaN;
                }
                else if (value.Type == CssValueType.Percentage)
                {
                    style.WidthCalc = new CalcValueNode(value);
                    style.Width = float.NaN;
                }
                else
                {
                    style.Width = ResolveLength(value, parentStyle);
                    style.WidthCalc = null;
                }
                break;
            case CssPropertyNames.Height:
                if (value.Type == CssValueType.Calc && value.CalcExpr != null)
                {
                    style.HeightCalc = value.CalcExpr;
                    style.Height = float.NaN;
                }
                else if (value.Type == CssValueType.Percentage)
                {
                    style.HeightCalc = new CalcValueNode(value);
                    style.Height = float.NaN;
                }
                else
                {
                    style.Height = ResolveLength(value, parentStyle);
                    style.HeightCalc = null;
                }
                break;

            case CssPropertyNames.BoxSizing:
                style.BoxSizing = value.Raw.ToLowerInvariant() switch
                {
                    "content-box" => BoxSizingType.ContentBox,
                    "border-box" => BoxSizingType.BorderBox,
                    _ => style.BoxSizing
                };
                break;

            case CssPropertyNames.MinWidth:
                var minW = ResolveLength(value, parentStyle);
                style.MinWidth = float.IsNaN(minW) ? 0 : minW;
                break;
            case CssPropertyNames.MaxWidth:
                if (value.Raw.Equals("none", StringComparison.OrdinalIgnoreCase))
                    style.MaxWidth = float.PositiveInfinity;
                else
                {
                    var maxW = ResolveLength(value, parentStyle);
                    style.MaxWidth = float.IsNaN(maxW) ? float.PositiveInfinity : maxW;
                }
                break;
            case CssPropertyNames.MinHeight:
                var minH = ResolveLength(value, parentStyle);
                style.MinHeight = float.IsNaN(minH) ? 0 : minH;
                break;
            case CssPropertyNames.MaxHeight:
                if (value.Raw.Equals("none", StringComparison.OrdinalIgnoreCase))
                    style.MaxHeight = float.PositiveInfinity;
                else
                {
                    var maxH = ResolveLength(value, parentStyle);
                    style.MaxHeight = float.IsNaN(maxH) ? float.PositiveInfinity : maxH;
                }
                break;

            case CssPropertyNames.MarginTop:
                style.Margin = style.Margin with { Top = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.MarginRight:
                style.Margin = style.Margin with { Right = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.MarginBottom:
                style.Margin = style.Margin with { Bottom = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.MarginLeft:
                style.Margin = style.Margin with { Left = ResolveLength(value, parentStyle) };
                break;

            case CssPropertyNames.PaddingTop:
                style.Padding = style.Padding with { Top = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.PaddingRight:
                style.Padding = style.Padding with { Right = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.PaddingBottom:
                style.Padding = style.Padding with { Bottom = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.PaddingLeft:
                style.Padding = style.Padding with { Left = ResolveLength(value, parentStyle) };
                break;

            case CssPropertyNames.BorderWidth:
                var bw = ResolveLength(value, parentStyle);
                style.BorderWidth = new EdgeSizes(bw);
                break;
            case CssPropertyNames.BorderTopWidth:
                style.BorderWidth = style.BorderWidth with { Top = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.BorderRightWidth:
                style.BorderWidth = style.BorderWidth with { Right = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.BorderBottomWidth:
                style.BorderWidth = style.BorderWidth with { Bottom = ResolveLength(value, parentStyle) };
                break;
            case CssPropertyNames.BorderLeftWidth:
                style.BorderWidth = style.BorderWidth with { Left = ResolveLength(value, parentStyle) };
                break;

            case CssPropertyNames.BorderStyle:
                var bs = value.Raw.ToLowerInvariant();
                style.BorderTopStyle = bs;
                style.BorderRightStyle = bs;
                style.BorderBottomStyle = bs;
                style.BorderLeftStyle = bs;
                break;
            case CssPropertyNames.BorderTopStyle:
                style.BorderTopStyle = value.Raw.ToLowerInvariant();
                break;
            case CssPropertyNames.BorderRightStyle:
                style.BorderRightStyle = value.Raw.ToLowerInvariant();
                break;
            case CssPropertyNames.BorderBottomStyle:
                style.BorderBottomStyle = value.Raw.ToLowerInvariant();
                break;
            case CssPropertyNames.BorderLeftStyle:
                style.BorderLeftStyle = value.Raw.ToLowerInvariant();
                break;

            case CssPropertyNames.BorderTopLeftRadius:
                style.BorderTopLeftRadius = ResolveBorderRadius(value, parentStyle);
                break;
            case CssPropertyNames.BorderTopRightRadius:
                style.BorderTopRightRadius = ResolveBorderRadius(value, parentStyle);
                break;
            case CssPropertyNames.BorderBottomRightRadius:
                style.BorderBottomRightRadius = ResolveBorderRadius(value, parentStyle);
                break;
            case CssPropertyNames.BorderBottomLeftRadius:
                style.BorderBottomLeftRadius = ResolveBorderRadius(value, parentStyle);
                break;

            default:
                return false;
        }
        return true;
    }
}
