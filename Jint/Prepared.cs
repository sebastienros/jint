using System.Diagnostics.CodeAnalysis;

namespace Jint;

public readonly struct Prepared<TProgram> where TProgram : Program
{
    internal Prepared(TProgram program, ParserOptions parserOptions)
    {
        Program = program;
        ParserOptions = parserOptions;
    }

    public TProgram? Program { get; }

    public ParserOptions? ParserOptions { get; }

    [MemberNotNullWhen(true, nameof(Program))]
    [MemberNotNullWhen(true, nameof(ParserOptions))]
    public bool IsValid => Program is not null;
}
