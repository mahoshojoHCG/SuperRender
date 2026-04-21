using System.Globalization;
using SuperRender.Document.Dom;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// Array-like wrapper for NodeList/HTMLCollection query results.
/// </summary>
[JsObject]
internal sealed partial class JsNodeListWrapper : JsDynamicObject
{
    private readonly List<Node> _nodes;
    private readonly NodeWrapperCache _cache;

    public JsNodeListWrapper(List<Node> nodes, NodeWrapperCache cache)
    {
        _nodes = nodes;
        _cache = cache;

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            DefineOwnProperty(i.ToString(CultureInfo.InvariantCulture), PropertyDescriptor.Data(
                cache.GetOrCreate(node), writable: false, enumerable: true, configurable: true));
        }
    }

    [JsProperty("length")]
    public int Length => _nodes.Count;

    [JsMethod("item")]
    public JsValue Item(int index)
    {
        if (index >= 0 && index < _nodes.Count)
            return _cache.GetOrCreate(_nodes[index]);
        return JsValue.Null;
    }

    [JsMethod("forEach")]
    public void ForEach(JsValue[] args)
    {
        if (args.Length > 0 && args[0] is JsFunction callback)
        {
            for (int j = 0; j < _nodes.Count; j++)
            {
                var wrapped = _cache.GetOrCreate(_nodes[j]);
                callback.Call(JsValue.Undefined, [wrapped, JsNumber.Create(j)]);
            }
        }
    }
}
