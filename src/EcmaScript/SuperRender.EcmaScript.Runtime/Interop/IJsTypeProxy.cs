namespace SuperRender.EcmaScript.Runtime.Interop;

/// <summary>
/// Implemented by every proxy (both source-generated and <see cref="System.Reflection.DispatchProxy"/>-based)
/// so that nested <see cref="IJsType"/> arguments can be unwrapped back to their underlying <see cref="JsObjectBase"/>.
/// Public so generator-emitted proxy classes in consumer assemblies can implement it.
/// </summary>
public interface IJsTypeProxy
{
    JsObjectBase Target { get; }
}
