namespace SuperRender.EcmaScript.Runtime.Interop;

using SuperRender.EcmaScript.Runtime.Errors;

/// <summary>
/// Boundary conversions shared by the source-generated proxies (emitted by
/// <c>SuperRender.Analyzer</c>) and the <see cref="JsTypeDispatchProxy"/> fallback.
/// All <c>ToJs</c> helpers marshal a C# primitive into a <see cref="JsValue"/>; all
/// <c>FromJs</c> helpers coerce a <see cref="JsValue"/> into a C# primitive using JS
/// coercion semantics (<see cref="JsValue.ToNumber"/>/<see cref="JsValue.ToBoolean"/>/
/// <see cref="JsValue.ToJsString"/>).
/// </summary>
public static class InteropConversions
{
    public static JsValue ToJs(string? value) => new JsString(value ?? string.Empty);

    public static JsValue ToJs(bool value) => value ? JsValue.True : JsValue.False;

    public static JsValue ToJs(double value) => JsNumber.Create(value);

    public static JsValue ToJs(float value) => JsNumber.Create(value);

    public static JsValue ToJs(decimal value) => JsNumber.Create((double)value);

    public static JsValue ToJs(int value) => JsNumber.Create(value);

    public static JsValue ToJs(uint value) => JsNumber.Create(value);

    public static JsValue ToJs(long value) => JsNumber.Create(value);

    public static JsValue ToJs(ulong value) => JsNumber.Create(value);

    public static JsValue ToJs(short value) => JsNumber.Create(value);

    public static JsValue ToJs(ushort value) => JsNumber.Create(value);

    public static JsValue ToJs(byte value) => JsNumber.Create(value);

    public static JsValue ToJs(sbyte value) => JsNumber.Create(value);

    public static string FromJsString(JsValue value) => value.ToJsString();

    public static bool FromJsBool(JsValue value) => value.ToBoolean();

    public static double FromJsDouble(JsValue value) => value.ToNumber();

    public static float FromJsFloat(JsValue value) => (float)value.ToNumber();

    public static decimal FromJsDecimal(JsValue value) => (decimal)value.ToNumber();

    public static int FromJsInt(JsValue value) => (int)value.ToNumber();

    public static uint FromJsUInt(JsValue value) => (uint)value.ToNumber();

    public static long FromJsLong(JsValue value) => (long)value.ToNumber();

    public static ulong FromJsULong(JsValue value) => (ulong)value.ToNumber();

    public static short FromJsShort(JsValue value) => (short)value.ToNumber();

    public static ushort FromJsUShort(JsValue value) => (ushort)value.ToNumber();

    public static byte FromJsByte(JsValue value) => (byte)value.ToNumber();

    public static sbyte FromJsSByte(JsValue value) => (sbyte)value.ToNumber();

    /// <summary>
    /// Requires <paramref name="value"/> to be a <see cref="JsObject"/>; throws <see cref="JsTypeError"/> otherwise.
    /// </summary>
    public static JsObject RequireObject(JsValue value, string memberName, int argIndex)
    {
        if (value is JsObject obj)
        {
            return obj;
        }

        throw new JsTypeError(
            $"{memberName}: argument {argIndex} must be an object",
            ExecutionContext.CurrentLine,
            ExecutionContext.CurrentColumn);
    }

    /// <summary>
    /// Unwraps an <see cref="IJsType"/> instance back to its underlying <see cref="JsObject"/>
    /// so it can be passed as an argument to a JS function. <c>null</c> maps to <see cref="JsValue.Null"/>.
    /// If the instance is a <c>[JsObject]</c>-partial that directly implements the interface, it is itself
    /// a <see cref="JsObject"/> and is used as-is.
    /// </summary>
    public static JsValue UnwrapIJsType(IJsType? value)
    {
        if (value is null)
        {
            return JsValue.Null;
        }

        if (value is IJsTypeProxy proxy)
        {
            return proxy.Target;
        }

        if (value is JsObject direct)
        {
            return direct;
        }

        throw new JsTypeError(
            $"IJsType value of type {value.GetType().FullName} has no backing JS object",
            ExecutionContext.CurrentLine,
            ExecutionContext.CurrentColumn);
    }

    /// <summary>
    /// Resolves a member on <paramref name="target"/> and requires it to be a callable <see cref="JsFunction"/>.
    /// </summary>
    public static JsFunction RequireFunction(JsObject target, string memberName)
    {
        var value = target.Get(memberName);
        if (value is JsFunction fn)
        {
            return fn;
        }

        throw new JsTypeError(
            $"'{memberName}' is not callable",
            ExecutionContext.CurrentLine,
            ExecutionContext.CurrentColumn);
    }
}
