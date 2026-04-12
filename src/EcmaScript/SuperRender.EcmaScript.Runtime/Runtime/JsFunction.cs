namespace SuperRender.EcmaScript.Runtime;

public class JsFunction : JsObject
{
    public Func<JsValue, JsValue[], JsValue>? CallTarget { get; set; }
    public Func<JsValue[], JsValue>? ConstructTarget { get; set; }
    public Environment? ClosureScope { get; set; }
    public string Name { get; set; } = "";
    public int Length { get; init; }
    public JsObject? PrototypeObject { get; set; }
    public bool IsConstructor { get; init; }

    public override string TypeOf => "function";

    public virtual JsValue Call(JsValue thisArg, JsValue[] arguments)
    {
        if (CallTarget is null)
        {
            throw new Errors.JsTypeError($"{Name} is not callable", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        return CallTarget(thisArg, arguments);
    }

    public virtual JsValue Construct(JsValue[] arguments)
    {
        if (ConstructTarget is not null)
        {
            return ConstructTarget(arguments);
        }

        if (!IsConstructor || CallTarget is null)
        {
            throw new Errors.JsTypeError($"{Name} is not a constructor", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        }

        var newObj = new JsObject
        {
            Prototype = PrototypeObject ?? Prototype
        };

        var result = CallTarget(newObj, arguments);

        // If the constructor returned an object, use that; otherwise use newObj
        if (result is JsObject returnedObj)
        {
            return returnedObj;
        }

        return newObj;
    }

    public override JsValue Get(string name)
    {
        return name switch
        {
            "name" => new JsString(Name),
            "length" => JsNumber.Create(Length),
            "prototype" => (JsValue?)PrototypeObject ?? Undefined,
            _ => base.Get(name)
        };
    }

    public override bool HasProperty(string name)
    {
        return name is "name" or "length" or "prototype" || base.HasProperty(name);
    }

    public static JsFunction CreateNative(string name, Func<JsValue, JsValue[], JsValue> impl, int length) =>
        new()
        {
            Name = name,
            CallTarget = impl,
            Length = length,
            IsConstructor = false
        };
}
