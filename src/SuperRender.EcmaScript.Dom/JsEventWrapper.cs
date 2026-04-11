using SuperRender.Core.Dom;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// JS wrapper for a DomEvent, exposing event properties to JavaScript.
/// </summary>
internal sealed class JsEventWrapper : JsObject
{
    public JsEventWrapper(DomEvent evt, NodeWrapperCache cache, Realm realm)
    {
        Prototype = realm.ObjectPrototype;

        DefineOwnProperty("type", PropertyDescriptor.Data(new JsString(evt.Type)));
        DefineOwnProperty("bubbles", PropertyDescriptor.Data(evt.Bubbles ? True : False));
        DefineOwnProperty("cancelable", PropertyDescriptor.Data(evt.Cancelable ? True : False));
        DefineOwnProperty("eventPhase", PropertyDescriptor.Data(JsNumber.Create(evt.EventPhase)));

        DefineOwnProperty("target", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get target", (_, _) => cache.WrapNullable(evt.Target), 0),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("currentTarget", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get currentTarget", (_, _) => cache.WrapNullable(evt.CurrentTarget), 0),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("defaultPrevented", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get defaultPrevented", (_, _) => evt.DefaultPrevented ? True : False, 0),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("preventDefault", PropertyDescriptor.Data(
            JsFunction.CreateNative("preventDefault", (_, _) => { evt.PreventDefault(); return Undefined; }, 0)));

        DefineOwnProperty("stopPropagation", PropertyDescriptor.Data(
            JsFunction.CreateNative("stopPropagation", (_, _) => { evt.StopPropagation(); return Undefined; }, 0)));

        DefineOwnProperty("stopImmediatePropagation", PropertyDescriptor.Data(
            JsFunction.CreateNative("stopImmediatePropagation", (_, _) => { evt.StopImmediatePropagation(); return Undefined; }, 0)));

        // MouseEvent properties
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

        // KeyboardEvent properties
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
}
