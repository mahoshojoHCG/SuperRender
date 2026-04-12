using System.Text.Json;
using SuperRender.EcmaScript.Runtime;
using SuperRender.EcmaScript.Runtime.Builtins;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// JS fetch() API implementation. Returns a Promise that resolves with a JsResponse.
/// Uses delegates so the EcmaScript.Dom project remains dependency-free.
/// </summary>
internal static class JsFetchApi
{
    /// <summary>
    /// Creates the global fetch() function.
    /// </summary>
    public static JsFunction Create(
        Func<string, string, IReadOnlyList<KeyValuePair<string, string>>?, string?, Task<FetchResult>> fetchAsync,
        Action<Action> enqueueMainThread,
        Realm realm)
    {
        return JsFunction.CreateNative("fetch", (_, args) =>
        {
            if (args.Length == 0)
                throw new Runtime.Errors.JsTypeError("Failed to execute 'fetch': 1 argument required");

            var url = args[0].ToJsString();
            var method = "GET";
            string? body = null;
            List<KeyValuePair<string, string>>? headers = null;

            // Parse options object
            if (args.Length > 1 && args[1] is JsObject options)
            {
                var methodVal = options.Get("method");
                if (methodVal is not JsUndefined)
                    method = methodVal.ToJsString().ToUpperInvariant();

                var bodyVal = options.Get("body");
                if (bodyVal is not JsUndefined && bodyVal is not JsNull)
                    body = bodyVal.ToJsString();

                var headersVal = options.Get("headers");
                if (headersVal is JsObject headersObj)
                {
                    headers = [];
                    foreach (var key in headersObj.OwnPropertyKeys())
                    {
                        var val = headersObj.Get(key);
                        headers.Add(new KeyValuePair<string, string>(key, val.ToJsString()));
                    }
                }
            }

            // Create a pending promise
            var promise = new JsPromiseObject { Prototype = realm.PromisePrototype };

            // Start async fetch — fire and forget, result comes via main-thread queue
            Task.Run(async () =>
            {
                try
                {
                    var result = await fetchAsync(url, method, headers, body).ConfigureAwait(false);
                    enqueueMainThread(() =>
                    {
                        var jsResponse = new JsResponseWrapper(result, realm);
                        PromiseConstructor.ResolvePromise(promise, jsResponse, realm);
                    });
                }
                catch (Exception ex)
                {
                    enqueueMainThread(() =>
                    {
                        var errorObj = new JsObject { Prototype = realm.ErrorPrototype };
                        errorObj.Set("message", new JsString(ex.Message));
                        errorObj.Set("name", new JsString("TypeError"));
                        PromiseConstructor.RejectPromise(promise, errorObj);
                    });
                }
            }).ContinueWith(_ => { }, TaskScheduler.Default);

            return promise;
        }, 1);
    }
}

/// <summary>
/// Result of a fetch operation, passed from the Browser layer to the EcmaScript.Dom layer.
/// </summary>
public sealed class FetchResult
{
    public int Status { get; init; }
    public string StatusText { get; init; } = "";
    public string Url { get; init; } = "";
    public string Body { get; init; } = "";
    public IReadOnlyList<KeyValuePair<string, string>> Headers { get; init; } = [];
}

/// <summary>
/// JS Response object for the fetch API.
/// </summary>
internal sealed class JsResponseWrapper : JsObject
{
    private readonly FetchResult _result;
    private readonly Realm _realm;

    public JsResponseWrapper(FetchResult result, Realm realm)
    {
        _result = result;
        _realm = realm;
        Prototype = realm.ObjectPrototype;
        InstallProperties();
    }

    private void InstallProperties()
    {
        DefineOwnProperty("status", PropertyDescriptor.Data(JsNumber.Create(_result.Status)));
        DefineOwnProperty("statusText", PropertyDescriptor.Data(new JsString(_result.StatusText)));
        DefineOwnProperty("url", PropertyDescriptor.Data(new JsString(_result.Url)));
        DefineOwnProperty("ok", PropertyDescriptor.Data(
            _result.Status >= 200 && _result.Status < 300 ? True : False));

        // headers object
        var headersObj = new JsObject { Prototype = _realm.ObjectPrototype };
        foreach (var h in _result.Headers)
            headersObj.Set(h.Key, new JsString(h.Value));
        DefineOwnProperty("headers", PropertyDescriptor.Data(headersObj));

        // .text() -> Promise<string>
        DefineOwnProperty("text", PropertyDescriptor.Data(
            JsFunction.CreateNative("text", (_, _) =>
            {
                var p = new JsPromiseObject { Prototype = _realm.PromisePrototype };
                PromiseConstructor.ResolvePromise(p, new JsString(_result.Body), _realm);
                return p;
            }, 0)));

        // .json() -> Promise<JsValue>
        DefineOwnProperty("json", PropertyDescriptor.Data(
            JsFunction.CreateNative("json", (_, _) =>
            {
                var p = new JsPromiseObject { Prototype = _realm.PromisePrototype };
                try
                {
                    var parsed = ParseJson(_result.Body);
                    PromiseConstructor.ResolvePromise(p, parsed, _realm);
                }
                catch (Exception ex)
                {
                    var jsonErr = new JsObject { Prototype = _realm.ErrorPrototype };
                    jsonErr.Set("message", new JsString(ex.Message));
                    PromiseConstructor.RejectPromise(p, jsonErr);
                }
                return p;
            }, 0)));
    }

    private static JsValue ParseJson(string text)
    {
        using var doc = JsonDocument.Parse(text);
        return ConvertElement(doc.RootElement);
    }

    private static JsValue ConvertElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => Null,
            JsonValueKind.True => True,
            JsonValueKind.False => False,
            JsonValueKind.Number => JsNumber.Create(element.GetDouble()),
            JsonValueKind.String => new JsString(element.GetString() ?? ""),
            JsonValueKind.Array => ConvertArray(element),
            JsonValueKind.Object => ConvertObject(element),
            _ => Undefined,
        };
    }

    private static JsArray ConvertArray(JsonElement element)
    {
        var arr = new JsArray();
        foreach (var item in element.EnumerateArray())
            arr.Push(ConvertElement(item));
        return arr;
    }

    private static JsObject ConvertObject(JsonElement element)
    {
        var obj = new JsObject();
        foreach (var prop in element.EnumerateObject())
            obj.Set(prop.Name, ConvertElement(prop.Value));
        return obj;
    }
}
