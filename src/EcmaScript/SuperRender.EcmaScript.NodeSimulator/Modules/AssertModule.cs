using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Node.js `assert` / `assert/strict` module.
/// </summary>
public static class AssertModule
{
    private static JsValue Arg(JsValue[] args, int index) => index < args.Length ? args[index] : JsValue.Undefined;

    private static string? ArgString(JsValue[] args, int index)
    {
        var v = Arg(args, index);
        return v is JsUndefined or JsNull ? null : v.ToJsString();
    }

    private static string RequireString(JsValue[] args, int index, string param)
    {
        var v = Arg(args, index);
        if (v is JsString s) return s.Value;
        throw new Runtime.Errors.JsTypeError($"The \"{param}\" argument must be of type string");
    }

    private static void DefineMethod(JsObject obj, string name, int length, Func<JsValue, JsValue[], JsValue> impl)
    {
        obj.DefineOwnProperty(name, PropertyDescriptor.Data(
            JsFunction.CreateNative(name, impl, length),
            writable: true, enumerable: false, configurable: true));
    }

    public static JsObject Create()
    {
        // The assert function itself is callable (behaves as assert.ok)
        var fn = new JsFunction
        {
            Name = "assert",
            Length = 1,
            IsConstructor = false,
            CallTarget = (_, args) => Ok(Arg(args, 0), ArgString(args, 1)),
        };

        DefineMethod(fn, "ok", 2, (_, args) => Ok(Arg(args, 0), ArgString(args, 1)));
        DefineMethod(fn, "equal", 3, (_, args) => Equal(Arg(args, 0), Arg(args, 1), strict: false, neg: false, ArgString(args, 2)));
        DefineMethod(fn, "notEqual", 3, (_, args) => Equal(Arg(args, 0), Arg(args, 1), strict: false, neg: true, ArgString(args, 2)));
        DefineMethod(fn, "strictEqual", 3, (_, args) => Equal(Arg(args, 0), Arg(args, 1), strict: true, neg: false, ArgString(args, 2)));
        DefineMethod(fn, "notStrictEqual", 3, (_, args) => Equal(Arg(args, 0), Arg(args, 1), strict: true, neg: true, ArgString(args, 2)));
        DefineMethod(fn, "deepEqual", 3, (_, args) => DeepEqual(Arg(args, 0), Arg(args, 1), strict: false, neg: false, ArgString(args, 2)));
        DefineMethod(fn, "notDeepEqual", 3, (_, args) => DeepEqual(Arg(args, 0), Arg(args, 1), strict: false, neg: true, ArgString(args, 2)));
        DefineMethod(fn, "deepStrictEqual", 3, (_, args) => DeepEqual(Arg(args, 0), Arg(args, 1), strict: true, neg: false, ArgString(args, 2)));
        DefineMethod(fn, "notDeepStrictEqual", 3, (_, args) => DeepEqual(Arg(args, 0), Arg(args, 1), strict: true, neg: true, ArgString(args, 2)));
        DefineMethod(fn, "fail", 1, (_, args) => throw new AssertionError(ArgString(args, 0) ?? "Failed"));
        DefineMethod(fn, "throws", 2, (_, args) => Throws(args, expected: true));
        DefineMethod(fn, "doesNotThrow", 2, (_, args) => Throws(args, expected: false));
        DefineMethod(fn, "match", 2, (_, args) =>
        {
            var str = RequireString(args, 0, "string");
            if (Arg(args, 1) is not JsRegExp rx)
                throw new Runtime.Errors.JsTypeError("match: regexp required");
            if (!rx.Test(str).Value) throw new AssertionError($"Expected {str} to match {rx.Pattern}");
            return JsValue.Undefined;
        });
        DefineMethod(fn, "doesNotMatch", 2, (_, args) =>
        {
            var str = RequireString(args, 0, "string");
            if (Arg(args, 1) is not JsRegExp rx)
                throw new Runtime.Errors.JsTypeError("doesNotMatch: regexp required");
            if (rx.Test(str).Value) throw new AssertionError($"Expected {str} not to match {rx.Pattern}");
            return JsValue.Undefined;
        });
        fn.DefineOwnProperty("strict", PropertyDescriptor.Data(fn, writable: true, enumerable: false, configurable: true));
        return fn;
    }

    private static JsValue Ok(JsValue v, string? msg)
    {
        if (!v.ToBoolean()) throw new AssertionError(msg ?? "Expected value to be truthy");
        return JsValue.Undefined;
    }

    private static JsValue Equal(JsValue a, JsValue b, bool strict, bool neg, string? msg)
    {
        bool equal = strict ? a.StrictEquals(b) : a.AbstractEquals(b);
        if (equal == neg) throw new AssertionError(msg ?? $"{UtilModule.Inspect(a, 2)} {(neg ? "==" : "!=")} {UtilModule.Inspect(b, 2)}");
        return JsValue.Undefined;
    }

    private static JsValue DeepEqual(JsValue a, JsValue b, bool strict, bool neg, string? msg)
    {
        bool equal = UtilModule.DeepEqual(a, b, strict);
        if (equal == neg) throw new AssertionError(msg ?? $"Expected values to{(neg ? " not" : "")} be deeply equal");
        return JsValue.Undefined;
    }

    private static JsValue Throws(JsValue[] args, bool expected)
    {
        if (Arg(args, 0) is not JsFunction fn)
            throw new Runtime.Errors.JsTypeError("first argument must be a function");
        bool threw = false;
        try { fn.Call(JsValue.Undefined, []); }
        catch (AssertionError) { throw; }
        catch (Runtime.Errors.JsErrorBase) { threw = true; }
        catch (Exception) { threw = true; }
        if (threw != expected)
        {
            throw new AssertionError(expected ? "Expected function to throw" : "Expected function not to throw");
        }
        return JsValue.Undefined;
    }
}

/// <summary>Thrown by Node assert.* failures.</summary>
public sealed class AssertionError : Runtime.Errors.JsErrorBase
{
    public AssertionError(string message) : base(message) { }
}
