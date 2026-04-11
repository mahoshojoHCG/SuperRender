namespace SuperRender.Core.Layout;

/// <summary>
/// Hit-tests mouse coordinates against laid-out text runs to find character positions.
/// </summary>
public static class TextHitTester
{
    /// <summary>
    /// Collects all TextRuns from a layout tree into a flat document-order list.
    /// Runs clipped by overflow:hidden ancestors are excluded.
    /// </summary>
    public static List<TextRun> CollectTextRuns(LayoutBox root)
    {
        var runs = new List<TextRun>();
        CollectTextRunsRecursive(root, runs, null);
        return runs;
    }

    private static void CollectTextRunsRecursive(LayoutBox box, List<TextRun> runs, RectF? clipRect)
    {
        // Update clip region for overflow:hidden boxes
        if (box.Style.Overflow == Style.OverflowType.Hidden)
        {
            var paddingRect = box.Dimensions.PaddingRect;
            clipRect = clipRect.HasValue ? Intersect(clipRect.Value, paddingRect) : paddingRect;
        }

        if (box.TextRuns is not null && box.TextRuns.Count > 0)
        {
            foreach (var run in box.TextRuns)
            {
                if (!string.IsNullOrEmpty(run.Text) && IsRunVisible(run, clipRect))
                    runs.Add(run);
            }
            // Don't recurse into children — this box's TextRuns already include them
            // (inline elements collect their children's text runs into their own list)
            return;
        }

        foreach (var child in box.Children)
            CollectTextRunsRecursive(child, runs, clipRect);
    }

    /// <summary>
    /// Checks whether a text run is at least partially within the clip rect.
    /// </summary>
    private static bool IsRunVisible(TextRun run, RectF? clipRect)
    {
        if (!clipRect.HasValue) return true;
        var clip = clipRect.Value;
        float runBottom = run.Y + run.Height;
        float runRight = run.X + run.Width;
        // Run must overlap the clip rect in both axes
        return run.X < clip.Right && runRight > clip.X
            && run.Y < clip.Bottom && runBottom > clip.Y;
    }

    /// <summary>
    /// Intersects two rectangles, returning the overlapping region.
    /// </summary>
    private static RectF Intersect(RectF a, RectF b)
    {
        float x = Math.Max(a.X, b.X);
        float y = Math.Max(a.Y, b.Y);
        float right = Math.Min(a.Right, b.Right);
        float bottom = Math.Min(a.Bottom, b.Bottom);
        return new RectF(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
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
            float w = measurer.MeasureWidth(text[..i], fontSize, best.Style.FontFamily, best.Style.FontWeight);
            if (w > relX)
            {
                float prevW = i > 1 ? measurer.MeasureWidth(text[..(i - 1)], fontSize, best.Style.FontFamily, best.Style.FontWeight) : 0;
                int offset = (relX - prevW < w - relX) ? i - 1 : i;
                return (bestRun, offset);
            }
        }

        return (bestRun, text.Length);
    }
}
