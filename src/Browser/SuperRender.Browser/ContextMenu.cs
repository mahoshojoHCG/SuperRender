using SuperRender.Document;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Painting;

namespace SuperRender.Browser;

public sealed class ContextMenuItem
{
    public required string Label { get; init; }
    public required Action Action { get; init; }
    public bool Enabled { get; init; } = true;
    public bool IsSeparator { get; init; }
}

/// <summary>
/// A simple context menu rendered as paint commands.
/// </summary>
public sealed class ContextMenu
{
    private const float ItemHeight = 24f;
    private const float SeparatorHeight = 9f;
    private const float MenuPadding = 4f;
    private const float MenuWidth = 160f;
    private const float FontSize = 12f;

    private static readonly Color MenuBg = Color.White;
    private static readonly Color MenuBorder = Color.FromRgb(173, 181, 189);
    private static readonly Color HoverBg = Color.FromRgb(0, 120, 215);
    private static readonly Color HoverText = Color.White;
    private static readonly Color ItemText = Color.FromRgb(33, 37, 41);
    private static readonly Color DisabledText = Color.FromRgb(173, 181, 189);
    private static readonly Color SeparatorColor = Color.FromRgb(222, 226, 230);

    public float X { get; }
    public float Y { get; }
    public List<ContextMenuItem> Items { get; }
    public bool IsVisible { get; set; } = true;
    public int HoveredIndex { get; set; } = -1;

    public ContextMenu(float x, float y, List<ContextMenuItem> items)
    {
        X = x;
        Y = y;
        Items = items;
    }

    public float Width => Items.Count > 0 ? MenuWidth : 0;

    public float Height
    {
        get
        {
            float h = MenuPadding * 2;
            foreach (var item in Items)
                h += item.IsSeparator ? SeparatorHeight : ItemHeight;
            return h;
        }
    }

    public PaintList BuildPaintList()
    {
        var list = new PaintList();

        // Background
        list.Add(new FillRectCommand
        {
            Rect = new RectF(X, Y, Width, Height),
            Color = MenuBg,
        });

        // Border
        list.Add(new StrokeRectCommand
        {
            Rect = new RectF(X, Y, Width, Height),
            Color = MenuBorder,
            LineWidth = 1f,
        });

        float itemY = Y + MenuPadding;
        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            if (item.IsSeparator)
            {
                // Separator line
                list.Add(new FillRectCommand
                {
                    Rect = new RectF(X + 8, itemY + 4, Width - 16, 1),
                    Color = SeparatorColor,
                });
                itemY += SeparatorHeight;
                continue;
            }

            // Hover highlight
            if (i == HoveredIndex && item.Enabled)
            {
                list.Add(new FillRectCommand
                {
                    Rect = new RectF(X + 2, itemY, Width - 4, ItemHeight),
                    Color = HoverBg,
                });
            }

            // Label
            var textColor = !item.Enabled ? DisabledText
                : (i == HoveredIndex) ? HoverText
                : ItemText;

            float textY = itemY + (ItemHeight - FontSize) / 2f;
            list.Add(new DrawTextCommand
            {
                Text = item.Label,
                X = X + 12,
                Y = textY,
                FontSize = FontSize,
                Color = textColor,
            });

            itemY += ItemHeight;
        }

        return list;
    }

    /// <summary>
    /// Returns the index of the item at the given position, or -1 if outside the menu.
    /// </summary>
    public int HitTest(float clickX, float clickY)
    {
        if (clickX < X || clickX > X + Width || clickY < Y || clickY > Y + Height)
            return -1;

        float itemY = Y + MenuPadding;
        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            float h = item.IsSeparator ? SeparatorHeight : ItemHeight;

            if (clickY >= itemY && clickY < itemY + h)
                return item.IsSeparator ? -1 : i;

            itemY += h;
        }

        return -1;
    }

    /// <summary>
    /// Updates the hovered item based on mouse position.
    /// </summary>
    public void UpdateHover(float mouseX, float mouseY)
    {
        HoveredIndex = HitTest(mouseX, mouseY);
    }
}
