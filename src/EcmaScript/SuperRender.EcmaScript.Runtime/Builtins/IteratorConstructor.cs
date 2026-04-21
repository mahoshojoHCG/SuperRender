namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

public sealed partial class IteratorConstructor : IJsInstallable
{
    public static void Install(Realm realm)
    {
        var iterProto = realm.IteratorPrototype;

        // Iterator is not a traditional constructor — it's an abstract class
        var ctor = JsFunction.CreateNative("Iterator", (_, _) =>
        {
            throw new Errors.JsTypeError("Iterator is not a constructor", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }, 0);
        ctor.Prototype = realm.FunctionPrototype;
        ctor.PrototypeObject = iterProto;

        // Static: Iterator.from(obj)
        BuiltinHelper.DefineMethod(ctor, "from", (_, args) =>
        {
            var obj = BuiltinHelper.Arg(args, 0);

            // If obj already has Symbol.iterator, get iterator from it
            if (obj is JsObject jsObj && jsObj.TryGetSymbolProperty(JsSymbol.Iterator, out var iterFn) && iterFn is JsFunction fn)
            {
                var iter = fn.Call(obj, []);
                if (iter is JsObject iterObj)
                {
                    // If it already inherits from IteratorPrototype, return as-is
                    if (HasPrototypeInChain(iterObj, iterProto))
                        return iterObj;

                    // Wrap it
                    return WrapIterator(iterObj, realm);
                }
            }

            // If obj itself looks like an iterator (has .next()), wrap it
            if (obj is JsObject candidate)
            {
                var nextProp = candidate.Get("next");
                if (nextProp is JsFunction)
                    return WrapIterator(candidate, realm);
            }

            throw new Errors.JsTypeError("object is not iterable", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }, 1);

        // === Lazy helper methods on IteratorPrototype ===

        BuiltinHelper.DefineMethod(iterProto, "map", (thisArg, args) =>
        {
            var callback = RequireFunction(BuiltinHelper.Arg(args, 0));
            var counter = 0;
            return CreateHelperIterator(thisArg, realm, () =>
            {
                var next = CallNext(thisArg);
                if (IsDone(next)) return next;
                var value = GetValue(next);
                var mapped = callback.Call(JsValue.Undefined, [value, JsNumber.Create(counter++)]);
                return BuiltinHelper.CreateIteratorResult(mapped, false);
            });
        }, 1);

        BuiltinHelper.DefineMethod(iterProto, "filter", (thisArg, args) =>
        {
            var predicate = RequireFunction(BuiltinHelper.Arg(args, 0));
            var counter = 0;
            return CreateHelperIterator(thisArg, realm, () =>
            {
                while (true)
                {
                    var next = CallNext(thisArg);
                    if (IsDone(next)) return next;
                    var value = GetValue(next);
                    if (predicate.Call(JsValue.Undefined, [value, JsNumber.Create(counter++)]).ToBoolean())
                        return BuiltinHelper.CreateIteratorResult(value, false);
                }
            });
        }, 1);

        BuiltinHelper.DefineMethod(iterProto, "take", (thisArg, args) =>
        {
            var limit = (int)BuiltinHelper.Arg(args, 0).ToNumber();
            if (limit < 0)
                throw new Errors.JsRangeError("take limit must be non-negative", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            var remaining = limit;
            return CreateHelperIterator(thisArg, realm, () =>
            {
                if (remaining <= 0) return BuiltinHelper.CreateIteratorResult(JsValue.Undefined, true);
                remaining--;
                return CallNext(thisArg);
            });
        }, 1);

        BuiltinHelper.DefineMethod(iterProto, "drop", (thisArg, args) =>
        {
            var count = (int)BuiltinHelper.Arg(args, 0).ToNumber();
            if (count < 0)
                throw new Errors.JsRangeError("drop count must be non-negative", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            var dropped = false;
            var toDrop = count;
            return CreateHelperIterator(thisArg, realm, () =>
            {
                if (!dropped)
                {
                    for (var i = 0; i < toDrop; i++)
                    {
                        var skip = CallNext(thisArg);
                        if (IsDone(skip)) return skip;
                    }

                    dropped = true;
                }

                return CallNext(thisArg);
            });
        }, 1);

        BuiltinHelper.DefineMethod(iterProto, "flatMap", (thisArg, args) =>
        {
            var mapper = RequireFunction(BuiltinHelper.Arg(args, 0));
            var counter = 0;
            JsValue? innerIterator = null;
            return CreateHelperIterator(thisArg, realm, () =>
            {
                while (true)
                {
                    if (innerIterator is not null)
                    {
                        var innerNext = CallNext(innerIterator);
                        if (!IsDone(innerNext)) return innerNext;
                        innerIterator = null;
                    }

                    var next = CallNext(thisArg);
                    if (IsDone(next)) return next;
                    var value = GetValue(next);
                    var mapped = mapper.Call(JsValue.Undefined, [value, JsNumber.Create(counter++)]);

                    // If mapped is iterable, iterate it
                    if (mapped is JsObject mappedObj && mappedObj.TryGetSymbolProperty(JsSymbol.Iterator, out var iterFn) && iterFn is JsFunction fn)
                    {
                        innerIterator = fn.Call(mapped, []);
                        continue;
                    }

                    // Otherwise yield the value directly
                    return BuiltinHelper.CreateIteratorResult(mapped, false);
                }
            });
        }, 1);

        // === Eager consuming methods ===

        BuiltinHelper.DefineMethod(iterProto, "reduce", (thisArg, args) =>
        {
            var reducer = RequireFunction(BuiltinHelper.Arg(args, 0));
            JsValue accumulator;
            if (args.Length > 1)
            {
                accumulator = args[1];
            }
            else
            {
                var first = CallNext(thisArg);
                if (IsDone(first))
                    throw new Errors.JsTypeError("Reduce of empty iterator with no initial value", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                accumulator = GetValue(first);
            }

            while (true)
            {
                var next = CallNext(thisArg);
                if (IsDone(next)) return accumulator;
                accumulator = reducer.Call(JsValue.Undefined, [accumulator, GetValue(next)]);
            }
        }, 1);

        BuiltinHelper.DefineMethod(iterProto, "toArray", (thisArg, _) =>
        {
            var result = new JsArray { Prototype = realm.ArrayPrototype };
            while (true)
            {
                var next = CallNext(thisArg);
                if (IsDone(next)) return result;
                result.Push(GetValue(next));
            }
        }, 0);

        BuiltinHelper.DefineMethod(iterProto, "forEach", (thisArg, args) =>
        {
            var callback = RequireFunction(BuiltinHelper.Arg(args, 0));
            var counter = 0;
            while (true)
            {
                var next = CallNext(thisArg);
                if (IsDone(next)) return JsValue.Undefined;
                callback.Call(JsValue.Undefined, [GetValue(next), JsNumber.Create(counter++)]);
            }
        }, 1);

        BuiltinHelper.DefineMethod(iterProto, "some", (thisArg, args) =>
        {
            var predicate = RequireFunction(BuiltinHelper.Arg(args, 0));
            var counter = 0;
            while (true)
            {
                var next = CallNext(thisArg);
                if (IsDone(next)) return JsValue.False;
                if (predicate.Call(JsValue.Undefined, [GetValue(next), JsNumber.Create(counter++)]).ToBoolean())
                    return JsValue.True;
            }
        }, 1);

        BuiltinHelper.DefineMethod(iterProto, "every", (thisArg, args) =>
        {
            var predicate = RequireFunction(BuiltinHelper.Arg(args, 0));
            var counter = 0;
            while (true)
            {
                var next = CallNext(thisArg);
                if (IsDone(next)) return JsValue.True;
                if (!predicate.Call(JsValue.Undefined, [GetValue(next), JsNumber.Create(counter++)]).ToBoolean())
                    return JsValue.False;
            }
        }, 1);

        BuiltinHelper.DefineMethod(iterProto, "find", (thisArg, args) =>
        {
            var predicate = RequireFunction(BuiltinHelper.Arg(args, 0));
            var counter = 0;
            while (true)
            {
                var next = CallNext(thisArg);
                if (IsDone(next)) return JsValue.Undefined;
                var value = GetValue(next);
                if (predicate.Call(JsValue.Undefined, [value, JsNumber.Create(counter++)]).ToBoolean())
                    return value;
            }
        }, 1);

        // Symbol.iterator on IteratorPrototype returns self
        iterProto.DefineSymbolProperty(JsSymbol.Iterator,
            PropertyDescriptor.Data(
                BuiltinHelper.__JsFn_SymbolIteratorSelf(),
                writable: false, enumerable: false, configurable: true));

        realm.InstallGlobal("Iterator", ctor);
    }

    private static JsObject CallNext(JsValue iterator)
    {
        if (iterator is not JsObject obj) throw new Errors.JsTypeError("Iterator is not an object");
        var nextFn = obj.Get("next");
        if (nextFn is not JsFunction fn) throw new Errors.JsTypeError("Iterator.next is not a function");
        var result = fn.Call(iterator, []);
        if (result is not JsObject resultObj) throw new Errors.JsTypeError("Iterator result is not an object");
        return resultObj;
    }

    private static bool IsDone(JsObject result)
    {
        return result.Get("done").ToBoolean();
    }

    private static JsValue GetValue(JsObject result)
    {
        return result.Get("value");
    }

    private static JsFunction RequireFunction(JsValue value)
    {
        if (value is JsFunction fn) return fn;
        throw new Errors.JsTypeError("callback must be a function", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }

    private static bool HasPrototypeInChain(JsObject obj, JsObject proto)
    {
        var current = obj.Prototype;
        while (current is not null)
        {
            if (ReferenceEquals(current, proto)) return true;
            current = current.Prototype;
        }

        return false;
    }

    private static JsDynamicObject WrapIterator(JsObject source, Realm realm)
    {
        var wrapper = new JsDynamicObject { Prototype = realm.IteratorPrototype };
        BuiltinHelper.DefineMethod(wrapper, "next", (_, args) =>
        {
            var nextFn = source.Get("next");
            if (nextFn is JsFunction fn)
                return fn.Call(source, args);
            throw new Errors.JsTypeError("Iterator.next is not a function");
        }, 0);
        return wrapper;
    }

    private static JsDynamicObject CreateHelperIterator(JsValue source, Realm realm, Func<JsObject> nextFn)
    {
        var iterator = new JsDynamicObject { Prototype = realm.IteratorPrototype };
        BuiltinHelper.DefineMethod(iterator, "next", (_, _) => nextFn(), 0);
        return iterator;
    }
}
