using System.Globalization;
using SuperRender.Document.Css;

namespace SuperRender.Renderer.Rendering.Style;

public sealed partial class StyleResolver
{
    private static bool ApplyAnimationProperty(ComputedStyle style, string prop, CssValue value, ComputedStyle? parentStyle)
    {
        switch (prop)
        {
            case CssPropertyNames.TransitionProperty:
                style.TransitionProperty = value.Raw.Trim().ToLowerInvariant();
                break;

            case CssPropertyNames.TransitionDuration:
                style.TransitionDuration = ParseTimeValue(value.Raw);
                break;

            case CssPropertyNames.TransitionTimingFunction:
                style.TransitionTimingFunction = TimingFunction.Parse(value.Raw);
                break;

            case CssPropertyNames.TransitionDelay:
                style.TransitionDelay = ParseTimeValue(value.Raw);
                break;

            case CssPropertyNames.Transition:
                ParseTransitionShorthand(style, value.Raw);
                break;

            case CssPropertyNames.AnimationName:
                style.AnimationName = value.Raw.Trim();
                break;

            case CssPropertyNames.AnimationDuration:
                style.AnimationDuration = ParseTimeValue(value.Raw);
                break;

            case CssPropertyNames.AnimationTimingFunction:
                style.AnimationTimingFunction = TimingFunction.Parse(value.Raw);
                break;

            case CssPropertyNames.AnimationDelay:
                style.AnimationDelay = ParseTimeValue(value.Raw);
                break;

            case CssPropertyNames.AnimationIterationCount:
            {
                var raw = value.Raw.Trim().ToLowerInvariant();
                if (raw == "infinite")
                    style.AnimationIterationCount = float.PositiveInfinity;
                else if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float count))
                    style.AnimationIterationCount = count;
                break;
            }

            case CssPropertyNames.AnimationDirection:
                style.AnimationDirection = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "normal" or "reverse" or "alternate" or "alternate-reverse" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.AnimationDirection
                };
                break;

            case CssPropertyNames.AnimationFillMode:
                style.AnimationFillMode = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "none" or "forwards" or "backwards" or "both" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.AnimationFillMode
                };
                break;

            case CssPropertyNames.AnimationPlayState:
                style.AnimationPlayState = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "running" or "paused" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.AnimationPlayState
                };
                break;

            case CssPropertyNames.Animation:
                ParseAnimationShorthand(style, value.Raw);
                break;

            default:
                return false;
        }
        return true;
    }

    internal static float ParseTimeValue(string raw)
    {
        var trimmed = raw.Trim().ToLowerInvariant();
        if (trimmed.EndsWith("ms", StringComparison.Ordinal))
        {
            if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ms))
                return ms / 1000f;
        }
        else if (trimmed.EndsWith('s'))
        {
            if (float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float s))
                return s;
        }
        else if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float bare))
        {
            return bare;
        }
        return 0;
    }

    private static void ParseTransitionShorthand(ComputedStyle style, string raw)
    {
        var parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var lower = part.ToLowerInvariant();
            if (lower.EndsWith('s') || lower.EndsWith("ms", StringComparison.Ordinal))
            {
                float time = ParseTimeValue(lower);
                if (style.TransitionDuration == 0)
                    style.TransitionDuration = time;
                else
                    style.TransitionDelay = time;
            }
            else if (lower is "ease" or "linear" or "ease-in" or "ease-out" or "ease-in-out"
                     || lower.StartsWith("cubic-bezier(", StringComparison.Ordinal)
                     || lower.StartsWith("steps(", StringComparison.Ordinal))
            {
                style.TransitionTimingFunction = TimingFunction.Parse(lower);
            }
            else if (lower != "all")
            {
                style.TransitionProperty = lower;
            }
        }
    }

    private static void ParseAnimationShorthand(ComputedStyle style, string raw)
    {
        var parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool durationSet = false;
        foreach (var part in parts)
        {
            var lower = part.ToLowerInvariant();
            if (lower.EndsWith('s') || lower.EndsWith("ms", StringComparison.Ordinal))
            {
                float time = ParseTimeValue(lower);
                if (!durationSet)
                {
                    style.AnimationDuration = time;
                    durationSet = true;
                }
                else
                {
                    style.AnimationDelay = time;
                }
            }
            else if (lower is "ease" or "linear" or "ease-in" or "ease-out" or "ease-in-out")
            {
                style.AnimationTimingFunction = TimingFunction.Parse(lower);
            }
            else if (lower == "infinite")
            {
                style.AnimationIterationCount = float.PositiveInfinity;
            }
            else if (lower is "normal" or "reverse" or "alternate" or "alternate-reverse")
            {
                style.AnimationDirection = lower;
            }
            else if (lower is "forwards" or "backwards" or "both")
            {
                style.AnimationFillMode = lower;
            }
            else if (lower is "running" or "paused")
            {
                style.AnimationPlayState = lower;
            }
            else if (lower != "none")
            {
                style.AnimationName = part; // preserve case for animation name
            }
        }
    }
}
