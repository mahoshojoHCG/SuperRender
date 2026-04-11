namespace SuperRender.Document.Html;

public enum HtmlTokenType
{
    StartTag,
    EndTag,
    Text,
    Comment,
    Doctype,
    EndOfFile,
}

public sealed class HtmlToken
{
    public HtmlTokenType Type { get; init; }

    /// <summary>Tag name in lowercase. Set for StartTag, EndTag, and Doctype tokens.</summary>
    public string? TagName { get; init; }

    /// <summary>Text content for Text and Comment tokens.</summary>
    public string? Text { get; init; }

    /// <summary>True when the tag is self-closing, e.g. &lt;br/&gt;.</summary>
    public bool SelfClosing { get; init; }

    /// <summary>Attribute key-value pairs for StartTag tokens. Keys are lowercase.</summary>
    public Dictionary<string, string> Attributes { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
