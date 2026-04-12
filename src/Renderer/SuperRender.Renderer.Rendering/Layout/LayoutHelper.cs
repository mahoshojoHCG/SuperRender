using SuperRender.Document.Css;
using SuperRender.Renderer.Rendering.Style;

namespace SuperRender.Renderer.Rendering.Layout;

/// <summary>
/// Shared helpers for resolving deferred calc/percentage values during layout.
/// </summary>
internal static class LayoutHelper
{
    /// <summary>
    /// Creates a CalcContext for evaluating deferred width/height expressions
    /// using the actual containing block size determined during layout.
    /// </summary>
    public static CalcContext MakeCalcContext(ComputedStyle style, double containingBlockSize)
    {
        return new CalcContext
        {
            FontSize = style.FontSize,
            ContainingBlockSize = containingBlockSize,
            ViewportWidth = 0,
            ViewportHeight = 0,
        };
    }

    /// <summary>
    /// Resolves a style Width, taking into account deferred calc/percentage expressions.
    /// Returns NaN if the width is auto (no explicit width and no calc expression).
    /// </summary>
    public static float ResolveWidth(ComputedStyle style, float containerWidth)
    {
        if (!float.IsNaN(style.Width))
            return style.Width;
        if (style.WidthCalc != null)
            return (float)style.WidthCalc.Evaluate(MakeCalcContext(style, containerWidth));
        return float.NaN;
    }

    /// <summary>
    /// Resolves a style Height, taking into account deferred calc/percentage expressions.
    /// Returns NaN if the height is auto (no explicit height and no calc expression).
    /// </summary>
    public static float ResolveHeight(ComputedStyle style, float containerHeight)
    {
        if (!float.IsNaN(style.Height))
            return style.Height;
        if (style.HeightCalc != null)
            return (float)style.HeightCalc.Evaluate(MakeCalcContext(style, containerHeight));
        return float.NaN;
    }
}
