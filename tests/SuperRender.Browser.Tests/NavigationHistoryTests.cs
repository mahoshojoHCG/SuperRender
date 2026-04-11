using Xunit;

namespace SuperRender.Browser.Tests;

public class NavigationHistoryTests
{
    [Fact]
    public void Empty_CanGoBack_ReturnsFalse()
    {
        var history = new NavigationHistory();
        Assert.False(history.CanGoBack);
    }

    [Fact]
    public void Empty_CanGoForward_ReturnsFalse()
    {
        var history = new NavigationHistory();
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void Empty_CurrentEntry_ReturnsNull()
    {
        var history = new NavigationHistory();
        Assert.Null(history.CurrentEntry);
    }

    [Fact]
    public void PushOne_CurrentEntry_MatchesPushedUri()
    {
        var history = new NavigationHistory();
        var uri = new Uri("https://example.com");
        history.Push(uri);
        Assert.Equal(uri, history.CurrentEntry);
    }

    [Fact]
    public void PushOne_CanGoBack_ReturnsFalse()
    {
        var history = new NavigationHistory();
        history.Push(new Uri("https://example.com"));
        Assert.False(history.CanGoBack);
    }

    [Fact]
    public void PushTwo_CanGoBack_ReturnsTrue()
    {
        var history = new NavigationHistory();
        history.Push(new Uri("https://example.com/1"));
        history.Push(new Uri("https://example.com/2"));
        Assert.True(history.CanGoBack);
    }

    [Fact]
    public void GoBack_ReturnsPreviousUri()
    {
        var history = new NavigationHistory();
        var first = new Uri("https://example.com/1");
        var second = new Uri("https://example.com/2");
        history.Push(first);
        history.Push(second);

        var result = history.GoBack();
        Assert.Equal(first, result);
        Assert.Equal(first, history.CurrentEntry);
    }

    [Fact]
    public void GoForward_ReturnsNextUri()
    {
        var history = new NavigationHistory();
        var first = new Uri("https://example.com/1");
        var second = new Uri("https://example.com/2");
        history.Push(first);
        history.Push(second);
        history.GoBack();

        var result = history.GoForward();
        Assert.Equal(second, result);
        Assert.Equal(second, history.CurrentEntry);
    }

    [Fact]
    public void PushAfterGoBack_TruncatesForwardHistory()
    {
        var history = new NavigationHistory();
        history.Push(new Uri("https://example.com/1"));
        history.Push(new Uri("https://example.com/2"));
        history.Push(new Uri("https://example.com/3"));
        history.GoBack();
        history.GoBack();

        var newUri = new Uri("https://example.com/new");
        history.Push(newUri);

        Assert.Equal(newUri, history.CurrentEntry);
        Assert.False(history.CanGoForward);
        Assert.True(history.CanGoBack);
    }

    [Fact]
    public void GoBack_AtStart_ReturnsNull()
    {
        var history = new NavigationHistory();
        history.Push(new Uri("https://example.com"));

        var result = history.GoBack();
        Assert.Null(result);
    }

    [Fact]
    public void GoForward_AtEnd_ReturnsNull()
    {
        var history = new NavigationHistory();
        history.Push(new Uri("https://example.com"));

        var result = history.GoForward();
        Assert.Null(result);
    }
}
