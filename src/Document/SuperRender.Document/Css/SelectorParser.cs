namespace SuperRender.Document.Css;

/// <summary>
/// Parses a list of CSS tokens into Selector objects.
/// Supports: tag, class, id, universal, compound selectors,
/// descendant/child/adjacent-sibling/general-sibling combinators,
/// attribute selectors, pseudo-classes, pseudo-elements, and comma-separated lists.
/// </summary>
public sealed class SelectorParser
{
    private readonly IReadOnlyList<CssToken> _tokens;
    private int _pos;

    public SelectorParser(IReadOnlyList<CssToken> tokens)
    {
        _tokens = tokens;
    }

    public List<Selector> ParseSelectorList()
    {
        var selectors = new List<Selector>();
        SkipWhitespace();

        while (!IsEnd())
        {
            int posBefore = _pos;
            var selector = ParseSelector();
            if (selector.Components.Count > 0)
                selectors.Add(selector);

            // Safety: if no tokens were consumed, skip one to prevent infinite loop
            if (_pos == posBefore && !IsEnd())
                _pos++;

            SkipWhitespace();
            if (!IsEnd() && Current().Type == CssTokenType.Comma)
            {
                _pos++;
                SkipWhitespace();
            }
        }

        return selectors;
    }

    private Selector ParseSelector()
    {
        var components = new List<SelectorComponent>();
        PseudoElementType? pseudoElement = null;

        while (!IsEnd() && Current().Type != CssTokenType.Comma)
        {
            var simple = ParseCompoundSelector(out var pe);
            if (simple == null) break;

            if (pe != null) pseudoElement = pe;

            var combinator = Combinator.None;

            if (!IsEnd() && Current().Type != CssTokenType.Comma)
            {
                bool hadWhitespace = false;
                if (!IsEnd() && Current().Type == CssTokenType.Whitespace)
                {
                    hadWhitespace = true;
                    SkipWhitespace();
                }

                if (!IsEnd() && Current().Type == CssTokenType.Delim)
                {
                    var val = Current().Value;
                    if (val == ">")
                    {
                        combinator = Combinator.Child;
                        _pos++;
                        SkipWhitespace();
                    }
                    else if (val == "+")
                    {
                        combinator = Combinator.AdjacentSibling;
                        _pos++;
                        SkipWhitespace();
                    }
                    else if (val == "~")
                    {
                        combinator = Combinator.GeneralSibling;
                        _pos++;
                        SkipWhitespace();
                    }
                    else if (hadWhitespace && !IsEnd() && Current().Type != CssTokenType.Comma)
                    {
                        combinator = Combinator.Descendant;
                    }
                }
                else if (hadWhitespace && !IsEnd() && Current().Type != CssTokenType.Comma)
                {
                    combinator = Combinator.Descendant;
                }
            }

            components.Add(new SelectorComponent
            {
                Simple = simple,
                Combinator = combinator
            });
        }

        if (components.Count > 0)
        {
            var last = components[^1];
            if (last.Combinator != Combinator.None)
            {
                components[^1] = new SelectorComponent
                {
                    Simple = last.Simple,
                    Combinator = Combinator.None
                };
            }
        }

        return new Selector { Components = components, PseudoElement = pseudoElement };
    }

    private SimpleSelector? ParseCompoundSelector(out PseudoElementType? pseudoElement)
    {
        pseudoElement = null;
        if (IsEnd()) return null;

        string? tagName = null;
        string? id = null;
        var classes = new List<string>();
        var attributes = new List<AttributeSelector>();
        var pseudoClasses = new List<PseudoClass>();
        bool matched = false;

        while (!IsEnd())
        {
            var tok = Current();

            if (tok.Type == CssTokenType.Ident && !matched && tagName == null)
            {
                tagName = tok.Value;
                _pos++;
                matched = true;
            }
            else if (tok.Type == CssTokenType.Delim && tok.Value == "*" && !matched)
            {
                _pos++;
                matched = true;
            }
            else if (tok.Type == CssTokenType.Hash)
            {
                id = tok.Value;
                _pos++;
                matched = true;
            }
            else if (tok.Type == CssTokenType.Dot)
            {
                _pos++;
                if (!IsEnd() && Current().Type == CssTokenType.Ident)
                {
                    classes.Add(Current().Value);
                    _pos++;
                }
                matched = true;
            }
            else if (tok.Type == CssTokenType.Delim && tok.Value == "[")
            {
                var attr = ParseAttributeSelector();
                if (attr != null) attributes.Add(attr);
                matched = true;
            }
            else if (tok.Type == CssTokenType.Colon)
            {
                _pos++;
                if (!IsEnd() && Current().Type == CssTokenType.Colon)
                {
                    // Pseudo-element (::before, ::after, ::first-line, etc.)
                    _pos++;
                    if (!IsEnd() && Current().Type == CssTokenType.Ident)
                    {
                        pseudoElement = Current().Value.ToLowerInvariant() switch
                        {
                            "before" => PseudoElementType.Before,
                            "after" => PseudoElementType.After,
                            "first-line" => PseudoElementType.FirstLine,
                            "first-letter" => PseudoElementType.FirstLetter,
                            "marker" => PseudoElementType.Marker,
                            "placeholder" => PseudoElementType.Placeholder,
                            "selection" => PseudoElementType.Selection,
                            "backdrop" => PseudoElementType.Backdrop,
                            "file-selector-button" => PseudoElementType.FileSelectorButton,
                            _ => null
                        };
                        _pos++;
                    }
                    matched = true;
                }
                else
                {
                    // Pseudo-class
                    var pc = ParsePseudoClass();
                    if (pc != null) pseudoClasses.Add(pc);
                    matched = true;
                }
            }
            else
            {
                break;
            }
        }

        if (!matched) return null;

        return new SimpleSelector
        {
            TagName = tagName,
            Id = id,
            Classes = classes,
            Attributes = attributes,
            PseudoClasses = pseudoClasses
        };
    }

    private AttributeSelector? ParseAttributeSelector()
    {
        // Current position is after '['
        _pos++; // skip [
        SkipWhitespace();

        if (IsEnd()) return null;

        string attrName;
        if (Current().Type == CssTokenType.Ident)
        {
            attrName = Current().Value;
            _pos++;
        }
        else return null;

        SkipWhitespace();

        // Check for ] (existence check) or operator
        if (!IsEnd() && Current().Type == CssTokenType.Delim && Current().Value == "]")
        {
            _pos++; // skip ]
            return new AttributeSelector { Name = attrName, Op = AttributeOp.Exists };
        }

        // Parse operator
        var op = AttributeOp.Equals;
        if (!IsEnd())
        {
            var opTok = Current();
            if (opTok.Type == CssTokenType.Delim)
            {
                switch (opTok.Value)
                {
                    case "=":
                        op = AttributeOp.Equals;
                        _pos++;
                        break;
                    case "~":
                        _pos++;
                        if (!IsEnd() && Current().Type == CssTokenType.Delim && Current().Value == "=")
                        { op = AttributeOp.WordMatch; _pos++; }
                        break;
                    case "|":
                        _pos++;
                        if (!IsEnd() && Current().Type == CssTokenType.Delim && Current().Value == "=")
                        { op = AttributeOp.DashMatch; _pos++; }
                        break;
                    case "^":
                        _pos++;
                        if (!IsEnd() && Current().Type == CssTokenType.Delim && Current().Value == "=")
                        { op = AttributeOp.StartsWith; _pos++; }
                        break;
                    case "$":
                        _pos++;
                        if (!IsEnd() && Current().Type == CssTokenType.Delim && Current().Value == "=")
                        { op = AttributeOp.EndsWith; _pos++; }
                        break;
                    case "*":
                        _pos++;
                        if (!IsEnd() && Current().Type == CssTokenType.Delim && Current().Value == "=")
                        { op = AttributeOp.Contains; _pos++; }
                        break;
                }
            }
        }

        SkipWhitespace();

        // Parse value
        string? value = null;
        if (!IsEnd())
        {
            if (Current().Type == CssTokenType.StringLiteral)
            {
                value = Current().Value;
                _pos++;
            }
            else if (Current().Type == CssTokenType.Ident)
            {
                value = Current().Value;
                _pos++;
            }
        }

        SkipWhitespace();

        // Check for case-sensitivity flag before closing ]
        var caseSensitivity = AttributeCaseSensitivity.Default;
        if (!IsEnd() && Current().Type == CssTokenType.Ident)
        {
            var flag = Current().Value.ToLowerInvariant();
            if (flag == "i")
            {
                caseSensitivity = AttributeCaseSensitivity.CaseInsensitive;
                _pos++;
                SkipWhitespace();
            }
            else if (flag == "s")
            {
                caseSensitivity = AttributeCaseSensitivity.CaseSensitive;
                _pos++;
                SkipWhitespace();
            }
        }

        // Skip closing ]
        if (!IsEnd() && Current().Type == CssTokenType.Delim && Current().Value == "]")
            _pos++;

        return new AttributeSelector { Name = attrName, Op = op, Value = value, CaseSensitivity = caseSensitivity };
    }

    private PseudoClass? ParsePseudoClass()
    {
        if (IsEnd()) return null;

        // Check for function-style pseudo-class (e.g., :nth-child(...), :not(...))
        if (Current().Type == CssTokenType.Function)
        {
            var funcName = Current().Value.ToLowerInvariant();
            _pos++; // skip function token

            return funcName switch
            {
                "nth-child" => ParseNthPseudoClass(PseudoClassType.NthChild),
                "nth-last-child" => ParseNthPseudoClass(PseudoClassType.NthLastChild),
                "nth-of-type" => ParseNthPseudoClass(PseudoClassType.NthOfType),
                "nth-last-of-type" => ParseNthPseudoClass(PseudoClassType.NthLastOfType),
                "not" => ParseSelectorFunctionPseudoClass(PseudoClassType.Not),
                "is" => ParseSelectorFunctionPseudoClass(PseudoClassType.Is),
                "where" => ParseSelectorFunctionPseudoClass(PseudoClassType.Where),
                "has" => ParseSelectorFunctionPseudoClass(PseudoClassType.Has),
                "lang" => ParseStringArgPseudoClass(PseudoClassType.Lang),
                "dir" => ParseStringArgPseudoClass(PseudoClassType.Dir),
                _ => SkipToCloseParen()
            };
        }

        if (Current().Type != CssTokenType.Ident) return null;

        var name = Current().Value.ToLowerInvariant();
        _pos++;

        return name switch
        {
            "first-child" => new PseudoClass { Type = PseudoClassType.FirstChild },
            "last-child" => new PseudoClass { Type = PseudoClassType.LastChild },
            "only-child" => new PseudoClass { Type = PseudoClassType.OnlyChild },
            "first-of-type" => new PseudoClass { Type = PseudoClassType.FirstOfType },
            "last-of-type" => new PseudoClass { Type = PseudoClassType.LastOfType },
            "only-of-type" => new PseudoClass { Type = PseudoClassType.OnlyOfType },
            "root" => new PseudoClass { Type = PseudoClassType.Root },
            "empty" => new PseudoClass { Type = PseudoClassType.Empty },
            "link" => new PseudoClass { Type = PseudoClassType.Link },
            "visited" => new PseudoClass { Type = PseudoClassType.Visited },
            "hover" => new PseudoClass { Type = PseudoClassType.Hover },
            "focus" => new PseudoClass { Type = PseudoClassType.Focus },
            "active" => new PseudoClass { Type = PseudoClassType.Active },
            "focus-within" => new PseudoClass { Type = PseudoClassType.FocusWithin },
            "focus-visible" => new PseudoClass { Type = PseudoClassType.FocusVisible },
            "any-link" => new PseudoClass { Type = PseudoClassType.AnyLink },
            "target" => new PseudoClass { Type = PseudoClassType.Target },
            "enabled" => new PseudoClass { Type = PseudoClassType.Enabled },
            "disabled" => new PseudoClass { Type = PseudoClassType.Disabled },
            "checked" => new PseudoClass { Type = PseudoClassType.Checked },
            "indeterminate" => new PseudoClass { Type = PseudoClassType.Indeterminate },
            "required" => new PseudoClass { Type = PseudoClassType.Required },
            "optional" => new PseudoClass { Type = PseudoClassType.Optional },
            "valid" => new PseudoClass { Type = PseudoClassType.Valid },
            "invalid" => new PseudoClass { Type = PseudoClassType.Invalid },
            "in-range" => new PseudoClass { Type = PseudoClassType.InRange },
            "out-of-range" => new PseudoClass { Type = PseudoClassType.OutOfRange },
            "read-only" => new PseudoClass { Type = PseudoClassType.ReadOnly },
            "read-write" => new PseudoClass { Type = PseudoClassType.ReadWrite },
            "placeholder-shown" => new PseudoClass { Type = PseudoClassType.PlaceholderShown },
            "default" => new PseudoClass { Type = PseudoClassType.Default },
            "defined" => new PseudoClass { Type = PseudoClassType.Defined },
            "scope" => new PseudoClass { Type = PseudoClassType.Scope },
            _ => null
        };
    }

    private PseudoClass ParseStringArgPseudoClass(PseudoClassType type)
    {
        var argStr = CollectFunctionArgument();
        return new PseudoClass { Type = type, Argument = argStr };
    }

    private PseudoClass ParseNthPseudoClass(PseudoClassType type)
    {
        // Collect argument tokens until ')'
        var argStr = CollectFunctionArgument();
        return new PseudoClass { Type = type, Argument = argStr };
    }

    private PseudoClass ParseSelectorFunctionPseudoClass(PseudoClassType type)
    {
        // Collect tokens until matching ')' and parse as selector list
        var argTokens = CollectFunctionArgumentTokens();
        var subParser = new SelectorParser(argTokens);
        var selectors = subParser.ParseSelectorList();
        return new PseudoClass { Type = type, SelectorArgument = selectors };
    }

    private string CollectFunctionArgument()
    {
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
            if (Current().Type != CssTokenType.Whitespace)
                parts.Add(Current().Value);
            _pos++;
        }
        return string.Join("", parts);
    }

    private List<CssToken> CollectFunctionArgumentTokens()
    {
        var tokens = new List<CssToken>();
        int depth = 1;
        while (!IsEnd() && depth > 0)
        {
            if (Current().Type == CssTokenType.LeftParen) depth++;
            else if (Current().Type == CssTokenType.RightParen)
            {
                depth--;
                if (depth == 0) { _pos++; break; }
            }
            tokens.Add(Current());
            _pos++;
        }
        return tokens;
    }

    private PseudoClass? SkipToCloseParen()
    {
        int depth = 1;
        while (!IsEnd() && depth > 0)
        {
            if (Current().Type == CssTokenType.LeftParen) depth++;
            else if (Current().Type == CssTokenType.RightParen) depth--;
            _pos++;
        }
        return null;
    }

    private CssToken Current() => _tokens[_pos];

    private bool IsEnd()
        => _pos >= _tokens.Count
           || _tokens[_pos].Type == CssTokenType.EndOfFile;

    private void SkipWhitespace()
    {
        while (!IsEnd() && Current().Type == CssTokenType.Whitespace)
            _pos++;
    }
}
