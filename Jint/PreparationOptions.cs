using Jint.Native.RegExp;

namespace Jint;

public interface IPreparationOptions<out TParsingOptions>
    where TParsingOptions : IParsingOptions
{
    TParsingOptions ParsingOptions { get; }

    /// <summary>
    /// Gets or sets whether to fold constant expressions during the preparation phase.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    bool FoldConstants { get; init; }
}

public sealed record class ScriptPreparationOptions : IPreparationOptions<ScriptParsingOptions>
{
    private static readonly ParserOptions _defaultParserOptions = ScriptParsingOptions.Default.GetParserOptions() with
    {
        OnRegExp = Engine.DefaultCompileRegExpHandler,
    };

    public static readonly ScriptPreparationOptions Default = new();

    public ScriptParsingOptions ParsingOptions { get; init; } = ScriptParsingOptions.Default;

    /// <inheritdoc/>
    public bool FoldConstants { get; init; } = Engine.FoldConstantsOnPrepareByDefault;

    /// <summary>
    /// Whether to collect, during preparation, the set of identifier names the script references but does not itself
    /// declare — the names that will resolve in the global scope (or fail) at run time. The result is exposed as
    /// <see cref="Prepared{TProgram}.ReferencedGlobals"/>, which documents exactly what is and is not reported.
    /// Defaults to <see langword="false"/>.
    /// <para>
    /// The intended use is letting a host build only the ambient API a script actually asks for. When
    /// <see langword="false"/> nothing is collected, preparation cost is unchanged, and
    /// <see cref="Prepared{TProgram}.ReferencedGlobals"/> is <see langword="null"/> — which is distinguishable from
    /// an empty set, meaning "the script references no free names".
    /// </para>
    /// </summary>
    public bool CollectReferencedGlobals { get; init; }

    internal ParserOptions GetParserOptions() => ReferenceEquals(this, Default)
        ? _defaultParserOptions
        : ParsingOptions.ApplyTo(_defaultParserOptions, RegexCompilation.Compiled, Engine.DefaultRegexTimeout);
}

public sealed record class ModulePreparationOptions : IPreparationOptions<ModuleParsingOptions>
{
    private static readonly ParserOptions _defaultParserOptions = ModuleParsingOptions.Default.GetParserOptions() with
    {
        OnRegExp = Engine.DefaultCompileRegExpHandler,
    };

    public static readonly ModulePreparationOptions Default = new();

    public ModuleParsingOptions ParsingOptions { get; init; } = ModuleParsingOptions.Default;

    /// <inheritdoc/>
    public bool FoldConstants { get; init; } = Engine.FoldConstantsOnPrepareByDefault;

    /// <summary>
    /// Whether to collect, during preparation, the set of identifier names the module references but does not itself
    /// declare — the names that will resolve in the global scope (or fail) at run time. The result is exposed as
    /// <see cref="Prepared{TProgram}.ReferencedGlobals"/>, which documents exactly what is and is not reported.
    /// Defaults to <see langword="false"/>.
    /// <para>
    /// Import and export bindings are module-scope declarations, so an imported name is bound and never reported.
    /// When <see langword="false"/> nothing is collected, preparation cost is unchanged, and
    /// <see cref="Prepared{TProgram}.ReferencedGlobals"/> is <see langword="null"/> — which is distinguishable from
    /// an empty set, meaning "the module references no free names".
    /// </para>
    /// </summary>
    public bool CollectReferencedGlobals { get; init; }

    internal ParserOptions GetParserOptions() => ReferenceEquals(this, Default)
        ? _defaultParserOptions
        : ParsingOptions.ApplyTo(_defaultParserOptions, RegexCompilation.Compiled, Engine.DefaultRegexTimeout);
}
