namespace SuperRender.EcmaScript.Runtime.Builtins;

using System.Runtime.CompilerServices;
using SuperRender.EcmaScript.Runtime;

public static class WeakSetConstructor
{
    public static void Install(Realm realm)
    {
        var proto = new JsDynamicObject { Prototype = realm.ObjectPrototype };

        var ctor = new JsFunction
        {
            Name = "WeakSet",
            Length = 0,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            ConstructTarget = args =>
            {
                var weakSet = new JsWeakSetObject { Prototype = proto };
                var iterable = BuiltinHelper.Arg(args, 0);
                if (iterable is JsArray arr)
                {
                    for (var i = 0; i < arr.DenseLength; i++)
                    {
                        var val = arr.GetIndex(i);
                        if (val is not JsDynamicObject objVal)
                        {
                            throw new Errors.JsTypeError("Invalid value used in weak set", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                        }

                        weakSet.WeakSetAdd(objVal);
                    }
                }

                return weakSet;
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError("Constructor WeakSet requires 'new'", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }
        };

        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "add", (thisArg, args) =>
        {
            var weakSet = RequireWeakSet(thisArg);
            var val = BuiltinHelper.Arg(args, 0);
            if (val is not JsDynamicObject objVal)
            {
                throw new Errors.JsTypeError("Invalid value used in weak set", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            weakSet.WeakSetAdd(objVal);
            return thisArg;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "has", (thisArg, args) =>
        {
            var weakSet = RequireWeakSet(thisArg);
            var val = BuiltinHelper.Arg(args, 0);
            if (val is not JsDynamicObject objVal)
            {
                return JsValue.False;
            }

            return weakSet.WeakSetHas(objVal) ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "delete", (thisArg, args) =>
        {
            var weakSet = RequireWeakSet(thisArg);
            var val = BuiltinHelper.Arg(args, 0);
            if (val is not JsDynamicObject objVal)
            {
                return JsValue.False;
            }

            return weakSet.WeakSetDelete(objVal) ? JsValue.True : JsValue.False;
        }, 1);

        // Symbol.toStringTag
        proto.DefineSymbolProperty(JsSymbol.ToStringTag,
            PropertyDescriptor.Data(new JsString("WeakSet"), writable: false, enumerable: false, configurable: true));

        realm.InstallGlobal("WeakSet", ctor);
    }

    private static JsWeakSetObject RequireWeakSet(JsValue value)
    {
        if (value is JsWeakSetObject weakSet)
        {
            return weakSet;
        }

        throw new Errors.JsTypeError("Method requires that 'this' be a WeakSet", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }
}

internal sealed class JsWeakSetObject : JsDynamicObject
{
    // Using ConditionalWeakTable with a sentinel value since we only need key presence
    private readonly ConditionalWeakTable<JsDynamicObject, JsDynamicObject> _table = new();
    private static readonly JsDynamicObject Sentinel = new();

    public void WeakSetAdd(JsDynamicObject value)
    {
        _table.AddOrUpdate(value, Sentinel);
    }

    public bool WeakSetHas(JsDynamicObject value)
    {
        return _table.TryGetValue(value, out _);
    }

    public bool WeakSetDelete(JsDynamicObject value)
    {
        return _table.Remove(value);
    }
}
