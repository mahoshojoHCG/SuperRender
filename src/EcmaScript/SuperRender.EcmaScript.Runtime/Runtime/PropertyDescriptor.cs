namespace SuperRender.EcmaScript.Runtime;

public sealed class PropertyDescriptor
{
    public JsValue? Value { get; set; }
    public JsValue? Get { get; set; }
    public JsValue? Set { get; set; }
    public bool? Writable { get; set; }
    public bool? Enumerable { get; set; }
    public bool? Configurable { get; set; }
    public bool IsAccessorDescriptor => Get is not null || Set is not null;
    public bool IsDataDescriptor => Value is not null || Writable is not null;

    public static PropertyDescriptor Data(JsValue value, bool writable = true, bool enumerable = true, bool configurable = true) =>
        new() { Value = value, Writable = writable, Enumerable = enumerable, Configurable = configurable };

    public static PropertyDescriptor Accessor(JsValue? getter, JsValue? setter, bool enumerable = true, bool configurable = true) =>
        new() { Get = getter, Set = setter, Enumerable = enumerable, Configurable = configurable };
}
