namespace SuperRender.Renderer.Rendering.Style;

/// <summary>
/// CSS timing function for transitions and animations.
/// Supports cubic-bezier, linear, and steps functions.
/// </summary>
public sealed class TimingFunction
{
    public TimingFunctionType Type { get; }
    public float X1 { get; }
    public float Y1 { get; }
    public float X2 { get; }
    public float Y2 { get; }
    public int Steps { get; }
    public string StepPosition { get; }

    private TimingFunction(TimingFunctionType type, float x1, float y1, float x2, float y2, int steps, string stepPosition)
    {
        Type = type;
        X1 = x1; Y1 = y1; X2 = x2; Y2 = y2;
        Steps = steps;
        StepPosition = stepPosition;
    }

    public static TimingFunction Linear => new(TimingFunctionType.CubicBezier, 0, 0, 1, 1, 0, "");
    public static TimingFunction Ease => new(TimingFunctionType.CubicBezier, 0.25f, 0.1f, 0.25f, 1f, 0, "");
    public static TimingFunction EaseIn => new(TimingFunctionType.CubicBezier, 0.42f, 0, 1, 1, 0, "");
    public static TimingFunction EaseOut => new(TimingFunctionType.CubicBezier, 0, 0, 0.58f, 1, 0, "");
    public static TimingFunction EaseInOut => new(TimingFunctionType.CubicBezier, 0.42f, 0, 0.58f, 1, 0, "");

    public static TimingFunction CubicBezier(float x1, float y1, float x2, float y2)
        => new(TimingFunctionType.CubicBezier, x1, y1, x2, y2, 0, "");

    public static TimingFunction CreateSteps(int steps, string position = "end")
        => new(TimingFunctionType.Steps, 0, 0, 0, 0, steps, position);

    /// <summary>Evaluates the timing function at progress t (0..1), returning the output value (0..1).</summary>
    public float Evaluate(float t)
    {
        t = Math.Clamp(t, 0, 1);

        if (Type == TimingFunctionType.Steps)
        {
            if (Steps <= 0) return t;
            float stepSize = 1f / Steps;
            return StepPosition.Equals("start", StringComparison.OrdinalIgnoreCase)
                ? MathF.Ceiling(t * Steps) * stepSize
                : MathF.Floor(t * Steps) * stepSize;
        }

        // Cubic bezier evaluation using binary search
        return EvaluateCubicBezier(t);
    }

    private float EvaluateCubicBezier(float t)
    {
        // Find the parameter u such that bezierX(u) = t
        float u = t;
        for (int i = 0; i < 20; i++)
        {
            float x = BezierValue(u, X1, X2);
            float dx = BezierDerivative(u, X1, X2);
            if (MathF.Abs(x - t) < 1e-6f) break;
            if (MathF.Abs(dx) < 1e-6f) break;
            u -= (x - t) / dx;
            u = Math.Clamp(u, 0, 1);
        }

        return BezierValue(u, Y1, Y2);
    }

    private static float BezierValue(float t, float p1, float p2)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 3 * (1 - t) * (1 - t) * t * p1 + 3 * (1 - t) * t2 * p2 + t3;
    }

    private static float BezierDerivative(float t, float p1, float p2)
    {
        float t2 = t * t;
        return 3 * (1 - t) * (1 - t) * p1 + 6 * (1 - t) * t * (p2 - p1) + 3 * t2 * (1 - p2);
    }

    /// <summary>Parses a CSS timing function string.</summary>
    public static TimingFunction Parse(string raw)
    {
        var trimmed = raw.Trim().ToLowerInvariant();
        return trimmed switch
        {
            "linear" => Linear,
            "ease" => Ease,
            "ease-in" => EaseIn,
            "ease-out" => EaseOut,
            "ease-in-out" => EaseInOut,
            _ when trimmed.StartsWith("cubic-bezier(", StringComparison.Ordinal) => ParseCubicBezier(trimmed),
            _ when trimmed.StartsWith("steps(", StringComparison.Ordinal) => ParseSteps(trimmed),
            _ => Ease,
        };
    }

    private static TimingFunction ParseCubicBezier(string raw)
    {
        var inner = raw["cubic-bezier(".Length..^1];
        var parts = inner.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length >= 4
            && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x1)
            && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y1)
            && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x2)
            && float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y2))
        {
            return CubicBezier(x1, y1, x2, y2);
        }
        return Ease;
    }

    private static TimingFunction ParseSteps(string raw)
    {
        var inner = raw["steps(".Length..^1];
        var parts = inner.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length >= 1 && int.TryParse(parts[0], out int steps))
        {
            string position = parts.Length >= 2 ? parts[1] : "end";
            return CreateSteps(steps, position);
        }
        return CreateSteps(1);
    }
}

public enum TimingFunctionType
{
    CubicBezier,
    Steps,
}
