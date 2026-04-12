namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

public static class WeakRefConstructor
{
    public static void Install(Realm realm)
    {
        var proto = realm.WeakRefPrototype;

        var ctor = new JsFunction
        {
            Name = "WeakRef",
            Length = 1,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            ConstructTarget = args =>
            {
                var target = BuiltinHelper.Arg(args, 0);
                if (target is not JsObject targetObj)
                {
                    throw new Errors.JsTypeError("WeakRef target must be an object", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                }

                var weakRef = new JsWeakRefObject(targetObj) { Prototype = realm.WeakRefPrototype };
                return weakRef;
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError("Constructor WeakRef requires 'new'", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }
        };

        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "deref", (thisArg, _) =>
        {
            if (thisArg is not JsWeakRefObject weakRef)
            {
                throw new Errors.JsTypeError("Method deref called on incompatible receiver", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            return weakRef.Deref();
        }, 0);

        proto.DefineSymbolProperty(JsSymbol.ToStringTag,
            PropertyDescriptor.Data(new JsString("WeakRef"), writable: false, enumerable: false, configurable: true));

        realm.InstallGlobal("WeakRef", ctor);
    }
}

internal sealed class JsWeakRefObject : JsObject
{
    private readonly WeakReference<JsObject> _target;

    public JsWeakRefObject(JsObject target)
    {
        _target = new WeakReference<JsObject>(target);
    }

    public JsValue Deref()
    {
        return _target.TryGetTarget(out var target) ? target : Undefined;
    }
}
