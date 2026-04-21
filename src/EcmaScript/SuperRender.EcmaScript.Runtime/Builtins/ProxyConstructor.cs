namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

public sealed class ProxyConstructor : IJsInstallable
{
    public static void Install(Realm realm)
    {
        var ctor = new JsFunction
        {
            Name = "Proxy",
            Length = 2,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            ConstructTarget = args =>
            {
                var target = BuiltinHelper.Arg(args, 0);
                var handler = BuiltinHelper.Arg(args, 1);

                if (target is not JsObject targetObj)
                {
                    throw new Errors.JsTypeError("Cannot create proxy with a non-object as target", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                }

                if (handler is not JsObject handlerObj)
                {
                    throw new Errors.JsTypeError("Cannot create proxy with a non-object as handler", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                }

                return new JsProxyObject(targetObj, handlerObj);
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError("Constructor Proxy requires 'new'", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }
        };

        // Proxy.revocable
        BuiltinHelper.DefineMethod(ctor, "revocable", (_, args) =>
        {
            var target = BuiltinHelper.Arg(args, 0);
            var handler = BuiltinHelper.Arg(args, 1);

            if (target is not JsObject targetObj)
            {
                throw new Errors.JsTypeError("Cannot create proxy with a non-object as target", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            if (handler is not JsObject handlerObj)
            {
                throw new Errors.JsTypeError("Cannot create proxy with a non-object as handler", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            var proxy = new JsProxyObject(targetObj, handlerObj);

            var result = new JsDynamicObject { Prototype = realm.ObjectPrototype };
            result.Set("proxy", proxy);
            result.Set("revoke", JsFunction.CreateNative("revoke", (_, _) =>
            {
                proxy.Revoke();
                return JsValue.Undefined;
            }, 0));

            return result;
        }, 2);

        realm.InstallGlobal("Proxy", ctor);
    }
}

internal sealed class JsProxyObject : JsObject
{
    private JsObject? _target;
    private JsObject? _handler;

    internal JsProxyObject(JsObject target, JsObject handler)
    {
        _target = target;
        _handler = handler;
    }

    internal void Revoke()
    {
        _target = null;
        _handler = null;
    }

    private void EnsureNotRevoked()
    {
        if (_target is null || _handler is null)
        {
            throw new Errors.JsTypeError("Cannot perform operation on a revoked proxy", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }
    }

    public override JsValue Get(string name)
    {
        EnsureNotRevoked();
        var trap = _handler!.Get("get");
        if (trap is JsFunction fn)
        {
            return fn.Call(_handler, [_target!, new JsString(name), this]);
        }

        return _target!.Get(name);
    }

    public override void Set(string name, JsValue value)
    {
        EnsureNotRevoked();
        var trap = _handler!.Get("set");
        if (trap is JsFunction fn)
        {
            var result = fn.Call(_handler, [_target!, new JsString(name), value, this]);
            if (!result.ToBoolean())
            {
                throw new Errors.JsTypeError("'set' on proxy: trap returned falsish for property '" + name + "'", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            return;
        }

        _target!.Set(name, value);
    }

    public override bool HasProperty(string name)
    {
        EnsureNotRevoked();
        var trap = _handler!.Get("has");
        if (trap is JsFunction fn)
        {
            return fn.Call(_handler, [_target!, new JsString(name)]).ToBoolean();
        }

        return _target!.HasProperty(name);
    }

    public override bool Delete(string name)
    {
        EnsureNotRevoked();
        var trap = _handler!.Get("deleteProperty");
        if (trap is JsFunction fn)
        {
            return fn.Call(_handler, [_target!, new JsString(name)]).ToBoolean();
        }

        return _target!.Delete(name);
    }

    public override PropertyDescriptor? GetOwnProperty(string name)
    {
        EnsureNotRevoked();
        var trap = _handler!.Get("getOwnPropertyDescriptor");
        if (trap is JsFunction fn)
        {
            var result = fn.Call(_handler, [_target!, new JsString(name)]);
            if (result is JsUndefined)
            {
                return null;
            }

            if (result is JsObject descObj)
            {
                var value = descObj.Get("value");
                var writable = descObj.Get("writable").ToBoolean();
                var enumerable = descObj.Get("enumerable").ToBoolean();
                var configurable = descObj.Get("configurable").ToBoolean();
                return PropertyDescriptor.Data(value, writable, enumerable, configurable);
            }

            return null;
        }

        return _target!.GetOwnProperty(name);
    }

    public override IEnumerable<string> OwnPropertyKeys()
    {
        EnsureNotRevoked();
        var trap = _handler!.Get("ownKeys");
        if (trap is JsFunction fn)
        {
            var result = fn.Call(_handler, [_target!]);
            if (result is JsArray arr)
            {
                var keys = new List<string>(arr.DenseLength);
                for (var i = 0; i < arr.DenseLength; i++)
                {
                    keys.Add(arr.GetIndex(i).ToJsString());
                }

                return keys;
            }

            return [];
        }

        return _target!.OwnPropertyKeys();
    }
}
