using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Style;

namespace SuperRender.Renderer.Rendering.Layout;

internal static class FlexLayout
{
    public static void Layout(LayoutBox flexBox, BoxDimensions containingBlock, ITextMeasurer measurer)
    {
        CalculateFlexContainerWidth(flexBox, containingBlock);
        CalculateFlexContainerPosition(flexBox, containingBlock);

        var style = flexBox.Style;
        var dims = flexBox.Dimensions;
        bool isRow = style.FlexDirection is FlexDirectionType.Row or FlexDirectionType.RowReverse;
        bool isReverse = style.FlexDirection is FlexDirectionType.RowReverse or FlexDirectionType.ColumnReverse;
        bool isWrap = style.FlexWrap != FlexWrapType.Nowrap;

        float mainGap = isRow
            ? (float.IsNaN(style.ColumnGap) ? style.Gap : style.ColumnGap)
            : (float.IsNaN(style.RowGap) ? style.Gap : style.RowGap);
        float crossGap = isRow
            ? (float.IsNaN(style.RowGap) ? style.Gap : style.RowGap)
            : (float.IsNaN(style.ColumnGap) ? style.Gap : style.ColumnGap);

        float resolvedContainerHeight = LayoutHelper.ResolveHeight(style, 0);
        float availableMain = isRow ? dims.Width : (!float.IsNaN(resolvedContainerHeight) ? resolvedContainerHeight : float.PositiveInfinity);
        float containerCrossSize = isRow ? resolvedContainerHeight : dims.Width;

        // Phase 1: Collect items and compute hypothetical sizes
        var flexItems = CollectFlexItems(flexBox);
        var hypotheticalMainSizes = new float[flexItems.Count];
        var hypotheticalCrossSizes = new float[flexItems.Count];
        ComputeHypotheticalSizes(flexItems, hypotheticalMainSizes, hypotheticalCrossSizes,
            isRow, dims, availableMain, measurer);

        // Phase 2: Wrap items into lines
        var lines = WrapIntoLines(flexItems, hypotheticalMainSizes, isRow, isWrap,
            availableMain, mainGap);

        // Phase 3: Resolve sizes, position items, and lay out children
        float crossCursor = isRow ? dims.Y : dims.X;
        PositionAllLines(lines, flexItems, hypotheticalMainSizes, hypotheticalCrossSizes,
            style, dims, isRow, isReverse, isWrap, availableMain, containerCrossSize,
            mainGap, crossGap, ref crossCursor, measurer);

        // Calculate flex container height
        CalculateFlexContainerHeight(flexBox, lines, crossGap, isRow);
    }

    private static List<LayoutBox> CollectFlexItems(LayoutBox flexBox)
    {
        var flexItems = new List<LayoutBox>();
        foreach (var child in flexBox.Children)
        {
            if (child.Style.Position == PositionType.Absolute)
                continue;
            flexItems.Add(child);
        }
        return flexItems;
    }

    private static void ComputeHypotheticalSizes(
        List<LayoutBox> flexItems, float[] hypotheticalMainSizes, float[] hypotheticalCrossSizes,
        bool isRow, BoxDimensions dims, float availableMain, ITextMeasurer measurer)
    {
        for (int i = 0; i < flexItems.Count; i++)
        {
            var item = flexItems[i];
            var itemStyle = item.Style;
            float basis = itemStyle.FlexBasis;

            if (float.IsNaN(basis))
            {
                basis = isRow
                    ? LayoutHelper.ResolveWidth(itemStyle, dims.Width)
                    : LayoutHelper.ResolveHeight(itemStyle, availableMain);
            }

            if (float.IsNaN(basis))
            {
                float tempWidth = isRow ? dims.Width : dims.Width;
                var intrinsicSize = ComputeIntrinsicSize(item, tempWidth, dims.Height, measurer);
                basis = isRow ? intrinsicSize.Width : intrinsicSize.Height;

                float crossSize = isRow
                    ? LayoutHelper.ResolveHeight(itemStyle, 0)
                    : LayoutHelper.ResolveWidth(itemStyle, dims.Width);
                if (float.IsNaN(crossSize))
                    crossSize = isRow ? intrinsicSize.Height : intrinsicSize.Width;
                hypotheticalCrossSizes[i] = crossSize;
            }
            else
            {
                if (isRow)
                {
                    float crossSize = itemStyle.Height;
                    if (float.IsNaN(crossSize))
                    {
                        var intrinsic = ComputeIntrinsicSize(item, basis, dims.Height, measurer);
                        crossSize = intrinsic.Height;
                    }
                    hypotheticalCrossSizes[i] = crossSize;
                }
                else
                {
                    float crossSize = itemStyle.Width;
                    if (float.IsNaN(crossSize))
                    {
                        var intrinsic = ComputeIntrinsicSize(item, dims.Width, basis, measurer);
                        crossSize = intrinsic.Width;
                    }
                    hypotheticalCrossSizes[i] = crossSize;
                }
            }

            hypotheticalMainSizes[i] = basis;
        }
    }

    private static List<FlexLine> WrapIntoLines(
        List<LayoutBox> flexItems, float[] hypotheticalMainSizes,
        bool isRow, bool isWrap, float availableMain, float mainGap)
    {
        var lines = new List<FlexLine>();
        if (isWrap && flexItems.Count > 0 && !float.IsPositiveInfinity(availableMain))
        {
            var currentLine = new FlexLine();
            float lineMainUsed = 0;

            for (int i = 0; i < flexItems.Count; i++)
            {
                float itemMain = hypotheticalMainSizes[i] + GetItemMainMargin(flexItems[i], isRow);
                float gapSize = currentLine.Items.Count > 0 ? mainGap : 0;

                if (currentLine.Items.Count > 0 && lineMainUsed + gapSize + itemMain > availableMain)
                {
                    lines.Add(currentLine);
                    currentLine = new FlexLine();
                    lineMainUsed = 0;
                    gapSize = 0;
                }

                currentLine.Items.Add(i);
                lineMainUsed += gapSize + itemMain;
            }

            if (currentLine.Items.Count > 0)
                lines.Add(currentLine);
        }
        else
        {
            var singleLine = new FlexLine();
            for (int i = 0; i < flexItems.Count; i++)
                singleLine.Items.Add(i);
            lines.Add(singleLine);
        }
        return lines;
    }

    private static void PositionAllLines(
        List<FlexLine> lines, List<LayoutBox> flexItems,
        float[] hypotheticalMainSizes, float[] hypotheticalCrossSizes,
        ComputedStyle style, BoxDimensions dims,
        bool isRow, bool isReverse, bool isWrap,
        float availableMain, float containerCrossSize,
        float mainGap, float crossGap,
        ref float crossCursor, ITextMeasurer measurer)
    {
        for (int lineIdx = 0; lineIdx < lines.Count; lineIdx++)
        {
            var line = lines[lineIdx];
            if (line.Items.Count == 0) continue;

            // Resolve final main sizes via flex-grow/shrink
            var finalMainSizes = ResolveFlexLineSizes(line, flexItems,
                hypotheticalMainSizes, isRow, availableMain, mainGap);

            // Determine cross size for this line
            float lineCrossSize = ComputeLineCrossSize(line, flexItems,
                hypotheticalCrossSizes, isRow);

            if (!isWrap && lines.Count == 1 && !float.IsNaN(containerCrossSize))
                lineCrossSize = Math.Max(lineCrossSize, containerCrossSize);

            // Apply align-items: stretch
            ApplyStretchAlignment(line, flexItems, hypotheticalCrossSizes,
                style, isRow, lineCrossSize);

            // Lay out each item with final size
            foreach (int idx in line.Items)
            {
                var item = flexItems[idx];
                float mainSize = finalMainSizes[idx];
                float crossSize = hypotheticalCrossSizes[idx];
                float itemWidth = isRow ? mainSize : crossSize;
                float itemHeight = isRow ? crossSize : mainSize;
                LayoutFlexItem(item, itemWidth, itemHeight, measurer);
            }

            // Position items along main and cross axes
            PositionLineItems(line, flexItems, style, dims, isRow, isReverse,
                availableMain, mainGap, lineCrossSize, crossCursor, measurer);

            line.CrossSize = lineCrossSize;
            crossCursor += lineCrossSize;
            if (lineIdx < lines.Count - 1)
                crossCursor += crossGap;
        }
    }

    private static float[] ResolveFlexLineSizes(
        FlexLine line, List<LayoutBox> flexItems,
        float[] hypotheticalMainSizes, bool isRow,
        float availableMain, float mainGap)
    {
        float totalHypothetical = 0;
        float totalMainMargin = 0;
        for (int j = 0; j < line.Items.Count; j++)
        {
            int idx = line.Items[j];
            totalHypothetical += hypotheticalMainSizes[idx];
            totalMainMargin += GetItemMainMargin(flexItems[idx], isRow);
        }

        float totalGaps = mainGap * Math.Max(0, line.Items.Count - 1);
        float remainingSpace = availableMain - totalHypothetical - totalMainMargin - totalGaps;

        var finalMainSizes = new float[hypotheticalMainSizes.Length];
        for (int j = 0; j < line.Items.Count; j++)
        {
            int idx = line.Items[j];
            finalMainSizes[idx] = hypotheticalMainSizes[idx];
        }

        if (remainingSpace > 0)
        {
            float totalGrow = 0;
            foreach (int idx in line.Items)
                totalGrow += flexItems[idx].Style.FlexGrow;

            if (totalGrow > 0)
            {
                foreach (int idx in line.Items)
                {
                    float grow = flexItems[idx].Style.FlexGrow;
                    finalMainSizes[idx] += remainingSpace * (grow / totalGrow);
                }
            }
        }
        else if (remainingSpace < 0)
        {
            float totalShrink = 0;
            foreach (int idx in line.Items)
                totalShrink += flexItems[idx].Style.FlexShrink * hypotheticalMainSizes[idx];

            if (totalShrink > 0)
            {
                foreach (int idx in line.Items)
                {
                    float shrink = flexItems[idx].Style.FlexShrink * hypotheticalMainSizes[idx];
                    float reduction = (-remainingSpace) * (shrink / totalShrink);
                    finalMainSizes[idx] = Math.Max(0, finalMainSizes[idx] - reduction);
                }
            }
        }

        return finalMainSizes;
    }

    private static float ComputeLineCrossSize(
        FlexLine line, List<LayoutBox> flexItems,
        float[] hypotheticalCrossSizes, bool isRow)
    {
        float lineCrossSize = 0;
        foreach (int idx in line.Items)
        {
            float itemCross = hypotheticalCrossSizes[idx] + GetItemCrossMargin(flexItems[idx], isRow);
            lineCrossSize = Math.Max(lineCrossSize, itemCross);
        }
        return lineCrossSize;
    }

    private static void ApplyStretchAlignment(
        FlexLine line, List<LayoutBox> flexItems,
        float[] hypotheticalCrossSizes, ComputedStyle style,
        bool isRow, float lineCrossSize)
    {
        foreach (int idx in line.Items)
        {
            var itemStyle = flexItems[idx].Style;
            var effectiveAlign = itemStyle.AlignSelf != AlignSelfType.Auto
                ? MapAlignSelf(itemStyle.AlignSelf)
                : style.AlignItems;

            if (effectiveAlign == AlignItemsType.Stretch)
            {
                float explicitCross = isRow ? itemStyle.Height : itemStyle.Width;
                if (float.IsNaN(explicitCross))
                {
                    float crossMargin = GetItemCrossMargin(flexItems[idx], isRow);
                    hypotheticalCrossSizes[idx] = lineCrossSize - crossMargin;
                }
            }
        }
    }

    private static void PositionLineItems(
        FlexLine line, List<LayoutBox> flexItems,
        ComputedStyle style, BoxDimensions dims,
        bool isRow, bool isReverse,
        float availableMain, float mainGap,
        float lineCrossSize, float crossCursor,
        ITextMeasurer measurer)
    {
        float totalGaps = mainGap * Math.Max(0, line.Items.Count - 1);
        float usedMain = 0;
        foreach (int idx in line.Items)
            usedMain += GetItemMainOuterSize(flexItems[idx], isRow);
        usedMain += totalGaps;

        float freeSpace = Math.Max(0,
            (float.IsPositiveInfinity(availableMain) ? usedMain : availableMain) - usedMain);
        int itemCount = line.Items.Count;

        float mainStart = isRow ? dims.X : dims.Y;
        ComputeJustifyPositions(style.JustifyContent, isReverse, freeSpace, itemCount,
            mainStart, availableMain, out float mainCursor, out float mainSpacing, out float startPad);

        mainCursor += startPad;

        var orderedItems = new List<int>(line.Items);
        if (isReverse)
            orderedItems.Reverse();

        for (int j = 0; j < orderedItems.Count; j++)
        {
            int idx = orderedItems[j];
            var item = flexItems[idx];
            var itemDims = item.Dimensions;

            float itemMainMarginBefore = isRow ? itemDims.Margin.Left : itemDims.Margin.Top;
            float itemMainMarginAfter = isRow ? itemDims.Margin.Right : itemDims.Margin.Bottom;

            mainCursor += itemMainMarginBefore;

            if (isRow)
                itemDims.X = mainCursor + itemDims.Border.Left + itemDims.Padding.Left;
            else
                itemDims.Y = mainCursor + itemDims.Border.Top + itemDims.Padding.Top;

            // Cross axis alignment
            var itemStyle = item.Style;
            var effectiveAlign = itemStyle.AlignSelf != AlignSelfType.Auto
                ? MapAlignSelf(itemStyle.AlignSelf)
                : style.AlignItems;

            float itemCrossOuter = isRow
                ? itemDims.Height + itemDims.VerticalEdge
                : itemDims.Width + itemDims.HorizontalEdge;

            float crossOffset = ComputeCrossOffset(effectiveAlign, lineCrossSize, itemCrossOuter);

            if (isRow)
                itemDims.Y = crossCursor + crossOffset + itemDims.TopEdge;
            else
                itemDims.X = crossCursor + crossOffset + itemDims.LeftEdge;

            item.Dimensions = itemDims;
            RelayoutFlexItemChildren(item, measurer);

            float itemMainSize = isRow
                ? itemDims.Width + itemDims.Border.Left + itemDims.Border.Right
                  + itemDims.Padding.Left + itemDims.Padding.Right
                : itemDims.Height + itemDims.Border.Top + itemDims.Border.Bottom
                  + itemDims.Padding.Top + itemDims.Padding.Bottom;

            mainCursor += itemMainSize + itemMainMarginAfter;

            if (j < orderedItems.Count - 1)
                mainCursor += mainGap + mainSpacing;
        }
    }

    private static void ComputeJustifyPositions(
        JustifyContentType justify, bool isReverse, float freeSpace, int itemCount,
        float mainStart, float availableMain, out float mainCursor, out float mainSpacing, out float startPad)
    {
        mainCursor = mainStart;
        mainSpacing = 0;
        startPad = 0;

        switch (justify)
        {
            case JustifyContentType.FlexStart:
                if (isReverse)
                    startPad = freeSpace;
                break;
            case JustifyContentType.FlexEnd:
                if (!isReverse)
                    startPad = freeSpace;
                break;
            case JustifyContentType.Center:
                startPad = freeSpace / 2f;
                break;
            case JustifyContentType.SpaceBetween:
                if (itemCount > 1)
                    mainSpacing = freeSpace / (itemCount - 1);
                break;
            case JustifyContentType.SpaceAround:
                if (itemCount > 0)
                {
                    float around = freeSpace / itemCount;
                    startPad = around / 2f;
                    mainSpacing = around;
                }
                break;
            case JustifyContentType.SpaceEvenly:
                if (itemCount > 0)
                {
                    float even = freeSpace / (itemCount + 1);
                    startPad = even;
                    mainSpacing = even;
                }
                break;
        }
    }

    private static float ComputeCrossOffset(AlignItemsType align, float lineCrossSize, float itemCrossOuter)
    {
        return align switch
        {
            AlignItemsType.FlexStart or AlignItemsType.Baseline => 0,
            AlignItemsType.FlexEnd => lineCrossSize - itemCrossOuter,
            AlignItemsType.Center => (lineCrossSize - itemCrossOuter) / 2f,
            AlignItemsType.Stretch => 0, // already stretched
            _ => 0
        };
    }

    private static AlignItemsType MapAlignSelf(AlignSelfType selfAlign)
    {
        return selfAlign switch
        {
            AlignSelfType.Stretch => AlignItemsType.Stretch,
            AlignSelfType.FlexStart => AlignItemsType.FlexStart,
            AlignSelfType.FlexEnd => AlignItemsType.FlexEnd,
            AlignSelfType.Center => AlignItemsType.Center,
            AlignSelfType.Baseline => AlignItemsType.Baseline,
            _ => AlignItemsType.Stretch
        };
    }

    private static void CalculateFlexContainerWidth(LayoutBox box, BoxDimensions containingBlock)
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

        float resolvedWidth = LayoutHelper.ResolveWidth(style, containerWidth);
        float contentWidth;
        if (!float.IsNaN(resolvedWidth))
        {
            contentWidth = resolvedWidth;
            if (style.BoxSizing == BoxSizingType.BorderBox)
            {
                contentWidth -= paddingLeft + paddingRight + borderLeft + borderRight;
                if (contentWidth < 0) contentWidth = 0;
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

    private static void CalculateFlexContainerPosition(LayoutBox box, BoxDimensions containingBlock)
    {
        var dims = box.Dimensions;
        dims.X = containingBlock.X + dims.Margin.Left + dims.Border.Left + dims.Padding.Left;
        dims.Y += dims.Margin.Top + dims.Border.Top + dims.Padding.Top;
        box.Dimensions = dims;
    }

    private static void CalculateFlexContainerHeight(LayoutBox box, List<FlexLine> lines, float crossGap, bool isRow)
    {
        var style = box.Style;
        var dims = box.Dimensions;

        float resolvedHeight = LayoutHelper.ResolveHeight(style, dims.Height);
        if (!float.IsNaN(resolvedHeight))
        {
            float height = resolvedHeight;
            if (style.BoxSizing == BoxSizingType.BorderBox)
            {
                height -= style.Padding.Top + style.Padding.Bottom
                        + style.BorderWidth.Top + style.BorderWidth.Bottom;
                if (height < 0) height = 0;
            }
            dims.Height = height;
        }
        else
        {
            if (isRow)
            {
                float totalCross = 0;
                for (int i = 0; i < lines.Count; i++)
                {
                    totalCross += lines[i].CrossSize;
                    if (i < lines.Count - 1) totalCross += crossGap;
                }
                dims.Height = totalCross;
            }
            else
            {
                // Column direction: height is the max main size used
                float maxMain = 0;
                foreach (var child in box.Children)
                {
                    if (child.Style.Position == PositionType.Absolute) continue;
                    float bottom = child.Dimensions.MarginRect.Bottom - dims.Y;
                    maxMain = Math.Max(maxMain, bottom);
                }
                dims.Height = maxMain;
            }
        }

        box.Dimensions = dims;
    }

    private static (float Width, float Height) ComputeIntrinsicSize(
        LayoutBox item, float availableWidth, float availableHeight, ITextMeasurer measurer)
    {
        // For replaced elements (e.g. <img>) with intrinsic dimensions from
        // HTML attributes or decoded image data, use those directly.
        if (item.DomNode is Document.Dom.Element imgEl && imgEl.TagName == HtmlTagNames.Img)
        {
            float imgW = 0, imgH = 0;
            var widthAttr = imgEl.GetAttribute(HtmlAttributeNames.Width) ?? imgEl.GetAttribute(HtmlAttributeNames.DataNaturalWidth);
            var heightAttr = imgEl.GetAttribute(HtmlAttributeNames.Height) ?? imgEl.GetAttribute(HtmlAttributeNames.DataNaturalHeight);
            if (widthAttr != null)
                float.TryParse(widthAttr, System.Globalization.CultureInfo.InvariantCulture, out imgW);
            if (heightAttr != null)
                float.TryParse(heightAttr, System.Globalization.CultureInfo.InvariantCulture, out imgH);
            if (imgW > 0 || imgH > 0)
            {
                // Preserve aspect ratio if only one dimension specified
                var naturalW = imgEl.GetAttribute(HtmlAttributeNames.DataNaturalWidth);
                var naturalH = imgEl.GetAttribute(HtmlAttributeNames.DataNaturalHeight);
                if (imgW > 0 && imgH == 0 && naturalW != null && naturalH != null
                    && float.TryParse(naturalW, System.Globalization.CultureInfo.InvariantCulture, out float nw)
                    && float.TryParse(naturalH, System.Globalization.CultureInfo.InvariantCulture, out float nh)
                    && nw > 0)
                {
                    imgH = imgW * nh / nw;
                }
                if (imgH > 0 && imgW == 0 && naturalW != null && naturalH != null
                    && float.TryParse(naturalW, System.Globalization.CultureInfo.InvariantCulture, out float nw2)
                    && float.TryParse(naturalH, System.Globalization.CultureInfo.InvariantCulture, out float nh2)
                    && nh2 > 0)
                {
                    imgW = imgH * nw2 / nh2;
                }
                return (imgW, imgH);
            }
        }

        // Lay out item with shrink-to-fit semantics: use available width as max,
        // then measure the actual content width used
        var tempContainer = new BoxDimensions
        {
            X = 0,
            Y = 0,
            Width = availableWidth,
            Height = availableHeight
        };

        var itemDims = item.Dimensions;
        itemDims.X = 0;
        itemDims.Y = 0;
        item.Dimensions = itemDims;

        LayoutFlexItem(item, availableWidth, float.NaN, measurer);

        var resultDims = item.Dimensions;

        // Compute shrink-to-fit width: measure actual content extent
        float contentRight = ComputeContentRight(item);
        float shrunkWidth = Math.Max(0, contentRight - resultDims.X);
        // Use the smaller of the shrunk width and the laid-out width
        float intrinsicWidth = Math.Min(shrunkWidth, resultDims.Width);

        return (intrinsicWidth, resultDims.Height);
    }

    private static float ComputeContentRight(LayoutBox box)
    {
        float maxRight = box.Dimensions.X;

        if (box.TextRuns is not null)
        {
            foreach (var run in box.TextRuns)
            {
                maxRight = Math.Max(maxRight, run.X + run.Width);
            }
        }

        foreach (var child in box.Children)
        {
            float childRight = child.Dimensions.MarginRect.Right;
            maxRight = Math.Max(maxRight, childRight);
            maxRight = Math.Max(maxRight, ComputeContentRight(child));
        }

        return maxRight;
    }

    private static void LayoutFlexItem(LayoutBox item, float width, float height, ITextMeasurer measurer)
    {
        var itemStyle = item.Style;

        var marginLeft = float.IsNaN(itemStyle.Margin.Left) ? 0 : itemStyle.Margin.Left;
        var marginRight = float.IsNaN(itemStyle.Margin.Right) ? 0 : itemStyle.Margin.Right;
        var marginTop = float.IsNaN(itemStyle.Margin.Top) ? 0 : itemStyle.Margin.Top;
        var marginBottom = float.IsNaN(itemStyle.Margin.Bottom) ? 0 : itemStyle.Margin.Bottom;

        var dims = item.Dimensions;
        dims.Width = width;
        dims.Padding = itemStyle.Padding;
        dims.Border = itemStyle.BorderWidth;
        dims.Margin = new EdgeSizes(marginTop, marginRight, marginBottom, marginLeft);
        dims.X = dims.Margin.Left + dims.Border.Left + dims.Padding.Left;
        dims.Y = dims.Margin.Top + dims.Border.Top + dims.Padding.Top;
        item.Dimensions = dims;

        // Layout children
        var childContainer = new BoxDimensions
        {
            X = dims.X,
            Y = dims.Y,
            Width = dims.Width,
            Height = float.IsNaN(height) ? 0 : height,
        };

        if (item.BoxType == LayoutBoxType.FlexContainer)
        {
            FlexLayout.Layout(item, childContainer, measurer);
        }
        else
        {
            BlockLayout.LayoutBlockChildren(item, measurer, childContainer);
        }

        dims = item.Dimensions;
        if (!float.IsNaN(height))
        {
            dims.Height = height;
        }
        // else Height was already set by LayoutBlockChildren

        item.Dimensions = dims;
    }

    private static void RelayoutFlexItemChildren(LayoutBox item, ITextMeasurer measurer)
    {
        var dims = item.Dimensions;
        float savedHeight = dims.Height;
        float savedWidth = dims.Width;

        var childContainer = new BoxDimensions
        {
            X = dims.X,
            Y = dims.Y,
            Width = dims.Width,
            Height = dims.Height,
        };

        if (item.BoxType == LayoutBoxType.FlexContainer)
        {
            FlexLayout.Layout(item, childContainer, measurer);
            dims = item.Dimensions;
            dims.Width = savedWidth;
            dims.Height = savedHeight;
            item.Dimensions = dims;
        }
        else
        {
            // Clear existing text runs so they get regenerated at the new position
            ClearTextRuns(item);
            BlockLayout.LayoutBlockChildren(item, measurer, childContainer);

            // Always preserve the flex-assigned dimensions
            var newDims = item.Dimensions;
            newDims.Height = savedHeight;
            newDims.Width = savedWidth;
            item.Dimensions = newDims;
        }
    }

    private static void ClearTextRuns(LayoutBox box)
    {
        box.TextRuns = null;
        foreach (var child in box.Children)
            ClearTextRuns(child);
    }

    private static float GetItemMainMargin(LayoutBox item, bool isRow)
    {
        var s = item.Style;
        if (isRow)
        {
            float ml = float.IsNaN(s.Margin.Left) ? 0 : s.Margin.Left;
            float mr = float.IsNaN(s.Margin.Right) ? 0 : s.Margin.Right;
            return ml + mr + s.BorderWidth.Left + s.BorderWidth.Right + s.Padding.Left + s.Padding.Right;
        }
        else
        {
            float mt = float.IsNaN(s.Margin.Top) ? 0 : s.Margin.Top;
            float mb = float.IsNaN(s.Margin.Bottom) ? 0 : s.Margin.Bottom;
            return mt + mb + s.BorderWidth.Top + s.BorderWidth.Bottom + s.Padding.Top + s.Padding.Bottom;
        }
    }

    private static float GetItemCrossMargin(LayoutBox item, bool isRow)
    {
        var s = item.Style;
        if (isRow)
        {
            float mt = float.IsNaN(s.Margin.Top) ? 0 : s.Margin.Top;
            float mb = float.IsNaN(s.Margin.Bottom) ? 0 : s.Margin.Bottom;
            return mt + mb + s.BorderWidth.Top + s.BorderWidth.Bottom + s.Padding.Top + s.Padding.Bottom;
        }
        else
        {
            float ml = float.IsNaN(s.Margin.Left) ? 0 : s.Margin.Left;
            float mr = float.IsNaN(s.Margin.Right) ? 0 : s.Margin.Right;
            return ml + mr + s.BorderWidth.Left + s.BorderWidth.Right + s.Padding.Left + s.Padding.Right;
        }
    }

    private static float GetItemMainOuterSize(LayoutBox item, bool isRow)
    {
        var d = item.Dimensions;
        if (isRow)
        {
            return d.Width + d.Margin.Left + d.Margin.Right
                   + d.Border.Left + d.Border.Right
                   + d.Padding.Left + d.Padding.Right;
        }
        else
        {
            return d.Height + d.Margin.Top + d.Margin.Bottom
                   + d.Border.Top + d.Border.Bottom
                   + d.Padding.Top + d.Padding.Bottom;
        }
    }

    private sealed class FlexLine
    {
        public List<int> Items { get; } = [];
        public float CrossSize { get; set; }
    }
}
