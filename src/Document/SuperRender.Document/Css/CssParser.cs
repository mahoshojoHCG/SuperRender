using System.Globalization;

namespace SuperRender.Document.Css;

/// <summary>
/// Parses CSS text into a Stylesheet. Handles rule blocks, declarations,
/// value parsing, and shorthand expansion for margin, padding, border-width, and border.
/// </summary>
public sealed partial class CssParser
{
    private readonly string _css;
    private List<CssToken> _tokens = [];
    private int _pos;

    public CssParser(string css)
    {
        _css = css;
    }

    public Stylesheet Parse()
    {
        var tokenizer = new CssTokenizer(_css);
        _tokens = tokenizer.Tokenize().ToList();
        _pos = 0;

        var stylesheet = new Stylesheet();

        SkipWhitespace();
        while (!IsEnd())
        {
            if (Current().Type == CssTokenType.AtKeyword)
            {
                var atRule = ParseAtRule();
                if (atRule != null)
                    stylesheet.AtRules.Add(atRule);
            }
            else
            {
                var nestedRules = new List<CssRule>();
                var rule = ParseRuleWithNesting(null, nestedRules);
                if (rule != null)
                    stylesheet.Rules.Add(rule);
                // Add any nested rules that were flattened during parsing
                stylesheet.Rules.AddRange(nestedRules);
            }
            SkipWhitespace();
        }

        return stylesheet;
    }

    private CssRule? ParseRule()
    {
        return ParseRuleWithNesting(null);
    }

    /// <summary>
    /// Parses a rule, optionally supporting CSS nesting within a parent selector context.
    /// When parentSelectors is non-null, nested rules are flattened into flattenedRules.
    /// </summary>
    private CssRule? ParseRuleWithNesting(List<Selector>? parentSelectors, List<CssRule>? flattenedRules = null)
    {
        // Collect tokens for the selector (everything up to '{')
        var selectorTokens = new List<CssToken>();
        while (!IsEnd() && Current().Type != CssTokenType.LeftBrace)
        {
            selectorTokens.Add(Current());
            _pos++;
        }

        if (IsEnd()) return null;

        _pos++; // skip '{'

        // Parse selector list
        var selectorParser = new SelectorParser(selectorTokens);
        var selectors = selectorParser.ParseSelectorList();

        // If we have parent selectors, expand nesting: prepend parent as ancestor
        if (parentSelectors != null && parentSelectors.Count > 0)
        {
            selectors = ExpandNestedSelectors(parentSelectors, selectors);
        }

        // Parse declaration block with nesting support
        var declarations = new List<Declaration>();
        var nestedRules = new List<CssRule>();
        ParseDeclarationBlockWithNesting(declarations, nestedRules, selectors);

        // Add nested rules to the flattened list or return them as additional rules
        if (flattenedRules != null)
        {
            flattenedRules.AddRange(nestedRules);
        }

        if (selectors.Count == 0) return null;

        return new CssRule
        {
            Selectors = selectors,
            Declarations = declarations
        };
    }

    /// <summary>
    /// Expands nested selectors by prepending each parent selector as an ancestor.
    /// E.g., parent ".foo" + nested ".bar" → ".foo .bar".
    /// The '&amp;' token in the nested selector is replaced with the parent selector.
    /// </summary>
    private static List<Selector> ExpandNestedSelectors(List<Selector> parentSelectors, List<Selector> nestedSelectors)
    {
        var expanded = new List<Selector>();
        foreach (var parentSel in parentSelectors)
        {
            foreach (var nestedSel in nestedSelectors)
            {
                // Check if the nested selector contains '&' (nesting selector)
                bool hasAmpersand = nestedSel.Components.Any(c =>
                    c.Simple.TagName != null && c.Simple.TagName == "&");

                if (hasAmpersand)
                {
                    // Replace '&' with the parent selector components
                    var newComponents = new List<SelectorComponent>();
                    foreach (var comp in nestedSel.Components)
                    {
                        if (comp.Simple.TagName == "&")
                        {
                            // Insert parent selector components
                            for (int i = 0; i < parentSel.Components.Count; i++)
                            {
                                var parentComp = parentSel.Components[i];
                                if (i == 0 && newComponents.Count > 0)
                                {
                                    // Use the existing combinator for the first parent component
                                    newComponents.Add(new SelectorComponent
                                    {
                                        Simple = parentComp.Simple,
                                        Combinator = parentComp.Combinator
                                    });
                                }
                                else
                                {
                                    newComponents.Add(parentComp);
                                }
                            }
                        }
                        else
                        {
                            newComponents.Add(comp);
                        }
                    }
                    expanded.Add(new Selector { Components = newComponents, PseudoElement = nestedSel.PseudoElement });
                }
                else
                {
                    // No '&' — use descendant combinator: parent <space> nested
                    var newComponents = new List<SelectorComponent>(parentSel.Components);
                    foreach (var comp in nestedSel.Components)
                    {
                        newComponents.Add(comp);
                    }
                    expanded.Add(new Selector { Components = newComponents, PseudoElement = nestedSel.PseudoElement });
                }
            }
        }
        return expanded;
    }

    /// <summary>
    /// Parses a declaration block that may contain nested rules.
    /// Declarations are collected into declarations list, nested rules into nestedRules.
    /// </summary>
    private void ParseDeclarationBlockWithNesting(List<Declaration> declarations, List<CssRule> nestedRules,
        List<Selector>? currentSelectors)
    {
        SkipWhitespace();
        while (!IsEnd() && Current().Type != CssTokenType.RightBrace)
        {
            // Check if this looks like a nested rule (selector { ... }) rather than a declaration
            if (LooksLikeNestedRule())
            {
                var nestedRule = ParseRuleWithNesting(currentSelectors, nestedRules);
                if (nestedRule != null)
                    nestedRules.Add(nestedRule);
            }
            else
            {
                var decl = ParseDeclaration();
                if (decl != null)
                    declarations.AddRange(decl);
            }
            SkipWhitespace();
        }

        if (!IsEnd()) _pos++; // skip '}'
    }

    /// <summary>
    /// Heuristic: looks ahead to determine if the current position starts a nested rule
    /// (selector block) rather than a declaration (property: value).
    /// A declaration starts with Ident Colon or custom-property prefix.
    /// A nested rule starts with a selector-like token and eventually has '{'.
    /// </summary>
    private bool LooksLikeNestedRule()
    {
        // Save position for lookahead
        int saved = _pos;

        SkipWhitespace();
        if (IsEnd() || Current().Type == CssTokenType.RightBrace)
        {
            _pos = saved;
            return false;
        }

        // If starts with '&', '.', '#', '>', '+', '~', '*', ':', or '[' — looks like a selector
        var cur = Current();
        if (cur.Type == CssTokenType.Delim && (cur.Value == "&" || cur.Value == "*" || cur.Value == ">" || cur.Value == "+" || cur.Value == "~"))
        {
            _pos = saved;
            return true;
        }
        if (cur.Type == CssTokenType.Dot || cur.Type == CssTokenType.Hash || cur.Type == CssTokenType.Colon)
        {
            // Could be a pseudo-class or class selector — likely nested rule
            // But ':' alone could be a property: value pair — need more context
            if (cur.Type == CssTokenType.Colon)
            {
                // Check for pseudo-class vs declaration — is this ident:value or :pseudo-class?
                // In nested context, :hover { } is a nested rule, but property: value; is a declaration
                // A declaration starts with Ident then Colon
                _pos = saved;
                return false;
            }
            _pos = saved;
            return true;
        }

        // If starts with AtKeyword, it's a nested at-rule (not handled here, but return false)
        if (cur.Type == CssTokenType.AtKeyword)
        {
            _pos = saved;
            return false;
        }

        // If starts with Ident, scan ahead: if Ident is followed by '{' or another selector token
        // before hitting ':', it's a nested rule. If followed by ':', it's a declaration.
        if (cur.Type == CssTokenType.Ident)
        {
            _pos++;
            SkipWhitespace();
            if (!IsEnd())
            {
                var next = Current();
                if (next.Type == CssTokenType.Colon)
                {
                    // Ident followed by ':' = declaration
                    _pos = saved;
                    return false;
                }
                if (next.Type == CssTokenType.LeftBrace)
                {
                    // Ident followed by '{' = nested rule (e.g., "div { ... }")
                    _pos = saved;
                    return true;
                }
                // Continue scanning — might be "div .child { ... }"
                // Look for '{' before ';' or '}'
                while (!IsEnd() && Current().Type != CssTokenType.LeftBrace
                    && Current().Type != CssTokenType.Semicolon
                    && Current().Type != CssTokenType.RightBrace)
                {
                    _pos++;
                }
                bool isNested = !IsEnd() && Current().Type == CssTokenType.LeftBrace;
                _pos = saved;
                return isNested;
            }
        }

        _pos = saved;
        return false;
    }

    #region At-Rule Parsing

    private CssAtRule? ParseAtRule()
    {
        if (IsEnd() || Current().Type != CssTokenType.AtKeyword)
            return null;

        var keyword = Current().Value.ToLowerInvariant();
        _pos++; // skip at-keyword token
        SkipWhitespace();

        return keyword switch
        {
            "import" => ParseImportRule(),
            "media" => ParseMediaRule(),
            "supports" => ParseSupportsRule(),
            "layer" => ParseLayerRule(),
            "font-face" => ParseFontFaceRule(),
            "keyframes" or "-webkit-keyframes" or "-moz-keyframes" => ParseKeyframesRule(),
            "namespace" => ParseNamespaceRule(),
            "scope" => ParseScopeRule(),
            _ => SkipUnknownAtRule()
        };
    }

    private CssImportRule? ParseImportRule()
    {
        // @import url("...") or @import "..." ;
        var url = CollectTextUntilSemicolon();
        // Strip url() wrapper if present
        url = url.Trim();
        if (url.StartsWith("url(", StringComparison.OrdinalIgnoreCase) && url.EndsWith(')'))
        {
            url = url.Substring(4, url.Length - 5).Trim();
        }
        // Strip quotes
        if (url.Length >= 2 && ((url[0] == '"' && url[^1] == '"') || (url[0] == '\'' && url[^1] == '\'')))
        {
            url = url[1..^1];
        }
        if (string.IsNullOrEmpty(url)) return null;
        return new CssImportRule { Url = url };
    }

    private CssMediaRule ParseMediaRule()
    {
        // Collect media query text until '{'
        var query = CollectTextUntilBrace();
        _pos++; // skip '{'

        var rules = new List<CssRule>();
        var nestedAtRules = new List<CssAtRule>();
        ParseNestedRulesBlock(rules, nestedAtRules);

        return new CssMediaRule { MediaQuery = query, Rules = rules, NestedAtRules = nestedAtRules };
    }

    private CssSupportsRule ParseSupportsRule()
    {
        // Collect condition text until '{'
        var condition = CollectTextUntilBrace();
        _pos++; // skip '{'

        var rules = new List<CssRule>();
        var nestedAtRules = new List<CssAtRule>();
        ParseNestedRulesBlock(rules, nestedAtRules);

        return new CssSupportsRule { Condition = condition, Rules = rules, NestedAtRules = nestedAtRules };
    }

    private CssLayerRule ParseLayerRule()
    {
        // @layer name { ... } or @layer name;
        string? name = null;
        if (!IsEnd() && Current().Type == CssTokenType.Ident)
        {
            name = Current().Value;
            _pos++;
            SkipWhitespace();
        }

        if (!IsEnd() && Current().Type == CssTokenType.Semicolon)
        {
            _pos++; // @layer name; (declaration only)
            return new CssLayerRule { Name = name };
        }

        if (!IsEnd() && Current().Type == CssTokenType.LeftBrace)
        {
            _pos++; // skip '{'
            var rules = new List<CssRule>();
            ParseNestedRulesBlock(rules, null);
            return new CssLayerRule { Name = name, Rules = rules };
        }

        return new CssLayerRule { Name = name };
    }

    private CssFontFaceRule ParseFontFaceRule()
    {
        if (!IsEnd() && Current().Type == CssTokenType.LeftBrace)
        {
            _pos++; // skip '{'
            var descriptors = ParseDeclarationBlock();
            return new CssFontFaceRule { Descriptors = descriptors };
        }
        return new CssFontFaceRule { Descriptors = [] };
    }

    private CssKeyframesRule? ParseKeyframesRule()
    {
        // @keyframes name { ... }
        string name = "";
        if (!IsEnd() && (Current().Type == CssTokenType.Ident || Current().Type == CssTokenType.StringLiteral))
        {
            name = Current().Value;
            _pos++;
            SkipWhitespace();
        }

        if (IsEnd() || Current().Type != CssTokenType.LeftBrace)
            return new CssKeyframesRule { Name = name, Keyframes = [] };

        _pos++; // skip '{'
        var keyframes = new List<CssKeyframe>();
        SkipWhitespace();

        while (!IsEnd() && Current().Type != CssTokenType.RightBrace)
        {
            // Parse keyframe selector (e.g. "0%", "50%", "100%", "from", "to")
            var selectorParts = new List<string>();
            while (!IsEnd() && Current().Type != CssTokenType.LeftBrace)
            {
                if (Current().Type != CssTokenType.Whitespace && Current().Type != CssTokenType.Comma)
                {
                    selectorParts.Add(Current().ToString());
                }
                _pos++;
            }

            if (IsEnd()) break;
            _pos++; // skip '{'

            var declarations = ParseDeclarationBlock();
            var selectorStr = string.Join(", ", selectorParts);
            if (!string.IsNullOrWhiteSpace(selectorStr))
            {
                keyframes.Add(new CssKeyframe { Selector = selectorStr, Declarations = declarations });
            }
            SkipWhitespace();
        }

        if (!IsEnd()) _pos++; // skip '}'
        return new CssKeyframesRule { Name = name, Keyframes = keyframes };
    }

    private CssNamespaceRule? ParseNamespaceRule()
    {
        // @namespace prefix "uri"; or @namespace "uri";
        string? prefix = null;
        string uri = "";

        if (!IsEnd() && Current().Type == CssTokenType.Ident)
        {
            // Could be prefix or just an ident before the URI
            var ident = Current().Value;
            _pos++;
            SkipWhitespace();

            if (!IsEnd() && (Current().Type == CssTokenType.StringLiteral || Current().Type == CssTokenType.Function))
            {
                prefix = ident;
                uri = CollectTextUntilSemicolon();
            }
            else
            {
                uri = ident + CollectTextUntilSemicolon();
            }
        }
        else
        {
            uri = CollectTextUntilSemicolon();
        }

        // Strip url() wrapper
        uri = uri.Trim();
        if (uri.StartsWith("url(", StringComparison.OrdinalIgnoreCase) && uri.EndsWith(')'))
        {
            uri = uri.Substring(4, uri.Length - 5).Trim();
        }
        // Strip quotes
        if (uri.Length >= 2 && ((uri[0] == '"' && uri[^1] == '"') || (uri[0] == '\'' && uri[^1] == '\'')))
        {
            uri = uri[1..^1];
        }

        if (string.IsNullOrEmpty(uri)) return null;
        return new CssNamespaceRule { Prefix = prefix, Uri = uri };
    }

    private CssScopeRule ParseScopeRule()
    {
        // @scope (selector) { ... }
        string? scope = null;
        if (!IsEnd() && Current().Type == CssTokenType.LeftParen)
        {
            _pos++; // skip '('
            var parts = new List<string>();
            int depth = 1;
            while (!IsEnd() && depth > 0)
            {
                if (Current().Type == CssTokenType.LeftParen) depth++;
                else if (Current().Type == CssTokenType.RightParen)
                {
                    depth--;
                    if (depth == 0) { _pos++; break; }
                }
                if (Current().Type != CssTokenType.Whitespace || parts.Count > 0)
                    parts.Add(Current().ToString());
                _pos++;
            }
            scope = string.Join("", parts).Trim();
            SkipWhitespace();
        }

        if (!IsEnd() && Current().Type == CssTokenType.LeftBrace)
        {
            _pos++; // skip '{'
            var rules = new List<CssRule>();
            ParseNestedRulesBlock(rules, null);
            return new CssScopeRule { Scope = scope, Rules = rules };
        }

        return new CssScopeRule { Scope = scope };
    }

    private CssAtRule? SkipUnknownAtRule()
    {
        // Skip until ';' or balanced '{' ... '}'
        while (!IsEnd())
        {
            if (Current().Type == CssTokenType.Semicolon)
            {
                _pos++;
                return null;
            }
            if (Current().Type == CssTokenType.LeftBrace)
            {
                _pos++;
                SkipBalancedBraces();
                return null;
            }
            _pos++;
        }
        return null;
    }

    private void SkipBalancedBraces()
    {
        int depth = 1;
        while (!IsEnd() && depth > 0)
        {
            if (Current().Type == CssTokenType.LeftBrace) depth++;
            else if (Current().Type == CssTokenType.RightBrace) depth--;
            _pos++;
        }
    }

    /// <summary>
    /// Parses a block of nested rules and at-rules until the closing '}'.
    /// </summary>
    private void ParseNestedRulesBlock(List<CssRule> rules, List<CssAtRule>? nestedAtRules)
    {
        SkipWhitespace();
        while (!IsEnd() && Current().Type != CssTokenType.RightBrace)
        {
            if (Current().Type == CssTokenType.AtKeyword)
            {
                var atRule = ParseAtRule();
                if (atRule != null)
                    nestedAtRules?.Add(atRule);
            }
            else
            {
                var rule = ParseRule();
                if (rule != null)
                    rules.Add(rule);
            }
            SkipWhitespace();
        }
        if (!IsEnd()) _pos++; // skip '}'
    }

    /// <summary>
    /// Collects raw text from current position until a semicolon, consuming the semicolon.
    /// Reconstructs function calls with their opening parentheses.
    /// </summary>
    private string CollectTextUntilSemicolon()
    {
        var parts = new List<string>();
        while (!IsEnd() && Current().Type != CssTokenType.Semicolon)
        {
            var tok = Current();
            if (tok.Type == CssTokenType.Whitespace)
                parts.Add(" ");
            else if (tok.Type == CssTokenType.Function)
                parts.Add(tok.Value + "(");
            else
                parts.Add(tok.ToString());
            _pos++;
        }
        if (!IsEnd()) _pos++; // skip ';'
        return string.Join("", parts).Trim();
    }

    /// <summary>
    /// Collects raw text from current position until '{' (does not consume the '{').
    /// </summary>
    private string CollectTextUntilBrace()
    {
        var parts = new List<string>();
        while (!IsEnd() && Current().Type != CssTokenType.LeftBrace)
        {
            parts.Add(Current().Type == CssTokenType.Whitespace ? " " : Current().ToString());
            _pos++;
        }
        return string.Join("", parts).Trim();
    }

    #endregion

    private List<Declaration> ParseDeclarationBlock()
    {
        var declarations = new List<Declaration>();

        SkipWhitespace();
        while (!IsEnd() && Current().Type != CssTokenType.RightBrace)
        {
            var decl = ParseDeclaration();
            if (decl != null)
                declarations.AddRange(decl);
            SkipWhitespace();
        }

        if (!IsEnd()) _pos++; // skip '}'

        return declarations;
    }

    /// <summary>
    /// Parses a single declaration (property: value [!important];) and returns
    /// one or more declarations (more than one when expanding shorthands).
    /// </summary>
    private List<Declaration>? ParseDeclaration()
    {
        SkipWhitespace();
        if (IsEnd() || Current().Type == CssTokenType.RightBrace)
            return null;

        // Property name
        if (Current().Type != CssTokenType.Ident)
        {
            // Check for custom property (--*): may start with special Delim tokens
            if (Current().Type == CssTokenType.Delim && Current().Value == "-" && _pos + 1 < _tokens.Count)
            {
                // Consume --propertyname
                var propBuilder = new System.Text.StringBuilder();
                while (!IsEnd() && Current().Type != CssTokenType.Colon && Current().Type != CssTokenType.Whitespace)
                {
                    propBuilder.Append(Current().Value);
                    _pos++;
                }
                var customProp = propBuilder.ToString();
                if (!customProp.StartsWith("--", StringComparison.Ordinal)) { return null; }

                SkipWhitespace();
                if (IsEnd() || Current().Type != CssTokenType.Colon) return null;
                _pos++; // skip colon
                SkipWhitespace();

                // Collect entire value (custom properties preserve everything)
                var cpValueTokens = new List<CssToken>();
                while (!IsEnd() && Current().Type != CssTokenType.Semicolon && Current().Type != CssTokenType.RightBrace)
                {
                    cpValueTokens.Add(Current());
                    _pos++;
                }
                if (!IsEnd() && Current().Type == CssTokenType.Semicolon) _pos++;

                bool cpImportant = false;
                var cpTrimmed = TrimWhitespace(cpValueTokens);
                if (cpTrimmed.Count >= 2)
                {
                    var last = cpTrimmed[^1];
                    var secondLast = cpTrimmed[^2];
                    if (last.Type == CssTokenType.Ident && last.Value.Equals("important", StringComparison.OrdinalIgnoreCase)
                        && secondLast.Type == CssTokenType.Delim && secondLast.Value == "!")
                    {
                        cpImportant = true;
                        cpTrimmed = TrimWhitespace(cpTrimmed.GetRange(0, cpTrimmed.Count - 2));
                    }
                }

                // Store the raw value string for custom properties
                string rawValue = string.Join("", cpTrimmed.Select(t => t.Type == CssTokenType.Whitespace ? " " : t.ToString())).Trim();
                return [new Declaration
                {
                    Property = customProp,
                    Value = new CssValue { Type = CssValueType.Keyword, Raw = rawValue },
                    Important = cpImportant
                }];
            }

            // skip unexpected token
            _pos++;
            return null;
        }

        string property = Current().Value.ToLowerInvariant();
        _pos++;

        SkipWhitespace();

        // Expect colon
        if (IsEnd() || Current().Type != CssTokenType.Colon)
            return null;
        _pos++; // skip colon

        SkipWhitespace();

        // Collect value tokens until semicolon or '}'
        var valueTokens = new List<CssToken>();
        while (!IsEnd() && Current().Type != CssTokenType.Semicolon && Current().Type != CssTokenType.RightBrace)
        {
            valueTokens.Add(Current());
            _pos++;
        }

        // Skip semicolon if present
        if (!IsEnd() && Current().Type == CssTokenType.Semicolon)
            _pos++;

        // Check for !important
        bool important = false;
        var trimmed = TrimWhitespace(valueTokens);
        if (trimmed.Count >= 2)
        {
            var last = trimmed[^1];
            var secondLast = trimmed[^2];
            if (last.Type == CssTokenType.Ident
                && last.Value.Equals("important", StringComparison.OrdinalIgnoreCase)
                && secondLast.Type == CssTokenType.Delim
                && secondLast.Value == "!")
            {
                important = true;
                trimmed = TrimWhitespace(trimmed.GetRange(0, trimmed.Count - 2));
            }
        }

        // Handle shorthand expansion
        if (IsBoxShorthand(property))
        {
            return ExpandBoxShorthand(property, trimmed, important);
        }

        if (property == "border")
        {
            return ExpandBorderShorthand(trimmed, important);
        }

        if (property is "border-top" or "border-right" or "border-bottom" or "border-left")
        {
            return ExpandPerSideBorderShorthand(property, trimmed, important);
        }

        if (property == "border-radius")
        {
            return ExpandBorderRadiusShorthand(trimmed, important);
        }

        if (property == "flex")
        {
            return ExpandFlexShorthand(trimmed, important);
        }

        if (property == "flex-flow")
        {
            return ExpandFlexFlowShorthand(trimmed, important);
        }

        if (property == "inset")
        {
            return ExpandInsetShorthand(trimmed, important);
        }

        if (property == "inset-block")
        {
            return ExpandInsetBlockShorthand(trimmed, important);
        }

        if (property == "inset-inline")
        {
            return ExpandInsetInlineShorthand(trimmed, important);
        }

        if (property == "margin-block")
        {
            return ExpandMarginBlockShorthand(trimmed, important);
        }

        if (property == "margin-inline")
        {
            return ExpandMarginInlineShorthand(trimmed, important);
        }

        if (property == "padding-block")
        {
            return ExpandPaddingBlockShorthand(trimmed, important);
        }

        if (property == "padding-inline")
        {
            return ExpandPaddingInlineShorthand(trimmed, important);
        }

        if (property == "place-items")
        {
            return ExpandPlaceItemsShorthand(trimmed, important);
        }

        if (property == "place-content")
        {
            return ExpandPlaceContentShorthand(trimmed, important);
        }

        if (property == "place-self")
        {
            return ExpandPlaceSelfShorthand(trimmed, important);
        }

        if (property == "background")
        {
            return ExpandBackgroundShorthand(trimmed, important);
        }

        if (property == "outline")
        {
            return ExpandOutlineShorthand(trimmed, important);
        }

        // Parse single value
        var value = ParseValueTokens(trimmed);
        return
        [
            new Declaration
            {
                Property = property,
                Value = value,
                Important = important
            }
        ];
    }

    #region Value Parsing

    /// <summary>
    /// Parses an inline style string (e.g. "border: 1px solid red; padding: 5px")
    /// into a list of expanded declarations, handling shorthand expansion.
    /// </summary>
    public static List<Declaration> ParseInlineStyleDeclarations(string cssText)
    {
        // Wrap in a dummy rule so the parser's declaration block parser handles it
        var parser = new CssParser($"x {{ {cssText} }}");
        parser._tokens = new CssTokenizer($"x {{ {cssText} }}").Tokenize().ToList();
        parser._pos = 0;
        var rule = parser.ParseRule();
        return rule?.Declarations ?? [];
    }

    /// <summary>
    /// Parses a raw CSS value string into a <see cref="CssValue"/> using the full
    /// tokenizer (handles functions like rgb(), hsl(), calc(), etc.).
    /// Used by inline style parsing.
    /// </summary>
    public static CssValue ParseValueText(string rawValue)
    {
        var tokenizer = new CssTokenizer(rawValue);
        var tokens = tokenizer.Tokenize()
            .Where(t => t.Type != CssTokenType.EndOfFile)
            .ToList();
        return ParseValueTokens(tokens);
    }

    private static CssValue ParseValueTokens(List<CssToken> tokens)
    {
        var trimmed = TrimWhitespace(tokens);
        if (trimmed.Count == 0)
        {
            return new CssValue { Type = CssValueType.Keyword, Raw = "" };
        }

        // Single token
        if (trimmed.Count == 1)
        {
            return SingleTokenToValue(trimmed[0]);
        }

        // Function call: e.g., rgb(255, 0, 0)
        if (trimmed.Count >= 1 && trimmed[0].Type == CssTokenType.Function)
        {
            return ParseFunction(trimmed);
        }

        // Multiple tokens — join as raw string with keyword type
        string raw = string.Join("", trimmed.Select(t =>
        {
            if (t.Type == CssTokenType.Whitespace) return " ";
            return t.ToString();
        })).Trim();

        return new CssValue { Type = CssValueType.Keyword, Raw = raw };
    }

    private static CssValue SingleTokenToValue(CssToken token)
    {
        switch (token.Type)
        {
            case CssTokenType.Ident:
                if (Document.Color.TryFromName(token.Value, out var namedColor))
                {
                    return new CssValue
                    {
                        Type = CssValueType.Color,
                        Raw = token.Value,
                        ColorValue = namedColor
                    };
                }
                if (Document.Color.TryFromSystemColor(token.Value, out var sysColor))
                {
                    return new CssValue
                    {
                        Type = CssValueType.Color,
                        Raw = token.Value,
                        ColorValue = sysColor
                    };
                }
                return new CssValue { Type = CssValueType.Keyword, Raw = token.Value };

            case CssTokenType.Number:
                return new CssValue
                {
                    Type = CssValueType.Number,
                    Raw = token.Value,
                    NumericValue = token.NumericValue
                };

            case CssTokenType.Dimension:
            {
                var unit = token.Unit?.ToLowerInvariant();
                var valueType = unit switch
                {
                    "deg" or "grad" or "rad" or "turn" => CssValueType.Angle,
                    "s" or "ms" => CssValueType.Time,
                    "dpi" or "dpcm" or "dppx" => CssValueType.Resolution,
                    _ => CssValueType.Length
                };
                return new CssValue
                {
                    Type = valueType,
                    Raw = token.Value,
                    NumericValue = token.NumericValue,
                    Unit = token.Unit
                };
            }

            case CssTokenType.Percentage:
                return new CssValue
                {
                    Type = CssValueType.Percentage,
                    Raw = token.NumericValue.ToString(CultureInfo.InvariantCulture) + "%",
                    NumericValue = token.NumericValue,
                    Unit = "%"
                };

            case CssTokenType.Hash:
                var color = Document.Color.FromHex(token.Value);
                return new CssValue
                {
                    Type = CssValueType.Color,
                    Raw = "#" + token.Value,
                    ColorValue = color
                };

            case CssTokenType.StringLiteral:
                return new CssValue
                {
                    Type = CssValueType.StringLiteral,
                    Raw = token.Value
                };

            default:
                return new CssValue
                {
                    Type = CssValueType.Keyword,
                    Raw = token.Value
                };
        }
    }

    private static CssValue ParseFunction(List<CssToken> tokens)
    {
        string funcName = tokens[0].Value.ToLowerInvariant();

        // Collect argument tokens (everything after the Function token, excluding final closing paren)
        // Handle nested parentheses/functions correctly
        var argTokens = new List<CssToken>();
        int depth = 1;
        for (int i = 1; i < tokens.Count; i++)
        {
            if (tokens[i].Type == CssTokenType.Function || tokens[i].Type == CssTokenType.LeftParen)
                depth++;
            else if (tokens[i].Type == CssTokenType.RightParen)
            {
                depth--;
                if (depth == 0) break;
            }
            argTokens.Add(tokens[i]);
        }

        if (funcName is "rgb" or "rgba")
        {
            return ParseRgbFunction(funcName, argTokens);
        }

        if (funcName is "hsl" or "hsla")
        {
            return ParseHslFunction(funcName, argTokens);
        }

        if (funcName == "hwb")
        {
            return ParseHwbFunction(argTokens);
        }

        if (funcName == "lab")
        {
            return ParseLabFunction(argTokens);
        }

        if (funcName == "lch")
        {
            return ParseLchFunction(argTokens);
        }

        if (funcName == "oklab")
        {
            return ParseOklabFunction(argTokens);
        }

        if (funcName == "oklch")
        {
            return ParseOklchFunction(argTokens);
        }

        if (funcName == "color")
        {
            return ParseColorFunction(argTokens);
        }

        if (funcName == "color-mix")
        {
            return ParseColorMixFunction(argTokens);
        }

        if (funcName == "light-dark")
        {
            return ParseLightDarkFunction(argTokens);
        }

        if (funcName == "var")
        {
            return ParseVarFunction(argTokens);
        }

        if (funcName is "calc" or "min" or "max" or "clamp")
        {
            return ParseCalcFunction(funcName, argTokens);
        }

        // Extended math functions — parse as calc expressions
        CalcMathFunction? topLevelMathFunc = funcName switch
        {
            "abs" => CalcMathFunction.Abs,
            "sign" => CalcMathFunction.Sign,
            "round" => CalcMathFunction.Round,
            "mod" => CalcMathFunction.Mod,
            "rem" => CalcMathFunction.Rem,
            "sin" => CalcMathFunction.Sin,
            "cos" => CalcMathFunction.Cos,
            "tan" => CalcMathFunction.Tan,
            "asin" => CalcMathFunction.Asin,
            "acos" => CalcMathFunction.Acos,
            "atan" => CalcMathFunction.Atan,
            "atan2" => CalcMathFunction.Atan2,
            "pow" => CalcMathFunction.Pow,
            "sqrt" => CalcMathFunction.Sqrt,
            "hypot" => CalcMathFunction.Hypot,
            "log" => CalcMathFunction.Log,
            "exp" => CalcMathFunction.Exp,
            _ => null
        };

        if (topLevelMathFunc != null)
        {
            var args = ParseCalcFunctionArgs(argTokens);
            var node = new CalcFunctionNode(topLevelMathFunc.Value, args);
            return new CssValue
            {
                Type = CssValueType.Calc,
                Raw = funcName + "(...)",
                CalcExpr = node
            };
        }

        if (funcName is "linear-gradient" or "repeating-linear-gradient")
        {
            var gradient = ParseLinearGradientArgs(argTokens);
            if (gradient != null)
            {
                return new CssValue
                {
                    Type = CssValueType.Keyword,
                    Raw = funcName + "(...)",
                    Gradient = gradient
                };
            }
        }

        if (funcName is "radial-gradient" or "repeating-radial-gradient")
        {
            var gradient = ParseRadialGradientArgs(argTokens);
            if (gradient != null)
            {
                return new CssValue
                {
                    Type = CssValueType.Keyword,
                    Raw = funcName + "(...)",
                    Gradient = gradient
                };
            }
        }

        if (funcName is "conic-gradient" or "repeating-conic-gradient")
        {
            var gradient = ParseConicGradientArgs(argTokens);
            if (gradient != null)
            {
                return new CssValue
                {
                    Type = CssValueType.Keyword,
                    Raw = funcName + "(...)",
                    Gradient = gradient
                };
            }
        }

        // Fallback: join everything as keyword, adding '(' after Function tokens
        string raw = string.Join("", tokens.Select(t =>
            t.Type == CssTokenType.Function ? t.Value + "(" : t.ToString()));
        return new CssValue { Type = CssValueType.Keyword, Raw = raw };
    }

    private static CssValue ParseRgbFunction(string funcName, List<CssToken> argTokens)
    {
        // Support both comma-separated and space-separated syntax:
        // rgb(255, 0, 0)  or  rgb(255 0 0)  or  rgb(255 0 0 / 0.5)
        // Also: percentage alpha: rgba(0, 0, 0, 50%) and none keyword
        var numbers = new List<double>();
        var isPercentage = new List<bool>();
        bool hasSlashAlpha = false;
        double slashAlpha = 1.0;
        bool hasCommas = argTokens.Any(t => t.Type == CssTokenType.Comma);

        if (hasCommas)
        {
            // Legacy comma-separated: rgb(r, g, b) or rgba(r, g, b, a)
            foreach (var t in argTokens)
            {
                if (t.Type == CssTokenType.Ident && t.Value.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    numbers.Add(0);
                    isPercentage.Add(false);
                }
                else if (t.Type is CssTokenType.Number or CssTokenType.Percentage or CssTokenType.Dimension)
                {
                    numbers.Add(t.NumericValue);
                    isPercentage.Add(t.Type == CssTokenType.Percentage);
                }
            }
        }
        else
        {
            // Modern space-separated: rgb(r g b) or rgb(r g b / alpha)
            for (int i = 0; i < argTokens.Count; i++)
            {
                var t = argTokens[i];
                if (t.Type == CssTokenType.Delim && t.Value == "/")
                {
                    // Everything after / is the alpha value
                    for (int j = i + 1; j < argTokens.Count; j++)
                    {
                        var at = argTokens[j];
                        if (at.Type == CssTokenType.Ident && at.Value.Equals("none", StringComparison.OrdinalIgnoreCase))
                        {
                            hasSlashAlpha = true;
                            slashAlpha = 0;
                            break;
                        }
                        if (at.Type is CssTokenType.Number)
                        {
                            hasSlashAlpha = true;
                            slashAlpha = at.NumericValue;
                            break;
                        }
                        if (at.Type is CssTokenType.Percentage)
                        {
                            hasSlashAlpha = true;
                            slashAlpha = at.NumericValue / 100.0;
                            break;
                        }
                    }
                    break;
                }
                if (t.Type == CssTokenType.Ident && t.Value.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    numbers.Add(0);
                    isPercentage.Add(false);
                }
                else if (t.Type is CssTokenType.Number or CssTokenType.Percentage or CssTokenType.Dimension)
                {
                    numbers.Add(t.NumericValue);
                    isPercentage.Add(t.Type == CssTokenType.Percentage);
                }
            }
        }

        if (numbers.Count >= 3)
        {
            // Handle percentage values for R/G/B (e.g., rgb(100%, 0%, 0%))
            byte r = isPercentage[0] ? (byte)Math.Clamp((int)Math.Round(numbers[0] * 255 / 100), 0, 255) : ClampByte(numbers[0]);
            byte g = isPercentage[1] ? (byte)Math.Clamp((int)Math.Round(numbers[1] * 255 / 100), 0, 255) : ClampByte(numbers[1]);
            byte b = isPercentage[2] ? (byte)Math.Clamp((int)Math.Round(numbers[2] * 255 / 100), 0, 255) : ClampByte(numbers[2]);

            Document.Color color;
            if (hasSlashAlpha)
            {
                byte a = (byte)Math.Clamp((int)Math.Round(slashAlpha * 255), 0, 255);
                color = Document.Color.FromRgba(r, g, b, a);
            }
            else if (numbers.Count >= 4)
            {
                // Legacy 4th arg: percentage alpha (50% = 0.5 alpha) or numeric (0-1)
                double alphaVal = isPercentage.Count > 3 && isPercentage[3]
                    ? numbers[3] / 100.0
                    : numbers[3];
                byte a = (byte)Math.Clamp((int)Math.Round(alphaVal * 255), 0, 255);
                color = Document.Color.FromRgba(r, g, b, a);
            }
            else
            {
                color = Document.Color.FromRgb(r, g, b);
            }

            return new CssValue
            {
                Type = CssValueType.Color,
                Raw = funcName + "(...)",
                ColorValue = color
            };
        }

        return new CssValue { Type = CssValueType.Keyword, Raw = funcName + "(...)" };
    }

    private static CssValue ParseHslFunction(string funcName, List<CssToken> argTokens)
    {
        // hsl(h, s%, l%) or hsl(h s% l%) or hsl(h, s%, l%, a) or hsl(h s% l% / a)
        // Also: none keyword, percentage alpha
        var numbers = new List<double>();
        var isPercentage = new List<bool>();
        bool hasSlashAlpha = false;
        double slashAlpha = 1.0;
        bool hasCommas = argTokens.Any(t => t.Type == CssTokenType.Comma);

        if (hasCommas)
        {
            foreach (var t in argTokens)
            {
                if (t.Type == CssTokenType.Ident && t.Value.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    numbers.Add(0);
                    isPercentage.Add(false);
                }
                else if (t.Type is CssTokenType.Number or CssTokenType.Percentage or CssTokenType.Dimension)
                {
                    numbers.Add(t.Type == CssTokenType.Percentage ? t.NumericValue / 100.0 : t.NumericValue);
                    isPercentage.Add(t.Type == CssTokenType.Percentage);
                }
            }
        }
        else
        {
            for (int i = 0; i < argTokens.Count; i++)
            {
                var t = argTokens[i];
                if (t.Type == CssTokenType.Delim && t.Value == "/")
                {
                    for (int j = i + 1; j < argTokens.Count; j++)
                    {
                        var at = argTokens[j];
                        if (at.Type == CssTokenType.Ident && at.Value.Equals("none", StringComparison.OrdinalIgnoreCase))
                        {
                            hasSlashAlpha = true;
                            slashAlpha = 0;
                            break;
                        }
                        if (at.Type is CssTokenType.Number)
                        {
                            hasSlashAlpha = true;
                            slashAlpha = at.NumericValue;
                            break;
                        }
                        if (at.Type is CssTokenType.Percentage)
                        {
                            hasSlashAlpha = true;
                            slashAlpha = at.NumericValue / 100.0;
                            break;
                        }
                    }
                    break;
                }
                if (t.Type == CssTokenType.Ident && t.Value.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    numbers.Add(0);
                    isPercentage.Add(false);
                }
                else if (t.Type is CssTokenType.Number or CssTokenType.Percentage or CssTokenType.Dimension)
                {
                    numbers.Add(t.Type == CssTokenType.Percentage ? t.NumericValue / 100.0 : t.NumericValue);
                    isPercentage.Add(t.Type == CssTokenType.Percentage);
                }
            }
        }

        if (numbers.Count >= 3)
        {
            double h = numbers[0]; // degrees
            double s = numbers[1]; // 0-1 (already normalized from %)
            double l = numbers[2]; // 0-1 (already normalized from %)

            double alpha;
            if (hasSlashAlpha)
                alpha = slashAlpha;
            else if (numbers.Count >= 4)
            {
                // Legacy 4th arg: percentage alpha or numeric
                alpha = isPercentage.Count > 3 && isPercentage[3]
                    ? numbers[3]  // already / 100 above
                    : numbers[3];
            }
            else
                alpha = 1.0;

            var color = Document.Color.FromHsla(h, s, l, alpha);

            return new CssValue
            {
                Type = CssValueType.Color,
                Raw = funcName + "(...)",
                ColorValue = color
            };
        }

        return new CssValue { Type = CssValueType.Keyword, Raw = funcName + "(...)" };
    }

    // --- var() ---
    private static CssValue ParseVarFunction(List<CssToken> argTokens)
    {
        // var(--name) or var(--name, fallback)
        var trimmed = TrimWhitespace(argTokens.ToList());
        if (trimmed.Count == 0) return new CssValue { Type = CssValueType.Keyword, Raw = "var()" };

        // Extract custom property name (everything before first comma)
        var nameTokens = new List<CssToken>();
        var fallbackTokens = new List<CssToken>();
        bool pastComma = false;

        foreach (var t in trimmed)
        {
            if (!pastComma && t.Type == CssTokenType.Comma)
            {
                pastComma = true;
                continue;
            }
            if (pastComma)
                fallbackTokens.Add(t);
            else
                nameTokens.Add(t);
        }

        string varName = string.Join("", nameTokens.Select(t => t.Value)).Trim();
        string? fallback = fallbackTokens.Count > 0
            ? string.Join("", fallbackTokens.Select(t => t.Type == CssTokenType.Whitespace ? " " : t.ToString())).Trim()
            : null;

        string raw = fallback != null ? $"var({varName}, {fallback})" : $"var({varName})";

        return new CssValue
        {
            Type = CssValueType.Keyword,
            Raw = raw,
            VarName = varName,
            VarFallback = fallback
        };
    }

    // --- HWB ---
    private static CssValue ParseHwbFunction(List<CssToken> argTokens)
    {
        // hwb(h w% b% [/ alpha])
        var (numbers, _, slashAlpha) = ParseColorArgs(argTokens, normalizePercentage: true);
        if (numbers.Count >= 3)
        {
            double h = numbers[0];
            double w = numbers[1];
            double b = numbers[2];
            var color = Document.Color.FromHwba(h, w, b, slashAlpha);
            return new CssValue { Type = CssValueType.Color, Raw = "hwb(...)", ColorValue = color };
        }
        return new CssValue { Type = CssValueType.Keyword, Raw = "hwb(...)" };
    }

    // --- Lab ---
    private static CssValue ParseLabFunction(List<CssToken> argTokens)
    {
        // lab(L a b [/ alpha]) — L is 0-100, a/b are unbounded
        var (numbers, _, slashAlpha) = ParseColorArgs(argTokens, normalizePercentage: false);
        if (numbers.Count >= 3)
        {
            var color = Document.Color.FromLaba(numbers[0], numbers[1], numbers[2], slashAlpha);
            return new CssValue { Type = CssValueType.Color, Raw = "lab(...)", ColorValue = color };
        }
        return new CssValue { Type = CssValueType.Keyword, Raw = "lab(...)" };
    }

    // --- LCH ---
    private static CssValue ParseLchFunction(List<CssToken> argTokens)
    {
        // lch(L C H [/ alpha])
        var (numbers, _, slashAlpha) = ParseColorArgs(argTokens, normalizePercentage: false);
        if (numbers.Count >= 3)
        {
            var color = Document.Color.FromLcha(numbers[0], numbers[1], numbers[2], slashAlpha);
            return new CssValue { Type = CssValueType.Color, Raw = "lch(...)", ColorValue = color };
        }
        return new CssValue { Type = CssValueType.Keyword, Raw = "lch(...)" };
    }

    // --- OKLab ---
    private static CssValue ParseOklabFunction(List<CssToken> argTokens)
    {
        // oklab(L a b [/ alpha]) — L is 0-1, a/b are unbounded
        var (numbers, _, slashAlpha) = ParseColorArgs(argTokens, normalizePercentage: false);
        if (numbers.Count >= 3)
        {
            var color = Document.Color.FromOklaba(numbers[0], numbers[1], numbers[2], slashAlpha);
            return new CssValue { Type = CssValueType.Color, Raw = "oklab(...)", ColorValue = color };
        }
        return new CssValue { Type = CssValueType.Keyword, Raw = "oklab(...)" };
    }

    // --- OKLCH ---
    private static CssValue ParseOklchFunction(List<CssToken> argTokens)
    {
        // oklch(L C H [/ alpha])
        var (numbers, _, slashAlpha) = ParseColorArgs(argTokens, normalizePercentage: false);
        if (numbers.Count >= 3)
        {
            var color = Document.Color.FromOklcha(numbers[0], numbers[1], numbers[2], slashAlpha);
            return new CssValue { Type = CssValueType.Color, Raw = "oklch(...)", ColorValue = color };
        }
        return new CssValue { Type = CssValueType.Keyword, Raw = "oklch(...)" };
    }

    // --- color() ---
    private static CssValue ParseColorFunction(List<CssToken> argTokens)
    {
        // color(srgb r g b [/ alpha]) — currently only srgb supported, maps to rgb
        string? colorSpace = null;
        var numbers = new List<double>();
        double alpha = 1.0;
        bool pastSlash = false;

        foreach (var t in argTokens)
        {
            if (t.Type == CssTokenType.Ident && colorSpace == null && t.Value.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                // If first ident is "none", treat as 0
                numbers.Add(0);
                continue;
            }
            if (t.Type == CssTokenType.Ident && colorSpace == null)
            {
                colorSpace = t.Value.ToLowerInvariant();
                continue;
            }
            if (t.Type == CssTokenType.Delim && t.Value == "/")
            {
                pastSlash = true;
                continue;
            }
            if (t.Type == CssTokenType.Ident && t.Value.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                if (pastSlash) alpha = 0;
                else numbers.Add(0);
                continue;
            }
            if (t.Type is CssTokenType.Number or CssTokenType.Percentage)
            {
                double val = t.Type == CssTokenType.Percentage ? t.NumericValue / 100.0 : t.NumericValue;
                if (pastSlash) alpha = val;
                else numbers.Add(val);
            }
        }

        if (numbers.Count >= 3)
        {
            // For srgb, values are 0-1 floats
            var color = new Document.Color(
                (float)Math.Clamp(numbers[0], 0, 1),
                (float)Math.Clamp(numbers[1], 0, 1),
                (float)Math.Clamp(numbers[2], 0, 1),
                (float)Math.Clamp(alpha, 0, 1));
            return new CssValue { Type = CssValueType.Color, Raw = "color(...)", ColorValue = color };
        }
        return new CssValue { Type = CssValueType.Keyword, Raw = "color(...)" };
    }

    // --- color-mix() ---
    private static CssValue ParseColorMixFunction(List<CssToken> argTokens)
    {
        // color-mix(in srgb, color1 [p1%], color2 [p2%])
        // Split by commas
        var parts = SplitByComma(argTokens);
        if (parts.Count < 3) return new CssValue { Type = CssValueType.Keyword, Raw = "color-mix(...)" };

        // parts[0] = "in srgb" (color space specification — ignored, we use sRGB)
        // parts[1] = color1 [percentage]
        // parts[2] = color2 [percentage]
        var (color1, p1) = ParseColorMixArg(parts[1]);
        var (color2, p2) = ParseColorMixArg(parts[2]);

        if (color1 == null || color2 == null)
            return new CssValue { Type = CssValueType.Keyword, Raw = "color-mix(...)" };

        var result = Document.Color.ColorMix(color1.Value, color2.Value, p1, p2);
        return new CssValue { Type = CssValueType.Color, Raw = "color-mix(...)", ColorValue = result };
    }

    private static (Document.Color? color, double percentage) ParseColorMixArg(List<CssToken> tokens)
    {
        var trimmed = TrimWhitespace(tokens);
        Document.Color? color = null;
        double pct = double.NaN;

        foreach (var t in trimmed)
        {
            if (t.Type == CssTokenType.Percentage)
            {
                pct = t.NumericValue / 100.0;
            }
            else if (t.Type == CssTokenType.Hash)
            {
                color = Document.Color.FromHex(t.Value);
            }
            else if (t.Type == CssTokenType.Ident && Document.Color.TryFromName(t.Value, out var named))
            {
                color = named;
            }
            else if (t.Type == CssTokenType.Function)
            {
                // Parse nested function call (e.g., rgb(...))
                var funcTokens = new List<CssToken> { t };
                // Collect remaining tokens as function args
                int idx = trimmed.IndexOf(t);
                for (int i = idx + 1; i < trimmed.Count; i++)
                {
                    funcTokens.Add(trimmed[i]);
                    if (trimmed[i].Type == CssTokenType.RightParen) break;
                }
                var val = ParseFunction(funcTokens);
                if (val.ColorValue.HasValue) color = val.ColorValue.Value;
            }
        }

        return (color, pct);
    }

    // --- light-dark() ---
    private static CssValue ParseLightDarkFunction(List<CssToken> argTokens)
    {
        // light-dark(lightColor, darkColor)
        var parts = SplitByComma(argTokens);
        if (parts.Count < 2) return new CssValue { Type = CssValueType.Keyword, Raw = "light-dark(...)" };

        var lightVal = ParseValueTokens(parts[0]);
        var darkVal = ParseValueTokens(parts[1]);

        var lightColor = lightVal.ColorValue ?? Document.Color.Black;
        var darkColor = darkVal.ColorValue ?? Document.Color.Black;

        var result = Document.Color.LightDark(lightColor, darkColor);
        return new CssValue { Type = CssValueType.Color, Raw = "light-dark(...)", ColorValue = result };
    }

    /// <summary>
    /// Helper to parse space-separated color function arguments with optional / alpha.
    /// Returns (numbers, isPercentage, resolvedAlpha).
    /// </summary>
    private static (List<double> numbers, List<bool> isPercentage, double alpha) ParseColorArgs(
        List<CssToken> argTokens, bool normalizePercentage)
    {
        var numbers = new List<double>();
        var isPercentage = new List<bool>();
        double alpha = 1.0;
        bool pastSlash = false;

        foreach (var t in argTokens)
        {
            if (t.Type == CssTokenType.Whitespace) continue;

            if (t.Type == CssTokenType.Delim && t.Value == "/")
            {
                pastSlash = true;
                continue;
            }

            if (t.Type == CssTokenType.Ident && t.Value.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                if (pastSlash) alpha = 0;
                else { numbers.Add(0); isPercentage.Add(false); }
                continue;
            }

            if (t.Type is CssTokenType.Number or CssTokenType.Percentage or CssTokenType.Dimension)
            {
                bool isPct = t.Type == CssTokenType.Percentage;
                double val = isPct && normalizePercentage ? t.NumericValue / 100.0 : t.NumericValue;

                if (pastSlash)
                    alpha = isPct ? t.NumericValue / 100.0 : t.NumericValue;
                else
                {
                    numbers.Add(val);
                    isPercentage.Add(isPct);
                }
            }
        }

        return (numbers, isPercentage, alpha);
    }

    private static List<List<CssToken>> SplitByComma(List<CssToken> tokens)
    {
        var result = new List<List<CssToken>>();
        var current = new List<CssToken>();
        foreach (var t in tokens)
        {
            if (t.Type == CssTokenType.Comma)
            {
                if (current.Count > 0) result.Add(current);
                current = [];
            }
            else
            {
                current.Add(t);
            }
        }
        if (current.Count > 0) result.Add(current);
        return result;
    }

    private static byte ClampByte(double v)
        => (byte)Math.Clamp((int)Math.Round(v), 0, 255);

    private static CssValue ParseCalcFunction(string funcName, List<CssToken> argTokens)
    {
        var node = funcName switch
        {
            "calc" => ParseCalcExpression(argTokens, 0, out _),
            "min" => ParseMinMaxClamp(CalcMinMaxType.Min, argTokens),
            "max" => ParseMinMaxClamp(CalcMinMaxType.Max, argTokens),
            "clamp" => ParseMinMaxClamp(CalcMinMaxType.Clamp, argTokens),
            _ => null
        };

        if (node != null)
        {
            return new CssValue
            {
                Type = CssValueType.Calc,
                Raw = funcName + "(...)",
                CalcExpr = node
            };
        }

        return new CssValue { Type = CssValueType.Keyword, Raw = funcName + "(...)" };
    }

    private static CalcNode? ParseCalcExpression(List<CssToken> tokens, int start, out int end)
    {
        end = start;
        var node = ParseCalcTerm(tokens, start, out end);
        if (node == null) return null;

        while (end < tokens.Count)
        {
            SkipWhitespaceInList(tokens, ref end);
            if (end >= tokens.Count) break;

            var tok = tokens[end];
            CalcOp? op = null;
            if (tok.Type == CssTokenType.Delim && tok.Value == "+") op = CalcOp.Add;
            else if ((tok.Type == CssTokenType.Delim || tok.Type == CssTokenType.Ident) && tok.Value == "-") op = CalcOp.Sub;
            else if (tok.Type == CssTokenType.Ident && tok.Value == "+") op = CalcOp.Add;

            if (op == null) break;

            end++;
            SkipWhitespaceInList(tokens, ref end);

            var right = ParseCalcTerm(tokens, end, out end);
            if (right == null) break;

            node = new CalcBinaryNode(node, op.Value, right);
        }

        return node;
    }

    private static CalcNode? ParseCalcTerm(List<CssToken> tokens, int start, out int end)
    {
        end = start;
        var node = ParseCalcAtom(tokens, start, out end);
        if (node == null) return null;

        while (end < tokens.Count)
        {
            SkipWhitespaceInList(tokens, ref end);
            if (end >= tokens.Count) break;

            var tok = tokens[end];
            CalcOp? op = null;
            if (tok.Type == CssTokenType.Delim && tok.Value == "*") op = CalcOp.Mul;
            else if ((tok.Type == CssTokenType.Delim || tok.Type == CssTokenType.Ident) && tok.Value == "/") op = CalcOp.Div;

            if (op == null) break;

            end++;
            SkipWhitespaceInList(tokens, ref end);

            var right = ParseCalcAtom(tokens, end, out end);
            if (right == null) break;

            node = new CalcBinaryNode(node, op.Value, right);
        }

        return node;
    }

    private static CalcNode? ParseCalcAtom(List<CssToken> tokens, int start, out int end)
    {
        end = start;
        if (start >= tokens.Count) return null;

        SkipWhitespaceInList(tokens, ref end);
        if (end >= tokens.Count) return null;

        var tok = tokens[end];

        if (tok.Type == CssTokenType.LeftParen)
        {
            end++;
            var inner = ParseCalcExpression(tokens, end, out end);
            SkipWhitespaceInList(tokens, ref end);
            if (end < tokens.Count && tokens[end].Type == CssTokenType.RightParen)
                end++;
            return inner;
        }

        if (tok.Type == CssTokenType.Function)
        {
            var fname = tok.Value.ToLowerInvariant();
            end++;
            var nestedArgs = new List<CssToken>();
            int depth = 1;
            while (end < tokens.Count && depth > 0)
            {
                if (tokens[end].Type == CssTokenType.LeftParen) depth++;
                else if (tokens[end].Type == CssTokenType.RightParen)
                {
                    depth--;
                    if (depth == 0) { end++; break; }
                }
                nestedArgs.Add(tokens[end]);
                end++;
            }

            if (fname is "min" or "max" or "clamp")
                return ParseMinMaxClamp(fname == "min" ? CalcMinMaxType.Min : fname == "max" ? CalcMinMaxType.Max : CalcMinMaxType.Clamp, nestedArgs);
            if (fname == "calc")
                return ParseCalcExpression(nestedArgs, 0, out _);

            // Extended math functions (CSS Values Level 4)
            CalcMathFunction? mathFunc = fname switch
            {
                "abs" => CalcMathFunction.Abs,
                "sign" => CalcMathFunction.Sign,
                "round" => CalcMathFunction.Round,
                "mod" => CalcMathFunction.Mod,
                "rem" => CalcMathFunction.Rem,
                "sin" => CalcMathFunction.Sin,
                "cos" => CalcMathFunction.Cos,
                "tan" => CalcMathFunction.Tan,
                "asin" => CalcMathFunction.Asin,
                "acos" => CalcMathFunction.Acos,
                "atan" => CalcMathFunction.Atan,
                "atan2" => CalcMathFunction.Atan2,
                "pow" => CalcMathFunction.Pow,
                "sqrt" => CalcMathFunction.Sqrt,
                "hypot" => CalcMathFunction.Hypot,
                "log" => CalcMathFunction.Log,
                "exp" => CalcMathFunction.Exp,
                _ => null
            };

            if (mathFunc != null)
            {
                var args = ParseCalcFunctionArgs(nestedArgs);
                return new CalcFunctionNode(mathFunc.Value, args);
            }

            return null;
        }

        if (tok.Type is CssTokenType.Number or CssTokenType.Dimension or CssTokenType.Percentage)
        {
            end++;
            var value = SingleTokenToValue(tok);
            return new CalcValueNode(value);
        }

        return null;
    }

    private static CalcMinMaxNode? ParseMinMaxClamp(CalcMinMaxType type, List<CssToken> argTokens)
    {
        var args = new List<CalcNode>();
        var current = new List<CssToken>();

        foreach (var t in argTokens)
        {
            if (t.Type == CssTokenType.Comma)
            {
                if (current.Count > 0)
                {
                    var node = ParseCalcExpression(current, 0, out _);
                    if (node != null) args.Add(node);
                    current = [];
                }
            }
            else
            {
                current.Add(t);
            }
        }

        if (current.Count > 0)
        {
            var node = ParseCalcExpression(current, 0, out _);
            if (node != null) args.Add(node);
        }

        if (args.Count == 0) return null;

        return new CalcMinMaxNode(type, args);
    }

    /// <summary>
    /// Parses comma-separated calc arguments for math functions.
    /// </summary>
    private static List<CalcNode> ParseCalcFunctionArgs(List<CssToken> argTokens)
    {
        var args = new List<CalcNode>();
        var current = new List<CssToken>();

        foreach (var t in argTokens)
        {
            if (t.Type == CssTokenType.Comma)
            {
                if (current.Count > 0)
                {
                    var node = ParseCalcExpression(current, 0, out _);
                    if (node != null) args.Add(node);
                    current = [];
                }
            }
            else
            {
                current.Add(t);
            }
        }

        if (current.Count > 0)
        {
            var node = ParseCalcExpression(current, 0, out _);
            if (node != null) args.Add(node);
        }

        return args;
    }

    private static void SkipWhitespaceInList(List<CssToken> tokens, ref int pos)
    {
        while (pos < tokens.Count && tokens[pos].Type == CssTokenType.Whitespace)
            pos++;
    }

    #endregion

    #region Token Helpers

    private CssToken Current() => _tokens[_pos];

    private bool IsEnd()
        => _pos >= _tokens.Count
           || _tokens[_pos].Type == CssTokenType.EndOfFile;

    private void SkipWhitespace()
    {
        while (!IsEnd() && Current().Type == CssTokenType.Whitespace)
            _pos++;
    }

    private static List<CssToken> TrimWhitespace(List<CssToken> tokens)
    {
        int start = 0;
        while (start < tokens.Count && tokens[start].Type == CssTokenType.Whitespace)
            start++;
        int end = tokens.Count - 1;
        while (end >= start && tokens[end].Type == CssTokenType.Whitespace)
            end--;
        if (start > end) return [];
        return tokens.GetRange(start, end - start + 1);
    }

    /// <summary>
    /// Splits a list of tokens into groups separated by whitespace tokens.
    /// </summary>
    private static List<List<CssToken>> SplitByWhitespace(List<CssToken> tokens)
    {
        var result = new List<List<CssToken>>();
        var current = new List<CssToken>();

        foreach (var token in tokens)
        {
            if (token.Type == CssTokenType.Whitespace)
            {
                if (current.Count > 0)
                {
                    result.Add(current);
                    current = [];
                }
            }
            else
            {
                current.Add(token);
            }
        }

        if (current.Count > 0)
            result.Add(current);

        return result;
    }

    #endregion

    #region Gradient Parsing

    private static LinearGradient? ParseLinearGradientArgs(List<CssToken> argTokens)
    {
        var groups = SplitByCommaRespectingDepth(argTokens);
        if (groups.Count < 2) return null;

        float angle = 180f;
        int stopStart = 0;

        var firstGroup = TrimWhitespace(groups[0]);
        if (TryParseGradientDirection(firstGroup, out float parsedAngle))
        {
            angle = parsedAngle;
            stopStart = 1;
        }

        var stops = new List<ColorStop>();
        for (int i = stopStart; i < groups.Count; i++)
        {
            var stop = ParseColorStop(groups[i]);
            if (stop != null) stops.Add(stop);
        }

        if (stops.Count < 2) return null;
        DistributeStopPositions(stops);
        return new LinearGradient { AngleDeg = angle, ColorStops = stops };
    }

    private static RadialGradient? ParseRadialGradientArgs(List<CssToken> argTokens)
    {
        var groups = SplitByCommaRespectingDepth(argTokens);
        if (groups.Count < 2) return null;

        var shape = RadialGradientShape.Ellipse;
        var size = RadialGradientSize.FarthestCorner;
        float cx = 0.5f, cy = 0.5f;
        int stopStart = 0;

        var firstGroup = TrimWhitespace(groups[0]);
        if (TryParseRadialConfig(firstGroup, out shape, out size, out cx, out cy))
            stopStart = 1;

        var stops = new List<ColorStop>();
        for (int i = stopStart; i < groups.Count; i++)
        {
            var stop = ParseColorStop(groups[i]);
            if (stop != null) stops.Add(stop);
        }

        if (stops.Count < 2) return null;
        DistributeStopPositions(stops);
        return new RadialGradient { Shape = shape, Size = size, CenterX = cx, CenterY = cy, ColorStops = stops };
    }

    private static ConicGradient? ParseConicGradientArgs(List<CssToken> argTokens)
    {
        var groups = SplitByCommaRespectingDepth(argTokens);
        if (groups.Count < 2) return null;

        float fromAngle = 0f;
        float cx = 0.5f, cy = 0.5f;
        int stopStart = 0;

        var firstGroup = TrimWhitespace(groups[0]);
        if (TryParseConicConfig(firstGroup, out fromAngle, out cx, out cy))
            stopStart = 1;

        var stops = new List<ColorStop>();
        for (int i = stopStart; i < groups.Count; i++)
        {
            var stop = ParseColorStop(groups[i]);
            if (stop != null) stops.Add(stop);
        }

        if (stops.Count < 2) return null;
        DistributeStopPositions(stops);
        return new ConicGradient { FromAngleDeg = fromAngle, CenterX = cx, CenterY = cy, ColorStops = stops };
    }

    private static bool TryParseGradientDirection(List<CssToken> tokens, out float angle)
    {
        angle = 180f;
        if (tokens.Count == 0) return false;

        if (tokens.Count == 1 && tokens[0].Type is CssTokenType.Dimension or CssTokenType.Number)
        {
            var val = SingleTokenToValue(tokens[0]);
            if (val.Type == CssValueType.Angle || val.Type == CssValueType.Number)
            {
                angle = ResolveAngleFromValue(val);
                return true;
            }
        }

        if (tokens.Count >= 2 && tokens[0].Type == CssTokenType.Ident
            && tokens[0].Value.Equals("to", StringComparison.OrdinalIgnoreCase))
        {
            var dirs = new List<string>();
            for (int i = 1; i < tokens.Count; i++)
            {
                if (tokens[i].Type == CssTokenType.Ident)
                    dirs.Add(tokens[i].Value.ToLowerInvariant());
            }
            angle = ResolveDirectionToAngle(dirs);
            return true;
        }

        return false;
    }

    private static float ResolveAngleFromValue(CssValue val)
    {
        return val.Unit?.ToLowerInvariant() switch
        {
            "deg" => (float)val.NumericValue,
            "grad" => (float)(val.NumericValue * 360.0 / 400.0),
            "rad" => (float)(val.NumericValue * 180.0 / Math.PI),
            "turn" => (float)(val.NumericValue * 360.0),
            _ => (float)val.NumericValue,
        };
    }

    private static float ResolveDirectionToAngle(List<string> dirs)
    {
        if (dirs.Count == 1)
        {
            return dirs[0] switch
            {
                "top" => 0f,
                "right" => 90f,
                "bottom" => 180f,
                "left" => 270f,
                _ => 180f,
            };
        }
        if (dirs.Count == 2)
        {
            bool hasTop = dirs.Contains("top", StringComparer.OrdinalIgnoreCase);
            bool hasBottom = dirs.Contains("bottom", StringComparer.OrdinalIgnoreCase);
            bool hasLeft = dirs.Contains("left", StringComparer.OrdinalIgnoreCase);
            bool hasRight = dirs.Contains("right", StringComparer.OrdinalIgnoreCase);
            if (hasTop && hasRight) return 45f;
            if (hasBottom && hasRight) return 135f;
            if (hasBottom && hasLeft) return 225f;
            if (hasTop && hasLeft) return 315f;
        }
        return 180f;
    }

    private static bool TryParseRadialConfig(List<CssToken> tokens,
        out RadialGradientShape shape, out RadialGradientSize size, out float cx, out float cy)
    {
        shape = RadialGradientShape.Ellipse;
        size = RadialGradientSize.FarthestCorner;
        cx = 0.5f; cy = 0.5f;
        if (tokens.Count == 0) return false;

        int atIndex = -1;
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Type == CssTokenType.Ident && tokens[i].Value.Equals("at", StringComparison.OrdinalIgnoreCase))
            { atIndex = i; break; }
        }

        bool hasConfig = false;
        int configEnd = atIndex >= 0 ? atIndex : tokens.Count;
        for (int i = 0; i < configEnd; i++)
        {
            if (tokens[i].Type != CssTokenType.Ident) continue;
            switch (tokens[i].Value.ToLowerInvariant())
            {
                case "circle": shape = RadialGradientShape.Circle; hasConfig = true; break;
                case "ellipse": shape = RadialGradientShape.Ellipse; hasConfig = true; break;
                case "closest-side": size = RadialGradientSize.ClosestSide; hasConfig = true; break;
                case "farthest-side": size = RadialGradientSize.FarthestSide; hasConfig = true; break;
                case "closest-corner": size = RadialGradientSize.ClosestCorner; hasConfig = true; break;
                case "farthest-corner": size = RadialGradientSize.FarthestCorner; hasConfig = true; break;
            }
        }

        if (atIndex >= 0)
        {
            hasConfig = true;
            ParseGradientPosition(tokens, atIndex + 1, out cx, out cy);
        }

        if (!hasConfig && tokens.Count >= 1)
        {
            var first = tokens[0];
            if (first.Type == CssTokenType.Hash || first.Type == CssTokenType.Function
                || (first.Type == CssTokenType.Ident && Document.Color.TryFromName(first.Value, out _)))
                return false;
        }
        return hasConfig;
    }

    private static bool TryParseConicConfig(List<CssToken> tokens, out float fromAngle, out float cx, out float cy)
    {
        fromAngle = 0f; cx = 0.5f; cy = 0.5f;
        if (tokens.Count == 0) return false;

        bool hasConfig = false;
        int i = 0;

        if (i < tokens.Count && tokens[i].Type == CssTokenType.Ident
            && tokens[i].Value.Equals("from", StringComparison.OrdinalIgnoreCase))
        {
            i++;
            while (i < tokens.Count && tokens[i].Type == CssTokenType.Whitespace) i++;
            if (i < tokens.Count && tokens[i].Type is CssTokenType.Dimension or CssTokenType.Number)
            {
                fromAngle = ResolveAngleFromValue(SingleTokenToValue(tokens[i]));
                i++;
                hasConfig = true;
            }
        }

        while (i < tokens.Count && tokens[i].Type == CssTokenType.Whitespace) i++;

        if (i < tokens.Count && tokens[i].Type == CssTokenType.Ident
            && tokens[i].Value.Equals("at", StringComparison.OrdinalIgnoreCase))
        {
            i++;
            ParseGradientPosition(tokens, i, out cx, out cy);
            hasConfig = true;
        }

        if (!hasConfig && tokens.Count >= 1)
        {
            var first = tokens[0];
            if (first.Type == CssTokenType.Hash || first.Type == CssTokenType.Function
                || (first.Type == CssTokenType.Ident && Document.Color.TryFromName(first.Value, out _)))
                return false;
        }
        return hasConfig;
    }

    private static void ParseGradientPosition(List<CssToken> tokens, int start, out float x, out float y)
    {
        x = 0.5f; y = 0.5f;
        var values = new List<float>();
        for (int i = start; i < tokens.Count; i++)
        {
            if (tokens[i].Type == CssTokenType.Whitespace) continue;
            if (tokens[i].Type == CssTokenType.Ident)
            {
                values.Add(tokens[i].Value.ToLowerInvariant() switch
                {
                    "center" => 0.5f, "left" => 0f, "right" => 1f, "top" => 0f, "bottom" => 1f, _ => 0.5f,
                });
            }
            else if (tokens[i].Type == CssTokenType.Percentage)
            {
                values.Add((float)(tokens[i].NumericValue / 100.0));
            }
        }
        if (values.Count >= 1) x = values[0];
        if (values.Count >= 2) y = values[1];
    }

    private static ColorStop? ParseColorStop(List<CssToken> tokens)
    {
        var trimmed = TrimWhitespace(tokens);
        if (trimmed.Count == 0) return null;

        var colorTokens = new List<CssToken>();
        int posStart = trimmed.Count;

        if (trimmed[0].Type == CssTokenType.Function)
        {
            int depth = 1;
            colorTokens.Add(trimmed[0]);
            int idx = 1;
            while (idx < trimmed.Count && depth > 0)
            {
                colorTokens.Add(trimmed[idx]);
                if (trimmed[idx].Type == CssTokenType.Function || trimmed[idx].Type == CssTokenType.LeftParen)
                    depth++;
                else if (trimmed[idx].Type == CssTokenType.RightParen)
                    depth--;
                idx++;
            }
            while (idx < trimmed.Count && trimmed[idx].Type == CssTokenType.Whitespace) idx++;
            posStart = idx;
        }
        else
        {
            colorTokens.Add(trimmed[0]);
            int idx = 1;
            while (idx < trimmed.Count && trimmed[idx].Type == CssTokenType.Whitespace) idx++;
            posStart = idx;
        }

        var colorValue = ParseValueTokens(colorTokens);
        Document.Color color;
        if (colorValue.ColorValue.HasValue)
            color = colorValue.ColorValue.Value;
        else if (Document.Color.TryFromName(colorValue.Raw, out var named))
            color = named;
        else
            color = Document.Color.Black;

        float? position = null;
        if (posStart < trimmed.Count)
        {
            var posToken = trimmed[posStart];
            if (posToken.Type == CssTokenType.Percentage)
                position = (float)(posToken.NumericValue / 100.0);
            else if (posToken.Type is CssTokenType.Number or CssTokenType.Dimension)
                position = (float)(posToken.NumericValue / 100.0);
        }

        return new ColorStop(color, position);
    }

    private static void DistributeStopPositions(List<ColorStop> stops)
    {
        if (stops.Count == 0) return;
        if (!stops[0].Position.HasValue)
            stops[0] = new ColorStop(stops[0].Color, 0f);
        if (!stops[^1].Position.HasValue)
            stops[^1] = new ColorStop(stops[^1].Color, 1f);

        int i = 0;
        while (i < stops.Count)
        {
            if (!stops[i].Position.HasValue)
            {
                int start = i - 1;
                int end = i + 1;
                while (end < stops.Count && !stops[end].Position.HasValue) end++;
                float startPos = stops[start].Position!.Value;
                float endPos = stops[end].Position!.Value;
                int count = end - start;
                for (int j = start + 1; j < end; j++)
                {
                    float t = (float)(j - start) / count;
                    stops[j] = new ColorStop(stops[j].Color, startPos + t * (endPos - startPos));
                }
                i = end;
            }
            else { i++; }
        }
    }

    private static List<List<CssToken>> SplitByCommaRespectingDepth(List<CssToken> tokens)
    {
        var result = new List<List<CssToken>>();
        var current = new List<CssToken>();
        int depth = 0;
        foreach (var token in tokens)
        {
            if (token.Type == CssTokenType.Function || token.Type == CssTokenType.LeftParen)
            { depth++; current.Add(token); }
            else if (token.Type == CssTokenType.RightParen)
            { depth = Math.Max(0, depth - 1); current.Add(token); }
            else if (token.Type == CssTokenType.Comma && depth == 0)
            { result.Add(current); current = []; }
            else
            { current.Add(token); }
        }
        if (current.Count > 0) result.Add(current);
        return result;
    }

    #endregion

    #region Box Shadow Parsing

    /// <summary>
    /// Parses a box-shadow CSS value string into a list of descriptors.
    /// </summary>
    public static List<BoxShadowDescriptor> ParseBoxShadowValue(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        if (raw.Equals("none", StringComparison.OrdinalIgnoreCase)) return [];

        var tokenizer = new CssTokenizer(raw);
        var tokens = tokenizer.Tokenize().Where(t => t.Type != CssTokenType.EndOfFile).ToList();
        return ParseBoxShadowTokens(tokens);
    }

    internal static List<BoxShadowDescriptor> ParseBoxShadowTokens(List<CssToken> tokens)
    {
        var shadows = new List<BoxShadowDescriptor>();
        foreach (var group in SplitByCommaRespectingDepth(tokens))
        {
            var trimmed = TrimWhitespace(group);
            if (trimmed.Count == 0) continue;
            var shadow = ParseSingleBoxShadow(trimmed);
            if (shadow != null) shadows.Add(shadow);
        }
        return shadows;
    }

    private static BoxShadowDescriptor? ParseSingleBoxShadow(List<CssToken> tokens)
    {
        bool inset = false;
        var lengths = new List<float>();
        Document.Color? color = null;
        int i = 0;

        while (i < tokens.Count)
        {
            var t = tokens[i];
            if (t.Type == CssTokenType.Whitespace) { i++; continue; }

            if (t.Type == CssTokenType.Ident && t.Value.Equals("inset", StringComparison.OrdinalIgnoreCase))
            { inset = true; i++; continue; }

            if (t.Type == CssTokenType.Function && color == null)
            {
                var funcTokens = new List<CssToken> { t };
                i++;
                int depth = 1;
                while (i < tokens.Count && depth > 0)
                {
                    funcTokens.Add(tokens[i]);
                    if (tokens[i].Type == CssTokenType.Function || tokens[i].Type == CssTokenType.LeftParen) depth++;
                    else if (tokens[i].Type == CssTokenType.RightParen) depth--;
                    i++;
                }
                var colorVal = ParseValueTokens(funcTokens);
                if (colorVal.ColorValue.HasValue) color = colorVal.ColorValue.Value;
                continue;
            }

            if (t.Type == CssTokenType.Hash && color == null)
            { color = Document.Color.FromHex(t.Value); i++; continue; }

            if (t.Type == CssTokenType.Ident && color == null && Document.Color.TryFromName(t.Value, out var named))
            { color = named; i++; continue; }

            if (t.Type is CssTokenType.Number or CssTokenType.Dimension or CssTokenType.Percentage)
            {
                var val = SingleTokenToValue(t);
                lengths.Add(val.Unit?.ToLowerInvariant() switch
                {
                    "px" => (float)val.NumericValue,
                    "em" => (float)(val.NumericValue * 16),
                    "rem" => (float)(val.NumericValue * 16),
                    _ => (float)val.NumericValue,
                });
                i++; continue;
            }
            i++;
        }

        if (lengths.Count < 2) return null;
        return new BoxShadowDescriptor
        {
            OffsetX = lengths[0],
            OffsetY = lengths[1],
            BlurRadius = lengths.Count > 2 ? Math.Max(0, lengths[2]) : 0,
            SpreadRadius = lengths.Count > 3 ? lengths[3] : 0,
            Color = color ?? new Document.Color(0, 0, 0, 1),
            Inset = inset,
        };
    }

    #endregion
}
