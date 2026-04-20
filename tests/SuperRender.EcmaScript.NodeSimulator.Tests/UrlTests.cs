using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class UrlTests
{
    [Fact]
    public void LegacyParse_ExtractsComponents()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const url = require('url');
            const u = url.parse('http://user:pass@example.com:8080/p/a/t/h?q=1#frag');
            [u.protocol, u.auth, u.hostname, u.port, u.pathname, u.search, u.hash].join('|')";
        Assert.Equal("http:|user:pass|example.com|8080|/p/a/t/h|?q=1|#frag", engine.RunString(code));
    }

    [Fact]
    public void LegacyParse_ParsesQueryWhenRequested()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const u = require('url').parse('http://a.com/?x=1&y=2', true);
            u.query.x + ',' + u.query.y";
        Assert.Equal("1,2", engine.RunString(code));
    }

    [Fact]
    public void LegacyFormat_RoundTrips()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const url = require('url');
            const orig = 'http://host.example:8080/path?x=1#h';
            url.format(url.parse(orig)) === orig";
        Assert.Equal("true", engine.RunString(code));
    }

    [Fact]
    public void Resolve_Relative()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal("http://example.com/a/d", engine.RunString(
            "require('url').resolve('http://example.com/a/b/c', '../d')"));
    }

    [Fact]
    public void Resolve_AbsolutePath()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal("http://example.com/x", engine.RunString(
            "require('url').resolve('http://example.com/a/b', '/x')"));
    }

    [Fact]
    public void Resolve_AbsoluteOther()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal("https://other.com/", engine.RunString(
            "require('url').resolve('http://example.com/a', 'https://other.com/')"));
    }

    [Fact]
    public void PathToFileUrl_RoundTripUnix()
    {
        if (OperatingSystem.IsWindows()) return; // different scheme on Windows
        var (engine, _) = TestHost.Create();
        var code = @"
            const u = require('url');
            const p = '/tmp/file name.txt';
            u.fileURLToPath(u.pathToFileURL(p).href) === p";
        Assert.Equal("true", engine.RunString(code));
    }

    [Fact]
    public void UrlToHttpOptions_FromLegacyShape()
    {
        var (engine, _) = TestHost.Create();
        // Pass a plain object shaped like a WHATWG URL — engine doesn't install global URL.
        var code = @"
            const u = require('url');
            const opts = u.urlToHttpOptions({
                protocol: 'http:', hostname: 'example.com', port: '9000',
                pathname: '/p', search: '?x=1', hash: '', host: 'example.com:9000',
                username: 'a', password: 'b', href: 'http://a:b@example.com:9000/p?x=1'
            });
            opts.hostname + '|' + opts.port + '|' + opts.path + '|' + opts.auth";
        Assert.Equal("example.com|9000|/p?x=1|a:b", engine.RunString(code));
    }
}
