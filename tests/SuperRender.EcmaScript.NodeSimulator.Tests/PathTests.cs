using SuperRender.EcmaScript.NodeSimulator.Modules;
using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class PathPosixTests
{
    [Fact]
    public void Join_CombinesSegments()
    {
        Assert.Equal("a/b/c", PathModule.Join(["a", "b", "c"], win32: false));
        Assert.Equal("a/b/c", PathModule.Join(["a/", "/b", "c"], win32: false));
    }

    [Fact]
    public void Normalize_CollapsesDots()
    {
        Assert.Equal("a/c", PathModule.Normalize("a/b/../c", win32: false));
        Assert.Equal("/a/b", PathModule.Normalize("/a//b/./", win32: false).TrimEnd('/'));
    }

    [Fact]
    public void IsAbsolute_PosixPath()
    {
        Assert.True(PathModule.IsAbsolute("/foo", win32: false));
        Assert.False(PathModule.IsAbsolute("foo/bar", win32: false));
    }

    [Fact]
    public void Dirname_ReturnsParent()
    {
        Assert.Equal("/a/b", PathModule.Dirname("/a/b/c", win32: false));
        Assert.Equal("/", PathModule.Dirname("/a", win32: false));
    }

    [Fact]
    public void Basename_WithExt_StripsExtension()
    {
        Assert.Equal("file", PathModule.Basename("/tmp/file.txt", ".txt", win32: false));
        Assert.Equal("file.txt", PathModule.Basename("/tmp/file.txt", null, win32: false));
    }

    [Fact]
    public void Extname_ReturnsSuffix()
    {
        Assert.Equal(".txt", PathModule.Extname("foo.txt", win32: false));
        Assert.Equal("", PathModule.Extname("README", win32: false));
        Assert.Equal("", PathModule.Extname(".hidden", win32: false));
    }

    [Fact]
    public void Relative_ProducesRelativePath()
    {
        var rel = PathModule.Relative("/a/b/c", "/a/x/y", win32: false);
        Assert.Equal("../../x/y", rel);
    }
}

public class PathWin32Tests
{
    [Fact]
    public void IsAbsolute_WithDriveLetter()
    {
        Assert.True(PathModule.IsAbsolute("C:\\foo", win32: true));
        Assert.True(PathModule.IsAbsolute("C:/foo", win32: true));
        Assert.False(PathModule.IsAbsolute("foo\\bar", win32: true));
    }

    [Fact]
    public void Join_UsesBackslash()
    {
        Assert.Equal("a\\b\\c", PathModule.Join(["a", "b", "c"], win32: true));
    }

    [Fact]
    public void Extname_ReturnsSuffix()
    {
        Assert.Equal(".txt", PathModule.Extname("C:\\foo.txt", win32: true));
    }
}

public class PathJsApiTests
{
    [Fact]
    public void Require_Path_ExposesSepAndDelimiter()
    {
        var (engine, _) = TestHost.Create();
        var typeofSep = engine.RunString("typeof require('path').sep");
        Assert.Equal("string", typeofSep);
    }

    [Fact]
    public void Require_Path_ParseReturnsComponents()
    {
        var (engine, _) = TestHost.Create();
        var name = engine.RunString("require('path/posix').parse('/a/b/file.txt').name");
        Assert.Equal("file", name);
        var ext = engine.RunString("require('path/posix').parse('/a/b/file.txt').ext");
        Assert.Equal(".txt", ext);
    }

    [Fact]
    public void Require_PathPosix_Join_DoesNotDependOnHost()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal("a/b", engine.RunString("require('path/posix').join('a', 'b')"));
    }
}
