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
        ApplyPositionOffsets(box, containingBlock);
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

        var paddingH = paddingLeft + paddingRight;
        var borderH = borderLeft + borderRight;
        var totalExtra = marginLeft + borderLeft + paddingLeft
                       + paddingRight + borderRight + marginRight;

        float contentWidth;
        if (!float.IsNaN(style.Width))
        {
            if (style.BoxSizing == BoxSizing.BorderBox)
            {
                // In border-box, specified width includes padding + border
                contentWidth = Math.Max(0, style.Width - paddingH - borderH);
            }
            else
            {
                contentWidth = style.Width;
            }

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

        // Apply min/max width constraints
        contentWidth = ApplyMinMaxWidth(contentWidth, style, paddingH, borderH);

        var dims = box.Dimensions;
        dims.Width = contentWidth;
        dims.Margin = new EdgeSizes(style.Margin.Top, marginRight,
                                     style.Margin.Bottom, marginLeft);
        dims.Border = style.BorderWidth;
        dims.Padding = style.Padding;
        box.Dimensions = dims;
    }

    private static float ApplyMinMaxWidth(float contentWidth, ComputedStyle style, float paddingH, float borderH)
    {
        if (!float.IsNaN(style.MinWidth))
        {
            float minContent = style.BoxSizing == BoxSizing.BorderBox
                ? Math.Max(0, style.MinWidth - paddingH - borderH)
                : style.MinWidth;
            if (contentWidth < minContent) contentWidth = minContent;
        }

        if (!float.IsNaN(style.MaxWidth))
        {
            float maxContent = style.BoxSizing == BoxSizing.BorderBox
                ? Math.Max(0, style.MaxWidth - paddingH - borderH)
                : style.MaxWidth;
            if (contentWidth > maxContent) contentWidth = maxContent;
        }

        return contentWidth;
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

        // Separate absolutely positioned children from normal flow
        var normalFlow = new List<LayoutBox>();
        var absoluteChildren = new List<LayoutBox>();

        foreach (var child in box.Children)
        {
            if (child.Style.Position == PositionType.Absolute)
                absoluteChildren.Add(child);
            else
                normalFlow.Add(child);
        }

        // Check if normal-flow children have a mix of block and inline
        bool hasBlock = false, hasInline = false;
        foreach (var child in normalFlow)
        {
            if (child.BoxType == LayoutBoxType.Block)
                hasBlock = true;
            else
                hasInline = true;
        }

        if (hasBlock && hasInline)
        {
            // Wrap inline runs in anonymous blocks
            WrapInlineChildren(box, normalFlow);
            // Re-scan normalFlow from box.Children minus absolute children
            normalFlow.Clear();
            foreach (var child in box.Children)
            {
                if (child.Style.Position != PositionType.Absolute)
                    normalFlow.Add(child);
            }
        }

        // Layout normal-flow children
        foreach (var child in normalFlow)
        {
            var childDims = child.Dimensions;
            childDims.Y = cursorY;
            child.Dimensions = childDims;

            if (child.BoxType == LayoutBoxType.Block || child.BoxType == LayoutBoxType.AnonymousBlock)
            {
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
                if (child.Style.Display == DisplayType.InlineBlock)
                {
                    // Inline-block in a block context: lay out as block
                    var ibContainer = new BoxDimensions
                    {
                        X = dims.X,
                        Y = cursorY,
                        Width = dims.Width,
                        Height = dims.Height
                    };
                    Layout(child, ibContainer, measurer);
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
                }
                cursorY = child.Dimensions.MarginRect.Bottom;
            }
        }

        dims.Height = cursorY - dims.Y;
        box.Dimensions = dims;

        // Layout absolutely positioned children relative to this box
        foreach (var absChild in absoluteChildren)
        {
            LayoutAbsoluteChild(absChild, box, measurer);
        }
    }

    private static void CalculateBlockHeight(LayoutBox box)
    {
        var style = box.Style;
        var dims = box.Dimensions;
        float height = dims.Height;

        // If an explicit height is set, use it
        if (!float.IsNaN(style.Height))
        {
            if (style.BoxSizing == BoxSizing.BorderBox)
            {
                var paddingV = style.Padding.Top + style.Padding.Bottom;
                var borderV = style.BorderWidth.Top + style.BorderWidth.Bottom;
                height = Math.Max(0, style.Height - paddingV - borderV);
            }
            else
            {
                height = style.Height;
            }
        }

        // Apply min/max height constraints
        height = ApplyMinMaxHeight(height, style);

        dims.Height = height;
        box.Dimensions = dims;
    }

    private static float ApplyMinMaxHeight(float height, ComputedStyle style)
    {
        float paddingV = style.Padding.Top + style.Padding.Bottom;
        float borderV = style.BorderWidth.Top + style.BorderWidth.Bottom;

        if (!float.IsNaN(style.MinHeight))
        {
            float minContent = style.BoxSizing == BoxSizing.BorderBox
                ? Math.Max(0, style.MinHeight - paddingV - borderV)
                : style.MinHeight;
            if (height < minContent) height = minContent;
        }

        if (!float.IsNaN(style.MaxHeight))
        {
            float maxContent = style.BoxSizing == BoxSizing.BorderBox
                ? Math.Max(0, style.MaxHeight - paddingV - borderV)
                : style.MaxHeight;
            if (height > maxContent) height = maxContent;
        }

        return height;
    }

    private static void ApplyPositionOffsets(LayoutBox box, BoxDimensions containingBlock)
    {
        var style = box.Style;
        if (style.Position != PositionType.Relative)
            return;

        var dims = box.Dimensions;

        if (!float.IsNaN(style.Top))
            dims.Y += style.Top;
        else if (!float.IsNaN(style.Bottom))
            dims.Y -= style.Bottom;

        if (!float.IsNaN(style.Left))
            dims.X += style.Left;
        else if (!float.IsNaN(style.Right))
            dims.X -= style.Right;

        box.Dimensions = dims;
    }

    private static void LayoutAbsoluteChild(LayoutBox child, LayoutBox containingBox, ITextMeasurer measurer)
    {
        var style = child.Style;
        var container = containingBox.Dimensions;

        // First, do a normal block layout to determine intrinsic size
        var absContainer = new BoxDimensions
        {
            X = container.X,
            Y = container.Y,
            Width = container.Width,
            Height = container.Height,
        };

        if (child.BoxType == LayoutBoxType.Block)
            Layout(child, absContainer, measurer);
        else
        {
            InlineLayout.LayoutSingleInline(child, absContainer, measurer);
        }

        // Then apply absolute positioning offsets
        var dims = child.Dimensions;

        if (!float.IsNaN(style.Left))
            dims.X = container.X + style.Left + dims.Margin.Left + dims.Border.Left + dims.Padding.Left;
        else if (!float.IsNaN(style.Right))
            dims.X = container.X + container.Width - style.Right - dims.Width
                     - dims.Padding.Right - dims.Border.Right - dims.Margin.Right;

        if (!float.IsNaN(style.Top))
            dims.Y = container.Y + style.Top + dims.Margin.Top + dims.Border.Top + dims.Padding.Top;
        else if (!float.IsNaN(style.Bottom))
            dims.Y = container.Y + container.Height - style.Bottom - dims.Height
                     - dims.Padding.Bottom - dims.Border.Bottom - dims.Margin.Bottom;

        child.Dimensions = dims;
    }

    private static void WrapInlineChildren(LayoutBox box, List<LayoutBox> normalFlow)
    {
        var newChildren = new List<LayoutBox>();
        var inlineRun = new List<LayoutBox>();

        // Preserve absolute children in order, wrapping only normalFlow
        foreach (var child in box.Children)
        {
            if (child.Style.Position == PositionType.Absolute)
            {
                // Flush current inline run before adding the absolute child
                if (inlineRun.Count > 0)
                {
                    newChildren.Add(CreateAnonymousBlock(inlineRun, box.Style));
                    inlineRun = [];
                }
                newChildren.Add(child);
                continue;
            }

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
