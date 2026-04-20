using System.IO;
using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class FsWatchTests
{
    [Fact]
    public void Watch_ReturnsWatcherWithExpectedApi()
    {
        var dir = Path.Combine(Path.GetTempPath(), "srn_watch_api_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var (engine, _) = TestHost.Create();
            var code = $@"
                const w = require('fs').watch({System.Text.Json.JsonSerializer.Serialize(dir)});
                const ok = typeof w.on === 'function' && typeof w.off === 'function' && typeof w.close === 'function';
                w.close();
                ok";
            Assert.Equal("true", engine.RunString(code));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Watch_AcceptsInitialListener()
    {
        var dir = Path.Combine(Path.GetTempPath(), "srn_watch_listener_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var (engine, _) = TestHost.Create();
            var code = $@"
                const w = require('fs').watch({System.Text.Json.JsonSerializer.Serialize(dir)}, () => {{}});
                w.close();
                true";
            Assert.Equal("true", engine.RunString(code));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void FsWatcher_FiresChangeEventCrossPlatform()
    {
        // Direct C# test: FileSystemWatcher works on Windows, Linux, and macOS.
        // This validates the cross-platform primitive independently of JS threading.
        var dir = Path.Combine(Path.GetTempPath(), "srn_watch_direct_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "t.txt");
        File.WriteAllText(file, "a");
        try
        {
            using var watcher = new FileSystemWatcher(dir)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            };
            var fired = new System.Threading.ManualResetEventSlim(false);
            watcher.Changed += (_, _) => fired.Set();
            watcher.Created += (_, _) => fired.Set();
            watcher.Renamed += (_, _) => fired.Set();
            watcher.EnableRaisingEvents = true;
            System.Threading.Thread.Sleep(50);
            File.WriteAllText(file, "b");
            Assert.True(fired.Wait(TimeSpan.FromSeconds(3)), "FileSystemWatcher did not fire on this platform");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }
}
