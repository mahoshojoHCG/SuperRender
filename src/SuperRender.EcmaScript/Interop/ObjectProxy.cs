using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Interop;

/// <summary>
/// Wraps a .NET object instance as a JavaScript object.
/// Only members of the registered type are accessible.
/// </summary>
public sealed class ObjectProxy : JsObject
{
    public object Target { get; }
    private readonly TypeProxy? _typeProxy;

    public ObjectProxy(object target, TypeProxy? typeProxy)
    {
        Target = target;
        _typeProxy = typeProxy;
        if (typeProxy?.PrototypeObject is not null)
        {
            Prototype = typeProxy.PrototypeObject;
        }
    }

    public override JsValue Get(string name)
    {
        // Try own properties first (set from JS side)
        var own = GetOwnProperty(name);
        if (own is not null)
        {
            if (own.IsAccessorDescriptor)
            {
                return own.Get is JsFunction getter ? getter.Call(this, []) : Undefined;
            }
            return own.Value ?? Undefined;
        }

        // Delegate to prototype chain (TypeProxy's prototype has the .NET methods/properties)
        return base.Get(name);
    }

    public override string ToJsString()
    {
        return Target.ToString() ?? "[object Object]";
    }

    public override double ToNumber()
    {
        if (Target is IConvertible c)
        {
            try
            {
                return c.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                return double.NaN;
            }
            catch (InvalidCastException)
            {
                return double.NaN;
            }
        }
        return double.NaN;
    }
}
