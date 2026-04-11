using SuperRender.Core.Dom;
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

        // 3. Clip if overflow:hidden
        bool clipping = box.Style.Overflow == OverflowType.Hidden;
        if (clipping)
        {
            list.Add(new PushClipCommand { Rect = box.Dimensions.PaddingRect });
        }

        // 4. Paint list marker for <li> elements
        PaintListMarker(box, list);

        // 5. Paint text runs (inside clip region, before children so positioned children render on top)
        PaintTextRuns(box, list);

        // 5. Paint children with z-index ordering for positioned elements
        PaintChildren(box, list);

        // 6. Pop clip
        if (clipping)
        {
            list.Add(new PopClipCommand());
        }
    }

    private static void PaintChildren(LayoutBox box, PaintList list)
    {
        // Separate children into non-positioned and positioned
        var nonPositioned = new List<LayoutBox>();
        var positioned = new List<LayoutBox>();

        foreach (var child in box.Children)
        {
            if (child.Style.Position != PositionType.Static)
                positioned.Add(child);
            else
                nonPositioned.Add(child);
        }

        // Paint non-positioned children first (normal flow)
        foreach (var child in nonPositioned)
        {
            PaintBox(child, list);
        }

        // Sort positioned children by z-index and paint in a new rendering segment
        // (forces quads+text together so positioned elements render on top of normal flow)
        if (positioned.Count > 0)
        {
            positioned.Sort((a, b) =>
            {
                var za = a.Style.ZIndexIsAuto ? 0 : a.Style.ZIndex;
                var zb = b.Style.ZIndexIsAuto ? 0 : b.Style.ZIndex;
                return za.CompareTo(zb);
            });

            // Push a full-viewport clip to start a new rendering segment
            list.Add(new PushClipCommand
            {
                Rect = new RectF(-100000, -100000, 200000, 200000),
            });

            foreach (var child in positioned)
            {
                PaintBox(child, list);
            }

            list.Add(new PopClipCommand());
        }
    }

    private static void PaintBackground(LayoutBox box, PaintList list)
    {
        var bg = box.Style.BackgroundColor;
        if (bg.A <= 0) return;

        // Skip box-level background for inline elements with text runs —
        // their backgrounds are painted per-run in PaintTextRuns for tighter highlighting
        if (box.Style.Display == DisplayType.Inline && box.TextRuns is { Count: > 0 })
            return;

        var rect = box.Dimensions.BorderRect;
        if (rect.Width <= 0 || rect.Height <= 0) return;

        list.Add(new FillRectCommand { Rect = rect, Color = bg });
    }

    private static void PaintBorder(LayoutBox box, PaintList list)
    {
        var style = box.Style;
        var border = style.BorderWidth;
        var borderRect = box.Dimensions.BorderRect;
        var paddingRect = box.Dimensions.PaddingRect;

        // Top border
        if (border.Top > 0 && style.BorderTopStyle != "none" && style.BorderTopStyle != "hidden"
            && style.BorderTopColor.A > 0)
        {
            list.Add(new FillRectCommand
            {
                Rect = new RectF(borderRect.X, borderRect.Y, borderRect.Width, border.Top),
                Color = style.BorderTopColor,
            });
        }

        // Bottom border
        if (border.Bottom > 0 && style.BorderBottomStyle != "none" && style.BorderBottomStyle != "hidden"
            && style.BorderBottomColor.A > 0)
        {
            list.Add(new FillRectCommand
            {
                Rect = new RectF(borderRect.X, paddingRect.Bottom, borderRect.Width, border.Bottom),
                Color = style.BorderBottomColor,
            });
        }

        // Left border
        if (border.Left > 0 && style.BorderLeftStyle != "none" && style.BorderLeftStyle != "hidden"
            && style.BorderLeftColor.A > 0)
        {
            list.Add(new FillRectCommand
            {
                Rect = new RectF(borderRect.X, borderRect.Y, border.Left, borderRect.Height),
                Color = style.BorderLeftColor,
            });
        }

        // Right border
        if (border.Right > 0 && style.BorderRightStyle != "none" && style.BorderRightStyle != "hidden"
            && style.BorderRightColor.A > 0)
        {
            list.Add(new FillRectCommand
            {
                Rect = new RectF(paddingRect.Right, borderRect.Y, border.Right, borderRect.Height),
                Color = style.BorderRightColor,
            });
        }
    }

    private static void PaintTextRuns(LayoutBox box, PaintList list)
    {
        if (box.TextRuns == null) return;

        foreach (var run in box.TextRuns)
        {
            // Paint per-run inline background (e.g. <mark> yellow highlight)
            if (run.Style.BackgroundColor.A > 0)
            {
                list.Add(new FillRectCommand
                {
                    Rect = new RectF(run.X, run.Y, run.Width, run.Style.FontSize),
                    Color = run.Style.BackgroundColor,
                });
            }

            // Always paint text decoration (so underlines span spaces in links)
            PaintTextDecoration(run, list);

            // Skip drawing invisible text, but decoration was already painted above
            if (string.IsNullOrWhiteSpace(run.Text)) continue;

            list.Add(new DrawTextCommand
            {
                Text = run.Text,
                X = run.X,
                Y = run.Y,
                FontSize = run.Style.FontSize,
                Color = run.Style.Color,
                FontWeight = run.Style.FontWeight,
                FontStyle = run.Style.FontStyle,
                FontFamily = run.Style.FontFamily,
                FontFamilies = run.Style.FontFamilies,
            });
        }
    }

    private static void PaintTextDecoration(TextRun run, PaintList list)
    {
        var decoration = run.Style.TextDecorationLine;
        if (decoration == TextDecorationLine.None) return;

        var color = run.Style.TextDecorationColor ?? run.Style.Color;
        float thickness = Math.Max(1f, run.Style.FontSize / 16f);

        if ((decoration & TextDecorationLine.Underline) != 0)
        {
            float underlineY = run.Y + run.Style.FontSize;
            list.Add(new FillRectCommand
            {
                Rect = new RectF(run.X, underlineY, run.Width, thickness),
                Color = color,
            });
        }

        if ((decoration & TextDecorationLine.LineThrough) != 0)
        {
            float strikeY = run.Y + run.Style.FontSize * 0.5f;
            list.Add(new FillRectCommand
            {
                Rect = new RectF(run.X, strikeY, run.Width, thickness),
                Color = color,
            });
        }

        if ((decoration & TextDecorationLine.Overline) != 0)
        {
            list.Add(new FillRectCommand
            {
                Rect = new RectF(run.X, run.Y, run.Width, thickness),
                Color = color,
            });
        }
    }

    private static void PaintListMarker(LayoutBox box, PaintList list)
    {
        if (box.DomNode is not Element el || el.TagName != "li") return;

        // Determine marker type from parent element
        var parent = el.Parent as Element;
        bool ordered = parent?.TagName == "ol";

        var dims = box.Dimensions;
        float fontSize = box.Style.FontSize;
        float markerX = dims.X - fontSize * 1.2f; // Position marker to the left of content
        float markerY = dims.Y;

        if (ordered)
        {
            // Compute item index (1-based)
            int index = 1;
            if (parent != null)
            {
                foreach (var sibling in parent.Children)
                {
                    if (sibling == el) break;
                    if (sibling is Element sibEl && sibEl.TagName == "li") index++;
                }
            }

            list.Add(new DrawTextCommand
            {
                Text = $"{index}.",
                X = markerX,
                Y = markerY,
                FontSize = fontSize,
                Color = box.Style.Color,
                FontFamilies = box.Style.FontFamilies,
                FontWeight = box.Style.FontWeight,
            });
        }
        else
        {
            // Bullet: draw a small filled circle (approximated by a unicode bullet character)
            list.Add(new DrawTextCommand
            {
                Text = "\u2022", // bullet character •
                X = markerX,
                Y = markerY,
                FontSize = fontSize,
                Color = box.Style.Color,
                FontFamilies = box.Style.FontFamilies,
                FontWeight = box.Style.FontWeight,
            });
        }
    }
}
