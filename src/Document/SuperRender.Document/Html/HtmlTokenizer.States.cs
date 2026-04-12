namespace SuperRender.Document.Html;

public sealed partial class HtmlTokenizer
{
    // ═══════════════════════════════════════════════════════════════════════
    //  DATA
    // ═══════════════════════════════════════════════════════════════════════

    private State HandleDataState(List<HtmlToken> tokens)
    {
        if (Eof)
        {
            if (_textBuffer.Length > 0)
            {
                tokens.Add(MakeTextToken(_textBuffer));
                _textBuffer.Clear();
            }
            tokens.Add(new HtmlToken { Type = HtmlTokenType.EndOfFile });
            return State.Data;
        }

        var ch = Current;
        if (ch == '<')
        {
            if (_textBuffer.Length > 0)
            {
                tokens.Add(MakeTextToken(_textBuffer));
                _textBuffer.Clear();
            }
            _pos++;
            return State.TagOpen;
        }
        else
        {
            _textBuffer.Append(Consume());
            return State.Data;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TAG OPEN — we just saw '<'
    // ═══════════════════════════════════════════════════════════════════════

    private State HandleTagOpenState(List<HtmlToken> tokens)
    {
        if (Eof)
        {
            _textBuffer.Append('<');
            return State.Data;
        }

        var ch = Current;

        if (ch == '/')
        {
            _pos++;
            return State.EndTagOpen;
        }
        else if (ch == '!')
        {
            _pos++;
            return State.MarkupDeclarationOpen;
        }
        else if (char.IsLetter(ch))
        {
            _isEndTag = false;
            _selfClosing = false;
            _tagNameBuffer.Clear();
            _attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return State.TagName;
        }
        else
        {
            // Not a tag — emit '<' as text
            _textBuffer.Append('<');
            return State.Data;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  END TAG OPEN — we saw '</'
    // ═══════════════════════════════════════════════════════════════════════

    private State HandleEndTagOpenState(List<HtmlToken> tokens)
    {
        if (Eof)
        {
            _textBuffer.Append("</");
            return State.Data;
        }

        if (char.IsLetter(Current))
        {
            _isEndTag = true;
            _selfClosing = false;
            _tagNameBuffer.Clear();
            _attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return State.TagName;
        }
        else
        {
            // bogus — treat as comment-like
            _commentBuffer.Clear();
            return State.BogusComment;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TAG NAME
    // ═══════════════════════════════════════════════════════════════════════

    private State HandleTagNameState(List<HtmlToken> tokens)
    {
        if (Eof)
        {
            // Unclosed tag — discard
            return State.Data;
        }

        var ch = Current;
        if (char.IsWhiteSpace(ch))
        {
            _pos++;
            return State.BeforeAttributeName;
        }
        else if (ch == '/')
        {
            _pos++;
            return State.SelfClosingStartTag;
        }
        else if (ch == '>')
        {
            _pos++;
            tokens.Add(EmitTag(_tagNameBuffer, _isEndTag, false, _attributes));
            return State.Data;
        }
        else
        {
            _tagNameBuffer.Append(char.ToLowerInvariant(Consume()));
            return State.TagName;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  BEFORE ATTRIBUTE NAME
    // ═══════════════════════════════════════════════════════════════════════

    private State HandleBeforeAttributeNameState(List<HtmlToken> tokens)
    {
        if (Eof)
        {
            return State.Data;
        }

        var ch = Current;
        if (char.IsWhiteSpace(ch))
        {
            _pos++;
            return State.BeforeAttributeName;
        }
        else if (ch == '/')
        {
            _pos++;
            return State.SelfClosingStartTag;
        }
        else if (ch == '>')
        {
            _pos++;
            tokens.Add(EmitTag(_tagNameBuffer, _isEndTag, _selfClosing, _attributes));
            return State.Data;
        }
        else
        {
            _attrNameBuffer.Clear();
            _attrValueBuffer.Clear();
            return State.AttributeName;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ATTRIBUTE NAME
    // ═══════════════════════════════════════════════════════════════════════

    private State HandleAttributeNameState(List<HtmlToken> tokens)
    {
        if (Eof)
        {
            StoreAttribute(_attributes, _attrNameBuffer, _attrValueBuffer, valueSet: false);
            return State.Data;
        }

        var ch = Current;
        if (ch == '=')
        {
            _pos++;
            return State.BeforeAttributeValue;
        }
        else if (char.IsWhiteSpace(ch))
        {
            _pos++;
            return State.AfterAttributeName;
        }
        else if (ch == '/' || ch == '>')
        {
            StoreAttribute(_attributes, _attrNameBuffer, _attrValueBuffer, valueSet: false);
            return State.BeforeAttributeName;
        }
        else
        {
            _attrNameBuffer.Append(char.ToLowerInvariant(Consume()));
            return State.AttributeName;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  AFTER ATTRIBUTE NAME (whitespace between name and '=')
    // ═══════════════════════════════════════════════════════════════════════

    private State HandleAfterAttributeNameState(List<HtmlToken> tokens)
    {
        if (Eof)
        {
            StoreAttribute(_attributes, _attrNameBuffer, _attrValueBuffer, valueSet: false);
            return State.Data;
        }

        var ch = Current;
        if (char.IsWhiteSpace(ch))
        {
            _pos++;
            return State.AfterAttributeName;
        }
        else if (ch == '=')
        {
            _pos++;
            return State.BeforeAttributeValue;
        }
        else if (ch == '/' || ch == '>')
        {
            StoreAttribute(_attributes, _attrNameBuffer, _attrValueBuffer, valueSet: false);
            return State.BeforeAttributeName;
        }
        else
        {
            // value-less attribute followed by next attribute
            StoreAttribute(_attributes, _attrNameBuffer, _attrValueBuffer, valueSet: false);
            _attrNameBuffer.Clear();
            _attrValueBuffer.Clear();
            return State.AttributeName;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  BEFORE ATTRIBUTE VALUE
    // ═══════════════════════════════════════════════════════════════════════

    private State HandleBeforeAttributeValueState(List<HtmlToken> tokens)
    {
        if (Eof)
        {
            StoreAttribute(_attributes, _attrNameBuffer, _attrValueBuffer, valueSet: true);
            return State.Data;
        }

        var ch = Current;
        if (char.IsWhiteSpace(ch))
        {
            _pos++;
            return State.BeforeAttributeValue;
        }
        else if (ch == '"' || ch == '\'')
        {
            _attrQuote = Consume();
            _attrValueBuffer.Clear();
            return State.AttributeValueQuoted;
        }
        else if (ch == '>')
        {
            StoreAttribute(_attributes, _attrNameBuffer, _attrValueBuffer, valueSet: true);
            _pos++;
            tokens.Add(EmitTag(_tagNameBuffer, _isEndTag, _selfClosing, _attributes));
            return State.Data;
        }
        else
        {
            _attrValueBuffer.Clear();
            return State.AttributeValueUnquoted;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ATTRIBUTE VALUE (QUOTED)
    // ═══════════════════════════════════════════════════════════════════════

    private State HandleAttributeValueQuotedState(List<HtmlToken> tokens)
    {
        if (Eof)
        {
            StoreAttribute(_attributes, _attrNameBuffer, _attrValueBuffer, valueSet: true);
            return State.Data;
        }

        var ch = Current;
        if (ch == _attrQuote)
        {
            _pos++;
            StoreAttribute(_attributes, _attrNameBuffer, _attrValueBuffer, valueSet: true);
            return State.BeforeAttributeName;
        }
        else
        {
            _attrValueBuffer.Append(Consume());
            return State.AttributeValueQuoted;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ATTRIBUTE VALUE (UNQUOTED)
    // ═══════════════════════════════════════════════════════════════════════

    private State HandleAttributeValueUnquotedState(List<HtmlToken> tokens)
    {
        if (Eof)
        {
            StoreAttribute(_attributes, _attrNameBuffer, _attrValueBuffer, valueSet: true);
            return State.Data;
        }

        var ch = Current;
        if (char.IsWhiteSpace(ch))
        {
            _pos++;
            StoreAttribute(_attributes, _attrNameBuffer, _attrValueBuffer, valueSet: true);
            return State.BeforeAttributeName;
        }
        else if (ch == '>')
        {
            StoreAttribute(_attributes, _attrNameBuffer, _attrValueBuffer, valueSet: true);
            _pos++;
            tokens.Add(EmitTag(_tagNameBuffer, _isEndTag, _selfClosing, _attributes));
            return State.Data;
        }
        else if (ch == '/')
        {
            StoreAttribute(_attributes, _attrNameBuffer, _attrValueBuffer, valueSet: true);
            _pos++;
            return State.SelfClosingStartTag;
        }
        else
        {
            _attrValueBuffer.Append(Consume());
            return State.AttributeValueUnquoted;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SELF-CLOSING START TAG — we saw '/'
    // ═══════════════════════════════════════════════════════════════════════

    private State HandleSelfClosingStartTagState(List<HtmlToken> tokens)
    {
        if (Eof)
        {
            return State.Data;
        }

        if (Current == '>')
        {
            _pos++;
            _selfClosing = true;
            tokens.Add(EmitTag(_tagNameBuffer, _isEndTag, true, _attributes));
            return State.Data;
        }
        else
        {
            // '/' not followed by '>' — treat as before-attribute-name
            return State.BeforeAttributeName;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  MARKUP DECLARATION OPEN — we saw '<!'
    // ═══════════════════════════════════════════════════════════════════════

    private State HandleMarkupDeclarationOpenState(List<HtmlToken> tokens)
    {
        if (Eof)
        {
            return State.Data;
        }

        // Check for '<!--'
        if (_pos + 1 < _input.Length && _input[_pos] == '-' && _input[_pos + 1] == '-')
        {
            _pos += 2;
            _commentBuffer.Clear();
            return State.Comment;
        }
        // Check for DOCTYPE (case-insensitive)
        else if (MatchesAt("doctype", _pos))
        {
            // Consume until '>'
            while (!Eof && Current != '>')
                _pos++;
            if (!Eof)
                _pos++; // consume '>'
            tokens.Add(new HtmlToken { Type = HtmlTokenType.Doctype, TagName = "doctype" });
            return State.Data;
        }
        else
        {
            // Bogus
            _commentBuffer.Clear();
            return State.BogusComment;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  COMMENT — inside <!-- ... -->
    // ═══════════════════════════════════════════════════════════════════════

    private State HandleCommentState(List<HtmlToken> tokens)
    {
        if (Eof)
        {
            tokens.Add(new HtmlToken { Type = HtmlTokenType.Comment, Text = _commentBuffer.ToString() });
            return State.Data;
        }

        var ch = Current;
        if (ch == '-')
        {
            _pos++;
            return State.CommentDash;
        }
        else
        {
            _commentBuffer.Append(Consume());
            return State.Comment;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  COMMENT DASH — we saw '-' inside a comment
    // ═══════════════════════════════════════════════════════════════════════

    private State HandleCommentDashState(List<HtmlToken> tokens)
    {
        if (Eof)
        {
            _commentBuffer.Append('-');
            tokens.Add(new HtmlToken { Type = HtmlTokenType.Comment, Text = _commentBuffer.ToString() });
            return State.Data;
        }

        if (Current == '-')
        {
            _pos++;
            return State.CommentEnd;
        }
        else
        {
            _commentBuffer.Append('-');
            return State.Comment;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  COMMENT END — we saw '--' inside a comment
    // ═══════════════════════════════════════════════════════════════════════

    private State HandleCommentEndState(List<HtmlToken> tokens)
    {
        if (Eof)
        {
            tokens.Add(new HtmlToken { Type = HtmlTokenType.Comment, Text = _commentBuffer.ToString() });
            return State.Data;
        }

        if (Current == '>')
        {
            _pos++;
            tokens.Add(new HtmlToken { Type = HtmlTokenType.Comment, Text = _commentBuffer.ToString() });
            return State.Data;
        }
        else if (Current == '-')
        {
            // extra dash
            _commentBuffer.Append('-');
            _pos++;
            // stay in CommentEnd
            return State.CommentEnd;
        }
        else
        {
            // false alarm — '--' was not followed by '>'
            _commentBuffer.Append("--");
            return State.Comment;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  BOGUS COMMENT
    // ═══════════════════════════════════════════════════════════════════════

    private State HandleBogusCommentState(List<HtmlToken> tokens)
    {
        if (Eof)
        {
            tokens.Add(new HtmlToken { Type = HtmlTokenType.Comment, Text = _commentBuffer.ToString() });
            return State.Data;
        }

        if (Current == '>')
        {
            _pos++;
            tokens.Add(new HtmlToken { Type = HtmlTokenType.Comment, Text = _commentBuffer.ToString() });
            return State.Data;
        }
        else
        {
            _commentBuffer.Append(Consume());
            return State.BogusComment;
        }
    }
}
