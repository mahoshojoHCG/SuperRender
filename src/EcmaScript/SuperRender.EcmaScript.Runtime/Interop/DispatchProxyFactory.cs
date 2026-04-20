namespace SuperRender.EcmaScript.Runtime.Interop;

using System.Reflection;
using SuperRender.EcmaScript.Runtime.Errors;

internal static class DispatchProxyFactory
{
    public static T Create<T>(JsObjectBase target)
        where T : class, IJsType
    {
        ValidateInterface(typeof(T));
        var proxy = DispatchProxy.Create<T, JsTypeDispatchProxy>();
        ((JsTypeDispatchProxy)(object)proxy).Init(target);
        return proxy;
    }

    public static object Create(Type interfaceType, JsObjectBase target)
    {
        ValidateInterface(interfaceType);
        var method = typeof(DispatchProxy).GetMethod(
            nameof(DispatchProxy.Create),
            BindingFlags.Public | BindingFlags.Static,
            Type.EmptyTypes)!;
        var constructed = method.MakeGenericMethod(interfaceType, typeof(JsTypeDispatchProxy));
        var proxy = constructed.Invoke(null, null)!;
        ((JsTypeDispatchProxy)proxy).Init(target);
        return proxy;
    }

    private static void ValidateInterface(Type t)
    {
        if (!t.IsInterface)
        {
            throw new JsTypeError($"AsInterface<T> requires T to be an interface; got {t.FullName}", 0, 0);
        }

        foreach (var member in t.GetMembers())
        {
            switch (member)
            {
                case EventInfo:
                    throw new JsTypeError($"Interface {t.Name} has an event '{member.Name}' which is not supported by IJsType", 0, 0);
                case PropertyInfo pi when pi.GetIndexParameters().Length > 0:
                    throw new JsTypeError($"Interface {t.Name} has an indexer which is not supported by IJsType", 0, 0);
                case MethodInfo mi when !mi.IsSpecialName && mi.IsGenericMethodDefinition:
                    throw new JsTypeError($"Interface {t.Name} has a generic method '{mi.Name}' which is not supported by IJsType", 0, 0);
                case MethodInfo mi when !mi.IsSpecialName:
                    foreach (var p in mi.GetParameters())
                    {
                        if (p.IsOut || p.ParameterType.IsByRef)
                        {
                            throw new JsTypeError($"Interface {t.Name}.{mi.Name} has a ref/out parameter which is not supported by IJsType", 0, 0);
                        }
                    }

                    break;
                default:
                    break;
            }
        }
    }
}
