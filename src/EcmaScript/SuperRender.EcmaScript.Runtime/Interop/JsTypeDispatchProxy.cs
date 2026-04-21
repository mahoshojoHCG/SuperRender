namespace SuperRender.EcmaScript.Runtime.Interop;

using System.Reflection;
using SuperRender.EcmaScript.Runtime.Errors;

/// <summary>
/// Runtime fallback proxy used when no source-generated proxy is registered for an
/// <see cref="IJsType"/>-derived interface. Created via
/// <see cref="DispatchProxy.Create{T, TProxy}"/>; access the backing JS object through
/// <see cref="IJsTypeProxy.Target"/> to unwrap nested arguments.
/// </summary>
public class JsTypeDispatchProxy : DispatchProxy, IJsTypeProxy
{
    private JsObject _target = null!;

    JsObject IJsTypeProxy.Target => _target;

    internal void Init(JsObject target) => _target = target;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
        {
            throw new JsTypeError("DispatchProxy invoked without a target method", 0, 0);
        }

        var memberName = targetMethod.Name;
        args ??= [];

        // Property accessors — compiler synthesises MethodInfo with IsSpecialName and a get_/set_ prefix.
        if (targetMethod.IsSpecialName)
        {
            if (memberName.StartsWith("get_", StringComparison.Ordinal))
            {
                var propName = memberName[4..];
                var jsName = ResolvePropertyJsName(targetMethod.DeclaringType!, propName);
                var raw = _target.Get(jsName);
                return ConvertFromJs(raw, targetMethod.ReturnType);
            }

            if (memberName.StartsWith("set_", StringComparison.Ordinal))
            {
                var propName = memberName[4..];
                var jsName = ResolvePropertyJsName(targetMethod.DeclaringType!, propName);
                var paramType = targetMethod.GetParameters()[0].ParameterType;
                _target.Set(jsName, ConvertToJs(args[0], paramType));
                return null;
            }
        }

        // Method call.
        var methodJsName = ResolveMethodJsName(targetMethod);
        var fn = InteropConversions.RequireFunction(_target, methodJsName);
        var parameters = targetMethod.GetParameters();
        var converted = new JsValue[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            converted[i] = ConvertToJs(args[i], parameters[i].ParameterType);
        }

        var result = fn.Call(_target, converted);

        if (targetMethod.ReturnType == typeof(void))
        {
            return null;
        }

        return ConvertFromJs(result, targetMethod.ReturnType);
    }

    private static string ResolvePropertyJsName(Type declaringType, string propName)
    {
        var prop = declaringType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var attr = prop?.GetCustomAttribute<JsNameAttribute>();
        return attr?.Name ?? CamelCase(propName);
    }

    private static string ResolveMethodJsName(MethodInfo method)
    {
        var attr = method.GetCustomAttribute<JsNameAttribute>();
        return attr?.Name ?? CamelCase(method.Name);
    }

    internal static string CamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0]))
        {
            return name;
        }

        var chars = name.ToCharArray();
        // Lowercase the leading run of uppercase letters, except the last upper before a lower.
        for (var i = 0; i < chars.Length; i++)
        {
            if (i > 0 && i < chars.Length - 1 && !char.IsUpper(chars[i + 1]))
            {
                break;
            }

            if (!char.IsUpper(chars[i]))
            {
                break;
            }

            chars[i] = char.ToLowerInvariant(chars[i]);
        }

        return new string(chars);
    }

    private static JsValue ConvertToJs(object? value, Type sourceType)
    {
        if (value is null)
        {
            return JsValue.Null;
        }

        if (value is JsValue jv)
        {
            return jv;
        }

        if (value is IJsType iface)
        {
            return InteropConversions.UnwrapIJsType(iface);
        }

        return sourceType switch
        {
            _ when sourceType == typeof(string) => InteropConversions.ToJs((string)value),
            _ when sourceType == typeof(bool) => InteropConversions.ToJs((bool)value),
            _ when sourceType == typeof(double) => InteropConversions.ToJs((double)value),
            _ when sourceType == typeof(float) => InteropConversions.ToJs((float)value),
            _ when sourceType == typeof(decimal) => InteropConversions.ToJs((decimal)value),
            _ when sourceType == typeof(int) => InteropConversions.ToJs((int)value),
            _ when sourceType == typeof(uint) => InteropConversions.ToJs((uint)value),
            _ when sourceType == typeof(long) => InteropConversions.ToJs((long)value),
            _ when sourceType == typeof(ulong) => InteropConversions.ToJs((ulong)value),
            _ when sourceType == typeof(short) => InteropConversions.ToJs((short)value),
            _ when sourceType == typeof(ushort) => InteropConversions.ToJs((ushort)value),
            _ when sourceType == typeof(byte) => InteropConversions.ToJs((byte)value),
            _ when sourceType == typeof(sbyte) => InteropConversions.ToJs((sbyte)value),
            _ => throw new JsTypeError($"Unsupported parameter type {sourceType.FullName} for IJsType proxy", 0, 0),
        };
    }

    private static object? ConvertFromJs(JsValue value, Type targetType)
    {
        if (targetType == typeof(void))
        {
            return null;
        }

        if (typeof(JsValue).IsAssignableFrom(targetType))
        {
            return value;
        }

        if (typeof(IJsType).IsAssignableFrom(targetType))
        {
            return JsValueExtension.AsInterfaceOf(value, targetType);
        }

        return targetType switch
        {
            _ when targetType == typeof(string) => InteropConversions.FromJsString(value),
            _ when targetType == typeof(bool) => InteropConversions.FromJsBool(value),
            _ when targetType == typeof(double) => InteropConversions.FromJsDouble(value),
            _ when targetType == typeof(float) => InteropConversions.FromJsFloat(value),
            _ when targetType == typeof(decimal) => InteropConversions.FromJsDecimal(value),
            _ when targetType == typeof(int) => InteropConversions.FromJsInt(value),
            _ when targetType == typeof(uint) => InteropConversions.FromJsUInt(value),
            _ when targetType == typeof(long) => InteropConversions.FromJsLong(value),
            _ when targetType == typeof(ulong) => InteropConversions.FromJsULong(value),
            _ when targetType == typeof(short) => InteropConversions.FromJsShort(value),
            _ when targetType == typeof(ushort) => InteropConversions.FromJsUShort(value),
            _ when targetType == typeof(byte) => InteropConversions.FromJsByte(value),
            _ when targetType == typeof(sbyte) => InteropConversions.FromJsSByte(value),
            _ => throw new JsTypeError($"Unsupported return type {targetType.FullName} for IJsType proxy", 0, 0),
        };
    }
}
