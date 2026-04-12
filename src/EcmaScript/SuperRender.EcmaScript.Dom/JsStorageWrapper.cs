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

        this.DefineMethod("getItem", 1, args =>
        {
            if (args.Length > 0)
            {
                var result = getItem(args[0].ToJsString());
                return result is not null ? new JsString(result) : Null;
            }
            return Null;
        });

        this.DefineMethod("setItem", 2, args =>
        {
            if (args.Length >= 2)
                setItem(args[0].ToJsString(), args[1].ToJsString());
            return Undefined;
        });

        this.DefineMethod("removeItem", 1, args =>
        {
            if (args.Length > 0) removeItem(args[0].ToJsString());
            return Undefined;
        });

        this.DefineMethod("clear", 0, _ =>
        {
            clear();
            return Undefined;
        });

        this.DefineMethod("key", 1, args =>
        {
            if (args.Length > 0)
            {
                var idx = (int)args[0].ToNumber();
                var result = key(idx);
                return result is not null ? new JsString(result) : Null;
            }
            return Null;
        });

        this.DefineGetter("length", () => JsNumber.Create(getLength()));
    }
}
