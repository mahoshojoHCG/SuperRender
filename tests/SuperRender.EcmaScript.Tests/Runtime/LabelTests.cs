using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Runtime;

public class LabelTests
{
    private static JsEngine CreateEngine() => new();

    [Fact]
    public void BreakLabel_NestedForLoop_BreaksOuter()
    {
        var engine = CreateEngine();
        var result = engine.Execute(@"
            var r = -1;
            outer: for (var i = 0; i < 3; i++) {
                for (var j = 0; j < 3; j++) {
                    if (j === 1) { r = i; break outer; }
                }
            }
            r;
        ");

        Assert.Equal(0.0, result.ToNumber());
    }

    [Fact]
    public void ContinueLabel_NestedForLoop_ContinuesOuter()
    {
        var engine = CreateEngine();
        var result = engine.Execute(@"
            var sum = 0;
            outer: for (var i = 0; i < 3; i++) {
                for (var j = 0; j < 3; j++) {
                    if (j === 1) continue outer;
                    sum++;
                }
            }
            sum;
        ");

        // Only j=0 executes for each i (0,1,2), so sum = 3
        Assert.Equal(3.0, result.ToNumber());
    }

    [Fact]
    public void BreakLabel_NestedWhileLoop_BreaksOuter()
    {
        var engine = CreateEngine();
        var result = engine.Execute(@"
            var i = 0;
            var j;
            outer: while (i < 3) {
                j = 0;
                while (j < 3) {
                    if (j === 1) break outer;
                    j++;
                }
                i++;
            }
            i;
        ");

        Assert.Equal(0.0, result.ToNumber());
    }

    [Fact]
    public void ContinueLabel_NestedWhileLoop_ContinuesOuter()
    {
        var engine = CreateEngine();
        var result = engine.Execute(@"
            var sum = 0;
            var i = 0;
            outer: while (i < 3) {
                var j = 0;
                i++;
                while (j < 3) {
                    if (j === 1) continue outer;
                    j++;
                    sum++;
                }
            }
            sum;
        ");

        // For each outer iteration, inner loop runs j=0 (sum++) then j=1 (continue outer)
        Assert.Equal(3.0, result.ToNumber());
    }

    [Fact]
    public void BreakLabel_OnBlock_BreaksBlock()
    {
        var engine = CreateEngine();
        var result = engine.Execute(@"
            var x = 0;
            foo: {
                x = 1;
                break foo;
                x = 2;
            }
            x;
        ");

        Assert.Equal(1.0, result.ToNumber());
    }

    [Fact]
    public void Label_MultipleLevels_CorrectTarget()
    {
        var engine = CreateEngine();
        var result = engine.Execute(@"
            var result = '';
            outer: for (var i = 0; i < 3; i++) {
                middle: for (var j = 0; j < 3; j++) {
                    for (var k = 0; k < 3; k++) {
                        if (k === 1 && j === 1) break outer;
                        if (k === 1) continue middle;
                        result += '' + i + j + k;
                    }
                }
            }
            result;
        ");

        // i=0,j=0,k=0 -> '000'; k=1 -> continue middle
        // i=0,j=1,k=0 -> '010'; k=1,j==1 -> break outer
        Assert.Equal("000010", result.ToJsString());
    }

    [Fact]
    public void BreakLabel_InSwitch_BreaksLoop()
    {
        var engine = CreateEngine();
        var result = engine.Execute(@"
            var sum = 0;
            loop: for (var i = 0; i < 5; i++) {
                switch (i) {
                    case 2:
                        break loop;
                    default:
                        sum += i;
                        break;
                }
            }
            sum;
        ");

        // i=0: sum=0, i=1: sum=1, i=2: break loop
        Assert.Equal(1.0, result.ToNumber());
    }

    [Fact]
    public void ContinueLabel_ForInLoop_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute(@"
            var obj = { a: 1, b: 2, c: 3 };
            var keys = '';
            outer: for (var key in obj) {
                for (var i = 0; i < 2; i++) {
                    if (i === 1) continue outer;
                }
                keys += key + '!';
            }
            keys;
        ");

        // continue outer fires at i=1 for every key, so the keys += line never runs
        Assert.Equal("", result.ToJsString());
    }

    [Fact]
    public void ContinueLabel_ForOfLoop_Works()
    {
        var engine = CreateEngine();
        var result = engine.Execute(@"
            var arr = [10, 20, 30];
            var sum = 0;
            outer: for (var val of arr) {
                for (var i = 0; i < 3; i++) {
                    if (i === 1) continue outer;
                    sum += val;
                }
            }
            sum;
        ");

        // For each val, only i=0 adds val: sum = 10 + 20 + 30 = 60
        Assert.Equal(60.0, result.ToNumber());
    }

    [Fact]
    public void Label_NoBreak_NormalCompletion()
    {
        var engine = CreateEngine();
        var result = engine.Execute(@"
            var x = 0;
            myLabel: for (var i = 0; i < 3; i++) {
                x += i;
            }
            x;
        ");

        // Normal loop: 0+1+2 = 3
        Assert.Equal(3.0, result.ToNumber());
    }

    [Fact]
    public void BreakLabel_DoWhileLoop_BreaksOuter()
    {
        var engine = CreateEngine();
        var result = engine.Execute(@"
            var i = 0;
            var j;
            outer: do {
                j = 0;
                do {
                    if (j === 1) break outer;
                    j++;
                } while (j < 3);
                i++;
            } while (i < 3);
            i;
        ");

        Assert.Equal(0.0, result.ToNumber());
    }
}
