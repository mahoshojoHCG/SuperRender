using System.Globalization;
using SuperRender.Renderer.Rendering.Style;

namespace SuperRender.Renderer.Rendering.Layout;

/// <summary>
/// CSS Grid layout algorithm.
/// Parses grid template definitions, places items, sizes tracks, and positions items.
/// </summary>
public static class GridLayout
{
    public static void Layout(LayoutBox box, BoxDimensions containingBlock, ITextMeasurer measurer)
    {
        var style = box.Style;
        var dims = box.Dimensions;

        // Apply padding, border, margin from style first
        dims.Padding = style.Padding;
        dims.Border = style.BorderWidth;
        dims.Margin = style.Margin;

        // Resolve width
        float contentWidth = !float.IsNaN(style.Width) ? style.Width : containingBlock.Width - dims.HorizontalEdge;
        dims.Width = Math.Max(0, Math.Clamp(contentWidth, style.MinWidth, style.MaxWidth));
        dims.X = containingBlock.X + dims.Margin.Left + dims.Border.Left + dims.Padding.Left;
        dims.Y = containingBlock.Y + dims.Margin.Top + dims.Border.Top + dims.Padding.Top;

        box.Dimensions = dims;

        // Parse column and row templates
        var columns = ParseTrackList(style.GridTemplateColumns);
        var rows = ParseTrackList(style.GridTemplateRows);

        // Auto columns/rows for implicit grid
        float autoColSize = ParseTrackSize(style.GridAutoColumns ?? "auto", dims.Width);
        float autoRowSize = ParseTrackSize(style.GridAutoRows ?? "auto", dims.Width);

        // Determine number of columns and rows needed
        int numCols = Math.Max(columns.Count, 1);
        int numRows = Math.Max(rows.Count, 1);

        // Count children to determine required grid size
        var children = box.Children.Where(c => c.Style.Display != DisplayType.None).ToList();
        int neededCells = children.Count;
        bool flowByRow = !style.GridAutoFlow.Contains("column", StringComparison.OrdinalIgnoreCase);

        if (flowByRow)
        {
            int neededRows = (int)Math.Ceiling((double)neededCells / numCols);
            numRows = Math.Max(numRows, neededRows);
        }
        else
        {
            int neededCols = (int)Math.Ceiling((double)neededCells / numRows);
            numCols = Math.Max(numCols, neededCols);
        }

        // Size columns
        float[] colSizes = new float[numCols];
        float totalFr = 0;
        float fixedWidth = 0;
        float gap = !float.IsNaN(style.ColumnGap) ? style.ColumnGap : style.Gap;
        float totalGapWidth = gap * Math.Max(0, numCols - 1);
        float availableWidth = dims.Width - totalGapWidth;

        for (int c = 0; c < numCols; c++)
        {
            if (c < columns.Count)
            {
                var track = columns[c];
                if (track.IsFr)
                {
                    totalFr += track.Value;
                }
                else
                {
                    colSizes[c] = track.Value;
                    fixedWidth += track.Value;
                }
            }
            else
            {
                colSizes[c] = autoColSize;
                fixedWidth += autoColSize;
            }
        }

        float frSpace = Math.Max(0, availableWidth - fixedWidth);
        for (int c = 0; c < numCols; c++)
        {
            if (c < columns.Count && columns[c].IsFr && totalFr > 0)
                colSizes[c] = frSpace * columns[c].Value / totalFr;
        }

        // Size rows
        float[] rowSizes = new float[numRows];
        float rowGap = !float.IsNaN(style.RowGap) ? style.RowGap : style.Gap;

        for (int r = 0; r < numRows; r++)
        {
            if (r < rows.Count)
            {
                var track = rows[r];
                rowSizes[r] = track.IsFr ? 0 : track.Value; // fr rows sized after content
            }
            else
            {
                rowSizes[r] = autoRowSize;
            }
        }

        // Place children and layout
        int childIndex = 0;

        for (int r = 0; r < numRows && childIndex < children.Count; r++)
        {
            float maxRowHeight = rowSizes[r];

            for (int c = 0; c < numCols && childIndex < children.Count; c++)
            {
                var child = children[childIndex];
                childIndex++;

                // Resolve explicit placement
                int placedRow = r;
                int placedCol = c;
                ResolveExplicitPlacement(child.Style, ref placedRow, ref placedCol);

                float cellX = dims.X;
                for (int ci = 0; ci < placedCol; ci++)
                    cellX += colSizes[ci] + gap;

                float cellY = dims.Y;
                for (int ri = 0; ri < placedRow; ri++)
                    cellY += rowSizes[ri] + rowGap;

                float cellWidth = colSizes[Math.Min(placedCol, numCols - 1)];
                float cellHeight = rowSizes[Math.Min(placedRow, numRows - 1)];

                var childDims = child.Dimensions;
                childDims.Padding = child.Style.Padding;
                childDims.Border = child.Style.BorderWidth;
                childDims.Margin = child.Style.Margin;
                childDims.X = cellX + childDims.Margin.Left + childDims.Border.Left + childDims.Padding.Left;
                childDims.Y = cellY + childDims.Margin.Top + childDims.Border.Top + childDims.Padding.Top;
                childDims.Width = !float.IsNaN(child.Style.Width) ? child.Style.Width : Math.Max(0, cellWidth - childDims.HorizontalEdge);
                child.Dimensions = childDims;

                // Layout child contents
                var childContaining = new BoxDimensions { X = childDims.X, Y = childDims.Y, Width = childDims.Width, Height = 0 };
                if (child.BoxType == LayoutBoxType.FlexContainer)
                    FlexLayout.Layout(child, childContaining, measurer);
                else
                    BlockLayout.Layout(child, childContaining, measurer);

                float childTotalHeight = child.Dimensions.Height + child.Dimensions.VerticalEdge;
                maxRowHeight = Math.Max(maxRowHeight, childTotalHeight);
            }

            if (rowSizes[r] == 0)
                rowSizes[r] = maxRowHeight;
        }

        // Compute total height
        float totalHeight = 0;
        for (int r = 0; r < numRows; r++)
            totalHeight += rowSizes[r];
        totalHeight += rowGap * Math.Max(0, numRows - 1);

        dims = box.Dimensions;
        dims.Height = !float.IsNaN(style.Height) ? style.Height : totalHeight;
        box.Dimensions = dims;
    }

    private static void ResolveExplicitPlacement(ComputedStyle style, ref int row, ref int col)
    {
        if (style.GridRowStart != null && int.TryParse(style.GridRowStart, CultureInfo.InvariantCulture, out int rs) && rs > 0)
            row = rs - 1;
        if (style.GridColumnStart != null && int.TryParse(style.GridColumnStart, CultureInfo.InvariantCulture, out int cs) && cs > 0)
            col = cs - 1;
    }

    public static List<GridTrack> ParseTrackList(string? template)
    {
        if (string.IsNullOrWhiteSpace(template) || template.Equals("none", StringComparison.OrdinalIgnoreCase))
            return [];

        var tracks = new List<GridTrack>();
        // Split by spaces but respect parentheses
        var parts = SplitTrackParts(template);

        foreach (var part in parts)
        {
            if (part.StartsWith("repeat(", StringComparison.OrdinalIgnoreCase))
            {
                ParseRepeat(part, tracks);
            }
            else
            {
                tracks.Add(ParseSingleTrack(part));
            }
        }

        return tracks;
    }

    private static List<string> SplitTrackParts(string template)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < template.Length; i++)
        {
            char c = template[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ' ' && depth == 0)
            {
                var part = template[start..i].Trim();
                if (part.Length > 0) parts.Add(part);
                start = i + 1;
            }
        }
        var last = template[start..].Trim();
        if (last.Length > 0) parts.Add(last);
        return parts;
    }

    private static GridTrack ParseSingleTrack(string value)
    {
        var lower = value.ToLowerInvariant();
        if (lower.EndsWith("fr", StringComparison.Ordinal))
        {
            if (float.TryParse(lower[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out float fr))
                return new GridTrack(fr, true);
        }
        if (lower == "auto")
            return new GridTrack(0, false); // auto-sized
        if (lower == "min-content" || lower == "max-content")
            return new GridTrack(0, false);

        return new GridTrack(ParseTrackSize(value, 0), false);
    }

    private static float ParseTrackSize(string value, float containingWidth)
    {
        var lower = value.Trim().ToLowerInvariant();
        if (lower == "auto") return 0;
        if (lower.EndsWith("px", StringComparison.Ordinal))
        {
            if (float.TryParse(lower[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out float px))
                return px;
        }
        if (lower.EndsWith('%'))
        {
            if (float.TryParse(lower[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float pct))
                return containingWidth * pct / 100f;
        }
        if (float.TryParse(lower, NumberStyles.Float, CultureInfo.InvariantCulture, out float bare))
            return bare;
        return 0;
    }

    private static void ParseRepeat(string value, List<GridTrack> tracks)
    {
        // repeat(count, track-size)
        var inner = value["repeat(".Length..];
        if (inner.EndsWith(')'))
            inner = inner[..^1];

        var commaIdx = inner.IndexOf(',', StringComparison.Ordinal);
        if (commaIdx < 0) return;

        var countStr = inner[..commaIdx].Trim();
        var trackStr = inner[(commaIdx + 1)..].Trim();

        if (!int.TryParse(countStr, CultureInfo.InvariantCulture, out int count)) return;
        count = Math.Min(count, 100); // safety limit

        var track = ParseSingleTrack(trackStr);
        for (int i = 0; i < count; i++)
            tracks.Add(track);
    }
}

/// <summary>
/// Represents a single grid track (column or row) definition.
/// </summary>
public readonly struct GridTrack
{
    public float Value { get; }
    public bool IsFr { get; }

    public GridTrack(float value, bool isFr)
    {
        Value = value;
        IsFr = isFr;
    }
}
