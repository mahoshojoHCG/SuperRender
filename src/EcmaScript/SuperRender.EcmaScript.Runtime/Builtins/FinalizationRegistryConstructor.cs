namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

public static class FinalizationRegistryConstructor
{
    public static void Install(Realm realm)
    {
        var proto = realm.FinalizationRegistryPrototype;

        var ctor = new JsFunction
        {
            Name = "FinalizationRegistry",
            Length = 1,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            ConstructTarget = args =>
            {
                var callback = BuiltinHelper.Arg(args, 0);
                if (callback is not JsFunction callbackFn)
                {
                    throw new Errors.JsTypeError("FinalizationRegistry callback must be a function", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                }

                var registry = new JsFinalizationRegistryObject(callbackFn) { Prototype = realm.FinalizationRegistryPrototype };
                return registry;
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError("Constructor FinalizationRegistry requires 'new'", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }
        };

        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "register", (thisArg, args) =>
        {
            if (thisArg is not JsFinalizationRegistryObject registry)
            {
                throw new Errors.JsTypeError("Method register called on incompatible receiver", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var target = BuiltinHelper.Arg(args, 0);
            if (target is not JsDynamicObject targetObj)
            {
                throw new Errors.JsTypeError("FinalizationRegistry target must be an object", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var heldValue = BuiltinHelper.Arg(args, 1);
            var unregisterToken = args.Length > 2 ? args[2] as JsDynamicObject : null;
            registry.Register(targetObj, heldValue, unregisterToken);
            return JsValue.Undefined;
        }, 2);

        BuiltinHelper.DefineMethod(proto, "unregister", (thisArg, args) =>
        {
            if (thisArg is not JsFinalizationRegistryObject registry)
            {
                throw new Errors.JsTypeError("Method unregister called on incompatible receiver", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var token = BuiltinHelper.Arg(args, 0);
            if (token is not JsDynamicObject tokenObj)
            {
                throw new Errors.JsTypeError("unregister token must be an object", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            return registry.Unregister(tokenObj) ? JsValue.True : JsValue.False;
        }, 1);

        proto.DefineSymbolProperty(JsSymbol.ToStringTag,
            PropertyDescriptor.Data(new JsString("FinalizationRegistry"), writable: false, enumerable: false, configurable: true));

        realm.InstallGlobal("FinalizationRegistry", ctor);
    }
}

internal sealed class JsFinalizationRegistryObject : JsDynamicObject
{
    private readonly JsFunction _callback;
    private readonly List<Registration> _registrations = [];

    public JsFinalizationRegistryObject(JsFunction callback)
    {
        _callback = callback;
    }

    public void Register(JsDynamicObject target, JsValue heldValue, JsDynamicObject? unregisterToken)
    {
        _registrations.Add(new Registration(new WeakReference<JsDynamicObject>(target), heldValue, unregisterToken));
    }

    public bool Unregister(JsDynamicObject token)
    {
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
