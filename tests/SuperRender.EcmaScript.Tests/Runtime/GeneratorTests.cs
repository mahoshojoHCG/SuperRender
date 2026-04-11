using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Runtime;

public class GeneratorTests
{
    private static JsEngine CreateEngine() => new();

    [Fact]
    public void BasicGenerator_YieldAndNext()
    {
        var engine = CreateEngine();
        engine.Execute(@"
            function* gen() {
                yield 1;
                yield 2;
                yield 3;
            }
            var g = gen();
            var r1 = g.next();
            var r2 = g.next();
            var r3 = g.next();
            var r4 = g.next();
        ");

        Assert.Equal(1.0, engine.Execute("r1.value").ToNumber());
        Assert.False(engine.Execute("r1.done").ToBoolean());

        Assert.Equal(2.0, engine.Execute("r2.value").ToNumber());
        Assert.False(engine.Execute("r2.done").ToBoolean());

        Assert.Equal(3.0, engine.Execute("r3.value").ToNumber());
        Assert.False(engine.Execute("r3.done").ToBoolean());

        Assert.True(engine.Execute("r4.done").ToBoolean());
    }

    [Fact]
    public void Generator_MultipleYields()
    {
        var engine = CreateEngine();
        engine.Execute(@"
            function* count() {
                yield 'a';
                yield 'b';
                yield 'c';
            }
            var g = count();
            var values = [];
            var result = g.next();
            while (!result.done) {
                values.push(result.value);
                result = g.next();
            }
        ");

        Assert.Equal(3.0, engine.Execute("values.length").ToNumber());
        Assert.Equal("a", engine.Execute("values[0]").ToJsString());
        Assert.Equal("b", engine.Execute("values[1]").ToJsString());
        Assert.Equal("c", engine.Execute("values[2]").ToJsString());
    }

    [Fact]
    public void Generator_Return_EndsGenerator()
    {
        var engine = CreateEngine();
        engine.Execute(@"
            function* gen() {
                yield 1;
                yield 2;
                yield 3;
            }
            var g = gen();
            var r1 = g.next();
            var r2 = g.return(42);
            var r3 = g.next();
        ");

        Assert.Equal(1.0, engine.Execute("r1.value").ToNumber());
        Assert.False(engine.Execute("r1.done").ToBoolean());

        Assert.Equal(42.0, engine.Execute("r2.value").ToNumber());
        Assert.True(engine.Execute("r2.done").ToBoolean());

        Assert.True(engine.Execute("r3.done").ToBoolean());
    }

    [Fact]
    public void ForOf_OverGenerator()
    {
        var engine = CreateEngine();
        engine.Execute(@"
            function* nums() {
                yield 10;
                yield 20;
                yield 30;
            }
            var sum = 0;
            for (var x of nums()) {
                sum += x;
            }
        ");

        Assert.Equal(60.0, engine.Execute("sum").ToNumber());
    }

    [Fact]
    public void Generator_YieldWithSentValue()
    {
        var engine = CreateEngine();
        engine.Execute(@"
            function* echo() {
                var received = yield 'first';
                yield received;
            }
            var g = echo();
            var r1 = g.next();
            var r2 = g.next('hello');
        ");

        Assert.Equal("first", engine.Execute("r1.value").ToJsString());
        Assert.Equal("hello", engine.Execute("r2.value").ToJsString());
    }
}
