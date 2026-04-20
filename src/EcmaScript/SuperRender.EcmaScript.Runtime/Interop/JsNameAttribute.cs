namespace SuperRender.EcmaScript.Runtime.Interop;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class JsNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
