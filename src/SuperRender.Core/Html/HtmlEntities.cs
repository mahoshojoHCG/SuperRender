using System.Text.RegularExpressions;

namespace SuperRender.Core.Html;

/// <summary>
/// Decodes HTML character references (named entities, decimal numeric, and hex numeric).
/// </summary>
public static partial class HtmlEntities
{
    private static readonly Dictionary<string, string> NamedEntities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["amp"] = "&",
        ["lt"] = "<",
        ["gt"] = ">",
        ["quot"] = "\"",
        ["apos"] = "'",
        ["nbsp"] = "\u00A0",
    };

    [GeneratedRegex(@"&(\#x([0-9a-fA-F]+)|\#([0-9]+)|([a-zA-Z]+));", RegexOptions.Compiled)]
    private static partial Regex EntityPattern();

    /// <summary>
    /// Replaces all HTML entity references in <paramref name="text"/> with their decoded characters.
    /// Supports named entities (&amp;amp;, &amp;lt;, etc.), decimal (&#NNN;) and hex (&#xHHH;) numeric references.
    /// </summary>
    public static string Decode(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('&'))
            return text;

        return EntityPattern().Replace(text, match =>
        {
            // &#xHHH; — hex numeric reference
            if (match.Groups[2].Success)
            {
                if (int.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.HexNumber, null, out var cp) && cp > 0)
                    return char.ConvertFromUtf32(cp);
                return match.Value; // malformed, leave as-is
            }

            // &#NNN; — decimal numeric reference
            if (match.Groups[3].Success)
            {
                if (int.TryParse(match.Groups[3].Value, out var cp) && cp > 0)
                    return char.ConvertFromUtf32(cp);
                return match.Value;
            }

            // Named entity
            if (match.Groups[4].Success && NamedEntities.TryGetValue(match.Groups[4].Value, out var decoded))
                return decoded;

            return match.Value; // unknown entity, leave as-is
        });
    }
}
