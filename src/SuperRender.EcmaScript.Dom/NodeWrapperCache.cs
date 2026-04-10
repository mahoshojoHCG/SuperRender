using System.Runtime.CompilerServices;
using SuperRender.Core.Dom;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// Maintains a 1:1 mapping between C# DOM nodes and their JS wrappers.
/// Uses ConditionalWeakTable so wrappers are collected when the C# node is collected.
/// </summary>
internal sealed class NodeWrapperCache
{
    private readonly ConditionalWeakTable<Node, JsObject> _cache = new();
    private readonly Realm _realm;

    public NodeWrapperCache(Realm realm)
    {
        _realm = realm;
    }

    public JsObject GetOrCreate(Node node)
    {
        if (_cache.TryGetValue(node, out var wrapper))
            return wrapper;

        wrapper = node switch
        {
            Document doc => new JsDocumentWrapper(doc, this, _realm),
            Element elem => new JsElementWrapper(elem, this, _realm),
            TextNode text => new JsTextNodeWrapper(text, this, _realm),
            _ => new JsNodeWrapper(node, this, _realm)
        };

        _cache.AddOrUpdate(node, wrapper);
        return wrapper;
    }

    public JsValue WrapNullable(Node? node)
    {
        return node is null ? JsValue.Null : GetOrCreate(node);
    }
}
