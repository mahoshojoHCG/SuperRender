using SuperRender.Core.Dom;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// Array-like wrapper for NodeList/HTMLCollection query results.
/// </summary>
internal sealed class JsNodeListWrapper : JsObject
{
    public JsNodeListWrapper(List<Node> nodes, NodeWrapperCache cache)
    {
        DefineOwnProperty("length", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get length", (_, _) => JsNumber.Create(nodes.Count), 0),
            null, enumerable: true, configurable: true));

        // Index access
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            DefineOwnProperty(i.ToString(System.Globalization.CultureInfo.InvariantCulture), PropertyDescriptor.Data(
                cache.GetOrCreate(node), writable: false, enumerable: true, configurable: true));
        }

        DefineOwnProperty("item", PropertyDescriptor.Data(
            JsFunction.CreateNative("item", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var idx = (int)args[0].ToNumber();
                    if (idx >= 0 && idx < nodes.Count)
                        return cache.GetOrCreate(nodes[idx]);
                }
                return Null;
            }, 1)));

        DefineOwnProperty("forEach", PropertyDescriptor.Data(
            JsFunction.CreateNative("forEach", (_, args) =>
            {
                if (args.Length > 0 && args[0] is JsFunction callback)
                {
                    for (int j = 0; j < nodes.Count; j++)
                    {
                        var wrapped = cache.GetOrCreate(nodes[j]);
                        callback.Call(Undefined, [wrapped, JsNumber.Create(j)]);
                    }
                }
                return Undefined;
            }, 1)));
    }
}
