using System.Text.RegularExpressions;

namespace Jint;

public interface IParsingOptions
{
    /// <summary>
    /// Gets or sets whether to create compiled <see cref="Regex"/> instances when adapting regular expressions.
    /// Defaults to <see langword="null"/>. When <see langword="true"/>, regex patterns are pre-compiled during
    /// preparation using <see cref="RegexOptions.Compiled"/>. When <see langword="false"/>, interpreted .NET Regex
    /// instances are created. When <see langword="null"/>, inherits from the base parser options.
    /// Patterns that require the custom QuickJS engine are always deferred to runtime regardless of this setting.
    /// </summary>
    bool? CompileRegex { get; init; }

    /// <summary>
    /// Gets or sets the default timeout for created <see cref="Regex"/> instances.
    /// Defaults to <see langword="null"/>, which means that in the case of non-prepared scripts and modules
    /// the <see cref="Options.ConstraintOptions.RegexTimeout"/> setting should apply.
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

public sealed record ScriptParsingOptions : IParsingOptions
{
    private static readonly ParserOptions _defaultParserOptions = Engine.BaseParserOptions with
    {
        AllowReturnOutsideFunction = true,
        AllowTopLevelUsing = true,
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

    /// <summary>
    /// Gets or sets the source location offset to apply when parsing.
    /// This allows mapping error locations back to the original source file
    /// when the JavaScript code is embedded within a larger file (e.g., a JSON file).
    /// The offset is 1-based for lines and 0-based for columns, matching the <see cref="Position"/> convention.
    /// Defaults to <see langword="default"/> (no offset).
    /// </summary>
    public Position SourceOffset { get; init; }

    internal ParserOptions ApplyTo(ParserOptions parserOptions, TimeSpan? engineRegexTimeout = null) => parserOptions with
    {
        AllowReturnOutsideFunction = AllowReturnOutsideFunction,
        OnRegExp = GetOnRegExpHandler(parserOptions, engineRegexTimeout),
        Tolerant = Tolerant,
    };

    private OnRegExpHandler? GetOnRegExpHandler(ParserOptions parserOptions, TimeSpan? engineRegexTimeout)
    {
        // Explicit RegexTimeout takes priority, then engine's configured timeout
        var timeout = RegexTimeout ?? engineRegexTimeout;

        if (timeout is { } t)
        {
            return CompileRegex switch
            {
                true => Engine.CreateCompileRegExpHandler(t),
                false => Engine.CreateConvertRegExpHandler(t),
                _ => parserOptions.OnRegExp,
            };
        }

        return CompileRegex switch
        {
            true => Engine.CompileRegExpHandler,
            false => Engine.ConvertRegExpHandler,
            null => parserOptions.OnRegExp, // inherit from base (validate for scripts, compile for preparation)
        };
    }

    internal ParserOptions GetParserOptions() => ReferenceEquals(this, Default)
        ? _defaultParserOptions
        : ApplyTo(_defaultParserOptions);

    internal ParserOptions GetParserOptions(Options engineOptions)
        => ApplyTo(_defaultParserOptions, engineOptions.Constraints.RegexTimeout);
}

public sealed record class ModuleParsingOptions : IParsingOptions
{
    private static readonly ParserOptions _defaultParserOptions = Engine.BaseParserOptions with
    {
    };

    public static readonly ModuleParsingOptions Default = new();

    /// <inheritdoc/>
    public bool? CompileRegex { get; init; }

    /// <inheritdoc/>
    public TimeSpan? RegexTimeout { get; init; }

    /// <inheritdoc/>
    public bool Tolerant { get; init; } = _defaultParserOptions.Tolerant;

    internal ParserOptions ApplyTo(ParserOptions baseOptions, TimeSpan? engineRegexTimeout = null) => baseOptions with
    {
        OnRegExp = GetOnRegExpHandler(baseOptions, engineRegexTimeout),
        Tolerant = Tolerant,
    };

    private OnRegExpHandler? GetOnRegExpHandler(ParserOptions baseOptions, TimeSpan? engineRegexTimeout)
    {
        // Explicit RegexTimeout takes priority, then engine's configured timeout
        var timeout = RegexTimeout ?? engineRegexTimeout;

        if (timeout is { } t)
        {
            return CompileRegex switch
            {
                true => Engine.CreateCompileRegExpHandler(t),
                false => Engine.CreateConvertRegExpHandler(t),
                _ => baseOptions.OnRegExp,
            };
        }

        return CompileRegex switch
        {
            true => Engine.CompileRegExpHandler,
            false => Engine.ConvertRegExpHandler,
            null => baseOptions.OnRegExp,
        };
    }

    internal ParserOptions GetParserOptions() => ReferenceEquals(this, Default)
        ? _defaultParserOptions
        : ApplyTo(_defaultParserOptions);

    internal ParserOptions GetParserOptions(Options engineOptions)
        => ApplyTo(_defaultParserOptions, engineOptions.Constraints.RegexTimeout);
}
