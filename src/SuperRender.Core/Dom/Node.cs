namespace SuperRender.Core.Dom;

public enum NodeType
{
    Document,
    Element,
    Text,
    Comment
}

public abstract class Node
{
    public abstract NodeType NodeType { get; }
    public Node? Parent { get; internal set; }
    public List<Node> Children { get; } = [];
    public Document? OwnerDocument { get; internal set; }

    public Node? FirstChild => Children.Count > 0 ? Children[0] : null;
    public Node? LastChild => Children.Count > 0 ? Children[^1] : null;
    public Node? NextSibling { get; internal set; }
    public Node? PreviousSibling { get; internal set; }

    public void AppendChild(Node child)
    {
        if (child.Parent != null)
            child.Parent.RemoveChild(child);

        child.Parent = this;
        child.OwnerDocument = this is Document doc ? doc : OwnerDocument;

        if (Children.Count > 0)
        {
            var last = Children[^1];
            last.NextSibling = child;
            child.PreviousSibling = last;
        }
        else
        {
            child.PreviousSibling = null;
        }

        child.NextSibling = null;
        Children.Add(child);
        MarkDirty();
    }

    public void RemoveChild(Node child)
    {
        var index = Children.IndexOf(child);
        if (index < 0) return;

        if (child.PreviousSibling != null)
            child.PreviousSibling.NextSibling = child.NextSibling;
        if (child.NextSibling != null)
            child.NextSibling.PreviousSibling = child.PreviousSibling;

        child.Parent = null;
        child.PreviousSibling = null;
        child.NextSibling = null;
        Children.RemoveAt(index);
        MarkDirty();
    }

    public void InsertBefore(Node newChild, Node? referenceChild)
    {
        if (referenceChild == null)
        {
            AppendChild(newChild);
            return;
        }

        var index = Children.IndexOf(referenceChild);
        if (index < 0)
        {
            AppendChild(newChild);
            return;
        }

        if (newChild.Parent != null)
            newChild.Parent.RemoveChild(newChild);

        newChild.Parent = this;
        newChild.OwnerDocument = this is Document doc ? doc : OwnerDocument;

        newChild.NextSibling = referenceChild;
        newChild.PreviousSibling = referenceChild.PreviousSibling;

        if (referenceChild.PreviousSibling != null)
            referenceChild.PreviousSibling.NextSibling = newChild;

        referenceChild.PreviousSibling = newChild;

        Children.Insert(index, newChild);
        MarkDirty();
    }

    internal bool IsDirty { get; set; } = true;

    internal void MarkDirty()
    {
        IsDirty = true;
        if (OwnerDocument != null)
            OwnerDocument.NeedsLayout = true;
        Parent?.MarkDirty();
    }
}
