using SuperRender.Core.Style;

namespace SuperRender.Core.Layout;

internal static class BlockLayout
{
    public static void Layout(LayoutBox box, BoxDimensions containingBlock, ITextMeasurer measurer)
    {
        CalculateBlockWidth(box, containingBlock);
        CalculateBlockPosition(box, containingBlock);
        LayoutBlockChildren(box, measurer, containingBlock);
        CalculateBlockHeight(box);
        ApplyRelativeOffset(box);
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

            // border-box: specified width includes padding + border
            if (style.BoxSizing == BoxSizingType.BorderBox)
            {
                contentWidth -= paddingLeft + paddingRight + borderLeft + borderRight;
                if (contentWidth < 0) contentWidth = 0;
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

        // Apply min/max constraints
        contentWidth = ApplyMinMaxWidth(contentWidth, style);

        var dims = box.Dimensions;
        dims.Width = contentWidth;
        dims.Margin = new EdgeSizes(style.Margin.Top, marginRight,
                                     style.Margin.Bottom, marginLeft);
        dims.Border = style.BorderWidth;
        dims.Padding = style.Padding;
        box.Dimensions = dims;
    }

    private static float ApplyMinMaxWidth(float contentWidth, ComputedStyle style)
    {
        float minW = style.MinWidth;
        float maxW = style.MaxWidth;

        // For border-box, min/max include padding+border too
        if (style.BoxSizing == BoxSizingType.BorderBox)
        {
            float extra = style.Padding.Left + style.Padding.Right
                        + style.BorderWidth.Left + style.BorderWidth.Right;
            if (minW > 0) minW = Math.Max(0, minW - extra);
            if (!float.IsPositiveInfinity(maxW)) maxW = Math.Max(0, maxW - extra);
        }

        if (contentWidth < minW) contentWidth = minW;
        if (!float.IsPositiveInfinity(maxW) && contentWidth > maxW) contentWidth = maxW;

        return contentWidth;
    }

    private static void CalculateBlockPosition(LayoutBox box, BoxDimensions containingBlock)
    {
        var dims = box.Dimensions;
        dims.X = containingBlock.X + dims.Margin.Left + dims.Border.Left + dims.Padding.Left;
        // Y is set by the caller to the current cursor position.
        // Adjust it past the box's own margin, border, and padding (same as X direction).
        dims.Y += dims.Margin.Top + dims.Border.Top + dims.Padding.Top;
        box.Dimensions = dims;
    }

    internal static void LayoutBlockChildren(LayoutBox box, ITextMeasurer measurer,
        BoxDimensions containingBlock)
    {
        var dims = box.Dimensions;
        float cursorY = dims.Y;

        // Separate normal-flow and absolutely-positioned children
        var normalChildren = new List<LayoutBox>();
        var absoluteChildren = new List<LayoutBox>();

        foreach (var child in box.Children)
        {
            if (child.Style.Position == PositionType.Absolute)
                absoluteChildren.Add(child);
            else
                normalChildren.Add(child);
        }

        // Check if we have a mix of block and inline children (normal flow only)
        bool hasBlock = false, hasInline = false;
        foreach (var child in normalChildren)
        {
            if (child.BoxType == LayoutBoxType.Block)
                hasBlock = true;
            else
                hasInline = true;
        }

        // Build the effective child list for layout
        var layoutChildren = normalChildren;
        if (hasBlock && hasInline)
        {
            // Mixed block/inline: wrap inline runs in anonymous blocks
            layoutChildren = WrapInlineChildren(normalChildren, box.Style);
        }
        else if (hasInline && !hasBlock)
        {
            // All inline: wrap all in a single anonymous block so InlineLayout handles
            // nested inline elements (e.g. <a> inside <li>) correctly
            layoutChildren = [CreateAnonymousBlock(normalChildren, box.Style)];
        }

        foreach (var child in layoutChildren)
        {
            var childDims = child.Dimensions;
            childDims.Y = cursorY;
            child.Dimensions = childDims;

            if (child.BoxType == LayoutBoxType.Block)
            {
                var childContainer = new BoxDimensions
                {
                    X = dims.X,
                    Y = cursorY,
                    Width = dims.Width,
                    Height = dims.Height
                };

                Layout(child, childContainer, measurer);
                cursorY = child.Dimensions.MarginRect.Bottom;
            }
            else
            {
                // AnonymousBlock or remaining inline
                var childContainer = new BoxDimensions
                {
                    X = dims.X,
                    Y = cursorY,
                    Width = dims.Width,
                    Height = dims.Height
                };

                InlineLayout.Layout(child, childContainer, measurer);
                cursorY = child.Dimensions.MarginRect.Bottom;
            }
        }

        dims.Height = cursorY - dims.Y;
        box.Dimensions = dims;

        // Apply explicit height BEFORE absolute children so bottom-positioning
        // can use the correct containing block height
        if (absoluteChildren.Count > 0)
        {
            CalculateBlockHeight(box);
        }

        // Now layout absolutely-positioned children
        foreach (var absChild in absoluteChildren)
        {
            LayoutAbsoluteChild(absChild, box, measurer);
        }
    }

    private static void LayoutAbsoluteChild(LayoutBox absChild, LayoutBox containingBox, ITextMeasurer measurer)
    {
        var style = absChild.Style;
        var cbDims = containingBox.Dimensions;

        // Calculate width: use explicit if given, otherwise shrink-to-fit (use containing width as max)
        float contentWidth;
        if (!float.IsNaN(style.Width))
        {
            contentWidth = style.Width;
            if (style.BoxSizing == BoxSizingType.BorderBox)
            {
                contentWidth -= style.Padding.Left + style.Padding.Right
                              + style.BorderWidth.Left + style.BorderWidth.Right;
                if (contentWidth < 0) contentWidth = 0;
            }
        }
        else if (!float.IsNaN(style.Left) && !float.IsNaN(style.Right))
        {
            // Width determined by left + right
            contentWidth = cbDims.Width - style.Left - style.Right
                         - style.Margin.Left - style.Margin.Right
                         - style.BorderWidth.Left - style.BorderWidth.Right
                         - style.Padding.Left - style.Padding.Right;
            if (contentWidth < 0) contentWidth = 0;
        }
        else
        {
            contentWidth = cbDims.Width; // initial max width for layout; will shrink-to-fit below
        }

        contentWidth = ApplyMinMaxWidth(contentWidth, style);
        bool needsShrinkToFit = float.IsNaN(style.Width)
            && (float.IsNaN(style.Left) || float.IsNaN(style.Right));

        var dims = absChild.Dimensions;
        dims.Width = contentWidth;
        dims.Padding = style.Padding;
        dims.Border = style.BorderWidth;
        dims.Margin = new EdgeSizes(
            float.IsNaN(style.Margin.Top) ? 0 : style.Margin.Top,
            float.IsNaN(style.Margin.Right) ? 0 : style.Margin.Right,
            float.IsNaN(style.Margin.Bottom) ? 0 : style.Margin.Bottom,
            float.IsNaN(style.Margin.Left) ? 0 : style.Margin.Left);

        // Position X
        float contentX;
        if (!float.IsNaN(style.Left))
        {
            contentX = cbDims.X + style.Left + dims.Margin.Left + dims.Border.Left + dims.Padding.Left;
        }
        else if (!float.IsNaN(style.Right))
        {
            float outerWidth = contentWidth + dims.Padding.Left + dims.Padding.Right
                             + dims.Border.Left + dims.Border.Right;
            contentX = cbDims.X + cbDims.Width - style.Right - dims.Margin.Right
                     - dims.Border.Right - dims.Padding.Right - contentWidth;
        }
        else
        {
            contentX = cbDims.X + dims.Margin.Left + dims.Border.Left + dims.Padding.Left;
        }

        // Position Y
        float contentY;
        if (!float.IsNaN(style.Top))
        {
            contentY = cbDims.Y + style.Top + dims.Margin.Top + dims.Border.Top + dims.Padding.Top;
        }
        else if (!float.IsNaN(style.Bottom))
        {
            // Need height first — layout children to get content height
            dims.X = contentX;
            dims.Y = cbDims.Y; // temporary
            absChild.Dimensions = dims;

            LayoutBlockChildren(absChild, measurer, new BoxDimensions
            {
                X = contentX,
                Y = cbDims.Y,
                Width = contentWidth,
                Height = cbDims.Height
            });

            float height = !float.IsNaN(style.Height) ? style.Height : absChild.Dimensions.Height;
            contentY = cbDims.Y + cbDims.Height - style.Bottom - dims.Margin.Bottom
                     - dims.Border.Bottom - dims.Padding.Bottom - height;
            dims = absChild.Dimensions;
            float deltaY = contentY - dims.Y;
            dims.X = contentX;
            dims.Y = contentY;
            if (!float.IsNaN(style.Height)) dims.Height = style.Height;

            // Shrink-to-fit width for auto-width absolute elements
            if (needsShrinkToFit)
            {
                float contentRight = InlineLayout.ComputeContentRight(absChild);
                float shrunkWidth = Math.Max(0, contentRight - dims.X);
                dims.Width = shrunkWidth;
            }

            absChild.Dimensions = dims;

            // Offset text runs and child positions from temporary Y to final Y
            if (deltaY != 0)
                OffsetSubtree(absChild, 0, deltaY);

            ApplyRelativeOffset(absChild);
            return;
        }
        else
        {
            contentY = cbDims.Y + dims.Margin.Top + dims.Border.Top + dims.Padding.Top;
        }

        dims.X = contentX;
        dims.Y = contentY;
        absChild.Dimensions = dims;

        // Layout children
        LayoutBlockChildren(absChild, measurer, new BoxDimensions
        {
            X = contentX,
            Y = contentY,
            Width = contentWidth,
            Height = cbDims.Height
        });

        dims = absChild.Dimensions;
        if (!float.IsNaN(style.Height))
        {
            dims.Height = style.Height;
            if (style.BoxSizing == BoxSizingType.BorderBox)
            {
                dims.Height -= style.Padding.Top + style.Padding.Bottom
                             + style.BorderWidth.Top + style.BorderWidth.Bottom;
                if (dims.Height < 0) dims.Height = 0;
            }
        }

        // Shrink-to-fit width for auto-width absolute elements
        if (needsShrinkToFit)
        {
            float contentRight = InlineLayout.ComputeContentRight(absChild);
            float shrunkWidth = Math.Max(0, contentRight - dims.X);
            dims.Width = shrunkWidth;
        }

        absChild.Dimensions = dims;
    }

    private static void ApplyRelativeOffset(LayoutBox box)
    {
        if (box.Style.Position != PositionType.Relative) return;

        var style = box.Style;
        var dims = box.Dimensions;

        float offsetX = 0, offsetY = 0;

        if (!float.IsNaN(style.Left))
            offsetX = style.Left;
        else if (!float.IsNaN(style.Right))
            offsetX = -style.Right;

        if (!float.IsNaN(style.Top))
            offsetY = style.Top;
        else if (!float.IsNaN(style.Bottom))
            offsetY = -style.Bottom;

        if (offsetX == 0 && offsetY == 0) return;

        dims.X += offsetX;
        dims.Y += offsetY;
        box.Dimensions = dims;

        // Also offset all text runs and child dimensions in the subtree
        OffsetSubtree(box, offsetX, offsetY);
    }

    /// <summary>
    /// Recursively offsets all TextRun coordinates and child box dimensions
    /// in the subtree. Used after repositioning a box (relative offset, absolute bottom).
    /// </summary>
    internal static void OffsetSubtree(LayoutBox box, float dx, float dy)
    {
        if (box.TextRuns is not null)
        {
            foreach (var run in box.TextRuns)
            {
                run.X += dx;
                run.Y += dy;
            }
        }

        foreach (var child in box.Children)
        {
            var childDims = child.Dimensions;
            childDims.X += dx;
            childDims.Y += dy;
            child.Dimensions = childDims;

            OffsetSubtree(child, dx, dy);
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
            height = style.Height;
            if (style.BoxSizing == BoxSizingType.BorderBox)
            {
                height -= style.Padding.Top + style.Padding.Bottom
                        + style.BorderWidth.Top + style.BorderWidth.Bottom;
                if (height < 0) height = 0;
            }
        }

        // When overflow is hidden and explicit height is set, clamp to that height
        if (style.Overflow == OverflowType.Hidden && !float.IsNaN(style.Height))
        {
            float explicitHeight = style.Height;
            if (style.BoxSizing == BoxSizingType.BorderBox)
            {
                explicitHeight -= style.Padding.Top + style.Padding.Bottom
                                + style.BorderWidth.Top + style.BorderWidth.Bottom;
                if (explicitHeight < 0) explicitHeight = 0;
            }
            height = explicitHeight;
        }

        // Apply min/max height constraints
        float minH = style.MinHeight;
        float maxH = style.MaxHeight;
        if (style.BoxSizing == BoxSizingType.BorderBox)
        {
            float extra = style.Padding.Top + style.Padding.Bottom
                        + style.BorderWidth.Top + style.BorderWidth.Bottom;
            if (minH > 0) minH = Math.Max(0, minH - extra);
            if (!float.IsPositiveInfinity(maxH)) maxH = Math.Max(0, maxH - extra);
        }
        if (height < minH) height = minH;
        if (!float.IsPositiveInfinity(maxH) && height > maxH) height = maxH;

        dims.Height = height;
        box.Dimensions = dims;
    }

    private static List<LayoutBox> WrapInlineChildren(List<LayoutBox> children, ComputedStyle parentStyle)
    {
        var newChildren = new List<LayoutBox>();
        var inlineRun = new List<LayoutBox>();

        foreach (var child in children)
        {
            if (child.BoxType == LayoutBoxType.Block)
            {
                if (inlineRun.Count > 0)
                {
                    newChildren.Add(CreateAnonymousBlock(inlineRun, parentStyle));
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
            newChildren.Add(CreateAnonymousBlock(inlineRun, parentStyle));
        }

        return newChildren;
    }

    private static LayoutBox CreateAnonymousBlock(List<LayoutBox> inlineChildren, ComputedStyle parentStyle)
    {
        // Anonymous blocks are layout wrappers — they inherit text properties from
        // the parent but must NOT have their own background, border, padding, or margin
        // to avoid painting duplicates.
        var anonStyle = new ComputedStyle
        {
            Color = parentStyle.Color,
            FontSize = parentStyle.FontSize,
            FontFamily = parentStyle.FontFamily,
            FontWeight = parentStyle.FontWeight,
            FontStyle = parentStyle.FontStyle,
            TextAlign = parentStyle.TextAlign,
            LineHeight = parentStyle.LineHeight,
            WhiteSpace = parentStyle.WhiteSpace,
        };

        var anon = new LayoutBox
        {
            Style = anonStyle,
            BoxType = LayoutBoxType.AnonymousBlock,
            DomNode = null,
        };
        anon.Children.AddRange(inlineChildren);
        return anon;
    }
}
