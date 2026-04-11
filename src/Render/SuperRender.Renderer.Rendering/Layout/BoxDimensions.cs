namespace SuperRender.Renderer.Rendering.Layout;

public struct EdgeSizes
{
    public float Top { get; set; }
    public float Right { get; set; }
    public float Bottom { get; set; }
    public float Left { get; set; }

    public EdgeSizes(float top, float right, float bottom, float left)
    {
        Top = top; Right = right; Bottom = bottom; Left = left;
    }

    public EdgeSizes(float all)
    {
        Top = Right = Bottom = Left = all;
    }

    public EdgeSizes(float vertical, float horizontal)
    {
        Top = Bottom = vertical;
        Left = Right = horizontal;
    }

    public float HorizontalTotal => Left + Right;
    public float VerticalTotal => Top + Bottom;

    public static readonly EdgeSizes Zero = new(0, 0, 0, 0);
}

public struct RectF
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }

    public RectF(float x, float y, float width, float height)
    {
        X = x; Y = y; Width = width; Height = height;
    }

    public float Right => X + Width;
    public float Bottom => Y + Height;

    public RectF Expand(EdgeSizes edges)
    {
        return new RectF(
            X - edges.Left,
            Y - edges.Top,
            Width + edges.Left + edges.Right,
            Height + edges.Top + edges.Bottom);
    }
}

public struct BoxDimensions
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public EdgeSizes Padding { get; set; }
    public EdgeSizes Border { get; set; }
    public EdgeSizes Margin { get; set; }

    public RectF ContentRect => new(X, Y, Width, Height);

    public RectF PaddingRect => ContentRect.Expand(Padding);

    public RectF BorderRect => PaddingRect.Expand(Border);

    public RectF MarginRect => BorderRect.Expand(Margin);
}

public enum DisplayType { Block, Inline, InlineBlock, FlowRoot, None }
