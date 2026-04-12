namespace SuperRender.Document.Css;

/// <summary>
/// Evaluates CSS @supports conditions.
/// Supports: property checks like (display: flex), and/or/not combinators.
/// </summary>
public sealed class SupportsCondition
{
    private readonly string _condition;

    /// <summary>
    /// Set of CSS properties recognized by the engine. Used to determine
    /// which @supports conditions evaluate to true.
    /// </summary>
    private static readonly HashSet<string> SupportedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "display", "color", "background-color", "margin", "margin-top", "margin-right", "margin-bottom", "margin-left",
        "padding", "padding-top", "padding-right", "padding-bottom", "padding-left",
        "border", "border-width", "border-style", "border-color",
        "border-top-width", "border-right-width", "border-bottom-width", "border-left-width",
        "border-top-color", "border-right-color", "border-bottom-color", "border-left-color",
        "border-top-style", "border-right-style", "border-bottom-style", "border-left-style",
        "border-radius", "border-top-left-radius", "border-top-right-radius",
        "border-bottom-right-radius", "border-bottom-left-radius",
        "width", "height", "min-width", "max-width", "min-height", "max-height",
        "font-size", "font-family", "font-weight", "font-style",
        "text-align", "line-height", "text-decoration", "text-decoration-line", "text-decoration-color",
        "text-transform", "text-overflow", "letter-spacing", "word-spacing",
        "white-space", "word-break", "overflow-wrap", "word-wrap",
        "position", "top", "left", "right", "bottom", "z-index",
        "overflow", "overflow-x", "overflow-y", "visibility", "opacity", "cursor",
        "box-sizing", "list-style-type", "list-style",
        "flex", "flex-direction", "flex-wrap", "flex-flow", "flex-grow", "flex-shrink", "flex-basis",
        "justify-content", "align-items", "align-self", "gap", "row-gap", "column-gap",
        "content",
        "text-indent", "tab-size", "font-variant", "direction", "quotes"
    };

    /// <summary>
    /// Set of display values supported by the engine.
    /// </summary>
    private static readonly HashSet<string> SupportedDisplayValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "block", "inline", "inline-block", "flex", "none"
    };

    public SupportsCondition(string condition)
    {
        _condition = condition.Trim();
    }

    /// <summary>
    /// Evaluates the @supports condition. Returns true if all referenced
    /// CSS features are supported by the engine.
    /// </summary>
    public bool Evaluate()
    {
        if (string.IsNullOrWhiteSpace(_condition))
            return false;
        return EvaluateExpression(_condition.Trim());
    }

    private static bool EvaluateExpression(string expr)
    {
        expr = expr.Trim();
        if (string.IsNullOrEmpty(expr))
            return false;

        // Handle "not" prefix
        if (expr.StartsWith("not ", StringComparison.OrdinalIgnoreCase) ||
            expr.StartsWith("not(", StringComparison.OrdinalIgnoreCase))
        {
            var rest = expr.StartsWith("not(", StringComparison.OrdinalIgnoreCase)
                ? expr.Substring(3).Trim()
                : expr.Substring(4).Trim();
            return !EvaluateExpression(rest);
        }

        // Try to split by top-level "or"
        var orParts = SplitTopLevel(expr, " or ");
        if (orParts.Count > 1)
        {
            foreach (var part in orParts)
            {
                if (EvaluateExpression(part.Trim()))
                    return true;
            }
            return false;
        }

        // Try to split by top-level "and"
        var andParts = SplitTopLevel(expr, " and ");
        if (andParts.Count > 1)
        {
            foreach (var part in andParts)
            {
                if (!EvaluateExpression(part.Trim()))
                    return false;
            }
            return true;
        }

        // Single condition — should be (property: value)
        if (expr.StartsWith('(') && expr.EndsWith(')'))
        {
            var inner = expr[1..^1].Trim();
            // Might be a nested expression
            if (inner.Contains(" and ", StringComparison.OrdinalIgnoreCase) ||
                inner.Contains(" or ", StringComparison.OrdinalIgnoreCase) ||
                inner.StartsWith("not ", StringComparison.OrdinalIgnoreCase))
            {
                return EvaluateExpression(inner);
            }
            return EvaluatePropertyCondition(inner);
        }

        // Bare property condition (unlikely in valid CSS, but handle gracefully)
        return EvaluatePropertyCondition(expr);
    }

    private static bool EvaluatePropertyCondition(string condition)
    {
        var colonIdx = condition.IndexOf(':', StringComparison.Ordinal);
        if (colonIdx < 0)
        {
            // Just a property name — check if it's recognized
            return SupportedProperties.Contains(condition.Trim());
        }

        var property = condition.Substring(0, colonIdx).Trim().ToLowerInvariant();
        var value = condition.Substring(colonIdx + 1).Trim().ToLowerInvariant();

        if (!SupportedProperties.Contains(property))
            return false;

        // Special case: display: grid is not supported
        if (property == "display" && !SupportedDisplayValues.Contains(value))
            return false;

        return true;
    }

    /// <summary>
    /// Splits an expression by a separator keyword at the top level (not inside parentheses).
    /// </summary>
    private static List<string> SplitTopLevel(string expr, string separator)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < expr.Length; i++)
        {
            if (expr[i] == '(') depth++;
            else if (expr[i] == ')') depth--;
            else if (depth == 0 && i + separator.Length <= expr.Length)
            {
                if (expr.Substring(i, separator.Length).Equals(separator, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(expr.Substring(start, i - start));
                    start = i + separator.Length;
                    i += separator.Length - 1; // loop increments
                }
            }
        }

        result.Add(expr.Substring(start));
        return result;
    }
}
