using System.Globalization;

namespace SuperRender.Document.Css;

/// <summary>
/// Evaluates CSS media queries against viewport dimensions.
/// Supports: all, screen, print, min-width, max-width, min-height, max-height,
/// and, not, only, and comma-separated media query lists.
/// </summary>
public sealed class MediaQuery
{
    private readonly string _query;

    public MediaQuery(string query)
    {
        _query = query.Trim();
    }

    /// <summary>
    /// Evaluates the media query against the given viewport dimensions.
    /// Media type "screen" always matches, "print" never matches.
    /// </summary>
    public bool Evaluate(float viewportWidth, float viewportHeight)
    {
        if (string.IsNullOrWhiteSpace(_query))
            return true;

        // Handle comma-separated media query list (OR semantics)
        var queries = SplitTopLevelCommas(_query);
        foreach (var q in queries)
        {
            if (EvaluateSingleQuery(q.Trim(), viewportWidth, viewportHeight))
                return true;
        }
        return false;
    }

    private static bool EvaluateSingleQuery(string query, float viewportWidth, float viewportHeight)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        bool negated = false;

        // Strip "only" keyword (has no effect, just for forward compat)
        if (query.StartsWith("only ", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Substring(5).Trim();
        }

        // Handle "not" keyword
        if (query.StartsWith("not ", StringComparison.OrdinalIgnoreCase))
        {
            negated = true;
            query = query.Substring(4).Trim();
        }

        // Split by "and" (case insensitive)
        var parts = SplitByAnd(query);
        bool result = true;

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            if (!EvaluatePart(trimmed, viewportWidth, viewportHeight))
            {
                result = false;
                break;
            }
        }

        return negated ? !result : result;
    }

    private static bool EvaluatePart(string part, float viewportWidth, float viewportHeight)
    {
        // Media type
        if (part.Equals("all", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("screen", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (part.Equals("print", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("speech", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("tty", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Feature query: (feature: value) or (feature)
        if (part.StartsWith('(') && part.EndsWith(')'))
        {
            var inner = part[1..^1].Trim();
            return EvaluateFeature(inner, viewportWidth, viewportHeight);
        }

        // Unknown → true (permissive)
        return true;
    }

    private static bool EvaluateFeature(string feature, float viewportWidth, float viewportHeight)
    {
        // Parse "feature: value" or just "feature"
        var colonIdx = feature.IndexOf(':', StringComparison.Ordinal);
        if (colonIdx < 0)
        {
            // Boolean context: feature without value
            return feature.Trim().ToLowerInvariant() switch
            {
                "color" => true,
                "hover" => true,
                "pointer" => true,
                _ => false
            };
        }

        var name = feature.Substring(0, colonIdx).Trim().ToLowerInvariant();
        var valueStr = feature.Substring(colonIdx + 1).Trim();
        float value = ParseLengthValue(valueStr);

        return name switch
        {
            "min-width" => viewportWidth >= value,
            "max-width" => viewportWidth <= value,
            "min-height" => viewportHeight >= value,
            "max-height" => viewportHeight <= value,
            "width" => Math.Abs(viewportWidth - value) < 0.01f,
            "height" => Math.Abs(viewportHeight - value) < 0.01f,
            "orientation" => valueStr.Trim().Equals("portrait", StringComparison.OrdinalIgnoreCase)
                ? viewportHeight >= viewportWidth
                : viewportWidth > viewportHeight,
            "prefers-color-scheme" => valueStr.Trim().Equals("light", StringComparison.OrdinalIgnoreCase),
            _ => true // Unknown features are permissive
        };
    }

    private static float ParseLengthValue(string value)
    {
        value = value.Trim();
        // Strip units — we support px and unitless
        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - 2).Trim();
        }
        else if (value.EndsWith("em", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - 2).Trim();
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float emVal))
                return emVal * 16f; // Assume 1em = 16px
            return 0;
        }
        else if (value.EndsWith("rem", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - 3).Trim();
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float remVal))
                return remVal * 16f;
            return 0;
        }

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            return result;
        return 0;
    }

    /// <summary>
    /// Splits a media query string by " and " (case insensitive), respecting parentheses.
    /// </summary>
    private static List<string> SplitByAnd(string query)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < query.Length; i++)
        {
            if (query[i] == '(') depth++;
            else if (query[i] == ')') depth--;
            else if (depth == 0 && i + 5 <= query.Length)
            {
                // Check for " and " (case insensitive)
                if (query.Substring(i, 5).Equals(" and ", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(query.Substring(start, i - start));
                    start = i + 5;
                    i += 4; // loop will advance by 1 more
                }
            }
        }

        result.Add(query.Substring(start));
        return result;
    }

    /// <summary>
    /// Splits by top-level commas (not inside parentheses).
    /// </summary>
    private static List<string> SplitTopLevelCommas(string query)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < query.Length; i++)
        {
            if (query[i] == '(') depth++;
            else if (query[i] == ')') depth--;
            else if (depth == 0 && query[i] == ',')
            {
                result.Add(query.Substring(start, i - start));
                start = i + 1;
            }
        }

        result.Add(query.Substring(start));
        return result;
    }
}
