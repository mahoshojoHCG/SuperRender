namespace SuperRender.Document.Css;

public sealed partial class CssParser
{
    #region Shorthand Expansion

    private static bool IsBoxShorthand(string property)
        => property is "margin" or "padding" or "border-width";

    private static List<Declaration> ExpandBoxShorthand(string property, List<CssToken> valueTokens, bool important)
    {
        // Split value tokens by whitespace into groups
        var parts = SplitByWhitespace(valueTokens);

        string prefix = property switch
        {
            "margin" => "margin",
            "padding" => "padding",
            "border-width" => "border",
            _ => property
        };

        string topProp, rightProp, bottomProp, leftProp;
        if (property == "border-width")
        {
            topProp = "border-top-width";
            rightProp = "border-right-width";
            bottomProp = "border-bottom-width";
            leftProp = "border-left-width";
        }
        else
        {
            topProp = $"{prefix}-top";
            rightProp = $"{prefix}-right";
            bottomProp = $"{prefix}-bottom";
            leftProp = $"{prefix}-left";
        }

        CssValue top, right, bottom, left;

        switch (parts.Count)
        {
            case 1:
                top = right = bottom = left = ParseValueTokens(parts[0]);
                break;
            case 2:
                top = bottom = ParseValueTokens(parts[0]);
                right = left = ParseValueTokens(parts[1]);
                break;
            case 3:
                top = ParseValueTokens(parts[0]);
                right = left = ParseValueTokens(parts[1]);
                bottom = ParseValueTokens(parts[2]);
                break;
            default: // 4+
                top = ParseValueTokens(parts[0]);
                right = ParseValueTokens(parts[1]);
                bottom = ParseValueTokens(parts[2]);
                left = ParseValueTokens(parts[3]);
                break;
        }

        return
        [
            new Declaration { Property = topProp, Value = top, Important = important },
            new Declaration { Property = rightProp, Value = right, Important = important },
            new Declaration { Property = bottomProp, Value = bottom, Important = important },
            new Declaration { Property = leftProp, Value = left, Important = important },
        ];
    }

    private static List<Declaration> ExpandBorderShorthand(List<CssToken> valueTokens, bool important)
    {
        // border: <width> <style> <color>
        var parts = SplitByWhitespace(valueTokens);
        var declarations = new List<Declaration>();

        CssValue? width = null;
        CssValue? style = null;
        CssValue? color = null;

        var borderStyles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "none", "hidden", "dotted", "dashed", "solid", "double", "groove", "ridge", "inset", "outset"
        };

        foreach (var part in parts)
        {
            var val = ParseValueTokens(part);

            if (val.Type is CssValueType.Length or CssValueType.Number or CssValueType.Percentage)
            {
                width ??= val;
            }
            else if (val.Type == CssValueType.Color)
            {
                color ??= val;
            }
            else if (val.Type == CssValueType.Keyword)
            {
                // Check if it's currentcolor
                if (val.Raw.Equals("currentcolor", StringComparison.OrdinalIgnoreCase) && color == null)
                {
                    color = val;
                }
                // Check if it's a color name
                else if (Document.Color.TryFromName(val.Raw, out _) && color == null)
                {
                    color = new CssValue
                    {
                        Type = CssValueType.Color,
                        Raw = val.Raw,
                        ColorValue = Document.Color.FromName(val.Raw)
                    };
                }
                else if (borderStyles.Contains(val.Raw))
                {
                    style ??= val;
                }
                else
                {
                    // fallback: treat as style
                    style ??= val;
                }
            }
        }

        if (width != null)
            declarations.Add(new Declaration { Property = "border-width", Value = width, Important = important });
        if (style != null)
            declarations.Add(new Declaration { Property = "border-style", Value = style, Important = important });
        if (color != null)
            declarations.Add(new Declaration { Property = "border-color", Value = color, Important = important });

        return declarations;
    }

    private static List<Declaration> ExpandPerSideBorderShorthand(string property, List<CssToken> valueTokens, bool important)
    {
        // border-top: <width> <style> <color>
        // → border-top-width, border-top-style, border-top-color
        var side = property["border-".Length..]; // "top", "right", "bottom", "left"
        var parts = SplitByWhitespace(valueTokens);
        var declarations = new List<Declaration>();

        CssValue? width = null;
        CssValue? style = null;
        CssValue? color = null;

        var borderStyles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "none", "hidden", "dotted", "dashed", "solid", "double", "groove", "ridge", "inset", "outset"
        };

        foreach (var part in parts)
        {
            var val = ParseValueTokens(part);

            if (val.Type is CssValueType.Length or CssValueType.Number or CssValueType.Percentage)
            {
                width ??= val;
            }
            else if (val.Type == CssValueType.Color)
            {
                color ??= val;
            }
            else if (val.Type == CssValueType.Keyword)
            {
                if (val.Raw.Equals("currentcolor", StringComparison.OrdinalIgnoreCase) && color == null)
                {
                    color = val;
                }
                else if (Document.Color.TryFromName(val.Raw, out _) && color == null)
                {
                    color = new CssValue
                    {
                        Type = CssValueType.Color,
                        Raw = val.Raw,
                        ColorValue = Document.Color.FromName(val.Raw)
                    };
                }
                else if (borderStyles.Contains(val.Raw))
                {
                    style ??= val;
                }
                else
                {
                    style ??= val;
                }
            }
        }

        if (width != null)
            declarations.Add(new Declaration { Property = $"border-{side}-width", Value = width, Important = important });
        if (style != null)
            declarations.Add(new Declaration { Property = $"border-{side}-style", Value = style, Important = important });
        if (color != null)
            declarations.Add(new Declaration { Property = $"border-{side}-color", Value = color, Important = important });

        return declarations;
    }

    private static List<Declaration> ExpandBorderRadiusShorthand(List<CssToken> valueTokens, bool important)
    {
        // border-radius: TL TR BR BL  (or 1/2/3 value syntax like margin/padding)
        var parts = SplitByWhitespace(valueTokens);
        var values = parts.Select(p => ParseValueTokens(p)).ToList();

        CssValue tl, tr, br, bl;
        switch (values.Count)
        {
            case 1:
                tl = tr = br = bl = values[0];
                break;
            case 2:
                tl = br = values[0];
                tr = bl = values[1];
                break;
            case 3:
                tl = values[0];
                tr = bl = values[1];
                br = values[2];
                break;
            default: // 4+
                tl = values[0];
                tr = values[1];
                br = values[2];
                bl = values[3];
                break;
        }

        return
        [
            new Declaration { Property = "border-top-left-radius", Value = tl, Important = important },
            new Declaration { Property = "border-top-right-radius", Value = tr, Important = important },
            new Declaration { Property = "border-bottom-right-radius", Value = br, Important = important },
            new Declaration { Property = "border-bottom-left-radius", Value = bl, Important = important },
        ];
    }

    private static List<Declaration> ExpandFlexShorthand(List<CssToken> valueTokens, bool important)
    {
        var parts = SplitByWhitespace(valueTokens);
        var declarations = new List<Declaration>();

        if (parts.Count == 1)
        {
            var val = ParseValueTokens(parts[0]);
            var raw = val.Raw.ToLowerInvariant();

            switch (raw)
            {
                case "initial":
                    declarations.Add(MakeDecl("flex-grow", "0", CssValueType.Number, 0, important));
                    declarations.Add(MakeDecl("flex-shrink", "1", CssValueType.Number, 1, important));
                    declarations.Add(MakeDecl("flex-basis", "auto", CssValueType.Keyword, 0, important));
                    return declarations;
                case "auto":
                    declarations.Add(MakeDecl("flex-grow", "1", CssValueType.Number, 1, important));
                    declarations.Add(MakeDecl("flex-shrink", "1", CssValueType.Number, 1, important));
                    declarations.Add(MakeDecl("flex-basis", "auto", CssValueType.Keyword, 0, important));
                    return declarations;
                case "none":
                    declarations.Add(MakeDecl("flex-grow", "0", CssValueType.Number, 0, important));
                    declarations.Add(MakeDecl("flex-shrink", "0", CssValueType.Number, 0, important));
                    declarations.Add(MakeDecl("flex-basis", "auto", CssValueType.Keyword, 0, important));
                    return declarations;
            }

            // Single number: flex: <grow> => grow:N, shrink:1, basis:0
            if (val.Type == CssValueType.Number)
            {
                declarations.Add(new Declaration { Property = "flex-grow", Value = val, Important = important });
                declarations.Add(MakeDecl("flex-shrink", "1", CssValueType.Number, 1, important));
                declarations.Add(MakeDecl("flex-basis", "0", CssValueType.Number, 0, important));
                return declarations;
            }
        }

        if (parts.Count == 2)
        {
            var grow = ParseValueTokens(parts[0]);
            var second = ParseValueTokens(parts[1]);

            declarations.Add(new Declaration { Property = "flex-grow", Value = grow, Important = important });
            declarations.Add(new Declaration { Property = "flex-shrink", Value = second, Important = important });
            declarations.Add(MakeDecl("flex-basis", "0", CssValueType.Number, 0, important));
            return declarations;
        }

        if (parts.Count >= 3)
        {
            var grow = ParseValueTokens(parts[0]);
            var shrink = ParseValueTokens(parts[1]);
            var basis = ParseValueTokens(parts[2]);

            declarations.Add(new Declaration { Property = "flex-grow", Value = grow, Important = important });
            declarations.Add(new Declaration { Property = "flex-shrink", Value = shrink, Important = important });
            declarations.Add(new Declaration { Property = "flex-basis", Value = basis, Important = important });
            return declarations;
        }

        return declarations;
    }

    private static List<Declaration> ExpandFlexFlowShorthand(List<CssToken> valueTokens, bool important)
    {
        var parts = SplitByWhitespace(valueTokens);
        var declarations = new List<Declaration>();

        foreach (var part in parts)
        {
            var val = ParseValueTokens(part);
            var raw = val.Raw.ToLowerInvariant();

            if (raw is "row" or "row-reverse" or "column" or "column-reverse")
            {
                declarations.Add(new Declaration { Property = "flex-direction", Value = val, Important = important });
            }
            else if (raw is "nowrap" or "wrap" or "wrap-reverse")
            {
                declarations.Add(new Declaration { Property = "flex-wrap", Value = val, Important = important });
            }
        }

        return declarations;
    }

    private static Declaration MakeDecl(string property, string raw, CssValueType type, double numericValue, bool important)
    {
        return new Declaration
        {
            Property = property,
            Value = new CssValue { Type = type, Raw = raw, NumericValue = numericValue },
            Important = important
        };
    }

    private static List<Declaration> ExpandInsetShorthand(List<CssToken> valueTokens, bool important)
    {
        // inset: <top> <right> <bottom> <left> (same 1/2/3/4 value syntax as margin/padding)
        var parts = SplitByWhitespace(valueTokens);

        CssValue top, right, bottom, left;
        switch (parts.Count)
        {
            case 1:
                top = right = bottom = left = ParseValueTokens(parts[0]);
                break;
            case 2:
                top = bottom = ParseValueTokens(parts[0]);
                right = left = ParseValueTokens(parts[1]);
                break;
            case 3:
                top = ParseValueTokens(parts[0]);
                right = left = ParseValueTokens(parts[1]);
                bottom = ParseValueTokens(parts[2]);
                break;
            default: // 4+
                top = ParseValueTokens(parts[0]);
                right = ParseValueTokens(parts[1]);
                bottom = ParseValueTokens(parts[2]);
                left = ParseValueTokens(parts[3]);
                break;
        }

        return
        [
            new Declaration { Property = "top", Value = top, Important = important },
            new Declaration { Property = "right", Value = right, Important = important },
            new Declaration { Property = "bottom", Value = bottom, Important = important },
            new Declaration { Property = "left", Value = left, Important = important },
        ];
    }

    private static List<Declaration> ExpandInsetBlockShorthand(List<CssToken> valueTokens, bool important)
    {
        // inset-block: <start> <end> (1 or 2 values)
        var parts = SplitByWhitespace(valueTokens);

        CssValue start, end;
        if (parts.Count >= 2)
        {
            start = ParseValueTokens(parts[0]);
            end = ParseValueTokens(parts[1]);
        }
        else
        {
            start = end = ParseValueTokens(parts[0]);
        }

        return
        [
            new Declaration { Property = "inset-block-start", Value = start, Important = important },
            new Declaration { Property = "inset-block-end", Value = end, Important = important },
        ];
    }

    private static List<Declaration> ExpandInsetInlineShorthand(List<CssToken> valueTokens, bool important)
    {
        // inset-inline: <start> <end> (1 or 2 values)
        var parts = SplitByWhitespace(valueTokens);

        CssValue start, end;
        if (parts.Count >= 2)
        {
            start = ParseValueTokens(parts[0]);
            end = ParseValueTokens(parts[1]);
        }
        else
        {
            start = end = ParseValueTokens(parts[0]);
        }

        return
        [
            new Declaration { Property = "inset-inline-start", Value = start, Important = important },
            new Declaration { Property = "inset-inline-end", Value = end, Important = important },
        ];
    }

    private static List<Declaration> ExpandMarginBlockShorthand(List<CssToken> valueTokens, bool important)
    {
        // margin-block: <start> <end> (1 or 2 values)
        var parts = SplitByWhitespace(valueTokens);

        CssValue start, end;
        if (parts.Count >= 2)
        {
            start = ParseValueTokens(parts[0]);
            end = ParseValueTokens(parts[1]);
        }
        else
        {
            start = end = ParseValueTokens(parts[0]);
        }

        return
        [
            new Declaration { Property = "margin-block-start", Value = start, Important = important },
            new Declaration { Property = "margin-block-end", Value = end, Important = important },
        ];
    }

    private static List<Declaration> ExpandMarginInlineShorthand(List<CssToken> valueTokens, bool important)
    {
        // margin-inline: <start> <end> (1 or 2 values)
        var parts = SplitByWhitespace(valueTokens);

        CssValue start, end;
        if (parts.Count >= 2)
        {
            start = ParseValueTokens(parts[0]);
            end = ParseValueTokens(parts[1]);
        }
        else
        {
            start = end = ParseValueTokens(parts[0]);
        }

        return
        [
            new Declaration { Property = "margin-inline-start", Value = start, Important = important },
            new Declaration { Property = "margin-inline-end", Value = end, Important = important },
        ];
    }

    private static List<Declaration> ExpandPaddingBlockShorthand(List<CssToken> valueTokens, bool important)
    {
        // padding-block: <start> <end> (1 or 2 values)
        var parts = SplitByWhitespace(valueTokens);

        CssValue start, end;
        if (parts.Count >= 2)
        {
            start = ParseValueTokens(parts[0]);
            end = ParseValueTokens(parts[1]);
        }
        else
        {
            start = end = ParseValueTokens(parts[0]);
        }

        return
        [
            new Declaration { Property = "padding-block-start", Value = start, Important = important },
            new Declaration { Property = "padding-block-end", Value = end, Important = important },
        ];
    }

    private static List<Declaration> ExpandPaddingInlineShorthand(List<CssToken> valueTokens, bool important)
    {
        // padding-inline: <start> <end> (1 or 2 values)
        var parts = SplitByWhitespace(valueTokens);

        CssValue start, end;
        if (parts.Count >= 2)
        {
            start = ParseValueTokens(parts[0]);
            end = ParseValueTokens(parts[1]);
        }
        else
        {
            start = end = ParseValueTokens(parts[0]);
        }

        return
        [
            new Declaration { Property = "padding-inline-start", Value = start, Important = important },
            new Declaration { Property = "padding-inline-end", Value = end, Important = important },
        ];
    }

    private static List<Declaration> ExpandPlaceItemsShorthand(List<CssToken> valueTokens, bool important)
    {
        // place-items: <align-items> <justify-content>
        // If only one value, it applies to both
        var parts = SplitByWhitespace(valueTokens);
        var first = ParseValueTokens(parts[0]);
        var second = parts.Count >= 2 ? ParseValueTokens(parts[1]) : first;

        return
        [
            new Declaration { Property = "align-items", Value = first, Important = important },
            new Declaration { Property = "justify-content", Value = second, Important = important },
        ];
    }

    private static List<Declaration> ExpandPlaceContentShorthand(List<CssToken> valueTokens, bool important)
    {
        // place-content: <align-content> <justify-content>
        // If only one value, it applies to both
        var parts = SplitByWhitespace(valueTokens);
        var first = ParseValueTokens(parts[0]);
        var second = parts.Count >= 2 ? ParseValueTokens(parts[1]) : first;

        return
        [
            new Declaration { Property = "align-content", Value = first, Important = important },
            new Declaration { Property = "justify-content", Value = second, Important = important },
        ];
    }

    private static List<Declaration> ExpandPlaceSelfShorthand(List<CssToken> valueTokens, bool important)
    {
        // place-self: <align-self> <justify-self>
        // We only support align-self, so just set align-self from first value
        var parts = SplitByWhitespace(valueTokens);
        var first = ParseValueTokens(parts[0]);

        return
        [
            new Declaration { Property = "align-self", Value = first, Important = important },
        ];
    }

    private static List<Declaration> ExpandBackgroundShorthand(List<CssToken> valueTokens, bool important)
    {
        // background: [color] [image] [repeat] [position] [/ size] [attachment] [origin] [clip]
        // This is a simplified parser: extract color and gradient/url, pass rest as-is.
        var declarations = new List<Declaration>();
        var parts = SplitByWhitespace(valueTokens);

        CssValue? bgColor = null;
        CssValue? bgImage = null;

        foreach (var part in parts)
        {
            var val = ParseValueTokens(part);

            // Check for gradient function
            if (val.Gradient != null)
            {
                bgImage = val;
                continue;
            }

            // Check for url()
            if (val.Raw.StartsWith("url(", System.StringComparison.OrdinalIgnoreCase))
            {
                bgImage = val;
                continue;
            }

            // Check for color
            if (val.Type == CssValueType.Color)
            {
                bgColor ??= val;
                continue;
            }

            if (val.Type == CssValueType.Keyword)
            {
                // "none" for background-image
                if (val.Raw.Equals("none", System.StringComparison.OrdinalIgnoreCase))
                {
                    bgImage ??= val;
                    continue;
                }

                // Named color
                if (Document.Color.TryFromName(val.Raw, out var named))
                {
                    bgColor ??= new CssValue
                    {
                        Type = CssValueType.Color,
                        Raw = val.Raw,
                        ColorValue = named,
                    };
                    continue;
                }

                // repeat values
                if (val.Raw is "repeat" or "repeat-x" or "repeat-y" or "no-repeat" or "space" or "round")
                {
                    declarations.Add(new Declaration
                    {
                        Property = "background-repeat",
                        Value = val,
                        Important = important,
                    });
                    continue;
                }
            }
        }

        if (bgColor != null)
        {
            declarations.Add(new Declaration
            {
                Property = "background-color",
                Value = bgColor,
                Important = important,
            });
        }

        if (bgImage != null)
        {
            declarations.Add(new Declaration
            {
                Property = "background-image",
                Value = bgImage,
                Important = important,
            });
        }

        return declarations;
    }

    private static List<Declaration> ExpandOutlineShorthand(List<CssToken> valueTokens, bool important)
    {
        // outline: [width] [style] [color]
        var parts = SplitByWhitespace(valueTokens);
        var declarations = new List<Declaration>();

        var outlineStyles = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "none", "hidden", "dotted", "dashed", "solid", "double", "groove", "ridge", "inset", "outset", "auto"
        };

        foreach (var part in parts)
        {
            var val = ParseValueTokens(part);

            if (val.Type is CssValueType.Length or CssValueType.Number)
            {
                declarations.Add(new Declaration
                {
                    Property = "outline-width",
                    Value = val,
                    Important = important,
                });
            }
            else if (val.Type == CssValueType.Color ||
                     (val.Type == CssValueType.Keyword && Document.Color.TryFromName(val.Raw, out _)))
            {
                if (val.Type == CssValueType.Keyword)
                {
                    val = new CssValue
                    {
                        Type = CssValueType.Color,
                        Raw = val.Raw,
                        ColorValue = Document.Color.FromName(val.Raw),
                    };
                }
                declarations.Add(new Declaration
                {
                    Property = "outline-color",
                    Value = val,
                    Important = important,
                });
            }
            else if (val.Type == CssValueType.Keyword && outlineStyles.Contains(val.Raw))
            {
                declarations.Add(new Declaration
                {
                    Property = "outline-style",
                    Value = val,
                    Important = important,
                });
            }
        }

        return declarations;
    }

    #endregion
}
