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
            style = parentStyle?.Clone() ?? new ComputedStyle();
            style.Display = DisplayType.Inline;
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
                style.BorderColor = ResolveColor(value);
                break;
            case "border-style":
                style.BorderStyle = value.Raw.ToLowerInvariant();
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
                style.FontFamily = value.Raw.Trim().Trim('"', '\'');
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

    private static void InheritFromParent(ComputedStyle style, ComputedStyle parentStyle)
    {
        style.Color = parentStyle.Color;
        style.FontSize = parentStyle.FontSize;
        style.FontFamily = parentStyle.FontFamily;
        style.TextAlign = parentStyle.TextAlign;
        style.LineHeight = parentStyle.LineHeight;
    }

    private static DisplayType GetDefaultDisplay(string tagName) => tagName switch
    {
        "div" or "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
            or "ul" or "ol" or "li" or "header" or "footer" or "main"
            or "section" or "article" or "nav" or "aside" or "hr"
            or "blockquote" or "pre" => DisplayType.Block,
        "span" or "a" or "strong" or "em" or "b" or "i" or "u"
            or "code" or "br" or "img" => DisplayType.Inline,
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
