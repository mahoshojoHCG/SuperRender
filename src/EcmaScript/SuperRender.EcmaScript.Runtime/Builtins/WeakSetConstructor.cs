namespace SuperRender.EcmaScript.Runtime.Builtins;

using System.Runtime.CompilerServices;
using SuperRender.EcmaScript.Runtime;

[JsObject]
public sealed partial class JsWeakSetObject : JsObject
{
    private readonly ConditionalWeakTable<JsObject, JsObject> _table = new();
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
                if (val is not JsObject objVal)
                {
                    throw new Errors.JsTypeError("Invalid value used in weak set", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                }

                _table.AddOrUpdate(objVal, Sentinel);
            }
        }
    }

#pragma warning disable JSGEN005, JSGEN006 // JsValue param: WeakSet accepts any object
    [JsMethod("add")]
    public JsValue Add(JsValue value)
    {
        if (value is not JsObject objVal)
        {
            throw new Errors.JsTypeError("Invalid value used in weak set", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        _table.AddOrUpdate(objVal, Sentinel);
        return this;
    }
#pragma warning restore JSGEN005, JSGEN006

#pragma warning disable JSGEN005 // JsValue param: WeakSet accepts any object
    [JsMethod("has")]
    public bool Has(JsValue value)
    {
        return value is JsObject objVal && _table.TryGetValue(objVal, out _);
    }

    [JsMethod("delete")]
    public bool Delete(JsValue value)
    {
        return value is JsObject objVal && _table.Remove(objVal);
    }
#pragma warning restore JSGEN005
}
