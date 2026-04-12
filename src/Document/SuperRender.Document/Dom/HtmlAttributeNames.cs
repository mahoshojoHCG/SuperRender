namespace SuperRender.Document.Dom;

/// <summary>
/// Centralized constants for HTML attribute names.
/// Eliminates magic string duplication across parsers, layout, painting, and browser layers.
/// </summary>
public static class HtmlAttributeNames
{
    public const string Id = "id";
    public const string Class = "class";
    public const string Src = "src";
    public const string Href = "href";
    public const string Rel = "rel";
    public const string Alt = "alt";
    public const string Target = "target";
    public const string Hidden = "hidden";
    public const string Style = "style";
    public const string Width = "width";
    public const string Height = "height";

    // Custom data attributes used by the rendering pipeline
    public const string DataNaturalWidth = "data-natural-width";
    public const string DataNaturalHeight = "data-natural-height";

    // Common attribute values
    public const string Stylesheet = "stylesheet";
    public const string TargetBlank = "_blank";
}
