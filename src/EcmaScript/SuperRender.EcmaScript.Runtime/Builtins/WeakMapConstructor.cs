namespace SuperRender.EcmaScript.Runtime.Builtins;

using System.Runtime.CompilerServices;
using SuperRender.EcmaScript.Runtime;

// JSGEN005/006: set() returns `this` (JsValue-derived); get() returns the stored value which
// can be any JsValue; key param is checked internally, not via the typed-coercion layer.
#pragma warning disable JSGEN005, JSGEN006
[JsObject]
public sealed partial class JsWeakMapObject : JsDynamicObject
{
    private readonly ConditionalWeakTable<JsDynamicObject, JsValue> _table = new();

    [JsConstructor("WeakMap", Length = 0, Callable = false)]
    public JsWeakMapObject(JsValue[] args)
    {
        var iterable = args.Length > 0 ? args[0] : Undefined;
        if (iterable is JsArray arr)
        {
            for (var i = 0; i < arr.DenseLength; i++)
            {
                var entry = arr.GetIndex(i);
                if (entry is not JsArray pair)
                {
                    throw new Errors.JsTypeError("Iterator value is not an entry object", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                }

                var key = pair.GetIndex(0);
                if (key is not JsDynamicObject keyObj)
                {
                    throw new Errors.JsTypeError("Invalid value used as weak map key", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                }

                _table.AddOrUpdate(keyObj, pair.GetIndex(1));
            }
        }
    }

    [JsMethod("get")]
    public JsValue Get(JsValue key)
    {
        if (key is not JsDynamicObject keyObj) return Undefined;
        return _table.TryGetValue(keyObj, out var value) ? value : Undefined;
    }

    [JsMethod("set")]
    public JsValue Set(JsValue key, JsValue value)
    {
        if (key is not JsDynamicObject keyObj)
        {
            throw new Errors.JsTypeError("Invalid value used as weak map key", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        _table.AddOrUpdate(keyObj, value);
        return this;
    }

    [JsMethod("has")]
    public bool Has(JsValue key)
    {
        return key is JsDynamicObject keyObj && _table.TryGetValue(keyObj, out _);
    }

    [JsMethod("delete")]
    public bool Delete(JsValue key)
    {
        return key is JsDynamicObject keyObj && _table.Remove(keyObj);
    }
}
#pragma warning restore JSGEN005, JSGEN006
