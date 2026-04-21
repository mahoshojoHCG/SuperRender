namespace SuperRender.EcmaScript.Runtime.Builtins;

using System.Runtime.CompilerServices;
using SuperRender.EcmaScript.Runtime;

// JSGEN005/006: prototype methods take raw JsValue (objects only — non-object fast-returns
// in has/delete match spec) and add() returns `this` (JsValue-derived).
#pragma warning disable JSGEN005, JSGEN006
[JsObject]
public sealed partial class JsWeakSetObject : JsDynamicObject
{
    private readonly ConditionalWeakTable<JsDynamicObject, JsDynamicObject> _table = new();
    private static readonly JsDynamicObject Sentinel = new();

    [JsConstructor("WeakSet", Length = 0, Callable = false)]
    public JsWeakSetObject(JsValue[] args)
    {
        var iterable = args.Length > 0 ? args[0] : Undefined;
        if (iterable is JsArray arr)
        {
            for (var i = 0; i < arr.DenseLength; i++)
            {
                var val = arr.GetIndex(i);
                if (val is not JsDynamicObject objVal)
                {
                    throw new Errors.JsTypeError("Invalid value used in weak set", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                }

                _table.AddOrUpdate(objVal, Sentinel);
            }
        }
    }

    [JsMethod("add")]
    public JsValue Add(JsValue value)
    {
        if (value is not JsDynamicObject objVal)
        {
            throw new Errors.JsTypeError("Invalid value used in weak set", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        _table.AddOrUpdate(objVal, Sentinel);
        return this;
    }

    [JsMethod("has")]
    public bool Has(JsValue value)
    {
        return value is JsDynamicObject objVal && _table.TryGetValue(objVal, out _);
    }

    [JsMethod("delete")]
    public bool Delete(JsValue value)
    {
        return value is JsDynamicObject objVal && _table.Remove(objVal);
    }
}
#pragma warning restore JSGEN005, JSGEN006
