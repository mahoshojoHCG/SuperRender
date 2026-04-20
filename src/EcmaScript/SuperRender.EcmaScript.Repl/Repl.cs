using SuperRender.EcmaScript.Runtime.Errors;
using SuperRender.EcmaScript.Engine;
using SuperRender.EcmaScript.NodeSimulator;
using SuperRender.EcmaScript.NodeSimulator.Modules;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Repl;

/// <summary>
/// Interactive Read-Eval-Print Loop mimicking the Node.js REPL.
/// </summary>
internal sealed class Repl
{
    private const string Prompt = "> ";
    private const string ContinuationPrompt = "... ";
    private const string ErrorColor = "\x1b[31m";
    private const string Reset = "\x1b[0m";

    private JsEngine _engine;
    private NodeRuntime _node;
    private readonly string[] _argv;
    private readonly LineEditor _editor = new();
    private readonly bool _useColors;

    public Repl() : this([]) { }

    public Repl(string[] argv)
    {
        _argv = argv;
        (_engine, _node) = CreateEngine(argv);
        _useColors = !Console.IsOutputRedirected
                     && System.Environment.GetEnvironmentVariable("NO_COLOR") is null;
    }

    /// <summary>
    /// Run the REPL loop until the user exits.
    /// </summary>
    public void Run()
    {
        while (true)
        {
            var input = ReadInput();
            if (input is null)
            {
                // EOF (Ctrl+D).
                Console.WriteLine();
                break;
            }

            var trimmed = input.TrimStart();

            if (trimmed.StartsWith('.'))
            {
                if (HandleDotCommand(trimmed))
                {
                    break;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            Evaluate(input);
        }
    }

    /// <summary>
    /// Execute a single script and print the result.
    /// </summary>
    public void ExecuteScript(string script)
    {
        try
        {
            var result = TryExecuteAsExpression(script);
            DrainEventLoop(waitForPending: true);
            // In non-interactive mode, only print if not undefined.
            if (result is not JsUndefined)
            {
                Console.WriteLine(ValueInspector.Inspect(result));
            }
        }
        catch (ProcessExitException exit)
        {
            System.Environment.ExitCode = exit.ExitCode;
        }
        catch (JsErrorBase ex)
        {
            PrintError(ex);
            System.Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// Read potentially multi-line input from the user.
    /// </summary>
    private string? ReadInput()
    {
        var line = _editor.ReadLine(Prompt);
        if (line is null)
        {
            return null;
        }

        // Check if the input looks incomplete (unmatched brackets, etc.).
        var accumulated = line;
        while (IsIncomplete(accumulated))
        {
            var continuation = _editor.ReadLine(ContinuationPrompt);
            if (continuation is null)
            {
                // EOF during continuation — execute what we have.
                break;
            }

            accumulated += "\n" + continuation;
        }

        return accumulated;
    }

    private void Evaluate(string input)
    {
        try
        {
            var result = TryExecuteAsExpression(input);
            DrainEventLoop(waitForPending: false);
            _editor.AddHistory(input);
            Console.WriteLine(ValueInspector.Inspect(result));
        }
        catch (ProcessExitException exit)
        {
            _editor.AddHistory(input);
            System.Environment.Exit(exit.ExitCode);
        }
        catch (JsErrorBase ex)
        {
            _editor.AddHistory(input);
            PrintError(ex);
        }
    }

    /// <summary>
    /// Drain process.nextTick, microtasks, setImmediate, and any due setTimeout/setInterval
    /// callbacks. When <paramref name="waitForPending"/> is true, also sleeps between polls
    /// while timers remain queued — used in batch (script / -e) mode so that scheduled
    /// callbacks run before the process exits.
    /// </summary>
    private void DrainEventLoop(bool waitForPending)
    {
        const int batchLimit = 10_000;
        int iter = 0;
        while (iter++ < batchLimit)
        {
            var fired = _node.DrainOnce();
            if (!waitForPending)
            {
                if (fired == 0) return;
                continue;
            }

            if (_node.Timers.PendingTimers == 0 && _node.Timers.PendingImmediates == 0 &&
                _node.Process.PendingNextTicks.Count == 0)
            {
                return;
            }

            if (fired == 0)
            {
                // Nothing due yet but timers still pending — sleep a bit.
                System.Threading.Thread.Sleep(1);
            }
        }
    }

    /// <summary>
    /// Execute code, automatically wrapping in parentheses when the input
    /// starts with '{' so that object literals are not mistaken for blocks.
    /// </summary>
    private JsValue TryExecuteAsExpression(string input)
    {
        if (input.TrimStart().StartsWith('{'))
        {
            try
            {
                return _engine.Execute("(" + input + ")");
            }
            catch (JsSyntaxError)
            {
                // Not a valid expression — fall through to evaluate as-is.
            }
        }

        return _engine.Execute(input);
    }

    /// <summary>
    /// Handle dot commands. Returns true if the REPL should exit.
    /// </summary>
    private bool HandleDotCommand(string input)
    {
        var command = input.Split(' ', 2)[0].ToLowerInvariant();
        switch (command)
        {
            case ".exit":
                return true;

            case ".help":
                PrintHelp();
                return false;

            case ".clear":
                (_engine, _node) = CreateEngine(_argv);
                Console.WriteLine("REPL context cleared.");
                return false;

            case ".editor":
                RunEditorMode();
                return false;

            default:
                Console.WriteLine($"Invalid REPL keyword '{command}'");
                return false;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine(".clear    Reset the REPL context");
        Console.WriteLine(".editor   Enter editor mode");
        Console.WriteLine(".exit     Exit the REPL");
        Console.WriteLine(".help     Print this help message");
    }

    private void RunEditorMode()
    {
        Console.WriteLine("// Entering editor mode (Ctrl+D to execute, Ctrl+C to cancel)");
        var lines = new List<string>();
        while (true)
        {
            var line = _editor.ReadLine("");
            if (line is null)
            {
                // Ctrl+D — execute accumulated input.
                break;
            }

            lines.Add(line);
        }

        if (lines.Count > 0)
        {
            var script = string.Join('\n', lines);
            Console.WriteLine();
            Evaluate(script);
        }
    }

    private void PrintError(JsErrorBase ex)
    {
        var prefix = ex switch
        {
            JsSyntaxError => "SyntaxError",
            JsReferenceError => "ReferenceError",
            JsTypeError => "TypeError",
            JsRangeError => "RangeError",
            JsUriError => "URIError",
            JsEvalError => "EvalError",
            _ => "Error"
        };

        var message = prefix + ": " + ex.Message;
        if (_useColors)
        {
            Console.Error.WriteLine(ErrorColor + message + Reset);
        }
        else
        {
            Console.Error.WriteLine(message);
        }
    }

    /// <summary>
    /// Detect whether a piece of input is syntactically incomplete
    /// by tracking unmatched delimiters outside of strings and comments.
    /// </summary>
    private static bool IsIncomplete(string input)
    {
        int braces = 0, brackets = 0, parens = 0;
        bool inSingleQuote = false, inDoubleQuote = false, inTemplate = false;
        bool inLineComment = false, inBlockComment = false;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            var next = i + 1 < input.Length ? input[i + 1] : '\0';

            // Handle line comments.
            if (inLineComment)
            {
                if (c == '\n')
                {
                    inLineComment = false;
                }

                continue;
            }

            // Handle block comments.
            if (inBlockComment)
            {
                if (c == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }

                continue;
            }

            // Handle strings.
            if (inSingleQuote)
            {
                if (c == '\\') { i++; continue; }
                if (c == '\'') { inSingleQuote = false; }
                continue;
            }

            if (inDoubleQuote)
            {
                if (c == '\\') { i++; continue; }
                if (c == '"') { inDoubleQuote = false; }
                continue;
            }

            if (inTemplate)
            {
                if (c == '\\') { i++; continue; }
                if (c == '`') { inTemplate = false; }
                continue;
            }

            // Start of comments.
            if (c == '/' && next == '/')
            {
                inLineComment = true;
                i++;
                continue;
            }

            if (c == '/' && next == '*')
            {
                inBlockComment = true;
                i++;
                continue;
            }

            // Start of strings.
            if (c == '\'') { inSingleQuote = true; continue; }
            if (c == '"') { inDoubleQuote = true; continue; }
            if (c == '`') { inTemplate = true; continue; }

            // Track delimiters.
            switch (c)
            {
                case '{': braces++; break;
                case '}': braces--; break;
                case '[': brackets++; break;
                case ']': brackets--; break;
                case '(': parens++; break;
                case ')': parens--; break;
            }
        }

        // Incomplete if any delimiter is unmatched or a string is unterminated.
        return braces > 0 || brackets > 0 || parens > 0
               || inSingleQuote || inDoubleQuote || inTemplate || inBlockComment;
    }

    private static (JsEngine engine, NodeRuntime node) CreateEngine(string[] argv)
    {
        var engine = new JsEngine();
        engine.SetConsoleOutput(Console.Out, Console.Error);
        var node = NodeSimulator.NodeSimulator.Install(engine, argv);
        return (engine, node);
    }
}
