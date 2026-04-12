using SuperRender.Renderer.Rendering.Style;

namespace SuperRender.Renderer.Rendering.Layout;

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

        // Resolve width including deferred calc/percentage expressions
        float resolvedWidth = LayoutHelper.ResolveWidth(style, containerWidth);

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
        if (!float.IsNaN(resolvedWidth))
        {
            contentWidth = resolvedWidth;

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
        else if (TryGetImageIntrinsicWidth(box, out float intrinsicWidth))
        {
            contentWidth = intrinsicWidth;
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
            else if (child.Style.Position == PositionType.Fixed)
                continue; // Fixed elements are laid out by LayoutEngine after the full tree
            else
                normalChildren.Add(child);
        }

        // Check if we have a mix of block and inline children (normal flow only)
        bool hasBlock = false, hasInline = false;
        foreach (var child in normalChildren)
        {
            // inline-flex participates in inline flow (like inline-block)
            if (child.Style.Display == DisplayType.InlineFlex)
                hasInline = true;
            else if (child.BoxType is LayoutBoxType.Block or LayoutBoxType.FlexContainer or LayoutBoxType.GridContainer or LayoutBoxType.TableContainer)
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

        float prevMarginBottom = 0;
        bool isFirstChild = true;

        // Parent-first-child margin collapsing:
        // When parent has no border-top/padding-top, its top margin collapses
        // with the first child's top margin.
        bool parentCollapseTop = box.Style.BorderWidth.Top == 0 && box.Style.Padding.Top == 0;

        for (int i = 0; i < layoutChildren.Count; i++)
        {
            var child = layoutChildren[i];

            // Margin collapsing between adjacent siblings
            float childMarginTop = child.Style.Margin.Top;
            if (float.IsNaN(childMarginTop)) childMarginTop = 0;

            if (!isFirstChild && prevMarginBottom != 0 && childMarginTop != 0)
            {
                // Collapse adjacent margins: use the larger of the two.
                // cursorY already includes prevMarginBottom (from MarginRect.Bottom of prev child).
                // CalculateBlockPosition will add child's margin.Top.
                // Without collapsing: gap = prevMarginBottom + childMarginTop
                // With collapsing: gap = max(prevMarginBottom, childMarginTop)
                // So we set child's margin.Top = collapsed - prevMarginBottom
                float collapsed = Math.Max(prevMarginBottom, childMarginTop);
                float adjustedTop = collapsed - prevMarginBottom;
                child.Style.Margin = child.Style.Margin with { Top = adjustedTop };
            }
            else if (isFirstChild && parentCollapseTop && childMarginTop > 0
                     && child.BoxType == LayoutBoxType.Block)
            {
                // Parent-first-child margin collapsing:
                // The parent's top margin and first child's top margin collapse.
                // The child's margin effectively moves to the parent.
                float parentMarginTop = box.Style.Margin.Top;
                if (float.IsNaN(parentMarginTop)) parentMarginTop = 0;
                float collapsed = Math.Max(parentMarginTop, childMarginTop);

                // Move the parent box up/down to account for the collapsed margin
                var boxDims = box.Dimensions;
                float oldParentTop = boxDims.Margin.Top;
                boxDims.Margin = boxDims.Margin with { Top = collapsed };
                float shift = collapsed - oldParentTop;
                if (shift != 0)
                {
                    boxDims.Y += shift;
                    box.Dimensions = boxDims;
                    dims = box.Dimensions;
                    cursorY = dims.Y;
                }

                // Zero out the child's top margin to avoid double-counting
                child.Style.Margin = child.Style.Margin with { Top = 0 };
            }

            var childDims = child.Dimensions;
            childDims.Y = cursorY;
            child.Dimensions = childDims;

            if (child.BoxType == LayoutBoxType.FlexContainer)
            {
                var childContainer = new BoxDimensions
                {
                    X = dims.X,
                    Y = cursorY,
                    Width = dims.Width,
                    Height = dims.Height
                };

                FlexLayout.Layout(child, childContainer, measurer);
                cursorY = child.Dimensions.MarginRect.Bottom;
            }
            else if (child.BoxType == LayoutBoxType.GridContainer)
            {
                var childContainer = new BoxDimensions
                {
                    X = dims.X,
                    Y = cursorY,
                    Width = dims.Width,
                    Height = dims.Height
                };

                GridLayout.Layout(child, childContainer, measurer);
                cursorY = child.Dimensions.MarginRect.Bottom;
            }
            else if (child.BoxType == LayoutBoxType.TableContainer)
            {
                var childContainer = new BoxDimensions
                {
                    X = dims.X,
                    Y = cursorY,
                    Width = dims.Width,
                    Height = dims.Height
                };

                TableLayout.Layout(child, childContainer, measurer);
                cursorY = child.Dimensions.MarginRect.Bottom;
            }
            else if (child.BoxType == LayoutBoxType.Block)
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

            // Margin collapsing through empty blocks:
            // When a block has zero height, no border, no padding, its top and bottom margins collapse
            float childHeight = child.Dimensions.Height;
            float childMarginBottom = child.Style.Margin.Bottom;
            if (float.IsNaN(childMarginBottom)) childMarginBottom = 0;

            if (childHeight == 0 && child.Style.BorderWidth.Top == 0
                && child.Style.BorderWidth.Bottom == 0
                && child.Style.Padding.Top == 0 && child.Style.Padding.Bottom == 0
                && child.BoxType == LayoutBoxType.Block)
            {
                // Empty block: collapse top and bottom margins into one
                float effectiveChildTop = child.Style.Margin.Top;
                if (float.IsNaN(effectiveChildTop)) effectiveChildTop = 0;
                float collapsedMargin = Math.Max(effectiveChildTop, childMarginBottom);
                // Rewind cursorY to before this empty block's margins
                cursorY = child.Dimensions.Y - (float.IsNaN(child.Style.Margin.Top) ? 0 : child.Style.Margin.Top)
                        - child.Style.BorderWidth.Top - child.Style.Padding.Top;
                cursorY += collapsedMargin;
                prevMarginBottom = collapsedMargin;
            }
            else
            {
                prevMarginBottom = childMarginBottom;
            }

            isFirstChild = false;
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
        float resolvedAbsWidth = LayoutHelper.ResolveWidth(style, cbDims.Width);
        if (!float.IsNaN(resolvedAbsWidth))
        {
            contentWidth = resolvedAbsWidth;
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
        bool needsShrinkToFit = float.IsNaN(resolvedAbsWidth)
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

            float absResolvedHeight = LayoutHelper.ResolveHeight(style, cbDims.Height);
            float height = !float.IsNaN(absResolvedHeight) ? absResolvedHeight : absChild.Dimensions.Height;
            contentY = cbDims.Y + cbDims.Height - style.Bottom - dims.Margin.Bottom
                     - dims.Border.Bottom - dims.Padding.Bottom - height;
            dims = absChild.Dimensions;
            float deltaY = contentY - dims.Y;
            dims.X = contentX;
            dims.Y = contentY;
            if (!float.IsNaN(absResolvedHeight)) dims.Height = absResolvedHeight;

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
        float absHeight2 = LayoutHelper.ResolveHeight(style, cbDims.Height);
        if (!float.IsNaN(absHeight2))
        {
            dims.Height = absHeight2;
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

            // Recalculate X when right positioning was used, since X was
            // originally computed with the pre-shrink width
            if (!float.IsNaN(style.Right) && float.IsNaN(style.Left))
            {
                dims.X = cbDims.X + cbDims.Width - style.Right - dims.Margin.Right
                       - dims.Border.Right - dims.Padding.Right - shrunkWidth;
                // Re-offset text runs and children to new X position
                float dx = dims.X - contentX;
                if (dx != 0)
                    OffsetSubtree(absChild, dx, 0);
            }
        }

        absChild.Dimensions = dims;
    }

    /// <summary>
    /// Lays out a position:fixed child relative to the viewport.
    /// Uses the same logic as absolute positioning but always relative to viewport origin.
    /// </summary>
    internal static void LayoutFixedChild(LayoutBox fixedChild, BoxDimensions viewport, ITextMeasurer measurer)
    {
        // Reuse absolute positioning logic with viewport as containing block
        var style = fixedChild.Style;
        var cbDims = viewport;

        float contentWidth;
        float resolvedWidth = LayoutHelper.ResolveWidth(style, cbDims.Width);
        if (!float.IsNaN(resolvedWidth))
        {
            contentWidth = resolvedWidth;
            if (style.BoxSizing == BoxSizingType.BorderBox)
            {
                contentWidth -= style.Padding.Left + style.Padding.Right
                              + style.BorderWidth.Left + style.BorderWidth.Right;
                if (contentWidth < 0) contentWidth = 0;
            }
        }
        else if (!float.IsNaN(style.Left) && !float.IsNaN(style.Right))
        {
            contentWidth = cbDims.Width - style.Left - style.Right
                         - style.Margin.Left - style.Margin.Right
                         - style.BorderWidth.Left - style.BorderWidth.Right
                         - style.Padding.Left - style.Padding.Right;
            if (contentWidth < 0) contentWidth = 0;
        }
        else
        {
            contentWidth = cbDims.Width;
        }

        contentWidth = ApplyMinMaxWidth(contentWidth, style);

        var dims = fixedChild.Dimensions;
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
            dims.X = contentX;
            dims.Y = cbDims.Y;
            fixedChild.Dimensions = dims;

            LayoutBlockChildren(fixedChild, measurer, new BoxDimensions
            {
                X = contentX, Y = cbDims.Y, Width = contentWidth, Height = cbDims.Height
            });

            float fixedResolvedH = LayoutHelper.ResolveHeight(style, cbDims.Height);
            float height = !float.IsNaN(fixedResolvedH) ? fixedResolvedH : fixedChild.Dimensions.Height;
            contentY = cbDims.Y + cbDims.Height - style.Bottom - dims.Margin.Bottom
                     - dims.Border.Bottom - dims.Padding.Bottom - height;
            dims = fixedChild.Dimensions;
            float deltaY = contentY - dims.Y;
            dims.X = contentX;
            dims.Y = contentY;
            if (!float.IsNaN(fixedResolvedH)) dims.Height = fixedResolvedH;
            fixedChild.Dimensions = dims;

            if (deltaY != 0)
                OffsetSubtree(fixedChild, 0, deltaY);

            return;
        }
        else
        {
            contentY = cbDims.Y + dims.Margin.Top + dims.Border.Top + dims.Padding.Top;
        }

        dims.X = contentX;
        dims.Y = contentY;
        fixedChild.Dimensions = dims;

        LayoutBlockChildren(fixedChild, measurer, new BoxDimensions
        {
            X = contentX, Y = contentY, Width = contentWidth, Height = cbDims.Height
        });

        dims = fixedChild.Dimensions;
        float fixedH = LayoutHelper.ResolveHeight(style, cbDims.Height);
        if (!float.IsNaN(fixedH))
        {
            dims.Height = fixedH;
            if (style.BoxSizing == BoxSizingType.BorderBox)
            {
                dims.Height -= style.Padding.Top + style.Padding.Bottom
                             + style.BorderWidth.Top + style.BorderWidth.Bottom;
                if (dims.Height < 0) dims.Height = 0;
            }
        }

        fixedChild.Dimensions = dims;
    }

    private static void ApplyRelativeOffset(LayoutBox box)
    {
        if (box.Style.Position != PositionType.Relative && box.Style.Position != PositionType.Sticky) return;

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

        // Resolve height including deferred calc/percentage expressions
        float resolvedHeight = LayoutHelper.ResolveHeight(style, dims.Height);

        // If an explicit height is set, use it
        if (!float.IsNaN(resolvedHeight))
        {
            height = resolvedHeight;
            if (style.BoxSizing == BoxSizingType.BorderBox)
            {
                height -= style.Padding.Top + style.Padding.Bottom
                        + style.BorderWidth.Top + style.BorderWidth.Bottom;
                if (height < 0) height = 0;
            }
        }
        else if (TryGetImageIntrinsicHeight(box, dims.Width, out float intrinsicHeight))
        {
            // For <img> without explicit CSS height, use intrinsic height (aspect-ratio aware)
            height = intrinsicHeight;
        }
        else if (!float.IsNaN(style.AspectRatio) && style.AspectRatio > 0)
        {
            // aspect-ratio: height auto + aspect-ratio set => compute height from width
            height = dims.Width / style.AspectRatio;
        }

        // When overflow is hidden and explicit height is set, clamp to that height
        bool overflowClipsVertically = style.Overflow == OverflowType.Hidden
                                    || style.Overflow == OverflowType.Clip
                                    || style.OverflowY == OverflowType.Hidden
                                    || style.OverflowY == OverflowType.Clip;
        if (overflowClipsVertically && !float.IsNaN(resolvedHeight))
        {
            float explicitHeight = resolvedHeight;
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
            FontFamilies = parentStyle.FontFamilies,
            FontWeight = parentStyle.FontWeight,
            FontStyle = parentStyle.FontStyle,
            TextAlign = parentStyle.TextAlign,
            LineHeight = parentStyle.LineHeight,
            WhiteSpace = parentStyle.WhiteSpace,
            LetterSpacing = parentStyle.LetterSpacing,
            WordSpacing = parentStyle.WordSpacing,
            TextTransform = parentStyle.TextTransform,
            TextDecorationLine = parentStyle.TextDecorationLine,
            TextDecorationColor = parentStyle.TextDecorationColor,
            Visibility = parentStyle.Visibility,
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

    /// <summary>
    /// For &lt;img&gt; elements, tries to get intrinsic width from HTML attributes or decoded image metadata.
    /// Checks: CSS width (already handled), HTML width attribute, data-natural-width attribute.
    /// </summary>
    private static bool TryGetImageIntrinsicWidth(LayoutBox box, out float width)
        => ImageIntrinsicSizeHelper.TryGetWidth(box, out width);

    /// <summary>
    /// For &lt;img&gt; elements, tries to get intrinsic height, preserving aspect ratio if only width is known.
    /// </summary>
    private static bool TryGetImageIntrinsicHeight(LayoutBox box, float currentWidth, out float height)
        => ImageIntrinsicSizeHelper.TryGetHeight(box, currentWidth, out height);
}
