using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Style;

namespace SuperRender.Renderer.Rendering.Layout;

public sealed class LayoutEngine
{
    private readonly ITextMeasurer _textMeasurer;

    public LayoutEngine(ITextMeasurer textMeasurer)
    {
        _textMeasurer = textMeasurer;
    }

    public LayoutBox BuildLayoutTree(DomDocument document, Dictionary<Node, ComputedStyle> styles,
        float viewportWidth, float viewportHeight,
        Dictionary<(Element, PseudoElementType), StyleResolver.PseudoElementInfo>? pseudoElements = null)
    {
        var body = document.Body;
        if (body == null)
        {
            return new LayoutBox
            {
                Style = new ComputedStyle(),
                BoxType = LayoutBoxType.Block,
                DomNode = document,
            };
        }

        var bodyStyle = styles.GetValueOrDefault(body) ?? new ComputedStyle();
        var root = BuildBox(body, styles, pseudoElements);

        // Layout from the viewport
        var viewport = new BoxDimensions
        {
            X = 0,
            Y = 0,
            Width = viewportWidth,
            Height = viewportHeight,
        };

        var rootDims = root.Dimensions;
        rootDims.Y = 0;
        root.Dimensions = rootDims;

        if (root.BoxType == LayoutBoxType.FlexContainer)
            FlexLayout.Layout(root, viewport, _textMeasurer);
        else if (root.BoxType == LayoutBoxType.GridContainer)
            GridLayout.Layout(root, viewport, _textMeasurer);
        else if (root.BoxType == LayoutBoxType.TableContainer)
            TableLayout.Layout(root, viewport, _textMeasurer);
        else
            BlockLayout.Layout(root, viewport, _textMeasurer);

        // Layout fixed-position elements relative to the viewport
        LayoutFixedDescendants(root, viewport, _textMeasurer);

        return root;
    }

    /// <summary>
    /// Recursively finds and lays out all position:fixed descendants relative to the viewport.
    /// Fixed elements are treated like absolute during layout but use viewport as containing block.
    /// </summary>
    private static void LayoutFixedDescendants(LayoutBox box, BoxDimensions viewport, ITextMeasurer measurer)
    {
        foreach (var child in box.Children)
        {
            if (child.Style.Position == PositionType.Fixed)
            {
                BlockLayout.LayoutFixedChild(child, viewport, measurer);
            }
            else
            {
                LayoutFixedDescendants(child, viewport, measurer);
            }
        }
    }

    private static LayoutBox BuildBox(Node node, Dictionary<Node, ComputedStyle> styles,
        Dictionary<(Element, PseudoElementType), StyleResolver.PseudoElementInfo>? pseudoElements)
    {
        var style = styles.GetValueOrDefault(node) ?? new ComputedStyle();

        if (style.Display == DisplayType.None)
        {
            return new LayoutBox
            {
                Style = style,
                BoxType = LayoutBoxType.Block,
                DomNode = node,
            };
        }

        if (node is TextNode textNode)
        {
            var text = ProcessWhitespace(textNode.Data, style.WhiteSpace);
            if (string.IsNullOrEmpty(text))
            {
                return new LayoutBox
                {
                    Style = style,
                    BoxType = LayoutBoxType.Inline,
                    DomNode = node,
                    TextContent = "",
                };
            }

            return new LayoutBox
            {
                Style = style,
                BoxType = LayoutBoxType.Inline,
                DomNode = node,
                TextContent = text,
            };
        }

        var boxType = style.Display switch
        {
            DisplayType.Inline => LayoutBoxType.Inline,
            DisplayType.InlineBlock => LayoutBoxType.InlineBlock,
            DisplayType.Flex or DisplayType.InlineFlex => LayoutBoxType.FlexContainer,
            DisplayType.Grid or DisplayType.InlineGrid => LayoutBoxType.GridContainer,
            DisplayType.Table or DisplayType.InlineTable => LayoutBoxType.TableContainer,
            DisplayType.TableRow => LayoutBoxType.TableRow,
            DisplayType.TableCell => LayoutBoxType.TableCell,
            DisplayType.ListItem => LayoutBoxType.Block,
            _ => LayoutBoxType.Block,
        };

        var box = new LayoutBox
        {
            Style = style,
            BoxType = boxType,
            DomNode = node,
        };

        // Insert ::before pseudo-element
        if (node is Element elem && pseudoElements != null)
        {
            if (pseudoElements.TryGetValue((elem, PseudoElementType.Before), out var beforeInfo))
            {
                box.Children.Add(new LayoutBox
                {
                    Style = beforeInfo.Style,
                    BoxType = LayoutBoxType.Inline,
                    DomNode = null,
                    TextContent = beforeInfo.Content,
                });
            }
        }

        foreach (var child in node.Children)
        {
            var childStyle = styles.GetValueOrDefault(child) ?? new ComputedStyle();

            if (childStyle.Display == DisplayType.None)
                continue;

            // display: contents — flatten this child's children into the current box
            if (childStyle.Display == DisplayType.Contents)
            {
                AddContentsChildren(child, box, styles, style, pseudoElements);
                continue;
            }

            var childBox = BuildBox(child, styles, pseudoElements);

            // Skip empty text nodes in block context (unless white-space preserves them)
            if (childBox.TextContent != null && string.IsNullOrWhiteSpace(childBox.TextContent) &&
                box.BoxType == LayoutBoxType.Block && box.Children.Count == 0 &&
                style.WhiteSpace != WhiteSpaceType.Pre && style.WhiteSpace != WhiteSpaceType.PreWrap)
                continue;

            box.Children.Add(childBox);
        }

        // Insert ::after pseudo-element
        if (node is Element elemAfter && pseudoElements != null)
        {
            if (pseudoElements.TryGetValue((elemAfter, PseudoElementType.After), out var afterInfo))
            {
                box.Children.Add(new LayoutBox
                {
                    Style = afterInfo.Style,
                    BoxType = LayoutBoxType.Inline,
                    DomNode = null,
                    TextContent = afterInfo.Content,
                });
            }
        }

        return box;
    }

    /// <summary>
    /// For display:contents elements, recursively adds their children to the parent box,
    /// skipping the element's own box generation.
    /// </summary>
    private static void AddContentsChildren(Node contentsNode, LayoutBox parentBox,
        Dictionary<Node, ComputedStyle> styles, ComputedStyle parentStyle,
        Dictionary<(Element, PseudoElementType), StyleResolver.PseudoElementInfo>? pseudoElements)
    {
        foreach (var grandchild in contentsNode.Children)
        {
            var grandchildStyle = styles.GetValueOrDefault(grandchild) ?? new ComputedStyle();
            if (grandchildStyle.Display == DisplayType.None)
                continue;

            if (grandchildStyle.Display == DisplayType.Contents)
            {
                AddContentsChildren(grandchild, parentBox, styles, parentStyle, pseudoElements);
                continue;
            }

            var childBox = BuildBox(grandchild, styles, pseudoElements);

            if (childBox.TextContent != null && string.IsNullOrWhiteSpace(childBox.TextContent) &&
                parentBox.BoxType == LayoutBoxType.Block && parentBox.Children.Count == 0 &&
                parentStyle.WhiteSpace != WhiteSpaceType.Pre && parentStyle.WhiteSpace != WhiteSpaceType.PreWrap)
                continue;

            parentBox.Children.Add(childBox);
        }
    }

    internal static string ProcessWhitespace(string text, WhiteSpaceType whiteSpace)
    {
        return whiteSpace switch
        {
            WhiteSpaceType.Pre or WhiteSpaceType.PreWrap => text, // preserve all whitespace
            WhiteSpaceType.PreLine => CollapseSpacesOnly(text), // collapse spaces, keep newlines
            _ => CollapseWhitespace(text), // Normal, Nowrap: collapse all whitespace
        };
    }

    private static string CollapseWhitespace(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        bool lastWasSpace = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }

        return sb.ToString();
    }

    private static string CollapseSpacesOnly(string text)
    {
        // Collapse spaces and tabs to single space, but preserve newlines
        var sb = new System.Text.StringBuilder(text.Length);
        bool lastWasSpace = false;

        foreach (var c in text)
        {
            if (c == '\n' || c == '\r')
            {
                if (c == '\r') continue; // skip \r, \n will handle it
                sb.Append('\n');
                lastWasSpace = false;
            }
            else if (c == ' ' || c == '\t')
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }

        return sb.ToString();
    }
}
