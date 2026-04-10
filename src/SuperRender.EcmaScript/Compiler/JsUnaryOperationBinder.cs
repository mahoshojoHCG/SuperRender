using System.Dynamic;
using System.Linq.Expressions;
using SuperRender.EcmaScript.Runtime;
using Expr = System.Linq.Expressions.Expression;

namespace SuperRender.EcmaScript.Compiler;

/// <summary>
/// DLR binder for JavaScript unary operations (typeof, !, -, +, ~, void).
/// Delegates to <see cref="RuntimeHelpers"/> static methods.
/// </summary>
public sealed class JsUnaryOperationBinder : UnaryOperationBinder
{
    public JsUnaryOperationBinder(ExpressionType operation) : base(operation) { }

    public override DynamicMetaObject FallbackUnaryOperation(DynamicMetaObject target, DynamicMetaObject? errorSuggestion)
    {
        if (!target.HasValue)
        {
            return Defer(target);
        }

        var restrictions = BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType);
        var operand = Expr.Convert(target.Expression, typeof(JsValue));

        string? methodName = Operation switch
        {
            ExpressionType.Not => nameof(RuntimeHelpers.Not),
            ExpressionType.Negate => nameof(RuntimeHelpers.Negate),
            ExpressionType.UnaryPlus => nameof(RuntimeHelpers.Plus),
            ExpressionType.OnesComplement => nameof(RuntimeHelpers.BitwiseNot),
            _ => null
        };

        if (methodName is null)
        {
            var throwExpr = Expr.Throw(
                Expr.New(
                    typeof(Errors.JsTypeError).GetConstructor([typeof(string)])!,
                    Expr.Constant($"Unsupported unary operation: {Operation}")),
                typeof(object));
            return errorSuggestion ?? new DynamicMetaObject(throwExpr, restrictions);
        }

        var method = typeof(RuntimeHelpers).GetMethod(methodName, [typeof(JsValue)])!;
        var call = Expr.Call(method, operand);

        return errorSuggestion ?? new DynamicMetaObject(
            Expr.Convert(call, typeof(object)),
            restrictions);
    }
}
