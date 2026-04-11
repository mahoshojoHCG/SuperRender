namespace SuperRender.Document.Dom;

/// <summary>
/// Base DOM event with capture/bubble propagation support.
/// </summary>
public class DomEvent
{
    public string Type { get; init; } = "";
    public Node? Target { get; internal set; }
    public Node? CurrentTarget { get; internal set; }
    public bool Bubbles { get; init; } = true;
    public bool Cancelable { get; init; } = true;

    /// <summary>
    /// 1 = CAPTURING_PHASE, 2 = AT_TARGET, 3 = BUBBLING_PHASE
    /// </summary>
    public int EventPhase { get; internal set; }

    public bool DefaultPrevented { get; private set; }
    internal bool PropagationStopped { get; private set; }
    internal bool ImmediatePropagationStopped { get; private set; }

    public void PreventDefault()
    {
        if (Cancelable) DefaultPrevented = true;
    }

    public void StopPropagation() => PropagationStopped = true;

    public void StopImmediatePropagation()
    {
        ImmediatePropagationStopped = true;
        PropagationStopped = true;
    }
}

/// <summary>
/// Mouse-related DOM event with coordinates and button info.
/// </summary>
public class MouseEvent : DomEvent
{
    public float ClientX { get; init; }
    public float ClientY { get; init; }
    public int Button { get; init; } // 0=left, 1=middle, 2=right
    public bool CtrlKey { get; init; }
    public bool ShiftKey { get; init; }
    public bool AltKey { get; init; }
    public bool MetaKey { get; init; }
}

/// <summary>
/// Keyboard-related DOM event with key info.
/// </summary>
public class KeyboardEvent : DomEvent
{
    public string Key { get; init; } = "";
    public string Code { get; init; } = "";
    public bool CtrlKey { get; init; }
    public bool ShiftKey { get; init; }
    public bool AltKey { get; init; }
    public bool MetaKey { get; init; }
    public bool Repeat { get; init; }
}

/// <summary>
/// A registered event listener on a DOM node.
/// </summary>
public sealed class EventListener
{
    public required string Type { get; init; }
    public required Action<DomEvent> Handler { get; init; }
    public bool Capture { get; init; }
}
