namespace SuperRender.EcmaScript.Runtime.Builtins;

using System.Globalization;
using System.Text;
using System.Text.Json;
using SuperRender.EcmaScript.Runtime;

[JsObject]
public sealed partial class JsonObject : JsObject
{
    private static readonly JsString ToStringTagValue = new("JSON");

    public JsonObject(Realm realm)
    {
        Prototype = realm.ObjectPrototype;
        Extensible = false;
    }

    public static void Install(Realm realm) => realm.InstallGlobal("JSON", new JsonObject(realm));

    public override bool TryGetSymbolProperty(JsSymbol symbol, out JsValue value)
    {
        if (symbol == JsSymbol.ToStringTag)
        {
            value = ToStringTagValue;
            return true;
        }

        return base.TryGetSymbolProperty(symbol, out value);
    }

#pragma warning disable JSGEN005, JSGEN006, JSGEN007 // legacy variadic: reviver/replacer callbacks + optional args
    [JsMethod("parse")]
    public static JsValue Parse(JsValue _, JsValue[] args)
    {
        var text = BuiltinHelper.Arg(args, 0).ToJsString();
        var reviver = BuiltinHelper.Arg(args, 1);

        JsValue result;
        try
        {
            using var doc = JsonDocument.Parse(text);
            result = ConvertElement(doc.RootElement);
        }
        catch (JsonException ex)
        {
            throw new Errors.JsSyntaxError("JSON.parse: " + ex.Message, ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        if (reviver is JsFunction reviverFn)
        {
            var root = new JsDynamicObject();
            root.Set("", result);
            return InternalizeJsonValue(root, "", reviverFn);
        }

        return result;
    }
#pragma warning restore JSGEN005, JSGEN006, JSGEN007

#pragma warning disable JSGEN005 // legacy variadic: reviver/replacer callbacks + optional args
    [JsMethod("stringify")]
    public static JsOptional<string> Stringify(JsValue value, JsValue replacer, JsValue space)
    {
        JsFunction? replacerFn = replacer as JsFunction;
        HashSet<string>? propertyList = null;
        if (replacer is JsArray replacerArr)
        {
            propertyList = [];
            for (var i = 0; i < replacerArr.DenseLength; i++)
            {
                var item = replacerArr.GetIndex(i);
                if (item is JsString s)
                {
                    propertyList.Add(s.Value);
                }
                else if (item is JsNumber)
                {
                    propertyList.Add(item.ToJsString());
                }
            }
        }

        var indent = "";
        if (space is JsNumber spaceNum)
        {
            var count = Math.Min((int)spaceNum.Value, 10);
            if (count > 0)
            {
                indent = new string(' ', count);
            }
        }
        else if (space is JsString spaceStr)
        {
            indent = spaceStr.Value.Length > 10 ? spaceStr.Value[..10] : spaceStr.Value;
        }

        var result = SerializeValue("", value, replacerFn, propertyList, indent, "");
        return result is not null ? JsOptional<string>.Of(result) : JsOptional<string>.Undefined;
    }
#pragma warning restore JSGEN005

    private static JsValue ConvertElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return JsValue.Null;
            case JsonValueKind.True:
                return JsValue.True;
            case JsonValueKind.False:
                return JsValue.False;
            case JsonValueKind.Number:
                return JsNumber.Create(element.GetDouble());
            case JsonValueKind.String:
                return new JsString(element.GetString() ?? "");
            case JsonValueKind.Array:
                var arr = new JsArray();
                foreach (var item in element.EnumerateArray())
                {
                    arr.Push(ConvertElement(item));
                }

                return arr;
            case JsonValueKind.Object:
                var obj = new JsDynamicObject();
                foreach (var prop in element.EnumerateObject())
                {
                    obj.Set(prop.Name, ConvertElement(prop.Value));
                }

                return obj;
            default:
                return JsValue.Undefined;
        }
    }

    private static JsValue InternalizeJsonValue(JsDynamicObject holder, string name, JsFunction reviver)
    {
        var val = holder.Get(name);
        if (val is JsDynamicObject obj)
        {
            if (obj is JsArray arr)
            {
                for (var i = 0; i < arr.DenseLength; i++)
                {
                    var key = i.ToString(CultureInfo.InvariantCulture);
                    var newElement = InternalizeJsonValue(arr, key, reviver);
                    if (newElement is JsUndefined)
                    {
                        arr.Delete(key);
                    }
                    else
                    {
                        arr.Set(key, newElement);
                    }
                }
            }
            else
            {
                foreach (var key in obj.OwnPropertyKeys().ToArray())
                {
                    var newElement = InternalizeJsonValue(obj, key, reviver);
                    if (newElement is JsUndefined)
                    {
                        obj.Delete(key);
                    }
                    else
                    {
                        obj.Set(key, newElement);
                    }
                }
            }
        }

        return reviver.Call(holder, [new JsString(name), val]);
    }

    private static string? SerializeValue(string key, JsValue value, JsFunction? replacerFn,
        HashSet<string>? propertyList, string indent, string currentIndent)
    {
        if (value is JsDynamicObject objWithToJson)
        {
            var toJsonFn = objWithToJson.Get("toJSON");
            if (toJsonFn is JsFunction toJson)
            {
                value = toJson.Call(objWithToJson, [new JsString(key)]);
            }
        }

        if (replacerFn is not null)
        {
            var holder = new JsDynamicObject();
            holder.Set(key, value);
            value = replacerFn.Call(holder, [new JsString(key), value]);
        }

        if (value is JsNull)
        {
            return "null";
        }

        if (value is JsBoolean b)
        {
            return b.Value ? "true" : "false";
        }

        if (value is JsNumber num)
        {
            if (double.IsFinite(num.Value))
            {
                return num.ToJsString();
            }

            return "null";
        }

        if (value is JsString str)
        {
            return QuoteString(str.Value);
        }

        if (value is JsArray array)
        {
            return SerializeArray(array, replacerFn, propertyList, indent, currentIndent);
        }

        if (value is JsFunction)
        {
            return null;
        }

        if (value is JsDynamicObject obj)
        {
            return SerializeObject(obj, replacerFn, propertyList, indent, currentIndent);
        }

        if (value is JsUndefined || value is JsSymbol)
        {
            return null;
        }

        return value.ToJsString();
    }

    private static string SerializeArray(JsArray array, JsFunction? replacerFn,
        HashSet<string>? propertyList, string indent, string currentIndent)
    {
        if (array.DenseLength == 0)
        {
            return "[]";
        }

        var nextIndent = currentIndent + indent;
        var sb = new StringBuilder();
        sb.Append('[');

        for (var i = 0; i < array.DenseLength; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            if (indent.Length > 0)
            {
                sb.Append('\n').Append(nextIndent);
            }

            var serialized = SerializeValue(i.ToString(CultureInfo.InvariantCulture),
                array.GetIndex(i), replacerFn, propertyList, indent, nextIndent);
            sb.Append(serialized ?? "null");
        }

        if (indent.Length > 0)
        {
            sb.Append('\n').Append(currentIndent);
        }

        sb.Append(']');
        return sb.ToString();
    }

    private static string SerializeObject(JsDynamicObject obj, JsFunction? replacerFn,
        HashSet<string>? propertyList, string indent, string currentIndent)
    {
        var keys = propertyList is not null
            ? propertyList
            : new HashSet<string>(obj.OwnPropertyKeys(), StringComparer.Ordinal);

        var nextIndent = currentIndent + indent;
        var sb = new StringBuilder();
        sb.Append('{');
        var first = true;

        foreach (var key in keys)
        {
            var desc = obj.GetOwnProperty(key);
            if (desc is null || desc.Enumerable != true)
            {
                continue;
            }

            var serialized = SerializeValue(key, obj.Get(key), replacerFn, propertyList, indent, nextIndent);
            if (serialized is null)
            {
                continue;
            }

            if (!first)
            {
                sb.Append(',');
            }

            if (indent.Length > 0)
            {
                sb.Append('\n').Append(nextIndent);
            }

            sb.Append(QuoteString(key));
            sb.Append(indent.Length > 0 ? ": " : ":");
            sb.Append(serialized);
            first = false;
        }

        if (!first && indent.Length > 0)
        {
            sb.Append('\n').Append(currentIndent);
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string QuoteString(string s)
    {
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }
}
