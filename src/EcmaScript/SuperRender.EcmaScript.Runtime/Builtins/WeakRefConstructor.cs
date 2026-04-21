namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

// JSGEN006: WeakRef.prototype.deref returns the wrapped object or undefined — JsValue is
// the honest union type here. Could be expressed as JsOptional<JsDynamicObject> once the
// inner-coercion path preserves reference identity for JsValue-derived inner types.
#pragma warning disable JSGEN006
[JsObject]
public sealed partial class JsWeakRefObject : JsDynamicObject
{
    private readonly WeakReference<JsDynamicObject> _target;

    [JsConstructor("WeakRef", Length = 1, Callable = false)]
    public JsWeakRefObject(JsValue[] args)
    {
        var target = args.Length > 0 ? args[0] : Undefined;
        if (target is not JsDynamicObject targetObj)
        {
            throw new Errors.JsTypeError("WeakRef target must be an object", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        _target = new WeakReference<JsDynamicObject>(targetObj);
    }

    [JsMethod("deref")]
    public JsValue Deref()
    {
        return _target.TryGetTarget(out var target) ? target : Undefined;
    }
}
#pragma warning restore JSGEN006
