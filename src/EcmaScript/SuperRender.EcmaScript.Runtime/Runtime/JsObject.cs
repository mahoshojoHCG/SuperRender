namespace SuperRender.EcmaScript.Runtime;

public class JsObject : JsValue
{
    private readonly Dictionary<string, PropertyDescriptor> _properties = new(StringComparer.Ordinal);
    private readonly List<string> _propertyOrder = [];
    private readonly Dictionary<JsSymbol, PropertyDescriptor> _symbolProperties = [];

    public JsObject? Prototype { get; set; }
    public bool Extensible { get; set; } = true;

    public override string TypeOf => "object";
    public override bool ToBoolean() => true;

    public override double ToNumber()
    {
        var prim = ToPrimitive("number");
        return prim.ToNumber();
    }

    public override string ToJsString()
    {
        var prim = ToPrimitive("string");
        return prim.ToJsString();
    }

    public override JsValue ToPrimitive(string? preferredType = null)
    {
        // Check for Symbol.toPrimitive method
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

        // OrdinaryToPrimitive
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
        var desc = GetProperty(name);
        if (desc is null)
        {
            return Undefined;
        }

        if (desc.IsAccessorDescriptor)
        {
            if (desc.Get is JsFunction getter)
            {
                return getter.Call(this, []);
            }

            return Undefined;
        }

        return desc.Value ?? Undefined;
    }

    public virtual void Set(string name, JsValue value)
    {
        var ownDesc = GetOwnProperty(name);

        if (ownDesc is not null)
        {
            if (ownDesc.IsAccessorDescriptor)
            {
                if (ownDesc.Set is JsFunction setter)
                {
                    setter.Call(this, [value]);
                    return;
                }

                throw new Errors.JsTypeError($"Cannot set property '{name}' which has only a getter", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            if (ownDesc.Writable == false)
            {
                throw new Errors.JsTypeError($"Cannot assign to read only property '{name}'", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            ownDesc.Value = value;
            return;
        }

        // Check prototype chain for accessor
        var inherited = GetPropertyFromPrototype(name);
        if (inherited is not null && inherited.IsAccessorDescriptor)
        {
            if (inherited.Set is JsFunction inheritedSetter)
            {
                inheritedSetter.Call(this, [value]);
                return;
            }

            throw new Errors.JsTypeError($"Cannot set property '{name}' which has only a getter", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        if (!Extensible)
        {
            throw new Errors.JsTypeError($"Cannot add property '{name}', object is not extensible", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        DefineOwnProperty(name, PropertyDescriptor.Data(value));
    }

    public virtual bool HasProperty(string name)
    {
        if (_properties.ContainsKey(name))
        {
            return true;
        }

        return Prototype?.HasProperty(name) ?? false;
    }

    public virtual bool Delete(string name)
    {
        if (!_properties.TryGetValue(name, out var desc))
        {
            return true;
        }

        if (desc.Configurable == false)
        {
            throw new Errors.JsTypeError($"Cannot delete property '{name}'", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        _properties.Remove(name);
        _propertyOrder.Remove(name);
        return true;
    }

    public void DefineOwnProperty(string name, PropertyDescriptor descriptor)
    {
        if (!_properties.ContainsKey(name))
        {
            if (!Extensible)
            {
                throw new Errors.JsTypeError($"Cannot define property '{name}', object is not extensible", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            _propertyOrder.Add(name);
        }

        _properties[name] = descriptor;
    }

    public PropertyDescriptor? GetOwnProperty(string name) =>
        _properties.GetValueOrDefault(name);

    public IEnumerable<string> OwnPropertyKeys() => _propertyOrder;

    // Symbol property support
    public void DefineSymbolProperty(JsSymbol symbol, PropertyDescriptor descriptor) =>
        _symbolProperties[symbol] = descriptor;

    public bool TryGetSymbolProperty(JsSymbol symbol, out JsValue value)
    {
        if (_symbolProperties.TryGetValue(symbol, out var desc))
        {
            if (desc.IsAccessorDescriptor)
            {
                value = desc.Get is JsFunction getter ? getter.Call(this, []) : Undefined;
            }
            else
            {
                value = desc.Value ?? Undefined;
            }

            return true;
        }

        if (Prototype is not null)
        {
            return Prototype.TryGetSymbolProperty(symbol, out value);
        }

        value = Undefined;
        return false;
    }

    private PropertyDescriptor? GetProperty(string name) =>
        GetOwnProperty(name) ?? Prototype?.GetProperty(name);

    private PropertyDescriptor? GetPropertyFromPrototype(string name) =>
        Prototype?.GetProperty(name);
}
