using System.Net;
using SuperRender.Browser.Networking;
using Xunit;

namespace SuperRender.Browser.Tests;

public class SecurityPolicyTests
{
    [Fact]
    public void IsSameOrigin_SameSchemeHostPort_ReturnsTrue()
    {
        var a = new Uri("https://example.com:443/page1");
        var b = new Uri("https://example.com:443/page2");

        Assert.True(SecurityPolicy.IsSameOrigin(a, b));
    }

    [Fact]
    public void IsSameOrigin_DifferentScheme_ReturnsFalse()
    {
        var a = new Uri("https://example.com");
        var b = new Uri("http://example.com");

        Assert.False(SecurityPolicy.IsSameOrigin(a, b));
    }

    [Fact]
    public void IsSameOrigin_DifferentHost_ReturnsFalse()
    {
        var a = new Uri("https://example.com");
        var b = new Uri("https://other.com");

        Assert.False(SecurityPolicy.IsSameOrigin(a, b));
    }

    [Fact]
    public void IsSameOrigin_DifferentPort_ReturnsFalse()
    {
        var a = new Uri("https://example.com:443");
        var b = new Uri("https://example.com:8080");

        Assert.False(SecurityPolicy.IsSameOrigin(a, b));
    }

    [Fact]
    public void AllowCrossOriginResponse_WildcardHeader_ReturnsTrue()
    {
        var requestUri = new Uri("https://api.example.com/data");
        var originUri = new Uri("https://example.com");
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("Access-Control-Allow-Origin", "*");

        Assert.True(SecurityPolicy.AllowCrossOriginResponse(requestUri, originUri, response));
    }

    [Fact]
    public void AllowCrossOriginResponse_ExactMatchHeader_ReturnsTrue()
    {
        var requestUri = new Uri("https://api.example.com/data");
        var originUri = new Uri("https://example.com");
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("Access-Control-Allow-Origin", "https://example.com");

        Assert.True(SecurityPolicy.AllowCrossOriginResponse(requestUri, originUri, response));
    }

    [Fact]
    public void AllowCrossOriginResponse_NoHeader_ReturnsFalse()
    {
        var requestUri = new Uri("https://api.example.com/data");
        var originUri = new Uri("https://example.com");
        var response = new HttpResponseMessage(HttpStatusCode.OK);

        Assert.False(SecurityPolicy.AllowCrossOriginResponse(requestUri, originUri, response));
    }

    [Fact]
    public void AllowCrossOriginResponse_SameOrigin_AlwaysAllowed()
    {
        var requestUri = new Uri("https://example.com/data");
        var originUri = new Uri("https://example.com");
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        // No CORS header at all

        Assert.True(SecurityPolicy.AllowCrossOriginResponse(requestUri, originUri, response));
    }
}
