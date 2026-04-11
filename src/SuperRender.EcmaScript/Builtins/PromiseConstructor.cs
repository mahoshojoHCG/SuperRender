namespace SuperRender.EcmaScript.Builtins;

using SuperRender.EcmaScript.Runtime;

public static class PromiseConstructor
{
    public static void Install(Realm realm)
    {
        var proto = realm.PromisePrototype;

        var ctor = new JsFunction
        {
            Name = "Promise",
            Length = 1,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            ConstructTarget = args =>
            {
                var executor = BuiltinHelper.Arg(args, 0);
                if (executor is not JsFunction executorFn)
                {
                    throw new Errors.JsTypeError("Promise resolver is not a function");
                }

                var promise = new JsPromiseObject { Prototype = realm.PromisePrototype };

                var resolveFn = JsFunction.CreateNative("resolve", (_, resolveArgs) =>
                {
                    var value = BuiltinHelper.Arg(resolveArgs, 0);
                    ResolvePromise(promise, value, realm);
                    return JsValue.Undefined;
                }, 1);

                var rejectFn = JsFunction.CreateNative("reject", (_, rejectArgs) =>
                {
                    var reason = BuiltinHelper.Arg(rejectArgs, 0);
                    RejectPromise(promise, reason);
                    return JsValue.Undefined;
                }, 1);

                try
                {
                    executorFn.Call(JsValue.Undefined, [resolveFn, rejectFn]);
                }
                catch (Exception ex) when (ex is Errors.JsErrorBase)
                {
                    RejectPromise(promise, new JsString(ex.Message));
                }

                return promise;
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError("Constructor Promise requires 'new'");
            }
        };

        // Prototype methods
        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "then", (thisArg, args) =>
        {
            var promise = RequirePromise(thisArg);
            var onFulfilled = BuiltinHelper.Arg(args, 0) as JsFunction;
            var onRejected = BuiltinHelper.Arg(args, 1) as JsFunction;
            return PromiseThen(promise, onFulfilled, onRejected, realm);
        }, 2);

        BuiltinHelper.DefineMethod(proto, "catch", (thisArg, args) =>
        {
            var promise = RequirePromise(thisArg);
            var onRejected = BuiltinHelper.Arg(args, 0) as JsFunction;
            return PromiseThen(promise, null, onRejected, realm);
        }, 1);

        BuiltinHelper.DefineMethod(proto, "finally", (thisArg, args) =>
        {
            var promise = RequirePromise(thisArg);
            var onFinally = BuiltinHelper.Arg(args, 0) as JsFunction;

            JsFunction? thenHandler = null;
            JsFunction? catchHandler = null;

            if (onFinally is not null)
            {
                thenHandler = JsFunction.CreateNative("", (_, thenArgs) =>
                {
                    var value = BuiltinHelper.Arg(thenArgs, 0);
                    onFinally.Call(JsValue.Undefined, []);
                    return value;
                }, 1);

                catchHandler = JsFunction.CreateNative("", (_, catchArgs) =>
                {
                    var reason = BuiltinHelper.Arg(catchArgs, 0);
                    onFinally.Call(JsValue.Undefined, []);
                    throw new PromiseRejectedException(reason);
                }, 1);
            }

            return PromiseThen(promise, thenHandler, catchHandler, realm);
        }, 1);

        // Symbol.toStringTag
        proto.DefineSymbolProperty(JsSymbol.ToStringTag,
            PropertyDescriptor.Data(new JsString("Promise"), writable: false, enumerable: false, configurable: true));

        // Static methods
        BuiltinHelper.DefineMethod(ctor, "resolve", (_, args) =>
        {
            var value = BuiltinHelper.Arg(args, 0);
            if (value is JsPromiseObject)
            {
                return value;
            }

            var promise = new JsPromiseObject { Prototype = realm.PromisePrototype };
            ResolvePromise(promise, value, realm);
            return promise;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "reject", (_, args) =>
        {
            var reason = BuiltinHelper.Arg(args, 0);
            var promise = new JsPromiseObject { Prototype = realm.PromisePrototype };
            RejectPromise(promise, reason);
            return promise;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "all", (_, args) =>
        {
            var iterable = BuiltinHelper.Arg(args, 0);
            if (iterable is not JsArray arr)
            {
                throw new Errors.JsTypeError("Promise.all requires an iterable");
            }

            var resultPromise = new JsPromiseObject { Prototype = realm.PromisePrototype };
            var results = new JsValue[arr.DenseLength];
            var remaining = arr.DenseLength;

            if (remaining == 0)
            {
                ResolvePromise(resultPromise, new JsArray(results) { Prototype = realm.ArrayPrototype }, realm);
                return resultPromise;
            }

            for (var i = 0; i < arr.DenseLength; i++)
            {
                var index = i;
                var item = arr.GetIndex(i);

                if (item is JsPromiseObject itemPromise)
                {
                    PromiseThen(itemPromise,
                        JsFunction.CreateNative("", (_, resolveArgs) =>
                        {
                            results[index] = BuiltinHelper.Arg(resolveArgs, 0);
                            remaining--;
                            if (remaining == 0)
                            {
                                ResolvePromise(resultPromise, new JsArray(results) { Prototype = realm.ArrayPrototype }, realm);
                            }

                            return JsValue.Undefined;
                        }, 1),
                        JsFunction.CreateNative("", (_, rejectArgs) =>
                        {
                            RejectPromise(resultPromise, BuiltinHelper.Arg(rejectArgs, 0));
                            return JsValue.Undefined;
                        }, 1),
                        realm);
                }
                else
                {
                    results[index] = item;
                    remaining--;
                    if (remaining == 0)
                    {
                        ResolvePromise(resultPromise, new JsArray(results) { Prototype = realm.ArrayPrototype }, realm);
                    }
                }
            }

            return resultPromise;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "race", (_, args) =>
        {
            var iterable = BuiltinHelper.Arg(args, 0);
            if (iterable is not JsArray arr)
            {
                throw new Errors.JsTypeError("Promise.race requires an iterable");
            }

            var resultPromise = new JsPromiseObject { Prototype = realm.PromisePrototype };

            for (var i = 0; i < arr.DenseLength; i++)
            {
                var item = arr.GetIndex(i);
                if (item is JsPromiseObject itemPromise)
                {
                    PromiseThen(itemPromise,
                        JsFunction.CreateNative("", (_, resolveArgs) =>
                        {
                            ResolvePromise(resultPromise, BuiltinHelper.Arg(resolveArgs, 0), realm);
                            return JsValue.Undefined;
                        }, 1),
                        JsFunction.CreateNative("", (_, rejectArgs) =>
                        {
                            RejectPromise(resultPromise, BuiltinHelper.Arg(rejectArgs, 0));
                            return JsValue.Undefined;
                        }, 1),
                        realm);
                }
                else
                {
                    ResolvePromise(resultPromise, item, realm);
                    break;
                }
            }

            return resultPromise;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "allSettled", (_, args) =>
        {
            var iterable = BuiltinHelper.Arg(args, 0);
            if (iterable is not JsArray arr)
            {
                throw new Errors.JsTypeError("Promise.allSettled requires an iterable");
            }

            var resultPromise = new JsPromiseObject { Prototype = realm.PromisePrototype };
            var results = new JsValue[arr.DenseLength];
            var remaining = arr.DenseLength;

            if (remaining == 0)
            {
                ResolvePromise(resultPromise, new JsArray(results) { Prototype = realm.ArrayPrototype }, realm);
                return resultPromise;
            }

            for (var i = 0; i < arr.DenseLength; i++)
            {
                var index = i;
                var item = arr.GetIndex(i);

                if (item is JsPromiseObject itemPromise)
                {
                    PromiseThen(itemPromise,
                        JsFunction.CreateNative("", (_, resolveArgs) =>
                        {
                            var result = new JsObject();
                            result.Set("status", new JsString("fulfilled"));
                            result.Set("value", BuiltinHelper.Arg(resolveArgs, 0));
                            results[index] = result;
                            remaining--;
                            if (remaining == 0)
                            {
                                ResolvePromise(resultPromise, new JsArray(results) { Prototype = realm.ArrayPrototype }, realm);
                            }

                            return JsValue.Undefined;
                        }, 1),
                        JsFunction.CreateNative("", (_, rejectArgs) =>
                        {
                            var result = new JsObject();
                            result.Set("status", new JsString("rejected"));
                            result.Set("reason", BuiltinHelper.Arg(rejectArgs, 0));
                            results[index] = result;
                            remaining--;
                            if (remaining == 0)
                            {
                                ResolvePromise(resultPromise, new JsArray(results) { Prototype = realm.ArrayPrototype }, realm);
                            }

                            return JsValue.Undefined;
                        }, 1),
                        realm);
                }
                else
                {
                    var result = new JsObject();
                    result.Set("status", new JsString("fulfilled"));
                    result.Set("value", item);
                    results[index] = result;
                    remaining--;
                    if (remaining == 0)
                    {
                        ResolvePromise(resultPromise, new JsArray(results) { Prototype = realm.ArrayPrototype }, realm);
                    }
                }
            }

            return resultPromise;
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "any", (_, args) =>
        {
            var iterable = BuiltinHelper.Arg(args, 0);
            if (iterable is not JsArray arr)
            {
                throw new Errors.JsTypeError("Promise.any requires an iterable");
            }

            var resultPromise = new JsPromiseObject { Prototype = realm.PromisePrototype };
            var errors = new JsValue[arr.DenseLength];
            var remaining = arr.DenseLength;

            if (remaining == 0)
            {
                var aggError = new JsObject { Prototype = realm.ErrorPrototype };
                aggError.Set("message", new JsString("All promises were rejected"));
                aggError.Set("errors", new JsArray(errors) { Prototype = realm.ArrayPrototype });
                RejectPromise(resultPromise, aggError);
                return resultPromise;
            }

            for (var i = 0; i < arr.DenseLength; i++)
            {
                var index = i;
                var item = arr.GetIndex(i);

                if (item is JsPromiseObject itemPromise)
                {
                    PromiseThen(itemPromise,
                        JsFunction.CreateNative("", (_, resolveArgs) =>
                        {
                            ResolvePromise(resultPromise, BuiltinHelper.Arg(resolveArgs, 0), realm);
                            return JsValue.Undefined;
                        }, 1),
                        JsFunction.CreateNative("", (_, rejectArgs) =>
                        {
                            errors[index] = BuiltinHelper.Arg(rejectArgs, 0);
                            remaining--;
                            if (remaining == 0)
                            {
                                var aggError = new JsObject { Prototype = realm.ErrorPrototype };
                                aggError.Set("message", new JsString("All promises were rejected"));
                                aggError.Set("errors", new JsArray(errors) { Prototype = realm.ArrayPrototype });
                                RejectPromise(resultPromise, aggError);
                            }

                            return JsValue.Undefined;
                        }, 1),
                        realm);
                }
                else
                {
                    ResolvePromise(resultPromise, item, realm);
                    break;
                }
            }

            return resultPromise;
        }, 1);

        realm.InstallGlobal("Promise", ctor);
    }

    internal static void ResolvePromise(JsPromiseObject promise, JsValue value, Realm realm)
    {
        if (promise.State != JsPromiseObject.PromiseState.Pending)
        {
            return;
        }

        // If value is a thenable, chain
        if (value is JsPromiseObject thenablePromise)
        {
            PromiseThen(thenablePromise,
                JsFunction.CreateNative("", (_, resolveArgs) =>
                {
                    ResolvePromise(promise, BuiltinHelper.Arg(resolveArgs, 0), realm);
                    return JsValue.Undefined;
                }, 1),
                JsFunction.CreateNative("", (_, rejectArgs) =>
                {
                    RejectPromise(promise, BuiltinHelper.Arg(rejectArgs, 0));
                    return JsValue.Undefined;
                }, 1),
                realm);
            return;
        }

        promise.State = JsPromiseObject.PromiseState.Fulfilled;
        promise.Result = value;
        TriggerReactions(promise);
    }

    internal static void RejectPromise(JsPromiseObject promise, JsValue reason)
    {
        if (promise.State != JsPromiseObject.PromiseState.Pending)
        {
            return;
        }

        promise.State = JsPromiseObject.PromiseState.Rejected;
        promise.Result = reason;
        TriggerReactions(promise);
    }

    internal static JsPromiseObject PromiseThen(JsPromiseObject promise, JsFunction? onFulfilled, JsFunction? onRejected, Realm realm)
    {
        var resultPromise = new JsPromiseObject { Prototype = realm.PromisePrototype };

        var reaction = new PromiseReaction(resultPromise, onFulfilled, onRejected, realm);

        if (promise.State == JsPromiseObject.PromiseState.Pending)
        {
            promise.Reactions.Add(reaction);
        }
        else
        {
            MicrotaskQueue.Enqueue(() => ProcessReaction(reaction, promise.State, promise.Result));
        }

        return resultPromise;
    }

    private static void TriggerReactions(JsPromiseObject promise)
    {
        var reactions = promise.Reactions.ToArray();
        promise.Reactions.Clear();

        foreach (var reaction in reactions)
        {
            var state = promise.State;
            var result = promise.Result;
            MicrotaskQueue.Enqueue(() => ProcessReaction(reaction, state, result));
        }
    }

    private static void ProcessReaction(PromiseReaction reaction, JsPromiseObject.PromiseState state, JsValue value)
    {
        var handler = state == JsPromiseObject.PromiseState.Fulfilled
            ? reaction.OnFulfilled
            : reaction.OnRejected;

        try
        {
            if (handler is not null)
            {
                var result = handler.Call(JsValue.Undefined, [value]);
                ResolvePromise(reaction.ResultPromise, result, reaction.Realm);
            }
            else if (state == JsPromiseObject.PromiseState.Fulfilled)
            {
                ResolvePromise(reaction.ResultPromise, value, reaction.Realm);
            }
            else
            {
                RejectPromise(reaction.ResultPromise, value);
            }
        }
        catch (PromiseRejectedException ex)
        {
            RejectPromise(reaction.ResultPromise, ex.Reason);
        }
        catch (Errors.JsErrorBase ex)
        {
            RejectPromise(reaction.ResultPromise, new JsString(ex.Message));
        }
    }

    private static JsPromiseObject RequirePromise(JsValue value)
    {
        if (value is JsPromiseObject promise)
        {
            return promise;
        }

        throw new Errors.JsTypeError("Method requires that 'this' be a Promise");
    }

    internal sealed record PromiseReaction(
        JsPromiseObject ResultPromise,
        JsFunction? OnFulfilled,
        JsFunction? OnRejected,
        Realm Realm);
}

internal sealed class JsPromiseObject : JsObject
{
    internal enum PromiseState
    {
        Pending,
        Fulfilled,
        Rejected
    }

    internal PromiseState State { get; set; } = PromiseState.Pending;
    internal JsValue Result { get; set; } = Undefined;
    internal List<PromiseConstructor.PromiseReaction> Reactions { get; } = [];
}

internal sealed class PromiseRejectedException : Exception
{
    public JsValue Reason { get; }

    public PromiseRejectedException(JsValue reason)
        : base("Promise rejected")
    {
        Reason = reason;
    }
}

internal static class MicrotaskQueue
{
    private static readonly Queue<Action> Tasks = new();
    private static bool _draining;

    internal static void Enqueue(Action task)
    {
        Tasks.Enqueue(task);
        if (!_draining)
        {
            Drain();
        }
    }

    private static void Drain()
    {
        _draining = true;
        try
        {
            while (Tasks.Count > 0)
            {
                Tasks.Dequeue()();
            }
        }
        finally
        {
            _draining = false;
        }
    }
}
