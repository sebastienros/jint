using System.Text.RegularExpressions;

namespace Jint;

public interface IParsingOptions
{
    /// <summary>
    /// Gets or sets whether to create compiled <see cref="Regex"/> instances when adapting regular expressions.
    /// Defaults to <see langword="null"/>, which means that in the case of non-prepared scripts and modules
    /// regular expressions will be interpreted, otherwise they will be compiled.
    /// </summary>
    bool? CompileRegex { get; init; }

    /// <summary>
    /// Gets or sets the default timeout for created <see cref="Regex"/> instances.
    /// Defaults to <see langword="null"/>, which means that in the case of non-prepared scripts and modules
    /// the <see cref="Options.ConstraintOptions.RegexTimeout"/> setting should apply,
    /// otherwise the default of the <see cref="ParserOptions.RegexTimeout"/> setting (10 seconds).
    /// </summary>
    /// <remarks>
    /// Please note that <see cref="Options.ConstraintOptions.RegexTimeout"/> setting will be ignored
    /// if this option is set to a value other than <see langword="null"/>.
    /// </remarks>
    TimeSpan? RegexTimeout { get; init; }

    /// <summary>
    /// Gets or sets whether to parse the source code in tolerant mode.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    bool Tolerant { get; init; }
}

public sealed record class ScriptParsingOptions : IParsingOptions
{
    private static readonly ParserOptions _defaultParserOptions = Engine.BaseParserOptions with
    {
        AllowReturnOutsideFunction = true,
        RegExpParseMode = RegExpParseMode.AdaptToInterpreted,
    };

    public static readonly ScriptParsingOptions Default = new();

    /// <summary>
    /// Gets or sets whether to allow return statements at the top level.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool AllowReturnOutsideFunction { get; init; } = _defaultParserOptions.AllowReturnOutsideFunction;

    /// <inheritdoc/>
    public bool? CompileRegex { get; init; }

    /// <inheritdoc/>
    public TimeSpan? RegexTimeout { get; init; }

    /// <inheritdoc/>
    public bool Tolerant { get; init; } = _defaultParserOptions.Tolerant;

    internal ParserOptions ApplyTo(ParserOptions parserOptions, RegExpParseMode defaultRegExpParseMode, TimeSpan defaultRegexTimeout) => parserOptions with
    {
        AllowReturnOutsideFunction = AllowReturnOutsideFunction,
        RegExpParseMode = CompileRegex is null
            ? defaultRegExpParseMode
            : (CompileRegex.Value ? RegExpParseMode.AdaptToCompiled : RegExpParseMode.AdaptToInterpreted),
        RegexTimeout = RegexTimeout ?? defaultRegexTimeout,
        Tolerant = Tolerant,
    };

    internal ParserOptions GetParserOptions() => ReferenceEquals(this, Default)
        ? _defaultParserOptions
        : ApplyTo(_defaultParserOptions, _defaultParserOptions.RegExpParseMode, _defaultParserOptions.RegexTimeout);

    internal ParserOptions GetParserOptions(Options engineOptions)
        => ApplyTo(_defaultParserOptions, _defaultParserOptions.RegExpParseMode, engineOptions.Constraints.RegexTimeout);
}

public sealed record class ModuleParsingOptions : IParsingOptions
{
    private static readonly ParserOptions _defaultParserOptions = Engine.BaseParserOptions with
    {
        RegExpParseMode = RegExpParseMode.AdaptToInterpreted,
    };

    public static readonly ModuleParsingOptions Default = new();

    /// <inheritdoc/>
    public bool? CompileRegex { get; init; }

    /// <inheritdoc/>
    public TimeSpan? RegexTimeout { get; init; }

    /// <inheritdoc/>
    public bool Tolerant { get; init; } = _defaultParserOptions.Tolerant;

    internal ParserOptions ApplyTo(ParserOptions baseOptions, RegExpParseMode defaultRegExpParseMode, TimeSpan defaultRegexTimeout) => baseOptions with
    {
        RegExpParseMode = CompileRegex is null
            ? defaultRegExpParseMode
            : (CompileRegex.Value ? RegExpParseMode.AdaptToCompiled : RegExpParseMode.AdaptToInterpreted),
        RegexTimeout = RegexTimeout ?? defaultRegexTimeout,
        Tolerant = Tolerant,
    };

    internal ParserOptions GetParserOptions() => ReferenceEquals(this, Default)
        ? _defaultParserOptions
        : ApplyTo(_defaultParserOptions, _defaultParserOptions.RegExpParseMode, _defaultParserOptions.RegexTimeout);

    internal ParserOptions GetParserOptions(Options engineOptions)
        => ApplyTo(_defaultParserOptions, _defaultParserOptions.RegExpParseMode, engineOptions.Constraints.RegexTimeout);
}