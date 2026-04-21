namespace SuperRender.EcmaScript.Runtime;

public abstract class JsObject : JsValue
{
    public JsObject? Prototype { get; set; }
    public bool Extensible { get; set; } = true;

    public override string TypeOf => "object";
    public override bool ToBoolean() => true;

    public override double ToNumber() => ToPrimitive("number").ToNumber();
    public override string ToJsString() => ToPrimitive("string").ToJsString();

    public override JsValue ToPrimitive(string? preferredType = null)
    {
        if (TryGetSymbolProperty(JsSymbol.ToPrimitiveSymbol, out var toPrimFn) && toPrimFn is JsFunction fn)
        {
            var hint = new JsString(preferredType ?? "default");
            var result = fn.Call(this, [hint]);
            if (result is not JsObject)
            {
                return result;
            }

            throw new Errors.JsTypeError("Cannot convert object to primitive value", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        string[] methodNames = preferredType == "string"
            ? ["toString", "valueOf"]
            : ["valueOf", "toString"];

        foreach (var methodName in methodNames)
        {
            var method = Get(methodName);
            if (method is JsFunction callable)
            {
                var result = callable.Call(this, []);
                if (result is not JsObject)
                {
                    return result;
                }
            }
        }

        throw new Errors.JsTypeError("Cannot convert object to primitive value", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }

    public virtual JsValue Get(string name)
    {
        for (var cur = this; cur is not null; cur = cur.Prototype)
        {
            var desc = cur.GetOwnPropertyDescriptor(name);
            if (desc is null)
            {
                continue;
            }

            if (desc.IsAccessorDescriptor)
            {
                return desc.Get is JsFunction getter ? getter.Call(this, []) : Undefined;
            }

            return desc.Value ?? Undefined;
        }

        return Undefined;
    }

    public virtual void Set(string name, JsValue value)
    {
        for (var cur = this; cur is not null; cur = cur.Prototype)
        {
            var desc = cur.GetOwnPropertyDescriptor(name);
            if (desc is null)
            {
                continue;
            }

            if (ReferenceEquals(cur, this))
            {
                if (desc.IsAccessorDescriptor)
                {
                    if (desc.Set is JsFunction setter)
                    {
                        setter.Call(this, [value]);
                        return;
                    }

                    throw new Errors.JsTypeError($"Cannot set property '{name}' which has only a getter", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                }

                if (desc.Writable == false)
                {
                    throw new Errors.JsTypeError($"Cannot assign to read only property '{name}'", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                }

                desc.Value = value;
                return;
            }

            if (desc.IsAccessorDescriptor)
            {
                if (desc.Set is JsFunction inheritedSetter)
                {
                    inheritedSetter.Call(this, [value]);
                    return;
                }

                throw new Errors.JsTypeError($"Cannot set property '{name}' which has only a getter", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            break;
        }

        if (!Extensible)
        {
            throw new Errors.JsTypeError($"Cannot add property '{name}', object is not extensible", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        DefineOwnPropertyCore(name, PropertyDescriptor.Data(value));
    }

    public virtual bool HasProperty(string name)
    {
        for (var cur = this; cur is not null; cur = cur.Prototype)
        {
            if (cur.GetOwnPropertyDescriptor(name) is not null)
            {
                return true;
            }
        }

        return false;
    }

    public virtual bool Delete(string name) => true;

    public virtual bool TryGetSymbolProperty(JsSymbol symbol, out JsValue value)
    {
        if (Prototype is not null)
        {
            return Prototype.TryGetSymbolProperty(symbol, out value);
        }

        value = Undefined;
        return false;
    }

    public virtual PropertyDescriptor? GetOwnProperty(string name) => null;

    public virtual IEnumerable<string> OwnPropertyKeys() => [];

    protected virtual PropertyDescriptor? GetOwnPropertyDescriptor(string name) => GetOwnProperty(name);

    protected virtual void DefineOwnPropertyCore(string name, PropertyDescriptor descriptor) =>
        throw new Errors.JsTypeError($"Cannot add property '{name}' to this object", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
}
