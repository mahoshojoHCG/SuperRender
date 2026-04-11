using System.Text.RegularExpressions;

namespace SuperRender.Document.Html;

/// <summary>
/// Decodes HTML character references (named entities, decimal numeric, and hex numeric).
/// </summary>
public static partial class HtmlEntities
{
    private const string ReplacementCharacter = "\uFFFD";

    [GeneratedRegex(@"&(\#x([0-9a-fA-F]+)|\#([0-9]+)|([a-zA-Z][a-zA-Z0-9]*));", RegexOptions.Compiled)]
    private static partial Regex EntityPattern();

    /// <summary>
    /// Replaces all HTML entity references in <paramref name="text"/> with their decoded characters.
    /// Supports named entities (&amp;amp;, &amp;lt;, etc.), decimal (&#NNN;) and hex (&#xHHH;) numeric references.
    /// Applies WHATWG-specified overflow and surrogate checking for numeric references.
    /// </summary>
    public static string Decode(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('&'))
            return text;

        return EntityPattern().Replace(text, match =>
        {
            // &#xHHH; — hex numeric reference
            if (match.Groups[2].Success)
                return DecodeNumericReference(match.Groups[2].Value, isHex: true, match.Value);

            // &#NNN; — decimal numeric reference
            if (match.Groups[3].Success)
                return DecodeNumericReference(match.Groups[3].Value, isHex: false, match.Value);

            // Named entity
            if (match.Groups[4].Success &&
                HtmlEntityTable.NamedEntities.TryGetValue(match.Groups[4].Value, out var decoded))
                return decoded;

            return match.Value; // unknown entity, leave as-is
        });
    }

    /// <summary>
    /// Decodes a numeric character reference with WHATWG-specified validation.
    /// </summary>
    private static string DecodeNumericReference(string digits, bool isHex, string original)
    {
        long codepoint;
        if (isHex)
        {
            if (!long.TryParse(digits, System.Globalization.NumberStyles.HexNumber, null, out codepoint))
                return original;
        }
        else
        {
            if (!long.TryParse(digits, out codepoint))
                return original;
        }

        // Null character → replacement character
        if (codepoint == 0)
            return ReplacementCharacter;

        // Surrogates 0xD800-0xDFFF → replacement character
        if (codepoint >= 0xD800 && codepoint <= 0xDFFF)
            return ReplacementCharacter;

        // Codepoints > 0x10FFFF → replacement character
        if (codepoint > 0x10FFFF)
            return ReplacementCharacter;

        return char.ConvertFromUtf32((int)codepoint);
    }
}
