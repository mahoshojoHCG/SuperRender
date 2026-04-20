using SuperRender.Document.Style;
using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document;
using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;

namespace SuperRender.Renderer.Rendering.Style;

public sealed partial class StyleResolver
{
    private readonly List<Stylesheet> _stylesheets;
    private readonly Stylesheet? _userAgentStylesheet;
    private float _viewportWidth = PropertyDefaults.DefaultViewportWidth;
    private float _viewportHeight = PropertyDefaults.DefaultViewportHeight;

    public StyleResolver(List<Stylesheet> stylesheets, Stylesheet? userAgentStylesheet = null)
    {
        _stylesheets = stylesheets;
        _userAgentStylesheet = userAgentStylesheet;
    }

    public record PseudoElementInfo(ComputedStyle Style, string Content);

    /// <summary>
    /// Per-element pseudo-element data: maps (Element, PseudoElementType) to style+content.
    /// </summary>
    public Dictionary<(Element, PseudoElementType), PseudoElementInfo>? PseudoElements { get; private set; }

    public Dictionary<Node, ComputedStyle> ResolveAll(DomDocument document, float viewportWidth = PropertyDefaults.DefaultViewportWidth, float viewportHeight = PropertyDefaults.DefaultViewportHeight)
    {
        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;
        PseudoElements = new Dictionary<(Element, PseudoElementType), PseudoElementInfo>();
        _customProperties = new Dictionary<Node, Dictionary<string, string>>();
        var styles = new Dictionary<Node, ComputedStyle>();
        ResolveNode(document, null, styles, null);
        return styles;
    }

    /// <summary>Per-element custom property maps for CSS variables.</summary>
    private Dictionary<Node, Dictionary<string, string>>? _customProperties;

    /// <summary>Active counter values during tree traversal (flat scope approximation).</summary>
    private readonly Dictionary<string, int> _counters = new(StringComparer.Ordinal);

    /// <summary>Gets the resolved custom properties for an element (for external access).</summary>
    public Dictionary<string, string>? GetCustomProperties(Node node)
        => _customProperties?.GetValueOrDefault(node);

    private void ResolveNode(Node node, ComputedStyle? parentStyle, Dictionary<Node, ComputedStyle> styles,
        Dictionary<string, string>? parentCustomProps)
    {
        ComputedStyle style;
        Dictionary<string, string>? elementCustomProps = parentCustomProps;

        if (node is Element element)
        {
            style = Resolve(element, parentStyle, parentCustomProps, out var resolvedCustomProps);
            elementCustomProps = resolvedCustomProps;
            if (_customProperties != null && resolvedCustomProps != null && resolvedCustomProps.Count > 0)
                _customProperties[node] = resolvedCustomProps;

            // Apply counter-reset / counter-increment BEFORE resolving pseudo-elements
            // so that counter() in ::before content reflects the incremented value.
            ApplyCounterReset(style.CounterReset);
            ApplyCounterIncrement(style.CounterIncrement);

            // Resolve pseudo-elements for this element
            if (PseudoElements != null)
            {
                var before = ResolvePseudoElement(element, PseudoElementType.Before, style);
                if (before.HasValue)
                    PseudoElements[(element, PseudoElementType.Before)] =
                        new PseudoElementInfo(before.Value.style, before.Value.content);

                var after = ResolvePseudoElement(element, PseudoElementType.After, style);
                if (after.HasValue)
                    PseudoElements[(element, PseudoElementType.After)] =
                        new PseudoElementInfo(after.Value.style, after.Value.content);
            }
        }
        else if (node is TextNode)
        {
            style = new ComputedStyle { Display = DisplayType.Inline };
            if (parentStyle != null)
            {
                InheritFromParent(style, parentStyle);
                PropagateTextPropsToTextNode(style, parentStyle);
            }
        }
        else
        {
            style = parentStyle?.Clone() ?? new ComputedStyle();
        }

        styles[node] = style;

        foreach (var child in node.Children)
        {
            ResolveNode(child, style, styles, elementCustomProps);
        }
    }

    public ComputedStyle Resolve(Element element, ComputedStyle? parentStyle = null)
        => Resolve(element, parentStyle, null, out _);

    private ComputedStyle Resolve(Element element, ComputedStyle? parentStyle,
        Dictionary<string, string>? parentCustomProps, out Dictionary<string, string>? customProps)
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

        // Collect inline style declarations
        var inlineDecls = new List<Declaration>();
        var inlineStyle = element.GetAttribute(HtmlAttributeNames.Style);
        if (!string.IsNullOrWhiteSpace(inlineStyle))
        {
            inlineDecls = CssParser.ParseInlineStyleDeclarations(inlineStyle);
        }

        // Resolve custom properties (--* variables)
        var allDecls = matchedDeclarations.Select(m => m.Declaration).Concat(inlineDecls).ToList();
        customProps = CustomPropertyResolver.Resolve(allDecls, parentCustomProps);

        // Apply declarations in order (later overrides earlier), resolving var() references
        foreach (var matched in matchedDeclarations)
        {
            var decl = matched.Declaration;
            if (decl.Property.StartsWith("--", StringComparison.Ordinal)) continue; // Custom properties already collected
            var resolvedDecl = ResolveVarInDeclaration(decl, customProps);
            ApplyDeclaration(style, resolvedDecl, parentStyle);
        }

        // Apply inline style (highest specificity for non-!important)
        foreach (var decl in inlineDecls)
        {
            if (decl.Property.StartsWith("--", StringComparison.Ordinal)) continue;
            var resolvedDecl = ResolveVarInDeclaration(decl, customProps);
            ApplyDeclaration(style, resolvedDecl, parentStyle);
        }

        // Apply hidden attribute
        if (element.Attributes.ContainsKey(HtmlAttributeNames.Hidden))
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

    private void CollectFromStylesheet(
        Stylesheet stylesheet, Element element,
        List<MatchedDeclaration> result, ref int sourceOrder)
    {
        CollectFromRules(stylesheet.Rules, element, result, ref sourceOrder);

        // Process at-rules (media, supports, layer, scope)
        foreach (var atRule in stylesheet.AtRules)
        {
            CollectFromAtRule(atRule, element, result, ref sourceOrder);
        }
    }

    private static void CollectFromRules(
        List<CssRule> rules, Element element,
        List<MatchedDeclaration> result, ref int sourceOrder)
    {
        foreach (var rule in rules)
        {
            foreach (var selector in rule.Selectors)
            {
                // Skip pseudo-element selectors — they're handled separately
                if (selector.PseudoElement != null)
                    continue;

                if (SelectorMatcher.Matches(selector, element))
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

    private void CollectFromAtRule(
        CssAtRule atRule, Element element,
        List<MatchedDeclaration> result, ref int sourceOrder)
    {
        switch (atRule)
        {
            case CssMediaRule mediaRule:
            {
                var mq = new MediaQuery(mediaRule.MediaQuery);
                if (mq.Evaluate(_viewportWidth, _viewportHeight))
                {
                    CollectFromRules(mediaRule.Rules, element, result, ref sourceOrder);
                    foreach (var nested in mediaRule.NestedAtRules)
                        CollectFromAtRule(nested, element, result, ref sourceOrder);
                }
                break;
            }
            case CssSupportsRule supportsRule:
            {
                var sc = new SupportsCondition(supportsRule.Condition);
                if (sc.Evaluate())
                {
                    CollectFromRules(supportsRule.Rules, element, result, ref sourceOrder);
                    foreach (var nested in supportsRule.NestedAtRules)
                        CollectFromAtRule(nested, element, result, ref sourceOrder);
                }
                break;
            }
            case CssLayerRule layerRule:
            {
                CollectFromRules(layerRule.Rules, element, result, ref sourceOrder);
                break;
            }
            case CssScopeRule scopeRule:
            {
                CollectFromRules(scopeRule.Rules, element, result, ref sourceOrder);
                break;
            }
        }
    }

    /// <summary>
    /// Resolves the style for a ::before or ::after pseudo-element on the given element.
    /// Returns null if no matching pseudo-element rule with content is found.
    /// </summary>
    public (ComputedStyle style, string content)? ResolvePseudoElement(
        Element element, PseudoElementType pseudoType, ComputedStyle parentStyle)
    {
        var declarations = new List<MatchedDeclaration>();
        int sourceOrder = 0;

        if (_userAgentStylesheet != null)
            CollectPseudoFromStylesheet(_userAgentStylesheet, element, pseudoType, declarations, ref sourceOrder);
        foreach (var stylesheet in _stylesheets)
            CollectPseudoFromStylesheet(stylesheet, element, pseudoType, declarations, ref sourceOrder);

        if (declarations.Count == 0) return null;

        // Build a style by applying matched declarations
        var style = new ComputedStyle();
        InheritFromParent(style, parentStyle);

        declarations.Sort((a, b) =>
        {
            var impCmp = a.Important.CompareTo(b.Important);
            if (impCmp != 0) return impCmp;
            var specCmp = a.Specificity.CompareTo(b.Specificity);
            if (specCmp != 0) return specCmp;
            return a.SourceOrder.CompareTo(b.SourceOrder);
        });

        string? content = null;
        foreach (var matched in declarations)
        {
            if (matched.Declaration.Property == CssPropertyNames.Content)
            {
                content = ParseContentValue(matched.Declaration.Value.Raw, _counters);
            }
            else
            {
                ApplyDeclaration(style, matched.Declaration, parentStyle);
            }
        }

        if (content == null) return null;

        // Pseudo-elements are inline by default
        if (style.Display == DisplayType.Block)
            style.Display = DisplayType.Inline;

        return (style, content);
    }

    private static void CollectPseudoFromStylesheet(
        Stylesheet stylesheet, Element element, PseudoElementType pseudoType,
        List<MatchedDeclaration> result, ref int sourceOrder)
    {
        foreach (var rule in stylesheet.Rules)
        {
            foreach (var selector in rule.Selectors)
            {
                if (selector.PseudoElement != pseudoType)
                    continue;

                if (SelectorMatcher.Matches(selector, element))
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
                    break;
                }
            }
        }
    }

    private static string? ParseContentValue(string raw)
        => ParseContentValue(raw, null);

    private void ApplyCounterReset(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("none", StringComparison.OrdinalIgnoreCase))
            return;
        foreach (var (name, value) in ParseCounterList(raw!, defaultValue: 0))
            _counters[name] = value;
    }

    private void ApplyCounterIncrement(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("none", StringComparison.OrdinalIgnoreCase))
            return;
        foreach (var (name, delta) in ParseCounterList(raw!, defaultValue: 1))
            _counters[name] = _counters.TryGetValue(name, out var v) ? v + delta : delta;
    }

    private static IEnumerable<(string name, int value)> ParseCounterList(string raw, int defaultValue)
    {
        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int i = 0;
        while (i < parts.Length)
        {
            string name = parts[i++];
            int value = defaultValue;
            if (i < parts.Length && int.TryParse(parts[i], System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out int n))
            {
                value = n;
                i++;
            }
            yield return (name, value);
        }
    }

    private static string? ParseContentValue(string raw, Dictionary<string, int>? counters)
    {
        var trimmed = raw.Trim();
        if (trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("normal", StringComparison.OrdinalIgnoreCase))
            return null;

        // Fast path: a single quoted string with no functions.
        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') ||
             (trimmed[0] == '\'' && trimmed[^1] == '\'')) &&
            !trimmed.Contains("counter(", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Contains("counters(", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[1..^1];
        }

        // Tokenize the content value: string literals + counter()/counters() function calls.
        var sb = new System.Text.StringBuilder();
        var tokenizer = new CssTokenizer(trimmed);
        var tokens = tokenizer.Tokenize().Where(t => t.Type != CssTokenType.EndOfFile
            && t.Type != CssTokenType.Whitespace).ToList();

        for (int i = 0; i < tokens.Count; i++)
        {
            var tk = tokens[i];
            if (tk.Type == CssTokenType.StringLiteral)
            {
                sb.Append(tk.Value);
            }
            else if (tk.Type == CssTokenType.Function
                && (tk.Value.Equals("counter", StringComparison.OrdinalIgnoreCase)
                    || tk.Value.Equals("counters", StringComparison.OrdinalIgnoreCase)))
            {
                // Collect arg tokens until matching RightParen.
                var args = new List<CssToken>();
                i++;
                int depth = 1;
                while (i < tokens.Count && depth > 0)
                {
                    var inner = tokens[i];
                    if (inner.Type == CssTokenType.LeftParen || inner.Type == CssTokenType.Function) depth++;
                    else if (inner.Type == CssTokenType.RightParen) { depth--; if (depth == 0) break; }
                    args.Add(inner);
                    i++;
                }

                string name = args.Count > 0 && args[0].Type == CssTokenType.Ident ? args[0].Value : "";
                int value = counters != null && counters.TryGetValue(name, out var v) ? v : 0;

                // Style arg: ident after comma. We only support decimal / upper-roman / lower-roman / alpha.
                string styleArg = "decimal";
                for (int a = 1; a < args.Count; a++)
                {
                    if (args[a].Type == CssTokenType.Ident) { styleArg = args[a].Value; break; }
                }
                sb.Append(FormatCounter(value, styleArg));
            }
            else if (tk.Type == CssTokenType.Ident
                && tk.Value.Equals("attr", StringComparison.OrdinalIgnoreCase))
            {
                // attr() not supported here; skip.
            }
        }

        return sb.Length > 0 ? sb.ToString() : "";
    }

    private static string FormatCounter(int n, string style)
    {
        return style.ToLowerInvariant() switch
        {
            "lower-alpha" or "lower-latin" => AlphaString(n, lower: true),
            "upper-alpha" or "upper-latin" => AlphaString(n, lower: false),
            "lower-roman" => RomanString(n).ToLowerInvariant(),
            "upper-roman" => RomanString(n),
            "none" => string.Empty,
            _ => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    private static string AlphaString(int n, bool lower)
    {
        if (n <= 0) return n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var sb = new System.Text.StringBuilder();
        int v = n;
        while (v > 0) { v--; sb.Insert(0, (char)((lower ? 'a' : 'A') + v % 26)); v /= 26; }
        return sb.ToString();
    }

    private static readonly (int V, string S)[] RomanPairs =
    [
        (1000,"M"),(900,"CM"),(500,"D"),(400,"CD"),(100,"C"),(90,"XC"),
        (50,"L"),(40,"XL"),(10,"X"),(9,"IX"),(5,"V"),(4,"IV"),(1,"I"),
    ];

    private static string RomanString(int n)
    {
        if (n <= 0 || n >= 4000) return n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var sb = new System.Text.StringBuilder();
        foreach (var (v, s) in RomanPairs) { while (n >= v) { sb.Append(s); n -= v; } }
        return sb.ToString();
    }

    private void ApplyDeclaration(ComputedStyle style, Declaration decl, ComputedStyle? parentStyle)
    {
        var prop = decl.Property.ToLowerInvariant();
        var value = decl.Value;

        // Handle CSS global keywords: initial, inherit, unset, revert
        if (value.Type == CssValueType.Keyword)
        {
            var kw = value.Raw.ToLowerInvariant();
            switch (kw)
            {
                case "initial":
                    PropertyDefaults.ApplyInitialValue(style, prop);
                    return;
                case "inherit":
                    if (parentStyle != null)
                        PropertyDefaults.InheritProperty(style, prop, parentStyle);
                    return;
                case "unset":
                    if (PropertyDefaults.IsInherited(prop) && parentStyle != null)
                        PropertyDefaults.InheritProperty(style, prop, parentStyle);
                    else
                        PropertyDefaults.ApplyInitialValue(style, prop);
                    return;
                case "revert":
                    // Revert to UA default: skip this declaration (UA styles already applied)
                    return;
            }
        }

        if (ApplyBoxModelProperty(style, prop, value, parentStyle))
            return;
        if (ApplyTypographyProperty(style, prop, value, parentStyle))
            return;
        if (ApplyColorProperty(style, prop, value, parentStyle))
            return;
        if (ApplyPositionProperty(style, prop, value, parentStyle))
            return;
        if (ApplyFlexProperty(style, prop, value, parentStyle))
            return;
        if (ApplyTransformProperty(style, prop, value, parentStyle))
            return;
        if (ApplyAnimationProperty(style, prop, value, parentStyle))
            return;
        if (ApplyGridProperty(style, prop, value, parentStyle))
            return;
        if (ApplyVisualProperty(style, prop, value, parentStyle))
            return;
        if (ApplyBackgroundProperty(style, prop, value, parentStyle))
            return;
        ApplyFilterProperty(style, prop, value, parentStyle);
    }

    private static Declaration ResolveVarInDeclaration(Declaration decl, Dictionary<string, string>? customProps)
    {
        if (decl.Value.VarName != null)
        {
            var resolved = CustomPropertyResolver.ResolveVarValue(decl.Value, customProps);
            return new Declaration { Property = decl.Property, Value = resolved, Important = decl.Important };
        }

        // Check if the raw value contains var() (for values not parsed as function tokens)
        if (customProps != null && customProps.Count > 0 && decl.Value.Raw.Contains("var(", StringComparison.OrdinalIgnoreCase))
        {
            var substituted = CustomPropertyResolver.SubstituteVars(decl.Value.Raw, customProps);
            if (substituted != decl.Value.Raw)
            {
                var resolved = CssParser.ParseValueText(substituted);
                return new Declaration { Property = decl.Property, Value = resolved, Important = decl.Important };
            }
        }

        return decl;
    }

    private void ApplyInlineStyle(ComputedStyle style, string cssText, ComputedStyle? parentStyle)
    {
        // Use the full CSS parser to handle shorthand expansion (border, margin, padding, etc.)
        var declarations = CssParser.ParseInlineStyleDeclarations(cssText);
        foreach (var decl in declarations)
        {
            ApplyDeclaration(style, decl, parentStyle);
        }
    }

    private static CssValue ParseInlineValue(string rawValue)
    {
        // Delegate to the full CSS tokenizer for function values (hsl, rgb, calc, etc.)
        // and shorthand values that the simple parser can't handle
        if (rawValue.Contains('('))
        {
            return CssParser.ParseValueText(rawValue);
        }

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
        style.Visibility = parentStyle.Visibility;
        style.TextTransform = parentStyle.TextTransform;
        style.LetterSpacing = parentStyle.LetterSpacing;
        style.WordSpacing = parentStyle.WordSpacing;
        style.Cursor = parentStyle.Cursor;
        style.WordBreak = parentStyle.WordBreak;
        style.OverflowWrap = parentStyle.OverflowWrap;
        style.ListStyleType = parentStyle.ListStyleType;
        style.TextIndent = parentStyle.TextIndent;
        style.TabSize = parentStyle.TabSize;
        style.FontVariant = parentStyle.FontVariant;
        style.Direction = parentStyle.Direction;
        style.Quotes = parentStyle.Quotes;
    }

    /// <summary>
    /// Text-decoration and vertical-align are formally NOT inherited, but text
    /// nodes have no declarations of their own — text runs built from a text
    /// node must carry the parent element's decoration/vertical-align so they
    /// paint correctly. Apply this only to text-node style resolution.
    /// </summary>
    private static void PropagateTextPropsToTextNode(ComputedStyle style, ComputedStyle parentStyle)
    {
        style.TextDecorationLine = parentStyle.TextDecorationLine;
        style.TextDecorationColor = parentStyle.TextDecorationColor;
        style.TextDecorationStyle = parentStyle.TextDecorationStyle;
        style.TextDecorationThickness = parentStyle.TextDecorationThickness;
        style.TextUnderlineOffset = parentStyle.TextUnderlineOffset;
        style.TextShadow = parentStyle.TextShadow;
        style.TextShadows = parentStyle.TextShadows;
        style.VerticalAlign = parentStyle.VerticalAlign;
        style.VerticalAlignLength = parentStyle.VerticalAlignLength;
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
            or "code" or "br" or "s" or "del" or "ins" or "strike"
            or "small" or "mark" or "cite" or "var" or "dfn" or "kbd" or "samp"
            or "abbr" or "sub" or "sup" or "q" or "time" or "data" => DisplayType.Inline,
        "img" => DisplayType.InlineBlock, // replaced element: needs width/height
        "html" or "body" => DisplayType.Block,
        "head" or "title" or "style" or "meta" or "link" or "script" => DisplayType.None,
        _ => DisplayType.Block,
    };

    private struct MatchedDeclaration
    {
        public Specificity Specificity;
        public int SourceOrder;
        public Declaration Declaration;
        public bool Important;
    }
}
