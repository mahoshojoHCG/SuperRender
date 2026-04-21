namespace SuperRender.EcmaScript.Runtime.Builtins;

using SuperRender.EcmaScript.Runtime;

public sealed partial class StructuredCloneHelper : IJsInstallable
{
    public static void Install(Realm realm)
    {
        realm.InstallGlobal("structuredClone", __JsFn_StructuredCloneImpl(realm));
    }

    [JsMethod("structuredClone")]
    internal static JsValue StructuredCloneImpl(JsValue value, Realm realm)
    {
        var seen = new Dictionary<JsObject, JsObject>(ReferenceEqualityComparer.Instance);
        return Clone(value, realm, seen);
    }

    private static JsValue Clone(JsValue value, Realm realm, Dictionary<JsObject, JsObject> seen)
    {
        // Primitives pass through
        if (value is JsUndefined or JsNull or JsBoolean or JsNumber or JsString)
            return value;

        // Functions and symbols are not cloneable
        if (value is JsFunction)
            throw new Errors.JsTypeError("Cannot clone a function", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);
        if (value is JsSymbol)
            throw new Errors.JsTypeError("Cannot clone a Symbol", ExecutionContext.CurrentLine, ExecutionContext.CurrentColumn);

        if (value is not JsObject obj)
            return value;

        // Circular reference check
        if (seen.TryGetValue(obj, out var existing))
            return existing;

        // Clone arrays
        if (obj is JsArray srcArr)
        {
            var cloneArr = new JsArray { Prototype = realm.ArrayPrototype };
            seen[obj] = cloneArr;
            for (var i = 0; i < srcArr.DenseLength; i++)
            {
                cloneArr.Push(Clone(srcArr.GetIndex(i), realm, seen));
            }

            return cloneArr;
        }

        // Clone RegExp
        if (obj is JsRegExp srcRegex)
        {
            var cloneRegex = new JsRegExp(srcRegex.Pattern, srcRegex.Flags) { Prototype = realm.RegExpPrototype };
            seen[obj] = cloneRegex;
            return cloneRegex;
        }

        // Clone Set
        if (obj is JsSetObject srcSet)
        {
            var cloneSet = new JsSetObject { Prototype = realm.SetPrototype };
            seen[obj] = cloneSet;
            foreach (var v in srcSet.SetValues())
            {
                cloneSet.SetAdd(Clone(v, realm, seen));
            }

            return cloneSet;
        }

        // Clone Map
        if (obj is JsMapObject srcMap)
        {
            var cloneMap = new JsMapObject { Prototype = realm.MapPrototype };
            seen[obj] = cloneMap;
            foreach (var (key, val) in srcMap.MapEntries())
            {
                cloneMap.MapSet(Clone(key, realm, seen), Clone(val, realm, seen));
            }

            return cloneMap;
        }

        // Clone Date
        if (obj is JsObject dateObj && dateObj.HasProperty("[[DateValue]]"))
        {
            var dateValue = dateObj.Get("[[DateValue]]");
            var cloneDate = new JsDynamicObject { Prototype = realm.DatePrototype };
            cloneDate.DefineOwnProperty("[[DateValue]]",
                PropertyDescriptor.Data(dateValue, writable: false, enumerable: false, configurable: false));
            seen[obj] = cloneDate;
            return cloneDate;
        }

        // Clone plain objects
        var clone = new JsDynamicObject { Prototype = realm.ObjectPrototype };
        seen[obj] = clone;
        foreach (var key in obj.OwnPropertyKeys())
        {
            var desc = obj.GetOwnProperty(key);
            if (desc?.Enumerable == true)
            {
                clone.Set(key, Clone(obj.Get(key), realm, seen));
            }
        }

        return clone;
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<JsObject>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public bool Equals(JsObject? x, JsObject? y) => ReferenceEquals(x, y);
        public int GetHashCode(JsObject obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
