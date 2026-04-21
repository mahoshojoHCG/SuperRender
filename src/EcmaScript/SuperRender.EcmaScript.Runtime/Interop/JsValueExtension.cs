namespace SuperRender.EcmaScript.Runtime.Interop;

using System.Runtime.CompilerServices;
using SuperRender.EcmaScript.Runtime.Errors;

/// <summary>
/// Provides <c>value.AsInterface&lt;T&gt;()</c> — a typed structural view over any JS object.
/// </summary>
public static class JsValueExtension
{
    private static readonly ConditionalWeakTable<JsObject, Dictionary<Type, object>> Cache = new();

    extension(JsValue value)
    {
        /// <summary>
        /// Returns a proxy that implements <typeparamref name="T"/> and forwards member access to the
        /// underlying JS object. Uses the source-generated proxy when registered, else falls back to
        /// <see cref="System.Reflection.DispatchProxy"/>.
        /// Throws <see cref="JsTypeError"/> if <paramref name="value"/> is not a JS object.
        /// </summary>
        public T AsInterface<T>()
            where T : class, IJsType
            => (T)AsInterfaceOf(value, typeof(T));
    }

    /// <summary>Non-generic counterpart; used internally for recursive wrapping of nested <see cref="IJsType"/> returns.</summary>
    internal static object AsInterfaceOf(JsValue value, Type interfaceType)
    {
        if (value is not JsObject obj)
        {
            throw new JsTypeError(
                $"Cannot apply interface {interfaceType.Name} to non-object value of type {value.TypeOf}",
                ExecutionContext.CurrentLine,
                ExecutionContext.CurrentColumn);
        }

        // Fast path: the backing object directly implements T (e.g., [JsObject] partial class : JsObject, IFoo).
        if (interfaceType.IsInstanceOfType(obj))
        {
            return obj;
        }

        var bag = Cache.GetValue(obj, static _ => new Dictionary<Type, object>());
        lock (bag)
        {
            if (bag.TryGetValue(interfaceType, out var cached))
            {
                return cached;
            }

            object proxy;
            if (JsTypeInterfaceProxyRegistry.TryCreate(interfaceType, obj, out var generated))
            {
                proxy = generated!;
            }
            else
            {
                proxy = DispatchProxyFactory.Create(interfaceType, obj);
            }

            bag[interfaceType] = proxy;
            return proxy;
        }
    }
}
