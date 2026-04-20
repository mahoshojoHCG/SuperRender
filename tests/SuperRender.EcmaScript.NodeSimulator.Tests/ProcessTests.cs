using SuperRender.EcmaScript.NodeSimulator.Modules;
using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class ProcessTests
{
    [Fact]
    public void Process_Exposes_Argv()
    {
        var (engine, _) = TestHost.Create("--flag", "value");
        var argv0 = engine.RunString("process.argv[2]");
        var argv1 = engine.RunString("process.argv[3]");
        Assert.Equal("--flag", argv0);
        Assert.Equal("value", argv1);
    }

    [Fact]
    public void Process_Platform_MatchesHost()
    {
        var (engine, _) = TestHost.Create();
        var platform = engine.RunString("process.platform");
        Assert.Equal(OsModule.GetPlatform(), platform);
    }

    [Fact]
    public void Process_Env_ReflectsEnvironment()
    {
        var (engine, _) = TestHost.Create();
        System.Environment.SetEnvironmentVariable("NODE_SIM_TEST_VAR", "hello");
        try
        {
            var (e2, _) = TestHost.Create();
            Assert.Equal("hello", e2.RunString("process.env.NODE_SIM_TEST_VAR"));
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("NODE_SIM_TEST_VAR", null);
        }
        _ = engine;
    }

    [Fact]
    public void Process_Cwd_ReturnsCurrentDirectory()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal(System.Environment.CurrentDirectory, engine.RunString("process.cwd()"));
    }

    [Fact]
    public void Process_NextTick_QueuesCallback()
    {
        var (engine, node) = TestHost.Create();
        engine.Execute("globalThis.__result = 0; process.nextTick(() => { globalThis.__result = 42; });");
        Assert.Equal(0, (int)engine.Execute("globalThis.__result").ToNumber());
        node.DrainOnce();
        Assert.Equal(42, (int)engine.Execute("globalThis.__result").ToNumber());
    }

    [Fact]
    public void Process_Exit_ThrowsProcessExit()
    {
        var (engine, _) = TestHost.Create();
        var ex = Assert.Throws<ProcessExitException>(() => engine.Execute("process.exit(7)"));
        Assert.Equal(7, ex.ExitCode);
    }

    [Fact]
    public void Process_Hrtime_ReturnsTwoNumberArray()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal(2, (int)engine.Execute("process.hrtime().length").ToNumber());
    }

    [Fact]
    public void Process_Stdout_Write_CallsWriter()
    {
        var (engine, node) = TestHost.Create();
        var sw = new System.IO.StringWriter();
        node.Process.StdOut = sw;
        engine.Execute("process.stdout.write('hello')");
        Assert.Equal("hello", sw.ToString());
    }

    [Fact]
    public void Process_On_RegistersListener()
    {
        var (engine, node) = TestHost.Create();
        engine.Execute("globalThis.__fired = 0; process.on('exit', () => { globalThis.__fired = 1; });");
        Assert.Throws<ProcessExitException>(() => engine.Execute("process.exit(0)"));
        Assert.Equal(1, (int)engine.Execute("globalThis.__fired").ToNumber());
        _ = node;
    }
}
