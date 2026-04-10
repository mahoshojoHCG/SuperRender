using SuperRender.Core.Layout;

namespace SuperRender.Core.Painting;

public sealed class Painter
{
    public PaintList Paint(LayoutBox root)
    {
        var list = new PaintList();
        PaintBox(root, list);
        return list;
    }

    private void PaintBox(LayoutBox box, PaintList list)
    {
        if (box.Style.Display == DisplayType.None)
            return;

        // 1. Paint background
        PaintBackground(box, list);

        // 2. Paint border
        PaintBorder(box, list);

        // 3. Paint children (recursively)
        foreach (var child in box.Children)
        {
            PaintBox(child, list);
        }

        // 4. Paint text runs
        PaintTextRuns(box, list);
    }

    private static void PaintBackground(LayoutBox box, PaintList list)
    {
        var bg = box.Style.BackgroundColor;
        if (bg.A <= 0) return;

        var rect = box.Dimensions.BorderRect;
        if (rect.Width <= 0 || rect.Height <= 0) return;

        list.Add(new FillRectCommand { Rect = rect, Color = bg });
    }

    private static void PaintBorder(LayoutBox box, PaintList list)
    {
        var style = box.Style;
        if (style.BorderStyle == "none" || style.BorderStyle == "hidden")
            return;

        var color = style.BorderColor;
        if (color.A <= 0) return;

        var border = style.BorderWidth;
        var borderRect = box.Dimensions.BorderRect;
        var paddingRect = box.Dimensions.PaddingRect;

        // Top border
        if (border.Top > 0)
        {
            list.Add(new FillRectCommand
            {
                Rect = new RectF(borderRect.X, borderRect.Y, borderRect.Width, border.Top),
                Color = color,
            });
        }

        // Bottom border
        if (border.Bottom > 0)
        {
            list.Add(new FillRectCommand
            {
                Rect = new RectF(borderRect.X, paddingRect.Bottom, borderRect.Width, border.Bottom),
                Color = color,
            });
        }

        // Left border
        if (border.Left > 0)
        {
            list.Add(new FillRectCommand
            {
                Rect = new RectF(borderRect.X, borderRect.Y, border.Left, borderRect.Height),
                Color = color,
            });
        }

        // Right border
        if (border.Right > 0)
        {
            list.Add(new FillRectCommand
            {
                Rect = new RectF(paddingRect.Right, borderRect.Y, border.Right, borderRect.Height),
                Color = color,
            });
        }
    }

    private static void PaintTextRuns(LayoutBox box, PaintList list)
    {
        if (box.TextRuns == null) return;

        foreach (var run in box.TextRuns)
        {
            if (string.IsNullOrWhiteSpace(run.Text)) continue;

            list.Add(new DrawTextCommand
            {
                Text = run.Text,
                X = run.X,
                Y = run.Y,
                FontSize = run.Style.FontSize,
                Color = run.Style.Color,
            });
        }
    }
}
