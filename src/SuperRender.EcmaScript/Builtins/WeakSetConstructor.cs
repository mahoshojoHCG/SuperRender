namespace SuperRender.EcmaScript.Builtins;

using System.Runtime.CompilerServices;
using SuperRender.EcmaScript.Runtime;

public static class WeakSetConstructor
{
    public static void Install(Realm realm)
    {
        var proto = new JsObject { Prototype = realm.ObjectPrototype };

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
                        if (val is not JsObject objVal)
                        {
                            throw new Errors.JsTypeError("Invalid value used in weak set");
                        }

                        weakSet.WeakSetAdd(objVal);
                    }
                }

                return weakSet;
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError("Constructor WeakSet requires 'new'");
            }
        };

        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "add", (thisArg, args) =>
        {
            var weakSet = RequireWeakSet(thisArg);
            var val = BuiltinHelper.Arg(args, 0);
            if (val is not JsObject objVal)
            {
                throw new Errors.JsTypeError("Invalid value used in weak set");
            }

            weakSet.WeakSetAdd(objVal);
            return thisArg;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "has", (thisArg, args) =>
        {
            var weakSet = RequireWeakSet(thisArg);
            var val = BuiltinHelper.Arg(args, 0);
            if (val is not JsObject objVal)
            {
                return JsValue.False;
            }

            return weakSet.WeakSetHas(objVal) ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "delete", (thisArg, args) =>
        {
            var weakSet = RequireWeakSet(thisArg);
            var val = BuiltinHelper.Arg(args, 0);
            if (val is not JsObject objVal)
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

        throw new Errors.JsTypeError("Method requires that 'this' be a WeakSet");
    }
}

internal sealed class JsWeakSetObject : JsObject
{
    // Using ConditionalWeakTable with a sentinel value since we only need key presence
    private readonly ConditionalWeakTable<JsObject, JsObject> _table = new();
    private static readonly JsObject Sentinel = new();

    public void WeakSetAdd(JsObject value)
    {
        _table.AddOrUpdate(value, Sentinel);
    }

    public bool WeakSetHas(JsObject value)
    {
        return _table.TryGetValue(value, out _);
    }

    public bool WeakSetDelete(JsObject value)
    {
        return _table.Remove(value);
    }
}
