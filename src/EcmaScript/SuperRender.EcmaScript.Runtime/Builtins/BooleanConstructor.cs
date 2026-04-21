namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

// Primitive-wrapper shape validated end-to-end with the generator:
//   * [JsCall] handles call-form coercion to a primitive (not a wrapper).
//   * Legacy [JsMethod] shape is required for toString/valueOf since `thisArg`
//     may be either the wrapper object or a JsBoolean primitive — the typed
//     coercion layer would reject the primitive receiver.
#pragma warning disable JSGEN005, JSGEN006, JSGEN007
[JsObject]
public sealed partial class JsBooleanObject : JsDynamicObject
{
    [JsConstructor("Boolean", Length = 1)]
    public JsBooleanObject(JsValue[] args)
    {
        var val = args.Length > 0 ? args[0] : Undefined;
        DefineOwnProperty("[[BooleanData]]",
            PropertyDescriptor.Data(val.ToBoolean() ? JsValue.True : JsValue.False, writable: false, enumerable: false, configurable: false));
    }

    [JsCall]
    public static JsValue Call(JsValue thisArg, JsValue[] args)
    {
        _ = thisArg;
        var val = args.Length > 0 ? args[0] : Undefined;
        return val.ToBoolean() ? JsValue.True : JsValue.False;
    }

    [JsMethod("toString")]
    public JsValue BooleanToString(JsValue thisArg, JsValue[] args)
    {
        _ = args;
        return new JsString(GetBooleanValue(thisArg) ? "true" : "false");
    }

    [JsMethod("valueOf")]
    public JsValue BooleanValueOf(JsValue thisArg, JsValue[] args)
    {
        _ = args;
        return GetBooleanValue(thisArg) ? JsValue.True : JsValue.False;
    }

    private static bool GetBooleanValue(JsValue thisArg)
    {
        if (thisArg is JsBoolean b)
        {
            return b.Value;
        }

        if (thisArg is JsDynamicObject obj)
        {
            var data = obj.GetOwnProperty("[[BooleanData]]");
            if (data?.Value is JsBoolean boolData)
            {
                return boolData.Value;
            }
        }

        throw new Errors.JsTypeError("Boolean.prototype.valueOf requires that 'this' be a Boolean", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }
}
#pragma warning restore JSGEN005, JSGEN006, JSGEN007

public static class BooleanConstructor
{
    public static void Install(Realm realm) => JsBooleanObject.__InstallConstructor(realm);
}
