namespace SuperRender.Renderer.Rendering.Layout;

/// <summary>
/// CSS table layout algorithm.
/// Handles table, table-row, and table-cell display types.
/// Supports both fixed and auto table layout algorithms.
/// </summary>
public static class TableLayout
{
    public static void Layout(LayoutBox table, BoxDimensions containingBlock, ITextMeasurer measurer)
    {
        var style = table.Style;
        var dims = table.Dimensions;

        // Resolve dimensions
        dims.Padding = style.Padding;
        dims.Border = style.BorderWidth;
        dims.Margin = style.Margin;
        float contentWidth = !float.IsNaN(style.Width) ? style.Width : containingBlock.Width - dims.HorizontalEdge;
        dims.Width = Math.Clamp(contentWidth, style.MinWidth, style.MaxWidth);
        dims.X = containingBlock.X + dims.Margin.Left + dims.Border.Left + dims.Padding.Left;
        dims.Y = containingBlock.Y + containingBlock.Height + dims.Margin.Top + dims.Border.Top + dims.Padding.Top;
        table.Dimensions = dims;

        bool isFixed = style.TableLayout == "fixed";
        bool isCollapse = style.BorderCollapse == "collapse";
        float spacing = isCollapse ? 0 : style.BorderSpacing;

        // Collect rows and cells
        var rows = new List<LayoutBox>();
        foreach (var child in table.Children)
        {
            if (child.BoxType == LayoutBoxType.TableRow || child.Style.Display == DisplayType.TableRow)
                rows.Add(child);
            else if (child.Style.Display is DisplayType.TableRowGroup or DisplayType.TableHeaderGroup or DisplayType.TableFooterGroup)
            {
                foreach (var rowChild in child.Children)
                {
                    if (rowChild.BoxType == LayoutBoxType.TableRow || rowChild.Style.Display == DisplayType.TableRow)
                        rows.Add(rowChild);
                }
            }
        }

        // Determine column count
        int numCols = 0;
        foreach (var row in rows)
        {
            int cellCount = row.Children.Count(c => c.Style.Display is DisplayType.TableCell
                || c.BoxType == LayoutBoxType.TableCell
                || c.Style.Display == DisplayType.Block);
            numCols = Math.Max(numCols, cellCount);
        }
        if (numCols == 0) numCols = 1;

        // Compute column widths
        float[] colWidths;
        if (isFixed)
        {
            // Fixed layout: equal column widths
            float totalSpacing = spacing * (numCols + 1);
            float colWidth = (dims.Width - totalSpacing) / numCols;
            colWidths = new float[numCols];
            Array.Fill(colWidths, colWidth);
        }
        else
        {
            // Auto layout: compute based on content
            colWidths = ComputeAutoColumnWidths(rows, numCols, dims.Width, spacing, measurer);
        }

        // Layout rows
        float currentY = dims.Y + spacing;
        foreach (var row in rows)
        {
            var rowDims = row.Dimensions;
            rowDims.X = dims.X;
            rowDims.Y = currentY;
            rowDims.Width = dims.Width;
            rowDims.Padding = row.Style.Padding;
            rowDims.Border = row.Style.BorderWidth;
            rowDims.Margin = row.Style.Margin;
            row.Dimensions = rowDims;

            float rowHeight = LayoutRow(row, colWidths, spacing, dims.X, currentY, measurer);

            rowDims = row.Dimensions;
            rowDims.Height = rowHeight;
            row.Dimensions = rowDims;

            currentY += rowHeight + spacing;
        }

        // Set table height
        dims = table.Dimensions;
        float totalHeight = currentY - dims.Y;
        dims.Height = !float.IsNaN(style.Height) ? Math.Max(style.Height, totalHeight) : totalHeight;
        table.Dimensions = dims;
    }

    private static float LayoutRow(LayoutBox row, float[] colWidths, float spacing,
        float startX, float startY, ITextMeasurer measurer)
    {
        float maxHeight = 0;
        float cellX = startX + spacing;
        int colIndex = 0;

        foreach (var cell in row.Children)
        {
            if (colIndex >= colWidths.Length) break;

            float cellWidth = colWidths[colIndex];
            var cellDims = cell.Dimensions;
            cellDims.Padding = cell.Style.Padding;
            cellDims.Border = cell.Style.BorderWidth;
            cellDims.Margin = cell.Style.Margin;
            cellDims.X = cellX + cellDims.Margin.Left + cellDims.Border.Left + cellDims.Padding.Left;
            cellDims.Y = startY + cellDims.Margin.Top + cellDims.Border.Top + cellDims.Padding.Top;
            cellDims.Width = cellWidth - cellDims.HorizontalEdge;
            cell.Dimensions = cellDims;

            // Layout cell contents
            var cellContaining = new BoxDimensions
            {
                X = cellDims.X,
                Y = cellDims.Y,
                Width = cellDims.Width,
                Height = 0,
            };

            if (cell.BoxType == LayoutBoxType.FlexContainer)
                FlexLayout.Layout(cell, cellContaining, measurer);
            else
                BlockLayout.Layout(cell, cellContaining, measurer);

            float cellTotalHeight = cell.Dimensions.Height + cell.Dimensions.VerticalEdge;
            maxHeight = Math.Max(maxHeight, cellTotalHeight);

            cellX += cellWidth + spacing;
            colIndex++;
        }

        return maxHeight;
    }

    private static float[] ComputeAutoColumnWidths(List<LayoutBox> rows, int numCols,
        float tableWidth, float spacing, ITextMeasurer measurer)
    {
        var colWidths = new float[numCols];
        float totalSpacing = spacing * (numCols + 1);
        float availableWidth = tableWidth - totalSpacing;

        // First pass: measure minimum content width per column
        foreach (var row in rows)
        {
            int colIndex = 0;
            foreach (var cell in row.Children)
            {
                if (colIndex >= numCols) break;
                float minWidth = EstimateCellWidth(cell, measurer);
                colWidths[colIndex] = Math.Max(colWidths[colIndex], minWidth);
                colIndex++;
            }
        }

        // Distribute remaining space equally
        float totalMin = colWidths.Sum();
        if (totalMin < availableWidth)
        {
            float extra = (availableWidth - totalMin) / numCols;
            for (int i = 0; i < numCols; i++)
                colWidths[i] += extra;
        }
        else if (totalMin > 0)
        {
            // Scale down proportionally
            float scale = availableWidth / totalMin;
            for (int i = 0; i < numCols; i++)
                colWidths[i] *= scale;
        }

        return colWidths;
    }

    private static float EstimateCellWidth(LayoutBox cell, ITextMeasurer measurer)
    {
        if (!float.IsNaN(cell.Style.Width))
            return cell.Style.Width;

        // Estimate from text content
        float maxTextWidth = 0;
        foreach (var child in cell.Children)
        {
            if (child.TextContent != null)
            {
                float textWidth = measurer.MeasureWidth(child.TextContent, child.Style.FontSize);
                maxTextWidth = Math.Max(maxTextWidth, textWidth);
            }
        }
        return maxTextWidth + cell.Style.Padding.HorizontalTotal;
    }
}
