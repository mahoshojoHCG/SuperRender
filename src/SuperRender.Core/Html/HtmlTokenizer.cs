using System.Text;

namespace SuperRender.Core.Html;

/// <summary>
/// A state-machine HTML tokenizer that converts raw HTML text into a sequence of <see cref="HtmlToken"/> values.
/// </summary>
public sealed class HtmlTokenizer
{
    private enum State
    {
        Data,
        TagOpen,
        EndTagOpen,
        TagName,
        BeforeAttributeName,
        AttributeName,
        AfterAttributeName,
        BeforeAttributeValue,
        AttributeValueQuoted,
        AttributeValueUnquoted,
        SelfClosingStartTag,
        BogusComment,
        MarkupDeclarationOpen,
        Comment,
        CommentDash,
        CommentEnd,
    }

    private readonly string _input;
    private int _pos;

    public HtmlTokenizer(string input)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private bool Eof => _pos >= _input.Length;
    private char Current => _input[_pos];

    private char Consume()
    {
        return _input[_pos++];
    }

    private bool TryPeek(out char c)
    {
        if (_pos < _input.Length) { c = _input[_pos]; return true; }
        c = default;
        return false;
    }

    private bool MatchesAt(string s, int offset)
    {
        if (offset + s.Length > _input.Length)
            return false;
        for (var i = 0; i < s.Length; i++)
        {
            if (char.ToLowerInvariant(_input[offset + i]) != char.ToLowerInvariant(s[i]))
                return false;
        }
        return true;
    }

    // ── main tokenize loop ───────────────────────────────────────────────────

    public IEnumerable<HtmlToken> Tokenize()
    {
        var state = State.Data;
        var textBuffer = new StringBuilder();
        var tagNameBuffer = new StringBuilder();
        var attrNameBuffer = new StringBuilder();
        var attrValueBuffer = new StringBuilder();
        var commentBuffer = new StringBuilder();
        char attrQuote = '"';
        bool isEndTag = false;
        bool selfClosing = false;
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            switch (state)
            {
                // ═══════════════════════════════════════════════════════════
                //  DATA
                // ═══════════════════════════════════════════════════════════
                case State.Data:
                {
                    if (Eof)
                    {
                        if (textBuffer.Length > 0)
                        {
                            yield return MakeTextToken(textBuffer);
                            textBuffer.Clear();
                        }
                        yield return new HtmlToken { Type = HtmlTokenType.EndOfFile };
                        yield break;
                    }

                    var ch = Current;
                    if (ch == '<')
                    {
                        if (textBuffer.Length > 0)
                        {
                            yield return MakeTextToken(textBuffer);
                            textBuffer.Clear();
                        }
                        _pos++;
                        state = State.TagOpen;
                    }
                    else
                    {
                        textBuffer.Append(Consume());
                    }
                    break;
                }

                // ═══════════════════════════════════════════════════════════
                //  TAG OPEN — we just saw '<'
                // ═══════════════════════════════════════════════════════════
                case State.TagOpen:
                {
                    if (Eof)
                    {
                        textBuffer.Append('<');
                        state = State.Data;
                        break;
                    }

                    var ch = Current;

                    if (ch == '/')
                    {
                        _pos++;
                        state = State.EndTagOpen;
                    }
                    else if (ch == '!')
                    {
                        _pos++;
                        state = State.MarkupDeclarationOpen;
                    }
                    else if (char.IsLetter(ch))
                    {
                        isEndTag = false;
                        selfClosing = false;
                        tagNameBuffer.Clear();
                        attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        state = State.TagName;
                    }
                    else
                    {
                        // Not a tag — emit '<' as text
                        textBuffer.Append('<');
                        state = State.Data;
                    }
                    break;
                }

                // ═══════════════════════════════════════════════════════════
                //  END TAG OPEN — we saw '</'
                // ═══════════════════════════════════════════════════════════
                case State.EndTagOpen:
                {
                    if (Eof)
                    {
                        textBuffer.Append("</");
                        state = State.Data;
                        break;
                    }

                    if (char.IsLetter(Current))
                    {
                        isEndTag = true;
                        selfClosing = false;
                        tagNameBuffer.Clear();
                        attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        state = State.TagName;
                    }
                    else
                    {
                        // bogus — treat as comment-like
                        commentBuffer.Clear();
                        state = State.BogusComment;
                    }
                    break;
                }

                // ═══════════════════════════════════════════════════════════
                //  TAG NAME
                // ═══════════════════════════════════════════════════════════
                case State.TagName:
                {
                    if (Eof)
                    {
                        // Unclosed tag — discard
                        state = State.Data;
                        break;
                    }

                    var ch = Current;
                    if (char.IsWhiteSpace(ch))
                    {
                        _pos++;
                        state = State.BeforeAttributeName;
                    }
                    else if (ch == '/')
                    {
                        _pos++;
                        state = State.SelfClosingStartTag;
                    }
                    else if (ch == '>')
                    {
                        _pos++;
                        yield return EmitTag(tagNameBuffer, isEndTag, false, attributes);
                        state = State.Data;
                    }
                    else
                    {
                        tagNameBuffer.Append(char.ToLowerInvariant(Consume()));
                    }
                    break;
                }

                // ═══════════════════════════════════════════════════════════
                //  BEFORE ATTRIBUTE NAME
                // ═══════════════════════════════════════════════════════════
                case State.BeforeAttributeName:
                {
                    if (Eof)
                    {
                        state = State.Data;
                        break;
                    }

                    var ch = Current;
                    if (char.IsWhiteSpace(ch))
                    {
                        _pos++;
                    }
                    else if (ch == '/')
                    {
                        _pos++;
                        state = State.SelfClosingStartTag;
                    }
                    else if (ch == '>')
                    {
                        _pos++;
                        yield return EmitTag(tagNameBuffer, isEndTag, selfClosing, attributes);
                        state = State.Data;
                    }
                    else
                    {
                        attrNameBuffer.Clear();
                        attrValueBuffer.Clear();
                        state = State.AttributeName;
                    }
                    break;
                }

                // ═══════════════════════════════════════════════════════════
                //  ATTRIBUTE NAME
                // ═══════════════════════════════════════════════════════════
                case State.AttributeName:
                {
                    if (Eof)
                    {
                        StoreAttribute(attributes, attrNameBuffer, attrValueBuffer, valueSet: false);
                        state = State.Data;
                        break;
                    }

                    var ch = Current;
                    if (ch == '=')
                    {
                        _pos++;
                        state = State.BeforeAttributeValue;
                    }
                    else if (char.IsWhiteSpace(ch))
                    {
                        _pos++;
                        state = State.AfterAttributeName;
                    }
                    else if (ch == '/' || ch == '>')
                    {
                        StoreAttribute(attributes, attrNameBuffer, attrValueBuffer, valueSet: false);
                        state = State.BeforeAttributeName;
                    }
                    else
                    {
                        attrNameBuffer.Append(char.ToLowerInvariant(Consume()));
                    }
                    break;
                }

                // ═══════════════════════════════════════════════════════════
                //  AFTER ATTRIBUTE NAME (whitespace between name and '=')
                // ═══════════════════════════════════════════════════════════
                case State.AfterAttributeName:
                {
                    if (Eof)
                    {
                        StoreAttribute(attributes, attrNameBuffer, attrValueBuffer, valueSet: false);
                        state = State.Data;
                        break;
                    }

                    var ch = Current;
                    if (char.IsWhiteSpace(ch))
                    {
                        _pos++;
                    }
                    else if (ch == '=')
                    {
                        _pos++;
                        state = State.BeforeAttributeValue;
                    }
                    else if (ch == '/' || ch == '>')
                    {
                        StoreAttribute(attributes, attrNameBuffer, attrValueBuffer, valueSet: false);
                        state = State.BeforeAttributeName;
                    }
                    else
                    {
                        // value-less attribute followed by next attribute
                        StoreAttribute(attributes, attrNameBuffer, attrValueBuffer, valueSet: false);
                        attrNameBuffer.Clear();
                        attrValueBuffer.Clear();
                        state = State.AttributeName;
                    }
                    break;
                }

                // ═══════════════════════════════════════════════════════════
                //  BEFORE ATTRIBUTE VALUE
                // ═══════════════════════════════════════════════════════════
                case State.BeforeAttributeValue:
                {
                    if (Eof)
                    {
                        StoreAttribute(attributes, attrNameBuffer, attrValueBuffer, valueSet: true);
                        state = State.Data;
                        break;
                    }

                    var ch = Current;
                    if (char.IsWhiteSpace(ch))
                    {
                        _pos++;
                    }
                    else if (ch == '"' || ch == '\'')
                    {
                        attrQuote = Consume();
                        attrValueBuffer.Clear();
                        state = State.AttributeValueQuoted;
                    }
                    else if (ch == '>')
                    {
                        StoreAttribute(attributes, attrNameBuffer, attrValueBuffer, valueSet: true);
                        _pos++;
                        yield return EmitTag(tagNameBuffer, isEndTag, selfClosing, attributes);
                        state = State.Data;
                    }
                    else
                    {
                        attrValueBuffer.Clear();
                        state = State.AttributeValueUnquoted;
                    }
                    break;
                }

                // ═══════════════════════════════════════════════════════════
                //  ATTRIBUTE VALUE (QUOTED)
                // ═══════════════════════════════════════════════════════════
                case State.AttributeValueQuoted:
                {
                    if (Eof)
                    {
                        StoreAttribute(attributes, attrNameBuffer, attrValueBuffer, valueSet: true);
                        state = State.Data;
                        break;
                    }

                    var ch = Current;
                    if (ch == attrQuote)
                    {
                        _pos++;
                        StoreAttribute(attributes, attrNameBuffer, attrValueBuffer, valueSet: true);
                        state = State.BeforeAttributeName;
                    }
                    else
                    {
                        attrValueBuffer.Append(Consume());
                    }
                    break;
                }

                // ═══════════════════════════════════════════════════════════
                //  ATTRIBUTE VALUE (UNQUOTED)
                // ═══════════════════════════════════════════════════════════
                case State.AttributeValueUnquoted:
                {
                    if (Eof)
                    {
                        StoreAttribute(attributes, attrNameBuffer, attrValueBuffer, valueSet: true);
                        state = State.Data;
                        break;
                    }

                    var ch = Current;
                    if (char.IsWhiteSpace(ch))
                    {
                        _pos++;
                        StoreAttribute(attributes, attrNameBuffer, attrValueBuffer, valueSet: true);
                        state = State.BeforeAttributeName;
                    }
                    else if (ch == '>')
                    {
                        StoreAttribute(attributes, attrNameBuffer, attrValueBuffer, valueSet: true);
                        _pos++;
                        yield return EmitTag(tagNameBuffer, isEndTag, selfClosing, attributes);
                        state = State.Data;
                    }
                    else if (ch == '/')
                    {
                        StoreAttribute(attributes, attrNameBuffer, attrValueBuffer, valueSet: true);
                        _pos++;
                        state = State.SelfClosingStartTag;
                    }
                    else
                    {
                        attrValueBuffer.Append(Consume());
                    }
                    break;
                }

                // ═══════════════════════════════════════════════════════════
                //  SELF-CLOSING START TAG — we saw '/'
                // ═══════════════════════════════════════════════════════════
                case State.SelfClosingStartTag:
                {
                    if (Eof)
                    {
                        state = State.Data;
                        break;
                    }

                    if (Current == '>')
                    {
                        _pos++;
                        selfClosing = true;
                        yield return EmitTag(tagNameBuffer, isEndTag, true, attributes);
                        state = State.Data;
                    }
                    else
                    {
                        // '/' not followed by '>' — treat as before-attribute-name
                        state = State.BeforeAttributeName;
                    }
                    break;
                }

                // ═══════════════════════════════════════════════════════════
                //  MARKUP DECLARATION OPEN — we saw '<!'
                // ═══════════════════════════════════════════════════════════
                case State.MarkupDeclarationOpen:
                {
                    if (Eof)
                    {
                        state = State.Data;
                        break;
                    }

                    // Check for '<!--'
                    if (_pos + 1 < _input.Length && _input[_pos] == '-' && _input[_pos + 1] == '-')
                    {
                        _pos += 2;
                        commentBuffer.Clear();
                        state = State.Comment;
                    }
                    // Check for DOCTYPE (case-insensitive)
                    else if (MatchesAt("doctype", _pos))
                    {
                        // Consume until '>'
                        var start = _pos;
                        while (!Eof && Current != '>')
                            _pos++;
                        if (!Eof)
                            _pos++; // consume '>'
                        yield return new HtmlToken { Type = HtmlTokenType.Doctype, TagName = "doctype" };
                        state = State.Data;
                    }
                    else
                    {
                        // Bogus
                        commentBuffer.Clear();
                        state = State.BogusComment;
                    }
                    break;
                }

                // ═══════════════════════════════════════════════════════════
                //  COMMENT — inside <!-- ... -->
                // ═══════════════════════════════════════════════════════════
                case State.Comment:
                {
                    if (Eof)
                    {
                        yield return new HtmlToken { Type = HtmlTokenType.Comment, Text = commentBuffer.ToString() };
                        state = State.Data;
                        break;
                    }

                    var ch = Current;
                    if (ch == '-')
                    {
                        _pos++;
                        state = State.CommentDash;
                    }
                    else
                    {
                        commentBuffer.Append(Consume());
                    }
                    break;
                }

                // ═══════════════════════════════════════════════════════════
                //  COMMENT DASH — we saw '-' inside a comment
                // ═══════════════════════════════════════════════════════════
                case State.CommentDash:
                {
                    if (Eof)
                    {
                        commentBuffer.Append('-');
                        yield return new HtmlToken { Type = HtmlTokenType.Comment, Text = commentBuffer.ToString() };
                        state = State.Data;
                        break;
                    }

                    if (Current == '-')
                    {
                        _pos++;
                        state = State.CommentEnd;
                    }
                    else
                    {
                        commentBuffer.Append('-');
                        state = State.Comment;
                    }
                    break;
                }

                // ═══════════════════════════════════════════════════════════
                //  COMMENT END — we saw '--' inside a comment
                // ═══════════════════════════════════════════════════════════
                case State.CommentEnd:
                {
                    if (Eof)
                    {
                        yield return new HtmlToken { Type = HtmlTokenType.Comment, Text = commentBuffer.ToString() };
                        state = State.Data;
                        break;
                    }

                    if (Current == '>')
                    {
                        _pos++;
                        yield return new HtmlToken { Type = HtmlTokenType.Comment, Text = commentBuffer.ToString() };
                        state = State.Data;
                    }
                    else if (Current == '-')
                    {
                        // extra dash
                        commentBuffer.Append('-');
                        _pos++;
                        // stay in CommentEnd
                    }
                    else
                    {
                        // false alarm — '--' was not followed by '>'
                        commentBuffer.Append("--");
                        state = State.Comment;
                    }
                    break;
                }

                // ═══════════════════════════════════════════════════════════
                //  BOGUS COMMENT
                // ═══════════════════════════════════════════════════════════
                case State.BogusComment:
                {
                    if (Eof)
                    {
                        yield return new HtmlToken { Type = HtmlTokenType.Comment, Text = commentBuffer.ToString() };
                        state = State.Data;
                        break;
                    }

                    if (Current == '>')
                    {
                        _pos++;
                        yield return new HtmlToken { Type = HtmlTokenType.Comment, Text = commentBuffer.ToString() };
                        state = State.Data;
                    }
                    else
                    {
                        commentBuffer.Append(Consume());
                    }
                    break;
                }
            }
        }
    }

    // ── token-emission helpers ───────────────────────────────────────────────

    private static HtmlToken MakeTextToken(StringBuilder buffer)
    {
        var raw = buffer.ToString();
        var decoded = HtmlEntities.Decode(raw);
        return new HtmlToken { Type = HtmlTokenType.Text, Text = decoded };
    }

    private static HtmlToken EmitTag(
        StringBuilder tagNameBuf,
        bool isEndTag,
        bool selfClosing,
        Dictionary<string, string> attributes)
    {
        var name = tagNameBuf.ToString(); // already lowercased during collection

        if (isEndTag)
        {
            return new HtmlToken
            {
                Type = HtmlTokenType.EndTag,
                TagName = name,
            };
        }

        // Decode entity references inside attribute values
        var decodedAttrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in attributes)
            decodedAttrs[kvp.Key] = HtmlEntities.Decode(kvp.Value);

        return new HtmlToken
        {
            Type = HtmlTokenType.StartTag,
            TagName = name,
            SelfClosing = selfClosing,
            Attributes = decodedAttrs,
        };
    }

    private static void StoreAttribute(
        Dictionary<string, string> attributes,
        StringBuilder nameBuffer,
        StringBuilder valueBuffer,
        bool valueSet)
    {
        var name = nameBuffer.ToString();
        if (name.Length == 0) return;

        // First attribute wins (per the HTML spec)
        if (!attributes.ContainsKey(name))
            attributes[name] = valueSet ? valueBuffer.ToString() : "";

        nameBuffer.Clear();
        valueBuffer.Clear();
    }
}
