using System.Text;

namespace SuperRender.Document.Dom;

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

    /// <summary>
    /// Gets or sets the text content of this node and its descendants.
    /// Getter recursively collects all TextNode text. Setter removes all children
    /// and replaces them with a single TextNode.
    /// </summary>
    public virtual string TextContent
    {
        get
        {
            var sb = new StringBuilder();
            CollectTextContent(this, sb);
            return sb.ToString();
        }
        set
        {
            // Remove all existing children
            while (Children.Count > 0)
                RemoveChild(Children[^1]);

            if (!string.IsNullOrEmpty(value))
            {
                var textNode = new TextNode(value)
                {
                    OwnerDocument = this is Document doc ? doc : OwnerDocument
                };
                AppendChild(textNode);
            }
        }
    }

    private static void CollectTextContent(Node node, StringBuilder sb)
    {
        if (node is TextNode text)
        {
            sb.Append(text.Data);
            return;
        }

        foreach (var child in node.Children)
            CollectTextContent(child, sb);
    }

    /// <summary>
    /// Returns true if this node has one or more children.
    /// </summary>
    public bool HasChildNodes() => Children.Count > 0;

    /// <summary>
    /// Returns true if <paramref name="other"/> is this node or a descendant of this node.
    /// </summary>
    public bool Contains(Node? other)
    {
        if (other is null) return false;

        var current = other;
        while (current is not null)
        {
            if (ReferenceEquals(current, this))
                return true;
            current = current.Parent;
        }

        return false;
    }

    /// <summary>
    /// Creates a copy of this node. If <paramref name="deep"/> is true, all descendants
    /// are cloned recursively.
    /// </summary>
    public abstract Node CloneNode(bool deep);

    /// <summary>
    /// Replaces <paramref name="oldChild"/> with <paramref name="newChild"/> in this node's
    /// children list. Returns the removed old child.
    /// </summary>
    public Node ReplaceChild(Node newChild, Node oldChild)
    {
        ArgumentNullException.ThrowIfNull(newChild);
        ArgumentNullException.ThrowIfNull(oldChild);

        var index = Children.IndexOf(oldChild);
        if (index < 0)
            throw new InvalidOperationException("The node to be replaced is not a child of this node.");

        // Remove newChild from its current parent if needed
        if (newChild.Parent != null)
            newChild.Parent.RemoveChild(newChild);

        // Fix sibling links for the old child's neighbors
        var prev = oldChild.PreviousSibling;
        var next = oldChild.NextSibling;

        newChild.PreviousSibling = prev;
        newChild.NextSibling = next;
        if (prev != null) prev.NextSibling = newChild;
        if (next != null) next.PreviousSibling = newChild;

        newChild.Parent = this;
        newChild.OwnerDocument = this is Document doc ? doc : OwnerDocument;

        // Clear old child's links
        oldChild.Parent = null;
        oldChild.PreviousSibling = null;
        oldChild.NextSibling = null;

        Children[index] = newChild;
        MarkDirty();

        return oldChild;
    }

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

    // --- EventTarget functionality ---

    private List<EventListener>? _eventListeners;

    public void AddEventListener(string type, Action<DomEvent> handler, bool capture = false)
    {
        _eventListeners ??= [];
        _eventListeners.Add(new EventListener { Type = type, Handler = handler, Capture = capture });
    }

    public void RemoveEventListener(string type, Action<DomEvent> handler, bool capture = false)
    {
        _eventListeners?.RemoveAll(l => l.Type == type && l.Handler == handler && l.Capture == capture);
    }

    /// <summary>
    /// Dispatches an event through the capture → target → bubble phases.
    /// Returns true if the event's default action was not prevented.
    /// </summary>
    public bool DispatchEvent(DomEvent evt)
    {
        evt.Target = this;

        // Build propagation path (target → root)
        var path = new List<Node>();
        Node? current = this;
        while (current is not null)
        {
            path.Add(current);
            current = current.Parent;
        }

        // Capture phase (root → parent of target)
        for (int i = path.Count - 1; i > 0; i--)
        {
            if (evt.PropagationStopped) break;
            evt.CurrentTarget = path[i];
            evt.EventPhase = 1; // CAPTURING_PHASE
            path[i].InvokeListeners(evt, capture: true);
        }

        // Target phase
        if (!evt.PropagationStopped)
        {
            evt.CurrentTarget = this;
            evt.EventPhase = 2; // AT_TARGET
            InvokeListeners(evt, capture: true);
            InvokeListeners(evt, capture: false);
        }

        // Bubble phase (parent of target → root)
        if (evt.Bubbles && !evt.PropagationStopped)
        {
            for (int i = 1; i < path.Count; i++)
            {
                if (evt.PropagationStopped) break;
                evt.CurrentTarget = path[i];
                evt.EventPhase = 3; // BUBBLING_PHASE
                path[i].InvokeListeners(evt, capture: false);
            }
        }

        return !evt.DefaultPrevented;
    }

    private void InvokeListeners(DomEvent evt, bool capture)
    {
        if (_eventListeners is null) return;
        // Snapshot to allow modification during iteration
        foreach (var listener in _eventListeners.ToArray())
        {
            if (evt.ImmediatePropagationStopped) break;
            if (listener.Type == evt.Type && listener.Capture == capture)
            {
                try { listener.Handler(evt); }
                catch (Exception ex) { Console.WriteLine($"[Event] Error in {evt.Type} handler: {ex.Message}"); }
            }
        }
    }
}
