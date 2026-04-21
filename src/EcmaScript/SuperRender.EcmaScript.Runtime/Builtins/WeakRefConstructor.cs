namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

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

#pragma warning disable JSGEN006 // returns stored value of any type
    [JsMethod("deref")]
    public JsValue Deref()
    {
        return _target.TryGetTarget(out var target) ? target : Undefined;
    }
#pragma warning restore JSGEN006
}
