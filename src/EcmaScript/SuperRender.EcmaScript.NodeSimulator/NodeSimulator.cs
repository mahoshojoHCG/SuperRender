namespace SuperRender.EcmaScript.NodeSimulator;

/// <summary>
/// Entry point for installing Node.js-compatible globals and built-in modules
/// (process, Buffer, require, fs, path, etc.) onto a <c>JsEngine</c>.
/// </summary>
public static class NodeSimulator
{
    public static void Install(SuperRender.EcmaScript.Engine.JsEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
    }
}
