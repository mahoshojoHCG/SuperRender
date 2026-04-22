namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

[JsGlobalInstall("ShadowRealm")]
public sealed partial class ShadowRealmConstructor
{
    private static void __Install(Realm realm)
    {
        var proto = new JsDynamicObject { Prototype = realm.ObjectPrototype };

        var ctor = new JsFunction
        {
            Name = "ShadowRealm",
            Length = 0,
            IsConstructor = true,
            Prototype = realm.FunctionPrototype,
            PrototypeObject = proto,
            ConstructTarget = _ =>
            {
                // Create a fresh isolated realm
                var shadowRealm = new Realm();

                // Copy the eval factory from parent realm
                shadowRealm.EvalFactory = realm.EvalFactory;

                shadowRealm
                    .Install<ObjectConstructor>()
                    .Install<FunctionConstructor>()
                    .Install<ArrayConstructor>()
                    .Install<StringConstructor>()
                    .Install<NumberConstructor>()
                    .Install<JsBooleanObject>()
                    .Install<SymbolConstructor>()
                    .Install<MathObject>()
                    .Install<JsonObject>()
                    .Install<DateConstructor>()
                    .Install<RegExpConstructor>()
                    .Install<ErrorConstructor>()
                    .Install<MapConstructor>()
                    .Install<SetConstructor>()
                    .Install<PromiseConstructor>()
                    .Install<ConsoleObject>();

                var srObj = new JsDynamicObject { Prototype = proto };
                srObj.Set("[[ShadowRealm]]", new JsShadowRealmData(shadowRealm));

                return srObj;
            },
            CallTarget = (_, _) =>
            {
                throw new Errors.JsTypeError("Constructor ShadowRealm requires 'new'", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }
        };

        BuiltinHelper.DefineMethod(proto, "evaluate", (self, args) =>
        {
            if (self is not JsDynamicObject selfObj)
                throw new Errors.JsTypeError("ShadowRealm.prototype.evaluate called on non-ShadowRealm", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);

            var srData = selfObj.Get("[[ShadowRealm]]");
            if (srData is not JsShadowRealmData data)
                throw new Errors.JsTypeError("ShadowRealm.prototype.evaluate called on non-ShadowRealm", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);

            var code = BuiltinHelper.Arg(args, 0);
            if (code is not JsString codeStr)
                throw new Errors.JsTypeError("ShadowRealm.prototype.evaluate requires a string argument", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);

            var evalFn = data.ShadowRealm.EvalFactory;
            if (evalFn is null)
                throw new Errors.JsTypeError("Eval is not available in this ShadowRealm", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);

            var result = evalFn(codeStr.Value, data.ShadowRealm);

            // Only primitive values can cross the boundary
            if (result is JsDynamicObject and not JsFunction)
            {
                throw new Errors.JsTypeError("ShadowRealm.prototype.evaluate can only return primitive values or callables", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
            }

            return result;
        }, 1);

        realm.InstallGlobal("ShadowRealm", ctor);
    }
}

/// <summary>
/// Internal wrapper for ShadowRealm data stored on JS objects.
/// </summary>
internal sealed class JsShadowRealmData : JsValue
{
    public Realm ShadowRealm { get; }

    public JsShadowRealmData(Realm shadowRealm) => ShadowRealm = shadowRealm;

    public override string TypeOf => "object";
    public override bool ToBoolean() => true;
    public override double ToNumber() => double.NaN;
    public override string ToJsString() => "[object ShadowRealm]";
}
