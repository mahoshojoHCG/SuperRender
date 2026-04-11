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
    private readonly Dictionary<EventHandlerKey, Action<DomEvent>> _eventHandlerMap = [];

    public Realm Realm => _realm;

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

    /// <summary>
    /// Stores a mapping from JS function to C# event handler delegate for removeEventListener.
    /// </summary>
    public void StoreEventHandler(Node node, string type, JsFunction jsFn, bool capture, Action<DomEvent> wrapper)
    {
        _eventHandlerMap[new EventHandlerKey(node, type, jsFn, capture)] = wrapper;
    }

    /// <summary>
    /// Retrieves the C# delegate for a previously registered JS event handler.
    /// </summary>
    public Action<DomEvent>? RetrieveEventHandler(Node node, string type, JsFunction jsFn, bool capture)
    {
        _eventHandlerMap.TryGetValue(new EventHandlerKey(node, type, jsFn, capture), out var handler);
        return handler;
    }

    /// <summary>
    /// Removes the stored event handler mapping.
    /// </summary>
    public void RemoveEventHandler(Node node, string type, JsFunction jsFn, bool capture)
    {
        _eventHandlerMap.Remove(new EventHandlerKey(node, type, jsFn, capture));
    }

    private readonly record struct EventHandlerKey(Node Node, string Type, JsFunction Handler, bool Capture);
}
