namespace SuperRender.Document.Dom;

/// <summary>
/// Centralized constants for HTML tag names.
/// Eliminates magic string duplication across parsers, layout, painting, and browser layers.
/// </summary>
public static class HtmlTagNames
{
    public const string Html = "html";
    public const string Head = "head";
    public const string Body = "body";
    public const string Title = "title";
    public const string Meta = "meta";
    public const string Link = "link";
    public const string Style = "style";
    public const string Script = "script";
    public const string NoScript = "noscript";
    public const string Base = "base";

    // Block elements
    public const string Div = "div";
    public const string P = "p";
    public const string Pre = "pre";

    // List elements
    public const string Ol = "ol";
    public const string Ul = "ul";
    public const string Li = "li";
    public const string Dd = "dd";
    public const string Dt = "dt";

    // Inline / media elements
    public const string A = "a";
    public const string Img = "img";
    public const string Span = "span";
    public const string Br = "br";

    // Form elements
    public const string Option = "option";
}
