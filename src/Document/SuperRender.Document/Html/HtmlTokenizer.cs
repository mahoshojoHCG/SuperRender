using System.Text;

namespace SuperRender.Document.Html;

/// <summary>
/// A state-machine HTML tokenizer that converts raw HTML text into a sequence of <see cref="HtmlToken"/> values.
/// </summary>
public sealed partial class HtmlTokenizer
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

    // ── tokenizer state fields (promoted from Tokenize() locals) ────────────

    private State _state;
    private readonly StringBuilder _textBuffer = new();
    private readonly StringBuilder _tagNameBuffer = new();
    private readonly StringBuilder _attrNameBuffer = new();
    private readonly StringBuilder _attrValueBuffer = new();
    private readonly StringBuilder _commentBuffer = new();
    private char _attrQuote = '"';
    private bool _isEndTag;
    private bool _selfClosing;
    private Dictionary<string, string> _attributes = new(StringComparer.OrdinalIgnoreCase);

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
        _state = State.Data;
        _textBuffer.Clear();
        _tagNameBuffer.Clear();
        _attrNameBuffer.Clear();
        _attrValueBuffer.Clear();
        _commentBuffer.Clear();
        _attrQuote = '"';
        _isEndTag = false;
        _selfClosing = false;
        _attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var emitted = new List<HtmlToken>();

        while (true)
        {
            emitted.Clear();

            _state = _state switch
            {
                State.Data => HandleDataState(emitted),
                State.TagOpen => HandleTagOpenState(emitted),
                State.EndTagOpen => HandleEndTagOpenState(emitted),
                State.TagName => HandleTagNameState(emitted),
                State.BeforeAttributeName => HandleBeforeAttributeNameState(emitted),
                State.AttributeName => HandleAttributeNameState(emitted),
                State.AfterAttributeName => HandleAfterAttributeNameState(emitted),
                State.BeforeAttributeValue => HandleBeforeAttributeValueState(emitted),
                State.AttributeValueQuoted => HandleAttributeValueQuotedState(emitted),
                State.AttributeValueUnquoted => HandleAttributeValueUnquotedState(emitted),
                State.SelfClosingStartTag => HandleSelfClosingStartTagState(emitted),
                State.BogusComment => HandleBogusCommentState(emitted),
                State.MarkupDeclarationOpen => HandleMarkupDeclarationOpenState(emitted),
                State.Comment => HandleCommentState(emitted),
                State.CommentDash => HandleCommentDashState(emitted),
                State.CommentEnd => HandleCommentEndState(emitted),
                _ => _state
            };

            foreach (var token in emitted)
            {
                yield return token;
            }

            if (emitted.Exists(t => t.Type == HtmlTokenType.EndOfFile))
                yield break;
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
