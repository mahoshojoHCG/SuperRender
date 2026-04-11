namespace SuperRender.Core.Style;

/// <summary>
/// Parses CSS font-family values into ordered lists of family names,
/// and identifies CSS generic family keywords.
/// </summary>
public static class FontFamilyParser
{
    private static readonly HashSet<string> GenericFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "serif",
        "sans-serif",
        "monospace",
        "cursive",
        "fantasy",
        "system-ui",
        "ui-serif",
        "ui-sans-serif",
        "ui-monospace",
        "ui-rounded",
        "math",
        "emoji",
        "fangsong",
    };

    /// <summary>
    /// Parses a CSS font-family value string into an ordered list of family names.
    /// Handles comma-separated lists, quoted names, and whitespace.
    /// </summary>
    public static List<string> Parse(string raw)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw))
            return result;

        var current = new System.Text.StringBuilder();
        bool inQuote = false;
        char quoteChar = '\0';

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];

            if (inQuote)
            {
                if (c == quoteChar)
                {
                    inQuote = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"' || c == '\'')
            {
                inQuote = true;
                quoteChar = c;
            }
            else if (c == ',')
            {
                EmitEntry(current, result);
            }
            else
            {
                current.Append(c);
            }
        }

        EmitEntry(current, result);
        return result;
    }

    /// <summary>
    /// Returns true if the name is a CSS generic font family keyword.
    /// </summary>
    public static bool IsGenericFamily(string name)
        => GenericFamilies.Contains(name);

    private static void EmitEntry(System.Text.StringBuilder sb, List<string> result)
    {
        var entry = sb.ToString().Trim();
        sb.Clear();
        if (entry.Length > 0)
            result.Add(entry);
    }
}
