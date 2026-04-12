namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

public static class FunctionConstructor
{
    public static void Install(Realm realm)
    {
        var proto = realm.FunctionPrototype;

        // Function.prototype.call
        BuiltinHelper.DefineMethod(proto, "call", (thisArg, args) =>
        {
            if (thisArg is not JsFunction fn)
            {
                throw new Errors.JsTypeError("Function.prototype.call called on non-function", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var callThisArg = BuiltinHelper.Arg(args, 0);
            var callArgs = args.Length > 1 ? args[1..] : [];
            return fn.Call(callThisArg, callArgs);
        }, 1);

        // Function.prototype.apply
        BuiltinHelper.DefineMethod(proto, "apply", (thisArg, args) =>
        {
            if (thisArg is not JsFunction fn)
            {
                throw new Errors.JsTypeError("Function.prototype.apply called on non-function", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var callThisArg = BuiltinHelper.Arg(args, 0);
            var argArray = BuiltinHelper.Arg(args, 1);

            JsValue[] callArgs;
            if (argArray is JsNull or JsUndefined)
            {
                callArgs = [];
            }
            else if (argArray is JsArray arr)
            {
                callArgs = new JsValue[arr.DenseLength];
                for (var i = 0; i < arr.DenseLength; i++)
                {
                    callArgs[i] = arr.GetIndex(i);
                }
            }
            else if (argArray is JsObject obj)
            {
                var len = BuiltinHelper.GetLength(obj);
                callArgs = new JsValue[len];
                for (var i = 0; i < len; i++)
                {
                    callArgs[i] = obj.Get(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            else
            {
                throw new Errors.JsTypeError("CreateListFromArrayLike called on non-object", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            return fn.Call(callThisArg, callArgs);
        }, 2);

        // Function.prototype.bind
        BuiltinHelper.DefineMethod(proto, "bind", (thisArg, args) =>
        {
            if (thisArg is not JsFunction fn)
            {
                throw new Errors.JsTypeError("Function.prototype.bind called on non-function", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var boundThis = BuiltinHelper.Arg(args, 0);
            var boundArgs = args.Length > 1 ? args[1..] : Array.Empty<JsValue>();

            var bound = new JsFunction
            {
                Name = "bound " + fn.Name,
                Length = Math.Max(0, fn.Length - boundArgs.Length),
                IsConstructor = fn.IsConstructor,
                Prototype = realm.FunctionPrototype,
                CallTarget = (_, callArgs) =>
                {
                    var combinedArgs = new JsValue[boundArgs.Length + callArgs.Length];
                    boundArgs.CopyTo(combinedArgs, 0);
                    callArgs.CopyTo(combinedArgs, boundArgs.Length);
                    return fn.Call(boundThis, combinedArgs);
                }
            };

            if (fn.IsConstructor)
            {
                bound.ConstructTarget = constructArgs =>
                {
                    var combinedArgs = new JsValue[boundArgs.Length + constructArgs.Length];
                    boundArgs.CopyTo(combinedArgs, 0);
                    constructArgs.CopyTo(combinedArgs, boundArgs.Length);
                    return fn.Construct(combinedArgs);
                };
            }

            return bound;
        }, 1);

        // Function.prototype.toString
        BuiltinHelper.DefineMethod(proto, "toString", (thisArg, _) =>
        {
            if (thisArg is JsFunction fn)
            {
                return new JsString("function " + fn.Name + "() { [native code] }");
            }

            throw new Errors.JsTypeError("Function.prototype.toString requires that 'this' be a Function", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }, 0);

        // Function.prototype[Symbol.hasInstance]
        proto.DefineSymbolProperty(JsSymbol.HasInstance,
            PropertyDescriptor.Data(JsFunction.CreateNative("[Symbol.hasInstance]", (thisArg, args) =>
            {
                if (thisArg is not JsFunction ctor)
                {
                    return JsValue.False;
                }

                var target = BuiltinHelper.Arg(args, 0);
                if (target is not JsObject obj)
                {
                    return JsValue.False;
                }

                var protoObj = ctor.PrototypeObject;
                if (protoObj is null)
                {
                    return JsValue.False;
                }

                var current = obj.Prototype;
                while (current is not null)
                {
                    if (ReferenceEquals(current, protoObj))
                    {
                        return JsValue.True;
                    }

                    current = current.Prototype;
                }

                return JsValue.False;
            }, 1), writable: false, enumerable: false, configurable: false));

        // Install a non-constructable Function as a global (just for typeof checks and .prototype access)
        var fnCtor = JsFunction.CreateNative("Function", (_, _) =>
        {
            throw new Errors.JsTypeError("Dynamic code evaluation is not supported", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }, 1);
        fnCtor.Prototype = realm.FunctionPrototype;
        fnCtor.PrototypeObject = proto;

        realm.InstallGlobal("Function", fnCtor);
    }
}
