namespace SuperRender.Document.Css;

/// <summary>
/// Parses a list of CSS tokens into Selector objects.
/// Supports tag, class, id, universal selectors, compound selectors,
/// descendant (whitespace) and child (>) combinators, and comma-separated lists.
/// </summary>
public sealed class SelectorParser
{
    private readonly IReadOnlyList<CssToken> _tokens;
    private int _pos;

    public SelectorParser(IReadOnlyList<CssToken> tokens)
    {
        _tokens = tokens;
    }

    /// <summary>
    /// Parses a comma-separated list of selectors.
    /// </summary>
    public List<Selector> ParseSelectorList()
    {
        var selectors = new List<Selector>();
        SkipWhitespace();

        while (!IsEnd())
        {
            var selector = ParseSelector();
            if (selector.Components.Count > 0)
                selectors.Add(selector);

            SkipWhitespace();
            if (!IsEnd() && Current().Type == CssTokenType.Comma)
            {
                _pos++; // skip comma
                SkipWhitespace();
            }
        }

        return selectors;
    }

    private Selector ParseSelector()
    {
        var components = new List<SelectorComponent>();

        while (!IsEnd() && Current().Type != CssTokenType.Comma)
        {
            var simple = ParseCompoundSelector();
            if (simple == null) break;

            // Determine combinator to the next component
            var combinator = Combinator.None;

            if (!IsEnd() && Current().Type != CssTokenType.Comma)
            {
                // Check for whitespace or explicit combinator
                bool hadWhitespace = false;
                if (!IsEnd() && Current().Type == CssTokenType.Whitespace)
                {
                    hadWhitespace = true;
                    SkipWhitespace();
                }

                if (!IsEnd() && Current().Type == CssTokenType.Delim && Current().Value == ">")
                {
                    combinator = Combinator.Child;
                    _pos++; // skip >
                    SkipWhitespace();
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

        // The last component should have Combinator.None (already the case since we only
        // set a combinator when there's a following component, but let's ensure it)
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

        return new Selector { Components = components };
    }

    /// <summary>
    /// Parses a compound selector (e.g. div.foo#bar) — consecutive simple selectors
    /// without whitespace between them.
    /// </summary>
    private SimpleSelector? ParseCompoundSelector()
    {
        if (IsEnd()) return null;

        string? tagName = null;
        string? id = null;
        var classes = new List<string>();

        bool matched = false;

        while (!IsEnd())
        {
            var tok = Current();

            if (tok.Type == CssTokenType.Ident)
            {
                // Tag name
                tagName = tok.Value;
                _pos++;
                matched = true;
            }
            else if (tok.Type == CssTokenType.Delim && tok.Value == "*")
            {
                // Universal selector — no tag constraint
                _pos++;
                matched = true;
            }
            else if (tok.Type == CssTokenType.Hash)
            {
                // ID selector
                id = tok.Value;
                _pos++;
                matched = true;
            }
            else if (tok.Type == CssTokenType.Dot)
            {
                // Class selector: . followed by Ident
                _pos++; // skip dot
                if (!IsEnd() && Current().Type == CssTokenType.Ident)
                {
                    classes.Add(Current().Value);
                    _pos++;
                }
                matched = true;
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
            Classes = classes
        };
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
