namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

internal static class BuiltinHelper
{
    internal static void DefineMethod(JsObject target, string name, Func<JsValue, JsValue[], JsValue> impl, int length)
    {
        var fn = JsFunction.CreateNative(name, impl, length);
        target.DefineOwnProperty(name, PropertyDescriptor.Data(fn, writable: true, enumerable: false, configurable: true));
    }

    internal static void DefineProperty(JsObject target, string name, JsValue value)
    {
        target.DefineOwnProperty(name, PropertyDescriptor.Data(value, writable: false, enumerable: false, configurable: false));
    }

    internal static void DefineGetter(JsObject target, string name, Func<JsValue, JsValue[], JsValue> getter)
    {
        var fn = JsFunction.CreateNative("get " + name, getter, 0);
        target.DefineOwnProperty(name, PropertyDescriptor.Accessor(fn, null, enumerable: false, configurable: true));
    }

    internal static JsValue Arg(JsValue[] args, int index)
    {
        return index < args.Length ? args[index] : JsValue.Undefined;
    }

    internal static int GetLength(JsObject obj)
    {
        return (int)obj.Get("length").ToNumber();
    }

    internal static JsObject CreateIteratorResult(JsValue value, bool done)
    {
        var result = new JsObject();
        result.DefineOwnProperty("value", PropertyDescriptor.Data(value, writable: true, enumerable: true, configurable: true));
        result.DefineOwnProperty("done", PropertyDescriptor.Data(done ? JsValue.True : JsValue.False, writable: true, enumerable: true, configurable: true));
        return result;
    }

    internal static JsObject CreateListIterator(IReadOnlyList<JsValue> items, Realm realm)
    {
        var index = 0;
        var iterator = new JsObject { Prototype = realm.IteratorPrototype };

        DefineMethod(iterator, "next", (_, _) =>
        {
            if (index < items.Count)
            {
                var val = items[index];
                index++;
                return CreateIteratorResult(val, false);
            }

            return CreateIteratorResult(JsValue.Undefined, true);
        }, 0);

        iterator.DefineSymbolProperty(JsSymbol.Iterator, PropertyDescriptor.Data(
            JsFunction.CreateNative("[Symbol.iterator]", (self, _) => self, 0),
            writable: false, enumerable: false, configurable: true));

        return iterator;
    }
}
