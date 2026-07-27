using System.Diagnostics.CodeAnalysis;

namespace Jint;

public readonly struct Prepared<TProgram> where TProgram : Program
{
    internal Prepared(TProgram program, ParserOptions parserOptions, ReferencedGlobals? referencedGlobals = null)
    {
        Program = program;
        ParserOptions = parserOptions;
        ReferencedGlobals = referencedGlobals;
    }

    public TProgram? Program { get; }

    public ParserOptions? ParserOptions { get; }

    /// <summary>
    /// The identifier names this program references but does not declare, when it was prepared with
    /// <see cref="ScriptPreparationOptions.CollectReferencedGlobals"/> (or
    /// <see cref="ModulePreparationOptions.CollectReferencedGlobals"/>) set; otherwise <see langword="null"/>.
    /// <para>
    /// <see langword="null"/> means "not collected" and is distinct from an empty collection, which means the program
    /// references no free names. Like the <see cref="Prepared{TProgram}"/> itself, the returned collection is
    /// immutable and safe to share across engines and threads.
    /// </para>
    /// </summary>
    public ReferencedGlobals? ReferencedGlobals { get; }

    [MemberNotNullWhen(true, nameof(Program))]
    [MemberNotNullWhen(true, nameof(ParserOptions))]
    public bool IsValid => Program is not null;
}
