namespace SuperRender.EcmaScript.Runtime;

public sealed class Realm
{
    public JsDynamicObject GlobalObject { get; }
    public Environment GlobalEnvironment { get; }

    // Intrinsic prototypes
    public JsDynamicObject ObjectPrototype { get; private set; } = null!;
    public JsFunction ObjectConstructorFn { get; private set; } = null!;
    public JsDynamicObject FunctionPrototype { get; private set; } = null!;
    public JsDynamicObject ArrayPrototype { get; private set; } = null!;
    public JsDynamicObject StringPrototype { get; private set; } = null!;
    public JsDynamicObject NumberPrototype { get; private set; } = null!;
    public JsDynamicObject BooleanPrototype { get; private set; } = null!;
    public JsDynamicObject RegExpPrototype { get; private set; } = null!;
    public JsDynamicObject DatePrototype { get; private set; } = null!;
    public JsDynamicObject ErrorPrototype { get; private set; } = null!;
    public JsDynamicObject SymbolPrototype { get; private set; } = null!;
    public JsDynamicObject MapPrototype { get; private set; } = null!;
    public JsDynamicObject SetPrototype { get; private set; } = null!;
    public JsDynamicObject WeakMapPrototype { get; private set; } = null!;
    public JsDynamicObject WeakSetPrototype { get; private set; } = null!;
    public JsDynamicObject PromisePrototype { get; private set; } = null!;
    public JsDynamicObject IteratorPrototype { get; private set; } = null!;
    public JsDynamicObject GeneratorPrototype { get; private set; } = null!;
    public JsDynamicObject BigIntPrototype { get; private set; } = null!;
    public JsDynamicObject WeakRefPrototype { get; private set; } = null!;
    public JsDynamicObject FinalizationRegistryPrototype { get; private set; } = null!;
    public JsDynamicObject ArrayBufferPrototype { get; private set; } = null!;
    public JsDynamicObject SharedArrayBufferPrototype { get; private set; } = null!;

    /// <summary>
    /// Factory for dynamic Function() construction. Set by JsEngine.
    /// Accepts (paramNames[], bodySource) and returns a JsFunction.
    /// </summary>
    public Func<string[], string, JsFunction>? FunctionFactory { get; set; }

    /// <summary>
    /// Factory for eval(). Set by JsEngine.
    /// Accepts source code and the target realm, returns the evaluation result.
    /// </summary>
    public Func<string, Realm, JsValue>? EvalFactory { get; set; }

    public Realm()
    {
        GlobalObject = new JsDynamicObject();
        GlobalEnvironment = new Environment();
        InitializeIntrinsics();
    }

    private void InitializeIntrinsics()
    {
        // Create the fundamental prototypes first
        ObjectPrototype = new JsDynamicObject { Prototype = null };
        FunctionPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        ArrayPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        StringPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        NumberPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        BooleanPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        RegExpPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        DatePrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        ErrorPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        SymbolPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        MapPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        SetPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        WeakMapPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        WeakSetPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        PromisePrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        IteratorPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        GeneratorPrototype = new JsDynamicObject { Prototype = IteratorPrototype };
        BigIntPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        WeakRefPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        FinalizationRegistryPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        ArrayBufferPrototype = new JsDynamicObject { Prototype = ObjectPrototype };
        SharedArrayBufferPrototype = new JsDynamicObject { Prototype = ObjectPrototype };

        // Generator prototype methods: next, return, throw
        GeneratorPrototype.DefineOwnProperty("next", PropertyDescriptor.Data(
            JsFunction.CreateNative("next", (thisArg, args) =>
            {
                if (thisArg is not JsGeneratorObject gen)
                    throw new Errors.JsTypeError("Method next called on incompatible receiver", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                var sent = args.Length > 0 ? args[0] : JsValue.Undefined;
                return gen.DoNext(sent);
            }, 1), writable: true, enumerable: false, configurable: true));

        GeneratorPrototype.DefineOwnProperty("return", PropertyDescriptor.Data(
            JsFunction.CreateNative("return", (thisArg, args) =>
            {
                if (thisArg is not JsGeneratorObject gen)
                    throw new Errors.JsTypeError("Method return called on incompatible receiver", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                var value = args.Length > 0 ? args[0] : JsValue.Undefined;
                return gen.DoReturn(value);
            }, 1), writable: true, enumerable: false, configurable: true));

        GeneratorPrototype.DefineOwnProperty("throw", PropertyDescriptor.Data(
            JsFunction.CreateNative("throw", (thisArg, args) =>
            {
                if (thisArg is not JsGeneratorObject gen)
                    throw new Errors.JsTypeError("Method throw called on incompatible receiver", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
                var error = args.Length > 0 ? args[0] : JsValue.Undefined;
                return gen.DoThrow(error);
            }, 1), writable: true, enumerable: false, configurable: true));

        // Symbol.iterator on generator prototype returns self
        GeneratorPrototype.DefineSymbolProperty(JsSymbol.Iterator,
            PropertyDescriptor.Data(
                Builtins.BuiltinHelper.__JsFn_SymbolIteratorSelf(),
                writable: false, enumerable: false, configurable: true));

        // Symbol.toStringTag
        GeneratorPrototype.DefineSymbolProperty(JsSymbol.ToStringTag,
            PropertyDescriptor.Data(new JsString("Generator"),
                writable: false, enumerable: false, configurable: true));

        // Object constructor
        ObjectConstructorFn = JsFunction.CreateNative("Object", (_, args) =>
        {
            if (args.Length == 0 || args[0] is JsNull or JsUndefined)
            {
                return new JsDynamicObject { Prototype = ObjectPrototype };
            }

            return args[0];
        }, 1);
        ObjectConstructorFn.Prototype = FunctionPrototype;
        ObjectConstructorFn.PrototypeObject = ObjectPrototype;

        // Set up global object prototype chain
        GlobalObject.Prototype = ObjectPrototype;

        // Install basic globals
        GlobalEnvironment.CreateAndInitializeBinding("undefined", false, JsValue.Undefined);
        GlobalEnvironment.CreateAndInitializeBinding("NaN", false, JsNumber.NaN);
        GlobalEnvironment.CreateAndInitializeBinding("Infinity", false, JsNumber.PositiveInfinity);
    }

    public void InstallGlobal(string name, JsValue value)
    {
        GlobalObject.DefineOwnProperty(name, PropertyDescriptor.Data(value, writable: true, enumerable: false, configurable: true));
        GlobalEnvironment.CreateAndInitializeBinding(name, true, value);
    }
}
