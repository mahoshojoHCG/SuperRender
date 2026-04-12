using System.Globalization;
using SuperRender.Document.Css;

namespace SuperRender.Renderer.Rendering.Style;

public sealed partial class StyleResolver
{
    private static bool ApplyFilterProperty(ComputedStyle style, string prop, CssValue value, ComputedStyle? parentStyle)
    {
        switch (prop)
        {
            case CssPropertyNames.Filter:
                style.Filter = ParseFilterFunctions(value.Raw);
                break;

            case CssPropertyNames.BackdropFilter:
                style.BackdropFilter = ParseFilterFunctions(value.Raw);
                break;

            case CssPropertyNames.MixBlendMode:
                style.MixBlendMode = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "normal" or "multiply" or "screen" or "overlay" or "darken" or "lighten"
                        or "color-dodge" or "color-burn" or "hard-light" or "soft-light"
                        or "difference" or "exclusion" or "hue" or "saturation"
                        or "color" or "luminosity" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.MixBlendMode,
                };
                break;

            case CssPropertyNames.Isolation:
                style.Isolation = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "auto" or "isolate" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.Isolation,
                };
                break;

            case CssPropertyNames.ClipPath:
                style.ClipPath = value.Raw.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : value.Raw.Trim();
                break;

            default:
                return false;
        }
        return true;
    }

    public static List<FilterFunction>? ParseFilterFunctions(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(trimmed))
            return null;

        var functions = new List<FilterFunction>();
        int pos = 0;

        while (pos < trimmed.Length)
        {
            while (pos < trimmed.Length && char.IsWhiteSpace(trimmed[pos])) pos++;
            if (pos >= trimmed.Length) break;

            int nameStart = pos;
            while (pos < trimmed.Length && trimmed[pos] != '(') pos++;
            if (pos >= trimmed.Length) break;

            string funcName = trimmed[nameStart..pos].Trim().ToLowerInvariant();
            pos++; // skip '('

            int depth = 1;
            int argsStart = pos;
            while (pos < trimmed.Length && depth > 0)
            {
                if (trimmed[pos] == '(') depth++;
                else if (trimmed[pos] == ')') depth--;
                if (depth > 0) pos++;
            }

            string args = trimmed[argsStart..pos];
            if (pos < trimmed.Length) pos++; // skip ')'

            var filter = CreateFilterFunction(funcName, args);
            if (filter != null)
                functions.Add(filter);
        }

        return functions.Count > 0 ? functions : null;
    }

    private static FilterFunction? CreateFilterFunction(string name, string args)
    {
        var trimmedArgs = args.Trim();

        switch (name)
        {
            case "blur":
                return new BlurFilter(ParseFilterLengthArg(trimmedArgs));

            case "brightness":
                return new BrightnessFilter(ParseFilterPercentArg(trimmedArgs, 1));

            case "contrast":
                return new ContrastFilter(ParseFilterPercentArg(trimmedArgs, 1));

            case "grayscale":
                return new GrayscaleFilter(ParseFilterPercentArg(trimmedArgs, 0));

            case "hue-rotate":
                return new HueRotateFilter(AngleParser.ParseToRadians(trimmedArgs));

            case "invert":
                return new InvertFilter(ParseFilterPercentArg(trimmedArgs, 0));

            case "opacity":
                return new OpacityFilter(ParseFilterPercentArg(trimmedArgs, 1));

            case "saturate":
                return new SaturateFilter(ParseFilterPercentArg(trimmedArgs, 1));

            case "sepia":
                return new SepiaFilter(ParseFilterPercentArg(trimmedArgs, 0));

            case "drop-shadow":
                return ParseDropShadow(trimmedArgs);

            default:
                return null;
        }
    }

    private static float ParseFilterLengthArg(string value)
    {
        var lower = value.ToLowerInvariant();
        if (lower.EndsWith("px", StringComparison.Ordinal))
        {
            if (float.TryParse(lower[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out float px))
                return px;
        }
        if (float.TryParse(lower, NumberStyles.Float, CultureInfo.InvariantCulture, out float bare))
            return bare;
        return 0;
    }

    private static float ParseFilterPercentArg(string value, float defaultVal)
    {
        var lower = value.Trim().ToLowerInvariant();
        if (lower.EndsWith('%'))
        {
            if (float.TryParse(lower[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float pct))
                return pct / 100f;
        }
        if (float.TryParse(lower, NumberStyles.Float, CultureInfo.InvariantCulture, out float num))
            return num;
        return defaultVal;
    }

    private static DropShadowFilter? ParseDropShadow(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return null;

        float offsetX = ParseFilterLengthArg(parts[0]);
        float offsetY = ParseFilterLengthArg(parts[1]);
        float blur = parts.Length >= 3 ? ParseFilterLengthArg(parts[2]) : 0;
        var color = Document.Color.Black;
        if (parts.Length >= 4 && Document.Color.TryFromName(parts[3], out var namedColor))
            color = namedColor;

        return new DropShadowFilter(offsetX, offsetY, blur, color);
    }
}
