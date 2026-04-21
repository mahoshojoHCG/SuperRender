using System.Globalization;
using System.Text;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Node.js `querystring` module: parse, stringify, escape, unescape.
/// </summary>
[JsObject]
public sealed partial class QueryStringModule : JsObject
{
    public QueryStringModule(Realm realm)
    {
        Prototype = realm.ObjectPrototype;
    }

    public static QueryStringModule Create(Realm realm) => new(realm);

#pragma warning disable JSGEN005, JSGEN006, JSGEN007 // legacy variadic: Node.js querystring API — optional sep/eq/options
    [JsMethod("parse")]
    public static JsValue ParseMethod(JsValue _, JsValue[] args)
    {
        var input = args.Length > 0 && args[0] is JsString s ? s.Value : "";
        var sep = args.Length > 1 && args[1] is JsString s1 ? s1.Value : "&";
        var eq = args.Length > 2 && args[2] is JsString s2 ? s2.Value : "=";
        return Parse(input, sep, eq);
    }

    [JsMethod("stringify")]
    public static JsValue StringifyMethod(JsValue _, JsValue[] args)
    {
        var o = args.Length > 0 && args[0] is JsDynamicObject j ? j : null;
        var sep = args.Length > 1 && args[1] is JsString s1 ? s1.Value : "&";
        var eq = args.Length > 2 && args[2] is JsString s2 ? s2.Value : "=";
        return new JsString(Stringify(o, sep, eq));
    }

    [JsMethod("encode")]
    public static JsValue EncodeMethod(JsValue thisArg, JsValue[] args) => StringifyMethod(thisArg, args);

    [JsMethod("decode")]
    public static JsValue DecodeMethod(JsValue thisArg, JsValue[] args) => ParseMethod(thisArg, args);
#pragma warning restore JSGEN005, JSGEN006, JSGEN007

    [JsMethod("escape")]
    public static string Escape(string v) => Uri.EscapeDataString(v);

    [JsMethod("unescape")]
    public static string UnescapeMethod(string v) => Unescape(v);

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
