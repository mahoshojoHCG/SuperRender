using System.Dynamic;
using SuperRender.EcmaScript.Runtime;
using Expr = System.Linq.Expressions.Expression;

namespace SuperRender.EcmaScript.Compiler;

/// <summary>
/// DLR binder for property get access on JS values.
/// Delegates to <see cref="JsDynamicObject.Get(string)"/> when the target is a JsDynamicObject;
/// otherwise returns <see cref="JsValue.Undefined"/>.
/// </summary>
public sealed class JsGetMemberBinder : GetMemberBinder
{
    public JsGetMemberBinder(string name) : base(name, ignoreCase: false) { }

    public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject? errorSuggestion)
    {
        // Ensure the target value is available (deferred binding)
        if (!target.HasValue)
        {
            return Defer(target);
        }

        var restrictions = BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType);

        Expr result;
        if (typeof(JsDynamicObject).IsAssignableFrom(target.LimitType))
        {
            // Target is a JsDynamicObject — call Get(name)
            var self = Expr.Convert(target.Expression, typeof(JsDynamicObject));
            result = Expr.Call(
                self,
                typeof(JsDynamicObject).GetMethod(nameof(JsDynamicObject.Get), [typeof(string)])!,
                Expr.Constant(Name));
        }
        else
        {
            // Not a JsDynamicObject — return undefined
            result = Expr.Constant(JsValue.Undefined, typeof(JsValue));
        }

        return errorSuggestion ?? new DynamicMetaObject(
            Expr.Convert(result, typeof(object)),
            restrictions);
    }
}
