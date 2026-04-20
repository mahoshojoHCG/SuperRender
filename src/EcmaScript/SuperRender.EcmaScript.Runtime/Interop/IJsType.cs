namespace SuperRender.EcmaScript.Runtime.Interop;

/// <summary>
/// Marker interface. Any interface inheriting <see cref="IJsType"/> can be passed as the
/// type parameter to <see cref="JsValueExtension.AsInterface{T}(JsValue)"/> to obtain a
/// structurally-typed view over a JS object. The <c>SuperRender.Analyzer</c> source
/// generator produces a sealed proxy implementation per interface; if the generator has
/// not run for the type, <see cref="System.Reflection.DispatchProxy"/> is used at runtime.
/// </summary>
public interface IJsType
{
}
