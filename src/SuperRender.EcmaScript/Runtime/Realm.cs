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
