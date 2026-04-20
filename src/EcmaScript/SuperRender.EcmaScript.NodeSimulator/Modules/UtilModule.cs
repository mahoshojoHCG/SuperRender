using System.Globalization;
using System.Text;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Node.js `util` module. Implements format / inspect / promisify / callbackify and
/// the isDeep* / types.* predicates needed by common library code.
/// </summary>
public static class UtilModule
{
    private static PropertyDescriptor MethodDesc(string name, Func<JsValue, JsValue[], JsValue> impl, int length) =>
        PropertyDescriptor.Data(JsFunction.CreateNative(name, impl, length), writable: true, enumerable: false, configurable: true);

    private static JsValue Arg(JsValue[] args, int index) => index < args.Length ? args[index] : JsValue.Undefined;

    public static JsDynamicObject Create(Realm realm)
    {
        var o = new JsDynamicObject();
        o.DefineOwnProperty("format", MethodDesc("format", (_, args) => new JsString(Format(args)), 0));
        o.DefineOwnProperty("formatWithOptions", MethodDesc("formatWithOptions", (_, args) =>
        {
            var rest = args.Length > 0 ? args[1..] : [];
            return new JsString(Format(rest));
        }, 0));
        o.DefineOwnProperty("inspect", MethodDesc("inspect", (_, args) => new JsString(Inspect(Arg(args, 0), depth: 2)), 2));
        o.DefineOwnProperty("isDeepStrictEqual", MethodDesc("isDeepStrictEqual", (_, args) =>
            DeepEqual(Arg(args, 0), Arg(args, 1), strict: true) ? JsValue.True : JsValue.False, 2));

        o.DefineOwnProperty("promisify", MethodDesc("promisify", (_, args) =>
        {
            if (Arg(args, 0) is not JsFunction fn)
                throw new Runtime.Errors.JsTypeError("promisify: first argument must be a function");
            return JsFunction.CreateNative(fn.Name + "Async", (thisArg, pArgs) =>
            {
                if (realm.GlobalObject.Get("Promise") is not JsFunction promiseCtor)
                    throw new Runtime.Errors.JsTypeError("Promise is not available");
                var executor = JsFunction.CreateNative("executor", (_, exArgs) =>
                {
                    var resolve = (JsFunction)exArgs[0];
                    var reject = (JsFunction)exArgs[1];
                    var cbArgs = new JsValue[pArgs.Length + 1];
                    Array.Copy(pArgs, cbArgs, pArgs.Length);
                    cbArgs[^1] = JsFunction.CreateNative("cb", (_, cbr) =>
                    {
                        var err = Arg(cbr, 0);
                        var val = Arg(cbr, 1);
                        if (err is not JsNull and not JsUndefined) reject.Call(JsValue.Undefined, [err]);
                        else resolve.Call(JsValue.Undefined, [val]);
                        return JsValue.Undefined;
                    }, 2);
                    try { fn.Call(thisArg, cbArgs); }
                    catch (Exception ex) { reject.Call(JsValue.Undefined, [new JsString(ex.Message)]); }
                    return JsValue.Undefined;
                }, 2);
                return promiseCtor.Construct([executor]);
            }, fn.Length);
        }, 1));

        o.DefineOwnProperty("callbackify", MethodDesc("callbackify", (_, args) =>
        {
            if (Arg(args, 0) is not JsFunction fn)
                throw new Runtime.Errors.JsTypeError("callbackify: first argument must be a function");
            return JsFunction.CreateNative(fn.Name + "Cb", (thisArg, cbArgs) =>
            {
                if (cbArgs.Length == 0 || cbArgs[^1] is not JsFunction cb)
                    throw new Runtime.Errors.JsTypeError("last argument must be a callback");
                var rest = cbArgs[..^1];
                var result = fn.Call(thisArg, rest);
                if (result is JsDynamicObject p && p.Get("then") is JsFunction thenFn)
                {
                    thenFn.Call(result, [
                        JsFunction.CreateNative("onResolved", (_, r) => { cb.Call(JsValue.Undefined, [JsValue.Null, Arg(r, 0)]); return JsValue.Undefined; }, 1),
                        JsFunction.CreateNative("onRejected", (_, r) => { cb.Call(JsValue.Undefined, [Arg(r, 0)]); return JsValue.Undefined; }, 1),
                    ]);
                }
                else
                {
                    cb.Call(JsValue.Undefined, [JsValue.Null, result]);
                }
                return JsValue.Undefined;
            }, fn.Length + 1);
        }, 1));

        o.DefineOwnProperty("deprecate", MethodDesc("deprecate", (_, args) =>
        {
            if (Arg(args, 0) is not JsFunction fn) return Arg(args, 0);
            return fn;
        }, 2));

        // util.types
        var types = new JsDynamicObject();
        types.DefineOwnProperty("isDate", MethodDesc("isDate", (_, args) =>
            (Arg(args, 0) is JsDynamicObject o2 && o2.Prototype == realm.DatePrototype) ? JsValue.True : JsValue.False, 1));
        types.DefineOwnProperty("isRegExp", MethodDesc("isRegExp", (_, args) => Arg(args, 0) is JsRegExp ? JsValue.True : JsValue.False, 1));
        types.DefineOwnProperty("isPromise", MethodDesc("isPromise", (_, args) =>
            (Arg(args, 0) is JsDynamicObject o2 && o2.Prototype == realm.PromisePrototype) ? JsValue.True : JsValue.False, 1));
        types.DefineOwnProperty("isMap", MethodDesc("isMap", (_, args) =>
            (Arg(args, 0) is JsDynamicObject o2 && o2.Prototype == realm.MapPrototype) ? JsValue.True : JsValue.False, 1));
        types.DefineOwnProperty("isSet", MethodDesc("isSet", (_, args) =>
            (Arg(args, 0) is JsDynamicObject o2 && o2.Prototype == realm.SetPrototype) ? JsValue.True : JsValue.False, 1));
        types.DefineOwnProperty("isNativeError", MethodDesc("isNativeError", (_, args) =>
            Arg(args, 0) is JsDynamicObject e && IsError(e, realm) ? JsValue.True : JsValue.False, 1));
        o.DefineOwnProperty("types", PropertyDescriptor.Data(types));

        o.DefineOwnProperty("TextEncoder", PropertyDescriptor.Data(realm.GlobalObject.Get("TextEncoder")));
        o.DefineOwnProperty("TextDecoder", PropertyDescriptor.Data(realm.GlobalObject.Get("TextDecoder")));

        return o;
    }

    private static bool IsError(JsDynamicObject o, Realm realm)
    {
        var proto = o.Prototype;
        while (proto is not null)
        {
            if (proto == realm.ErrorPrototype) return true;
            proto = proto.Prototype;
        }
        return false;
    }

    public static string Format(JsValue[] args)
    {
        if (args.Length == 0) return "";
        if (args[0] is not JsString fmt)
        {
            return string.Join(' ', args.Select(a => Inspect(a, depth: 2)));
        }
        var s = fmt.Value;
        var sb = new StringBuilder();
        int ai = 1;
        int i = 0;
        while (i < s.Length)
        {
            if (s[i] == '%' && i + 1 < s.Length)
            {
                char code = s[i + 1];
                if (ai < args.Length)
                {
                    var v = args[ai];
                    switch (code)
                    {
                        case 's': sb.Append(v.ToJsString()); ai++; i += 2; continue;
                        case 'd': sb.Append(((long)v.ToNumber()).ToString(CultureInfo.InvariantCulture)); ai++; i += 2; continue;
                        case 'i': sb.Append(((long)v.ToNumber()).ToString(CultureInfo.InvariantCulture)); ai++; i += 2; continue;
                        case 'f': sb.Append(v.ToNumber().ToString("G", CultureInfo.InvariantCulture)); ai++; i += 2; continue;
                        case 'j': sb.Append(JsonStringify(v)); ai++; i += 2; continue;
                        case 'o':
                        case 'O': sb.Append(Inspect(v, depth: 2)); ai++; i += 2; continue;
                        case '%': sb.Append('%'); i += 2; continue;
                    }
                }
                if (code == '%') { sb.Append('%'); i += 2; continue; }
            }
            sb.Append(s[i]);
            i++;
        }
        for (; ai < args.Length; ai++) { sb.Append(' '); sb.Append(Inspect(args[ai], depth: 2)); }
        return sb.ToString();
    }

    public static string Inspect(JsValue value, int depth) => Inspect(value, depth, new HashSet<JsDynamicObject>());

    private static string Inspect(JsValue value, int depth, HashSet<JsDynamicObject> seen)
    {
        switch (value)
        {
            case JsUndefined: return "undefined";
            case JsNull: return "null";
            case JsString s: return "'" + s.Value.Replace("'", "\\'", StringComparison.Ordinal) + "'";
            case JsBoolean b: return b.ToJsString();
            case JsNumber n: return n.ToJsString();
            case JsFunction f: return $"[Function: {(string.IsNullOrEmpty(f.Name) ? "anonymous" : f.Name)}]";
            case JsArray arr:
                if (depth < 0) return "[Array]";
                if (!seen.Add(arr)) return "[Circular]";
                try
                {
                    var parts = new List<string>();
                    for (int i = 0; i < arr.DenseLength; i++)
                        parts.Add(Inspect(arr.Get(i.ToString(CultureInfo.InvariantCulture)), depth - 1, seen));
                    return "[ " + string.Join(", ", parts) + " ]";
                }
                finally { seen.Remove(arr); }
            case JsDynamicObject o:
                if (depth < 0) return "[Object]";
                if (!seen.Add(o)) return "[Circular]";
                try
                {
                    var parts = new List<string>();
                    foreach (var key in o.OwnPropertyKeys())
                    {
                        var desc = o.GetOwnProperty(key);
                        if (desc is null || desc.Enumerable == false) continue;
                        var v = o.Get(key);
                        parts.Add(key + ": " + Inspect(v, depth - 1, seen));
                    }
                    return "{ " + string.Join(", ", parts) + " }";
                }
                finally { seen.Remove(o); }
            default: return value.ToJsString();
        }
    }

    private static string JsonStringify(JsValue v) => v switch
    {
        JsString s => "\"" + s.Value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"",
        JsNumber or JsBoolean => v.ToJsString(),
        JsNull => "null",
        _ => v.ToJsString(),
    };

    public static bool DeepEqual(JsValue a, JsValue b, bool strict)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is JsDynamicObject oa && b is JsDynamicObject ob)
        {
            if (a is JsArray aa && b is JsArray ba)
            {
                if (aa.DenseLength != ba.DenseLength) return false;
                for (int i = 0; i < aa.DenseLength; i++)
                {
                    var k = i.ToString(CultureInfo.InvariantCulture);
                    if (!DeepEqual(aa.Get(k), ba.Get(k), strict)) return false;
                }
                return true;
            }
            var keysA = oa.OwnPropertyKeys().Where(k => oa.GetOwnProperty(k)?.Enumerable != false).ToHashSet();
            var keysB = ob.OwnPropertyKeys().Where(k => ob.GetOwnProperty(k)?.Enumerable != false).ToHashSet();
            if (!keysA.SetEquals(keysB)) return false;
            foreach (var k in keysA)
            {
                if (!DeepEqual(oa.Get(k), ob.Get(k), strict)) return false;
            }
            return true;
        }
        return strict ? a.StrictEquals(b) : a.AbstractEquals(b);
    }
}
