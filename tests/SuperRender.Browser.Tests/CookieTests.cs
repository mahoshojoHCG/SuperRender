using SuperRender.Browser.Networking;
using Xunit;

namespace SuperRender.Browser.Tests;

public class CookieTests
{
    [Fact]
    public void SetCookie_BasicParsing_StoresCookie()
    {
        var jar = new CookieJar();
        var origin = new Uri("https://example.com/page");
        jar.SetCookie(origin, "name=value");
        var cookies = jar.GetCookiesForRequest(new Uri("https://example.com/"));
        Assert.Equal("name=value", cookies);
    }

    [Fact]
    public void SetCookie_WithDomain_MatchesSubdomain()
    {
        var jar = new CookieJar();
        var origin = new Uri("https://www.example.com/");
        jar.SetCookie(origin, "sid=abc; Domain=example.com");
        var cookies = jar.GetCookiesForRequest(new Uri("https://sub.example.com/"));
        Assert.Equal("sid=abc", cookies);
    }

    [Fact]
    public void SetCookie_WithPath_MatchesPrefixOnly()
    {
        var jar = new CookieJar();
        var origin = new Uri("https://example.com/app");
        jar.SetCookie(origin, "key=val; Path=/app");
        Assert.Contains("key=val", jar.GetCookiesForRequest(new Uri("https://example.com/app/page")));
        Assert.Empty(jar.GetCookiesForRequest(new Uri("https://example.com/other")));
    }

    [Fact]
    public void SetCookie_SecureFlag_BlockedOnHttp()
    {
        var jar = new CookieJar();
        var origin = new Uri("https://example.com/");
        jar.SetCookie(origin, "secure_key=secure_val; Secure");
        Assert.Empty(jar.GetCookiesForRequest(new Uri("http://example.com/")));
        Assert.Contains("secure_key=secure_val", jar.GetCookiesForRequest(new Uri("https://example.com/")));
    }

    [Fact]
    public void SetCookie_HttpOnly_NotVisibleToScript()
    {
        var jar = new CookieJar();
        var origin = new Uri("https://example.com/");
        jar.SetCookie(origin, "httponly_key=val; HttpOnly");
        // HTTP requests should still see it
        Assert.Contains("httponly_key=val", jar.GetCookiesForRequest(origin));
        // Script should NOT see it
        Assert.Empty(jar.GetCookiesForScript(origin));
    }

    [Fact]
    public void SetCookie_ScriptCannotSetHttpOnly()
    {
        var jar = new CookieJar();
        var origin = new Uri("https://example.com/");
        jar.SetCookieFromScript(origin, "httponly_key=val; HttpOnly");
        Assert.Empty(jar.GetCookiesForRequest(origin));
    }

    [Fact]
    public void SetCookie_MaxAgeZero_DeletesCookie()
    {
        var jar = new CookieJar();
        var origin = new Uri("https://example.com/");
        jar.SetCookie(origin, "to_delete=val");
        Assert.Contains("to_delete=val", jar.GetCookiesForRequest(origin));
        jar.SetCookie(origin, "to_delete=; Max-Age=0");
        Assert.Empty(jar.GetCookiesForRequest(origin));
    }

    [Fact]
    public void SetCookie_SameSiteStrict_BlocksCrossSite()
    {
        var jar = new CookieJar();
        var origin = new Uri("https://example.com/");
        jar.SetCookie(origin, "strict=val; SameSite=Strict");
        // Same-site request: should include cookie
        Assert.Contains("strict=val", jar.GetCookiesForRequest(origin, origin));
        // Cross-site request: should block
        var crossOrigin = new Uri("https://other.com/");
        Assert.Empty(jar.GetCookiesForRequest(origin, crossOrigin));
    }

    [Fact]
    public void SetCookie_MultipleCookies_ReturnsAll()
    {
        var jar = new CookieJar();
        var origin = new Uri("https://example.com/");
        jar.SetCookie(origin, "a=1");
        jar.SetCookie(origin, "b=2");
        var cookies = jar.GetCookiesForRequest(origin);
        Assert.Contains("a=1", cookies);
        Assert.Contains("b=2", cookies);
    }

    [Fact]
    public void SetCookie_Replaces_ExistingCookie()
    {
        var jar = new CookieJar();
        var origin = new Uri("https://example.com/");
        jar.SetCookie(origin, "key=old");
        jar.SetCookie(origin, "key=new");
        var cookies = jar.GetCookiesForRequest(origin);
        Assert.Equal("key=new", cookies);
    }

    [Fact]
    public void DomainMatches_ExactMatch_ReturnsTrue()
    {
        Assert.True(CookieJar.DomainMatches("example.com", "example.com"));
    }

    [Fact]
    public void DomainMatches_SubdomainMatch_ReturnsTrue()
    {
        Assert.True(CookieJar.DomainMatches("sub.example.com", "example.com"));
    }

    [Fact]
    public void DomainMatches_DifferentDomain_ReturnsFalse()
    {
        Assert.False(CookieJar.DomainMatches("other.com", "example.com"));
    }

    [Fact]
    public void PathMatches_ExactMatch_ReturnsTrue()
    {
        Assert.True(CookieJar.PathMatches("/app", "/app"));
    }

    [Fact]
    public void PathMatches_PrefixMatch_ReturnsTrue()
    {
        Assert.True(CookieJar.PathMatches("/app/sub", "/app"));
    }

    [Fact]
    public void PathMatches_NoMatch_ReturnsFalse()
    {
        Assert.False(CookieJar.PathMatches("/other", "/app"));
    }

    [Fact]
    public void CookieParser_ParsesAllAttributes()
    {
        var cookie = CookieParser.Parse(
            "test=value; Domain=.example.com; Path=/api; Secure; HttpOnly; SameSite=Strict",
            new Uri("https://example.com/"));

        Assert.NotNull(cookie);
        Assert.Equal("test", cookie.Name);
        Assert.Equal("value", cookie.Value);
        Assert.Equal("example.com", cookie.Domain);
        Assert.Equal("/api", cookie.Path);
        Assert.True(cookie.Secure);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(SameSitePolicy.Strict, cookie.SameSite);
    }

    [Fact]
    public void CookieParser_InvalidHeader_ReturnsNull()
    {
        var cookie = CookieParser.Parse("", new Uri("https://example.com/"));
        Assert.Null(cookie);
    }
}
