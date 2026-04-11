using SuperRender.Core.Dom;
using SuperRender.Core.Style;

namespace SuperRender.Core.Layout;

public sealed class LayoutEngine
{
    private readonly ITextMeasurer _textMeasurer;

    public LayoutEngine(ITextMeasurer textMeasurer)
    {
        _textMeasurer = textMeasurer;
    }

    public LayoutBox BuildLayoutTree(Document document, Dictionary<Node, ComputedStyle> styles,
        float viewportWidth, float viewportHeight)
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
        var root = BuildBox(body, styles);

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

        BlockLayout.Layout(root, viewport, _textMeasurer);

        return root;
    }

    private static LayoutBox BuildBox(Node node, Dictionary<Node, ComputedStyle> styles)
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
            _ => LayoutBoxType.Block,
        };

        var box = new LayoutBox
        {
            Style = style,
            BoxType = boxType,
            DomNode = node,
        };

        foreach (var child in node.Children)
        {
            var childStyle = styles.GetValueOrDefault(child) ?? new ComputedStyle();

            if (childStyle.Display == DisplayType.None)
                continue;

            var childBox = BuildBox(child, styles);

            // Skip empty text nodes in block context (unless white-space preserves them)
            if (childBox.TextContent != null && string.IsNullOrWhiteSpace(childBox.TextContent) &&
                box.BoxType == LayoutBoxType.Block && box.Children.Count == 0 &&
                style.WhiteSpace != WhiteSpaceType.Pre && style.WhiteSpace != WhiteSpaceType.PreWrap)
                continue;

            box.Children.Add(childBox);
        }

        return box;
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
