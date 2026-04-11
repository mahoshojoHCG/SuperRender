using SuperRender.EcmaScript.Repl;

const string version = "1.0.0";

if (args.Length == 0)
{
    // Interactive REPL mode.
    PrintBanner();
    var repl = new Repl();
    repl.Run();
    return;
}

if (args[0] is "-e" or "--eval" && args.Length > 1)
{
    // Evaluate a single expression.
    var repl = new Repl();
    repl.ExecuteScript(string.Join(' ', args.Skip(1)));
    return;
}

if (args[0] is "-h" or "--help")
{
    PrintUsage();
    return;
}

if (args[0] is "-v" or "--version")
{
    Console.WriteLine(version);
    return;
}

// Treat the first argument as a script file path.
var filePath = args[0];
if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"Error: File not found: {filePath}");
    Environment.ExitCode = 1;
    return;
}

var script = File.ReadAllText(filePath);
var runner = new Repl();
runner.ExecuteScript(script);

static void PrintBanner()
{
    Console.WriteLine($"Welcome to SuperRender EcmaScript v{version} (ES2025).");
    Console.WriteLine("Type \".help\" for more information.");
}

static void PrintUsage()
{
    Console.WriteLine($"SuperRender EcmaScript Console v{version}");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  srjs                     Start interactive REPL");
    Console.WriteLine("  srjs <file.js>           Execute a script file");
    Console.WriteLine("  srjs -e <code>           Evaluate code");
    Console.WriteLine("  srjs --eval <code>       Evaluate code");
    Console.WriteLine("  srjs -v, --version       Print version");
    Console.WriteLine("  srjs -h, --help          Show this help");
}
