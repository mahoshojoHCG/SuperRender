using SuperRender.EcmaScript.Engine;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

/// <summary>Shared helpers for NodeSimulator tests.</summary>
internal static class TestHost
{
    internal static (JsEngine engine, NodeRuntime node) Create(params string[] argv)
    {
        var engine = new JsEngine();
        var node = SuperRender.EcmaScript.NodeSimulator.NodeSimulator.Install(engine, argv);
        return (engine, node);
    }

    internal static JsValue Run(this JsEngine engine, string code) => engine.Execute(code);

    internal static string RunString(this JsEngine engine, string code) => engine.Execute(code).ToJsString();
}
