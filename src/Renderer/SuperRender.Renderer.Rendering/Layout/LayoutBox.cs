using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Style;

namespace SuperRender.Renderer.Rendering.Layout;

public sealed class TextRun
{
    public required string Text { get; init; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public required ComputedStyle Style { get; init; }
    /// <summary>True if this run was generated from a ::before/::after pseudo-element.</summary>
    public bool IsPseudoElement { get; init; }
}

public enum LayoutBoxType { Block, Inline, AnonymousBlock, InlineBlock, FlexContainer }

public sealed class LayoutBox
{
    public BoxDimensions Dimensions { get; set; }
    public Node? DomNode { get; init; }
    public required ComputedStyle Style { get; init; }
    public LayoutBoxType BoxType { get; init; }
    public List<LayoutBox> Children { get; } = [];
    public string? TextContent { get; init; }
    public List<TextRun>? TextRuns { get; set; }
}
