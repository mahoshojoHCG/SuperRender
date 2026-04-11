using SuperRender.Renderer.Rendering;
using SuperRender.EcmaScript.Dom;
using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.Browser.Tests;

public class FetchApiTests
{
    private static (JsEngine engine, DomBridge bridge, Action drainQueue) CreateTestEnvironment(
        Func<string, string, IReadOnlyList<KeyValuePair<string, string>>?, string?, Task<FetchResult>>? fetchFunc = null)
    {
        var pipeline = new RenderPipeline(new SuperRender.Renderer.Rendering.Layout.MonospaceTextMeasurer());
        var doc = pipeline.LoadHtml("<html><body></body></html>");
        var engine = new JsEngine();
        var bridge = new DomBridge(engine, doc);

        fetchFunc ??= (_, _, _, _) => Task.FromResult(new FetchResult
        {
            Status = 200,
            StatusText = "OK",
            Body = "response body",
            Url = "https://example.com/data",
            Headers = [new KeyValuePair<string, string>("Content-Type", "text/plain")],
        });

        // Use a queue so we can control when actions are executed
        var pendingActions = new Queue<Action>();
        bridge.SetFetch(fetchFunc, action => pendingActions.Enqueue(action));

        bridge.Install();

        void DrainQueue()
        {
            // Give the Task.Run a moment to complete and enqueue its action
            Thread.Sleep(50);
            while (pendingActions.Count > 0)
                pendingActions.Dequeue()();
        }

        return (engine, bridge, DrainQueue);
    }

    [Fact]
    public void Fetch_ReturnsPromise()
    {
        var (engine, _, _) = CreateTestEnvironment();
        var result = engine.Execute("typeof fetch");
        Assert.Equal("function", result.ToJsString());
    }

    [Fact]
    public void Fetch_ResolvesWithResponse()
    {
        var (engine, _, drain) = CreateTestEnvironment();
        engine.Execute(@"
            var resp = null;
            fetch('https://example.com/data').then(function(r) { resp = r; });
        ");
        drain();
        var result = engine.Execute("resp !== null");
        Assert.True(result.ToBoolean());
    }

    [Fact]
    public void Fetch_Response_Status()
    {
        var (engine, _, drain) = CreateTestEnvironment();
        engine.Execute(@"
            var status = 0;
            fetch('https://example.com/data').then(function(r) { status = r.status; });
        ");
        drain();
        var result = engine.Execute("status");
        Assert.Equal(200.0, result.ToNumber());
    }

    [Fact]
    public void Fetch_Response_Ok()
    {
        var (engine, _, drain) = CreateTestEnvironment();
        engine.Execute(@"
            var isOk = false;
            fetch('https://example.com/data').then(function(r) { isOk = r.ok; });
        ");
        drain();
        var result = engine.Execute("isOk");
        Assert.True(result.ToBoolean());
    }

    [Fact]
    public void Fetch_Response_StatusText()
    {
        var (engine, _, drain) = CreateTestEnvironment();
        engine.Execute(@"
            var st = '';
            fetch('https://example.com/data').then(function(r) { st = r.statusText; });
        ");
        drain();
        var result = engine.Execute("st");
        Assert.Equal("OK", result.ToJsString());
    }

    [Fact]
    public void Fetch_Response_Url()
    {
        var (engine, _, drain) = CreateTestEnvironment();
        engine.Execute(@"
            var u = '';
            fetch('https://example.com/data').then(function(r) { u = r.url; });
        ");
        drain();
        var result = engine.Execute("u");
        Assert.Equal("https://example.com/data", result.ToJsString());
    }

    [Fact]
    public void Fetch_Response_Text()
    {
        var (engine, _, drain) = CreateTestEnvironment();
        engine.Execute(@"
            var body = '';
            fetch('https://example.com/data').then(function(r) {
                return r.text();
            }).then(function(t) { body = t; });
        ");
        drain();
        var result = engine.Execute("body");
        Assert.Equal("response body", result.ToJsString());
    }

    [Fact]
    public void Fetch_Response_Json()
    {
        var fetchFunc = (string url, string method, IReadOnlyList<KeyValuePair<string, string>>? headers, string? body) =>
        {
            return Task.FromResult(new FetchResult
            {
                Status = 200,
                StatusText = "OK",
                Body = "{\"name\":\"test\",\"value\":42}",
                Url = url,
                Headers = [new KeyValuePair<string, string>("Content-Type", "application/json")],
            });
        };

        var (engine, _, drain) = CreateTestEnvironment(fetchFunc);
        engine.Execute(@"
            var data = null;
            fetch('https://example.com/api').then(function(r) {
                return r.json();
            }).then(function(j) { data = j; });
        ");
        drain();
        var result = engine.Execute("data.name + ':' + data.value");
        Assert.Equal("test:42", result.ToJsString());
    }

    [Fact]
    public void Fetch_WithPostMethod()
    {
        string? capturedMethod = null;
        var fetchFunc = (string url, string method, IReadOnlyList<KeyValuePair<string, string>>? headers, string? body) =>
        {
            capturedMethod = method;
            return Task.FromResult(new FetchResult
            {
                Status = 201,
                StatusText = "Created",
                Body = "",
                Url = url,
                Headers = [],
            });
        };

        var (engine, _, drain) = CreateTestEnvironment(fetchFunc);
        engine.Execute(@"
            fetch('https://example.com/api', { method: 'POST', body: '{""key"":""value""}' })
        ");
        drain();
        Assert.Equal("POST", capturedMethod);
    }

    [Fact]
    public void Fetch_WithHeaders()
    {
        List<KeyValuePair<string, string>>? capturedHeaders = null;
        var fetchFunc = (string url, string method, IReadOnlyList<KeyValuePair<string, string>>? headers, string? body) =>
        {
            capturedHeaders = headers?.ToList();
            return Task.FromResult(new FetchResult
            {
                Status = 200,
                StatusText = "OK",
                Body = "",
                Url = url,
                Headers = [],
            });
        };

        var (engine, _, drain) = CreateTestEnvironment(fetchFunc);
        engine.Execute(@"
            fetch('https://example.com/api', {
                headers: { 'Authorization': 'Bearer token123' }
            })
        ");
        drain();
        Assert.NotNull(capturedHeaders);
        Assert.Contains(capturedHeaders, h => h.Key == "Authorization" && h.Value == "Bearer token123");
    }

    [Fact]
    public void Fetch_Error_RejectsPromise()
    {
        var fetchFunc = (string url, string method, IReadOnlyList<KeyValuePair<string, string>>? headers, string? body) =>
        {
            return Task.FromException<FetchResult>(new InvalidOperationException("Network error"));
        };

        var (engine, _, drain) = CreateTestEnvironment(fetchFunc);
        engine.Execute(@"
            var errorMsg = '';
            fetch('https://example.com/bad').then(
                function(r) { },
                function(err) { errorMsg = err; }
            );
        ");
        drain();
        var result = engine.Execute("errorMsg");
        Assert.Contains("Network error", result.ToJsString());
    }

    [Fact]
    public void Fetch_Response_Headers()
    {
        var (engine, _, drain) = CreateTestEnvironment();
        engine.Execute(@"
            var ct = '';
            fetch('https://example.com/data').then(function(r) {
                ct = r.headers['Content-Type'];
            });
        ");
        drain();
        var result = engine.Execute("ct");
        Assert.Equal("text/plain", result.ToJsString());
    }

    [Fact]
    public void Fetch_Response_NotOk()
    {
        var fetchFunc = (string url, string method, IReadOnlyList<KeyValuePair<string, string>>? headers, string? body) =>
        {
            return Task.FromResult(new FetchResult
            {
                Status = 404,
                StatusText = "Not Found",
                Body = "",
                Url = url,
                Headers = [],
            });
        };

        var (engine, _, drain) = CreateTestEnvironment(fetchFunc);
        engine.Execute(@"
            var isOk = true;
            var status = 0;
            fetch('https://example.com/missing').then(function(r) {
                isOk = r.ok;
                status = r.status;
            });
        ");
        drain();
        var result = engine.Execute("status + ':' + isOk");
        Assert.Equal("404:false", result.ToJsString());
    }

    [Fact]
    public void Fetch_NoArgs_Throws()
    {
        var (engine, _, _) = CreateTestEnvironment();
        Assert.Throws<SuperRender.EcmaScript.Runtime.Errors.JsTypeError>(() =>
        {
            engine.Execute("fetch()");
        });
    }
}
