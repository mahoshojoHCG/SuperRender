using SuperRender.Core.Css;
using SuperRender.Core.Dom;
using SuperRender.Core.Layout;

namespace SuperRender.Core.Style;

public sealed class StyleResolver
{
    private readonly List<Stylesheet> _stylesheets;
    private readonly Stylesheet? _userAgentStylesheet;

    public StyleResolver(List<Stylesheet> stylesheets, Stylesheet? userAgentStylesheet = null)
    {
        _stylesheets = stylesheets;
        _userAgentStylesheet = userAgentStylesheet;
    }

    public Dictionary<Node, ComputedStyle> ResolveAll(Document document)
    {
        var styles = new Dictionary<Node, ComputedStyle>();
        ResolveNode(document, null, styles);
        return styles;
    }

    private void ResolveNode(Node node, ComputedStyle? parentStyle, Dictionary<Node, ComputedStyle> styles)
    {
        ComputedStyle style;

        if (node is Element element)
        {
            style = Resolve(element, parentStyle);
        }
        else if (node is TextNode)
        {
            // Text nodes only inherit text-related properties, not box-model properties
            style = new ComputedStyle { Display = DisplayType.Inline };
            if (parentStyle != null)
                InheritFromParent(style, parentStyle);
        }
        else
        {
            style = parentStyle?.Clone() ?? new ComputedStyle();
        }

        styles[node] = style;

        foreach (var child in node.Children)
        {
            ResolveNode(child, style, styles);
        }
    }

    public ComputedStyle Resolve(Element element, ComputedStyle? parentStyle = null)
    {
        var style = new ComputedStyle();

        // Start with inherited properties from parent
        if (parentStyle != null)
        {
            InheritFromParent(style, parentStyle);
        }

        // Apply default display based on tag
        style.Display = GetDefaultDisplay(element.TagName);

        // Collect all matching rules
        var matchedDeclarations = CollectMatchingDeclarations(element);

        // Sort by: !important > specificity > source order
        matchedDeclarations.Sort((a, b) =>
        {
            // !important first
            var impCmp = a.Important.CompareTo(b.Important);
            if (impCmp != 0) return impCmp;

            // Then specificity
            var specCmp = a.Specificity.CompareTo(b.Specificity);
            if (specCmp != 0) return specCmp;

            // Then source order
            return a.SourceOrder.CompareTo(b.SourceOrder);
        });

        // Apply declarations in order (later overrides earlier)
        foreach (var matched in matchedDeclarations)
        {
            ApplyDeclaration(style, matched.Declaration, parentStyle);
        }

        // Apply inline style attribute (highest specificity for non-!important)
        var inlineStyle = element.GetAttribute("style");
        if (!string.IsNullOrWhiteSpace(inlineStyle))
        {
            ApplyInlineStyle(style, inlineStyle, parentStyle);
        }

        // Apply hidden attribute
        if (element.Attributes.ContainsKey("hidden"))
        {
            style.Display = DisplayType.None;
        }

        return style;
    }

    private List<MatchedDeclaration> CollectMatchingDeclarations(Element element)
    {
        var result = new List<MatchedDeclaration>();
        int sourceOrder = 0;

        // User-agent stylesheet first (lowest cascade priority)
        if (_userAgentStylesheet != null)
        {
            CollectFromStylesheet(_userAgentStylesheet, element, result, ref sourceOrder);
        }

        foreach (var stylesheet in _stylesheets)
        {
            CollectFromStylesheet(stylesheet, element, result, ref sourceOrder);
        }

        return result;
    }

    private static void CollectFromStylesheet(
        Stylesheet stylesheet, Element element,
        List<MatchedDeclaration> result, ref int sourceOrder)
    {
        foreach (var rule in stylesheet.Rules)
        {
            foreach (var selector in rule.Selectors)
            {
                if (selector.Matches(element))
                {
                    var specificity = selector.GetSpecificity();
                    foreach (var declaration in rule.Declarations)
                    {
                        result.Add(new MatchedDeclaration
                        {
                            Specificity = specificity,
                            SourceOrder = sourceOrder++,
                            Declaration = declaration,
                            Important = declaration.Important,
                        });
                    }
                    break; // Only match once per rule
                }
            }
        }
    }

    private static void ApplyDeclaration(ComputedStyle style, Declaration decl, ComputedStyle? parentStyle)
    {
        var prop = decl.Property.ToLowerInvariant();
        var value = decl.Value;

        switch (prop)
        {
            case "display":
                style.Display = value.Raw.ToLowerInvariant() switch
                {
                    "block" => DisplayType.Block,
                    "inline" => DisplayType.Inline,
                    "inline-block" => DisplayType.InlineBlock,
                    "flow-root" => DisplayType.FlowRoot,
                    "none" => DisplayType.None,
                    _ => style.Display
                };
                break;

            case "width":
                style.Width = ResolveLength(value, parentStyle);
                break;
            case "height":
                style.Height = ResolveLength(value, parentStyle);
                break;

            case "box-sizing":
                style.BoxSizing = value.Raw.ToLowerInvariant() switch
                {
                    "content-box" => BoxSizingType.ContentBox,
                    "border-box" => BoxSizingType.BorderBox,
                    _ => style.BoxSizing
                };
                break;

            case "min-width":
                var minW = ResolveLength(value, parentStyle);
                style.MinWidth = float.IsNaN(minW) ? 0 : minW;
                break;
            case "max-width":
                if (value.Raw.Equals("none", StringComparison.OrdinalIgnoreCase))
                    style.MaxWidth = float.PositiveInfinity;
                else
                {
                    var maxW = ResolveLength(value, parentStyle);
                    style.MaxWidth = float.IsNaN(maxW) ? float.PositiveInfinity : maxW;
                }
                break;
            case "min-height":
                var minH = ResolveLength(value, parentStyle);
                style.MinHeight = float.IsNaN(minH) ? 0 : minH;
                break;
            case "max-height":
                if (value.Raw.Equals("none", StringComparison.OrdinalIgnoreCase))
                    style.MaxHeight = float.PositiveInfinity;
                else
                {
                    var maxH = ResolveLength(value, parentStyle);
                    style.MaxHeight = float.IsNaN(maxH) ? float.PositiveInfinity : maxH;
                }
                break;

            case "overflow" or "overflow-x" or "overflow-y":
                style.Overflow = value.Raw.ToLowerInvariant() switch
                {
                    "visible" => OverflowType.Visible,
                    "hidden" => OverflowType.Hidden,
                    "scroll" => OverflowType.Scroll,
                    "auto" => OverflowType.Auto,
                    _ => style.Overflow
                };
                break;

            case "text-overflow":
                style.TextOverflow = value.Raw.ToLowerInvariant() switch
                {
                    "clip" => TextOverflowType.Clip,
                    "ellipsis" => TextOverflowType.Ellipsis,
                    _ => style.TextOverflow
                };
                break;

            case "white-space":
                style.WhiteSpace = value.Raw.ToLowerInvariant() switch
                {
                    "normal" => WhiteSpaceType.Normal,
                    "pre" => WhiteSpaceType.Pre,
                    "nowrap" => WhiteSpaceType.Nowrap,
                    "pre-wrap" => WhiteSpaceType.PreWrap,
                    "pre-line" => WhiteSpaceType.PreLine,
                    _ => style.WhiteSpace
                };
                break;

            case "margin-top":
                style.Margin = style.Margin with { Top = ResolveLength(value, parentStyle) };
                break;
            case "margin-right":
                style.Margin = style.Margin with { Right = ResolveLength(value, parentStyle) };
                break;
            case "margin-bottom":
                style.Margin = style.Margin with { Bottom = ResolveLength(value, parentStyle) };
                break;
            case "margin-left":
                style.Margin = style.Margin with { Left = ResolveLength(value, parentStyle) };
                break;

            case "padding-top":
                style.Padding = style.Padding with { Top = ResolveLength(value, parentStyle) };
                break;
            case "padding-right":
                style.Padding = style.Padding with { Right = ResolveLength(value, parentStyle) };
                break;
            case "padding-bottom":
                style.Padding = style.Padding with { Bottom = ResolveLength(value, parentStyle) };
                break;
            case "padding-left":
                style.Padding = style.Padding with { Left = ResolveLength(value, parentStyle) };
                break;

            case "border-width":
                var bw = ResolveLength(value, parentStyle);
                style.BorderWidth = new EdgeSizes(bw);
                break;
            case "border-top-width":
                style.BorderWidth = style.BorderWidth with { Top = ResolveLength(value, parentStyle) };
                break;
            case "border-right-width":
                style.BorderWidth = style.BorderWidth with { Right = ResolveLength(value, parentStyle) };
                break;
            case "border-bottom-width":
                style.BorderWidth = style.BorderWidth with { Bottom = ResolveLength(value, parentStyle) };
                break;
            case "border-left-width":
                style.BorderWidth = style.BorderWidth with { Left = ResolveLength(value, parentStyle) };
                break;

            case "border-color":
                var bc = ResolveColor(value);
                style.BorderTopColor = bc;
                style.BorderRightColor = bc;
                style.BorderBottomColor = bc;
                style.BorderLeftColor = bc;
                break;
            case "border-top-color":
                style.BorderTopColor = ResolveColor(value);
                break;
            case "border-right-color":
                style.BorderRightColor = ResolveColor(value);
                break;
            case "border-bottom-color":
                style.BorderBottomColor = ResolveColor(value);
                break;
            case "border-left-color":
                style.BorderLeftColor = ResolveColor(value);
                break;
            case "border-style":
                var bs = value.Raw.ToLowerInvariant();
                style.BorderTopStyle = bs;
                style.BorderRightStyle = bs;
                style.BorderBottomStyle = bs;
                style.BorderLeftStyle = bs;
                break;
            case "border-top-style":
                style.BorderTopStyle = value.Raw.ToLowerInvariant();
                break;
            case "border-right-style":
                style.BorderRightStyle = value.Raw.ToLowerInvariant();
                break;
            case "border-bottom-style":
                style.BorderBottomStyle = value.Raw.ToLowerInvariant();
                break;
            case "border-left-style":
                style.BorderLeftStyle = value.Raw.ToLowerInvariant();
                break;

            case "color":
                style.Color = ResolveColor(value);
                break;
            case "background-color":
                style.BackgroundColor = ResolveColor(value);
                break;

            case "font-size":
                style.FontSize = ResolveFontSize(value, parentStyle);
                break;
            case "font-family":
                style.FontFamilies = FontFamilyParser.Parse(value.Raw);
                break;

            case "text-align":
                style.TextAlign = value.Raw.ToLowerInvariant() switch
                {
                    "left" => TextAlign.Left,
                    "right" => TextAlign.Right,
                    "center" => TextAlign.Center,
                    "justify" => TextAlign.Justify,
                    _ => style.TextAlign
                };
                break;

            case "line-height":
                if (value.Type == CssValueType.Number)
                    style.LineHeight = (float)value.NumericValue;
                else if (value.Type == CssValueType.Length)
                    style.LineHeight = (float)value.NumericValue / style.FontSize;
                else if (value.Type == CssValueType.Percentage)
                    style.LineHeight = (float)value.NumericValue / 100f;
                break;

            case "font-weight":
                style.FontWeight = ResolveFontWeight(value, parentStyle);
                break;

            case "font-style":
                style.FontStyle = value.Raw.ToLowerInvariant() switch
                {
                    "normal" => FontStyleType.Normal,
                    "italic" => FontStyleType.Italic,
                    "oblique" => FontStyleType.Oblique,
                    _ => style.FontStyle
                };
                break;

            case "text-decoration" or "text-decoration-line":
                style.TextDecorationLine = ResolveTextDecorationLine(value);
                break;

            case "text-decoration-color":
                style.TextDecorationColor = ResolveColor(value);
                break;

            case "position":
                style.Position = value.Raw.ToLowerInvariant() switch
                {
                    "static" => PositionType.Static,
                    "relative" => PositionType.Relative,
                    "absolute" => PositionType.Absolute,
                    _ => style.Position
                };
                break;

            case "top":
                style.Top = ResolveLength(value, parentStyle);
                break;
            case "left":
                style.Left = ResolveLength(value, parentStyle);
                break;
            case "right":
                style.Right = ResolveLength(value, parentStyle);
                break;
            case "bottom":
                style.Bottom = ResolveLength(value, parentStyle);
                break;

            case "z-index":
                if (value.Raw.Equals("auto", StringComparison.OrdinalIgnoreCase))
                {
                    style.ZIndex = 0;
                    style.ZIndexIsAuto = true;
                }
                else if (value.Type == CssValueType.Number)
                {
                    style.ZIndex = (int)value.NumericValue;
                    style.ZIndexIsAuto = false;
                }
                break;
        }
    }

    private static float ResolveLength(CssValue value, ComputedStyle? parentStyle)
    {
        if (value.Raw.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return float.NaN;

        if (value.Raw == "0")
            return 0;

        return value.Type switch
        {
            CssValueType.Length => value.Unit?.ToLowerInvariant() switch
            {
                "px" => (float)value.NumericValue,
                "em" => (float)value.NumericValue * (parentStyle?.FontSize ?? 16f),
                "rem" => (float)value.NumericValue * 16f,
                _ => (float)value.NumericValue
            },
            CssValueType.Number => (float)value.NumericValue,
            CssValueType.Percentage => (float)value.NumericValue, // percentage stored as-is, resolved during layout
            _ => float.NaN
        };
    }

    private static float ResolveFontSize(CssValue value, ComputedStyle? parentStyle)
    {
        var parentFontSize = parentStyle?.FontSize ?? 16f;

        return value.Type switch
        {
            CssValueType.Length => value.Unit?.ToLowerInvariant() switch
            {
                "px" => (float)value.NumericValue,
                "em" => (float)value.NumericValue * parentFontSize,
                "rem" => (float)value.NumericValue * 16f,
                "pt" => (float)value.NumericValue * 1.333f,
                _ => (float)value.NumericValue
            },
            CssValueType.Percentage => parentFontSize * (float)value.NumericValue / 100f,
            CssValueType.Number => (float)value.NumericValue,
            CssValueType.Keyword => value.Raw.ToLowerInvariant() switch
            {
                "small" => 13f,
                "medium" => 16f,
                "large" => 18f,
                "x-large" => 24f,
                "xx-large" => 32f,
                "smaller" => parentFontSize * 0.833f,
                "larger" => parentFontSize * 1.2f,
                _ => parentFontSize
            },
            _ => parentFontSize
        };
    }

    private static Color ResolveColor(CssValue value)
    {
        if (value.ColorValue.HasValue)
            return value.ColorValue.Value;

        if (value.Type == CssValueType.Color)
            return Color.FromHex(value.Raw);

        if (Color.TryFromName(value.Raw, out var named))
            return named;

        if (value.Raw.StartsWith('#'))
            return Color.FromHex(value.Raw);

        return Color.Black;
    }

    private static int ResolveFontWeight(CssValue value, ComputedStyle? parentStyle)
    {
        var raw = value.Raw.ToLowerInvariant();
        return raw switch
        {
            "normal" => 400,
            "bold" => 700,
            "bolder" => Math.Min((parentStyle?.FontWeight ?? 400) + 300, 900),
            "lighter" => Math.Max((parentStyle?.FontWeight ?? 400) - 100, 100),
            _ => value.Type == CssValueType.Number
                ? Math.Clamp((int)value.NumericValue, 100, 900)
                : parentStyle?.FontWeight ?? 400
        };
    }

    private static TextDecorationLine ResolveTextDecorationLine(CssValue value)
    {
        var raw = value.Raw.ToLowerInvariant().Trim();
        if (raw == "none")
            return TextDecorationLine.None;

        var result = TextDecorationLine.None;
        foreach (var part in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            result |= part switch
            {
                "underline" => TextDecorationLine.Underline,
                "overline" => TextDecorationLine.Overline,
                "line-through" => TextDecorationLine.LineThrough,
                _ => TextDecorationLine.None
            };
        }
        return result;
    }

    private static void InheritFromParent(ComputedStyle style, ComputedStyle parentStyle)
    {
        style.Color = parentStyle.Color;
        style.FontSize = parentStyle.FontSize;
        style.FontFamilies = parentStyle.FontFamilies;
        style.FontWeight = parentStyle.FontWeight;
        style.FontStyle = parentStyle.FontStyle;
        style.TextAlign = parentStyle.TextAlign;
        style.LineHeight = parentStyle.LineHeight;
        style.WhiteSpace = parentStyle.WhiteSpace;
    }

    private static DisplayType GetDefaultDisplay(string tagName) => tagName switch
    {
        "div" or "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
            or "ul" or "ol" or "li" or "header" or "footer" or "main"
            or "section" or "article" or "nav" or "aside" or "hr"
            or "blockquote" or "pre" or "address" or "figure" or "figcaption"
            or "details" or "summary" or "dialog" or "dd" or "dl" or "dt"
            or "fieldset" or "form" or "hgroup" => DisplayType.Block,
        "span" or "a" or "strong" or "em" or "b" or "i" or "u"
            or "code" or "br" or "img" or "s" or "del" or "ins" or "strike"
            or "small" or "mark" or "cite" or "var" or "dfn" or "kbd" or "samp"
            or "abbr" or "sub" or "sup" or "q" or "time" or "data" => DisplayType.Inline,
        "html" or "body" => DisplayType.Block,
        "head" or "title" or "style" or "meta" or "link" or "script" => DisplayType.None,
        _ => DisplayType.Block,
    };

    private static void ApplyInlineStyle(ComputedStyle style, string cssText, ComputedStyle? parentStyle)
    {
        // Parse inline style as declarations
        // Simple parsing: split by ; then split by :
        var parts = cssText.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var colonIdx = part.IndexOf(':');
            if (colonIdx < 0) continue;

            var property = part[..colonIdx].Trim().ToLowerInvariant();
            var rawValue = part[(colonIdx + 1)..].Trim();

            var important = false;
            if (rawValue.EndsWith("!important", StringComparison.OrdinalIgnoreCase))
            {
                important = true;
                rawValue = rawValue[..^"!important".Length].Trim();
            }

            var cssValue = ParseInlineValue(rawValue);
            var decl = new Declaration { Property = property, Value = cssValue, Important = important };
            ApplyDeclaration(style, decl, parentStyle);
        }
    }

    private static CssValue ParseInlineValue(string rawValue)
    {
        // Try to parse as a simple value
        if (rawValue.StartsWith('#'))
        {
            return new CssValue
            {
                Type = CssValueType.Color,
                Raw = rawValue,
                ColorValue = Color.FromHex(rawValue)
            };
        }

        if (Color.TryFromName(rawValue, out var namedColor))
        {
            return new CssValue
            {
                Type = CssValueType.Color,
                Raw = rawValue,
                ColorValue = namedColor
            };
        }

        // Try number + unit
        var numEnd = 0;
        while (numEnd < rawValue.Length && (char.IsDigit(rawValue[numEnd]) || rawValue[numEnd] == '.' || rawValue[numEnd] == '-'))
            numEnd++;

        if (numEnd > 0 && double.TryParse(rawValue[..numEnd], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var num))
        {
            var unit = rawValue[numEnd..].Trim().ToLowerInvariant();
            if (unit == "%")
            {
                return new CssValue { Type = CssValueType.Percentage, Raw = rawValue, NumericValue = num };
            }
            if (unit.Length > 0)
            {
                return new CssValue { Type = CssValueType.Length, Raw = rawValue, NumericValue = num, Unit = unit };
            }
            return new CssValue { Type = CssValueType.Number, Raw = rawValue, NumericValue = num };
        }

        return new CssValue { Type = CssValueType.Keyword, Raw = rawValue };
    }

    private struct MatchedDeclaration
    {
        public Specificity Specificity;
        public int SourceOrder;
        public Declaration Declaration;
        public bool Important;
    }
}
