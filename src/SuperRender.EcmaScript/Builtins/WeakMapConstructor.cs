namespace SuperRender.EcmaScript.Builtins;

using System.Runtime.CompilerServices;
using SuperRender.EcmaScript.Runtime;

public static class WeakMapConstructor
{
    public static void Install(Realm realm)
    {
        var proto = new JsObject { Prototype = realm.ObjectPrototype };

        var ctor = new JsFunction
        {
            Name = "WeakMap",
            Length = 0,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            ConstructTarget = args =>
            {
                var weakMap = new JsWeakMapObject { Prototype = proto };
                var iterable = BuiltinHelper.Arg(args, 0);
                if (iterable is JsArray arr)
                {
                    for (var i = 0; i < arr.DenseLength; i++)
                    {
                        var entry = arr.GetIndex(i);
                        if (entry is not JsArray pair)
                        {
                            throw new Errors.JsTypeError("Iterator value is not an entry object");
                        }

                        var key = pair.GetIndex(0);
                        if (key is not JsObject keyObj)
                        {
                            throw new Errors.JsTypeError("Invalid value used as weak map key");
                        }

                        weakMap.WeakMapSet(keyObj, pair.GetIndex(1));
                    }
                }

                return weakMap;
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError("Constructor WeakMap requires 'new'");
            }
        };

        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "get", (thisArg, args) =>
        {
            var weakMap = RequireWeakMap(thisArg);
            var key = BuiltinHelper.Arg(args, 0);
            if (key is not JsObject keyObj)
            {
                return JsValue.Undefined;
            }

            return weakMap.WeakMapGet(keyObj);
        }, 1);

        BuiltinHelper.DefineMethod(proto, "set", (thisArg, args) =>
        {
            var weakMap = RequireWeakMap(thisArg);
            var key = BuiltinHelper.Arg(args, 0);
            if (key is not JsObject keyObj)
            {
                throw new Errors.JsTypeError("Invalid value used as weak map key");
            }

            weakMap.WeakMapSet(keyObj, BuiltinHelper.Arg(args, 1));
            return thisArg;
        }, 2);

        BuiltinHelper.DefineMethod(proto, "has", (thisArg, args) =>
        {
            var weakMap = RequireWeakMap(thisArg);
            var key = BuiltinHelper.Arg(args, 0);
            if (key is not JsObject keyObj)
            {
                return JsValue.False;
            }

            return weakMap.WeakMapHas(keyObj) ? JsValue.True : JsValue.False;
        }, 1);

        BuiltinHelper.DefineMethod(proto, "delete", (thisArg, args) =>
        {
            var weakMap = RequireWeakMap(thisArg);
            var key = BuiltinHelper.Arg(args, 0);
            if (key is not JsObject keyObj)
            {
                return JsValue.False;
            }

            return weakMap.WeakMapDelete(keyObj) ? JsValue.True : JsValue.False;
        }, 1);

        // Symbol.toStringTag
        proto.DefineSymbolProperty(JsSymbol.ToStringTag,
            PropertyDescriptor.Data(new JsString("WeakMap"), writable: false, enumerable: false, configurable: true));

        realm.InstallGlobal("WeakMap", ctor);
    }

    private static JsWeakMapObject RequireWeakMap(JsValue value)
    {
        if (value is JsWeakMapObject weakMap)
        {
            return weakMap;
        }

        throw new Errors.JsTypeError("Method requires that 'this' be a WeakMap");
    }
}

internal sealed class JsWeakMapObject : JsObject
{
    private readonly ConditionalWeakTable<JsObject, JsValue> _table = new();

    public JsValue WeakMapGet(JsObject key)
    {
        return _table.TryGetValue(key, out var value) ? value : Undefined;
    }

    public void WeakMapSet(JsObject key, JsValue value)
    {
        _table.AddOrUpdate(key, value);
    }

    public bool WeakMapHas(JsObject key)
    {
        return _table.TryGetValue(key, out _);
    }

    public bool WeakMapDelete(JsObject key)
    {
        return _table.Remove(key);
    }
}
