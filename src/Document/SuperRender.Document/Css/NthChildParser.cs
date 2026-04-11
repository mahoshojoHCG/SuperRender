namespace SuperRender.Document.Css;

/// <summary>
/// Parses An+B expressions used in :nth-child(), :nth-last-child(), :nth-of-type(), :nth-last-of-type().
/// Supports: "odd", "even", "3", "-n+3", "2n+1", "2n", "-2n+3", "n", "-n", "+3n-1"
/// </summary>
public static class NthChildParser
{
    /// <summary>
    /// Parses an An+B expression and returns true if the given 1-based index matches.
    /// </summary>
    public static bool Matches(string expression, int index)
    {
        var (a, b) = Parse(expression);
        return MatchesAnPlusB(a, b, index);
    }

    public static (int A, int B) Parse(string expression)
    {
        var expr = expression.Trim().ToLowerInvariant();

        if (expr == "odd") return (2, 1);
        if (expr == "even") return (2, 0);

        // Try plain integer
        if (int.TryParse(expr, out int plainInt))
            return (0, plainInt);

        // Parse An+B form
        int a = 0, b = 0;
        int nIdx = expr.IndexOf('n');

        if (nIdx < 0)
        {
            // No 'n', must be just a number
            _ = int.TryParse(expr, out b);
            return (0, b);
        }

        // Parse A (before 'n')
        var aStr = expr[..nIdx].Trim();
        if (aStr is "" or "+")
            a = 1;
        else if (aStr == "-")
            a = -1;
        else
            _ = int.TryParse(aStr, out a);

        // Parse B (after 'n')
        var rest = expr[(nIdx + 1)..].Trim();
        if (rest.Length > 0)
        {
            // Remove whitespace around +/-
            rest = rest.Replace(" ", "");
            _ = int.TryParse(rest, out b);
        }

        return (a, b);
    }

    private static bool MatchesAnPlusB(int a, int b, int index)
    {
        if (a == 0)
            return index == b;

        // index = a*n + b, where n >= 0 (for positive a) or n >= 0 (for negative a, check both)
        // n = (index - b) / a, must be non-negative integer
        int diff = index - b;
        if (diff == 0) return true;
        if (a == 0) return false;
        if (diff % a != 0) return false;
        int n = diff / a;
        return n >= 0;
    }
}
