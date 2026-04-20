using System.Globalization;
using System.Text;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Node.js `querystring` module: parse, stringify, escape, unescape.
/// </summary>
public static class QueryStringModule
{
    private static PropertyDescriptor MethodDesc(string name, Func<JsValue, JsValue[], JsValue> impl, int length) =>
        PropertyDescriptor.Data(JsFunction.CreateNative(name, impl, length), writable: true, enumerable: false, configurable: true);

    public static JsDynamicObject Create()
    {
        var obj = new JsDynamicObject();
        obj.DefineOwnProperty("parse", MethodDesc("parse", (_, args) =>
        {
            var input = args.Length > 0 && args[0] is JsString s ? s.Value : "";
            var sep = args.Length > 1 && args[1] is JsString s1 ? s1.Value : "&";
            var eq = args.Length > 2 && args[2] is JsString s2 ? s2.Value : "=";
            return Parse(input, sep, eq);
        }, 4));

        obj.DefineOwnProperty("stringify", MethodDesc("stringify", (_, args) =>
        {
            var o = args.Length > 0 && args[0] is JsDynamicObject j ? j : null;
            var sep = args.Length > 1 && args[1] is JsString s1 ? s1.Value : "&";
            var eq = args.Length > 2 && args[2] is JsString s2 ? s2.Value : "=";
            return new JsString(Stringify(o, sep, eq));
        }, 4));

        obj.DefineOwnProperty("escape", MethodDesc("escape", (_, args) =>
        {
            var v = args.Length > 0 ? args[0].ToJsString() : "";
            return new JsString(Uri.EscapeDataString(v));
        }, 1));

        obj.DefineOwnProperty("unescape", MethodDesc("unescape", (_, args) =>
        {
            var v = args.Length > 0 ? args[0].ToJsString() : "";
            return new JsString(Unescape(v));
        }, 1));

        obj.DefineOwnProperty("encode", PropertyDescriptor.Data(obj.Get("stringify")));
        obj.DefineOwnProperty("decode", PropertyDescriptor.Data(obj.Get("parse")));
        return obj;
    }

    internal static JsDynamicObject Parse(string input, string sep, string eq)
    {
        var result = new JsDynamicObject();
        if (string.IsNullOrEmpty(input)) return result;
        var pairs = input.Split(new[] { sep }, StringSplitOptions.None);
        foreach (var pair in pairs)
        {
            if (pair.Length == 0) continue;
            int idx = pair.IndexOf(eq, StringComparison.Ordinal);
            string key, val;
            if (idx < 0) { key = pair; val = ""; }
            else { key = pair[..idx]; val = pair[(idx + eq.Length)..]; }
            key = Unescape(key.Replace('+', ' '));
            val = Unescape(val.Replace('+', ' '));
            var existing = result.Get(key);
            if (existing is JsUndefined)
            {
                result.DefineOwnProperty(key, PropertyDescriptor.Data(new JsString(val)));
            }
            else if (existing is JsArray arr)
            {
                arr.Push(new JsString(val));
            }
            else
            {
                var newArr = new JsArray();
                newArr.Push(existing);
                newArr.Push(new JsString(val));
                result.DefineOwnProperty(key, PropertyDescriptor.Data(newArr));
            }
        }
        return result;
    }

    internal static string Stringify(JsDynamicObject? obj, string sep, string eq)
    {
        if (obj is null) return "";
        var sb = new StringBuilder();
        bool first = true;
        foreach (var key in obj.OwnPropertyKeys())
        {
            var val = obj.Get(key);
            void Write(string v)
            {
                if (!first) sb.Append(sep);
                first = false;
                sb.Append(Uri.EscapeDataString(key)).Append(eq).Append(Uri.EscapeDataString(v));
            }
            if (val is JsArray a)
            {
                for (int i = 0; i < a.DenseLength; i++)
                {
                    var item = a.Get(i.ToString(CultureInfo.InvariantCulture));
                    Write(ScalarToString(item));
                }
            }
            else if (val is not JsUndefined and not JsNull)
            {
                Write(ScalarToString(val));
            }
            else
            {
                Write("");
            }
        }
        return sb.ToString();
    }

    private static string ScalarToString(JsValue v) => v switch
    {
        JsString s => s.Value,
        JsBoolean b => b.Value ? "true" : "false",
        JsNumber n => n.ToJsString(),
        JsUndefined or JsNull => "",
        _ => v.ToJsString(),
    };

    private static string Unescape(string s)
    {
        if (s.IndexOf('%') < 0) return s;
        try { return Uri.UnescapeDataString(s); }
        catch (UriFormatException) { return s; }
    }
}
