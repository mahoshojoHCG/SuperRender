namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

// JSGEN005: FinalizationRegistry.register/unregister accept an object target, arbitrary held value,
// and an optional object token — object-vs-not checks happen internally, not via typed coercion.
#pragma warning disable JSGEN005
[JsObject]
public sealed partial class JsFinalizationRegistryObject : JsDynamicObject
{
    private readonly JsFunction _callback;
    private readonly List<Registration> _registrations = [];

    [JsConstructor("FinalizationRegistry", Length = 1, Callable = false)]
    public JsFinalizationRegistryObject(JsValue[] args)
    {
        var callback = args.Length > 0 ? args[0] : Undefined;
        if (callback is not JsFunction callbackFn)
        {
            throw new Errors.JsTypeError("FinalizationRegistry callback must be a function", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        _callback = callbackFn;
    }

    [JsMethod("register")]
    public void Register(JsValue target, JsValue heldValue, params JsValue[] rest)
    {
        if (target is not JsDynamicObject targetObj)
        {
            throw new Errors.JsTypeError("FinalizationRegistry target must be an object", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        var unregisterToken = rest.Length > 0 ? rest[0] as JsDynamicObject : null;
        _registrations.Add(new Registration(new WeakReference<JsDynamicObject>(targetObj), heldValue, unregisterToken));
    }

    [JsMethod("unregister")]
    public bool Unregister(JsValue tokenValue)
    {
        if (tokenValue is not JsDynamicObject token)
        {
            throw new Errors.JsTypeError("unregister token must be an object", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        var removed = false;
        for (var i = _registrations.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_registrations[i].UnregisterToken, token))
            {
                _registrations.RemoveAt(i);
                removed = true;
            }
        }

        return removed;
    }

    /// <summary>
    /// Check for collected targets and invoke cleanup callback.
    /// Called during microtask drain or explicitly via cleanupSome().
    /// </summary>
    internal void CleanupSome()
    {
        for (var i = _registrations.Count - 1; i >= 0; i--)
        {
            if (!_registrations[i].Target.TryGetTarget(out _))
            {
                var heldValue = _registrations[i].HeldValue;
                _registrations.RemoveAt(i);
                try
                {
                    _callback.Call(Undefined, [heldValue]);
                }
                catch
                {
                    // Cleanup callbacks must not throw
                }
            }
        }
    }

    private sealed record Registration(WeakReference<JsDynamicObject> Target, JsValue HeldValue, JsDynamicObject? UnregisterToken);
}
#pragma warning restore JSGEN005
