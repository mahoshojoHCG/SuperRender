namespace SuperRender.Core.Layout;

/// <summary>
/// Hit-tests mouse coordinates against laid-out text runs to find character positions.
/// </summary>
public static class TextHitTester
{
    /// <summary>
    /// Collects all TextRuns from a layout tree into a flat document-order list.
    /// </summary>
    public static List<TextRun> CollectTextRuns(LayoutBox root)
    {
        var runs = new List<TextRun>();
        CollectTextRunsRecursive(root, runs);
        return runs;
    }

    private static void CollectTextRunsRecursive(LayoutBox box, List<TextRun> runs)
    {
        if (box.TextRuns is not null)
        {
            foreach (var run in box.TextRuns)
            {
                if (!string.IsNullOrEmpty(run.Text))
                    runs.Add(run);
            }
        }

        foreach (var child in box.Children)
            CollectTextRunsRecursive(child, runs);
    }

    /// <summary>
    /// Finds the nearest (runIndex, charOffset) for a click at (x, y) in content coordinates.
    /// </summary>
    public static (int runIndex, int charOffset)? HitTest(
        IReadOnlyList<TextRun> allRuns, float x, float y, ITextMeasurer measurer)
    {
        if (allRuns.Count == 0) return null;

        // Find the closest run by Y first, then by X within that line
        int bestRun = -1;
        float bestDist = float.MaxValue;

        for (int i = 0; i < allRuns.Count; i++)
        {
            var run = allRuns[i];
            float runBottom = run.Y + run.Height;

            // Vertical distance: 0 if click is within run's Y range
            float yDist = 0;
            if (y < run.Y) yDist = run.Y - y;
            else if (y > runBottom) yDist = y - runBottom;

            // Horizontal distance: 0 if click is within run's X range
            float xDist = 0;
            float runRight = run.X + run.Width;
            if (x < run.X) xDist = run.X - x;
            else if (x > runRight) xDist = x - runRight;

            float dist = yDist * 10 + xDist; // Bias toward Y proximity
            if (dist < bestDist)
            {
                bestDist = dist;
                bestRun = i;
            }
        }

        if (bestRun < 0) return null;

        // Find character offset within the best run
        var best = allRuns[bestRun];
        float fontSize = best.Style.FontSize;
        float relX = x - best.X;
        if (relX <= 0) return (bestRun, 0);

        string text = best.Text;
        for (int i = 1; i <= text.Length; i++)
        {
            float w = measurer.MeasureWidth(text[..i], fontSize);
            if (w > relX)
            {
                float prevW = i > 1 ? measurer.MeasureWidth(text[..(i - 1)], fontSize) : 0;
                int offset = (relX - prevW < w - relX) ? i - 1 : i;
                return (bestRun, offset);
            }
        }

        return (bestRun, text.Length);
    }
}
