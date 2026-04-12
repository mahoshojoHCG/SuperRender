using SuperRender.Document.Css;

namespace SuperRender.Renderer.Rendering.Style;

/// <summary>
/// Resolves CSS custom properties (--* variables) with var() substitution and cycle detection.
/// Custom properties inherit by default. Values are stored as raw strings and substituted
/// before normal property application.
/// </summary>
internal sealed class CustomPropertyResolver
{
    /// <summary>
    /// Resolves custom properties for an element given its collected declarations and parent properties.
    /// Returns a dictionary of resolved custom property values for this element.
    /// </summary>
    public static Dictionary<string, string> Resolve(
        List<Declaration> declarations,
        Dictionary<string, string>? parentProperties)
    {
        // Start with inherited custom properties from parent
        var properties = parentProperties != null
            ? new Dictionary<string, string>(parentProperties, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        // Collect custom property declarations (--* properties)
        foreach (var decl in declarations)
        {
            if (decl.Property.StartsWith("--", StringComparison.Ordinal))
            {
                properties[decl.Property] = decl.Value.Raw;
            }
        }

        // Resolve var() references in custom property values (they can reference each other)
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        var resolving = new HashSet<string>(StringComparer.Ordinal);

        foreach (var kvp in properties)
        {
            if (!resolved.ContainsKey(kvp.Key))
            {
                resolved[kvp.Key] = ResolveValue(kvp.Value, properties, resolved, resolving);
            }
        }

        return resolved;
    }

    /// <summary>
    /// Substitutes var() references in a CSS value string using the given custom property map.
    /// Returns the substituted string, or the fallback value if the variable is not found.
    /// </summary>
    public static string SubstituteVars(string value, Dictionary<string, string>? customProperties)
    {
        if (customProperties == null || !value.Contains("var(", StringComparison.OrdinalIgnoreCase))
            return value;

        return SubstituteVarsInString(value, customProperties, []);
    }

    /// <summary>
    /// Resolves a CssValue that contains a var() reference.
    /// Returns a new CssValue with the variable substituted.
    /// </summary>
    public static CssValue ResolveVarValue(CssValue value, Dictionary<string, string>? customProperties)
    {
        if (value.VarName == null || customProperties == null)
            return value;

        if (customProperties.TryGetValue(value.VarName, out var resolved) && resolved.Length > 0)
        {
            return CssParser.ParseValueText(resolved);
        }

        if (value.VarFallback != null)
        {
            return CssParser.ParseValueText(value.VarFallback);
        }

        // Variable not found, empty (cyclic), or no fallback — return as keyword (will be ignored)
        return new CssValue { Type = CssValueType.Keyword, Raw = "" };
    }

    private static string ResolveValue(
        string value,
        Dictionary<string, string> allProperties,
        Dictionary<string, string> resolved,
        HashSet<string> resolving)
    {
        if (!value.Contains("var(", StringComparison.OrdinalIgnoreCase))
            return value;

        return SubstituteVarsInString(value, allProperties, resolving);
    }

    private static string SubstituteVarsInString(
        string value,
        Dictionary<string, string> properties,
        HashSet<string> resolving)
    {
        int idx = 0;
        var result = new System.Text.StringBuilder();

        while (idx < value.Length)
        {
            int varStart = value.IndexOf("var(", idx, StringComparison.OrdinalIgnoreCase);
            if (varStart < 0)
            {
                result.Append(value.AsSpan(idx));
                break;
            }

            result.Append(value.AsSpan(idx, varStart - idx));

            // Find matching closing paren
            int depth = 1;
            int contentStart = varStart + 4;
            int i = contentStart;
            while (i < value.Length && depth > 0)
            {
                if (value[i] == '(') depth++;
                else if (value[i] == ')') depth--;
                if (depth > 0) i++;
            }

            string content = value[contentStart..i];
            idx = i < value.Length ? i + 1 : i;

            // Parse var name and fallback
            int commaIdx = FindTopLevelComma(content);
            string varName = (commaIdx >= 0 ? content[..commaIdx] : content).Trim();
            string? fallback = commaIdx >= 0 ? content[(commaIdx + 1)..].Trim() : null;

            // Cycle detection
            if (resolving.Contains(varName))
            {
                // Cyclic reference — use fallback or empty
                result.Append(fallback ?? "");
                continue;
            }

            if (properties.TryGetValue(varName, out var propValue))
            {
                resolving.Add(varName);
                string resolvedVal = SubstituteVarsInString(propValue, properties, resolving);
                resolving.Remove(varName);
                result.Append(resolvedVal);
            }
            else if (fallback != null)
            {
                result.Append(SubstituteVarsInString(fallback, properties, resolving));
            }
            // else: variable not found, no fallback → omit
        }

        return result.ToString();
    }

    private static int FindTopLevelComma(string s)
    {
        int depth = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '(') depth++;
            else if (s[i] == ')') depth--;
            else if (s[i] == ',' && depth == 0) return i;
        }
        return -1;
    }
}
