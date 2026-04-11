using SuperRender.Core.Dom;

namespace SuperRender.Core.Layout;

/// <summary>
/// Hit-tests mouse coordinates against the layout box tree to find
/// which DOM element was clicked.
/// </summary>
public static class LayoutBoxHitTester
{
    /// <summary>
    /// Returns the deepest LayoutBox whose border rect contains (x, y).
    /// Children are tested in reverse order so later-painted (visually on-top) boxes win.
    /// </summary>
    public static LayoutBox? HitTest(LayoutBox root, float x, float y)
    {
        // Check children in reverse order (last child paints on top)
        for (int i = root.Children.Count - 1; i >= 0; i--)
        {
            var childHit = HitTest(root.Children[i], x, y);
            if (childHit is not null)
                return childHit;
        }

        var rect = root.Dimensions.BorderRect;
        if (x >= rect.X && x <= rect.Right && y >= rect.Y && y <= rect.Bottom)
            return root;

        return null;
    }

    /// <summary>
    /// Walks from a LayoutBox's DomNode up through parents to find the nearest anchor element.
    /// </summary>
    public static Element? FindAnchorAncestor(LayoutBox? box)
    {
        if (box is null) return null;

        var node = box.DomNode;
        while (node is not null)
        {
            if (node is Element el && el.TagName == "a")
                return el;
            node = node.Parent;
        }
        return null;
    }
}
