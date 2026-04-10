using SuperRender.Core.Layout;
using SuperRender.Core.Style;

namespace SuperRender.Core.Painting;

public sealed class Painter
{
    public static PaintList Paint(LayoutBox root)
    {
        var list = new PaintList();
        PaintBox(root, list);
        return list;
    }

    private static void PaintBox(LayoutBox box, PaintList list)
    {
        if (box.Style.Display == DisplayType.None)
            return;

        // 1. Paint background
        PaintBackground(box, list);

        // 2. Paint border
        PaintBorder(box, list);

        // Emit clip if overflow is not visible
        bool clipped = box.Style.Overflow != OverflowType.Visible;
        if (clipped)
        {
            list.Add(new ClipRectCommand { Rect = box.Dimensions.PaddingRect });
        }

        // 3. Separate children into non-positioned and positioned for z-index ordering
        var nonPositioned = new List<LayoutBox>();
        var positioned = new List<LayoutBox>();

        foreach (var child in box.Children)
        {
            if (child.Style.Position != PositionType.Static)
                positioned.Add(child);
            else
                nonPositioned.Add(child);
        }

        // Paint non-positioned children first
        foreach (var child in nonPositioned)
        {
            PaintBox(child, list);
        }

        // Paint positioned children in z-index order
        positioned.Sort((a, b) => a.Style.ZIndex.CompareTo(b.Style.ZIndex));
        foreach (var child in positioned)
        {
            PaintBox(child, list);
        }

        // Restore clip
        if (clipped)
        {
            list.Add(new RestoreClipCommand());
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

            var style = run.Style;

            list.Add(new DrawTextCommand
            {
                Text = run.Text,
                X = run.X,
                Y = run.Y,
                FontSize = style.FontSize,
                Color = style.Color,
                FontWeight = style.FontWeight,
                FontStyle = style.FontStyle,
                TextDecoration = style.TextDecoration,
            });

            // Draw text decorations as thin rectangles
            if (style.TextDecoration != TextDecorationLine.None)
            {
                float lineThickness = Math.Max(1f, style.FontSize / 14f);
                var decoColor = style.Color;

                if (style.TextDecoration.HasFlag(TextDecorationLine.Underline))
                {
                    float underlineY = run.Y + style.FontSize * 0.9f;
                    list.Add(new FillRectCommand
                    {
                        Rect = new RectF(run.X, underlineY, run.Width, lineThickness),
                        Color = decoColor,
                    });
                }

                if (style.TextDecoration.HasFlag(TextDecorationLine.LineThrough))
                {
                    float strikeY = run.Y + style.FontSize * 0.5f;
                    list.Add(new FillRectCommand
                    {
                        Rect = new RectF(run.X, strikeY, run.Width, lineThickness),
                        Color = decoColor,
                    });
                }

                if (style.TextDecoration.HasFlag(TextDecorationLine.Overline))
                {
                    list.Add(new FillRectCommand
                    {
                        Rect = new RectF(run.X, run.Y, run.Width, lineThickness),
                        Color = decoColor,
                    });
                }
            }
        }
    }
}
