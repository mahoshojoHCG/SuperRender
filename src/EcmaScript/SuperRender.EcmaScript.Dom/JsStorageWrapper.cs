using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// JS wrapper for the Web Storage API (localStorage / sessionStorage).
/// Wraps a WebStorage-like interface using delegates, so the EcmaScript.Dom project
/// remains dependency-free (no direct reference to Browser's WebStorage class).
/// </summary>
internal sealed class JsStorageWrapper : JsObject
{
    public JsStorageWrapper(
        Func<string, string?> getItem,
        Action<string, string> setItem,
        Action<string> removeItem,
        Action clear,
        Func<int, string?> key,
        Func<int> getLength,
        Realm realm)
    {
        Prototype = realm.ObjectPrototype;

        DefineOwnProperty("getItem", PropertyDescriptor.Data(
            JsFunction.CreateNative("getItem", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var result = getItem(args[0].ToJsString());
                    return result is not null ? new JsString(result) : Null;
                }
                return Null;
            }, 1)));

        DefineOwnProperty("setItem", PropertyDescriptor.Data(
            JsFunction.CreateNative("setItem", (_, args) =>
            {
                if (args.Length >= 2)
                    setItem(args[0].ToJsString(), args[1].ToJsString());
                return Undefined;
            }, 2)));

        DefineOwnProperty("removeItem", PropertyDescriptor.Data(
            JsFunction.CreateNative("removeItem", (_, args) =>
            {
                if (args.Length > 0)
                    removeItem(args[0].ToJsString());
                return Undefined;
            }, 1)));

        DefineOwnProperty("clear", PropertyDescriptor.Data(
            JsFunction.CreateNative("clear", (_, _) =>
            {
                clear();
                return Undefined;
            }, 0)));

        DefineOwnProperty("key", PropertyDescriptor.Data(
            JsFunction.CreateNative("key", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var idx = (int)args[0].ToNumber();
                    var result = key(idx);
                    return result is not null ? new JsString(result) : Null;
                }
                return Null;
            }, 1)));

        DefineOwnProperty("length", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get length", (_, _) => JsNumber.Create(getLength()), 0),
            null, enumerable: true, configurable: true));
    }
}
