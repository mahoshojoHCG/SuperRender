namespace SuperRender.EcmaScript.Runtime;

#pragma warning disable CA1000 // per-T static members are intentional on this value wrapper

public readonly record struct JsOptional<T>(bool HasValue, T? Value)
{
    public static JsOptional<T> Undefined { get; } = new(false, default);

    public static JsOptional<T> Of(T value) => new(true, value);

    public static implicit operator JsOptional<T>(T value) => Of(value);
}
