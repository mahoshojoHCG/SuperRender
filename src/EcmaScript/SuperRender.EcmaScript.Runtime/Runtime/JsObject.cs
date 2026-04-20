namespace SuperRender.EcmaScript.Runtime;

// Transitional shim: existing call sites (`new JsObject()`, `: JsObject`, `is JsObject`)
// continue to work while the split into JsObjectBase + JsDynamicObject rolls out.
// New attribute-driven [JsObject] classes inherit JsObjectBase directly.
public class JsObject : JsDynamicObject
{
}
