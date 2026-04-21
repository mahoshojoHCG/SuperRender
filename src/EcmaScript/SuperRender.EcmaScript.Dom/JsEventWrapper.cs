using SuperRender.Document.Dom;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

// JSGEN006: event.target / event.currentTarget return the wrapped EventTarget (JsValue).
// Migration to IJsEventTarget IJsType is tracked separately.
#pragma warning disable JSGEN006

/// <summary>
/// JS wrapper for a DomEvent, exposing event properties to JavaScript.
/// Subtype-specific properties (MouseEvent, KeyboardEvent) are attached dynamically.
/// </summary>
[JsObject]
internal sealed partial class JsEventWrapper : JsDynamicObject
{
    private readonly DomEvent _evt;
    private readonly NodeWrapperCache _cache;

    public JsEventWrapper(DomEvent evt, NodeWrapperCache cache, Realm realm)
    {
        _evt = evt;
        _cache = cache;
        Prototype = realm.ObjectPrototype;

        if (evt is MouseEvent me)
        {
            DefineOwnProperty("clientX", PropertyDescriptor.Data(JsNumber.Create(Math.Round(me.ClientX))));
            DefineOwnProperty("clientY", PropertyDescriptor.Data(JsNumber.Create(Math.Round(me.ClientY))));
            DefineOwnProperty("button", PropertyDescriptor.Data(JsNumber.Create(me.Button)));
            DefineOwnProperty("ctrlKey", PropertyDescriptor.Data(me.CtrlKey ? True : False));
            DefineOwnProperty("shiftKey", PropertyDescriptor.Data(me.ShiftKey ? True : False));
            DefineOwnProperty("altKey", PropertyDescriptor.Data(me.AltKey ? True : False));
            DefineOwnProperty("metaKey", PropertyDescriptor.Data(me.MetaKey ? True : False));
        }

        if (evt is KeyboardEvent ke)
        {
            DefineOwnProperty("key", PropertyDescriptor.Data(new JsString(ke.Key)));
            DefineOwnProperty("code", PropertyDescriptor.Data(new JsString(ke.Code)));
            DefineOwnProperty("ctrlKey", PropertyDescriptor.Data(ke.CtrlKey ? True : False));
            DefineOwnProperty("shiftKey", PropertyDescriptor.Data(ke.ShiftKey ? True : False));
            DefineOwnProperty("altKey", PropertyDescriptor.Data(ke.AltKey ? True : False));
            DefineOwnProperty("metaKey", PropertyDescriptor.Data(ke.MetaKey ? True : False));
            DefineOwnProperty("repeat", PropertyDescriptor.Data(ke.Repeat ? True : False));
        }
    }

    [JsProperty("type")] public string Type => _evt.Type;
    [JsProperty("bubbles")] public bool Bubbles => _evt.Bubbles;
    [JsProperty("cancelable")] public bool Cancelable => _evt.Cancelable;
    [JsProperty("eventPhase")] public int EventPhase => _evt.EventPhase;
    [JsProperty("defaultPrevented")] public bool DefaultPrevented => _evt.DefaultPrevented;

    [JsProperty("target")] public JsValue Target => _cache.WrapNullable(_evt.Target);
    [JsProperty("currentTarget")] public JsValue CurrentTarget => _cache.WrapNullable(_evt.CurrentTarget);

    [JsMethod("preventDefault")] public void PreventDefault() => _evt.PreventDefault();
    [JsMethod("stopPropagation")] public void StopPropagation() => _evt.StopPropagation();
    [JsMethod("stopImmediatePropagation")] public void StopImmediatePropagation() => _evt.StopImmediatePropagation();
}
