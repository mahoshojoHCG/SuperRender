using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class FsTests : System.IDisposable
{
    private readonly string _tmp;

    public FsTests()
    {
        _tmp = Path.Combine(Path.GetTempPath(), "nodesim-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tmp);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tmp)) Directory.Delete(_tmp, recursive: true); } catch (IOException) { }
        System.GC.SuppressFinalize(this);
    }

    [Fact]
    public void WriteFileSync_Then_ReadFileSync_RoundTrips()
    {
        var (engine, _) = TestHost.Create();
        var file = Path.Combine(_tmp, "a.txt").Replace("\\", "\\\\", System.StringComparison.Ordinal);
        engine.Execute($"const fs = require('fs'); fs.writeFileSync('{file}', 'hello');");
        var text = engine.RunString($"require('fs').readFileSync('{file}', 'utf8')");
        Assert.Equal("hello", text);
    }

    [Fact]
    public void ReadFileSync_Default_ReturnsBuffer()
    {
        var file = Path.Combine(_tmp, "b.txt");
        File.WriteAllText(file, "xy");
        var (engine, _) = TestHost.Create();
        var jsFile = file.Replace("\\", "\\\\", System.StringComparison.Ordinal);
        var len = (int)engine.Execute($"require('fs').readFileSync('{jsFile}').length").ToNumber();
        Assert.Equal(2, len);
    }

    [Fact]
    public void ExistsSync_True_ForExistingFile()
    {
        var file = Path.Combine(_tmp, "c.txt");
        File.WriteAllText(file, "");
        var (engine, _) = TestHost.Create();
        var jsFile = file.Replace("\\", "\\\\", System.StringComparison.Ordinal);
        Assert.True(engine.Execute($"require('fs').existsSync('{jsFile}')").ToBoolean());
        Assert.False(engine.Execute("require('fs').existsSync('/no/such/path/xyz')").ToBoolean());
    }

    [Fact]
    public void MkdirSync_Recursive_CreatesTree()
    {
        var nested = Path.Combine(_tmp, "x", "y", "z");
        var (engine, _) = TestHost.Create();
        var jsNested = nested.Replace("\\", "\\\\", System.StringComparison.Ordinal);
        engine.Execute($"require('fs').mkdirSync('{jsNested}', {{ recursive: true }});");
        Assert.True(Directory.Exists(nested));
    }

    [Fact]
    public void ReaddirSync_ListsEntries()
    {
        File.WriteAllText(Path.Combine(_tmp, "1.txt"), "");
        File.WriteAllText(Path.Combine(_tmp, "2.txt"), "");
        var (engine, _) = TestHost.Create();
        var jsTmp = _tmp.Replace("\\", "\\\\", System.StringComparison.Ordinal);
        var len = (int)engine.Execute($"require('fs').readdirSync('{jsTmp}').length").ToNumber();
        Assert.Equal(2, len);
    }

    [Fact]
    public void StatSync_ReturnsFileFlag()
    {
        var file = Path.Combine(_tmp, "s.txt");
        File.WriteAllText(file, "abc");
        var (engine, _) = TestHost.Create();
        var jsFile = file.Replace("\\", "\\\\", System.StringComparison.Ordinal);
        Assert.True(engine.Execute($"require('fs').statSync('{jsFile}').isFile()").ToBoolean());
        Assert.Equal(3, (int)engine.Execute($"require('fs').statSync('{jsFile}').size").ToNumber());
    }

    [Fact]
    public void Promises_ReadFile_ResolvesValue()
    {
        var file = Path.Combine(_tmp, "p.txt");
        File.WriteAllText(file, "pv");
        var (engine, node) = TestHost.Create();
        var jsFile = file.Replace("\\", "\\\\", System.StringComparison.Ordinal);
        engine.Execute($@"
            globalThis.__r = null;
            require('fs').promises.readFile('{jsFile}', 'utf8').then(v => {{ globalThis.__r = v; }});
        ");
        // Flush microtasks
        for (int i = 0; i < 4; i++) node.DrainOnce();
        Assert.Equal("pv", engine.RunString("globalThis.__r"));
    }
}
