using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;

namespace SuperRender.Renderer.Rendering.Painting;

public sealed class Painter
{
    public static PaintList Paint(LayoutBox root)
    {
        var list = new PaintList();
        PaintBox(root, list, 1f, null);
        return list;
    }

    /// <summary>
    /// Tracks the active rounded clip region from an ancestor with overflow:hidden + border-radius.
    /// Children's backgrounds are clipped to this rounded boundary.
    /// </summary>
    private sealed record RoundedClip(RectF Rect, float RadiusTL, float RadiusTR, float RadiusBR, float RadiusBL);

    private static void PaintBox(LayoutBox box, PaintList list, float parentOpacity, RoundedClip? roundedClip)
    {
        if (box.Style.Display == DisplayType.None)
            return;

        // visibility: hidden — skip painting but keep layout space; children CAN override
        bool isHidden = box.Style.Visibility == VisibilityType.Hidden;

        // Effective opacity: multiply with parent (opacity is composited)
        float effectiveOpacity = parentOpacity * box.Style.Opacity;

        // 1. Paint background (if visible)
        if (!isHidden)
            PaintBackground(box, list, effectiveOpacity, roundedClip);

        // 2. Paint border (if visible)
        if (!isHidden)
            PaintBorder(box, list, effectiveOpacity);

        // 3. Clip if overflow:hidden
        bool clipping = box.Style.Overflow == OverflowType.Hidden;
        RoundedClip? childClip = roundedClip;
        if (clipping)
        {
            list.Add(new PushClipCommand { Rect = box.Dimensions.PaddingRect });

            // Establish a new rounded clip for children when overflow:hidden + border-radius
            var borderRect = box.Dimensions.BorderRect;
            var (rtl, rtr, rbr, rbl) = ResolveBorderRadii(box.Style, borderRect.Width, borderRect.Height);
            if (rtl > 0 || rtr > 0 || rbr > 0 || rbl > 0)
            {
                var bw = box.Style.BorderWidth;
                var pr = box.Dimensions.PaddingRect;
                childClip = new RoundedClip(pr,
                    Math.Max(0, rtl - Math.Max(bw.Top, bw.Left)),
                    Math.Max(0, rtr - Math.Max(bw.Top, bw.Right)),
                    Math.Max(0, rbr - Math.Max(bw.Bottom, bw.Right)),
                    Math.Max(0, rbl - Math.Max(bw.Bottom, bw.Left)));
            }
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
        PaintChildren(box, list, effectiveOpacity, childClip);

        // 7. Pop clip
        if (clipping)
        {
            list.Add(new PopClipCommand());
        }
    }

    private static void PaintChildren(LayoutBox box, PaintList list, float parentOpacity, RoundedClip? roundedClip)
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
            PaintBox(child, list, parentOpacity, roundedClip);
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
                PaintBox(child, list, parentOpacity, roundedClip);
            }

            list.Add(new PopClipCommand());
        }
    }

    private static void PaintBackground(LayoutBox box, PaintList list, float opacity, RoundedClip? roundedClip)
    {
        var bg = box.Style.BackgroundColor;

        if (box.Style.Display == DisplayType.Inline && box.TextRuns is { Count: > 0 })
            return;

        var borderRect = box.Dimensions.BorderRect;
        if (borderRect.Width <= 0 || borderRect.Height <= 0) return;

        var (rtl, rtr, rbr, rbl) = ResolveBorderRadii(box.Style, borderRect.Width, borderRect.Height);
        bool hasRadius = rtl > 0 || rtr > 0 || rbr > 0 || rbl > 0;

        // Apply parent's rounded clip to this child's background.
        // When the child's edge touches the parent's rounded clip boundary,
        // inherit the parent's corner radius for that corner.
        if (roundedClip != null && !hasRadius)
        {
            var c = roundedClip;
            const float tolerance = 1f;
            bool touchesTop = Math.Abs(borderRect.Y - c.Rect.Y) < tolerance;
            bool touchesBottom = Math.Abs(borderRect.Bottom - c.Rect.Bottom) < tolerance;
            bool touchesLeft = Math.Abs(borderRect.X - c.Rect.X) < tolerance;
            bool touchesRight = Math.Abs(borderRect.Right - c.Rect.Right) < tolerance;

            if (touchesTop && touchesLeft) rtl = c.RadiusTL;
            if (touchesTop && touchesRight) rtr = c.RadiusTR;
            if (touchesBottom && touchesRight) rbr = c.RadiusBR;
            if (touchesBottom && touchesLeft) rbl = c.RadiusBL;
            hasRadius = rtl > 0 || rtr > 0 || rbr > 0 || rbl > 0;
        }

        // For elements with border-radius AND visible SOLID border, render the border as a
        // filled outer rounded rect, then the background as a filled inner rounded rect
        // on top. This avoids the problem of a rounded StrokeRectCommand being rendered
        // as a filled rect that covers the background.
        // Dashed borders with radius fall through to PaintBorder for per-side rendering.
        if (hasRadius && HasVisibleBorder(box.Style) && !HasDashedBorder(box.Style))
        {
            var style = box.Style;
            var border = style.BorderWidth;

            // Determine border color (use first available side)
            Document.Color borderColor = Document.Color.Transparent;
            if (border.Top > 0 && style.BorderTopStyle is not "none" and not "hidden")
                borderColor = style.BorderTopColor;
            if (borderColor.A <= 0 && border.Right > 0 && style.BorderRightStyle is not "none" and not "hidden")
                borderColor = style.BorderRightColor;
            if (borderColor.A <= 0 && border.Bottom > 0 && style.BorderBottomStyle is not "none" and not "hidden")
                borderColor = style.BorderBottomColor;
            if (borderColor.A <= 0 && border.Left > 0 && style.BorderLeftStyle is not "none" and not "hidden")
                borderColor = style.BorderLeftColor;

            // 1. Outer fill: border color at border rect with outer radii
            if (borderColor.A > 0)
            {
                list.Add(new FillRectCommand
                {
                    Rect = borderRect,
                    Color = ApplyOpacity(borderColor, opacity),
                    RadiusTL = rtl, RadiusTR = rtr, RadiusBR = rbr, RadiusBL = rbl,
                });
            }

            // 2. Inner fill: background color at padding rect with inner radii
            if (bg.A > 0)
            {
                var paddingRect = box.Dimensions.PaddingRect;
                if (paddingRect.Width > 0 && paddingRect.Height > 0)
                {
                    // Inner radii are reduced by the border width (clamped to 0)
                    float irtl = Math.Max(0, rtl - Math.Max(border.Top, border.Left));
                    float irtr = Math.Max(0, rtr - Math.Max(border.Top, border.Right));
                    float irbr = Math.Max(0, rbr - Math.Max(border.Bottom, border.Right));
                    float irbl = Math.Max(0, rbl - Math.Max(border.Bottom, border.Left));

                    list.Add(new FillRectCommand
                    {
                        Rect = paddingRect,
                        Color = ApplyOpacity(bg, opacity),
                        RadiusTL = irtl, RadiusTR = irtr, RadiusBR = irbr, RadiusBL = irbl,
                    });
                }
            }

            return; // Border is handled here, PaintBorder will skip rounded borders
        }

        // Normal case: no border-radius or no visible border
        if (bg.A <= 0) return;

        list.Add(new FillRectCommand
        {
            Rect = borderRect, Color = ApplyOpacity(bg, opacity),
            RadiusTL = rtl, RadiusTR = rtr, RadiusBR = rbr, RadiusBL = rbl,
        });
    }

    private static bool HasVisibleBorder(ComputedStyle style)
    {
        var border = style.BorderWidth;
        return (border.Top > 0 && style.BorderTopStyle is not "none" and not "hidden")
            || (border.Right > 0 && style.BorderRightStyle is not "none" and not "hidden")
            || (border.Bottom > 0 && style.BorderBottomStyle is not "none" and not "hidden")
            || (border.Left > 0 && style.BorderLeftStyle is not "none" and not "hidden");
    }

    private static bool HasDashedBorder(ComputedStyle style)
    {
        return style.BorderTopStyle == "dashed" || style.BorderRightStyle == "dashed"
            || style.BorderBottomStyle == "dashed" || style.BorderLeftStyle == "dashed";
    }

    private static void PaintBorder(LayoutBox box, PaintList list, float opacity = 1f)
    {
        var style = box.Style;
        var border = style.BorderWidth;
        var borderRect = box.Dimensions.BorderRect;
        var paddingRect = box.Dimensions.PaddingRect;

        var (rtl, rtr, rbr, rbl) = ResolveBorderRadii(style, borderRect.Width, borderRect.Height);
        bool hasRadius = rtl > 0 || rtr > 0 || rbr > 0 || rbl > 0;

        // When border-radius is present and border is solid, PaintBackground already
        // renders the border as an outer fill + inner fill pair. Skip here.
        // Dashed borders still need per-side rendering even with radius.
        if (hasRadius && !HasDashedBorder(style))
        {
            return;
        }

        // Top border
        if (border.Top > 0 && style.BorderTopStyle != "none" && style.BorderTopStyle != "hidden"
            && style.BorderTopColor.A > 0)
        {
            var color = ApplyOpacity(style.BorderTopColor, opacity);
            float insetL = hasRadius ? rtl : 0;
            float insetR = hasRadius ? rtr : 0;
            if (style.BorderTopStyle == "dashed")
                EmitDashedHorizontal(list, borderRect.X + insetL, borderRect.Y,
                    borderRect.Width - insetL - insetR, border.Top, color);
            else
                list.Add(new FillRectCommand
                {
                    Rect = new RectF(borderRect.X, borderRect.Y, borderRect.Width, border.Top),
                    Color = color,
                });
        }

        // Bottom border
        if (border.Bottom > 0 && style.BorderBottomStyle != "none" && style.BorderBottomStyle != "hidden"
            && style.BorderBottomColor.A > 0)
        {
            var color = ApplyOpacity(style.BorderBottomColor, opacity);
            float insetL = hasRadius ? rbl : 0;
            float insetR = hasRadius ? rbr : 0;
            if (style.BorderBottomStyle == "dashed")
                EmitDashedHorizontal(list, borderRect.X + insetL, paddingRect.Bottom,
                    borderRect.Width - insetL - insetR, border.Bottom, color);
            else
                list.Add(new FillRectCommand
                {
                    Rect = new RectF(borderRect.X, paddingRect.Bottom, borderRect.Width, border.Bottom),
                    Color = color,
                });
        }

        // Left border
        if (border.Left > 0 && style.BorderLeftStyle != "none" && style.BorderLeftStyle != "hidden"
            && style.BorderLeftColor.A > 0)
        {
            var color = ApplyOpacity(style.BorderLeftColor, opacity);
            float insetT = hasRadius ? rtl : 0;
            float insetB = hasRadius ? rbl : 0;
            if (style.BorderLeftStyle == "dashed")
                EmitDashedVertical(list, borderRect.X, borderRect.Y + insetT,
                    border.Left, borderRect.Height - insetT - insetB, color);
            else
                list.Add(new FillRectCommand
                {
                    Rect = new RectF(borderRect.X, borderRect.Y, border.Left, borderRect.Height),
                    Color = color,
                });
        }

        // Right border
        if (border.Right > 0 && style.BorderRightStyle != "none" && style.BorderRightStyle != "hidden"
            && style.BorderRightColor.A > 0)
        {
            var color = ApplyOpacity(style.BorderRightColor, opacity);
            float insetT = hasRadius ? rtr : 0;
            float insetB = hasRadius ? rbr : 0;
            if (style.BorderRightStyle == "dashed")
                EmitDashedVertical(list, paddingRect.Right, borderRect.Y + insetT,
                    border.Right, borderRect.Height - insetT - insetB, color);
            else
                list.Add(new FillRectCommand
                {
                    Rect = new RectF(paddingRect.Right, borderRect.Y, border.Right, borderRect.Height),
                    Color = color,
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

    /// <summary>
    /// Resolves border-radius values to pixels. Negative stored values indicate
    /// percentages (e.g., -50 → 50% of the element's minimum dimension).
    /// Clamps each radius to half the box dimension to avoid overlap.
    /// </summary>
    private static (float tl, float tr, float br, float bl) ResolveBorderRadii(
        ComputedStyle style, float width, float height)
    {
        float tl = ResolveRadius(style.BorderTopLeftRadius, width, height);
        float tr = ResolveRadius(style.BorderTopRightRadius, width, height);
        float br = ResolveRadius(style.BorderBottomRightRadius, width, height);
        float bl = ResolveRadius(style.BorderBottomLeftRadius, width, height);

        // Clamp so adjacent radii don't exceed the side length (CSS spec)
        float maxH = width;
        float maxV = height;
        float topSum = tl + tr;
        float bottomSum = bl + br;
        float leftSum = tl + bl;
        float rightSum = tr + br;
        float factor = 1f;
        if (topSum > maxH) factor = Math.Min(factor, maxH / topSum);
        if (bottomSum > maxH) factor = Math.Min(factor, maxH / bottomSum);
        if (leftSum > maxV) factor = Math.Min(factor, maxV / leftSum);
        if (rightSum > maxV) factor = Math.Min(factor, maxV / rightSum);
        if (factor < 1f)
        {
            tl *= factor; tr *= factor; br *= factor; bl *= factor;
        }

        return (tl, tr, br, bl);
    }

    private static float ResolveRadius(float value, float width, float height)
    {
        if (value == 0) return 0;
        if (value < 0)
        {
            // Negative = percentage: resolve against minimum dimension for symmetry
            float pct = -value / 100f;
            return pct * Math.Min(width, height);
        }
        return value;
    }

    /// <summary>
    /// Emits a series of dash-gap FillRects along a horizontal border.
    /// Dash length = 3 * thickness, gap = dash length.
    /// </summary>
    private static void EmitDashedHorizontal(PaintList list, float x, float y, float totalWidth, float thickness, Document.Color color)
    {
        float dashLen = Math.Max(4, thickness * 3);
        float gapLen = dashLen;
        float cursor = 0;
        while (cursor < totalWidth)
        {
            float w = Math.Min(dashLen, totalWidth - cursor);
            list.Add(new FillRectCommand
            {
                Rect = new RectF(x + cursor, y, w, thickness),
                Color = color,
            });
            cursor += dashLen + gapLen;
        }
    }

    /// <summary>
    /// Emits a series of dash-gap FillRects along a vertical border.
    /// </summary>
    private static void EmitDashedVertical(PaintList list, float x, float y, float thickness, float totalHeight, Document.Color color)
    {
        float dashLen = Math.Max(4, thickness * 3);
        float gapLen = dashLen;
        float cursor = 0;
        while (cursor < totalHeight)
        {
            float h = Math.Min(dashLen, totalHeight - cursor);
            list.Add(new FillRectCommand
            {
                Rect = new RectF(x, y + cursor, thickness, h),
                Color = color,
            });
            cursor += dashLen + gapLen;
        }
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
