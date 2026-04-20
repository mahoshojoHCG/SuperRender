using System.Diagnostics;
using System.Globalization;
using SuperRender.Document;
using SuperRender.Document.Css;
using SuperRender.Document.Dom;

namespace SuperRender.Renderer.Rendering.Style;

/// <summary>
/// Drives CSS @keyframes animations across render passes: looks up the active
/// keyframe rule for each styled node, advances time, interpolates between the
/// surrounding keyframes, and overwrites the affected fields on the computed
/// style. Currently supports background-color, color, opacity, and transform.
/// </summary>
public sealed class AnimationController
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<string, List<ParsedKeyframe>> _rules =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Node, NodeTracker> _tracking = new();

    /// <summary>
    /// Viewport size for resolving percentage/calc length values in keyframes.
    /// Used as a best-effort containing-block size; AnimationController runs
    /// before layout so exact containing-block dimensions are unavailable.
    /// </summary>
    public float ViewportWidth { get; set; } = 800f;
    public float ViewportHeight { get; set; } = 600f;

    private sealed class ParsedKeyframe
    {
        public float Offset;
        public Color? BackgroundColor;
        public Color? Color;
        public float? Opacity;
        public TransformMatrix? Transform;
        public CssValue? Left;
        public CssValue? Right;
        public CssValue? Top;
        public CssValue? Bottom;
    }

    private sealed class NodeTracker
    {
        public string? ActiveName;
        public float StartTime;
    }

    public void LoadKeyframes(IEnumerable<Stylesheet> sheets)
    {
        _rules.Clear();
        foreach (var sheet in sheets)
        {
            foreach (var at in sheet.AtRules)
            {
                if (at is CssKeyframesRule kf)
                {
                    var frames = new List<ParsedKeyframe>();
                    foreach (var frame in kf.Keyframes)
                    {
                        foreach (var offset in ParseKeyframeSelectors(frame.Selector))
                        {
                            var parsed = new ParsedKeyframe { Offset = offset };
                            ApplyDeclarations(parsed, frame.Declarations);
                            frames.Add(parsed);
                        }
                    }
                    frames.Sort((a, b) => a.Offset.CompareTo(b.Offset));
                    _rules[kf.Name] = frames;
                }
            }
        }
    }

    private static IEnumerable<float> ParseKeyframeSelectors(string selector)
    {
        foreach (var part in selector.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var lower = part.ToLowerInvariant();
            if (lower == "from") yield return 0f;
            else if (lower == "to") yield return 1f;
            else if (lower.EndsWith('%') && float.TryParse(lower[..^1],
                NumberStyles.Float, CultureInfo.InvariantCulture, out float pct))
            {
                yield return Math.Clamp(pct / 100f, 0f, 1f);
            }
        }
    }

    private static void ApplyDeclarations(ParsedKeyframe frame, IEnumerable<Declaration> decls)
    {
        foreach (var d in decls)
        {
            var prop = d.Property.ToLowerInvariant();
            switch (prop)
            {
                case "background-color":
                    frame.BackgroundColor = ResolveKeyframeColor(d.Value);
                    break;
                case "color":
                    frame.Color = ResolveKeyframeColor(d.Value);
                    break;
                case "opacity":
                    if (d.Value.Type == CssValueType.Number)
                        frame.Opacity = (float)d.Value.NumericValue;
                    break;
                case "transform":
                {
                    var list = StyleResolver.ParseTransformFunctions(d.Value.Raw);
                    if (list is { Count: > 0 })
                    {
                        frame.Transform = TransformMatrix.Compose(list.Select(f => f.ToMatrix()));
                    }
                    else
                    {
                        frame.Transform = TransformMatrix.Identity();
                    }
                    break;
                }
                case "left":   frame.Left = d.Value;   break;
                case "right":  frame.Right = d.Value;  break;
                case "top":    frame.Top = d.Value;    break;
                case "bottom": frame.Bottom = d.Value; break;
            }
        }
    }

    private static Color ResolveKeyframeColor(CssValue value)
    {
        if (value.ColorValue.HasValue) return value.ColorValue.Value;
        if (value.Raw.StartsWith('#')) return Color.FromHex(value.Raw);
        if (Color.TryFromName(value.Raw, out var named)) return named;
        return Color.Black;
    }

    private float ResolveLengthPx(CssValue value, ComputedStyle style, float containingBlock)
    {
        if (value.Type == CssValueType.Calc && value.CalcExpr != null)
        {
            var ctx = new CalcContext
            {
                FontSize = style.FontSize,
                ContainingBlockSize = containingBlock,
                ViewportWidth = ViewportWidth,
                ViewportHeight = ViewportHeight,
                LineHeight = style.LineHeight,
                RootLineHeight = 1.2,
                SmallViewportWidth = ViewportWidth,
                SmallViewportHeight = ViewportHeight,
                LargeViewportWidth = ViewportWidth,
                LargeViewportHeight = ViewportHeight,
            };
            return (float)value.CalcExpr.Evaluate(ctx);
        }
        return value.Type switch
        {
            CssValueType.Length => value.Unit?.ToLowerInvariant() switch
            {
                "px" => (float)value.NumericValue,
                "em" => (float)value.NumericValue * style.FontSize,
                "rem" => (float)value.NumericValue * 16f,
                "vw" => (float)value.NumericValue * ViewportWidth / 100f,
                "vh" => (float)value.NumericValue * ViewportHeight / 100f,
                _ => (float)value.NumericValue,
            },
            CssValueType.Percentage => (float)value.NumericValue / 100f * containingBlock,
            CssValueType.Number => (float)value.NumericValue,
            _ => 0f,
        };
    }

    public bool Apply(Dictionary<Node, ComputedStyle> styles)
    {
        bool any = false;
        float now = (float)_clock.Elapsed.TotalSeconds;

        // Clean up trackers for nodes that no longer carry a matching animation.
        var staleKeys = new List<Node>();
        foreach (var node in _tracking.Keys)
        {
            if (!styles.TryGetValue(node, out var s) || string.IsNullOrWhiteSpace(s.AnimationName))
                staleKeys.Add(node);
        }
        foreach (var k in staleKeys) _tracking.Remove(k);

        foreach (var (node, style) in styles)
        {
            if (string.IsNullOrWhiteSpace(style.AnimationName)
                || style.AnimationDuration <= 0
                || !_rules.TryGetValue(style.AnimationName!, out var frames)
                || frames.Count == 0)
            {
                continue;
            }

            if (!_tracking.TryGetValue(node, out var tracker) || tracker.ActiveName != style.AnimationName)
            {
                tracker = new NodeTracker { ActiveName = style.AnimationName, StartTime = now };
                _tracking[node] = tracker;
            }

            float elapsed = now - tracker.StartTime - style.AnimationDelay;
            if (elapsed < 0) continue;

            bool infinite = float.IsPositiveInfinity(style.AnimationIterationCount);
            float iterationCount = infinite ? float.PositiveInfinity : Math.Max(1f, style.AnimationIterationCount);
            float iteration = elapsed / style.AnimationDuration;
            bool finished = !infinite && iteration >= iterationCount;
            if (finished) iteration = iterationCount;

            float currentIter = iteration % 1f;
            if (currentIter == 0f && iteration > 0f) currentIter = 1f;
            int iterIdx = (int)Math.Floor(iteration);
            if (finished && style.AnimationFillMode is "forwards" or "both")
            {
                // Hold on the last-visible keyframe.
                currentIter = 1f;
            }

            bool reverse = style.AnimationDirection switch
            {
                "reverse" => true,
                "alternate" => (iterIdx % 2) == 1,
                "alternate-reverse" => (iterIdx % 2) == 0,
                _ => false,
            };

            float t = reverse ? 1f - currentIter : currentIter;
            float eased = (style.AnimationTimingFunction ?? TimingFunction.Ease).Evaluate(Math.Clamp(t, 0f, 1f));

            ParsedKeyframe from = frames[0], to = frames[^1];
            for (int i = 0; i < frames.Count - 1; i++)
            {
                if (eased >= frames[i].Offset && eased <= frames[i + 1].Offset)
                {
                    from = frames[i]; to = frames[i + 1]; break;
                }
            }

            float span = to.Offset - from.Offset;
            float localT = span > 0 ? (eased - from.Offset) / span : 0f;

            if (from.BackgroundColor.HasValue && to.BackgroundColor.HasValue)
                style.BackgroundColor = PropertyInterpolation.LerpColor(
                    from.BackgroundColor.Value, to.BackgroundColor.Value, localT);

            if (from.Color.HasValue && to.Color.HasValue)
                style.Color = PropertyInterpolation.LerpColor(
                    from.Color.Value, to.Color.Value, localT);

            if (from.Opacity.HasValue && to.Opacity.HasValue)
                style.Opacity = PropertyInterpolation.LerpFloat(
                    from.Opacity.Value, to.Opacity.Value, localT);

            if (from.Transform != null && to.Transform != null)
            {
                var m = PropertyInterpolation.LerpMatrix(from.Transform, to.Transform, localT);
                // Matrix3DFunction.ToMatrix() treats input as row-major and transposes into
                // column-major storage; TransformMatrix.Elements is column-major, so
                // transpose first so the round-trip reproduces the matrix we just built.
                var rm = new float[16];
                for (int row = 0; row < 4; row++)
                    for (int col = 0; col < 4; col++)
                        rm[row * 4 + col] = m.Elements[col * 4 + row];
                style.Transform = [new Matrix3DFunction(rm)];
            }

            if (from.Left is not null && to.Left is not null)
                style.Left = PropertyInterpolation.LerpFloat(
                    ResolveLengthPx(from.Left, style, ViewportWidth),
                    ResolveLengthPx(to.Left, style, ViewportWidth),
                    localT);
            if (from.Right is not null && to.Right is not null)
                style.Right = PropertyInterpolation.LerpFloat(
                    ResolveLengthPx(from.Right, style, ViewportWidth),
                    ResolveLengthPx(to.Right, style, ViewportWidth),
                    localT);
            if (from.Top is not null && to.Top is not null)
                style.Top = PropertyInterpolation.LerpFloat(
                    ResolveLengthPx(from.Top, style, ViewportHeight),
                    ResolveLengthPx(to.Top, style, ViewportHeight),
                    localT);
            if (from.Bottom is not null && to.Bottom is not null)
                style.Bottom = PropertyInterpolation.LerpFloat(
                    ResolveLengthPx(from.Bottom, style, ViewportHeight),
                    ResolveLengthPx(to.Bottom, style, ViewportHeight),
                    localT);

            if (!finished || style.AnimationFillMode is "forwards" or "both")
                any = true;
        }

        return any;
    }
}
