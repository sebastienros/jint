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
        RegExpParseMode = RegExpParseMode.AdaptToCompiled,
    };

    public static readonly ScriptPreparationOptions Default = new();

    public ScriptParsingOptions ParsingOptions { get; init; } = ScriptParsingOptions.Default;

    /// <inheritdoc/>
    public bool FoldConstants { get; init; } = Engine.FoldConstantsOnPrepareByDefault;

    internal ParserOptions GetParserOptions() => ReferenceEquals(this, Default)
        ? _defaultParserOptions
        : ParsingOptions.ApplyTo(_defaultParserOptions, _defaultParserOptions.RegExpParseMode, _defaultParserOptions.RegexTimeout);
}

public sealed record class ModulePreparationOptions : IPreparationOptions<ModuleParsingOptions>
{
    private static readonly ParserOptions _defaultParserOptions = ModuleParsingOptions.Default.GetParserOptions() with
    {
        RegExpParseMode = RegExpParseMode.AdaptToCompiled
    };

    public static readonly ModulePreparationOptions Default = new();

    public ModuleParsingOptions ParsingOptions { get; init; } = ModuleParsingOptions.Default;

    /// <inheritdoc/>
    public bool FoldConstants { get; init; } = Engine.FoldConstantsOnPrepareByDefault;

    internal ParserOptions GetParserOptions() => ReferenceEquals(this, Default)
        ? _defaultParserOptions
        : ParsingOptions.ApplyTo(_defaultParserOptions, _defaultParserOptions.RegExpParseMode, _defaultParserOptions.RegexTimeout);
}
