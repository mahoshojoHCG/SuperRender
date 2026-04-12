using System.Globalization;
using SuperRender.Document.Css;

namespace SuperRender.Renderer.Rendering.Style;

public sealed partial class StyleResolver
{
    private bool ApplyTransformProperty(ComputedStyle style, string prop, CssValue value, ComputedStyle? parentStyle)
    {
        switch (prop)
        {
            case CssPropertyNames.Transform:
                style.Transform = ParseTransformFunctions(value.Raw);
                break;

            case CssPropertyNames.TransformOrigin:
                ParseTransformOrigin(value.Raw, out float ox, out float oy);
                style.TransformOriginX = ox;
                style.TransformOriginY = oy;
                break;

            case CssPropertyNames.TransformStyle:
                style.TransformStyle = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "preserve-3d" => "preserve-3d",
                    _ => "flat",
                };
                break;

            case CssPropertyNames.Perspective:
                if (value.Raw.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
                    style.Perspective = float.NaN;
                else
                    style.Perspective = ResolveLength(value, parentStyle);
                break;

            case CssPropertyNames.BackfaceVisibility:
                style.BackfaceVisibility = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "hidden" => "hidden",
                    _ => "visible",
                };
                break;

            default:
                return false;
        }
        return true;
    }

    public static List<TransformFunction>? ParseTransformFunctions(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(trimmed))
            return null;

        var functions = new List<TransformFunction>();
        int pos = 0;

        while (pos < trimmed.Length)
        {
            // Skip whitespace
            while (pos < trimmed.Length && char.IsWhiteSpace(trimmed[pos])) pos++;
            if (pos >= trimmed.Length) break;

            // Read function name
            int nameStart = pos;
            while (pos < trimmed.Length && trimmed[pos] != '(') pos++;
            if (pos >= trimmed.Length) break;

            string funcName = trimmed[nameStart..pos].Trim().ToLowerInvariant();
            pos++; // skip '('

            // Read arguments until matching ')'
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

            var func = CreateTransformFunction(funcName, args);
            if (func != null)
                functions.Add(func);
        }

        return functions.Count > 0 ? functions : null;
    }

    private static TransformFunction? CreateTransformFunction(string name, string args)
    {
        var parts = SplitArgs(args);

        switch (name)
        {
            case "translatex":
                return parts.Length >= 1 ? new TranslateXFunction(ParseLengthArg(parts[0])) : null;

            case "translatey":
                return parts.Length >= 1 ? new TranslateYFunction(ParseLengthArg(parts[0])) : null;

            case "translate":
                if (parts.Length >= 2)
                    return new TranslateFunction(ParseLengthArg(parts[0]), ParseLengthArg(parts[1]));
                if (parts.Length == 1)
                    return new TranslateFunction(ParseLengthArg(parts[0]), 0);
                return null;

            case "translate3d":
                if (parts.Length >= 3)
                    return new Translate3DFunction(ParseLengthArg(parts[0]), ParseLengthArg(parts[1]), ParseLengthArg(parts[2]));
                return null;

            case "scalex":
                return parts.Length >= 1 ? new ScaleXFunction(ParseFloatArg(parts[0])) : null;

            case "scaley":
                return parts.Length >= 1 ? new ScaleYFunction(ParseFloatArg(parts[0])) : null;

            case "scale":
                if (parts.Length >= 2)
                    return new ScaleFunction(ParseFloatArg(parts[0]), ParseFloatArg(parts[1]));
                if (parts.Length == 1)
                    return new ScaleFunction(ParseFloatArg(parts[0]));
                return null;

            case "scale3d":
                if (parts.Length >= 3)
                    return new Scale3DFunction(ParseFloatArg(parts[0]), ParseFloatArg(parts[1]), ParseFloatArg(parts[2]));
                return null;

            case "rotate":
                return parts.Length >= 1 ? new RotateFunction(AngleParser.ParseToRadians(parts[0])) : null;

            case "rotatex":
                return parts.Length >= 1 ? new RotateXFunction(AngleParser.ParseToRadians(parts[0])) : null;

            case "rotatey":
                return parts.Length >= 1 ? new RotateYFunction(AngleParser.ParseToRadians(parts[0])) : null;

            case "rotatez":
                return parts.Length >= 1 ? new RotateZFunction(AngleParser.ParseToRadians(parts[0])) : null;

            case "rotate3d":
                if (parts.Length >= 4)
                    return new Rotate3DFunction(ParseFloatArg(parts[0]), ParseFloatArg(parts[1]),
                        ParseFloatArg(parts[2]), AngleParser.ParseToRadians(parts[3]));
                return null;

            case "skewx":
                return parts.Length >= 1 ? new SkewXFunction(AngleParser.ParseToRadians(parts[0])) : null;

            case "skewy":
                return parts.Length >= 1 ? new SkewYFunction(AngleParser.ParseToRadians(parts[0])) : null;

            case "skew":
                if (parts.Length >= 2)
                    // skew(ax, ay) is equivalent to skewX(ax) then skewY(ay) — but as a single function
                    // CSS spec: skew(ax) = skew(ax, 0) = skewX(ax)
                    return new SkewXFunction(AngleParser.ParseToRadians(parts[0]));
                if (parts.Length == 1)
                    return new SkewXFunction(AngleParser.ParseToRadians(parts[0]));
                return null;

            case "matrix":
                if (parts.Length >= 6)
                    return new MatrixFunction(
                        ParseFloatArg(parts[0]), ParseFloatArg(parts[1]),
                        ParseFloatArg(parts[2]), ParseFloatArg(parts[3]),
                        ParseFloatArg(parts[4]), ParseFloatArg(parts[5]));
                return null;

            case "matrix3d":
                if (parts.Length >= 16)
                {
                    var values = new float[16];
                    for (int i = 0; i < 16; i++)
                        values[i] = ParseFloatArg(parts[i]);
                    return new Matrix3DFunction(values);
                }
                return null;

            case "perspective":
                return parts.Length >= 1 ? new PerspectiveFunction(ParseLengthArg(parts[0])) : null;

            default:
                return null;
        }
    }

    private static string[] SplitArgs(string args)
    {
        return args.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static float ParseFloatArg(string arg)
    {
        if (float.TryParse(arg.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
            return val;
        return 0;
    }

    private static float ParseLengthArg(string arg)
    {
        var trimmed = arg.Trim().ToLowerInvariant();
        if (trimmed.EndsWith("px", StringComparison.Ordinal))
        {
            if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out float px))
                return px;
        }
        else if (trimmed.EndsWith("em", StringComparison.Ordinal))
        {
            if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out float em))
                return em * PropertyDefaults.DefaultFontSize;
        }
        else if (trimmed.EndsWith("rem", StringComparison.Ordinal))
        {
            if (float.TryParse(trimmed[..^3], NumberStyles.Float, CultureInfo.InvariantCulture, out float rem))
                return rem * PropertyDefaults.DefaultFontSize;
        }
        else if (trimmed.EndsWith('%'))
        {
            if (float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float pct))
                return pct; // Percentage stored as-is; context determines resolution
        }
        else
        {
            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float bare))
                return bare;
        }
        return 0;
    }

    private static void ParseTransformOrigin(string raw, out float x, out float y)
    {
        x = 50; // default 50%
        y = 50;

        var parts = raw.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        x = ParseOriginComponent(parts[0], true);
        if (parts.Length >= 2)
            y = ParseOriginComponent(parts[1], false);
        else
            y = 50; // If only one value, y defaults to center
    }

    private static float ParseOriginComponent(string value, bool isX)
    {
        return value switch
        {
            "left" => 0,
            "right" => 100,
            "top" => 0,
            "bottom" => 100,
            "center" => 50,
            _ => ParsePercentOrLength(value),
        };
    }

    private static float ParsePercentOrLength(string value)
    {
        if (value.EndsWith('%'))
        {
            if (float.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float pct))
                return pct;
        }
        if (value.EndsWith("px", StringComparison.Ordinal))
        {
            if (float.TryParse(value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out float px))
                return px; // For pixels, stored as absolute value (not percentage)
        }
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float bare))
            return bare;
        return 50;
    }
}
