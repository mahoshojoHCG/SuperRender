using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class OsTests
{
    private static readonly string[] ValidEndianness = ["LE", "BE"];

    [Fact]
    public void Platform_MatchesHost()
    {
        var (engine, _) = TestHost.Create();
        var expected = System.OperatingSystem.IsMacOS() ? "darwin"
            : System.OperatingSystem.IsLinux() ? "linux"
            : System.OperatingSystem.IsWindows() ? "win32" : null;
        if (expected is null) return;
        Assert.Equal(expected, engine.RunString("require('os').platform()"));
    }

    [Fact]
    public void EOL_IsStringSeparator()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal(System.Environment.NewLine, engine.RunString("require('os').EOL"));
    }

    [Fact]
    public void Homedir_Returns_UserProfile()
    {
        var (engine, _) = TestHost.Create();
        var homedir = engine.RunString("require('os').homedir()");
        Assert.False(string.IsNullOrEmpty(homedir));
    }

    [Fact]
    public void Endianness_ReturnsLEorBE()
    {
        var (engine, _) = TestHost.Create();
        var e = engine.RunString("require('os').endianness()");
        Assert.Contains(e, ValidEndianness);
    }

    [Fact]
    public void Cpus_ReturnsArrayOfProcessors()
    {
        var (engine, _) = TestHost.Create();
        var len = (int)engine.Execute("require('os').cpus().length").ToNumber();
        Assert.Equal(System.Environment.ProcessorCount, len);
    }
}
