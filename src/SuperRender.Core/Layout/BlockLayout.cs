using SuperRender.Core.Style;

namespace SuperRender.Core.Layout;

internal static class BlockLayout
{
    public static void Layout(LayoutBox box, BoxDimensions containingBlock, ITextMeasurer measurer)
    {
        CalculateBlockWidth(box, containingBlock);
        CalculateBlockPosition(box, containingBlock);
        LayoutBlockChildren(box, measurer);
        CalculateBlockHeight(box);
    }

    private static void CalculateBlockWidth(LayoutBox box, BoxDimensions containingBlock)
    {
        var style = box.Style;
        var containerWidth = containingBlock.Width;

        var marginLeft = style.Margin.Left;
        var marginRight = style.Margin.Right;
        var borderLeft = style.BorderWidth.Left;
        var borderRight = style.BorderWidth.Right;
        var paddingLeft = style.Padding.Left;
        var paddingRight = style.Padding.Right;

        if (float.IsNaN(marginLeft)) marginLeft = 0;
        if (float.IsNaN(marginRight)) marginRight = 0;

        var totalExtra = marginLeft + borderLeft + paddingLeft
                       + paddingRight + borderRight + marginRight;

        float contentWidth;
        if (!float.IsNaN(style.Width))
        {
            contentWidth = style.Width;
            // If total exceeds container, zero out auto margins
            var remaining = containerWidth - contentWidth - totalExtra;
            if (remaining > 0 && float.IsNaN(style.Margin.Left) && float.IsNaN(style.Margin.Right))
            {
                // Center: split remaining space
                marginLeft = remaining / 2f;
                marginRight = remaining / 2f;
            }
        }
        else
        {
            contentWidth = containerWidth - totalExtra;
            if (contentWidth < 0) contentWidth = 0;
        }

        var dims = box.Dimensions;
        dims.Width = contentWidth;
        dims.Margin = new EdgeSizes(style.Margin.Top, marginRight,
                                     style.Margin.Bottom, marginLeft);
        dims.Border = style.BorderWidth;
        dims.Padding = style.Padding;
        box.Dimensions = dims;
    }

    private static void CalculateBlockPosition(LayoutBox box, BoxDimensions containingBlock)
    {
        var dims = box.Dimensions;
        dims.X = containingBlock.X + dims.Margin.Left + dims.Border.Left + dims.Padding.Left;
        // Y is set by the caller (stacking logic)
        box.Dimensions = dims;
    }

    internal static void LayoutBlockChildren(LayoutBox box, ITextMeasurer measurer)
    {
        var dims = box.Dimensions;
        float cursorY = dims.Y;

        // Check if we have a mix of block and inline children
        bool hasBlock = false, hasInline = false;
        foreach (var child in box.Children)
        {
            if (child.BoxType == LayoutBoxType.Block)
                hasBlock = true;
            else
                hasInline = true;
        }

        if (hasBlock && hasInline)
        {
            // Wrap inline runs in anonymous blocks
            WrapInlineChildren(box);
        }

        foreach (var child in box.Children)
        {
            var childDims = child.Dimensions;
            childDims.Y = cursorY;
            child.Dimensions = childDims;

            if (child.BoxType == LayoutBoxType.Block || child.BoxType == LayoutBoxType.AnonymousBlock)
            {
                // Set up containing block for child
                var childContainer = new BoxDimensions
                {
                    X = dims.X,
                    Y = cursorY,
                    Width = dims.Width,
                    Height = dims.Height
                };

                if (child.BoxType == LayoutBoxType.AnonymousBlock)
                {
                    InlineLayout.Layout(child, childContainer, measurer);
                }
                else
                {
                    Layout(child, childContainer, measurer);
                }

                cursorY = child.Dimensions.MarginRect.Bottom;
            }
            else
            {
                // Single inline child in a block context — wrap in anonymous
                var anonContainer = new BoxDimensions
                {
                    X = dims.X,
                    Y = cursorY,
                    Width = dims.Width,
                    Height = dims.Height
                };
                InlineLayout.LayoutSingleInline(child, anonContainer, measurer);
                cursorY = child.Dimensions.MarginRect.Bottom;
            }
        }

        dims.Height = cursorY - dims.Y;
        box.Dimensions = dims;
    }

    private static void CalculateBlockHeight(LayoutBox box)
    {
        // If an explicit height is set, use it
        if (!float.IsNaN(box.Style.Height))
        {
            var dims = box.Dimensions;
            dims.Height = box.Style.Height;
            box.Dimensions = dims;
        }
    }

    private static void WrapInlineChildren(LayoutBox box)
    {
        var newChildren = new List<LayoutBox>();
        var inlineRun = new List<LayoutBox>();

        foreach (var child in box.Children)
        {
            if (child.BoxType == LayoutBoxType.Block)
            {
                if (inlineRun.Count > 0)
                {
                    newChildren.Add(CreateAnonymousBlock(inlineRun, box.Style));
                    inlineRun = [];
                }
                newChildren.Add(child);
            }
            else
            {
                inlineRun.Add(child);
            }
        }

        if (inlineRun.Count > 0)
        {
            newChildren.Add(CreateAnonymousBlock(inlineRun, box.Style));
        }

        box.Children.Clear();
        box.Children.AddRange(newChildren);
    }

    private static LayoutBox CreateAnonymousBlock(List<LayoutBox> inlineChildren, ComputedStyle parentStyle)
    {
        var anon = new LayoutBox
        {
            Style = parentStyle,
            BoxType = LayoutBoxType.AnonymousBlock,
            DomNode = null,
        };
        anon.Children.AddRange(inlineChildren);
        return anon;
    }
}
