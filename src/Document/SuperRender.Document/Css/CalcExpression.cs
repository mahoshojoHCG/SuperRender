namespace SuperRender.Document.Css;

/// <summary>
/// AST for CSS math functions: calc(), min(), max(), clamp(), round(), abs(), sign(),
/// trigonometric functions, pow(), sqrt(), hypot(), log(), exp(), mod(), rem().
/// </summary>
public abstract class CalcNode
{
    /// <summary>
    /// Evaluates this node to a pixel value.
    /// </summary>
    public abstract double Evaluate(CalcContext context);
}

/// <summary>
/// A leaf value in a calc expression (e.g., 10px, 50%, 2em).
/// </summary>
public sealed class CalcValueNode : CalcNode
{
    public CssValue Value { get; }

    public CalcValueNode(CssValue value) => Value = value;

    public override double Evaluate(CalcContext context)
    {
        return Value.Type switch
        {
            CssValueType.Length => Value.Unit?.ToLowerInvariant() switch
            {
                "px" => Value.NumericValue,
                "em" => Value.NumericValue * context.FontSize,
                "rem" => Value.NumericValue * 16.0,
                "pt" => Value.NumericValue * 1.333,
                "vw" => Value.NumericValue * context.ViewportWidth / 100.0,
                "vh" => Value.NumericValue * context.ViewportHeight / 100.0,
                "vmin" => Value.NumericValue * Math.Min(context.ViewportWidth, context.ViewportHeight) / 100.0,
                "vmax" => Value.NumericValue * Math.Max(context.ViewportWidth, context.ViewportHeight) / 100.0,
                // Absolute units
                "cm" => Value.NumericValue * 96.0 / 2.54,
                "mm" => Value.NumericValue * 96.0 / 25.4,
                "in" => Value.NumericValue * 96.0,
                "pc" => Value.NumericValue * 96.0 / 6.0,
                "q" => Value.NumericValue * 96.0 / 101.6,
                // Font-relative
                "ex" => Value.NumericValue * context.FontSize * 0.5,
                "ch" => Value.NumericValue * context.FontSize * 0.5,
                "lh" => Value.NumericValue * context.FontSize * context.LineHeight,
                "rlh" => Value.NumericValue * 16.0 * context.RootLineHeight,
                "cap" => Value.NumericValue * context.FontSize * 0.7,
                "ic" => Value.NumericValue * context.FontSize,
                // Dynamic viewport
                "dvw" => Value.NumericValue * context.ViewportWidth / 100.0,
                "dvh" => Value.NumericValue * context.ViewportHeight / 100.0,
                "dvmin" => Value.NumericValue * Math.Min(context.ViewportWidth, context.ViewportHeight) / 100.0,
                "dvmax" => Value.NumericValue * Math.Max(context.ViewportWidth, context.ViewportHeight) / 100.0,
                // Small viewport
                "svw" => Value.NumericValue * context.SmallViewportWidth / 100.0,
                "svh" => Value.NumericValue * context.SmallViewportHeight / 100.0,
                "svmin" => Value.NumericValue * Math.Min(context.SmallViewportWidth, context.SmallViewportHeight) / 100.0,
                "svmax" => Value.NumericValue * Math.Max(context.SmallViewportWidth, context.SmallViewportHeight) / 100.0,
                // Large viewport
                "lvw" => Value.NumericValue * context.LargeViewportWidth / 100.0,
                "lvh" => Value.NumericValue * context.LargeViewportHeight / 100.0,
                "lvmin" => Value.NumericValue * Math.Min(context.LargeViewportWidth, context.LargeViewportHeight) / 100.0,
                "lvmax" => Value.NumericValue * Math.Max(context.LargeViewportWidth, context.LargeViewportHeight) / 100.0,
                _ => Value.NumericValue
            },
            CssValueType.Percentage => Value.NumericValue * context.ContainingBlockSize / 100.0,
            CssValueType.Number => Value.NumericValue,
            CssValueType.Angle => Value.Unit?.ToLowerInvariant() switch
            {
                "deg" => Value.NumericValue,
                "grad" => Value.NumericValue * 360.0 / 400.0,
                "rad" => Value.NumericValue * 180.0 / Math.PI,
                "turn" => Value.NumericValue * 360.0,
                _ => Value.NumericValue
            },
            CssValueType.Time => Value.Unit?.ToLowerInvariant() switch
            {
                "s" => Value.NumericValue * 1000.0,
                "ms" => Value.NumericValue,
                _ => Value.NumericValue
            },
            _ => 0
        };
    }
}

/// <summary>
/// A binary operation: left op right (e.g., 50% - 10px).
/// </summary>
public sealed class CalcBinaryNode : CalcNode
{
    public CalcNode Left { get; }
    public CalcOp Op { get; }
    public CalcNode Right { get; }

    public CalcBinaryNode(CalcNode left, CalcOp op, CalcNode right)
    {
        Left = left;
        Op = op;
        Right = right;
    }

    public override double Evaluate(CalcContext context)
    {
        double l = Left.Evaluate(context);
        double r = Right.Evaluate(context);
        return Op switch
        {
            CalcOp.Add => l + r,
            CalcOp.Sub => l - r,
            CalcOp.Mul => l * r,
            CalcOp.Div => r != 0 ? l / r : 0,
            _ => 0
        };
    }
}

/// <summary>
/// A min(), max(), or clamp() function.
/// </summary>
public sealed class CalcMinMaxNode : CalcNode
{
    public CalcMinMaxType Type { get; }
    public List<CalcNode> Args { get; }

    public CalcMinMaxNode(CalcMinMaxType type, List<CalcNode> args)
    {
        Type = type;
        Args = args;
    }

    public override double Evaluate(CalcContext context)
    {
        if (Args.Count == 0) return 0;

        return Type switch
        {
            CalcMinMaxType.Min => Args.Min(a => a.Evaluate(context)),
            CalcMinMaxType.Max => Args.Max(a => a.Evaluate(context)),
            CalcMinMaxType.Clamp when Args.Count >= 3 =>
                Math.Clamp(Args[1].Evaluate(context), Args[0].Evaluate(context), Args[2].Evaluate(context)),
            _ => Args[0].Evaluate(context)
        };
    }
}

/// <summary>
/// CSS math function node: round(), mod(), rem(), abs(), sign(), trig functions,
/// pow(), sqrt(), hypot(), log(), exp().
/// </summary>
public sealed class CalcFunctionNode : CalcNode
{
    public CalcMathFunction Function { get; }
    public List<CalcNode> Args { get; }

    public CalcFunctionNode(CalcMathFunction function, List<CalcNode> args)
    {
        Function = function;
        Args = args;
    }

    public override double Evaluate(CalcContext context)
    {
        double Arg(int i) => i < Args.Count ? Args[i].Evaluate(context) : 0;

        return Function switch
        {
            CalcMathFunction.Abs => Math.Abs(Arg(0)),
            CalcMathFunction.Sign => Math.Sign(Arg(0)),
            CalcMathFunction.Round => Args.Count >= 2
                ? Math.Round(Arg(0) / Arg(1)) * Arg(1)
                : Math.Round(Arg(0)),
            CalcMathFunction.Mod => Arg(1) != 0 ? Arg(0) % Arg(1) : 0,
            CalcMathFunction.Rem => Arg(1) != 0 ? Arg(0) - Math.Truncate(Arg(0) / Arg(1)) * Arg(1) : 0,
            CalcMathFunction.Sin => Math.Sin(Arg(0) * Math.PI / 180.0),
            CalcMathFunction.Cos => Math.Cos(Arg(0) * Math.PI / 180.0),
            CalcMathFunction.Tan => Math.Tan(Arg(0) * Math.PI / 180.0),
            CalcMathFunction.Asin => Math.Asin(Arg(0)) * 180.0 / Math.PI,
            CalcMathFunction.Acos => Math.Acos(Arg(0)) * 180.0 / Math.PI,
            CalcMathFunction.Atan => Math.Atan(Arg(0)) * 180.0 / Math.PI,
            CalcMathFunction.Atan2 => Math.Atan2(Arg(0), Arg(1)) * 180.0 / Math.PI,
            CalcMathFunction.Pow => Math.Pow(Arg(0), Arg(1)),
            CalcMathFunction.Sqrt => Math.Sqrt(Arg(0)),
            CalcMathFunction.Hypot => Args.Count switch
            {
                1 => Math.Abs(Arg(0)),
                2 => Math.Sqrt(Arg(0) * Arg(0) + Arg(1) * Arg(1)),
                _ => Math.Sqrt(Args.Sum(a => { double v = a.Evaluate(context); return v * v; }))
            },
            CalcMathFunction.Log => Args.Count >= 2 ? Math.Log(Arg(0), Arg(1)) : Math.Log(Arg(0)),
            CalcMathFunction.Exp => Math.Exp(Arg(0)),
            _ => 0
        };
    }
}

public enum CalcOp { Add, Sub, Mul, Div }
public enum CalcMinMaxType { Min, Max, Clamp }

public enum CalcMathFunction
{
    Abs, Sign,
    Round, Mod, Rem,
    Sin, Cos, Tan, Asin, Acos, Atan, Atan2,
    Pow, Sqrt, Hypot, Log, Exp
}

/// <summary>
/// Context for evaluating calc expressions, providing the values needed to resolve
/// relative units (em, %, vw, vh, etc.).
/// </summary>
public readonly struct CalcContext
{
    public double FontSize { get; init; }
    public double ContainingBlockSize { get; init; }
    public double ViewportWidth { get; init; }
    public double ViewportHeight { get; init; }
    public double LineHeight { get; init; }
    public double RootLineHeight { get; init; }
    public double SmallViewportWidth { get; init; }
    public double SmallViewportHeight { get; init; }
    public double LargeViewportWidth { get; init; }
    public double LargeViewportHeight { get; init; }

    public static CalcContext Default => new()
    {
        FontSize = 16,
        ContainingBlockSize = 0,
        ViewportWidth = 800,
        ViewportHeight = 600,
        LineHeight = 1.2,
        RootLineHeight = 1.2,
        SmallViewportWidth = 800,
        SmallViewportHeight = 600,
        LargeViewportWidth = 800,
        LargeViewportHeight = 600
    };
}
