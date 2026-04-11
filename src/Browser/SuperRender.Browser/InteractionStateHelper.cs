using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document.Dom;

namespace SuperRender.Browser;

/// <summary>
/// Manages :hover, :active, and :focus CSS pseudo-class state flags on DOM elements.
/// Setting these flags causes the style resolver to re-evaluate pseudo-class selectors
/// on the next layout pass.
/// </summary>
public static class InteractionStateHelper
{
    /// <summary>
    /// Sets IsHovered = true on the given node (if Element) and all its Element ancestors.
    /// </summary>
    public static void SetHovered(Node? node)
    {
        var current = node;
        while (current is not null)
        {
            if (current is Element el)
                el.IsHovered = true;
            current = current.Parent;
        }
    }

    /// <summary>
    /// Clears IsHovered on the given node (if Element) and all its Element ancestors.
    /// </summary>
    public static void ClearHovered(Node? node)
    {
        var current = node;
        while (current is not null)
        {
            if (current is Element el)
                el.IsHovered = false;
            current = current.Parent;
        }
    }

    /// <summary>
    /// Updates hover state when the hovered element changes.
    /// Clears hover on the old target chain, sets hover on the new target chain,
    /// and marks the document for re-layout so style resolution picks up the
    /// :hover pseudo-class changes.
    /// </summary>
    public static void UpdateHover(Node? oldTarget, Node? newTarget, DomDocument? doc)
    {
        ClearHovered(oldTarget);
        SetHovered(newTarget);
        if (doc is not null)
            doc.NeedsLayout = true;
    }

    /// <summary>
    /// Sets IsActive = true on the given node (if it is an Element).
    /// </summary>
    public static void SetActive(Node? node)
    {
        if (node is Element el)
            el.IsActive = true;
    }

    /// <summary>
    /// Clears IsActive on the given node and all its Element ancestors.
    /// </summary>
    public static void ClearActive(Node? node)
    {
        var current = node;
        while (current is not null)
        {
            if (current is Element el)
                el.IsActive = false;
            current = current.Parent;
        }
    }

    /// <summary>
    /// Sets focus on a new element, clearing focus on the previously focused element.
    /// Only sets focus if the element is focusable (a, input, button, textarea, select, [tabindex]).
    /// Returns the newly focused element, or null if the target was not focusable.
    /// </summary>
    public static Element? SetFocus(Element? newFocus, Element? oldFocus)
    {
        if (oldFocus is not null)
            oldFocus.IsFocused = false;

        if (newFocus is not null && IsFocusable(newFocus))
        {
            newFocus.IsFocused = true;
            return newFocus;
        }

        return null;
    }

    /// <summary>
    /// Returns true if the element is focusable.
    /// Focusable elements: a, input, button, textarea, select, or any element with [tabindex].
    /// </summary>
    public static bool IsFocusable(Element element)
    {
        var tag = element.TagName;
        if (tag is "a" or "input" or "button" or "textarea" or "select")
            return true;
        return element.HasAttribute("tabindex");
    }

    /// <summary>
    /// Collects the chain of Element ancestors from a node (inclusive of the node itself
    /// if it is an Element) up to the root. Returns them in bottom-up order.
    /// </summary>
    public static List<Element> GetElementAncestors(Node? node)
    {
        var ancestors = new List<Element>();
        var current = node;
        while (current is not null)
        {
            if (current is Element el)
                ancestors.Add(el);
            current = current.Parent;
        }
        return ancestors;
    }
}
