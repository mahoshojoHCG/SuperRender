namespace SuperRender.EcmaScript.Runtime;

public class JsDynamicObject : JsObjectBase
{
    private readonly Dictionary<string, PropertyDescriptor> _properties = new(StringComparer.Ordinal);
    private readonly List<string> _propertyOrder = [];
    private readonly Dictionary<JsSymbol, PropertyDescriptor> _symbolProperties = [];

    public override bool Delete(string name)
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

    public void DefineSymbolProperty(JsSymbol symbol, PropertyDescriptor descriptor) =>
        _symbolProperties[symbol] = descriptor;

    public override bool TryGetSymbolProperty(JsSymbol symbol, out JsValue value)
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

        return base.TryGetSymbolProperty(symbol, out value);
    }

    protected override PropertyDescriptor? GetOwnPropertyDescriptor(string name) =>
        _properties.GetValueOrDefault(name);

    protected override void DefineOwnPropertyCore(string name, PropertyDescriptor descriptor) =>
        DefineOwnProperty(name, descriptor);
}
