using SuperRender.Core.Style;

namespace SuperRender.Core.Layout;

internal static class InlineLayout
{
    public static void Layout(LayoutBox anonymousBlock, BoxDimensions containingBlock, ITextMeasurer measurer)
    {
        var dims = anonymousBlock.Dimensions;
        dims.X = containingBlock.X;
        dims.Width = containingBlock.Width;

        float cursorX = dims.X;
        float cursorY = dims.Y;
        float lineHeight = 0;      // line-height based (for inter-line spacing)
        float visualLineHeight = 0; // max of font sizes + inline-block heights (for alignment)
        float availableWidth = dims.Width;
        var whiteSpace = anonymousBlock.Style.WhiteSpace;

        var currentLineRuns = new List<TextRun>();
        var currentLineBoxes = new List<LayoutBox>();

        foreach (var child in anonymousBlock.Children)
        {
            if (child.BoxType == LayoutBoxType.InlineBlock)
            {
                LayoutInlineBlockChild(child, ref cursorX, ref cursorY, ref lineHeight,
                    dims.X, availableWidth, measurer, currentLineRuns, currentLineBoxes, whiteSpace);
                // Inline-block outer height contributes to both lineHeight and visual height
                float outerH = child.Dimensions.MarginRect.Height;
                visualLineHeight = Math.Max(visualLineHeight, outerH);
                currentLineBoxes.Add(child);
            }
            else if (child.TextContent != null)
            {
                var style = child.Style;
                var fontSize = style.FontSize;
                var lh = measurer.GetLineHeight(fontSize, style.LineHeight);
                var ws = style.WhiteSpace;
                var words = SplitIntoWords(child.TextContent, ws);

                child.TextRuns = [];

                foreach (var word in words)
                {
                    if (ws == WhiteSpaceType.Pre || ws == WhiteSpaceType.PreWrap || ws == WhiteSpaceType.PreLine)
                    {
                        // Handle newlines in pre modes
                        if (word == "\n")
                        {
                            AlignLine(currentLineRuns, dims.X, availableWidth, child.Style.TextAlign);
                            AlignLineBoxes(currentLineBoxes, cursorY, visualLineHeight);
                            cursorY += lineHeight > 0 ? lineHeight : lh;
                            cursorX = dims.X;
                            lineHeight = 0;
                            visualLineHeight = 0;
                            currentLineRuns = [];
                            currentLineBoxes = [];
                            continue;
                        }
                    }

                    var wordWidth = measurer.MeasureWidth(word, fontSize, style.FontFamily, style.FontWeight);

                    // Check if we need to wrap (not in nowrap or pre mode without pre-wrap)
                    bool canWrap = ws != WhiteSpaceType.Nowrap && ws != WhiteSpaceType.Pre;
                    if (canWrap && cursorX - dims.X + wordWidth > availableWidth && cursorX > dims.X)
                    {
                        // New line
                        AlignLine(currentLineRuns, dims.X, availableWidth, child.Style.TextAlign);
                        AlignLineBoxes(currentLineBoxes, cursorY, visualLineHeight);
                        cursorY += lineHeight;
                        cursorX = dims.X;
                        lineHeight = 0;
                        visualLineHeight = 0;
                        currentLineRuns = [];
                        currentLineBoxes = [];
                    }

                    var run = new TextRun
                    {
                        Text = word,
                        X = cursorX,
                        Y = cursorY,
                        Width = wordWidth,
                        Height = fontSize,
                        Style = style,
                    };

                    child.TextRuns.Add(run);
                    currentLineRuns.Add(run);

                    cursorX += wordWidth;
                    lineHeight = Math.Max(lineHeight, lh);
                    visualLineHeight = Math.Max(visualLineHeight, fontSize);
                }
            }
            else
            {
                // Inline element (like <strong>, <em>)
                LayoutInlineElement(child, ref cursorX, ref cursorY, ref lineHeight,
                    dims.X, availableWidth, measurer, currentLineRuns);
            }
        }

        // Finish last line
        AlignLineBoxes(currentLineBoxes, cursorY, visualLineHeight);
        if (lineHeight > 0)
        {
            // Use visual height for the last line so block content height
            // is tight around characters (line-height only spaces between lines).
            float lastLineHeight = visualLineHeight > 0 ? visualLineHeight : lineHeight;
            cursorY += lastLineHeight;
        }

        dims.Height = cursorY - dims.Y;
        anonymousBlock.Dimensions = dims;
    }

    /// <summary>
    /// Vertically aligns inline-block boxes on a line. Moves shorter boxes down
    /// so their bottom edges align with the line bottom (baseline approximation).
    /// </summary>
    private static void AlignLineBoxes(List<LayoutBox> boxes, float lineY, float lineHeight)
    {
        if (boxes.Count == 0 || lineHeight <= 0) return;

        foreach (var box in boxes)
        {
            float boxHeight = box.Dimensions.MarginRect.Height;
            if (boxHeight < lineHeight)
            {
                float deltaY = lineHeight - boxHeight;
                var d = box.Dimensions;
                d.Y += deltaY;
                box.Dimensions = d;
                BlockLayout.OffsetSubtree(box, 0, deltaY);
            }
        }
    }

    private static void LayoutInlineElement(LayoutBox box, ref float cursorX, ref float cursorY,
        ref float lineHeight, float lineStartX, float availableWidth,
        ITextMeasurer measurer, List<TextRun> currentLineRuns)
    {
        var style = box.Style;

        // Record the starting position for this inline element
        float startX = cursorX;
        float startY = cursorY;

        cursorX += style.Margin.Left + style.BorderWidth.Left + style.Padding.Left;

        box.TextRuns = [];
        float maxRunHeight = 0;

        foreach (var child in box.Children)
        {
            if (child.BoxType == LayoutBoxType.InlineBlock)
            {
                LayoutInlineBlockChild(child, ref cursorX, ref cursorY, ref lineHeight,
                    lineStartX, availableWidth, measurer, currentLineRuns, [], style.WhiteSpace);
            }
            else if (child.TextContent != null)
            {
                var fontSize = style.FontSize;
                var lh = measurer.GetLineHeight(fontSize, style.LineHeight);
                var ws = style.WhiteSpace;
                var words = SplitIntoWords(child.TextContent, ws);

                child.TextRuns = [];

                foreach (var word in words)
                {
                    if ((ws == WhiteSpaceType.Pre || ws == WhiteSpaceType.PreWrap || ws == WhiteSpaceType.PreLine)
                        && word == "\n")
                    {
                        AlignLine(currentLineRuns, lineStartX, availableWidth, style.TextAlign);
                        cursorY += lineHeight > 0 ? lineHeight : lh;
                        cursorX = lineStartX;
                        lineHeight = 0;
                        currentLineRuns = [];
                        continue;
                    }

                    var wordWidth = measurer.MeasureWidth(word, fontSize, style.FontFamily, style.FontWeight);

                    bool canWrap = ws != WhiteSpaceType.Nowrap && ws != WhiteSpaceType.Pre;
                    if (canWrap && cursorX - lineStartX + wordWidth > availableWidth && cursorX > lineStartX)
                    {
                        AlignLine(currentLineRuns, lineStartX, availableWidth, style.TextAlign);
                        cursorY += lineHeight;
                        cursorX = lineStartX;
                        lineHeight = 0;
                        currentLineRuns = [];
                    }

                    var run = new TextRun
                    {
                        Text = word,
                        X = cursorX,
                        Y = cursorY,
                        Width = wordWidth,
                        Height = fontSize,
                        Style = style,
                    };

                    child.TextRuns.Add(run);
                    box.TextRuns.Add(run);
                    currentLineRuns.Add(run);

                    cursorX += wordWidth;
                    lineHeight = Math.Max(lineHeight, lh);
                    maxRunHeight = Math.Max(maxRunHeight, fontSize);
                }
            }
        }

        cursorX += style.Padding.Right + style.BorderWidth.Right + style.Margin.Right;

        // Set full dimensions for hit-testing
        var dims = box.Dimensions;
        dims.X = startX;
        dims.Y = startY;
        dims.Width = cursorX - startX;
        dims.Height = maxRunHeight > 0 ? maxRunHeight : lineHeight;
        dims.Padding = style.Padding;
        dims.Border = style.BorderWidth;
        box.Dimensions = dims;
    }

    private static void LayoutInlineBlockChild(LayoutBox child, ref float cursorX, ref float cursorY,
        ref float lineHeight, float lineStartX, float availableWidth,
        ITextMeasurer measurer, List<TextRun> currentLineRuns, List<LayoutBox> currentLineBoxes,
        WhiteSpaceType parentWs)
    {
        var style = child.Style;
        var marginLeft = float.IsNaN(style.Margin.Left) ? 0 : style.Margin.Left;
        var marginRight = float.IsNaN(style.Margin.Right) ? 0 : style.Margin.Right;

        bool canWrap = parentWs != WhiteSpaceType.Nowrap && parentWs != WhiteSpaceType.Pre;

        if (!float.IsNaN(style.Width))
        {
            // Explicit width: check wrapping before layout
            float estimatedWidth = style.Width + marginLeft + marginRight
                               + style.Padding.Left + style.Padding.Right
                               + style.BorderWidth.Left + style.BorderWidth.Right;
            if (style.BoxSizing == BoxSizingType.BorderBox)
                estimatedWidth = style.Width + marginLeft + marginRight;

            if (canWrap && cursorX - lineStartX + estimatedWidth > availableWidth && cursorX > lineStartX)
            {
                AlignLine(currentLineRuns, lineStartX, availableWidth, child.Style.TextAlign);
                AlignLineBoxes(currentLineBoxes, cursorY, lineHeight);
                cursorY += lineHeight;
                cursorX = lineStartX;
                lineHeight = 0;
                currentLineRuns.Clear();
                currentLineBoxes.Clear();
            }
        }

        // Container width: available space (CalculateBlockWidth handles margin/border/padding subtraction)
        float containerWidth = !float.IsNaN(style.Width) ? style.Width
            : availableWidth - (cursorX - lineStartX);
        if (style.BoxSizing == BoxSizingType.BorderBox && !float.IsNaN(style.Width))
        {
            containerWidth = style.Width - style.Padding.Left - style.Padding.Right
                           - style.BorderWidth.Left - style.BorderWidth.Right;
            if (containerWidth < 0) containerWidth = 0;
        }

        var container = new BoxDimensions
        {
            X = cursorX,
            Y = cursorY,
            Width = containerWidth,
            Height = 0,
        };

        var childDims = child.Dimensions;
        childDims.Y = cursorY;
        child.Dimensions = childDims;

        BlockLayout.Layout(child, container, measurer);

        // For auto-width inline-blocks, shrink to content width (shrink-to-fit)
        if (float.IsNaN(style.Width))
        {
            float contentRight = ComputeContentRight(child);
            float contentX = child.Dimensions.X;
            float shrunkWidth = Math.Max(0, contentRight - contentX);
            var dims = child.Dimensions;
            dims.Width = shrunkWidth;
            child.Dimensions = dims;

            // Check wrapping AFTER shrink-to-fit using actual width
            float actualOuterWidth = child.Dimensions.MarginRect.Width;
            if (canWrap && cursorX - lineStartX + actualOuterWidth > availableWidth
                && cursorX > lineStartX)
            {
                // Wrap to next line and re-layout at the new position
                AlignLine(currentLineRuns, lineStartX, availableWidth, child.Style.TextAlign);
                AlignLineBoxes(currentLineBoxes, cursorY, lineHeight);
                cursorY += lineHeight;
                cursorX = lineStartX;
                lineHeight = 0;
                currentLineRuns.Clear();
                currentLineBoxes.Clear();

                containerWidth = availableWidth;
                container = new BoxDimensions
                {
                    X = cursorX,
                    Y = cursorY,
                    Width = containerWidth,
                    Height = 0,
                };
                childDims = child.Dimensions;
                childDims.Y = cursorY;
                child.Dimensions = childDims;
                BlockLayout.Layout(child, container, measurer);

                // Shrink again after re-layout
                contentRight = ComputeContentRight(child);
                contentX = child.Dimensions.X;
                shrunkWidth = Math.Max(0, contentRight - contentX);
                dims = child.Dimensions;
                dims.Width = shrunkWidth;
                child.Dimensions = dims;
            }
        }

        // The inline-block participates in inline flow with its outer dimensions
        float outerHeight = child.Dimensions.MarginRect.Height;
        lineHeight = Math.Max(lineHeight, outerHeight);

        // Advance cursor past the full outer width (margin box)
        cursorX += child.Dimensions.MarginRect.Width;
    }

    public static void LayoutSingleInline(LayoutBox box, BoxDimensions containingBlock, ITextMeasurer measurer)
    {
        if (box.TextContent != null)
        {
            var style = box.Style;
            var fontSize = style.FontSize;
            var lh = measurer.GetLineHeight(fontSize, style.LineHeight);
            var ws = style.WhiteSpace;

            box.TextRuns = [];

            float cursorX = containingBlock.X;
            float cursorY = containingBlock.Y;
            float lineHeight = 0;
            var words = SplitIntoWords(box.TextContent, ws);

            foreach (var word in words)
            {
                if ((ws == WhiteSpaceType.Pre || ws == WhiteSpaceType.PreWrap || ws == WhiteSpaceType.PreLine)
                    && word == "\n")
                {
                    cursorY += lineHeight > 0 ? lineHeight : lh;
                    cursorX = containingBlock.X;
                    lineHeight = 0;
                    continue;
                }

                var wordWidth = measurer.MeasureWidth(word, fontSize, style.FontFamily, style.FontWeight);

                bool canWrap = ws != WhiteSpaceType.Nowrap && ws != WhiteSpaceType.Pre;
                if (canWrap && cursorX - containingBlock.X + wordWidth > containingBlock.Width && cursorX > containingBlock.X)
                {
                    cursorY += lineHeight;
                    cursorX = containingBlock.X;
                    lineHeight = 0;
                }

                box.TextRuns.Add(new TextRun
                {
                    Text = word,
                    X = cursorX,
                    Y = cursorY,
                    Width = wordWidth,
                    Height = fontSize,
                    Style = style,
                });

                cursorX += wordWidth;
                lineHeight = Math.Max(lineHeight, lh);
            }

            if (lineHeight > 0) cursorY += fontSize;

            var dims = box.Dimensions;
            dims.X = containingBlock.X;
            dims.Y = containingBlock.Y;
            dims.Width = containingBlock.Width;
            dims.Height = cursorY - containingBlock.Y;
            box.Dimensions = dims;
        }
    }

    internal static List<string> SplitIntoWords(string text, WhiteSpaceType whiteSpace)
    {
        if (whiteSpace == WhiteSpaceType.Pre || whiteSpace == WhiteSpaceType.PreWrap)
            return SplitPreservingWhitespace(text);
        if (whiteSpace == WhiteSpaceType.PreLine)
            return SplitPreservingNewlines(text);

        return SplitIntoWords(text);
    }

    private static List<string> SplitPreservingWhitespace(string text)
    {
        var words = new List<string>();
        var sb = new System.Text.StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                if (sb.Length > 0) { words.Add(sb.ToString()); sb.Clear(); }
                words.Add("\n");
            }
            else if (c == '\r')
            {
                // skip \r
            }
            else if (c == ' ' || c == '\t')
            {
                if (sb.Length > 0) { words.Add(sb.ToString()); sb.Clear(); }
                words.Add(c == '\t' ? "    " : " "); // tab as 4 spaces
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0) words.Add(sb.ToString());
        return words;
    }

    private static List<string> SplitPreservingNewlines(string text)
    {
        var words = new List<string>();
        var sb = new System.Text.StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                if (sb.Length > 0) { words.Add(sb.ToString()); sb.Clear(); }
                words.Add("\n");
            }
            else if (c == '\r')
            {
                // skip
            }
            else if (char.IsWhiteSpace(c))
            {
                if (sb.Length > 0)
                {
                    words.Add(sb.ToString());
                    sb.Clear();
                }
                if (words.Count == 0 || words[^1] != " ")
                    words.Add(" ");
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0) words.Add(sb.ToString());
        return words;
    }

    private static List<string> SplitIntoWords(string text)
    {
        var words = new List<string>();
        var sb = new System.Text.StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c))
            {
                if (sb.Length > 0)
                {
                    words.Add(sb.ToString());
                    sb.Clear();
                }
                // Add a space token to maintain word spacing
                if (words.Count == 0 || !words[^1].EndsWith(' '))
                {
                    // Collapse whitespace to single space
                    while (i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]))
                        i++;
                    sb.Append(' ');
                    words.Add(sb.ToString());
                    sb.Clear();
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0)
            words.Add(sb.ToString());

        return words;
    }

    private static void AlignLine(List<TextRun> lineRuns, float lineStartX, float availableWidth, TextAlign align)
    {
        if (lineRuns.Count == 0 || align == TextAlign.Left) return;

        // Calculate total width of content on this line
        float totalWidth = 0;
        if (lineRuns.Count > 0)
        {
            totalWidth = lineRuns[^1].X + lineRuns[^1].Width - lineRuns[0].X;
        }

        float offset = 0;
        switch (align)
        {
            case TextAlign.Center:
                offset = (availableWidth - totalWidth) / 2f;
                break;
            case TextAlign.Right:
                offset = availableWidth - totalWidth;
                break;
        }

        if (offset > 0)
        {
            foreach (var run in lineRuns)
            {
                run.X += offset;
            }
        }
    }

    /// <summary>
    /// Computes the rightmost edge of content (text runs and child boxes) in a layout box subtree.
    /// Used for shrink-to-fit width calculation of inline-block elements.
    /// </summary>
    internal static float ComputeContentRight(LayoutBox box)
    {
        float maxRight = 0;

        if (box.TextRuns is not null)
        {
            foreach (var run in box.TextRuns)
                maxRight = Math.Max(maxRight, run.X + run.Width);
        }

        foreach (var child in box.Children)
        {
            var childRight = child.Dimensions.BorderRect.Right;
            maxRight = Math.Max(maxRight, childRight);
            maxRight = Math.Max(maxRight, ComputeContentRight(child));
        }

        return maxRight;
    }
}
