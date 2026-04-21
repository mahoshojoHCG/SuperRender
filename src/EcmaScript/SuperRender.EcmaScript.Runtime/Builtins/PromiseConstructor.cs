namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

public sealed class PromiseConstructor : IJsInstallable
{
    public static void Install(Realm realm)
    {
        var proto = realm.PromisePrototype;
        JsPromise.DefaultPrototype = proto;

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
                    throw new Errors.JsTypeError("Promise resolver is not a function", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                }

                var promise = new JsPromise(proto);

                var resolveFn = JsFunction.CreateNative("resolve", (_, resolveArgs) =>
                {
                    promise.Resolve(BuiltinHelper.Arg(resolveArgs, 0));
                    return JsValue.Undefined;
                }, 1);

                var rejectFn = JsFunction.CreateNative("reject", (_, rejectArgs) =>
                {
                    promise.Reject(BuiltinHelper.Arg(rejectArgs, 0));
                    return JsValue.Undefined;
                }, 1);

                try
                {
                    executorFn.Call(JsValue.Undefined, [resolveFn, rejectFn]);
                }
                catch (Errors.JsErrorBase ex)
                {
                    promise.Reject(new JsString(ex.Message));
                }

                return promise;
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError("Constructor Promise requires 'new'", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }
        };

        // Prototype pointer wiring — the SG on JsPromise already emits per-instance .then/.catch/.finally;
        // these prototype methods exist for explicit Promise.prototype.then.call(p, ...) usage.
        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "then", (thisArg, args) =>
        {
            var p = RequirePromise(thisArg, "then");
            return p.Then(BuiltinHelper.Arg(args, 0), BuiltinHelper.Arg(args, 1));
        }, 2);

        BuiltinHelper.DefineMethod(proto, "catch", (thisArg, args) =>
        {
            var p = RequirePromise(thisArg, "catch");
            return p.Catch(BuiltinHelper.Arg(args, 0));
        }, 1);

        BuiltinHelper.DefineMethod(proto, "finally", (thisArg, args) =>
        {
            var p = RequirePromise(thisArg, "finally");
            return p.Finally(BuiltinHelper.Arg(args, 0));
        }, 1);

        proto.DefineSymbolProperty(JsSymbol.ToStringTag,
            PropertyDescriptor.Data(new JsString("Promise"), writable: false, enumerable: false, configurable: true));

        // Static methods
        BuiltinHelper.DefineMethod(ctor, "resolve", (_, args) =>
        {
            var value = BuiltinHelper.Arg(args, 0);
            if (value is JsPromise) return value;
            return JsPromise.Resolved(value);
        }, 1);

        BuiltinHelper.DefineMethod(ctor, "reject", (_, args) =>
            JsPromise.Rejected(BuiltinHelper.Arg(args, 0)), 1);

        BuiltinHelper.DefineMethod(ctor, "withResolvers", (_, _) =>
        {
            var promise = new JsPromise(proto);

            var resolveFn = JsFunction.CreateNative("resolve", (_, resolveArgs) =>
            {
                promise.Resolve(BuiltinHelper.Arg(resolveArgs, 0));
                return JsValue.Undefined;
            }, 1);

            var rejectFn = JsFunction.CreateNative("reject", (_, rejectArgs) =>
            {
                promise.Reject(BuiltinHelper.Arg(rejectArgs, 0));
                return JsValue.Undefined;
            }, 1);

            var result = new JsDynamicObject { Prototype = realm.ObjectPrototype };
            result.Set("promise", promise);
            result.Set("resolve", resolveFn);
            result.Set("reject", rejectFn);
            return result;
        }, 0);

        BuiltinHelper.DefineMethod(ctor, "all", (_, args) =>
        {
            var iterable = BuiltinHelper.Arg(args, 0);
            if (iterable is not JsArray arr)
            {
                throw new Errors.JsTypeError("Promise.all requires an iterable", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var resultPromise = new JsPromise(proto);
            var results = new JsValue[arr.DenseLength];
            var remaining = arr.DenseLength;

            if (remaining == 0)
            {
                resultPromise.Resolve(new JsArray(results) { Prototype = realm.ArrayPrototype });
                return resultPromise;
            }

            for (var i = 0; i < arr.DenseLength; i++)
            {
                var index = i;
                var item = arr.GetIndex(i);

                if (item is JsPromise itemPromise)
                {
                    itemPromise.Then(
                        JsFunction.CreateNative(string.Empty, (_, resolveArgs) =>
                        {
                            results[index] = BuiltinHelper.Arg(resolveArgs, 0);
                            remaining--;
                            if (remaining == 0)
                            {
                                resultPromise.Resolve(new JsArray(results) { Prototype = realm.ArrayPrototype });
                            }
                            return JsValue.Undefined;
                        }, 1),
                        JsFunction.CreateNative(string.Empty, (_, rejectArgs) =>
                        {
                            resultPromise.Reject(BuiltinHelper.Arg(rejectArgs, 0));
                            return JsValue.Undefined;
                        }, 1));
                }
                else
                {
                    results[index] = item;
                    remaining--;
                    if (remaining == 0)
                    {
                        resultPromise.Resolve(new JsArray(results) { Prototype = realm.ArrayPrototype });
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
                throw new Errors.JsTypeError("Promise.race requires an iterable", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var resultPromise = new JsPromise(proto);

            for (var i = 0; i < arr.DenseLength; i++)
            {
                var item = arr.GetIndex(i);
                if (item is JsPromise itemPromise)
                {
                    itemPromise.Then(
                        JsFunction.CreateNative(string.Empty, (_, resolveArgs) =>
                        {
                            resultPromise.Resolve(BuiltinHelper.Arg(resolveArgs, 0));
                            return JsValue.Undefined;
                        }, 1),
                        JsFunction.CreateNative(string.Empty, (_, rejectArgs) =>
                        {
                            resultPromise.Reject(BuiltinHelper.Arg(rejectArgs, 0));
                            return JsValue.Undefined;
                        }, 1));
                }
                else
                {
                    resultPromise.Resolve(item);
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
                throw new Errors.JsTypeError("Promise.allSettled requires an iterable", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var resultPromise = new JsPromise(proto);
            var results = new JsValue[arr.DenseLength];
            var remaining = arr.DenseLength;

            if (remaining == 0)
            {
                resultPromise.Resolve(new JsArray(results) { Prototype = realm.ArrayPrototype });
                return resultPromise;
            }

            for (var i = 0; i < arr.DenseLength; i++)
            {
                var index = i;
                var item = arr.GetIndex(i);

                if (item is JsPromise itemPromise)
                {
                    itemPromise.Then(
                        JsFunction.CreateNative(string.Empty, (_, resolveArgs) =>
                        {
                            var r = new JsDynamicObject();
                            r.Set("status", new JsString("fulfilled"));
                            r.Set("value", BuiltinHelper.Arg(resolveArgs, 0));
                            results[index] = r;
                            remaining--;
                            if (remaining == 0)
                            {
                                resultPromise.Resolve(new JsArray(results) { Prototype = realm.ArrayPrototype });
                            }
                            return JsValue.Undefined;
                        }, 1),
                        JsFunction.CreateNative(string.Empty, (_, rejectArgs) =>
                        {
                            var r = new JsDynamicObject();
                            r.Set("status", new JsString("rejected"));
                            r.Set("reason", BuiltinHelper.Arg(rejectArgs, 0));
                            results[index] = r;
                            remaining--;
                            if (remaining == 0)
                            {
                                resultPromise.Resolve(new JsArray(results) { Prototype = realm.ArrayPrototype });
                            }
                            return JsValue.Undefined;
                        }, 1));
                }
                else
                {
                    var r = new JsDynamicObject();
                    r.Set("status", new JsString("fulfilled"));
                    r.Set("value", item);
                    results[index] = r;
                    remaining--;
                    if (remaining == 0)
                    {
                        resultPromise.Resolve(new JsArray(results) { Prototype = realm.ArrayPrototype });
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
                throw new Errors.JsTypeError("Promise.any requires an iterable", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var resultPromise = new JsPromise(proto);
            var errors = new JsValue[arr.DenseLength];
            var remaining = arr.DenseLength;

            if (remaining == 0)
            {
                var aggError = new JsDynamicObject { Prototype = realm.ErrorPrototype };
                aggError.Set("message", new JsString("All promises were rejected"));
                aggError.Set("errors", new JsArray(errors) { Prototype = realm.ArrayPrototype });
                resultPromise.Reject(aggError);
                return resultPromise;
            }

            for (var i = 0; i < arr.DenseLength; i++)
            {
                var index = i;
                var item = arr.GetIndex(i);

                if (item is JsPromise itemPromise)
                {
                    itemPromise.Then(
                        JsFunction.CreateNative(string.Empty, (_, resolveArgs) =>
                        {
                            resultPromise.Resolve(BuiltinHelper.Arg(resolveArgs, 0));
                            return JsValue.Undefined;
                        }, 1),
                        JsFunction.CreateNative(string.Empty, (_, rejectArgs) =>
                        {
                            errors[index] = BuiltinHelper.Arg(rejectArgs, 0);
                            remaining--;
                            if (remaining == 0)
                            {
                                var aggError = new JsDynamicObject { Prototype = realm.ErrorPrototype };
                                aggError.Set("message", new JsString("All promises were rejected"));
                                aggError.Set("errors", new JsArray(errors) { Prototype = realm.ArrayPrototype });
                                resultPromise.Reject(aggError);
                            }
                            return JsValue.Undefined;
                        }, 1));
                }
                else
                {
                    resultPromise.Resolve(item);
                    break;
                }
            }

            return resultPromise;
        }, 1);

        realm.InstallGlobal("Promise", ctor);
    }

    private static JsPromise RequirePromise(JsValue value, string member)
    {
        if (value is JsPromise promise)
        {
            return promise;
        }

        throw new Errors.JsTypeError(
            $"Method Promise.prototype.{member} called on incompatible receiver",
            ExecutionContext.CurrentLine,
            ExecutionContext.CurrentColumn);
    }

    // Back-compat static forwarders for callers still using the old API surface.
    public static void ResolvePromise(JsPromise promise, JsValue value, Realm _) => promise.Resolve(value);
    public static void RejectPromise(JsPromise promise, JsValue reason) => promise.Reject(reason);
    public static JsPromise PromiseThen(JsPromise promise, JsFunction? onFulfilled, JsFunction? onRejected, Realm _)
        => promise.AttachHandlers(onFulfilled, onRejected);
}
