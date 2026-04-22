namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

[JsGlobalInstall("Symbol")]
public sealed partial class SymbolConstructor
{
    private static readonly Dictionary<string, JsSymbol> GlobalRegistry = new(StringComparer.Ordinal);

    private static void __Install(Realm realm)
    {
        var proto = realm.SymbolPrototype;

        // Symbol is a factory function, NOT a constructor (no new Symbol())
        var ctor = JsFunction.CreateNative("Symbol", (_, args) =>
        {
            var description = args.Length > 0 && args[0] is not JsUndefined
                ? args[0].ToJsString()
                : null;
            return new JsSymbol(description);
        }, 0);
        ctor.Prototype = realm.FunctionPrototype;
        ctor.PrototypeObject = proto;

        // Well-known symbols as static properties
        BuiltinHelper.DefineProperty(ctor, "iterator", JsSymbol.Iterator);
        BuiltinHelper.DefineProperty(ctor, "asyncIterator", JsSymbol.AsyncIterator);
        BuiltinHelper.DefineProperty(ctor, "toPrimitive", JsSymbol.ToPrimitiveSymbol);
        BuiltinHelper.DefineProperty(ctor, "hasInstance", JsSymbol.HasInstance);
        BuiltinHelper.DefineProperty(ctor, "toStringTag", JsSymbol.ToStringTag);
        BuiltinHelper.DefineProperty(ctor, "species", JsSymbol.Species);

        // Symbol.for
        BuiltinHelper.DefineMethod(ctor, "for", (_, args) =>
        {
            var key = BuiltinHelper.Arg(args, 0).ToJsString();
            if (GlobalRegistry.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var sym = new JsSymbol(key);
            GlobalRegistry[key] = sym;
            return sym;
        }, 1);

        // Symbol.keyFor
        BuiltinHelper.DefineMethod(ctor, "keyFor", (_, args) =>
        {
            var sym = BuiltinHelper.Arg(args, 0);
            if (sym is not JsSymbol symbol)
            {
                throw new Errors.JsTypeError("Symbol.keyFor requires a symbol argument", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            foreach (var kvp in GlobalRegistry)
            {
                if (kvp.Value.StrictEquals(symbol))
                {
                    return new JsString(kvp.Key);
                }
            }

            return JsValue.Undefined;
        }, 1);

        // Prototype methods
        BuiltinHelper.DefineProperty(proto, "constructor", ctor);

        BuiltinHelper.DefineMethod(proto, "toString", (thisArg, _) =>
        {
            var sym = GetSymbolValue(thisArg);
            return new JsString(sym.ToString());
        }, 0);

        BuiltinHelper.DefineMethod(proto, "valueOf", (thisArg, _) =>
        {
            return GetSymbolValue(thisArg);
        }, 0);

        BuiltinHelper.DefineGetter(proto, "description", (thisArg, _) =>
        {
            var sym = GetSymbolValue(thisArg);
            return sym.Description is not null ? new JsString(sym.Description) : JsValue.Undefined;
        });

        // Symbol.prototype[Symbol.toPrimitive]
        proto.DefineSymbolProperty(JsSymbol.ToPrimitiveSymbol,
            PropertyDescriptor.Data(__JsFn_SymbolToPrimitive(), writable: false, enumerable: false, configurable: true));

        // Symbol.prototype[Symbol.toStringTag]
        proto.DefineSymbolProperty(JsSymbol.ToStringTag,
            PropertyDescriptor.Data(new JsString("Symbol"), writable: false, enumerable: false, configurable: true));

        realm.InstallGlobal("Symbol", ctor);
    }

    [JsMethod("[Symbol.toPrimitive]")]
    internal static JsValue SymbolToPrimitive(JsValue thisArg, JsValue[] args) => GetSymbolValue(thisArg);

    private static JsSymbol GetSymbolValue(JsValue thisArg)
    {
        if (thisArg is JsSymbol sym)
        {
            return sym;
        }

        if (thisArg is JsObject obj)
        {
            var data = obj.GetOwnProperty("[[SymbolData]]");
            if (data?.Value is JsSymbol symData)
            {
                return symData;
            }
        }

        throw new Errors.JsTypeError("Symbol.prototype.valueOf requires that 'this' be a Symbol", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
    }
}
