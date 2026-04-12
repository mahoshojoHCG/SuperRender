using SuperRender.Document;
using SuperRender.Renderer.Rendering.Layout;

namespace SuperRender.Renderer.Rendering.Painting;

/// <summary>
/// Generates highlight paint commands for text selection.
/// </summary>
public static class SelectionPainter
{
    private static readonly Color HighlightColor = Color.FromRgba(51, 144, 255, 80);

    /// <summary>
    /// Builds FillRectCommands for the selected text region.
    /// These should be drawn before (behind) text commands.
    /// </summary>
    public static PaintList BuildHighlights(
        TextSelectionState selection,
        IReadOnlyList<TextRun> allRuns,
        ITextMeasurer measurer)
    {
        var list = new PaintList();
        if (!selection.HasSelection || allRuns.Count == 0) return list;

        var (start, end) = selection.GetOrdered();

        for (int i = start.RunIndex; i <= end.RunIndex && i < allRuns.Count; i++)
        {
            var run = allRuns[i];
            float fontSize = run.Style.FontSize;
            string text = run.Text;

            int startChar = (i == start.RunIndex) ? start.CharOffset : 0;
            int endChar = (i == end.RunIndex) ? end.CharOffset : text.Length;

            if (startChar >= endChar) continue;

            // Compute X offsets for the selection region within this run.
            // Letter-spacing is between characters, so N characters span (N-1) spacings
            // in the layout model. Clamp to run boundaries to avoid overlap with adjacent runs.
            float letterSpacing = run.Style.LetterSpacing;
            float startX = run.X;
            if (startChar > 0)
            {
                startX += measurer.MeasureWidth(text[..startChar], fontSize, run.Style.FontFamily, run.Style.FontWeight)
                        + letterSpacing * startChar;
            }

            float endX;
            if (endChar >= text.Length)
            {
                // Full run: use layout width directly to stay in bounds
                endX = run.X + run.Width;
            }
            else
            {
                endX = run.X + measurer.MeasureWidth(text[..endChar], fontSize, run.Style.FontFamily, run.Style.FontWeight)
                     + letterSpacing * endChar;
            }

            // Clamp to run boundaries
            endX = Math.Min(endX, run.X + run.Width);

            list.Add(new FillRectCommand
            {
                Rect = new RectF(startX, run.Y, endX - startX, run.Height),
                Color = HighlightColor,
            });
        }

        return list;
    }
}
