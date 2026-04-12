namespace SuperRender.Renderer.Rendering.Style;

/// <summary>
/// Tracks active CSS transitions per element and interpolates property values.
/// </summary>
public sealed class TransitionEngine
{
    private readonly Dictionary<object, List<ActiveTransition>> _activeTransitions = new();

    public void Update(float deltaTime)
    {
        var toRemove = new List<object>();
        foreach (var (key, transitions) in _activeTransitions)
        {
            transitions.RemoveAll(t =>
            {
                t.Elapsed += deltaTime;
                return t.Elapsed >= t.Duration + t.Delay;
            });
            if (transitions.Count == 0)
                toRemove.Add(key);
        }
        foreach (var key in toRemove)
            _activeTransitions.Remove(key);
    }

    public void StartTransition(object elementKey, string property, float from, float to,
        float duration, float delay, TimingFunction timingFunction)
    {
        if (!_activeTransitions.TryGetValue(elementKey, out var list))
        {
            list = [];
            _activeTransitions[elementKey] = list;
        }

        // Replace existing transition for same property
        list.RemoveAll(t => t.Property == property);
        list.Add(new ActiveTransition
        {
            Property = property,
            FromValue = from,
            ToValue = to,
            Duration = duration,
            Delay = delay,
            TimingFunction = timingFunction,
        });
    }

    public float? GetTransitionValue(object elementKey, string property)
    {
        if (!_activeTransitions.TryGetValue(elementKey, out var list))
            return null;

        var transition = list.Find(t => t.Property == property);
        if (transition == null) return null;

        float elapsed = transition.Elapsed - transition.Delay;
        if (elapsed < 0) return transition.FromValue;
        if (elapsed >= transition.Duration) return transition.ToValue;

        float progress = transition.Duration > 0 ? elapsed / transition.Duration : 1;
        float easedProgress = transition.TimingFunction.Evaluate(progress);

        return transition.FromValue + (transition.ToValue - transition.FromValue) * easedProgress;
    }

    public bool HasActiveTransitions => _activeTransitions.Count > 0;

    private sealed class ActiveTransition
    {
        public required string Property { get; init; }
        public float FromValue { get; init; }
        public float ToValue { get; init; }
        public float Duration { get; init; }
        public float Delay { get; init; }
        public required TimingFunction TimingFunction { get; init; }
        public float Elapsed { get; set; }
    }
}
