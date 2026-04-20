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

        // Push transform if present
        bool hasTransform = box.Style.Transform is { Count: > 0 };
        if (hasTransform)
        {
            var inner = TransformMatrix.Compose(box.Style.Transform!.Select(f => f.ToMatrix()));

            // Apply transform-origin: result = T(origin) * M * T(-origin)
            // Origin is resolved against the element's border box in absolute coordinates.
            var borderRect = box.Dimensions.BorderRect;
            float ox = borderRect.X + borderRect.Width * (box.Style.TransformOriginX / 100f);
            float oy = borderRect.Y + borderRect.Height * (box.Style.TransformOriginY / 100f);

            var pre = TransformMatrix.CreateTranslation(-ox, -oy);
            var post = TransformMatrix.CreateTranslation(ox, oy);
            var final = post.Multiply(inner).Multiply(pre);

            list.Add(new PushTransformCommand { Matrix4x4 = final.Elements });
        }

        // Push filter if present
        bool hasFilter = box.Style.Filter is { Count: > 0 };
        if (hasFilter)
        {
            list.Add(new PushFilterCommand { Filters = box.Style.Filter! });
        }

        // 0. Paint box shadows (before background, outer shadows only)
        if (!isHidden)
            PaintBoxShadow(box, list, effectiveOpacity);

        // 1. Paint background (if visible)
        if (!isHidden)
            PaintBackground(box, list, effectiveOpacity, roundedClip);

        // 1b. Paint background image / gradient (if visible)
        if (!isHidden)
            PaintBackgroundImage(box, list, effectiveOpacity);

        // 2. Paint border (if visible)
        if (!isHidden)
            PaintBorder(box, list, effectiveOpacity);

        // 3. Clip if overflow:hidden (or clip)
        bool clipping = box.Style.Overflow == OverflowType.Hidden
                     || box.Style.Overflow == OverflowType.Clip
                     || box.Style.OverflowX == OverflowType.Hidden
                     || box.Style.OverflowX == OverflowType.Clip
                     || box.Style.OverflowY == OverflowType.Hidden
                     || box.Style.OverflowY == OverflowType.Clip;
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

        // 7b. Paint outline (after content, outside border box)
        if (!isHidden)
            PaintOutline(box, list, effectiveOpacity);

        // 8. Pop filter
        if (hasFilter)
        {
            list.Add(new PopFilterCommand());
        }

        // 9. Pop transform
        if (hasTransform)
        {
            list.Add(new PopTransformCommand());
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

            // Text shadow (painted before the main glyph run so it sits behind).
            PaintTextShadows(run, list, opacity);

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

    // Poor-man's blur: emit copies on a small grid around the offset with reduced alpha.
    // Properly blurred text would need a GPU pass that samples the text atlas through a kernel.
    private static readonly (float dx, float dy, float weight)[] BlurKernel =
    [
        (0f, 0f, 0.36f),
        (-0.7f, 0f, 0.16f), (0.7f, 0f, 0.16f),
        (0f, -0.7f, 0.16f), (0f, 0.7f, 0.16f),
        (-0.5f, -0.5f, 0.08f), (0.5f, -0.5f, 0.08f),
        (-0.5f, 0.5f, 0.08f), (0.5f, 0.5f, 0.08f),
    ];

    private static void PaintTextShadows(TextRun run, PaintList list, float opacity)
    {
        var shadows = run.Style.TextShadows;
        if (shadows == null || shadows.Count == 0) return;

        // Paint in reverse so first declared shadow ends up on top.
        for (int i = shadows.Count - 1; i >= 0; i--)
        {
            var s = shadows[i];
            var baseColor = ApplyOpacity(s.Color, opacity);
            if (baseColor.A <= 0) continue;

            if (s.BlurRadius <= 0.5f)
            {
                list.Add(new DrawTextCommand
                {
                    Text = run.Text,
                    X = run.X + s.OffsetX,
                    Y = run.Y + s.OffsetY,
                    FontSize = run.Style.FontSize,
                    Color = baseColor,
                    FontWeight = run.Style.FontWeight,
                    FontStyle = run.Style.FontStyle,
                    FontFamily = run.Style.FontFamily,
                    FontFamilies = run.Style.FontFamilies,
                    LetterSpacing = run.Style.LetterSpacing,
                    WordSpacing = run.Style.WordSpacing,
                });
            }
            else
            {
                float step = s.BlurRadius * 0.35f;
                foreach (var (dx, dy, w) in BlurKernel)
                {
                    var sampleColor = new Document.Color(baseColor.R, baseColor.G, baseColor.B, baseColor.A * w);
                    if (sampleColor.A <= 0.01f) continue;
                    list.Add(new DrawTextCommand
                    {
                        Text = run.Text,
                        X = run.X + s.OffsetX + dx * step,
                        Y = run.Y + s.OffsetY + dy * step,
                        FontSize = run.Style.FontSize,
                        Color = sampleColor,
                        FontWeight = run.Style.FontWeight,
                        FontStyle = run.Style.FontStyle,
                        FontFamily = run.Style.FontFamily,
                        FontFamilies = run.Style.FontFamilies,
                        LetterSpacing = run.Style.LetterSpacing,
                        WordSpacing = run.Style.WordSpacing,
                    });
                }
            }
        }
    }

    private static void PaintTextDecoration(TextRun run, PaintList list, float opacity = 1f)
    {
        var decoration = run.Style.TextDecorationLine;
        if (decoration == TextDecorationLine.None) return;

        var color = ApplyOpacity(run.Style.TextDecorationColor ?? run.Style.Color, opacity);
        float thickness = float.IsNaN(run.Style.TextDecorationThickness)
            ? Math.Max(1f, run.Style.FontSize / 16f)
            : Math.Max(1f, run.Style.TextDecorationThickness);
        string decoStyle = run.Style.TextDecorationStyle;
        float extraOffset = float.IsNaN(run.Style.TextUnderlineOffset) ? 0f : run.Style.TextUnderlineOffset;

        if ((decoration & TextDecorationLine.Underline) != 0)
        {
            float underlineY = run.Y + run.Style.FontSize + extraOffset;
            EmitDecorationLine(list, run.X, underlineY, run.Width, thickness, color, decoStyle);
        }

        if ((decoration & TextDecorationLine.LineThrough) != 0)
        {
            float strikeY = run.Y + run.Style.FontSize * 0.5f;
            EmitDecorationLine(list, run.X, strikeY, run.Width, thickness, color, decoStyle);
        }

        if ((decoration & TextDecorationLine.Overline) != 0)
        {
            EmitDecorationLine(list, run.X, run.Y, run.Width, thickness, color, decoStyle);
        }
    }

    private static void EmitDecorationLine(PaintList list, float x, float y, float width, float thickness,
        Document.Color color, string decoStyle)
    {
        if (width <= 0) return;
        switch (decoStyle)
        {
            case "double":
            {
                float gap = Math.Max(1f, thickness);
                list.Add(new FillRectCommand { Rect = new RectF(x, y, width, thickness), Color = color });
                list.Add(new FillRectCommand { Rect = new RectF(x, y + thickness + gap, width, thickness), Color = color });
                break;
            }
            case "dotted":
            {
                float dot = Math.Max(1f, thickness);
                float step = dot * 2f;
                for (float cursor = 0; cursor < width; cursor += step)
                {
                    float w = Math.Min(dot, width - cursor);
                    list.Add(new FillRectCommand { Rect = new RectF(x + cursor, y, w, thickness), Color = color });
                }
                break;
            }
            case "dashed":
            {
                float dash = Math.Max(3f, thickness * 3f);
                float step = dash * 2f;
                for (float cursor = 0; cursor < width; cursor += step)
                {
                    float w = Math.Min(dash, width - cursor);
                    list.Add(new FillRectCommand { Rect = new RectF(x + cursor, y, w, thickness), Color = color });
                }
                break;
            }
            case "wavy":
            {
                // Approximate sine wave with small axis-aligned segments.
                float amp = Math.Max(1.5f, thickness * 1.2f);
                float period = Math.Max(6f, thickness * 6f);
                int steps = Math.Max(4, (int)(width / 1.5f));
                float prevOff = 0f;
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    float cx = t * width;
                    float off = amp * (float)Math.Sin((cx / period) * Math.PI * 2);
                    if (i > 0)
                    {
                        float segLen = width / steps;
                        float lowY = Math.Min(prevOff, off);
                        float highY = Math.Max(prevOff, off);
                        list.Add(new FillRectCommand
                        {
                            Rect = new RectF(x + cx - segLen, y + lowY, segLen, thickness + (highY - lowY)),
                            Color = color,
                        });
                    }
                    prevOff = off;
                }
                break;
            }
            default:
                list.Add(new FillRectCommand { Rect = new RectF(x, y, width, thickness), Color = color });
                break;
        }
    }

    private static void PaintListMarker(LayoutBox box, PaintList list, float opacity = 1f)
    {
        if (box.DomNode is not Element el || el.TagName != HtmlTagNames.Li) return;

        var parent = el.Parent as Element;
        bool ordered = parent?.TagName == HtmlTagNames.Ol;

        var dims = box.Dimensions;
        float fontSize = box.Style.FontSize;
        bool inside = box.Style.ListStylePosition == "inside";
        float markerX = inside ? dims.X : dims.X - fontSize * 1.2f;
        float markerY = dims.Y;
        var color = ApplyOpacity(box.Style.Color, opacity);

        string markerText;
        if (ordered)
        {
            int index = 1;
            if (parent != null)
            {
                foreach (var sibling in parent.Children)
                {
                    if (sibling == el) break;
                    if (sibling is Element sibEl && sibEl.TagName == HtmlTagNames.Li) index++;
                }
            }
            markerText = FormatOrderedMarker(index, box.Style.ListStyleType);
        }
        else
        {
            markerText = box.Style.ListStyleType switch
            {
                "circle" => "\u25E6",
                "square" => "\u25AA",
                "none" => "",
                _ => "\u2022",
            };
        }

        if (markerText.Length == 0) return;

        list.Add(new DrawTextCommand
        {
            Text = markerText,
            X = markerX,
            Y = markerY,
            FontSize = fontSize,
            Color = color,
            FontFamilies = box.Style.FontFamilies,
            FontWeight = box.Style.FontWeight,
        });
    }

    private static string FormatOrderedMarker(int index, string type)
    {
        return type switch
        {
            "lower-alpha" or "lower-latin" => ToAlpha(index, lower: true) + ".",
            "upper-alpha" or "upper-latin" => ToAlpha(index, lower: false) + ".",
            "lower-roman" => ToRoman(index).ToLowerInvariant() + ".",
            "upper-roman" => ToRoman(index) + ".",
            "none" => "",
            _ => index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".",
        };
    }

    private static string ToAlpha(int n, bool lower)
    {
        if (n <= 0) return n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var sb = new System.Text.StringBuilder();
        int v = n;
        while (v > 0)
        {
            v--;
            sb.Insert(0, (char)((lower ? 'a' : 'A') + v % 26));
            v /= 26;
        }
        return sb.ToString();
    }

    private static readonly (int Value, string Numeral)[] RomanTable =
    [
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
        (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
        (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
    ];

    private static string ToRoman(int n)
    {
        if (n <= 0 || n >= 4000) return n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var sb = new System.Text.StringBuilder();
        foreach (var (v, s) in RomanTable)
        {
            while (n >= v) { sb.Append(s); n -= v; }
        }
        return sb.ToString();
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
        if (box.DomNode is not Element el || el.TagName != HtmlTagNames.Img) return;

        var src = el.GetAttribute(HtmlAttributeNames.Src);
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
            var alt = el.GetAttribute(HtmlAttributeNames.Alt);
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

    private static void PaintBackgroundImage(LayoutBox box, PaintList list, float opacity)
    {
        var gradient = box.Style.BackgroundImage;
        if (gradient == null) return;

        // Inline elements with text runs don't get background images
        if (box.Style.Display == DisplayType.Inline && box.TextRuns is { Count: > 0 })
            return;

        var borderRect = box.Dimensions.BorderRect;
        if (borderRect.Width <= 0 || borderRect.Height <= 0) return;

        var (rtl, rtr, rbr, rbl) = ResolveBorderRadii(box.Style, borderRect.Width, borderRect.Height);

        switch (gradient)
        {
            case Document.Css.LinearGradient linear:
            {
                var stops = ConvertColorStops(linear.ColorStops, opacity);
                if (stops.Count >= 2)
                {
                    list.Add(new DrawLinearGradientCommand
                    {
                        Rect = borderRect,
                        AngleDeg = linear.AngleDeg,
                        ColorStops = stops,
                        RadiusTL = rtl,
                        RadiusTR = rtr,
                        RadiusBR = rbr,
                        RadiusBL = rbl,
                    });
                }
                break;
            }

            case Document.Css.RadialGradient radial:
            {
                var stops = ConvertColorStops(radial.ColorStops, opacity);
                if (stops.Count >= 2)
                {
                    list.Add(new DrawRadialGradientCommand
                    {
                        Rect = borderRect,
                        CenterX = radial.CenterX,
                        CenterY = radial.CenterY,
                        RadiusX = 0.5f,
                        RadiusY = 0.5f,
                        ColorStops = stops,
                    });
                }
                break;
            }

            case Document.Css.ConicGradient conic:
            {
                // Conic gradients are approximated as a series of linear gradient segments
                // For now, render a simple two-color approximation
                var stops = ConvertColorStops(conic.ColorStops, opacity);
                if (stops.Count >= 2)
                {
                    // Use linear gradient as a visual approximation
                    list.Add(new DrawLinearGradientCommand
                    {
                        Rect = borderRect,
                        AngleDeg = conic.FromAngleDeg,
                        ColorStops = stops,
                        RadiusTL = rtl,
                        RadiusTR = rtr,
                        RadiusBR = rbr,
                        RadiusBL = rbl,
                    });
                }
                break;
            }
        }
    }

    private static List<GradientColorStop> ConvertColorStops(
        IReadOnlyList<Document.Css.ColorStop> cssStops, float opacity)
    {
        var result = new List<GradientColorStop>();
        foreach (var stop in cssStops)
        {
            result.Add(new GradientColorStop
            {
                Color = ApplyOpacity(stop.Color, opacity),
                Position = stop.Position ?? 0f,
            });
        }
        return result;
    }

    private static void PaintBoxShadow(LayoutBox box, PaintList list, float opacity)
    {
        var shadows = box.Style.BoxShadows;
        if (shadows == null || shadows.Count == 0) return;

        var borderRect = box.Dimensions.BorderRect;
        if (borderRect.Width <= 0 || borderRect.Height <= 0) return;

        var (rtl, rtr, rbr, rbl) = ResolveBorderRadii(box.Style, borderRect.Width, borderRect.Height);

        // Paint shadows in reverse order (first declared = topmost)
        for (int i = shadows.Count - 1; i >= 0; i--)
        {
            var shadow = shadows[i];
            var color = ApplyOpacity(shadow.Color, opacity);
            if (color.A <= 0) continue;

            list.Add(new DrawBoxShadowCommand
            {
                Rect = borderRect,
                OffsetX = shadow.OffsetX,
                OffsetY = shadow.OffsetY,
                BlurRadius = shadow.BlurRadius,
                SpreadRadius = shadow.SpreadRadius,
                Color = color,
                Inset = shadow.Inset,
                RadiusTL = rtl,
                RadiusTR = rtr,
                RadiusBR = rbr,
                RadiusBL = rbl,
            });
        }
    }

    private static void PaintOutline(LayoutBox box, PaintList list, float opacity)
    {
        var style = box.Style;
        if (style.OutlineStyle == "none" || style.OutlineWidth <= 0) return;

        var borderRect = box.Dimensions.BorderRect;
        if (borderRect.Width <= 0 || borderRect.Height <= 0) return;

        var color = ApplyOpacity(style.OutlineColor, opacity);
        if (color.A <= 0) return;

        list.Add(new DrawOutlineCommand
        {
            Rect = borderRect,
            Width = style.OutlineWidth,
            Color = color,
            Style = style.OutlineStyle,
            Offset = style.OutlineOffset,
        });
    }
}
