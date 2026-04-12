namespace SuperRender.Renderer.Rendering.Style;

/// <summary>
/// Tracks active CSS @keyframes animations per element.
/// </summary>
public sealed class AnimationEngine
{
    private readonly Dictionary<object, List<ActiveAnimation>> _activeAnimations = new();
    private readonly Dictionary<string, List<Keyframe>> _keyframeRules = new();

    /// <summary>Registers a @keyframes rule by name.</summary>
    public void RegisterKeyframes(string name, List<Keyframe> keyframes)
    {
        _keyframeRules[name] = keyframes;
    }

    public void StartAnimation(object elementKey, string animationName, float duration,
        float delay, float iterationCount, string direction, string fillMode,
        TimingFunction? timingFunction)
    {
        if (!_keyframeRules.ContainsKey(animationName)) return;

        if (!_activeAnimations.TryGetValue(elementKey, out var list))
        {
            list = [];
            _activeAnimations[elementKey] = list;
        }

        list.RemoveAll(a => a.Name == animationName);
        list.Add(new ActiveAnimation
        {
            Name = animationName,
            Duration = duration,
            Delay = delay,
            IterationCount = iterationCount,
            Direction = direction,
            FillMode = fillMode,
            TimingFunction = timingFunction ?? TimingFunction.Ease,
        });
    }

    public void Update(float deltaTime)
    {
        var toRemove = new List<object>();
        foreach (var (key, animations) in _activeAnimations)
        {
            foreach (var anim in animations)
                anim.Elapsed += deltaTime;

            animations.RemoveAll(a =>
                !float.IsPositiveInfinity(a.IterationCount)
                && a.Elapsed >= a.Duration * a.IterationCount + a.Delay
                && a.FillMode is "none");

            if (animations.Count == 0)
                toRemove.Add(key);
        }
        foreach (var key in toRemove)
            _activeAnimations.Remove(key);
    }

    /// <summary>Gets the current animation progress for a given property.</summary>
    public float? GetAnimatedValue(object elementKey, string animationName, string property)
    {
        if (!_activeAnimations.TryGetValue(elementKey, out var list)) return null;
        if (!_keyframeRules.TryGetValue(animationName, out var keyframes)) return null;

        var anim = list.Find(a => a.Name == animationName);
        if (anim == null) return null;

        float elapsed = anim.Elapsed - anim.Delay;
        if (elapsed < 0) return null;

        float progress;
        if (anim.Duration <= 0)
        {
            progress = 1;
        }
        else
        {
            float iteration = elapsed / anim.Duration;
            if (!float.IsPositiveInfinity(anim.IterationCount) && iteration >= anim.IterationCount)
                iteration = anim.IterationCount;

            float currentIteration = iteration % 1f;
            if (currentIteration == 0 && iteration > 0) currentIteration = 1;

            bool reverse = anim.Direction switch
            {
                "reverse" => true,
                "alternate" => ((int)iteration % 2) == 1,
                "alternate-reverse" => ((int)iteration % 2) == 0,
                _ => false,
            };

            progress = reverse ? 1 - currentIteration : currentIteration;
        }

        float easedProgress = anim.TimingFunction.Evaluate(progress);

        // Find surrounding keyframes
        var sorted = keyframes.OrderBy(k => k.Offset).ToList();
        Keyframe? from = null;
        Keyframe? to = null;

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (easedProgress >= sorted[i].Offset && easedProgress <= sorted[i + 1].Offset)
            {
                from = sorted[i];
                to = sorted[i + 1];
                break;
            }
        }

        if (from == null || to == null) return null;

        if (!from.Values.TryGetValue(property, out float fromVal)) return null;
        if (!to.Values.TryGetValue(property, out float toVal)) return null;

        float segmentProgress = (to.Offset - from.Offset) > 0
            ? (easedProgress - from.Offset) / (to.Offset - from.Offset)
            : 1;

        return PropertyInterpolation.LerpFloat(fromVal, toVal, segmentProgress);
    }

    public bool HasActiveAnimations => _activeAnimations.Count > 0;

    private sealed class ActiveAnimation
    {
        public required string Name { get; init; }
        public float Duration { get; init; }
        public float Delay { get; init; }
        public float IterationCount { get; init; }
        public required string Direction { get; init; }
        public required string FillMode { get; init; }
        public required TimingFunction TimingFunction { get; init; }
        public float Elapsed { get; set; }
    }
}

/// <summary>
/// A single keyframe in a @keyframes rule.
/// </summary>
public sealed class Keyframe
{
    /// <summary>Offset in 0..1 range (0 = from, 1 = to).</summary>
    public float Offset { get; init; }

    /// <summary>Property name -> numeric value.</summary>
    public Dictionary<string, float> Values { get; init; } = new();
}
