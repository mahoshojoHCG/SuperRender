using System.Reflection;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Interop;

/// <summary>
/// Wraps a .NET <see cref="Type"/> as a JavaScript constructor function.
/// Only explicitly registered types can be accessed from JavaScript.
/// </summary>
public sealed class TypeProxy : JsFunction
{
    private readonly Type _type;
    private readonly HashSet<string> _allowedMembers;

    public TypeProxy(Type type, Realm realm)
    {
        _type = type;
        Name = type.Name;
        Prototype = realm.FunctionPrototype;
        IsConstructor = true;

        // Allow all public instance members by default
        _allowedMembers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            _allowedMembers.Add(member.Name);
        }

        // Set up the constructor call
        CallTarget = (_, args) => ConstructInstance(args);
        ConstructTarget = ConstructInstance;

        // Expose static members on the constructor
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (method.IsSpecialName) continue;
            var m = method;
            var fn = JsFunction.CreateNative(method.Name, (_, a) => InvokeStatic(m, a), method.GetParameters().Length);
            DefineOwnProperty(method.Name, PropertyDescriptor.Data(fn, writable: true, enumerable: false, configurable: true));
        }

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (!prop.CanRead) continue;
            var p = prop;
            var getter = JsFunction.CreateNative("get " + prop.Name, (_, _) => MarshalToJs(p.GetValue(null)), 0);
            JsFunction? setter = null;
            if (prop.CanWrite)
            {
                var ps = prop;
                setter = JsFunction.CreateNative("set " + prop.Name, (_, a) =>
                {
                    ps.SetValue(null, MarshalFromJs(a.Length > 0 ? a[0] : JsValue.Undefined, ps.PropertyType));
                    return JsValue.Undefined;
                }, 1);
            }
            DefineOwnProperty(prop.Name, PropertyDescriptor.Accessor(getter, setter, enumerable: false, configurable: true));
        }

        // Set up prototype object with instance methods
        PrototypeObject = new JsObject { Prototype = realm.ObjectPrototype };

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (method.IsSpecialName) continue;
            var m = method;
            var fn = JsFunction.CreateNative(method.Name, (thisVal, a) =>
            {
                if (thisVal is ObjectProxy proxy)
                    return InvokeInstance(proxy.Target, m, a);
                return JsValue.Undefined;
            }, method.GetParameters().Length);
            PrototypeObject.DefineOwnProperty(method.Name, PropertyDescriptor.Data(fn, writable: true, enumerable: false, configurable: true));
        }

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!prop.CanRead) continue;
            var p = prop;
            var getter = JsFunction.CreateNative("get " + prop.Name, (thisVal, _) =>
            {
                if (thisVal is ObjectProxy proxy)
                    return MarshalToJs(p.GetValue(proxy.Target));
                return JsValue.Undefined;
            }, 0);
            JsFunction? setter = null;
            if (prop.CanWrite)
            {
                var ps = prop;
                setter = JsFunction.CreateNative("set " + prop.Name, (thisVal, a) =>
                {
                    if (thisVal is ObjectProxy proxy)
                        ps.SetValue(proxy.Target, MarshalFromJs(a.Length > 0 ? a[0] : JsValue.Undefined, ps.PropertyType));
                    return JsValue.Undefined;
                }, 1);
            }
            PrototypeObject.DefineOwnProperty(prop.Name, PropertyDescriptor.Accessor(getter, setter, enumerable: false, configurable: true));
        }
    }

    private JsValue ConstructInstance(JsValue[] args)
    {
        var ctors = _type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        foreach (var ctor in ctors.OrderByDescending(c => c.GetParameters().Length))
        {
            var paramInfos = ctor.GetParameters();
            if (args.Length >= paramInfos.Length)
            {
                var clrArgs = new object?[paramInfos.Length];
                for (int i = 0; i < paramInfos.Length; i++)
                {
                    clrArgs[i] = MarshalFromJs(i < args.Length ? args[i] : JsValue.Undefined, paramInfos[i].ParameterType);
                }
                var instance = ctor.Invoke(clrArgs);
                return new ObjectProxy(instance, this);
            }
        }

        if (ctors.Length > 0)
        {
            var paramInfos = ctors[0].GetParameters();
            var clrArgs = new object?[paramInfos.Length];
            for (int i = 0; i < paramInfos.Length; i++)
            {
                clrArgs[i] = MarshalFromJs(i < args.Length ? args[i] : JsValue.Undefined, paramInfos[i].ParameterType);
            }
            var instance = ctors[0].Invoke(clrArgs);
            return new ObjectProxy(instance, this);
        }

        throw new Errors.JsTypeError($"Cannot construct {_type.Name}");
    }

    private static JsValue InvokeStatic(MethodInfo method, JsValue[] args)
    {
        var paramInfos = method.GetParameters();
        var clrArgs = new object?[paramInfos.Length];
        for (int i = 0; i < paramInfos.Length; i++)
        {
            clrArgs[i] = MarshalFromJs(i < args.Length ? args[i] : JsValue.Undefined, paramInfos[i].ParameterType);
        }
        var result = method.Invoke(null, clrArgs);
        return MarshalToJs(result);
    }

    private static JsValue InvokeInstance(object target, MethodInfo method, JsValue[] args)
    {
        var paramInfos = method.GetParameters();
        var clrArgs = new object?[paramInfos.Length];
        for (int i = 0; i < paramInfos.Length; i++)
        {
            clrArgs[i] = MarshalFromJs(i < args.Length ? args[i] : JsValue.Undefined, paramInfos[i].ParameterType);
        }
        var result = method.Invoke(target, clrArgs);
        return MarshalToJs(result);
    }

    internal static JsValue MarshalToJs(object? value)
    {
        return value switch
        {
            null => JsValue.Null,
            JsValue js => js,
            bool b => b ? JsValue.True : JsValue.False,
            byte n => JsNumber.Create(n),
            sbyte n => JsNumber.Create(n),
            short n => JsNumber.Create(n),
            ushort n => JsNumber.Create(n),
            int n => JsNumber.Create(n),
            uint n => JsNumber.Create(n),
            long n => JsNumber.Create(n),
            ulong n => JsNumber.Create(n),
            float n => JsNumber.Create(n),
            double n => JsNumber.Create(n),
            decimal n => JsNumber.Create((double)n),
            string s => new JsString(s),
            char c => new JsString(c.ToString()),
            _ => new ObjectProxy(value, null)
        };
    }

    internal static object? MarshalFromJs(JsValue value, Type targetType)
    {
        if (targetType == typeof(JsValue)) return value;
        if (value is JsNull or JsUndefined)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
        if (targetType == typeof(string)) return value.ToJsString();
        if (targetType == typeof(bool)) return value.ToBoolean();
        if (targetType == typeof(int)) return (int)value.ToNumber();
        if (targetType == typeof(long)) return (long)value.ToNumber();
        if (targetType == typeof(float)) return (float)value.ToNumber();
        if (targetType == typeof(double)) return value.ToNumber();
        if (targetType == typeof(decimal)) return (decimal)value.ToNumber();
        if (targetType == typeof(object))
        {
            return value switch
            {
                JsNumber n => (object)n.Value,
                JsString s => s.Value,
                JsBoolean b => b.Value,
                _ => value
            };
        }
        if (value is ObjectProxy proxy && targetType.IsInstanceOfType(proxy.Target))
        {
            return proxy.Target;
        }
        return Convert.ChangeType(value.ToNumber(), targetType, System.Globalization.CultureInfo.InvariantCulture);
    }
}
