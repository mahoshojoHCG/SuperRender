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
        float lineHeight = 0;
        float availableWidth = dims.Width;

        var currentLineRuns = new List<TextRun>();

        foreach (var child in anonymousBlock.Children)
        {
            if (child.TextContent != null)
            {
                var style = child.Style;
                var fontSize = style.FontSize;
                var lh = measurer.GetLineHeight(fontSize, style.LineHeight);
                var words = SplitIntoWords(child.TextContent);

                child.TextRuns = [];

                foreach (var word in words)
                {
                    var wordWidth = measurer.MeasureWidth(word, fontSize);

                    // Check if we need to wrap
                    if (cursorX - dims.X + wordWidth > availableWidth && cursorX > dims.X)
                    {
                        // New line
                        AlignLine(currentLineRuns, dims.X, availableWidth, child.Style.TextAlign);
                        cursorY += lineHeight;
                        cursorX = dims.X;
                        lineHeight = 0;
                        currentLineRuns = [];
                    }

                    var run = new TextRun
                    {
                        Text = word,
                        X = cursorX,
                        Y = cursorY,
                        Width = wordWidth,
                        Height = lh,
                        Style = style,
                    };

                    child.TextRuns.Add(run);
                    currentLineRuns.Add(run);

                    cursorX += wordWidth;
                    lineHeight = Math.Max(lineHeight, lh);
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
        if (lineHeight > 0)
        {
            cursorY += lineHeight;
        }

        dims.Height = cursorY - dims.Y;
        anonymousBlock.Dimensions = dims;
    }

    private static void LayoutInlineElement(LayoutBox box, ref float cursorX, ref float cursorY,
        ref float lineHeight, float lineStartX, float availableWidth,
        ITextMeasurer measurer, List<TextRun> currentLineRuns)
    {
        var style = box.Style;
        var paddingH = style.Padding.Left + style.Padding.Right;
        var borderH = style.BorderWidth.Left + style.BorderWidth.Right;
        var marginH = style.Margin.Left + style.Margin.Right;

        cursorX += style.Margin.Left + style.BorderWidth.Left + style.Padding.Left;

        box.TextRuns = [];

        foreach (var child in box.Children)
        {
            if (child.TextContent != null)
            {
                var fontSize = style.FontSize;
                var lh = measurer.GetLineHeight(fontSize, style.LineHeight);
                var words = SplitIntoWords(child.TextContent);

                child.TextRuns = [];

                foreach (var word in words)
                {
                    var wordWidth = measurer.MeasureWidth(word, fontSize);

                    if (cursorX - lineStartX + wordWidth > availableWidth && cursorX > lineStartX)
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
                        Height = lh,
                        Style = style,
                    };

                    child.TextRuns.Add(run);
                    box.TextRuns.Add(run);
                    currentLineRuns.Add(run);

                    cursorX += wordWidth;
                    lineHeight = Math.Max(lineHeight, lh);
                }
            }
        }

        cursorX += style.Padding.Right + style.BorderWidth.Right + style.Margin.Right;

        var dims = box.Dimensions;
        dims.Width = cursorX - dims.X;
        box.Dimensions = dims;
    }

    public static void LayoutSingleInline(LayoutBox box, BoxDimensions containingBlock, ITextMeasurer measurer)
    {
        if (box.TextContent != null)
        {
            var style = box.Style;
            var fontSize = style.FontSize;
            var lh = measurer.GetLineHeight(fontSize, style.LineHeight);

            box.TextRuns = [];

            float cursorX = containingBlock.X;
            float cursorY = containingBlock.Y;
            float lineHeight = 0;
            var words = SplitIntoWords(box.TextContent);

            foreach (var word in words)
            {
                var wordWidth = measurer.MeasureWidth(word, fontSize);

                if (cursorX - containingBlock.X + wordWidth > containingBlock.Width && cursorX > containingBlock.X)
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
                    Height = lh,
                    Style = style,
                });

                cursorX += wordWidth;
                lineHeight = Math.Max(lineHeight, lh);
            }

            if (lineHeight > 0) cursorY += lineHeight;

            var dims = box.Dimensions;
            dims.X = containingBlock.X;
            dims.Y = containingBlock.Y;
            dims.Width = containingBlock.Width;
            dims.Height = cursorY - containingBlock.Y;
            box.Dimensions = dims;
        }
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
}
