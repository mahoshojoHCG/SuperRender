namespace SuperRender.Document.Css;

/// <summary>
/// AST for CSS math functions: calc(), min(), max(), clamp().
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
                _ => Value.NumericValue
            },
            CssValueType.Percentage => Value.NumericValue * context.ContainingBlockSize / 100.0,
            CssValueType.Number => Value.NumericValue,
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

public enum CalcOp { Add, Sub, Mul, Div }
public enum CalcMinMaxType { Min, Max, Clamp }

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

    public static CalcContext Default => new()
    {
        FontSize = 16,
        ContainingBlockSize = 0,
        ViewportWidth = 800,
        ViewportHeight = 600
    };
}
