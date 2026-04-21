namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

[JsObject]
public sealed partial class JsBooleanObject : JsObject
{
    internal JsValue BooleanData { get; }

    [JsConstructor("Boolean", Length = 1)]
    public JsBooleanObject(JsValue[] args)
    {
        var val = args.Length > 0 ? args[0] : Undefined;
        BooleanData = val.ToBoolean() ? JsValue.True : JsValue.False;
    }

#pragma warning disable JSGEN005, JSGEN006, JSGEN007 // primitive-wrapper: thisArg may be primitive or wrapper object
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
#pragma warning restore JSGEN005, JSGEN006, JSGEN007

    private static bool GetBooleanValue(JsValue thisArg)
    {
        if (thisArg is JsBoolean b)
        {
            return b.Value;
        }

        if (thisArg is JsBooleanObject boolObj)
        {
            if (boolObj.BooleanData is JsBoolean boolData)
            {
                return boolData.Value;
            }
        }

        throw new Errors.JsTypeError("Boolean.prototype.valueOf requires that 'this' be a Boolean", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }
}
