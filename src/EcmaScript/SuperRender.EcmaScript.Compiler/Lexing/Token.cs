namespace SuperRender.EcmaScript.Compiler.Lexing;

public sealed class Token
{
    public required TokenType Type { get; init; }
    public required string Value { get; init; }
    public double NumericValue { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public bool PrecedingLineTerminator { get; init; }

    public override string ToString() => $"{Type}({Value}) at {Line}:{Column}";
}
