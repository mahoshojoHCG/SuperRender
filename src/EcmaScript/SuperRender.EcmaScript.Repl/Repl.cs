using SuperRender.EcmaScript.Runtime.Errors;
using SuperRender.EcmaScript.Engine;
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
    private readonly LineEditor _editor = new();
    private readonly bool _useColors;

    public Repl()
    {
        _engine = CreateEngine();
        _useColors = !System.Console.IsOutputRedirected
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
                System.Console.WriteLine();
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
            // In non-interactive mode, only print if not undefined.
            if (result is not JsUndefined)
            {
                System.Console.WriteLine(ValueInspector.Inspect(result));
            }
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
            _editor.AddHistory(input);
            System.Console.WriteLine(ValueInspector.Inspect(result));
        }
        catch (JsErrorBase ex)
        {
            _editor.AddHistory(input);
            PrintError(ex);
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
                _engine = CreateEngine();
                System.Console.WriteLine("REPL context cleared.");
                return false;

            case ".editor":
                RunEditorMode();
                return false;

            default:
                System.Console.WriteLine($"Invalid REPL keyword '{command}'");
                return false;
        }
    }

    private static void PrintHelp()
    {
        System.Console.WriteLine(".clear    Reset the REPL context");
        System.Console.WriteLine(".editor   Enter editor mode");
        System.Console.WriteLine(".exit     Exit the REPL");
        System.Console.WriteLine(".help     Print this help message");
    }

    private void RunEditorMode()
    {
        System.Console.WriteLine("// Entering editor mode (Ctrl+D to execute, Ctrl+C to cancel)");
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
            System.Console.WriteLine();
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
            System.Console.Error.WriteLine(ErrorColor + message + Reset);
        }
        else
        {
            System.Console.Error.WriteLine(message);
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

    private static JsEngine CreateEngine()
    {
        var engine = new JsEngine();
        engine.SetConsoleOutput(System.Console.Out, System.Console.Error);

        // Install Node-like global helpers.
        engine.SetValue("global", engine.GetValue("undefined") is JsUndefined
            ? engine.Execute("({})")
            : engine.GetValue("global"));

        return engine;
    }
}
