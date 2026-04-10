using SuperRender.EcmaScript.Interop;
using SuperRender.EcmaScript.Runtime;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Interop;

public class JsEngineTests
{
    private static JsEngine CreateEngine() => new();

    // ═══════════════════════════════════════════
    //  Simple expressions
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_IntegerAddition_ReturnsCorrectResult()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("1 + 2");
        Assert.Equal(3, result);
    }

    [Fact]
    public void Execute_FloatArithmetic_ReturnsCorrectResult()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("2.5 * 4");
        Assert.Equal(10.0, result);
    }

    [Fact]
    public void Execute_StringConcatenation_ReturnsCorrectResult()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("'hello' + ' ' + 'world'");
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void Execute_Comparison_ReturnsBoolean()
    {
        var engine = CreateEngine();
        Assert.True(engine.Execute<bool>("5 > 3"));
        Assert.False(engine.Execute<bool>("2 > 10"));
    }

    [Fact]
    public void Execute_StrictEquality_Works()
    {
        var engine = CreateEngine();
        Assert.True(engine.Execute<bool>("1 === 1"));
        Assert.False(engine.Execute<bool>("1 === '1'"));
    }

    [Fact]
    public void Execute_LooseEquality_Works()
    {
        var engine = CreateEngine();
        Assert.True(engine.Execute<bool>("1 == '1'"));
        Assert.True(engine.Execute<bool>("null == undefined"));
    }

    // ═══════════════════════════════════════════
    //  Variable declarations
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_LetDeclaration_ReturnsValue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("let x = 5; x");
        Assert.Equal(5, result);
    }

    [Fact]
    public void Execute_ConstDeclaration_ReturnsValue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const y = 10; y");
        Assert.Equal(10, result);
    }

    [Fact]
    public void Execute_VarDeclaration_ReturnsValue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("var z = 7; z");
        Assert.Equal(7, result);
    }

    // ═══════════════════════════════════════════
    //  Function definitions and calls
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_FunctionDeclarationAndCall_ReturnsResult()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("function add(a, b) { return a + b; } add(3, 4)");
        Assert.Equal(7, result);
    }

    [Fact]
    public void Execute_FunctionExpression_ReturnsResult()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const mul = function(a, b) { return a * b; }; mul(3, 4)");
        Assert.Equal(12, result);
    }

    [Fact]
    public void Execute_RecursiveFunction_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>(@"
            function factorial(n) {
                if (n <= 1) return 1;
                return n * factorial(n - 1);
            }
            factorial(5)
        ");
        Assert.Equal(120, result);
    }

    // ═══════════════════════════════════════════
    //  Arrow functions
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_ArrowFunction_ExpressionBody()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const double = x => x * 2; double(5)");
        Assert.Equal(10, result);
    }

    [Fact]
    public void Execute_ArrowFunction_BlockBody()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const add = (a, b) => { return a + b; }; add(3, 7)");
        Assert.Equal(10, result);
    }

    // ═══════════════════════════════════════════
    //  Object literals and member access
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_ObjectLiteral_MemberAccess()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("const obj = { name: 'test', value: 42 }; obj.name");
        Assert.Equal("test", result);
    }

    [Fact]
    public void Execute_ObjectLiteral_BracketAccess()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const obj = { x: 10 }; obj['x']");
        Assert.Equal(10, result);
    }

    [Fact]
    public void Execute_ObjectLiteral_NestedAccess()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const obj = { inner: { value: 99 } }; obj.inner.value");
        Assert.Equal(99, result);
    }

    // ═══════════════════════════════════════════
    //  Array literals and methods
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_ArrayLiteral_IndexAccess()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const arr = [10, 20, 30]; arr[1]");
        Assert.Equal(20, result);
    }

    [Fact]
    public void Execute_ArrayLength_ReturnsCorrectValue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const arr = [1, 2, 3]; arr.length");
        Assert.Equal(3, result);
    }

    [Fact]
    public void Execute_ArrayPush_AddsElement()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const arr = [1, 2]; arr.push(3); arr.length");
        Assert.Equal(3, result);
    }

    // ═══════════════════════════════════════════
    //  Control flow
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_IfElse_ChoosesCorrectBranch()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            let result;
            if (true) { result = 'yes'; } else { result = 'no'; }
            result
        ");
        Assert.Equal("yes", result);
    }

    [Fact]
    public void Execute_ForLoop_Accumulates()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>(@"
            let sum = 0;
            for (let i = 0; i < 5; i++) {
                sum += i;
            }
            sum
        ");
        Assert.Equal(10, result);
    }

    [Fact]
    public void Execute_WhileLoop_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>(@"
            let count = 0;
            while (count < 3) {
                count++;
            }
            count
        ");
        Assert.Equal(3, result);
    }

    [Fact]
    public void Execute_DoWhileLoop_ExecutesAtLeastOnce()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>(@"
            let x = 0;
            do {
                x++;
            } while (false);
            x
        ");
        Assert.Equal(1, result);
    }

    [Fact]
    public void Execute_SwitchStatement_MatchesCase()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            let r;
            switch (2) {
                case 1: r = 'one'; break;
                case 2: r = 'two'; break;
                default: r = 'other';
            }
            r
        ");
        Assert.Equal("two", result);
    }

    // ═══════════════════════════════════════════
    //  String methods
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_StringLength_ReturnsCorrectValue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("'hello'.length");
        Assert.Equal(5, result);
    }

    [Fact]
    public void Execute_StringToUpperCase_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("'hello'.toUpperCase()");
        Assert.Equal("HELLO", result);
    }

    [Fact]
    public void Execute_StringIndexOf_FindsSubstring()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("'hello world'.indexOf('world')");
        Assert.Equal(6, result);
    }

    // ═══════════════════════════════════════════
    //  Math methods
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_MathMax_ReturnsMaximum()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("Math.max(3, 7, 1, 9, 4)");
        Assert.Equal(9, result);
    }

    [Fact]
    public void Execute_MathFloor_ReturnsFloor()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("Math.floor(3.7)");
        Assert.Equal(3, result);
    }

    [Fact]
    public void Execute_MathPI_ReturnsApproximateValue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<double>("Math.PI");
        Assert.Equal(Math.PI, result, 10);
    }

    [Fact]
    public void Execute_MathAbs_ReturnsAbsoluteValue()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("Math.abs(-5)");
        Assert.Equal(5, result);
    }

    // ═══════════════════════════════════════════
    //  JSON
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_JSONParse_ParsesObject()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const obj = JSON.parse('{\"x\":42}'); obj.x");
        Assert.Equal(42, result);
    }

    [Fact]
    public void Execute_JSONStringify_SerializesObject()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("JSON.stringify({a: 1})");
        Assert.Equal("{\"a\":1}", result);
    }

    // ═══════════════════════════════════════════
    //  Error handling
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_TryCatch_CatchesError()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            let msg;
            try {
                throw new Error('oops');
            } catch (e) {
                msg = e.message;
            }
            msg
        ");
        Assert.Equal("oops", result);
    }

    [Fact]
    public void Execute_TryFinally_FinallyAlwaysRuns()
    {
        var engine = CreateEngine();
        var result = engine.Execute<bool>(@"
            let ran = false;
            try {
                let x = 1;
            } finally {
                ran = true;
            }
            ran
        ");
        Assert.True(result);
    }

    // ═══════════════════════════════════════════
    //  Closures
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_Closure_CapturesOuterVariable()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>(@"
            function makeCounter() {
                let count = 0;
                return function() {
                    count++;
                    return count;
                };
            }
            const counter = makeCounter();
            counter();
            counter();
            counter()
        ");
        Assert.Equal(3, result);
    }

    // ═══════════════════════════════════════════
    //  Classes
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_ClassConstructorAndMethod_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            class Greeter {
                constructor(name) {
                    this.name = name;
                }
                greet() {
                    return 'Hello, ' + this.name;
                }
            }
            const g = new Greeter('World');
            g.greet()
        ");
        Assert.Equal("Hello, World", result);
    }

    [Fact]
    public void Execute_ClassInheritance_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>(@"
            class Animal {
                constructor(name) {
                    this.name = name;
                }
                speak() {
                    return this.name + ' makes a sound';
                }
            }
            class Dog extends Animal {
                speak() {
                    return this.name + ' barks';
                }
            }
            const d = new Dog('Rex');
            d.speak()
        ");
        Assert.Equal("Rex barks", result);
    }

    // ═══════════════════════════════════════════
    //  Template literals
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_TemplateLiteral_NoSubstitution()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("`hello world`");
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void Execute_TemplateLiteral_WithSubstitution()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("const name = 'World'; `Hello, ${name}!`");
        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public void Execute_TemplateLiteral_WithExpression()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("`2 + 3 = ${2 + 3}`");
        Assert.Equal("2 + 3 = 5", result);
    }

    // ═══════════════════════════════════════════
    //  Spread operator
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_SpreadInArray_ExpandsElements()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const a = [1, 2]; const b = [...a, 3]; b.length");
        Assert.Equal(3, result);
    }

    // ═══════════════════════════════════════════
    //  Destructuring
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_ArrayDestructuring_AssignsValues()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const [a, b, c] = [10, 20, 30]; b");
        Assert.Equal(20, result);
    }

    [Fact]
    public void Execute_ObjectDestructuring_AssignsValues()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const { x, y } = { x: 1, y: 2 }; y");
        Assert.Equal(2, result);
    }

    // ═══════════════════════════════════════════
    //  Optional chaining
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_OptionalChaining_ReturnsUndefinedOnNull()
    {
        var engine = CreateEngine();
        var result = engine.Execute("const obj = null; obj?.prop");
        Assert.Same(JsValue.Undefined, result);
    }

    [Fact]
    public void Execute_OptionalChaining_ReturnsValueOnNonNull()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const obj = { a: { b: 42 } }; obj?.a?.b");
        Assert.Equal(42, result);
    }

    // ═══════════════════════════════════════════
    //  Nullish coalescing
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_NullishCoalescing_ReturnsLeftWhenNotNullish()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const x = 5; x ?? 10");
        Assert.Equal(5, result);
    }

    [Fact]
    public void Execute_NullishCoalescing_ReturnsRightWhenNull()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const x = null; x ?? 10");
        Assert.Equal(10, result);
    }

    [Fact]
    public void Execute_NullishCoalescing_ReturnsRightWhenUndefined()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const x = undefined; x ?? 42");
        Assert.Equal(42, result);
    }

    [Fact]
    public void Execute_NullishCoalescing_ZeroIsNotNullish()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("const x = 0; x ?? 10");
        Assert.Equal(0, result);
    }

    // ═══════════════════════════════════════════
    //  .NET interop: SetValue
    // ═══════════════════════════════════════════

    [Fact]
    public void SetValue_Int_AccessibleFromJs()
    {
        var engine = CreateEngine();
        engine.SetValue("x", 42);
        var result = engine.Execute<int>("x");
        Assert.Equal(42, result);
    }

    [Fact]
    public void SetValue_String_AccessibleFromJs()
    {
        var engine = CreateEngine();
        engine.SetValue("greeting", "hi");
        var result = engine.Execute<string>("greeting");
        Assert.Equal("hi", result);
    }

    [Fact]
    public void SetValue_Bool_AccessibleFromJs()
    {
        var engine = CreateEngine();
        engine.SetValue("flag", true);
        var result = engine.Execute<bool>("flag");
        Assert.True(result);
    }

    [Fact]
    public void SetValue_Null_AccessibleFromJs()
    {
        var engine = CreateEngine();
        engine.SetValue("nothing", (object?)null);
        var result = engine.Execute("nothing");
        Assert.Same(JsValue.Null, result);
    }

    // ═══════════════════════════════════════════
    //  .NET interop: GetValue
    // ═══════════════════════════════════════════

    [Fact]
    public void GetValue_ExistingVariable_ReturnsValue()
    {
        var engine = CreateEngine();
        engine.Execute("let myVar = 99;");
        var result = engine.GetValue("myVar");
        Assert.IsType<JsNumber>(result);
        Assert.Equal(99.0, ((JsNumber)result).Value);
    }

    [Fact]
    public void GetValue_NonExistent_ReturnsUndefined()
    {
        var engine = CreateEngine();
        var result = engine.GetValue("nonExistent");
        Assert.Same(JsValue.Undefined, result);
    }

    // ═══════════════════════════════════════════
    //  .NET interop: Invoke
    // ═══════════════════════════════════════════

    [Fact]
    public void Invoke_JsFunction_ReturnsResult()
    {
        var engine = CreateEngine();
        engine.Execute("function add(a, b) { return a + b; }");
        var result = engine.Invoke("add", 3, 4);
        Assert.Equal(7.0, ((JsNumber)result).Value);
    }

    // ═══════════════════════════════════════════
    //  .NET interop: Delegate
    // ═══════════════════════════════════════════

    [Fact]
    public void SetValue_Delegate_CallableFromJs()
    {
        var engine = CreateEngine();
        engine.SetValue("multiply", new Func<int, int, int>((a, b) => a * b));
        var result = engine.Execute<int>("multiply(6, 7)");
        Assert.Equal(42, result);
    }

    [Fact]
    public void SetValue_ActionDelegate_CallableFromJs()
    {
        var engine = CreateEngine();
        var captured = 0;
        engine.SetValue("capture", new Action<int>(v => captured = v));
        engine.Execute("capture(99)");
        Assert.Equal(99, captured);
    }

    // ═══════════════════════════════════════════
    //  .NET interop: RegisterType
    // ═══════════════════════════════════════════

    [Fact]
    public void RegisterType_CanConstructFromJs()
    {
        var engine = CreateEngine();
        engine.RegisterType<TestPoint>();
        var result = engine.Execute<int>("const p = new TestPoint(10, 20); p.X");
        Assert.Equal(10, result);
    }

    [Fact]
    public void RegisterType_CanCallMethods()
    {
        var engine = CreateEngine();
        engine.RegisterType<TestPoint>();
        var result = engine.Execute<int>("const p = new TestPoint(3, 4); p.Sum()");
        Assert.Equal(7, result);
    }

    [Fact]
    public void RegisterType_CanCallStaticMethods()
    {
        var engine = CreateEngine();
        engine.RegisterType<TestPoint>();
        var result = engine.Execute<int>("TestPoint.Origin().X");
        Assert.Equal(0, result);
    }

    // ═══════════════════════════════════════════
    //  Console output capture
    // ═══════════════════════════════════════════

    [Fact]
    public void SetConsoleOutput_CapturesConsoleLog()
    {
        var engine = CreateEngine();
        var writer = new StringWriter();
        engine.SetConsoleOutput(writer);
        engine.Execute("console.log('hello from js')");
        Assert.Contains("hello from js", writer.ToString());
    }

    [Fact]
    public void SetConsoleOutput_CapturesMultipleArgs()
    {
        var engine = CreateEngine();
        var writer = new StringWriter();
        engine.SetConsoleOutput(writer);
        engine.Execute("console.log('a', 'b', 'c')");
        Assert.Contains("a b c", writer.ToString());
    }

    // ═══════════════════════════════════════════
    //  Sandboxing
    // ═══════════════════════════════════════════

    [Fact]
    public void Sandboxing_UnregisteredType_NotAccessible()
    {
        var engine = CreateEngine();
        // System.IO.File should not be accessible from JS
        var result = engine.GetValue("System");
        Assert.Same(JsValue.Undefined, result);
    }

    [Fact]
    public void Sandboxing_OnlyRegisteredTypesAccessible()
    {
        var engine = CreateEngine();
        // Register TestPoint but not other types
        engine.RegisterType<TestPoint>();
        // TestPoint should be accessible
        var tp = engine.GetValue("TestPoint");
        Assert.NotSame(JsValue.Undefined, tp);
        // Random .NET type should not be
        var missing = engine.GetValue("StringBuilder");
        Assert.Same(JsValue.Undefined, missing);
    }

    // ═══════════════════════════════════════════
    //  Miscellaneous
    // ═══════════════════════════════════════════

    [Fact]
    public void Execute_TernaryOperator_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<string>("true ? 'yes' : 'no'");
        Assert.Equal("yes", result);
    }

    [Fact]
    public void Execute_TypeofOperator_ReturnsCorrectType()
    {
        var engine = CreateEngine();
        Assert.Equal("number", engine.Execute<string>("typeof 42"));
        Assert.Equal("string", engine.Execute<string>("typeof 'hello'"));
        Assert.Equal("boolean", engine.Execute<string>("typeof true"));
        Assert.Equal("undefined", engine.Execute<string>("typeof undefined"));
    }

    [Fact]
    public void Execute_LogicalOperators_ShortCircuit()
    {
        var engine = CreateEngine();
        // 0 is falsy, so `0 || 7` returns 7 (the first truthy operand, or the last operand)
        var result = engine.Execute<int>("0 || 7");
        Assert.Equal(7, result);
    }

    [Fact]
    public void Execute_LogicalAnd_ShortCircuits()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("5 && 10");
        Assert.Equal(10, result);
    }

    [Fact]
    public void Execute_BitwiseOperations_Work()
    {
        var engine = CreateEngine();
        Assert.Equal(3, engine.Execute<int>("1 | 2"));
        Assert.Equal(0, engine.Execute<int>("1 & 2"));
        Assert.Equal(3, engine.Execute<int>("1 ^ 2"));
    }

    [Fact]
    public void Execute_ForOfLoop_IteratesArray()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>(@"
            let sum = 0;
            for (const item of [1, 2, 3, 4]) {
                sum += item;
            }
            sum
        ");
        Assert.Equal(10, result);
    }

    [Fact]
    public void Execute_Exponentiation_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute<int>("2 ** 10");
        Assert.Equal(1024, result);
    }

    // ═══════════════════════════════════════════
    //  Test helper class
    // ═══════════════════════════════════════════

    public class TestPoint
    {
        public int X { get; }
        public int Y { get; }

        public TestPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int Sum() => X + Y;

        public static TestPoint Origin() => new(0, 0);
    }
}
