namespace SuperRender.EcmaScript.Runtime.Interop;

using System.Collections.Concurrent;

/// <summary>
/// Registry of source-generated proxy factories, keyed by the <see cref="IJsType"/>-derived interface.
/// Generated proxy classes register themselves via <c>[ModuleInitializer]</c> at assembly load.
/// </summary>
public static class JsTypeInterfaceProxyRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<JsObjectBase, object>> Factories = new();

    /// <summary>Registers a factory for <paramref name="interfaceType"/>. First writer wins.</summary>
    public static void Register(Type interfaceType, Func<JsObjectBase, object> factory)
    {
        ArgumentNullException.ThrowIfNull(interfaceType);
        ArgumentNullException.ThrowIfNull(factory);
        Factories.TryAdd(interfaceType, factory);
    }

    public static bool TryCreate(Type interfaceType, JsObjectBase target, out object? proxy)
    {
        if (Factories.TryGetValue(interfaceType, out var factory))
        {
            proxy = factory(target);
            return true;
        }

        proxy = null;
        return false;
    }

    internal static bool IsRegistered(Type interfaceType) => Factories.ContainsKey(interfaceType);
}
