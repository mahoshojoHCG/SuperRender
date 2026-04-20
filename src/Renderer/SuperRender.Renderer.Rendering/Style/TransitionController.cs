using System.Diagnostics;
using SuperRender.Document;
using SuperRender.Document.Dom;

namespace SuperRender.Renderer.Rendering.Style;

/// <summary>
/// Drives CSS transitions across render passes: detects changed transitionable
/// properties, starts interpolations, and overrides the new computed style with
/// the currently interpolated values. Returns whether any transitions are still
/// active so the pipeline can request another frame.
/// </summary>
public sealed class TransitionController
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<Node, NodeState> _state = new();

    private sealed class NodeState
    {
        public readonly Dictionary<string, Active> Active = new(StringComparer.Ordinal);
        public Snapshot? Last;
    }

    private sealed class Snapshot
    {
        public Color BackgroundColor;
        public Color Color;
        public float Opacity;
        public List<TransformFunction>? Transform;
    }

    private sealed class Active
    {
        public required string Property;
        public required float StartTime;
        public required float Duration;
        public required float Delay;
        public required TimingFunction Timing;
        public required object From;
        public required object To;
    }

    private float Now => (float)_clock.Elapsed.TotalSeconds;

    /// <summary>
    /// Updates transitions for all nodes, detects changes, applies interpolated
    /// values by mutating entries in <paramref name="styles"/>. Returns true if
    /// any transitions remain active.
    /// </summary>
    public bool Apply(Dictionary<Node, ComputedStyle> styles)
    {
        bool anyActive = false;
        float now = Now;

        foreach (var (node, style) in styles)
        {
            if (!_state.TryGetValue(node, out var ns))
            {
                ns = new NodeState();
                _state[node] = ns;
            }

            // Start new transitions when the TARGET value (the freshly-resolved style,
            // before we overwrite it below) changes. We compare against the last known
            // target (stored on the active transition if any, or the last snapshot).
            if (ns.Last is not null && style.TransitionDuration > 0)
            {
                var props = ParseTransitionProperty(style.TransitionProperty);
                var timing = style.TransitionTimingFunction ?? TimingFunction.Ease;
                float duration = style.TransitionDuration;
                float delay = style.TransitionDelay;

                if (IsTransitionable(props, "background-color"))
                {
                    var lastTarget = ns.Active.TryGetValue("background-color", out var ea) && ea.To is Color eac
                        ? eac : ns.Last.BackgroundColor;
                    if (!ColorEquals(lastTarget, style.BackgroundColor))
                    {
                        ns.Active["background-color"] = new Active
                        {
                            Property = "background-color",
                            StartTime = now, Duration = duration, Delay = delay, Timing = timing,
                            From = ns.Last.BackgroundColor,
                            To = style.BackgroundColor,
                        };
                    }
                }
                if (IsTransitionable(props, "color"))
                {
                    var lastTarget = ns.Active.TryGetValue("color", out var ea) && ea.To is Color eac
                        ? eac : ns.Last.Color;
                    if (!ColorEquals(lastTarget, style.Color))
                    {
                        ns.Active["color"] = new Active
                        {
                            Property = "color",
                            StartTime = now, Duration = duration, Delay = delay, Timing = timing,
                            From = ns.Last.Color,
                            To = style.Color,
                        };
                    }
                }
                if (IsTransitionable(props, "opacity"))
                {
                    var lastTarget = ns.Active.TryGetValue("opacity", out var ea) && ea.To is float eao
                        ? eao : ns.Last.Opacity;
                    if (Math.Abs(lastTarget - style.Opacity) > 1e-4f)
                    {
                        ns.Active["opacity"] = new Active
                        {
                            Property = "opacity",
                            StartTime = now, Duration = duration, Delay = delay, Timing = timing,
                            From = ns.Last.Opacity,
                            To = style.Opacity,
                        };
                    }
                }
                if (IsTransitionable(props, "transform"))
                {
                    var newMat = style.Transform is { Count: > 0 }
                        ? TransformMatrix.Compose(style.Transform.Select(f => f.ToMatrix()))
                        : TransformMatrix.Identity();
                    var lastTargetMat = ns.Active.TryGetValue("transform", out var ea) && ea.To is TransformMatrix eam
                        ? eam
                        : (ns.Last.Transform is { Count: > 0 }
                            ? TransformMatrix.Compose(ns.Last.Transform.Select(f => f.ToMatrix()))
                            : TransformMatrix.Identity());
                    if (!MatrixEquals(lastTargetMat, newMat))
                    {
                        var fromMat = ns.Last.Transform is { Count: > 0 }
                            ? TransformMatrix.Compose(ns.Last.Transform.Select(f => f.ToMatrix()))
                            : TransformMatrix.Identity();
                        ns.Active["transform"] = new Active
                        {
                            Property = "transform",
                            StartTime = now, Duration = duration, Delay = delay, Timing = timing,
                            From = fromMat,
                            To = newMat,
                        };
                    }
                }
            }

            // Evaluate active transitions: apply interpolated values and drop finished ones.
            if (ns.Active.Count > 0)
            {
                var finished = new List<string>();
                foreach (var kv in ns.Active)
                {
                    var t = kv.Value;
                    float elapsed = now - t.StartTime - t.Delay;
                    if (elapsed < 0)
                    {
                        ApplyValue(style, t.Property, t.From);
                        anyActive = true;
                        continue;
                    }
                    if (elapsed >= t.Duration)
                    {
                        ApplyValue(style, t.Property, t.To);
                        finished.Add(kv.Key);
                        continue;
                    }

                    float progress = t.Duration > 0 ? elapsed / t.Duration : 1;
                    float eased = t.Timing.Evaluate(progress);
                    object interp = Interpolate(t.From, t.To, eased);
                    ApplyValue(style, t.Property, interp);
                    anyActive = true;
                }
                foreach (var key in finished) ns.Active.Remove(key);
            }

            // Snapshot the style AFTER interpolation so next frame's "from" is the
            // currently-displayed value (handles mid-transition reversal correctly).
            ns.Last = new Snapshot
            {
                BackgroundColor = style.BackgroundColor,
                Color = style.Color,
                Opacity = style.Opacity,
                Transform = style.Transform,
            };
        }

        return anyActive;
    }

    private static HashSet<string> ParseTransitionProperty(string? raw)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(raw))
        {
            set.Add("all");
            return set;
        }
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            set.Add(part);
        return set;
    }

    private static bool IsTransitionable(HashSet<string> props, string prop)
        => props.Contains("all") || props.Contains(prop);

    private static bool ColorEquals(Color a, Color b)
        => Math.Abs(a.R - b.R) < 1e-4f && Math.Abs(a.G - b.G) < 1e-4f
        && Math.Abs(a.B - b.B) < 1e-4f && Math.Abs(a.A - b.A) < 1e-4f;

    private static bool TransformsEqual(List<TransformFunction>? a, List<TransformFunction>? b)
    {
        var am = a is { Count: > 0 } ? TransformMatrix.Compose(a.Select(f => f.ToMatrix())) : TransformMatrix.Identity();
        var bm = b is { Count: > 0 } ? TransformMatrix.Compose(b.Select(f => f.ToMatrix())) : TransformMatrix.Identity();
        return MatrixEquals(am, bm);
    }

    private static bool MatrixEquals(TransformMatrix a, TransformMatrix b)
    {
        for (int i = 0; i < 16; i++)
            if (Math.Abs(a.Elements[i] - b.Elements[i]) > 1e-4f) return false;
        return true;
    }

    private static object Interpolate(object from, object to, float t)
    {
        if (from is Color cf && to is Color ct)
            return PropertyInterpolation.LerpColor(cf, ct, t);
        if (from is float ff && to is float ft)
            return PropertyInterpolation.LerpFloat(ff, ft, t);
        if (from is TransformMatrix tf && to is TransformMatrix tt)
            return PropertyInterpolation.LerpMatrix(tf, tt, t);
        return to;
    }

    private static void ApplyValue(ComputedStyle style, string property, object value)
    {
        switch (property)
        {
            case "background-color":
                if (value is Color bg) style.BackgroundColor = bg;
                break;
            case "color":
                if (value is Color c) style.Color = c;
                break;
            case "opacity":
                if (value is float o) style.Opacity = o;
                break;
            case "transform":
                if (value is TransformMatrix m)
                {
                    // TransformMatrix.Elements is column-major; Matrix3DFunction.ToMatrix()
                    // treats its input as row-major and transposes it into column-major
                    // storage. Transpose first so the round-trip reproduces the original.
                    var rm = new float[16];
                    for (int row = 0; row < 4; row++)
                        for (int col = 0; col < 4; col++)
                            rm[row * 4 + col] = m.Elements[col * 4 + row];
                    style.Transform = [new Matrix3DFunction(rm)];
                }
                break;
        }
    }
}
