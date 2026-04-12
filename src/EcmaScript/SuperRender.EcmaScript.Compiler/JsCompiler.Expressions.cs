// CA1859: These private Compile* methods intentionally return the abstract Expr type
// for a uniform internal API — the caller (CompileNode) dispatches via pattern matching
// and always works with Expr, so narrowing return types would add no value.
// CA1822: Private Compile* methods are part of the compiler instance API even when a
// particular method does not currently access instance state (e.g. stubs for hoisted decls).
#pragma warning disable CA1859, CA1822

using System.Globalization;
using SuperRender.EcmaScript.Compiler.Ast;
using SuperRender.EcmaScript.Runtime.Errors;
using SuperRender.EcmaScript.Runtime;
using Expr = System.Linq.Expressions.Expression;

namespace SuperRender.EcmaScript.Compiler;

public sealed partial class JsCompiler
{
    // ───────────────────────── Expressions ─────────────────────────

    private Expr CompileIdentifier(Identifier node)
    {
        return CallEnvMethod("GetBinding", Expr.Constant(node.Name));
    }

    private Expr CompileLiteral(Literal node)
    {
        return node.Value switch
        {
            null => Expr.Constant(JsValue.Null, typeof(JsValue)),
            bool b => Expr.Constant(b ? JsValue.True : JsValue.False, typeof(JsValue)),
            double d => Expr.Call(
                typeof(JsNumber).GetMethod(nameof(JsNumber.Create), [typeof(double)])!,
                Expr.Constant(d)),
            int i => Expr.Call(
                typeof(JsNumber).GetMethod(nameof(JsNumber.Create), [typeof(double)])!,
                Expr.Constant((double)i)),
            long l => Expr.Call(
                typeof(JsNumber).GetMethod(nameof(JsNumber.Create), [typeof(double)])!,
                Expr.Constant((double)l)),
            string s => Expr.Convert(
                Expr.New(
                    typeof(JsString).GetConstructor([typeof(string)])!,
                    Expr.Constant(s)),
                typeof(JsValue)),
            _ => Expr.Constant(JsValue.Undefined, typeof(JsValue))
        };
    }

    private Expr CompileBinary(BinaryExpression node)
    {
        var left = CompileNode(node.Left);
        var right = CompileNode(node.Right);

        string methodName = node.Operator switch
        {
            "+" => nameof(RuntimeHelpers.Add),
            "-" => nameof(RuntimeHelpers.Sub),
            "*" => nameof(RuntimeHelpers.Mul),
            "/" => nameof(RuntimeHelpers.Div),
            "%" => nameof(RuntimeHelpers.Mod),
            "**" => nameof(RuntimeHelpers.Power),
            "===" => nameof(RuntimeHelpers.StrictEqual),
            "!==" => nameof(RuntimeHelpers.StrictNotEqual),
            "==" => nameof(RuntimeHelpers.AbstractEqual),
            "!=" => nameof(RuntimeHelpers.AbstractNotEqual),
            "<" => nameof(RuntimeHelpers.LessThan),
            ">" => nameof(RuntimeHelpers.GreaterThan),
            "<=" => nameof(RuntimeHelpers.LessThanOrEqual),
            ">=" => nameof(RuntimeHelpers.GreaterThanOrEqual),
            "&" => nameof(RuntimeHelpers.BitwiseAnd),
            "|" => nameof(RuntimeHelpers.BitwiseOr),
            "^" => nameof(RuntimeHelpers.BitwiseXor),
            "<<" => nameof(RuntimeHelpers.LeftShift),
            ">>" => nameof(RuntimeHelpers.RightShift),
            ">>>" => nameof(RuntimeHelpers.UnsignedRightShift),
            "instanceof" => nameof(RuntimeHelpers.InstanceOf),
            "in" => nameof(RuntimeHelpers.In),
            _ => throw new JsSyntaxError($"Unknown binary operator: {node.Operator}")
        };

        return Expr.Call(
            typeof(RuntimeHelpers).GetMethod(methodName, [typeof(JsValue), typeof(JsValue)])!,
            EnsureJsValue(left),
            EnsureJsValue(right));
    }

    private Expr CompileLogical(LogicalExpression node)
    {
        var left = CompileNode(node.Left);
        var leftVar = Expr.Parameter(typeof(JsValue), "logicalLeft");

        return node.Operator switch
        {
            "&&" => Expr.Block(typeof(JsValue), [leftVar],
                Expr.Assign(leftVar, EnsureJsValue(left)),
                Expr.Condition(
                    CompileToBool(leftVar),
                    EnsureJsValue(CompileNode(node.Right)),
                    leftVar,
                    typeof(JsValue))),

            "||" => Expr.Block(typeof(JsValue), [leftVar],
                Expr.Assign(leftVar, EnsureJsValue(left)),
                Expr.Condition(
                    CompileToBool(leftVar),
                    leftVar,
                    EnsureJsValue(CompileNode(node.Right)),
                    typeof(JsValue))),

            "??" => Expr.Block(typeof(JsValue), [leftVar],
                Expr.Assign(leftVar, EnsureJsValue(left)),
                Expr.Condition(
                    Expr.OrElse(
                        Expr.TypeIs(leftVar, typeof(JsNull)),
                        Expr.TypeIs(leftVar, typeof(JsUndefined))),
                    EnsureJsValue(CompileNode(node.Right)),
                    leftVar,
                    typeof(JsValue))),

            _ => throw new JsSyntaxError($"Unknown logical operator: {node.Operator}")
        };
    }

    private Expr CompileUnary(UnaryExpression node)
    {
        if (node.Operator == "typeof")
        {
            // typeof needs special handling for undefined variables
            if (node.Argument is Identifier id)
            {
                return Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.TypeofSafe))!,
                    _envParam,
                    Expr.Constant(id.Name));
            }

            return Expr.Call(
                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.Typeof))!,
                EnsureJsValue(CompileNode(node.Argument)));
        }

        if (node.Operator == "delete")
        {
            return CompileDelete(node.Argument);
        }

        if (node.Operator == "void")
        {
            return Expr.Block(typeof(JsValue),
                ToVoid(CompileNode(node.Argument)),
                Expr.Constant(JsValue.Undefined, typeof(JsValue)));
        }

        string methodName = node.Operator switch
        {
            "!" => nameof(RuntimeHelpers.Not),
            "-" => nameof(RuntimeHelpers.Negate),
            "+" => nameof(RuntimeHelpers.Plus),
            "~" => nameof(RuntimeHelpers.BitwiseNot),
            _ => throw new JsSyntaxError($"Unknown unary operator: {node.Operator}")
        };

        return Expr.Call(
            typeof(RuntimeHelpers).GetMethod(methodName, [typeof(JsValue)])!,
            EnsureJsValue(CompileNode(node.Argument)));
    }

    private Expr CompileUpdate(UpdateExpression node)
    {
        // ++x, --x, x++, x--
        if (node.Argument is Identifier id)
        {
            var current = CallEnvMethod("GetBinding", Expr.Constant(id.Name));
            var currentVar = Expr.Parameter(typeof(JsValue), "current");
            var numVal = Expr.Call(currentVar, typeof(JsValue).GetMethod(nameof(JsValue.ToNumber))!);

            var delta = node.Operator == "++" ? 1.0 : -1.0;
            var updated = Expr.Call(
                typeof(JsNumber).GetMethod(nameof(JsNumber.Create), [typeof(double)])!,
                Expr.Add(numVal, Expr.Constant(delta)));

            var updatedAsJsValue = Expr.Convert(updated, typeof(JsValue));

            var assign = CallEnvMethod("SetBinding", Expr.Constant(id.Name), updatedAsJsValue);

            var result = node.Prefix ? updatedAsJsValue : Expr.Convert(
                Expr.Call(typeof(JsNumber).GetMethod(nameof(JsNumber.Create), [typeof(double)])!, numVal),
                typeof(JsValue));

            return Expr.Block(typeof(JsValue), [currentVar],
                Expr.Assign(currentVar, current),
                assign,
                result);
        }

        if (node.Argument is MemberExpression me)
        {
            return CompileUpdateMember(me, node.Operator, node.Prefix);
        }

        throw new JsSyntaxError("Invalid update expression target");
    }

    private Expr CompileUpdateMember(MemberExpression me, string op, bool prefix)
    {
        var obj = CompileNode(me.Object);
        var objVar = Expr.Parameter(typeof(JsValue), "updObj");
        var currentVar = Expr.Parameter(typeof(JsValue), "updCurrent");

        var exprs = new List<Expr> { Expr.Assign(objVar, EnsureJsValue(obj)) };

        Expr getCurrent;
        Expr setCurrent;

        if (me.Computed)
        {
            var key = CompileNode(me.Property);
            var keyVar = Expr.Parameter(typeof(JsValue), "updKey");
            exprs.Add(Expr.Assign(keyVar, EnsureJsValue(key)));

            getCurrent = Expr.Call(
                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetComputedMember))!,
                objVar, keyVar);

            var delta = op == "++" ? 1.0 : -1.0;
            var numVal = Expr.Call(currentVar, typeof(JsValue).GetMethod(nameof(JsValue.ToNumber))!);
            var updated = Expr.Convert(
                Expr.Call(typeof(JsNumber).GetMethod(nameof(JsNumber.Create), [typeof(double)])!,
                    Expr.Add(numVal, Expr.Constant(delta))),
                typeof(JsValue));

            setCurrent = Expr.Call(
                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.SetComputedMember))!,
                objVar, keyVar, updated);

            var result = prefix ? updated : Expr.Convert(
                Expr.Call(typeof(JsNumber).GetMethod(nameof(JsNumber.Create), [typeof(double)])!, numVal),
                typeof(JsValue));

            exprs.Add(Expr.Assign(currentVar, getCurrent));
            exprs.Add(setCurrent);
            exprs.Add(result);

            return Expr.Block(typeof(JsValue), [objVar, keyVar, currentVar], exprs);
        }
        else
        {
            var name = ((Identifier)me.Property).Name;
            getCurrent = Expr.Call(
                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetMember))!,
                objVar, Expr.Constant(name));

            var delta = op == "++" ? 1.0 : -1.0;
            var numVal = Expr.Call(currentVar, typeof(JsValue).GetMethod(nameof(JsValue.ToNumber))!);
            var updated = Expr.Convert(
                Expr.Call(typeof(JsNumber).GetMethod(nameof(JsNumber.Create), [typeof(double)])!,
                    Expr.Add(numVal, Expr.Constant(delta))),
                typeof(JsValue));

            setCurrent = Expr.Call(
                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.SetMember))!,
                objVar, Expr.Constant(name), updated);

            var result = prefix ? updated : Expr.Convert(
                Expr.Call(typeof(JsNumber).GetMethod(nameof(JsNumber.Create), [typeof(double)])!, numVal),
                typeof(JsValue));

            exprs.Add(Expr.Assign(currentVar, getCurrent));
            exprs.Add(setCurrent);
            exprs.Add(result);

            return Expr.Block(typeof(JsValue), [objVar, currentVar], exprs);
        }
    }

    private Expr CompileAssignment(AssignmentExpression node)
    {
        if (node.Left is Identifier id)
        {
            return CompileSimpleAssignment(id.Name, node.Operator, node.Right);
        }

        if (node.Left is MemberExpression me)
        {
            return CompileMemberAssignment(me, node.Operator, node.Right);
        }

        if (node.Left is ObjectPattern or ArrayPattern)
        {
            // Destructuring assignment
            var value = CompileNode(node.Right);
            var valVar = Expr.Parameter(typeof(JsValue), "destructVal");
            return Expr.Block(typeof(JsValue), [valVar],
                Expr.Assign(valVar, EnsureJsValue(value)),
                CompileBindingPattern(node.Left, valVar, mutable: true),
                valVar);
        }

        throw new JsSyntaxError($"Invalid assignment target: {node.Left.GetType().Name}");
    }

    private Expr CompileSimpleAssignment(string name, string op, SyntaxNode right)
    {
        Expr value;
        if (op == "=")
        {
            value = CompileNode(right);
        }
        else
        {
            var current = CallEnvMethod("GetBinding", Expr.Constant(name));
            var rightVal = CompileNode(right);
            string helperMethod = op switch
            {
                "+=" => nameof(RuntimeHelpers.Add),
                "-=" => nameof(RuntimeHelpers.Sub),
                "*=" => nameof(RuntimeHelpers.Mul),
                "/=" => nameof(RuntimeHelpers.Div),
                "%=" => nameof(RuntimeHelpers.Mod),
                "**=" => nameof(RuntimeHelpers.Power),
                "&=" => nameof(RuntimeHelpers.BitwiseAnd),
                "|=" => nameof(RuntimeHelpers.BitwiseOr),
                "^=" => nameof(RuntimeHelpers.BitwiseXor),
                "<<=" => nameof(RuntimeHelpers.LeftShift),
                ">>=" => nameof(RuntimeHelpers.RightShift),
                ">>>=" => nameof(RuntimeHelpers.UnsignedRightShift),
                "&&=" => throw new NotImplementedException("Logical assignment not yet implemented"),
                "||=" => throw new NotImplementedException("Logical assignment not yet implemented"),
                "??=" => throw new NotImplementedException("Logical assignment not yet implemented"),
                _ => throw new JsSyntaxError($"Unknown assignment operator: {op}")
            };
            value = Expr.Call(
                typeof(RuntimeHelpers).GetMethod(helperMethod, [typeof(JsValue), typeof(JsValue)])!,
                current,
                EnsureJsValue(rightVal));
        }

        var ensured = EnsureJsValue(value);
        var resultVar = Expr.Parameter(typeof(JsValue), "assignResult");

        return Expr.Block(typeof(JsValue), [resultVar],
            Expr.Assign(resultVar, ensured),
            CallEnvMethod("SetBinding", Expr.Constant(name), resultVar),
            resultVar);
    }

    private Expr CompileMemberAssignment(MemberExpression me, string op, SyntaxNode right)
    {
        var obj = CompileNode(me.Object);
        var objVar = Expr.Parameter(typeof(JsValue), "assignObj");

        var exprs = new List<Expr> { Expr.Assign(objVar, EnsureJsValue(obj)) };

        if (me.Computed)
        {
            var key = CompileNode(me.Property);
            var keyVar = Expr.Parameter(typeof(JsValue), "assignKey");
            exprs.Add(Expr.Assign(keyVar, EnsureJsValue(key)));

            Expr value;
            if (op == "=")
            {
                value = CompileNode(right);
            }
            else
            {
                var current = Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetComputedMember))!,
                    objVar, keyVar);
                var rightVal = CompileNode(right);
                string helperMethod = GetCompoundAssignmentHelper(op);
                value = Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(helperMethod, [typeof(JsValue), typeof(JsValue)])!,
                    current, EnsureJsValue(rightVal));
            }

            var resultVar = Expr.Parameter(typeof(JsValue), "assignResult");
            exprs.Add(Expr.Assign(resultVar, EnsureJsValue(value)));
            exprs.Add(Expr.Call(
                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.SetComputedMember))!,
                objVar, keyVar, resultVar));
            exprs.Add(resultVar);

            return Expr.Block(typeof(JsValue), [objVar, keyVar, resultVar], exprs);
        }
        else
        {
            var name = ((Identifier)me.Property).Name;

            Expr value;
            if (op == "=")
            {
                value = CompileNode(right);
            }
            else
            {
                var current = Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetMember))!,
                    objVar, Expr.Constant(name));
                var rightVal = CompileNode(right);
                string helperMethod = GetCompoundAssignmentHelper(op);
                value = Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(helperMethod, [typeof(JsValue), typeof(JsValue)])!,
                    current, EnsureJsValue(rightVal));
            }

            var resultVar = Expr.Parameter(typeof(JsValue), "assignResult");
            exprs.Add(Expr.Assign(resultVar, EnsureJsValue(value)));
            exprs.Add(Expr.Call(
                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.SetMember))!,
                objVar, Expr.Constant(name), resultVar));
            exprs.Add(resultVar);

            return Expr.Block(typeof(JsValue), [objVar, resultVar], exprs);
        }
    }

    private Expr CompileConditional(ConditionalExpression node)
    {
        return Expr.Condition(
            CompileToBool(CompileNode(node.Test)),
            EnsureJsValue(CompileNode(node.Consequent)),
            EnsureJsValue(CompileNode(node.Alternate)),
            typeof(JsValue));
    }

    private Expr CompileCall(CallExpression node)
    {
        // Collect arguments (handling spread)
        var argsExpr = CompileCallArguments(node.Arguments);

        // Handle method calls: obj.method(args) => method.Call(obj, args)
        if (node.Callee is MemberExpression me)
        {
            var obj = CompileNode(me.Object);
            var objVar = Expr.Parameter(typeof(JsValue), "callObj");

            Expr callee;
            if (me.Computed)
            {
                callee = Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetComputedMember))!,
                    objVar,
                    EnsureJsValue(CompileNode(me.Property)));
            }
            else
            {
                callee = Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetMember))!,
                    objVar,
                    Expr.Constant(((Identifier)me.Property).Name));
            }

            return Expr.Block(typeof(JsValue), [objVar],
                Expr.Assign(objVar, EnsureJsValue(obj)),
                Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CallFunction))!,
                    EnsureJsValue(callee),
                    objVar,
                    argsExpr));
        }

        // Regular function call
        var calleeExpr = CompileNode(node.Callee);
        return Expr.Call(
            typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CallFunction))!,
            EnsureJsValue(calleeExpr),
            Expr.Constant(JsValue.Undefined, typeof(JsValue)),
            argsExpr);
    }

    private Expr CompileNew(NewExpression node)
    {
        var callee = CompileNode(node.Callee);
        var args = CompileCallArguments(node.Arguments);

        return Expr.Call(
            typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.NewCall))!,
            EnsureJsValue(callee),
            args);
    }

    private Expr CompileMember(MemberExpression node)
    {
        var obj = CompileNode(node.Object);

        if (node.Computed)
        {
            return Expr.Call(
                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetComputedMember))!,
                EnsureJsValue(obj),
                EnsureJsValue(CompileNode(node.Property)));
        }

        var name = ((Identifier)node.Property).Name;
        return Expr.Call(
            typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetMember))!,
            EnsureJsValue(obj),
            Expr.Constant(name));
    }

    private Expr CompileObject(ObjectExpression node)
    {
        var objVar = Expr.Parameter(typeof(JsObject), "newObj");
        var exprs = new List<Expr>
        {
            Expr.Assign(objVar, Expr.New(typeof(JsObject)))
        };

        // Set prototype from realm
        exprs.Add(Expr.Assign(
            Expr.Property(objVar, nameof(JsObject.Prototype)),
            Expr.Constant(_realm.ObjectPrototype, typeof(JsObject))));

        foreach (var prop in node.Properties)
        {
            if (prop is SpreadElement spread)
            {
                // Object spread: {...source}
                exprs.Add(Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.ObjectSpread))!,
                    objVar,
                    EnsureJsValue(CompileNode(spread.Argument))));
                continue;
            }

            if (prop is not Property p)
            {
                continue;
            }

            Expr keyExpr;
            if (p.Computed)
            {
                keyExpr = Expr.Call(
                    EnsureJsValue(CompileNode(p.Key)),
                    typeof(JsValue).GetMethod(nameof(JsValue.ToJsString))!);
            }
            else if (p.Key is Identifier keyId)
            {
                keyExpr = Expr.Constant(keyId.Name);
            }
            else if (p.Key is Literal keyLit)
            {
                keyExpr = Expr.Constant(Convert.ToString(keyLit.Value, CultureInfo.InvariantCulture) ?? "");
            }
            else
            {
                continue;
            }

            if (p.Kind == PropertyKind.Get || p.Kind == PropertyKind.Set)
            {
                // Getter/setter
                var fnExpr = CompileNode(p.Value);
                if (p.Kind == PropertyKind.Get)
                {
                    exprs.Add(Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.DefineGetter))!,
                        objVar, keyExpr, EnsureJsValue(fnExpr)));
                }
                else
                {
                    exprs.Add(Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.DefineSetter))!,
                        objVar, keyExpr, EnsureJsValue(fnExpr)));
                }
            }
            else
            {
                // Normal or shorthand property
                var value = CompileNode(p.Value);
                exprs.Add(Expr.Call(
                    Expr.Convert(objVar, typeof(JsObject)),
                    typeof(JsObject).GetMethod(nameof(JsObject.Set), [typeof(string), typeof(JsValue)])!,
                    keyExpr,
                    EnsureJsValue(value)));
            }
        }

        exprs.Add(Expr.Convert(objVar, typeof(JsValue)));
        return Expr.Block(typeof(JsValue), [objVar], exprs);
    }

    private Expr CompileArray(ArrayExpression node)
    {
        var arrVar = Expr.Parameter(typeof(JsArray), "newArr");
        var exprs = new List<Expr>
        {
            Expr.Assign(arrVar, Expr.New(typeof(JsArray)))
        };

        // Set prototype from realm
        exprs.Add(Expr.Assign(
            Expr.Property(arrVar, nameof(JsObject.Prototype)),
            Expr.Constant(_realm.ArrayPrototype, typeof(JsObject))));

        foreach (var element in node.Elements)
        {
            if (element is null)
            {
                // Elision: push undefined
                exprs.Add(Expr.Call(arrVar,
                    typeof(JsArray).GetMethod(nameof(JsArray.Push))!,
                    Expr.Constant(JsValue.Undefined, typeof(JsValue))));
            }
            else if (element is SpreadElement spread)
            {
                // Spread: iterate and push each element
                exprs.Add(Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.ArraySpread))!,
                    arrVar,
                    EnsureJsValue(CompileNode(spread.Argument))));
            }
            else
            {
                exprs.Add(Expr.Call(arrVar,
                    typeof(JsArray).GetMethod(nameof(JsArray.Push))!,
                    EnsureJsValue(CompileNode(element))));
            }
        }

        exprs.Add(Expr.Convert(arrVar, typeof(JsValue)));
        return Expr.Block(typeof(JsValue), [arrVar], exprs);
    }

    private Expr CompileArrow(ArrowFunctionExpression node)
    {
        // Arrow functions capture the current 'this'
        return CompileFunctionBody(
            name: "",
            parameters: node.Params,
            body: node.Body,
            isExpression: node.IsExpression,
            isArrow: true,
            isAsync: node.IsAsync);
    }

    private Expr CompileFunctionExpr(FunctionExpression node)
    {
        return CompileFunctionBody(
            name: node.Id?.Name ?? "",
            parameters: node.Params,
            body: node.Body,
            isExpression: false,
            isArrow: false,
            isGenerator: node.IsGenerator,
            isAsync: node.IsAsync);
    }

    private Expr CompileFunctionDecl(FunctionDeclaration node)
    {
        // Function declarations are hoisted in HoistFunctions
        return Expr.Constant(JsValue.Undefined, typeof(JsValue));
    }

    private Expr CompileSpread(SpreadElement node)
    {
        // Spread is handled by the parent context (array, call args, etc.)
        // If we reach here, just evaluate the argument
        return CompileNode(node.Argument);
    }

    private Expr CompileTemplateLiteral(TemplateLiteral node)
    {
        if (node.Expressions.Count == 0 && node.Quasis.Count == 1)
        {
            // Simple template with no expressions
            return Expr.Convert(
                Expr.New(
                    typeof(JsString).GetConstructor([typeof(string)])!,
                    Expr.Constant(node.Quasis[0].Value)),
                typeof(JsValue));
        }

        // Build concatenation: quasi0 + expr0 + quasi1 + expr1 + ...
        var parts = new List<Expr>();
        for (int i = 0; i < node.Quasis.Count; i++)
        {
            parts.Add(Expr.Constant(node.Quasis[i].Value));

            if (i < node.Expressions.Count)
            {
                parts.Add(Expr.Call(
                    EnsureJsValue(CompileNode(node.Expressions[i])),
                    typeof(JsValue).GetMethod(nameof(JsValue.ToJsString))!));
            }
        }

        // Concatenate all strings
        var concatMethod = typeof(string).GetMethod(nameof(string.Concat), [typeof(string[])]);
        var strArray = Expr.NewArrayInit(typeof(string), parts);
        var concatResult = Expr.Call(concatMethod!, strArray);

        return Expr.Convert(
            Expr.New(typeof(JsString).GetConstructor([typeof(string)])!, concatResult),
            typeof(JsValue));
    }

    private Expr CompileTaggedTemplate(TaggedTemplateExpression node)
    {
        // Compile the tag function
        var tag = CompileNode(node.Tag);

        // Build the template strings array
        var quasisExprs = node.Quasi.Quasis.Select(q =>
            (Expr)Expr.Convert(
                Expr.New(typeof(JsString).GetConstructor([typeof(string)])!, Expr.Constant(q.Value)),
                typeof(JsValue)))
            .ToArray();

        var stringsArray = Expr.NewArrayInit(typeof(JsValue), quasisExprs);
        var stringsJsArray = Expr.New(
            typeof(JsArray).GetConstructor([typeof(IEnumerable<JsValue>)])!,
            Expr.Convert(stringsArray, typeof(IEnumerable<JsValue>)));

        // Build substitution values
        var subsExprs = node.Quasi.Expressions
            .Select(e => EnsureJsValue(CompileNode(e)))
            .ToArray();

        // Build the final args array: [strings, ...substitutions]
        var allArgs = new List<Expr> { Expr.Convert(stringsJsArray, typeof(JsValue)) };
        allArgs.AddRange(subsExprs);
        var argsArray = Expr.NewArrayInit(typeof(JsValue), allArgs);

        return Expr.Call(
            typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CallFunction))!,
            EnsureJsValue(tag),
            Expr.Constant(JsValue.Undefined, typeof(JsValue)),
            argsArray);
    }

    private Expr CompileSequence(SequenceExpression node)
    {
        var exprs = node.Expressions.Select(CompileNode).ToList();
        if (exprs.Count == 0)
        {
            return Expr.Constant(JsValue.Undefined, typeof(JsValue));
        }

        // Ensure last expression returns JsValue
        exprs[^1] = EnsureJsValue(exprs[^1]);

        // All but last should be void
        var allExprs = new List<Expr>();
        for (int i = 0; i < exprs.Count - 1; i++)
        {
            allExprs.Add(ToVoid(exprs[i]));
        }

        allExprs.Add(exprs[^1]);
        return Expr.Block(typeof(JsValue), allExprs);
    }

    private Expr CompileChain(ChainExpression node)
    {
        // Optional chaining: compile the inner expression with null-checking
        return CompileOptionalChain(node.Expression);
    }

    private Expr CompileOptionalChain(SyntaxNode node)
    {
        if (node is MemberExpression me)
        {
            var obj = me.Object is MemberExpression { Optional: true } or CallExpression
                ? CompileOptionalChain(me.Object)
                : CompileNode(me.Object);

            var objVar = Expr.Parameter(typeof(JsValue), "chainObj");

            Expr memberAccess;
            if (me.Computed)
            {
                memberAccess = Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetComputedMember))!,
                    objVar,
                    EnsureJsValue(CompileNode(me.Property)));
            }
            else
            {
                memberAccess = Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetMember))!,
                    objVar,
                    Expr.Constant(((Identifier)me.Property).Name));
            }

            if (me.Optional)
            {
                return Expr.Block(typeof(JsValue), [objVar],
                    Expr.Assign(objVar, EnsureJsValue(obj)),
                    Expr.Condition(
                        Expr.OrElse(
                            Expr.TypeIs(objVar, typeof(JsNull)),
                            Expr.TypeIs(objVar, typeof(JsUndefined))),
                        Expr.Constant(JsValue.Undefined, typeof(JsValue)),
                        memberAccess,
                        typeof(JsValue)));
            }

            return Expr.Block(typeof(JsValue), [objVar],
                Expr.Assign(objVar, EnsureJsValue(obj)),
                memberAccess);
        }

        if (node is CallExpression call)
        {
            var callee = CompileOptionalChain(call.Callee);
            var calleeVar = Expr.Parameter(typeof(JsValue), "chainCallee");
            var args = CompileCallArguments(call.Arguments);

            var callExpr = Expr.Call(
                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CallFunction))!,
                calleeVar,
                Expr.Constant(JsValue.Undefined, typeof(JsValue)),
                args);

            // Check if callee is null/undefined
            return Expr.Block(typeof(JsValue), [calleeVar],
                Expr.Assign(calleeVar, EnsureJsValue(callee)),
                Expr.Condition(
                    Expr.OrElse(
                        Expr.TypeIs(calleeVar, typeof(JsNull)),
                        Expr.TypeIs(calleeVar, typeof(JsUndefined))),
                    Expr.Constant(JsValue.Undefined, typeof(JsValue)),
                    callExpr,
                    typeof(JsValue)));
        }

        return CompileNode(node);
    }

    private Expr CompileDelete(SyntaxNode target)
    {
        if (target is MemberExpression me)
        {
            var obj = CompileNode(me.Object);

            if (me.Computed)
            {
                return Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.DeleteComputedMember))!,
                    EnsureJsValue(obj),
                    EnsureJsValue(CompileNode(me.Property)));
            }

            var name = ((Identifier)me.Property).Name;
            return Expr.Call(
                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.DeleteMember))!,
                EnsureJsValue(obj),
                Expr.Constant(name));
        }

        // delete of a non-member expression always returns true
        return Expr.Block(typeof(JsValue),
            ToVoid(CompileNode(target)),
            Expr.Constant(JsValue.True, typeof(JsValue)));
    }

}
