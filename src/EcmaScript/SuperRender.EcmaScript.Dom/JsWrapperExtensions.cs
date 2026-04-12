using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// Extension methods that reduce boilerplate when defining DOM wrapper properties.
/// Provides shorthand for the common DefineOwnProperty + PropertyDescriptor patterns.
/// </summary>
internal static class JsWrapperExtensions
{
    /// <summary>
    /// Define a native method property: DefineOwnProperty(name, Data(CreateNative(name, handler, argCount))).
    /// </summary>
    internal static void DefineMethod(this JsObject obj, string name, int argCount, Func<JsValue[], JsValue> handler)
    {
        obj.DefineOwnProperty(name, PropertyDescriptor.Data(
            JsFunction.CreateNative(name, (_, args) => handler(args), argCount)));
    }

    /// <summary>
    /// Define a read-only accessor property with a getter.
    /// </summary>
    internal static void DefineGetter(this JsObject obj, string name, Func<JsValue> getter)
    {
        obj.DefineOwnProperty(name, PropertyDescriptor.Accessor(
            JsFunction.CreateNative($"get {name}", (_, _) => getter(), 0),
            null, enumerable: true, configurable: true));
    }

    /// <summary>
    /// Define a read-write accessor property with getter and setter.
    /// </summary>
    internal static void DefineGetterSetter(this JsObject obj, string name, Func<JsValue> getter, Action<JsValue> setter)
    {
        obj.DefineOwnProperty(name, PropertyDescriptor.Accessor(
            JsFunction.CreateNative($"get {name}", (_, _) => getter(), 0),
            JsFunction.CreateNative($"set {name}", (_, args) =>
            {
                setter(args.Length > 0 ? args[0] : JsValue.Undefined);
                return JsValue.Undefined;
            }, 1),
            enumerable: true, configurable: true));
    }
}
