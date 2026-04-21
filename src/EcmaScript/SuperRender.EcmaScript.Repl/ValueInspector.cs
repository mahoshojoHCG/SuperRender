using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Repl;

/// <summary>
/// Node.js-style value inspector with ANSI color output.
/// </summary>
internal static class ValueInspector
{
    private const int DefaultDepth = 2;
    private const int MaxArrayDisplay = 100;

    // ANSI color codes matching Node.js REPL style.
    private const string Reset = "\x1b[0m";
    private const string Bold = "\x1b[1m";
    private const string Grey = "\x1b[90m";
    private const string Green = "\x1b[32m";
    private const string Yellow = "\x1b[33m";
    private const string Cyan = "\x1b[36m";
    private const string Red = "\x1b[31m";

    private static bool ColorsEnabled { get; } = DetectColorSupport();

    /// <summary>
    /// Format a JsValue for REPL display (with colors if supported).
    /// </summary>
    public static string Inspect(JsValue value)
    {
        return Inspect(value, DefaultDepth);
    }

    private static string Inspect(JsValue value, int depth)
    {
        return value switch
        {
            JsUndefined => Colorize(Grey, "undefined"),
            JsNull => Colorize(Bold, "null"),
            JsBoolean b => Colorize(Yellow, b.Value ? "true" : "false"),
            JsNumber n => InspectNumber(n),
            JsString s => Colorize(Green, "'" + EscapeString(s.Value) + "'"),
            JsSymbol sym => Colorize(Green, sym.ToString()),
            JsRegExp re => Colorize(Red, "/" + re.Pattern + "/" + re.Flags),
            JsArray arr => InspectArray(arr, depth),
            JsFunction fn => InspectFunction(fn),
            JsObject obj => InspectObject(obj, depth),
            _ => value.ToJsString()
        };
    }

    private static string InspectNumber(JsNumber n)
    {
        var text = n.ToJsString();
        // Node.js shows -0 explicitly.
        if (n.Value == 0 && double.IsNegative(n.Value))
        {
            text = "-0";
        }

        return Colorize(Yellow, text);
    }

    private static string InspectFunction(JsFunction fn)
    {
        var name = string.IsNullOrEmpty(fn.Name) ? "(anonymous)" : fn.Name;
        return Colorize(Cyan, "[Function: " + name + "]");
    }

    private static string InspectArray(JsArray arr, int depth)
    {
        var len = arr.DenseLength;
        if (len == 0)
        {
            return "[]";
        }

        if (depth <= 0)
        {
            return "[Array]";
        }

        var parts = new List<string>(Math.Min(len, MaxArrayDisplay));
        var displayed = Math.Min(len, MaxArrayDisplay);
        for (var i = 0; i < displayed; i++)
        {
            parts.Add(Inspect(arr.GetIndex(i), depth - 1));
        }

        if (len > MaxArrayDisplay)
        {
            parts.Add("... " + (len - MaxArrayDisplay) + " more items");
        }

        return "[ " + string.Join(", ", parts) + " ]";
    }

    private static string InspectObject(JsObject obj, int depth)
    {
        var keys = obj.OwnPropertyKeys().ToArray();
        if (keys.Length == 0)
        {
            return "{}";
        }

        if (depth <= 0)
        {
            return "[Object]";
        }

        var entries = new string[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            var v = obj.Get(keys[i]);
            entries[i] = keys[i] + ": " + Inspect(v, depth - 1);
        }

        return "{ " + string.Join(", ", entries) + " }";
    }

    private static string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static string Colorize(string color, string text)
    {
        return ColorsEnabled ? color + text + Reset : text;
    }

    private static bool DetectColorSupport()
    {
        // Respect NO_COLOR convention (https://no-color.org/).
        if (System.Environment.GetEnvironmentVariable("NO_COLOR") is not null)
        {
            return false;
        }

        // Disable colors when stdout is redirected (piped).
        if (!Console.IsOutputRedirected)
        {
            return true;
        }

        // FORCE_COLOR overrides redirect detection.
        return System.Environment.GetEnvironmentVariable("FORCE_COLOR") is not null;
    }
}
