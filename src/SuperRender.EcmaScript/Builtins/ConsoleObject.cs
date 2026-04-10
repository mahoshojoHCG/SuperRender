namespace SuperRender.EcmaScript.Builtins;

using System.Diagnostics;
using System.Globalization;
using SuperRender.EcmaScript.Runtime;

public static class ConsoleObject
{
    private static TextWriter _output = Console.Out;
    private static TextWriter _errorOutput = Console.Error;
    private static readonly Dictionary<string, Stopwatch> Timers = new(StringComparer.Ordinal);

    public static void SetOutput(TextWriter output)
    {
        _output = output;
    }

    public static void SetErrorOutput(TextWriter errorOutput)
    {
        _errorOutput = errorOutput;
    }

    public static void Install(Realm realm)
    {
        var console = new JsObject { Prototype = realm.ObjectPrototype };

        BuiltinHelper.DefineMethod(console, "log", (_, args) =>
        {
            WriteFormatted(_output, args);
            return JsValue.Undefined;
        }, 0);

        BuiltinHelper.DefineMethod(console, "info", (_, args) =>
        {
            WriteFormatted(_output, args);
            return JsValue.Undefined;
        }, 0);

        BuiltinHelper.DefineMethod(console, "debug", (_, args) =>
        {
            WriteFormatted(_output, args);
            return JsValue.Undefined;
        }, 0);

        BuiltinHelper.DefineMethod(console, "warn", (_, args) =>
        {
            WriteFormatted(_errorOutput, args);
            return JsValue.Undefined;
        }, 0);

        BuiltinHelper.DefineMethod(console, "error", (_, args) =>
        {
            WriteFormatted(_errorOutput, args);
            return JsValue.Undefined;
        }, 0);

        BuiltinHelper.DefineMethod(console, "dir", (_, args) =>
        {
            if (args.Length > 0)
            {
                _output.WriteLine(Inspect(args[0], 2));
            }

            return JsValue.Undefined;
        }, 1);

        BuiltinHelper.DefineMethod(console, "time", (_, args) =>
        {
            var label = args.Length > 0 ? args[0].ToJsString() : "default";
            if (!Timers.ContainsKey(label))
            {
                Timers[label] = Stopwatch.StartNew();
            }

            return JsValue.Undefined;
        }, 0);

        BuiltinHelper.DefineMethod(console, "timeEnd", (_, args) =>
        {
            var label = args.Length > 0 ? args[0].ToJsString() : "default";
            if (Timers.Remove(label, out var sw))
            {
                sw.Stop();
                _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}: {1:F3}ms", label, sw.Elapsed.TotalMilliseconds));
            }
            else
            {
                _errorOutput.WriteLine(string.Format(CultureInfo.InvariantCulture, "Timer '{0}' does not exist", label));
            }

            return JsValue.Undefined;
        }, 0);

        BuiltinHelper.DefineMethod(console, "assert", (_, args) =>
        {
            var condition = args.Length > 0 && args[0].ToBoolean();
            if (!condition)
            {
                var msg = args.Length > 1 ? args[1].ToJsString() : "Assertion failed";
                _errorOutput.WriteLine("Assertion failed: " + msg);
            }

            return JsValue.Undefined;
        }, 1);

        BuiltinHelper.DefineMethod(console, "clear", (_, _) =>
        {
            return JsValue.Undefined;
        }, 0);

        realm.InstallGlobal("console", console);
    }

    private static void WriteFormatted(TextWriter writer, JsValue[] args)
    {
        if (args.Length == 0)
        {
            writer.WriteLine();
            return;
        }

        var parts = new string[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            parts[i] = FormatValue(args[i]);
        }

        writer.WriteLine(string.Join(' ', parts));
    }

    private static string FormatValue(JsValue value)
    {
        if (value is JsUndefined)
        {
            return "undefined";
        }

        if (value is JsNull)
        {
            return "null";
        }

        if (value is JsString s)
        {
            return s.Value;
        }

        if (value is JsArray arr)
        {
            return FormatArray(arr);
        }

        if (value is JsFunction fn)
        {
            return "[Function: " + fn.Name + "]";
        }

        if (value is JsObject)
        {
            return Inspect(value, 1);
        }

        return value.ToJsString();
    }

    private static string FormatArray(JsArray arr)
    {
        var len = arr.DenseLength;
        var parts = new string[len];
        for (var i = 0; i < len; i++)
        {
            var item = arr.GetIndex(i);
            parts[i] = item is JsString str ? "'" + str.Value + "'" : FormatValue(item);
        }

        return "[ " + string.Join(", ", parts) + " ]";
    }

    private static string Inspect(JsValue value, int depth)
    {
        if (depth <= 0 || value is not JsObject obj)
        {
            return value.ToJsString();
        }

        var keys = obj.OwnPropertyKeys().ToArray();
        if (keys.Length == 0)
        {
            return "{}";
        }

        var entries = new string[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            var v = obj.Get(keys[i]);
            var formatted = v is JsString str ? "'" + str.Value + "'" : Inspect(v, depth - 1);
            entries[i] = keys[i] + ": " + formatted;
        }

        return "{ " + string.Join(", ", entries) + " }";
    }
}
