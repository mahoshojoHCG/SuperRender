namespace SuperRender.Renderer.Rendering.Layout;

/// <summary>
/// CSS float layout. Float boxes are removed from normal flow and positioned left/right.
/// Subsequent inline content flows around floats.
/// </summary>
public static class FloatLayout
{
    /// <summary>
    /// Collects float children from a block container and positions them.
    /// Returns the list of active floats for inline content to flow around.
    /// </summary>
    public static List<FloatBox> CollectFloats(LayoutBox container, BoxDimensions containingBlock, ITextMeasurer measurer)
    {
        var floats = new List<FloatBox>();
        float leftOffset = 0;
        float rightOffset = 0;
        float currentY = containingBlock.Y;

        foreach (var child in container.Children)
        {
            if (child.Style.Float == "none") continue;

            // Layout the float to determine its size
            BlockLayout.Layout(child, containingBlock, measurer);

            var dims = child.Dimensions;
            float floatWidth = dims.Width + dims.HorizontalEdge;
            float floatHeight = dims.Height + dims.VerticalEdge;

            if (child.Style.Float == "left")
            {
                dims.X = containingBlock.X + leftOffset + dims.Margin.Left + dims.Border.Left + dims.Padding.Left;
                dims.Y = currentY + dims.Margin.Top + dims.Border.Top + dims.Padding.Top;
                child.Dimensions = dims;
                leftOffset += floatWidth;

                floats.Add(new FloatBox(child, FloatSide.Left,
                    containingBlock.X + leftOffset - floatWidth, currentY, floatWidth, floatHeight));
            }
            else if (child.Style.Float == "right")
            {
                dims.X = containingBlock.X + containingBlock.Width - rightOffset - floatWidth
                    + dims.Margin.Left + dims.Border.Left + dims.Padding.Left;
                dims.Y = currentY + dims.Margin.Top + dims.Border.Top + dims.Padding.Top;
                child.Dimensions = dims;
                rightOffset += floatWidth;

                floats.Add(new FloatBox(child, FloatSide.Right,
                    dims.X - dims.Margin.Left - dims.Border.Left - dims.Padding.Left, currentY, floatWidth, floatHeight));
            }
        }

        return floats;
    }

    /// <summary>
    /// Returns the available width at the given Y coordinate, accounting for active floats.
    /// </summary>
    public static (float x, float width) GetAvailableWidth(List<FloatBox> floats, float y, float containerX, float containerWidth)
    {
        float leftInset = 0;
        float rightInset = 0;

        foreach (var fb in floats)
        {
            if (y < fb.Y || y >= fb.Y + fb.Height) continue;

            if (fb.Side == FloatSide.Left)
                leftInset = Math.Max(leftInset, fb.X + fb.Width - containerX);
            else
                rightInset = Math.Max(rightInset, containerX + containerWidth - fb.X);
        }

        return (containerX + leftInset, containerWidth - leftInset - rightInset);
    }

    /// <summary>
    /// Computes the Y position below all floats matching the given clear value.
    /// </summary>
    public static float ClearFloats(List<FloatBox> floats, string clearValue)
    {
        float maxY = 0;
        foreach (var fb in floats)
        {
            bool matches = clearValue switch
            {
                "left" => fb.Side == FloatSide.Left,
                "right" => fb.Side == FloatSide.Right,
                "both" => true,
                _ => false,
            };
            if (matches)
                maxY = Math.Max(maxY, fb.Y + fb.Height);
        }
        return maxY;
    }
}

public enum FloatSide { Left, Right }

public sealed class FloatBox
{
    public LayoutBox Box { get; }
    public FloatSide Side { get; }
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }

    public FloatBox(LayoutBox box, FloatSide side, float x, float y, float width, float height)
    {
        Box = box; Side = side; X = x; Y = y; Width = width; Height = height;
    }
}
