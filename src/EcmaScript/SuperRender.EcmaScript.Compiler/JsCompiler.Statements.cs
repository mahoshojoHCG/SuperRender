// CA1859: These private Compile* methods intentionally return the abstract Expr type
// for a uniform internal API — the caller (CompileNode) dispatches via pattern matching
// and always works with Expr, so narrowing return types would add no value.
// CA1822: Private Compile* methods are part of the compiler instance API even when a
// particular method does not currently access instance state (e.g. stubs for hoisted decls).
#pragma warning disable CA1859, CA1822

using SuperRender.EcmaScript.Compiler.Ast;
using SuperRender.EcmaScript.Runtime.Errors;
using SuperRender.EcmaScript.Runtime;
using Environment = SuperRender.EcmaScript.Runtime.Environment;
using Expr = System.Linq.Expressions.Expression;

namespace SuperRender.EcmaScript.Compiler;

public sealed partial class JsCompiler
{
    // ───────────────────────── Statements ─────────────────────────

    private Expr CompileBlock(BlockStatement block)
    {
        // Block creates a new lexical scope
        var outerEnv = _envParam;
        var innerEnv = Expr.Parameter(typeof(Environment), "blockEnv");

        var exprs = new List<Expr>
        {
            Expr.Assign(innerEnv, Expr.New(
                typeof(Environment).GetConstructor([typeof(Environment)])!,
                outerEnv))
        };

        _envParam = innerEnv;

        try
        {
            HoistFunctions(block.Body, exprs);

            foreach (var stmt in block.Body)
            {
                if (stmt is FunctionDeclaration)
                {
                    continue;
                }

                // Emit location tracking
                if (stmt.Location is not null)
                {
                    exprs.Add(Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.SetLocation))!,
                        Expr.Constant(stmt.Location.Line),
                        Expr.Constant(stmt.Location.Column)));
                }

                exprs.Add(CompileNode(stmt));
            }

            if (exprs.Count == 1 || !typeof(JsValue).IsAssignableFrom(exprs[^1].Type))
            {
                exprs.Add(Expr.Constant(JsValue.Undefined, typeof(JsValue)));
            }

            return Expr.Block(typeof(JsValue), [innerEnv], exprs);
        }
        finally
        {
            _envParam = outerEnv;
        }
    }

    private Expr CompileIf(IfStatement node)
    {
        var test = CompileToBool(CompileNode(node.Test));

        if (node.Alternate is not null)
        {
            return Expr.Condition(
                test,
                EnsureJsValue(CompileNode(node.Consequent)),
                EnsureJsValue(CompileNode(node.Alternate)),
                typeof(JsValue));
        }

        return Expr.Condition(
            test,
            EnsureJsValue(CompileNode(node.Consequent)),
            Expr.Constant(JsValue.Undefined, typeof(JsValue)),
            typeof(JsValue));
    }

    private Expr CompileFor(ForStatement node)
    {
        var outerEnv = _envParam;
        var loopEnv = Expr.Parameter(typeof(Environment), "forEnv");
        var prevBreak = _breakLabel;
        var prevContinue = _continueLabel;
        var breakLbl = _pendingLabelBreak ?? Expr.Label(typeof(JsValue), "forBreak");
        var continueLbl = _pendingLabelContinue ?? Expr.Label("forContinue");
        _pendingLabelBreak = null;
        _pendingLabelContinue = null;
        _breakLabel = breakLbl;
        _continueLabel = continueLbl;

        var exprs = new List<Expr>
        {
            Expr.Assign(loopEnv, Expr.New(
                typeof(Environment).GetConstructor([typeof(Environment)])!,
                outerEnv))
        };

        _envParam = loopEnv;

        try
        {
            // Init
            if (node.Init is not null)
            {
                exprs.Add(CompileNode(node.Init));
            }

            // Build loop body
            var bodyExprs = new List<Expr>();

            // Test
            if (node.Test is not null)
            {
                var test = CompileToBool(CompileNode(node.Test));
                bodyExprs.Add(Expr.IfThen(
                    Expr.Not(test),
                    Expr.Break(breakLbl, Expr.Constant(JsValue.Undefined, typeof(JsValue)))));
            }

            bodyExprs.Add(CompileNode(node.Body));

            // Continue label goes before update
            bodyExprs.Add(Expr.Label(continueLbl));

            // Update
            if (node.Update is not null)
            {
                bodyExprs.Add(CompileNode(node.Update));
            }

            var loopBody = Expr.Block(typeof(void), bodyExprs.Select(ToVoid));

            exprs.Add(Expr.Loop(loopBody, breakLbl));

            return Expr.Block(typeof(JsValue), [loopEnv], exprs);
        }
        finally
        {
            _envParam = outerEnv;
            _breakLabel = prevBreak;
            _continueLabel = prevContinue;
        }
    }

    private Expr CompileForIn(ForInStatement node)
    {
        return CompileForInOf(node.Left, node.Right, node.Body, isForOf: false);
    }

    private Expr CompileForOf(ForOfStatement node)
    {
        // TODO: for-await-of — When node.IsAwait is true, call RuntimeHelpers.GetAsyncIterator()
        // which checks Symbol.asyncIterator first, then falls back to Symbol.iterator.
        // After each IteratorNext, if the result is a JsPromiseObject, await it via coroutine yield.
        // Deferred: requires async iterator protocol and coroutine integration.
        return CompileForOfIterator(node.Left, node.Right, node.Body);
    }

    private Expr CompileForOfIterator(SyntaxNode left, SyntaxNode right, SyntaxNode body)
    {
        var prevBreak = _breakLabel;
        var prevContinue = _continueLabel;
        var breakLbl = _pendingLabelBreak ?? Expr.Label(typeof(JsValue), "forOfBreak");
        var continueLbl = _pendingLabelContinue ?? Expr.Label("forOfContinue");
        _pendingLabelBreak = null;
        _pendingLabelContinue = null;
        _breakLabel = breakLbl;
        _continueLabel = continueLbl;

        try
        {
            var objExpr = CompileNode(right);
            var iterVar = Expr.Parameter(typeof(JsValue), "iter");
            var resultVar = Expr.Parameter(typeof(JsValue), "iterResult");

            var exprs = new List<Expr>
            {
                Expr.Assign(iterVar, Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetIterator))!,
                    EnsureJsValue(objExpr)))
            };

            var bodyExprs = new List<Expr>
            {
                // Call iterator.next() and check done
                Expr.Assign(resultVar, Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.IteratorNext))!,
                    iterVar)),

                Expr.IfThen(
                    Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.IteratorComplete))!,
                        resultVar),
                    Expr.Break(breakLbl, Expr.Constant(JsValue.Undefined, typeof(JsValue))))
            };

            var currentValue = Expr.Call(
                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.IteratorValue))!,
                resultVar);

            bodyExprs.Add(CompileForInOfAssignment(left, currentValue));
            bodyExprs.Add(CompileNode(body));
            bodyExprs.Add(Expr.Label(continueLbl));

            var loopBody = Expr.Block(typeof(void), bodyExprs.Select(ToVoid));
            exprs.Add(Expr.Loop(loopBody, breakLbl));

            return Expr.Block(typeof(JsValue), [iterVar, resultVar], exprs);
        }
        finally
        {
            _breakLabel = prevBreak;
            _continueLabel = prevContinue;
        }
    }

    private Expr CompileForInOf(SyntaxNode left, SyntaxNode right, SyntaxNode body, bool isForOf)
    {
        var prevBreak = _breakLabel;
        var prevContinue = _continueLabel;
        var breakLbl = _pendingLabelBreak ?? Expr.Label(typeof(JsValue), "forInOfBreak");
        var continueLbl = _pendingLabelContinue ?? Expr.Label("forInOfContinue");
        _pendingLabelBreak = null;
        _pendingLabelContinue = null;
        _breakLabel = breakLbl;
        _continueLabel = continueLbl;

        try
        {
            // Evaluate the right-hand side once and cache it
            var objExpr = CompileNode(right);
            var objVar = Expr.Parameter(typeof(JsValue), "forOfObj");
            var keysVar = Expr.Parameter(typeof(string[]), "keys");
            var indexVar = Expr.Parameter(typeof(int), "idx");

            Expr getKeys;
            if (isForOf)
            {
                // For-of: use RuntimeHelpers.GetIterableKeys
                getKeys = Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetForOfValues))!,
                    objVar);
            }
            else
            {
                // For-in: get enumerable property keys
                getKeys = Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetForInKeys))!,
                    objVar);
            }

            var exprs = new List<Expr>
            {
                Expr.Assign(objVar, EnsureJsValue(objExpr)),
                Expr.Assign(keysVar, getKeys),
                Expr.Assign(indexVar, Expr.Constant(0))
            };

            // Loop body
            var bodyExprs = new List<Expr>();

            // Test: idx < keys.Length
            bodyExprs.Add(Expr.IfThen(
                Expr.GreaterThanOrEqual(indexVar, Expr.ArrayLength(keysVar)),
                Expr.Break(breakLbl, Expr.Constant(JsValue.Undefined, typeof(JsValue)))));

            // Assign current key to the loop variable
            var currentKey = Expr.ArrayIndex(keysVar, indexVar);
            Expr keyAsJsValue;
            if (isForOf)
            {
                // For-of values are already stored as stringified JsValues in the helper
                keyAsJsValue = Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetForOfValueAt))!,
                    objVar,
                    indexVar);
            }
            else
            {
                keyAsJsValue = Expr.New(
                    typeof(JsString).GetConstructor([typeof(string)])!,
                    currentKey);
            }

            bodyExprs.Add(CompileForInOfAssignment(left, keyAsJsValue));

            bodyExprs.Add(CompileNode(body));
            bodyExprs.Add(Expr.Label(continueLbl));
            bodyExprs.Add(Expr.PostIncrementAssign(indexVar));

            var loopBody = Expr.Block(typeof(void), bodyExprs.Select(ToVoid));
            exprs.Add(Expr.Loop(loopBody, breakLbl));

            return Expr.Block(typeof(JsValue), [objVar, keysVar, indexVar], exprs);
        }
        finally
        {
            _breakLabel = prevBreak;
            _continueLabel = prevContinue;
        }
    }

    private Expr CompileForInOfAssignment(SyntaxNode left, Expr value)
    {
        if (left is VariableDeclaration vd && vd.Declarations.Count > 0)
        {
            var declarator = vd.Declarations[0];
            bool mutable = vd.Kind != VariableKind.Const;

            if (declarator.Id is Identifier id)
            {
                return Expr.Block(
                    typeof(JsValue),
                    CallEnvMethod("CreateAndInitializeBinding",
                        Expr.Constant(id.Name),
                        Expr.Constant(mutable),
                        EnsureJsValue(value)),
                    EnsureJsValue(value));
            }

            return CompileBindingPattern(declarator.Id, EnsureJsValue(value), mutable);
        }

        if (left is Identifier leftId)
        {
            return Expr.Block(
                typeof(JsValue),
                CallEnvMethod("SetBinding", Expr.Constant(leftId.Name), EnsureJsValue(value)),
                EnsureJsValue(value));
        }

        return EnsureJsValue(value);
    }

    private Expr CompileWhile(WhileStatement node)
    {
        var prevBreak = _breakLabel;
        var prevContinue = _continueLabel;
        var breakLbl = _pendingLabelBreak ?? Expr.Label(typeof(JsValue), "whileBreak");
        var continueLbl = _pendingLabelContinue ?? Expr.Label("whileContinue");
        _pendingLabelBreak = null;
        _pendingLabelContinue = null;
        _breakLabel = breakLbl;
        _continueLabel = continueLbl;

        try
        {
            var bodyExprs = new List<Expr>
            {
                Expr.IfThen(
                    Expr.Not(CompileToBool(CompileNode(node.Test))),
                    Expr.Break(breakLbl, Expr.Constant(JsValue.Undefined, typeof(JsValue)))),
                CompileNode(node.Body),
                Expr.Label(continueLbl)
            };

            var loopBody = Expr.Block(typeof(void), bodyExprs.Select(ToVoid));
            return Expr.Loop(loopBody, breakLbl);
        }
        finally
        {
            _breakLabel = prevBreak;
            _continueLabel = prevContinue;
        }
    }

    private Expr CompileDoWhile(DoWhileStatement node)
    {
        var prevBreak = _breakLabel;
        var prevContinue = _continueLabel;
        var breakLbl = _pendingLabelBreak ?? Expr.Label(typeof(JsValue), "doWhileBreak");
        var continueLbl = _pendingLabelContinue ?? Expr.Label("doWhileContinue");
        _pendingLabelBreak = null;
        _pendingLabelContinue = null;
        _breakLabel = breakLbl;
        _continueLabel = continueLbl;

        try
        {
            var bodyExprs = new List<Expr>
            {
                CompileNode(node.Body),
                Expr.Label(continueLbl),
                Expr.IfThen(
                    Expr.Not(CompileToBool(CompileNode(node.Test))),
                    Expr.Break(breakLbl, Expr.Constant(JsValue.Undefined, typeof(JsValue))))
            };

            var loopBody = Expr.Block(typeof(void), bodyExprs.Select(ToVoid));
            return Expr.Loop(loopBody, breakLbl);
        }
        finally
        {
            _breakLabel = prevBreak;
            _continueLabel = prevContinue;
        }
    }

    private Expr CompileSwitch(SwitchStatement node)
    {
        var prevBreak = _breakLabel;
        var breakLbl = Expr.Label(typeof(JsValue), "switchBreak");
        _breakLabel = breakLbl;

        try
        {
            var discriminant = CompileNode(node.Discriminant);
            var discVar = Expr.Parameter(typeof(JsValue), "disc");
            var matchedVar = Expr.Parameter(typeof(bool), "matched");

            var exprs = new List<Expr>
            {
                Expr.Assign(discVar, EnsureJsValue(discriminant)),
                Expr.Assign(matchedVar, Expr.Constant(false))
            };

            foreach (var @case in node.Cases)
            {
                var caseBody = new List<Expr>();

                if (@case.Test is not null)
                {
                    // Non-default case: check for match
                    var testVal = CompileNode(@case.Test);
                    var isMatch = Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.StrictEqualBool))!,
                        discVar,
                        EnsureJsValue(testVal));

                    caseBody.Add(Expr.IfThen(isMatch, Expr.Assign(matchedVar, Expr.Constant(true))));
                }
                else
                {
                    // Default case: always fall through
                    caseBody.Add(Expr.Assign(matchedVar, Expr.Constant(true)));
                }

                // Execute consequent only if matched
                var consequent = new List<Expr>();
                foreach (var stmt in @case.Consequent)
                {
                    consequent.Add(CompileNode(stmt));
                }

                if (consequent.Count > 0)
                {
                    caseBody.Add(Expr.IfThen(
                        matchedVar,
                        Expr.Block(typeof(void), consequent.Select(ToVoid))));
                }

                exprs.Add(Expr.Block(typeof(void), caseBody.Select(ToVoid)));
            }

            exprs.Add(Expr.Label(breakLbl, Expr.Constant(JsValue.Undefined, typeof(JsValue))));

            return Expr.Block(typeof(JsValue), [discVar, matchedVar], exprs);
        }
        finally
        {
            _breakLabel = prevBreak;
        }
    }

    private Expr CompileTry(TryStatement node)
    {
        var resultVar = Expr.Parameter(typeof(JsValue), "tryResult");
        var body = Expr.Assign(resultVar, EnsureJsValue(CompileBlock(node.Block)));

        System.Linq.Expressions.CatchBlock? catchBlock = null;
        if (node.Handler is not null)
        {
            var exParam = Expr.Parameter(typeof(Exception), "ex");

            var catchExprs = new List<Expr>();

            // Re-throw GeneratorReturnException so generator.return() bypasses catch blocks
            catchExprs.Add(Expr.IfThen(
                Expr.TypeIs(exParam, typeof(GeneratorReturnException)),
                Expr.Rethrow()));

            if (node.Handler.Param is not null)
            {
                // Create a new environment for the catch block
                var catchEnv = Expr.Parameter(typeof(Environment), "catchEnv");
                var outerEnv = _envParam;

                catchExprs.Add(Expr.Assign(catchEnv, Expr.New(
                    typeof(Environment).GetConstructor([typeof(Environment)])!,
                    outerEnv)));

                // Bind the exception to the catch parameter
                var errorValue = Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.ExceptionToJsValue))!,
                    exParam);

                _envParam = catchEnv;

                if (node.Handler.Param is Identifier catchId)
                {
                    catchExprs.Add(Expr.Call(
                        catchEnv,
                        typeof(Environment).GetMethod(nameof(Environment.CreateAndInitializeBinding))!,
                        Expr.Constant(catchId.Name),
                        Expr.Constant(true),
                        errorValue));
                }

                catchExprs.Add(Expr.Assign(resultVar, EnsureJsValue(CompileBlock(node.Handler.Body))));
                _envParam = outerEnv;

                catchBlock = Expr.Catch(
                    exParam,
                    Expr.Block(typeof(JsValue), [catchEnv], catchExprs));
            }
            else
            {
                catchExprs.Add(Expr.Assign(resultVar, EnsureJsValue(CompileBlock(node.Handler.Body))));
                catchBlock = Expr.Catch(
                    exParam,
                    Expr.Block(typeof(JsValue), catchExprs));
            }
        }

        Expr tryExpr;
        if (catchBlock is not null)
        {
            tryExpr = Expr.TryCatch(body, catchBlock);
        }
        else
        {
            tryExpr = body;
        }

        if (node.Finalizer is not null)
        {
            var finallyBody = CompileBlock(node.Finalizer);
            tryExpr = Expr.TryFinally(
                Expr.Block(typeof(JsValue), tryExpr, resultVar),
                ToVoid(finallyBody));
        }

        return Expr.Block(typeof(JsValue), [resultVar], [tryExpr, resultVar]);
    }

    private Expr CompileReturn(ReturnStatement node)
    {
        if (_returnLabel is null)
        {
            throw new JsSyntaxError("Illegal return statement");
        }

        var value = node.Argument is not null
            ? CompileNode(node.Argument)
            : Expr.Constant(JsValue.Undefined, typeof(JsValue));

        return Expr.Return(_returnLabel, EnsureJsValue(value), typeof(JsValue));
    }

    private Expr CompileThrow(ThrowStatement node)
    {
        var value = CompileNode(node.Argument);
        return Expr.Throw(
            Expr.Call(
                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CreateException))!,
                EnsureJsValue(value)),
            typeof(JsValue));
    }

    private Expr CompileBreak(BreakStatement node)
    {
        if (node.Label is not null)
        {
            if (!_labelTargets.TryGetValue(node.Label, out var targets))
            {
                throw new JsSyntaxError($"Undefined label '{node.Label}'");
            }

            return Expr.Break(targets.breakTarget, Expr.Constant(JsValue.Undefined, typeof(JsValue)));
        }

        if (_breakLabel is null)
        {
            throw new JsSyntaxError("Illegal break statement");
        }

        return Expr.Break(_breakLabel, Expr.Constant(JsValue.Undefined, typeof(JsValue)));
    }

    private Expr CompileContinue(ContinueStatement node)
    {
        if (node.Label is not null)
        {
            if (!_labelTargets.TryGetValue(node.Label, out var targets))
            {
                throw new JsSyntaxError($"Undefined label '{node.Label}'");
            }

            if (targets.continueTarget is null)
            {
                throw new JsSyntaxError($"Illegal continue statement: label '{node.Label}' is not on a loop");
            }

            return Expr.Continue(targets.continueTarget);
        }

        if (_continueLabel is null)
        {
            throw new JsSyntaxError("Illegal continue statement");
        }

        return Expr.Continue(_continueLabel);
    }

    private static bool IsLoopStatement(SyntaxNode node) => node is
        ForStatement or WhileStatement or DoWhileStatement or ForInStatement or ForOfStatement;

    private Expr CompileLabeledStatement(LabeledStatement node)
    {
        var breakLbl = Expr.Label(typeof(JsValue), $"label_{node.Label}_break");

        if (IsLoopStatement(node.Body))
        {
            // For labeled loops, create both break and continue targets.
            // The loop compiler will pick these up via _pendingLabelBreak/_pendingLabelContinue
            // and use them as its own targets, so break/continue label jump correctly.
            var continueLbl = Expr.Label($"label_{node.Label}_continue");
            _labelTargets[node.Label] = (breakLbl, continueLbl);
            _pendingLabelBreak = breakLbl;
            _pendingLabelContinue = continueLbl;

            try
            {
                var body = CompileNode(node.Body);
                return body;
            }
            finally
            {
                _labelTargets.Remove(node.Label);
                _pendingLabelBreak = null;
                _pendingLabelContinue = null;
            }
        }
        else
        {
            // For non-loop labeled statements (e.g. blocks), only break is valid.
            // Wrap the body so that break label jumps past it.
            _labelTargets[node.Label] = (breakLbl, null);

            try
            {
                var body = CompileNode(node.Body);
                return Expr.Block(typeof(JsValue),
                    EnsureJsValue(body),
                    Expr.Label(breakLbl, Expr.Constant(JsValue.Undefined, typeof(JsValue))));
            }
            finally
            {
                _labelTargets.Remove(node.Label);
            }
        }
    }

    private Expr CompileVariableDeclaration(VariableDeclaration node)
    {
        var exprs = new List<Expr>();
        bool mutable = node.Kind != VariableKind.Const;

        foreach (var decl in node.Declarations)
        {
            if (decl.Id is Identifier id)
            {
                if (decl.Init is not null)
                {
                    var value = CompileNode(decl.Init);
                    exprs.Add(CallEnvMethod("CreateAndInitializeBinding",
                        Expr.Constant(id.Name),
                        Expr.Constant(mutable),
                        EnsureJsValue(value)));
                }
                else
                {
                    // var/let without initializer
                    exprs.Add(CallEnvMethod("CreateAndInitializeBinding",
                        Expr.Constant(id.Name),
                        Expr.Constant(mutable),
                        Expr.Constant(JsValue.Undefined, typeof(JsValue))));
                }
            }
            else
            {
                // Destructuring pattern
                var init = decl.Init is not null
                    ? CompileNode(decl.Init)
                    : Expr.Constant(JsValue.Undefined, typeof(JsValue));
                exprs.Add(CompileBindingPattern(decl.Id, EnsureJsValue(init), mutable));
            }
        }

        if (exprs.Count == 0)
        {
            return Expr.Constant(JsValue.Undefined, typeof(JsValue));
        }

        exprs.Add(Expr.Constant(JsValue.Undefined, typeof(JsValue)));
        return Expr.Block(typeof(JsValue), exprs);
    }

}
