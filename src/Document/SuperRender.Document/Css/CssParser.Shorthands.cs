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

    #endregion
}
