namespace SuperRender.EcmaScript.Runtime;

public sealed class Realm
{
    public JsObject GlobalObject { get; }
    public Environment GlobalEnvironment { get; }

    // Intrinsic prototypes
    public JsObject ObjectPrototype { get; private set; } = null!;
    public JsFunction ObjectConstructorFn { get; private set; } = null!;
    public JsObject FunctionPrototype { get; private set; } = null!;
    public JsObject ArrayPrototype { get; private set; } = null!;
    public JsObject StringPrototype { get; private set; } = null!;
    public JsObject NumberPrototype { get; private set; } = null!;
    public JsObject BooleanPrototype { get; private set; } = null!;
    public JsObject RegExpPrototype { get; private set; } = null!;
    public JsObject DatePrototype { get; private set; } = null!;
    public JsObject ErrorPrototype { get; private set; } = null!;
    public JsObject SymbolPrototype { get; private set; } = null!;
    public JsObject MapPrototype { get; private set; } = null!;
    public JsObject SetPrototype { get; private set; } = null!;
    public JsObject PromisePrototype { get; private set; } = null!;
    public JsObject IteratorPrototype { get; private set; } = null!;
    public JsObject GeneratorPrototype { get; private set; } = null!;
    public JsObject BigIntPrototype { get; private set; } = null!;
    public JsObject WeakRefPrototype { get; private set; } = null!;
    public JsObject FinalizationRegistryPrototype { get; private set; } = null!;
    public JsObject ArrayBufferPrototype { get; private set; } = null!;
    public JsObject SharedArrayBufferPrototype { get; private set; } = null!;

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
        GlobalObject = new JsObject();
        GlobalEnvironment = new Environment();
        InitializeIntrinsics();
    }

    private void InitializeIntrinsics()
    {
        // Create the fundamental prototypes first
        ObjectPrototype = new JsObject { Prototype = null };
        FunctionPrototype = new JsObject { Prototype = ObjectPrototype };
        ArrayPrototype = new JsObject { Prototype = ObjectPrototype };
        StringPrototype = new JsObject { Prototype = ObjectPrototype };
        NumberPrototype = new JsObject { Prototype = ObjectPrototype };
        BooleanPrototype = new JsObject { Prototype = ObjectPrototype };
        RegExpPrototype = new JsObject { Prototype = ObjectPrototype };
        DatePrototype = new JsObject { Prototype = ObjectPrototype };
        ErrorPrototype = new JsObject { Prototype = ObjectPrototype };
        SymbolPrototype = new JsObject { Prototype = ObjectPrototype };
        MapPrototype = new JsObject { Prototype = ObjectPrototype };
        SetPrototype = new JsObject { Prototype = ObjectPrototype };
        PromisePrototype = new JsObject { Prototype = ObjectPrototype };
        IteratorPrototype = new JsObject { Prototype = ObjectPrototype };
        GeneratorPrototype = new JsObject { Prototype = IteratorPrototype };
        BigIntPrototype = new JsObject { Prototype = ObjectPrototype };
        WeakRefPrototype = new JsObject { Prototype = ObjectPrototype };
        FinalizationRegistryPrototype = new JsObject { Prototype = ObjectPrototype };
        ArrayBufferPrototype = new JsObject { Prototype = ObjectPrototype };
        SharedArrayBufferPrototype = new JsObject { Prototype = ObjectPrototype };

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
                JsFunction.CreateNative("[Symbol.iterator]", static (self, _) => self, 0),
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
                return new JsObject { Prototype = ObjectPrototype };
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
