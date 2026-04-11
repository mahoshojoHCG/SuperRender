// CA1859: These private Compile* methods intentionally return the abstract Expr type
// for a uniform internal API — the caller (CompileNode) dispatches via pattern matching
// and always works with Expr, so narrowing return types would add no value.
// CA1822: Private Compile* methods are part of the compiler instance API even when a
// particular method does not currently access instance state (e.g. stubs for hoisted decls).
#pragma warning disable CA1859, CA1822

using System.Globalization;
using System.Runtime.CompilerServices;
using SuperRender.EcmaScript.Compiler.Ast;
using SuperRender.EcmaScript.Runtime.Builtins;
using SuperRender.EcmaScript.Runtime.Errors;
using SuperRender.EcmaScript.Runtime;
using Environment = SuperRender.EcmaScript.Runtime.Environment;
using Expr = System.Linq.Expressions.Expression;
using ParameterExpression = System.Linq.Expressions.ParameterExpression;
using LabelTarget = System.Linq.Expressions.LabelTarget;

namespace SuperRender.EcmaScript.Compiler;

/// <summary>
/// Compiles a JavaScript AST (<see cref="Program"/>) into a DLR expression tree
/// that can be executed as a <c>Func&lt;Environment, JsValue&gt;</c> delegate.
/// </summary>
public sealed class JsCompiler
{
    private readonly Realm _realm;

    // Current compilation state
    private ParameterExpression _envParam = null!;
    private ParameterExpression _thisParam = null!;
    private LabelTarget? _returnLabel;
    private LabelTarget? _breakLabel;
    private LabelTarget? _continueLabel;
    private ParameterExpression? _coroutineParam;

    // Label tracking for break/continue with named labels
    private readonly Dictionary<string, (LabelTarget breakTarget, LabelTarget? continueTarget)> _labelTargets = new();
    private LabelTarget? _pendingLabelBreak;
    private LabelTarget? _pendingLabelContinue;

    public JsCompiler(Realm realm)
    {
        _realm = realm;
    }

    // ───────────────────────── Public entry point ─────────────────────────

    /// <summary>
    /// Compiles a <see cref="Program"/> AST into an executable delegate.
    /// The delegate takes an <see cref="Environment"/> and returns the completion value.
    /// </summary>
    public Func<Environment, JsValue> Compile(Program program)
    {
        var envParam = Expr.Parameter(typeof(Environment), "env");
        var thisParam = Expr.Parameter(typeof(JsValue), "this");

        var prevEnv = _envParam;
        var prevThis = _thisParam;
        var prevReturn = _returnLabel;
        _envParam = envParam;
        _thisParam = thisParam;
        _returnLabel = null;

        try
        {
            var bodyExprs = new List<Expr>();

            // Hoist function declarations
            HoistFunctions(program.Body, bodyExprs);

            // Compile each statement
            foreach (var stmt in program.Body)
            {
                if (stmt is FunctionDeclaration)
                {
                    // Already hoisted
                    continue;
                }

                bodyExprs.Add(CompileNode(stmt));
            }

            // Ensure the block returns a JsValue
            if (bodyExprs.Count == 0 || !typeof(JsValue).IsAssignableFrom(bodyExprs[^1].Type))
            {
                bodyExprs.Add(Expr.Constant(JsValue.Undefined, typeof(JsValue)));
            }

            var body = Expr.Block(typeof(JsValue), bodyExprs);

            // The outer lambda captures thisParam; we bind it to the global object at call time
            var inner = Expr.Lambda<Func<Environment, JsValue, JsValue>>(body, envParam, thisParam);
            var compiled = inner.Compile();

            var globalObj = _realm.GlobalObject;
            return env => compiled(env, globalObj);
        }
        finally
        {
            _envParam = prevEnv;
            _thisParam = prevThis;
            _returnLabel = prevReturn;
        }
    }

    // ───────────────────────── Node dispatch ─────────────────────────

    private Expr CompileNode(SyntaxNode node) => node switch
    {
        // Statements
        BlockStatement block => CompileBlock(block),
        ExpressionStatement es => CompileNode(es.Expression),
        EmptyStatement => Expr.Constant(JsValue.Undefined, typeof(JsValue)),
        IfStatement ifs => CompileIf(ifs),
        ForStatement fs => CompileFor(fs),
        ForInStatement fi => CompileForIn(fi),
        ForOfStatement fo => CompileForOf(fo),
        WhileStatement ws => CompileWhile(ws),
        DoWhileStatement dw => CompileDoWhile(dw),
        SwitchStatement ss => CompileSwitch(ss),
        TryStatement ts => CompileTry(ts),
        ReturnStatement rs => CompileReturn(rs),
        ThrowStatement ths => CompileThrow(ths),
        BreakStatement bs => CompileBreak(bs),
        ContinueStatement cs => CompileContinue(cs),
        LabeledStatement ls => CompileLabeledStatement(ls),
        VariableDeclaration vd => CompileVariableDeclaration(vd),
        FunctionDeclaration fd => CompileFunctionDecl(fd),
        ClassDeclaration cd => CompileClassDecl(cd),

        // Expressions
        Identifier id => CompileIdentifier(id),
        Literal lit => CompileLiteral(lit),
        ThisExpression => _thisParam,
        BinaryExpression be => CompileBinary(be),
        LogicalExpression le => CompileLogical(le),
        UnaryExpression ue => CompileUnary(ue),
        UpdateExpression upd => CompileUpdate(upd),
        AssignmentExpression ae => CompileAssignment(ae),
        ConditionalExpression ce => CompileConditional(ce),
        CallExpression call => CompileCall(call),
        NewExpression ne => CompileNew(ne),
        MemberExpression me => CompileMember(me),
        ArrowFunctionExpression arrow => CompileArrow(arrow),
        FunctionExpression fe => CompileFunctionExpr(fe),
        ObjectExpression oe => CompileObject(oe),
        ArrayExpression arrExpr => CompileArray(arrExpr),
        SpreadElement se => CompileSpread(se),
        TemplateLiteral tl => CompileTemplateLiteral(tl),
        TaggedTemplateExpression tte => CompileTaggedTemplate(tte),
        SequenceExpression seq => CompileSequence(seq),
        ClassExpression clsExpr => CompileClassExpr(clsExpr),
        ChainExpression chain => CompileChain(chain),
        YieldExpression ye => CompileYield(ye),
        AwaitExpression ae => CompileAwait(ae),

        _ => throw new JsSyntaxError($"Unsupported AST node: {node.GetType().Name}")
    };

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

    // ───────────────────────── Classes ─────────────────────────

    private Expr CompileClassDecl(ClassDeclaration node)
    {
        var classExpr = CompileClassBody(node.Id?.Name, node.SuperClass, node.Body);

        if (node.Id is not null)
        {
            var resultVar = Expr.Parameter(typeof(JsValue), "classVal");
            return Expr.Block(typeof(JsValue), [resultVar],
                Expr.Assign(resultVar, classExpr),
                CallEnvMethod("CreateAndInitializeBinding",
                    Expr.Constant(node.Id.Name),
                    Expr.Constant(false),
                    resultVar),
                resultVar);
        }

        return classExpr;
    }

    private Expr CompileClassExpr(ClassExpression node)
    {
        return CompileClassBody(node.Id?.Name, node.SuperClass, node.Body);
    }

    private Expr CompileClassBody(string? className, SyntaxNode? superClass, ClassBody body)
    {
        var ctorVar = Expr.Parameter(typeof(JsFunction), "classCtor");
        var protoVar = Expr.Parameter(typeof(JsObject), "classProto");
        var exprs = new List<Expr>();

        // Find constructor method
        MethodDefinition? ctorDef = null;
        foreach (var member in body.Body)
        {
            if (member is MethodDefinition md && md.Kind == MethodKind.Constructor)
            {
                ctorDef = md;
                break;
            }
        }

        // Compile constructor function
        if (ctorDef is not null)
        {
            var ctorFn = CompileFunctionBody(
                name: className ?? "",
                parameters: ctorDef.Value.Params,
                body: ctorDef.Value.Body,
                isExpression: false,
                isArrow: false);

            exprs.Add(Expr.Assign(ctorVar, Expr.Convert(ctorFn, typeof(JsFunction))));
            // Mark as constructor
            exprs.Add(Expr.Assign(
                Expr.Property(ctorVar, nameof(JsFunction.IsConstructor)),
                Expr.Constant(true)));
        }
        else
        {
            // Default constructor — for derived classes, calls super(...args)
            if (superClass is not null)
            {
                var superVal = CompileNode(superClass);
                exprs.Add(Expr.Assign(ctorVar, Expr.Convert(
                    Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CreateDerivedDefaultConstructor))!,
                        Expr.Constant(className ?? ""),
                        EnsureJsValue(superVal)),
                    typeof(JsFunction))));
            }
            else
            {
                exprs.Add(Expr.Assign(ctorVar, Expr.Convert(
                    Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CreateDefaultConstructor))!,
                        Expr.Constant(className ?? "")),
                    typeof(JsFunction))));
            }
        }

        // Create prototype
        exprs.Add(Expr.Assign(protoVar, Expr.New(typeof(JsObject))));

        // Set up inheritance
        if (superClass is not null)
        {
            var superVal = CompileNode(superClass);
            exprs.Add(Expr.Call(
                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.SetupInheritance))!,
                ctorVar,
                protoVar,
                EnsureJsValue(superVal)));
        }
        else
        {
            exprs.Add(Expr.Assign(
                Expr.Property(protoVar, nameof(JsObject.Prototype)),
                Expr.Constant(_realm.ObjectPrototype, typeof(JsObject))));
        }

        // Link constructor and prototype
        exprs.Add(Expr.Assign(
            Expr.Property(ctorVar, nameof(JsFunction.PrototypeObject)),
            protoVar));

        exprs.Add(Expr.Call(
            Expr.Convert(protoVar, typeof(JsObject)),
            typeof(JsObject).GetMethod(nameof(JsObject.Set), [typeof(string), typeof(JsValue)])!,
            Expr.Constant("constructor"),
            Expr.Convert(ctorVar, typeof(JsValue))));

        // Compile methods and properties
        foreach (var member in body.Body)
        {
            if (member is MethodDefinition md && md.Kind != MethodKind.Constructor)
            {
                var target = md.IsStatic ? (Expr)ctorVar : protoVar;
                var fnExpr = CompileFunctionBody(
                    name: md.Key is Identifier mid ? mid.Name : "",
                    parameters: md.Value.Params,
                    body: md.Value.Body,
                    isExpression: false,
                    isArrow: false);

                Expr keyExpr;
                if (md.Computed)
                {
                    keyExpr = Expr.Call(
                        EnsureJsValue(CompileNode(md.Key)),
                        typeof(JsValue).GetMethod(nameof(JsValue.ToJsString))!);
                }
                else if (md.Key is Identifier mkid)
                {
                    keyExpr = Expr.Constant(mkid.Name);
                }
                else if (md.Key is Literal mlit)
                {
                    keyExpr = Expr.Constant(Convert.ToString(mlit.Value, CultureInfo.InvariantCulture) ?? "");
                }
                else
                {
                    continue;
                }

                if (md.Kind == MethodKind.Get)
                {
                    exprs.Add(Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.DefineGetter))!,
                        Expr.Convert(target, typeof(JsObject)),
                        keyExpr,
                        EnsureJsValue(fnExpr)));
                }
                else if (md.Kind == MethodKind.Set)
                {
                    exprs.Add(Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.DefineSetter))!,
                        Expr.Convert(target, typeof(JsObject)),
                        keyExpr,
                        EnsureJsValue(fnExpr)));
                }
                else
                {
                    exprs.Add(Expr.Call(
                        Expr.Convert(target, typeof(JsObject)),
                        typeof(JsObject).GetMethod(nameof(JsObject.Set), [typeof(string), typeof(JsValue)])!,
                        keyExpr,
                        EnsureJsValue(fnExpr)));
                }
            }
            else if (member is PropertyDefinition pd)
            {
                // Static fields go on constructor, instance fields would need constructor initialization
                if (pd.IsStatic && pd.Value is not null)
                {
                    Expr keyExpr;
                    if (pd.Computed)
                    {
                        keyExpr = Expr.Call(
                            EnsureJsValue(CompileNode(pd.Key)),
                            typeof(JsValue).GetMethod(nameof(JsValue.ToJsString))!);
                    }
                    else if (pd.Key is Identifier pkid)
                    {
                        keyExpr = Expr.Constant(pkid.Name);
                    }
                    else
                    {
                        continue;
                    }

                    var val = CompileNode(pd.Value);
                    exprs.Add(Expr.Call(
                        Expr.Convert(ctorVar, typeof(JsObject)),
                        typeof(JsObject).GetMethod(nameof(JsObject.Set), [typeof(string), typeof(JsValue)])!,
                        keyExpr,
                        EnsureJsValue(val)));
                }
            }
        }

        exprs.Add(Expr.Convert(ctorVar, typeof(JsValue)));
        return Expr.Block(typeof(JsValue), [ctorVar, protoVar], exprs);
    }

    // ───────────────────────── Functions ─────────────────────────

    private Expr CompileFunctionBody(
        string name,
        IReadOnlyList<SyntaxNode> parameters,
        SyntaxNode body,
        bool isExpression,
        bool isArrow,
        bool isGenerator = false,
        bool isAsync = false)
    {
        // Save current state
        var outerEnv = _envParam;
        var outerThis = _thisParam;
        var outerReturn = _returnLabel;
        var outerBreak = _breakLabel;
        var outerContinue = _continueLabel;
        var outerCoroutine = _coroutineParam;
        var outerLabelTargets = new Dictionary<string, (LabelTarget, LabelTarget?)>(_labelTargets);
        var outerPendingBreak = _pendingLabelBreak;
        var outerPendingContinue = _pendingLabelContinue;

        // New parameters for the function body
        var fnThisParam = Expr.Parameter(typeof(JsValue), "fnThis");
        var fnArgsParam = Expr.Parameter(typeof(JsValue[]), "fnArgs");
        var fnEnv = Expr.Parameter(typeof(Environment), "fnEnv");

        var returnLabel = Expr.Label(typeof(JsValue), "fnReturn");
        _envParam = fnEnv;
        _thisParam = isArrow ? outerThis : fnThisParam;
        _returnLabel = returnLabel;
        _breakLabel = null;
        _continueLabel = null;
        _labelTargets.Clear();
        _pendingLabelBreak = null;
        _pendingLabelContinue = null;

        // Set up coroutine parameter for generators/async
        ParameterExpression? coroutineParam = null;
        if (isGenerator || isAsync)
        {
            coroutineParam = Expr.Parameter(typeof(GeneratorCoroutine), "co");
            _coroutineParam = coroutineParam;
        }
        else
        {
            _coroutineParam = null;
        }

        try
        {
            var bodyExprs = new List<Expr>();

            // Create function-scoped environment (will be closed over the caller's env)
            // At runtime, we'll create the env from the closure scope captured in JsFunction
            // For now, fnEnv is a lambda parameter that we'll set up in the CallTarget

            // Bind parameters
            for (int i = 0; i < parameters.Count; i++)
            {
                var param = parameters[i];

                if (param is Identifier paramId)
                {
                    var argVal = Expr.Condition(
                        Expr.LessThan(Expr.Constant(i), Expr.ArrayLength(fnArgsParam)),
                        Expr.ArrayIndex(fnArgsParam, Expr.Constant(i)),
                        Expr.Constant(JsValue.Undefined, typeof(JsValue)),
                        typeof(JsValue));
                    bodyExprs.Add(CallEnvMethod("CreateAndInitializeBinding",
                        Expr.Constant(paramId.Name),
                        Expr.Constant(true),
                        argVal));
                }
                else if (param is AssignmentPattern ap)
                {
                    // Default parameter: param = defaultValue
                    var argVal = Expr.Condition(
                        Expr.LessThan(Expr.Constant(i), Expr.ArrayLength(fnArgsParam)),
                        Expr.ArrayIndex(fnArgsParam, Expr.Constant(i)),
                        Expr.Constant(JsValue.Undefined, typeof(JsValue)),
                        typeof(JsValue));

                    var argVar = Expr.Parameter(typeof(JsValue), "arg");
                    var defaultVal = CompileNode(ap.Right);

                    var finalVal = Expr.Condition(
                        Expr.TypeIs(argVar, typeof(JsUndefined)),
                        EnsureJsValue(defaultVal),
                        argVar,
                        typeof(JsValue));

                    if (ap.Left is Identifier defId)
                    {
                        bodyExprs.Add(Expr.Block(typeof(void), [argVar],
                            Expr.Assign(argVar, argVal),
                            CallEnvMethod("CreateAndInitializeBinding",
                                Expr.Constant(defId.Name),
                                Expr.Constant(true),
                                finalVal)));
                    }
                    else
                    {
                        // Destructuring with default
                        bodyExprs.Add(Expr.Block(typeof(void), [argVar],
                            Expr.Assign(argVar, argVal),
                            CompileBindingPattern(ap.Left, finalVal, mutable: true)));
                    }
                }
                else if (param is RestElement rest)
                {
                    // Rest parameter: ...args
                    if (rest.Argument is Identifier restId)
                    {
                        bodyExprs.Add(CallEnvMethod("CreateAndInitializeBinding",
                            Expr.Constant(restId.Name),
                            Expr.Constant(true),
                            Expr.Call(
                                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CreateRestArray))!,
                                fnArgsParam,
                                Expr.Constant(i),
                                Expr.Constant(_realm.ArrayPrototype, typeof(JsObject)))));
                    }
                }
                else if (param is ObjectPattern or ArrayPattern)
                {
                    // Destructuring parameter
                    var argVal = Expr.Condition(
                        Expr.LessThan(Expr.Constant(i), Expr.ArrayLength(fnArgsParam)),
                        Expr.ArrayIndex(fnArgsParam, Expr.Constant(i)),
                        Expr.Constant(JsValue.Undefined, typeof(JsValue)),
                        typeof(JsValue));
                    bodyExprs.Add(CompileBindingPattern(param, argVal, mutable: true));
                }
            }

            // Bind 'arguments' object (not for arrow functions)
            if (!isArrow)
            {
                bodyExprs.Add(CallEnvMethod("CreateAndInitializeBinding",
                    Expr.Constant("arguments"),
                    Expr.Constant(true),
                    Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CreateArgumentsObject))!,
                        fnArgsParam,
                        Expr.Constant(_realm.ArrayPrototype, typeof(JsObject)))));
            }

            // Compile function body
            if (isExpression)
            {
                // Arrow with expression body: () => expr
                bodyExprs.Add(Expr.Return(returnLabel, EnsureJsValue(CompileNode(body)), typeof(JsValue)));
            }
            else if (body is BlockStatement block)
            {
                // Hoist function declarations within function body
                HoistFunctions(block.Body, bodyExprs);

                foreach (var stmt in block.Body)
                {
                    if (stmt is FunctionDeclaration)
                    {
                        continue;
                    }

                    bodyExprs.Add(CompileNode(stmt));
                }
            }
            else
            {
                bodyExprs.Add(CompileNode(body));
            }

            // Default return value
            bodyExprs.Add(Expr.Label(returnLabel, Expr.Constant(JsValue.Undefined, typeof(JsValue))));

            var fnBody = Expr.Block(typeof(JsValue), bodyExprs);

            // Build the CallTarget lambda: (thisArg, args) => { create env from closure; body }
            // We need to capture the current environment at compile time for closure
            var closureEnvCapture = outerEnv;

            // The actual lambda wraps environment creation
            var fullBody = Expr.Block(typeof(JsValue), [fnEnv],
                Expr.Assign(fnEnv, Expr.New(
                    typeof(Environment).GetConstructor([typeof(Environment)])!,
                    closureEnvCapture)),
                fnBody);

            // Create the CallTarget based on function kind
            Expr callTarget;

            if (isGenerator || isAsync)
            {
                // Generator/async: create a 3-parameter inner lambda (this, args, coroutine)
                var innerLambda = Expr.Lambda<Func<JsValue, JsValue[], GeneratorCoroutine, JsValue>>(
                    fullBody, fnThisParam, fnArgsParam, coroutineParam!);

                // The outer CallTarget wraps the inner lambda
                var wrapperThis = Expr.Parameter(typeof(JsValue), "wrapThis");
                var wrapperArgs = Expr.Parameter(typeof(JsValue[]), "wrapArgs");

                Expr wrapperBody;
                if (isGenerator)
                {
                    wrapperBody = Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.CreateGeneratorObject))!,
                        innerLambda,
                        wrapperThis,
                        wrapperArgs,
                        Expr.Constant(_realm.GeneratorPrototype, typeof(JsObject)));
                }
                else
                {
                    wrapperBody = Expr.Call(
                        typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.RunAsyncFunction))!,
                        innerLambda,
                        wrapperThis,
                        wrapperArgs,
                        Expr.Constant(_realm.PromisePrototype, typeof(JsObject)),
                        Expr.Constant(_realm, typeof(Realm)));
                }

                callTarget = Expr.Lambda<Func<JsValue, JsValue[], JsValue>>(
                    wrapperBody, wrapperThis, wrapperArgs);
            }
            else
            {
                // Normal function: 2-parameter lambda
                callTarget = Expr.Lambda<Func<JsValue, JsValue[], JsValue>>(
                    fullBody, fnThisParam, fnArgsParam);
            }

            // Create JsFunction
            var fnVar = Expr.Parameter(typeof(JsFunction), "fn");
            var createFn = new List<Expr>
            {
                Expr.Assign(fnVar, Expr.New(typeof(JsFunction))),
                Expr.Assign(Expr.Property(fnVar, nameof(JsFunction.Name)), Expr.Constant(name)),
                Expr.Assign(Expr.Property(fnVar, nameof(JsFunction.CallTarget)), callTarget),
                Expr.Assign(Expr.Property(fnVar, nameof(JsFunction.ClosureScope)), closureEnvCapture),
                Expr.Assign(
                    Expr.Property(fnVar, nameof(JsObject.Prototype)),
                    Expr.Constant(_realm.FunctionPrototype, typeof(JsObject)))
            };

            if (!isArrow && !isGenerator && !isAsync)
            {
                // Non-arrow, non-generator, non-async functions get a prototype object for construction
                var protoObj = Expr.Parameter(typeof(JsObject), "fnProto");
                createFn.Add(Expr.Block(typeof(void), [protoObj],
                    Expr.Assign(protoObj, Expr.New(typeof(JsObject))),
                    Expr.Assign(
                        Expr.Property(protoObj, nameof(JsObject.Prototype)),
                        Expr.Constant(_realm.ObjectPrototype, typeof(JsObject))),
                    Expr.Assign(Expr.Property(fnVar, nameof(JsFunction.PrototypeObject)), protoObj)));
            }

            createFn.Add(Expr.Convert(fnVar, typeof(JsValue)));

            return Expr.Block(typeof(JsValue), [fnVar], createFn);
        }
        finally
        {
            _envParam = outerEnv;
            _thisParam = outerThis;
            _returnLabel = outerReturn;
            _breakLabel = outerBreak;
            _continueLabel = outerContinue;
            _coroutineParam = outerCoroutine;
            _labelTargets.Clear();
            foreach (var kvp in outerLabelTargets)
            {
                _labelTargets[kvp.Key] = kvp.Value;
            }

            _pendingLabelBreak = outerPendingBreak;
            _pendingLabelContinue = outerPendingContinue;
        }
    }

    // ───────────────────────── Generators / Async ─────────────────────────

    private Expr CompileYield(YieldExpression ye)
    {
        if (_coroutineParam is null)
        {
            throw new JsSyntaxError("yield is only valid in generator functions");
        }

        if (ye.Delegate)
        {
            // yield* iterable — delegate to another iterator
            var iterable = CompileNode(ye.Argument!);
            return Expr.Call(
                typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.YieldStar))!,
                _coroutineParam,
                EnsureJsValue(iterable));
        }

        var value = ye.Argument is not null
            ? CompileNode(ye.Argument)
            : Expr.Constant(JsValue.Undefined, typeof(JsValue));

        return Expr.Call(
            typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GeneratorYield))!,
            _coroutineParam,
            EnsureJsValue(value));
    }

    private Expr CompileAwait(AwaitExpression ae)
    {
        if (_coroutineParam is null)
        {
            throw new JsSyntaxError("await is only valid in async functions");
        }

        var value = CompileNode(ae.Argument);

        return Expr.Call(
            typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GeneratorYield))!,
            _coroutineParam,
            EnsureJsValue(value));
    }

    // ───────────────────────── Destructuring ─────────────────────────

    private Expr CompileBindingPattern(SyntaxNode pattern, Expr value, bool mutable)
    {
        if (pattern is Identifier id)
        {
            return CallEnvMethod("CreateAndInitializeBinding",
                Expr.Constant(id.Name),
                Expr.Constant(mutable),
                EnsureJsValue(value));
        }

        if (pattern is ObjectPattern objPat)
        {
            var valVar = Expr.Parameter(typeof(JsValue), "objPatVal");
            var exprs = new List<Expr> { Expr.Assign(valVar, EnsureJsValue(value)) };

            foreach (var prop in objPat.Properties)
            {
                if (prop is RestElement rest)
                {
                    // Rest element in object pattern: let { a, ...rest } = obj
                    // Collect remaining properties
                    var usedKeys = new List<string>();
                    foreach (var p in objPat.Properties)
                    {
                        if (p is Property pp && pp.Key is Identifier ppId)
                        {
                            usedKeys.Add(ppId.Name);
                        }
                    }

                    exprs.Add(CompileBindingPattern(rest.Argument,
                        Expr.Call(
                            typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.ObjectRestProperties))!,
                            valVar,
                            Expr.NewArrayInit(typeof(string),
                                usedKeys.Select(k => (Expr)Expr.Constant(k)))),
                        mutable));
                    continue;
                }

                if (prop is not Property p2)
                {
                    continue;
                }

                string propName;
                if (p2.Key is Identifier keyId)
                {
                    propName = keyId.Name;
                }
                else if (p2.Key is Literal keyLit)
                {
                    propName = Convert.ToString(keyLit.Value, CultureInfo.InvariantCulture) ?? "";
                }
                else
                {
                    continue;
                }

                var memberVal = Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetMember))!,
                    valVar,
                    Expr.Constant(propName));

                if (p2.Value is AssignmentPattern ap)
                {
                    // Default value: { x = 5 } = obj
                    var tempVar = Expr.Parameter(typeof(JsValue), "defaultTemp");
                    var defaultVal = CompileNode(ap.Right);

                    exprs.Add(Expr.Block(typeof(void), [tempVar],
                        Expr.Assign(tempVar, memberVal),
                        CompileBindingPattern(ap.Left,
                            Expr.Condition(
                                Expr.TypeIs(tempVar, typeof(JsUndefined)),
                                EnsureJsValue(defaultVal),
                                tempVar,
                                typeof(JsValue)),
                            mutable)));
                }
                else
                {
                    exprs.Add(CompileBindingPattern(p2.Value, memberVal, mutable));
                }
            }

            exprs.Add(valVar);
            return Expr.Block(typeof(JsValue), [valVar], exprs);
        }

        if (pattern is ArrayPattern arrPat)
        {
            var valVar = Expr.Parameter(typeof(JsValue), "arrPatVal");
            var exprs = new List<Expr> { Expr.Assign(valVar, EnsureJsValue(value)) };

            for (int i = 0; i < arrPat.Elements.Count; i++)
            {
                var element = arrPat.Elements[i];
                if (element is null)
                {
                    continue;
                }

                if (element is RestElement rest)
                {
                    // Rest element: let [a, ...rest] = arr
                    exprs.Add(CompileBindingPattern(rest.Argument,
                        Expr.Call(
                            typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.ArraySliceFrom))!,
                            valVar,
                            Expr.Constant(i),
                            Expr.Constant(_realm.ArrayPrototype, typeof(JsObject))),
                        mutable));
                    continue;
                }

                var elementVal = Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetComputedMember))!,
                    valVar,
                    Expr.Convert(
                        Expr.Call(typeof(JsNumber).GetMethod(nameof(JsNumber.Create), [typeof(double)])!,
                            Expr.Constant((double)i)),
                        typeof(JsValue)));

                if (element is AssignmentPattern ap)
                {
                    var tempVar = Expr.Parameter(typeof(JsValue), "defaultTemp");
                    var defaultVal = CompileNode(ap.Right);

                    exprs.Add(Expr.Block(typeof(void), [tempVar],
                        Expr.Assign(tempVar, elementVal),
                        CompileBindingPattern(ap.Left,
                            Expr.Condition(
                                Expr.TypeIs(tempVar, typeof(JsUndefined)),
                                EnsureJsValue(defaultVal),
                                tempVar,
                                typeof(JsValue)),
                            mutable)));
                }
                else
                {
                    exprs.Add(CompileBindingPattern(element, elementVal, mutable));
                }
            }

            exprs.Add(valVar);
            return Expr.Block(typeof(JsValue), [valVar], exprs);
        }

        throw new JsSyntaxError($"Unsupported binding pattern: {pattern.GetType().Name}");
    }

    // ───────────────────────── Hoisting ─────────────────────────

    private void HoistFunctions(IReadOnlyList<SyntaxNode> body, List<Expr> exprs)
    {
        foreach (var stmt in body)
        {
            if (stmt is FunctionDeclaration fd && fd.Id is not null)
            {
                var fnExpr = CompileFunctionBody(
                    name: fd.Id.Name,
                    parameters: fd.Params,
                    body: fd.Body,
                    isExpression: false,
                    isArrow: false,
                    isGenerator: fd.IsGenerator,
                    isAsync: fd.IsAsync);

                exprs.Add(CallEnvMethod("CreateAndInitializeBinding",
                    Expr.Constant(fd.Id.Name),
                    Expr.Constant(true),
                    EnsureJsValue(fnExpr)));
            }
        }
    }

    // ───────────────────────── Helpers ─────────────────────────

    private Expr CallEnvMethod(string methodName, params Expr[] args)
    {
        var method = typeof(Environment).GetMethod(methodName,
            args.Select(a => a.Type).ToArray());
        if (method is null)
        {
            // Try with the exact parameter types we need
            method = methodName switch
            {
                "GetBinding" => typeof(Environment).GetMethod(methodName, [typeof(string)]),
                "SetBinding" => typeof(Environment).GetMethod(methodName, [typeof(string), typeof(JsValue)]),
                "CreateAndInitializeBinding" => typeof(Environment).GetMethod(methodName, [typeof(string), typeof(bool), typeof(JsValue)]),
                "HasBinding" => typeof(Environment).GetMethod(methodName, [typeof(string)]),
                _ => throw new InvalidOperationException($"Method {methodName} not found on Environment")
            };
        }

        return Expr.Call(_envParam, method!, args);
    }

    private Expr CompileCallArguments(IReadOnlyList<SyntaxNode> arguments)
    {
        // Check if any argument is a spread element
        bool hasSpread = arguments.Any(a => a is SpreadElement);

        if (!hasSpread)
        {
            // Simple case: compile each argument directly into an array
            var argExprs = arguments.Select(a => EnsureJsValue(CompileNode(a))).ToArray();
            return Expr.NewArrayInit(typeof(JsValue), argExprs);
        }

        // Complex case: need to handle spread elements
        var listVar = Expr.Parameter(typeof(List<JsValue>), "argList");
        var exprs = new List<Expr>
        {
            Expr.Assign(listVar, Expr.New(typeof(List<JsValue>)))
        };

        foreach (var arg in arguments)
        {
            if (arg is SpreadElement spread)
            {
                exprs.Add(Expr.Call(
                    typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.SpreadIntoList))!,
                    listVar,
                    EnsureJsValue(CompileNode(spread.Argument))));
            }
            else
            {
                exprs.Add(Expr.Call(
                    listVar,
                    typeof(List<JsValue>).GetMethod(nameof(List<JsValue>.Add))!,
                    EnsureJsValue(CompileNode(arg))));
            }
        }

        exprs.Add(Expr.Call(listVar,
            typeof(List<JsValue>).GetMethod(nameof(List<JsValue>.ToArray))!));

        return Expr.Block(typeof(JsValue[]), [listVar], exprs);
    }

    private static Expr CompileToBool(Expr value)
    {
        if (value.Type == typeof(bool))
        {
            return value;
        }

        return Expr.Call(
            EnsureJsValue(value),
            typeof(JsValue).GetMethod(nameof(JsValue.ToBoolean))!);
    }

    private static Expr EnsureJsValue(Expr expr)
    {
        if (expr.Type == typeof(JsValue))
        {
            return expr;
        }

        if (typeof(JsValue).IsAssignableFrom(expr.Type))
        {
            return Expr.Convert(expr, typeof(JsValue));
        }

        // Void expressions (e.g. Break, Continue, Goto) represent non-local control
        // flow that never falls through. Wrap in a block with a dead-code Undefined
        // so the overall expression is typed as JsValue for use in Condition nodes.
        if (expr.Type == typeof(void))
        {
            return Expr.Block(typeof(JsValue), expr,
                Expr.Constant(JsValue.Undefined, typeof(JsValue)));
        }

        return expr;
    }

    private static Expr ToVoid(Expr expr)
    {
        if (expr.Type == typeof(void))
        {
            return expr;
        }

        return Expr.Block(typeof(void), expr);
    }

    private static string GetCompoundAssignmentHelper(string op) => op switch
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
        _ => throw new JsSyntaxError($"Unknown compound assignment operator: {op}")
    };
}

/// <summary>
/// Static helper methods called from compiled expression trees for complex JS operations
/// that are easier to implement in C# than directly in expression trees.
/// </summary>
public static class RuntimeHelpers
{
    // Set by JsEngine before each execution to enable primitive autoboxing
    [ThreadStatic]
#pragma warning disable CS0649, CA2211 // Assigned by JsEngine in a separate assembly
    public static Realm? CurrentRealm;
#pragma warning restore CS0649, CA2211

    // ───────────────────────── Arithmetic ─────────────────────────

    public static JsValue Add(JsValue left, JsValue right)
    {
        var l = left.ToPrimitive();
        var r = right.ToPrimitive();

        if (l is JsString || r is JsString)
        {
            return new JsString(l.ToJsString() + r.ToJsString());
        }

        return JsNumber.Create(l.ToNumber() + r.ToNumber());
    }

    public static JsValue Sub(JsValue left, JsValue right) =>
        JsNumber.Create(left.ToNumber() - right.ToNumber());

    public static JsValue Mul(JsValue left, JsValue right) =>
        JsNumber.Create(left.ToNumber() * right.ToNumber());

    public static JsValue Div(JsValue left, JsValue right) =>
        JsNumber.Create(left.ToNumber() / right.ToNumber());

    public static JsValue Mod(JsValue left, JsValue right) =>
        JsNumber.Create(left.ToNumber() % right.ToNumber());

    public static JsValue Power(JsValue left, JsValue right) =>
        JsNumber.Create(Math.Pow(left.ToNumber(), right.ToNumber()));

    // ───────────────────────── Comparison ─────────────────────────

    public static JsValue StrictEqual(JsValue left, JsValue right) =>
        left.StrictEquals(right) ? JsValue.True : JsValue.False;

    public static JsValue StrictNotEqual(JsValue left, JsValue right) =>
        left.StrictEquals(right) ? JsValue.False : JsValue.True;

    public static JsValue AbstractEqual(JsValue left, JsValue right) =>
        left.AbstractEquals(right) ? JsValue.True : JsValue.False;

    public static JsValue AbstractNotEqual(JsValue left, JsValue right) =>
        left.AbstractEquals(right) ? JsValue.False : JsValue.True;

    public static bool StrictEqualBool(JsValue left, JsValue right) =>
        left.StrictEquals(right);

    public static JsValue LessThan(JsValue left, JsValue right)
    {
        var l = left.ToPrimitive("number");
        var r = right.ToPrimitive("number");

        if (l is JsString ls && r is JsString rs)
        {
            return string.Compare(ls.Value, rs.Value, StringComparison.Ordinal) < 0
                ? JsValue.True
                : JsValue.False;
        }

        var ln = l.ToNumber();
        var rn = r.ToNumber();
        if (double.IsNaN(ln) || double.IsNaN(rn)) return JsValue.Undefined;
        return ln < rn ? JsValue.True : JsValue.False;
    }

    public static JsValue GreaterThan(JsValue left, JsValue right)
    {
        var l = left.ToPrimitive("number");
        var r = right.ToPrimitive("number");

        if (l is JsString ls && r is JsString rs)
        {
            return string.Compare(ls.Value, rs.Value, StringComparison.Ordinal) > 0
                ? JsValue.True
                : JsValue.False;
        }

        var ln = l.ToNumber();
        var rn = r.ToNumber();
        if (double.IsNaN(ln) || double.IsNaN(rn)) return JsValue.Undefined;
        return ln > rn ? JsValue.True : JsValue.False;
    }

    public static JsValue LessThanOrEqual(JsValue left, JsValue right)
    {
        var gt = GreaterThan(left, right);
        if (gt is JsUndefined) return JsValue.False;
        return gt.ToBoolean() ? JsValue.False : JsValue.True;
    }

    public static JsValue GreaterThanOrEqual(JsValue left, JsValue right)
    {
        var lt = LessThan(left, right);
        if (lt is JsUndefined) return JsValue.False;
        return lt.ToBoolean() ? JsValue.False : JsValue.True;
    }

    // ───────────────────────── Bitwise ─────────────────────────

    public static JsValue BitwiseAnd(JsValue left, JsValue right) =>
        JsNumber.Create(ToInt32(left) & ToInt32(right));

    public static JsValue BitwiseOr(JsValue left, JsValue right) =>
        JsNumber.Create(ToInt32(left) | ToInt32(right));

    public static JsValue BitwiseXor(JsValue left, JsValue right) =>
        JsNumber.Create(ToInt32(left) ^ ToInt32(right));

    public static JsValue LeftShift(JsValue left, JsValue right) =>
        JsNumber.Create(ToInt32(left) << (ToInt32(right) & 0x1F));

    public static JsValue RightShift(JsValue left, JsValue right) =>
        JsNumber.Create(ToInt32(left) >> (ToInt32(right) & 0x1F));

    public static JsValue UnsignedRightShift(JsValue left, JsValue right) =>
        JsNumber.Create((double)(ToUint32(left) >> (ToInt32(right) & 0x1F)));

    // ───────────────────────── Unary ─────────────────────────

    public static JsValue Typeof(JsValue value) => new JsString(value.TypeOf);

    public static JsValue TypeofSafe(Environment env, string name)
    {
        if (!env.HasBinding(name))
        {
            return new JsString("undefined");
        }

        return new JsString(env.GetBinding(name).TypeOf);
    }

    public static JsValue Not(JsValue value) =>
        value.ToBoolean() ? JsValue.False : JsValue.True;

    public static JsValue Negate(JsValue value) =>
        JsNumber.Create(-value.ToNumber());

    public static JsValue Plus(JsValue value) =>
        JsNumber.Create(value.ToNumber());

    public static JsValue BitwiseNot(JsValue value) =>
        JsNumber.Create(~ToInt32(value));

    public static JsValue Void(JsValue _) => JsValue.Undefined;

    // ───────────────────────── Type checks ─────────────────────────

    public static JsValue InstanceOf(JsValue obj, JsValue ctor)
    {
        if (ctor is not JsFunction ctorFn)
        {
            throw new JsTypeError("Right-hand side of instanceof is not callable");
        }

        var proto = ctorFn.PrototypeObject;
        if (proto is null)
        {
            return JsValue.False;
        }

        if (obj is not JsObject jsObj)
        {
            return JsValue.False;
        }

        var current = jsObj.Prototype;
        while (current is not null)
        {
            if (ReferenceEquals(current, proto))
            {
                return JsValue.True;
            }

            current = current.Prototype;
        }

        return JsValue.False;
    }

    public static JsValue In(JsValue key, JsValue obj)
    {
        if (obj is not JsObject jsObj)
        {
            throw new JsTypeError("Cannot use 'in' operator to search for '" +
                key.ToJsString() + "' in " + obj.ToJsString());
        }

        return jsObj.HasProperty(key.ToJsString()) ? JsValue.True : JsValue.False;
    }

    // ───────────────────────── Member access ─────────────────────────

    public static JsValue GetMember(JsValue obj, string name)
    {
        if (obj is JsNull or JsUndefined)
        {
            throw new JsTypeError($"Cannot read properties of {obj.TypeOf} (reading '{name}')");
        }

        if (obj is JsObject jsObj)
        {
            return jsObj.Get(name);
        }

        // Primitive autoboxing for strings
        if (obj is JsString str)
        {
            if (name == "length") return JsNumber.Create(str.Length);
            if (int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var idx))
            {
                return str[idx];
            }
            // Look up method on StringPrototype
            if (CurrentRealm?.StringPrototype is { } strProto)
            {
                var method = strProto.Get(name);
                if (method is not JsUndefined) return method;
            }
        }

        // Primitive autoboxing for numbers
        if (obj is JsNumber)
        {
            if (CurrentRealm?.NumberPrototype is { } numProto)
            {
                var method = numProto.Get(name);
                if (method is not JsUndefined) return method;
            }
        }

        // Primitive autoboxing for booleans
        if (obj is JsBoolean)
        {
            if (CurrentRealm?.BooleanPrototype is { } boolProto)
            {
                var method = boolProto.Get(name);
                if (method is not JsUndefined) return method;
            }
        }

        return JsValue.Undefined;
    }

    public static JsValue GetComputedMember(JsValue obj, JsValue key)
    {
        var name = key.ToJsString();
        return GetMember(obj, name);
    }

    public static void SetMember(JsValue obj, string name, JsValue value)
    {
        if (obj is JsNull or JsUndefined)
        {
            throw new JsTypeError($"Cannot set properties of {obj.TypeOf} (setting '{name}')");
        }

        if (obj is JsObject jsObj)
        {
            jsObj.Set(name, value);
        }

        // Setting on primitives is a silent no-op
    }

    public static void SetComputedMember(JsValue obj, JsValue key, JsValue value)
    {
        var name = key.ToJsString();
        SetMember(obj, name, value);
    }

    // ───────────────────────── Function calls ─────────────────────────

    public static JsValue CallFunction(JsValue callee, JsValue thisArg, JsValue[] args)
    {
        if (callee is not JsFunction fn)
        {
            throw new JsTypeError($"{callee.ToJsString()} is not a function");
        }

        return fn.Call(thisArg, args);
    }

    public static JsValue NewCall(JsValue callee, JsValue[] args)
    {
        if (callee is not JsFunction fn)
        {
            throw new JsTypeError($"{callee.ToJsString()} is not a constructor");
        }

        return fn.Construct(args);
    }

    // ───────────────────────── Delete ─────────────────────────

    public static JsValue DeleteMember(JsValue obj, string name)
    {
        if (obj is JsObject jsObj)
        {
            return jsObj.Delete(name) ? JsValue.True : JsValue.False;
        }

        return JsValue.True;
    }

    public static JsValue DeleteComputedMember(JsValue obj, JsValue key)
    {
        return DeleteMember(obj, key.ToJsString());
    }

    // ───────────────────────── Object/Array helpers ─────────────────────────

    public static void ObjectSpread(JsObject target, JsValue source)
    {
        if (source is not JsObject srcObj)
        {
            return;
        }

        foreach (var key in srcObj.OwnPropertyKeys())
        {
            var desc = srcObj.GetOwnProperty(key);
            if (desc is not null && (desc.Enumerable ?? true))
            {
                target.Set(key, srcObj.Get(key));
            }
        }
    }

    public static void ArraySpread(JsArray target, JsValue source)
    {
        if (source is JsArray arr)
        {
            for (int i = 0; i < arr.DenseLength; i++)
            {
                target.Push(arr.GetIndex(i));
            }
        }
        else if (source is JsString str)
        {
            foreach (char c in str.Value)
            {
                target.Push(new JsString(c.ToString()));
            }
        }
        else if (source is JsObject obj
            && obj.TryGetSymbolProperty(JsSymbol.Iterator, out var iterFn)
            && iterFn is JsFunction fn)
        {
            // Iterator protocol — supports generators, Sets, Maps, etc.
            var iterator = fn.Call(source, []);
            while (true)
            {
                var result = CallFunction(GetMember(iterator, "next"), iterator, []);
                if (GetMember(result, "done").ToBoolean()) break;
                target.Push(GetMember(result, "value"));
            }
        }
        else if (source is JsObject genObj)
        {
            // Fallback: iterate using numeric keys
            for (int i = 0; ; i++)
            {
                var idx = i.ToString(CultureInfo.InvariantCulture);
                if (!genObj.HasProperty(idx)) break;
                target.Push(genObj.Get(idx));
            }
        }
    }

    public static void SpreadIntoList(List<JsValue> list, JsValue source)
    {
        if (source is JsArray arr)
        {
            for (int i = 0; i < arr.DenseLength; i++)
            {
                list.Add(arr.GetIndex(i));
            }
        }
        else if (source is JsString str)
        {
            foreach (char c in str.Value)
            {
                list.Add(new JsString(c.ToString()));
            }
        }
        else if (source is JsObject obj
            && obj.TryGetSymbolProperty(JsSymbol.Iterator, out var iterFn)
            && iterFn is JsFunction fn)
        {
            var iterator = fn.Call(source, []);
            while (true)
            {
                var result = CallFunction(GetMember(iterator, "next"), iterator, []);
                if (GetMember(result, "done").ToBoolean()) break;
                list.Add(GetMember(result, "value"));
            }
        }
    }

    // ───────────────────────── For-in/of helpers ─────────────────────────

    public static string[] GetForInKeys(JsValue obj)
    {
        if (obj is not JsObject jsObj)
        {
            return [];
        }

        var keys = new List<string>();
        var current = jsObj;
        while (current is not null)
        {
            foreach (var key in current.OwnPropertyKeys())
            {
                var desc = current.GetOwnProperty(key);
                if (desc is not null && (desc.Enumerable ?? true) && !keys.Contains(key))
                {
                    keys.Add(key);
                }
            }

            current = current.Prototype;
        }

        return [.. keys];
    }

    // For-of values: stored as stringified keys for the simple string[] return
    // But we need actual JsValues, so we use a two-step approach
    private static readonly ConditionalWeakTable<JsValue, JsValue[]> ForOfCache = new();

    public static string[] GetForOfValues(JsValue obj)
    {
        JsValue[] values;

        if (obj is JsArray arr)
        {
            values = new JsValue[arr.DenseLength];
            for (int i = 0; i < arr.DenseLength; i++)
            {
                values[i] = arr.GetIndex(i);
            }
        }
        else if (obj is JsString str)
        {
            values = new JsValue[str.Length];
            for (int i = 0; i < str.Length; i++)
            {
                values[i] = new JsString(str.Value[i].ToString());
            }
        }
        else
        {
            values = [];
        }

        ForOfCache.AddOrUpdate(obj, values);

        var indices = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            indices[i] = i.ToString(CultureInfo.InvariantCulture);
        }

        return indices;
    }

    public static JsValue GetForOfValueAt(JsValue obj, int index)
    {
        if (ForOfCache.TryGetValue(obj, out var values) && index < values.Length)
        {
            return values[index];
        }

        return JsValue.Undefined;
    }

    // ───────────────────────── Rest/Arguments ─────────────────────────

    public static JsValue CreateRestArray(JsValue[] args, int startIndex, JsObject arrayPrototype)
    {
        var arr = new JsArray();
        arr.Prototype = arrayPrototype;
        for (int i = startIndex; i < args.Length; i++)
        {
            arr.Push(args[i]);
        }

        return arr;
    }

    public static JsValue CreateArgumentsObject(JsValue[] args, JsObject arrayPrototype)
    {
        var obj = new JsObject();
        for (int i = 0; i < args.Length; i++)
        {
            obj.Set(i.ToString(CultureInfo.InvariantCulture), args[i]);
        }

        obj.DefineOwnProperty("length", PropertyDescriptor.Data(
            JsNumber.Create(args.Length), writable: true, enumerable: false, configurable: true));

        return obj;
    }

    // ───────────────────────── Destructuring helpers ─────────────────────────

    public static JsValue ObjectRestProperties(JsValue source, string[] excludeKeys)
    {
        var result = new JsObject();
        if (source is not JsObject srcObj)
        {
            return result;
        }

        var excludeSet = new HashSet<string>(excludeKeys, StringComparer.Ordinal);
        foreach (var key in srcObj.OwnPropertyKeys())
        {
            if (!excludeSet.Contains(key))
            {
                var desc = srcObj.GetOwnProperty(key);
                if (desc is not null && (desc.Enumerable ?? true))
                {
                    result.Set(key, srcObj.Get(key));
                }
            }
        }

        return result;
    }

    public static JsValue ArraySliceFrom(JsValue source, int startIndex, JsObject arrayPrototype)
    {
        var arr = new JsArray();
        arr.Prototype = arrayPrototype;

        if (source is JsArray srcArr)
        {
            for (int i = startIndex; i < srcArr.DenseLength; i++)
            {
                arr.Push(srcArr.GetIndex(i));
            }
        }

        return arr;
    }

    // ───────────────────────── Class helpers ─────────────────────────

    public static JsValue CreateDefaultConstructor(string name)
    {
        var fn = new JsFunction
        {
            Name = name,
            CallTarget = static (_, _) => JsValue.Undefined,
            IsConstructor = true
        };
        return fn;
    }

    public static JsValue CreateDerivedDefaultConstructor(string name, JsValue superClass)
    {
        // Default derived constructor: constructor(...args) { super(...args); }
        var fn = new JsFunction
        {
            Name = name,
            IsConstructor = true,
            CallTarget = (thisArg, args) =>
            {
                if (superClass is JsFunction superFn)
                {
                    superFn.Call(thisArg, args);
                }
                return JsValue.Undefined;
            }
        };
        return fn;
    }

    public static void SetupInheritance(JsFunction ctor, JsObject proto, JsValue superClass)
    {
        if (superClass is not JsFunction superFn)
        {
            throw new JsTypeError("Super expression must be a function");
        }

        proto.Prototype = superFn.PrototypeObject;
        ctor.Prototype = superFn as JsObject;
    }

    public static void DefineGetter(JsObject obj, string name, JsValue getter)
    {
        var existing = obj.GetOwnProperty(name);
        if (existing is not null && existing.IsAccessorDescriptor)
        {
            existing.Get = getter;
        }
        else
        {
            obj.DefineOwnProperty(name, PropertyDescriptor.Accessor(getter, null));
        }
    }

    public static void DefineSetter(JsObject obj, string name, JsValue setter)
    {
        var existing = obj.GetOwnProperty(name);
        if (existing is not null && existing.IsAccessorDescriptor)
        {
            existing.Set = setter;
        }
        else
        {
            obj.DefineOwnProperty(name, PropertyDescriptor.Accessor(null, setter));
        }
    }

    // ───────────────────────── Exception helpers ─────────────────────────

    public static Exception CreateException(JsValue value)
    {
        if (value is JsString str)
        {
            return new JsTypeError(str.Value);
        }

        if (value is JsObject obj)
        {
            var msg = obj.Get("message");
            return new JsTypeError(msg is JsString s ? s.Value : value.ToJsString());
        }

        return new JsTypeError(value.ToJsString());
    }

    public static JsValue ExceptionToJsValue(Exception ex)
    {
        if (ex is JsThrownValueException jtv)
        {
            return jtv.ThrownValue;
        }

        if (ex is JsErrorBase jsErr)
        {
            var errObj = new JsObject();
            if (CurrentRealm?.ErrorPrototype is not null)
            {
                errObj.Prototype = CurrentRealm.ErrorPrototype;
            }

            errObj.Set("message", new JsString(jsErr.Message));
            errObj.Set("name", new JsString(ex.GetType().Name switch
            {
                nameof(JsTypeError) => "TypeError",
                nameof(JsReferenceError) => "ReferenceError",
                nameof(JsSyntaxError) => "SyntaxError",
                nameof(JsRangeError) => "RangeError",
                _ => "Error"
            }));
            return errObj;
        }

        var obj = new JsObject();
        if (CurrentRealm?.ErrorPrototype is not null)
        {
            obj.Prototype = CurrentRealm.ErrorPrototype;
        }

        obj.Set("message", new JsString(ex.Message));
        obj.Set("name", new JsString("Error"));
        return obj;
    }

    // ───────────────────────── Numeric conversion helpers ─────────────────────────

    private static int ToInt32(JsValue value)
    {
        var n = value.ToNumber();
        if (double.IsNaN(n) || double.IsInfinity(n) || n == 0)
        {
            return 0;
        }

        return (int)(long)n;
    }

    private static uint ToUint32(JsValue value)
    {
        var n = value.ToNumber();
        if (double.IsNaN(n) || double.IsInfinity(n) || n == 0)
        {
            return 0;
        }

        return (uint)(long)n;
    }

    // ───────────────────────── Generator / Async helpers ─────────────────────────

    public static JsValue GeneratorYield(GeneratorCoroutine coroutine, JsValue value) =>
        coroutine.Yield(value);

    public static JsValue CreateGeneratorObject(
        Func<JsValue, JsValue[], GeneratorCoroutine, JsValue> body,
        JsValue thisArg,
        JsValue[] args,
        JsObject generatorPrototype)
    {
        var coroutine = new GeneratorCoroutine();
        // Defer starting the body until the first next() call
        Action startAction = () => coroutine.Start(co => body(thisArg, args, co));
        return new JsGeneratorObject(coroutine, startAction, generatorPrototype);
    }

    public static JsValue RunAsyncFunction(
        Func<JsValue, JsValue[], GeneratorCoroutine, JsValue> body,
        JsValue thisArg,
        JsValue[] args,
        JsObject promisePrototype,
        Realm realm)
    {
        var outerPromise = new JsPromiseObject { Prototype = promisePrototype };
        var coroutine = new GeneratorCoroutine();

        void Advance(JsValue sent, bool isThrow)
        {
            while (true)
            {
                (JsValue value, bool done) result;
                try
                {
                    result = isThrow ? coroutine.Throw(sent) : coroutine.Next(sent);
                }
                catch (Exception ex)
                {
                    SuperRender.EcmaScript.Runtime.Builtins.PromiseConstructor.RejectPromise(outerPromise, ExceptionToJsValue(ex));
                    return;
                }

                if (result.done)
                {
                    SuperRender.EcmaScript.Runtime.Builtins.PromiseConstructor.ResolvePromise(outerPromise, result.value, realm);
                    return;
                }

                // The yielded value is what was awaited
                if (result.value is JsPromiseObject promise)
                {
                    SuperRender.EcmaScript.Runtime.Builtins.PromiseConstructor.PromiseThen(promise,
                        JsFunction.CreateNative("", (_, a) =>
                        {
                            Advance(a.Length > 0 ? a[0] : JsValue.Undefined, false);
                            return JsValue.Undefined;
                        }, 1),
                        JsFunction.CreateNative("", (_, a) =>
                        {
                            Advance(a.Length > 0 ? a[0] : JsValue.Undefined, true);
                            return JsValue.Undefined;
                        }, 1),
                        realm);
                    return;
                }

                // Not a promise — resume immediately with the value
                sent = result.value;
                isThrow = false;
            }
        }

        coroutine.Start(co => body(thisArg, args, co));

        (JsValue Value, bool Done) initial;
        try
        {
            initial = coroutine.GetInitialResult();
        }
        catch (Exception ex)
        {
            SuperRender.EcmaScript.Runtime.Builtins.PromiseConstructor.RejectPromise(outerPromise, ExceptionToJsValue(ex));
            return outerPromise;
        }

        if (initial.Done)
        {
            SuperRender.EcmaScript.Runtime.Builtins.PromiseConstructor.ResolvePromise(outerPromise, initial.Value, realm);
        }
        else if (initial.Value is JsPromiseObject initPromise)
        {
            SuperRender.EcmaScript.Runtime.Builtins.PromiseConstructor.PromiseThen(initPromise,
                JsFunction.CreateNative("", (_, a) =>
                {
                    Advance(a.Length > 0 ? a[0] : JsValue.Undefined, false);
                    return JsValue.Undefined;
                }, 1),
                JsFunction.CreateNative("", (_, a) =>
                {
                    Advance(a.Length > 0 ? a[0] : JsValue.Undefined, true);
                    return JsValue.Undefined;
                }, 1),
                realm);
        }
        else
        {
            // Non-promise initial value — resume immediately
            Advance(initial.Value, false);
        }

        return outerPromise;
    }

    public static JsValue YieldStar(GeneratorCoroutine coroutine, JsValue iterable)
    {
        var iterator = GetIterator(iterable);
        var nextFn = GetMember(iterator, "next");

        var result = CallFunction(nextFn, iterator, []);
        while (!GetMember(result, "done").ToBoolean())
        {
            var innerValue = GetMember(result, "value");
            var sent = coroutine.Yield(innerValue);
            result = CallFunction(nextFn, iterator, [sent]);
        }

        return GetMember(result, "value");
    }

    // ───────────────────────── Iterator protocol ─────────────────────────

    public static JsValue GetIterator(JsValue obj)
    {
        if (obj is JsNull or JsUndefined)
        {
            throw new JsTypeError($"Cannot read properties of {obj.TypeOf}");
        }

        // Check for Symbol.iterator on JsObject (includes arrays, generators, etc.)
        if (obj is JsObject jsObj
            && jsObj.TryGetSymbolProperty(JsSymbol.Iterator, out var iterFn)
            && iterFn is JsFunction fn)
        {
            var iterator = fn.Call(obj, []);
            if (iterator is JsObject)
            {
                return iterator;
            }

            throw new JsTypeError("Result of the Symbol.iterator method is not an object");
        }

        // Autoboxing for primitives: check prototype for Symbol.iterator
        JsObject? proto = null;
        if (obj is JsString)
        {
            proto = CurrentRealm?.StringPrototype;
        }

        if (proto is not null
            && proto.TryGetSymbolProperty(JsSymbol.Iterator, out var primIterFn)
            && primIterFn is JsFunction primFn)
        {
            var iterator = primFn.Call(obj, []);
            if (iterator is JsObject)
            {
                return iterator;
            }
        }

        throw new JsTypeError(obj.TypeOf + " is not iterable");
    }

    public static JsValue IteratorNext(JsValue iterator)
    {
        var nextFn = GetMember(iterator, "next");
        return CallFunction(nextFn, iterator, []);
    }

    public static bool IteratorComplete(JsValue result)
    {
        return GetMember(result, "done").ToBoolean();
    }

    public static JsValue IteratorValue(JsValue result)
    {
        return GetMember(result, "value");
    }
}
