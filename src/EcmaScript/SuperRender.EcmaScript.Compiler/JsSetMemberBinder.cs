using System.Dynamic;
using SuperRender.EcmaScript.Runtime;
using Expr = System.Linq.Expressions.Expression;

namespace SuperRender.EcmaScript.Compiler;

/// <summary>
/// DLR binder for property set access on JS values.
/// Delegates to <see cref="JsDynamicObject.Set(string, JsValue)"/> when the target is a JsDynamicObject;
/// otherwise silently ignores the assignment (non-strict mode behavior).
/// </summary>
public sealed class JsSetMemberBinder : SetMemberBinder
{
    public JsSetMemberBinder(string name) : base(name, ignoreCase: false) { }

    public override DynamicMetaObject FallbackSetMember(DynamicMetaObject target, DynamicMetaObject value, DynamicMetaObject? errorSuggestion)
    {
        if (!target.HasValue || !value.HasValue)
        {
            return Defer(target, value);
        }

        var restrictions = BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType);

        Expr result;
        if (typeof(JsDynamicObject).IsAssignableFrom(target.LimitType))
        {
            var self = Expr.Convert(target.Expression, typeof(JsDynamicObject));
            var val = Expr.Convert(value.Expression, typeof(JsValue));
            var call = Expr.Call(
                self,
                typeof(JsDynamicObject).GetMethod(nameof(JsDynamicObject.Set), [typeof(string), typeof(JsValue)])!,
                Expr.Constant(Name),
                val);
            // SetMember must return the value that was assigned
            result = Expr.Block(typeof(JsValue), call, Expr.Convert(value.Expression, typeof(JsValue)));
        }
        else
        {
            // Not a JsDynamicObject — return the value as-is (no-op set)
            result = Expr.Convert(value.Expression, typeof(JsValue));
        }

        return errorSuggestion ?? new DynamicMetaObject(
            Expr.Convert(result, typeof(object)),
            restrictions);
    }
}
