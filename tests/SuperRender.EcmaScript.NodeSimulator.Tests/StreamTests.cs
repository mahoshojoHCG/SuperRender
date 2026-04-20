using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class StreamTests
{
    [Fact]
    public void Readable_DataEventsFireForPushedChunks()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const { Readable } = require('stream');
            const r = new Readable();
            const seen = [];
            r.on('data', (c) => seen.push(c));
            r.push('a'); r.push('b'); r.push(null);
            seen.join('') + '|ended:' + r.listenerCount('end')";
        Assert.Equal("ab|ended:0", engine.RunString(code));
    }

    [Fact]
    public void Readable_FiresEndAfterPushNull()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const { Readable } = require('stream');
            const r = new Readable();
            let ended = false;
            r.on('end', () => ended = true);
            r.on('data', () => {});
            r.push('x'); r.push(null);
            ended";
        Assert.Equal("true", engine.RunString(code));
    }

    [Fact]
    public void PassThrough_ForwardsWrites()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const { PassThrough } = require('stream');
            const pt = new PassThrough();
            const chunks = [];
            pt.on('data', c => chunks.push(c));
            pt.write('one');
            pt.write('two');
            pt.end();
            chunks.join(',')";
        Assert.Equal("one,two", engine.RunString(code));
    }

    [Fact]
    public void Transform_RunsTransformFunction()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const { Transform } = require('stream');
            const up = new Transform({
                transform(chunk, enc, cb) { this.push(String(chunk).toUpperCase()); cb(); }
            });
            const seen = [];
            up.on('data', c => seen.push(c));
            up.write('hi');
            up.write('there');
            up.end();
            seen.join('-')";
        Assert.Equal("HI-THERE", engine.RunString(code));
    }

    [Fact]
    public void Writable_CollectsChunksAndFinish()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const { Writable } = require('stream');
            const collected = [];
            const w = new Writable({ write(chunk, enc, cb) { collected.push(chunk); cb(); } });
            let finished = false;
            w.on('finish', () => finished = true);
            w.write('a');
            w.write('b');
            w.end();
            collected.join(',') + '|' + finished";
        Assert.Equal("a,b|true", engine.RunString(code));
    }

    [Fact]
    public void ReadableFrom_ArrayDrainsOnDataListener()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const { Readable } = require('stream');
            const r = Readable.from(['x','y','z']);
            const out = [];
            r.on('data', c => out.push(c));
            out.join('')";
        Assert.Equal("xyz", engine.RunString(code));
    }

    [Fact]
    public void Pipeline_CallsCallbackOnFinish()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const { Readable, PassThrough, pipeline } = require('stream');
            const src = new Readable();
            const dst = new PassThrough();
            const got = [];
            dst.on('data', c => got.push(c));
            let done = null;
            pipeline(src, dst, (err) => done = err === null);
            src.push('p'); src.push('q');
            dst.end();
            got.join('') + '|' + done";
        Assert.Equal("pq|true", engine.RunString(code));
    }
}
