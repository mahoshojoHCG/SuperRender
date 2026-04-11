using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;

namespace SuperRender.Renderer.Rendering.Painting;

public sealed class Painter
{
    public static PaintList Paint(LayoutBox root)
    {
        var list = new PaintList();
        PaintBox(root, list, 1f);
        return list;
    }

    private static void PaintBox(LayoutBox box, PaintList list, float parentOpacity = 1f)
    {
        if (box.Style.Display == DisplayType.None)
            return;

        // visibility: hidden — skip painting but keep layout space; children CAN override
        bool isHidden = box.Style.Visibility == VisibilityType.Hidden;

        // Effective opacity: multiply with parent (opacity is composited)
        float effectiveOpacity = parentOpacity * box.Style.Opacity;

        // 1. Paint background (if visible)
        if (!isHidden)
            PaintBackground(box, list, effectiveOpacity);

        // 2. Paint border (if visible)
        if (!isHidden)
            PaintBorder(box, list, effectiveOpacity);

        // 3. Clip if overflow:hidden
        bool clipping = box.Style.Overflow == OverflowType.Hidden;
        if (clipping)
        {
            list.Add(new PushClipCommand { Rect = box.Dimensions.PaddingRect });
        }

        // 4. Paint list marker for <li> elements (if visible)
        if (!isHidden)
            PaintListMarker(box, list, effectiveOpacity);

        // 5. Paint text runs (if visible)
        if (!isHidden)
            PaintTextRuns(box, list, effectiveOpacity);

        // 5b. Paint image for <img> elements (if visible)
        if (!isHidden)
            PaintImage(box, list, effectiveOpacity);

        // 6. Paint children with z-index ordering for positioned elements
        PaintChildren(box, list, effectiveOpacity);

        // 7. Pop clip
        if (clipping)
        {
            list.Add(new PopClipCommand());
        }
    }

    private static void PaintChildren(LayoutBox box, PaintList list, float parentOpacity)
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
            PaintBox(child, list, parentOpacity);
        }

        // Sort positioned children by z-index and paint in a new rendering segment
        if (positioned.Count > 0)
        {
            positioned.Sort((a, b) =>
            {
                var za = a.Style.ZIndexIsAuto ? 0 : a.Style.ZIndex;
                var zb = b.Style.ZIndexIsAuto ? 0 : b.Style.ZIndex;
                return za.CompareTo(zb);
            });

            list.Add(new PushClipCommand
            {
                Rect = new RectF(-100000, -100000, 200000, 200000),
            });

            foreach (var child in positioned)
            {
                PaintBox(child, list, parentOpacity);
            }

            list.Add(new PopClipCommand());
        }
    }

    private static void PaintBackground(LayoutBox box, PaintList list, float opacity = 1f)
    {
        var bg = box.Style.BackgroundColor;
        if (bg.A <= 0) return;

        if (box.Style.Display == DisplayType.Inline && box.TextRuns is { Count: > 0 })
            return;

        var rect = box.Dimensions.BorderRect;
        if (rect.Width <= 0 || rect.Height <= 0) return;

        list.Add(new FillRectCommand { Rect = rect, Color = ApplyOpacity(bg, opacity) });
    }

    private static void PaintBorder(LayoutBox box, PaintList list, float opacity = 1f)
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
                Color = ApplyOpacity(style.BorderTopColor, opacity),
            });
        }

        // Bottom border
        if (border.Bottom > 0 && style.BorderBottomStyle != "none" && style.BorderBottomStyle != "hidden"
            && style.BorderBottomColor.A > 0)
        {
            list.Add(new FillRectCommand
            {
                Rect = new RectF(borderRect.X, paddingRect.Bottom, borderRect.Width, border.Bottom),
                Color = ApplyOpacity(style.BorderBottomColor, opacity),
            });
        }

        // Left border
        if (border.Left > 0 && style.BorderLeftStyle != "none" && style.BorderLeftStyle != "hidden"
            && style.BorderLeftColor.A > 0)
        {
            list.Add(new FillRectCommand
            {
                Rect = new RectF(borderRect.X, borderRect.Y, border.Left, borderRect.Height),
                Color = ApplyOpacity(style.BorderLeftColor, opacity),
            });
        }

        // Right border
        if (border.Right > 0 && style.BorderRightStyle != "none" && style.BorderRightStyle != "hidden"
            && style.BorderRightColor.A > 0)
        {
            list.Add(new FillRectCommand
            {
                Rect = new RectF(paddingRect.Right, borderRect.Y, border.Right, borderRect.Height),
                Color = ApplyOpacity(style.BorderRightColor, opacity),
            });
        }
    }

    private static void PaintTextRuns(LayoutBox box, PaintList list, float opacity = 1f)
    {
        if (box.TextRuns == null) return;

        foreach (var run in box.TextRuns)
        {
            if (run.Style.BackgroundColor.A > 0)
            {
                list.Add(new FillRectCommand
                {
                    Rect = new RectF(run.X, run.Y, run.Width, run.Style.FontSize),
                    Color = ApplyOpacity(run.Style.BackgroundColor, opacity),
                });
            }

            PaintTextDecoration(run, list, opacity);

            if (string.IsNullOrWhiteSpace(run.Text)) continue;

            list.Add(new DrawTextCommand
            {
                Text = run.Text,
                X = run.X,
                Y = run.Y,
                FontSize = run.Style.FontSize,
                Color = ApplyOpacity(run.Style.Color, opacity),
                FontWeight = run.Style.FontWeight,
                FontStyle = run.Style.FontStyle,
                FontFamily = run.Style.FontFamily,
                FontFamilies = run.Style.FontFamilies,
                LetterSpacing = run.Style.LetterSpacing,
                WordSpacing = run.Style.WordSpacing,
            });
        }
    }

    private static void PaintTextDecoration(TextRun run, PaintList list, float opacity = 1f)
    {
        var decoration = run.Style.TextDecorationLine;
        if (decoration == TextDecorationLine.None) return;

        var color = ApplyOpacity(run.Style.TextDecorationColor ?? run.Style.Color, opacity);
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

    private static void PaintListMarker(LayoutBox box, PaintList list, float opacity = 1f)
    {
        if (box.DomNode is not Element el || el.TagName != "li") return;

        var parent = el.Parent as Element;
        bool ordered = parent?.TagName == "ol";

        var dims = box.Dimensions;
        float fontSize = box.Style.FontSize;
        float markerX = dims.X - fontSize * 1.2f;
        float markerY = dims.Y;
        var color = ApplyOpacity(box.Style.Color, opacity);

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
                Color = color,
                FontFamilies = box.Style.FontFamilies,
                FontWeight = box.Style.FontWeight,
            });
        }
        else
        {
            list.Add(new DrawTextCommand
            {
                Text = "\u2022",
                X = markerX,
                Y = markerY,
                FontSize = fontSize,
                Color = color,
                FontFamilies = box.Style.FontFamilies,
                FontWeight = box.Style.FontWeight,
            });
        }
    }

    private static Document.Color ApplyOpacity(Document.Color color, float opacity)
    {
        if (opacity >= 1f) return color;
        return new Document.Color(color.R, color.G, color.B, color.A * opacity);
    }

    private static void PaintImage(LayoutBox box, PaintList list, float opacity = 1f)
    {
        if (box.DomNode is not Element el || el.TagName != "img") return;

        var src = el.GetAttribute("src");
        var contentRect = box.Dimensions.ContentRect;

        if (!string.IsNullOrWhiteSpace(src) && contentRect.Width > 0 && contentRect.Height > 0)
        {
            list.Add(new DrawImageCommand
            {
                ImageUrl = src,
                Rect = contentRect,
                Opacity = opacity,
            });
        }
        else
        {
            // Alt text fallback when no image loaded
            var alt = el.GetAttribute("alt");
            if (!string.IsNullOrWhiteSpace(alt) && contentRect.Width > 0 && contentRect.Height > 0)
            {
                // Light gray placeholder box
                var placeholderColor = new Document.Color(0.93f, 0.93f, 0.93f, 1f);
                list.Add(new FillRectCommand
                {
                    Rect = contentRect,
                    Color = ApplyOpacity(placeholderColor, opacity),
                });

                // Border
                var borderColor = new Document.Color(0.8f, 0.8f, 0.8f, 1f);
                list.Add(new StrokeRectCommand
                {
                    Rect = contentRect,
                    Color = ApplyOpacity(borderColor, opacity),
                });

                // Alt text centered
                var fontSize = box.Style.FontSize;
                list.Add(new DrawTextCommand
                {
                    Text = alt,
                    X = contentRect.X + 4,
                    Y = contentRect.Y + 4,
                    FontSize = fontSize,
                    Color = ApplyOpacity(new Document.Color(0.4f, 0.4f, 0.4f, 1f), opacity),
                    FontFamilies = box.Style.FontFamilies,
                    FontWeight = box.Style.FontWeight,
                });
            }
        }
    }
}
