using System.Text.RegularExpressions;

namespace Jint;

public interface IParsingOptions
{
    /// <summary>
    /// Gets or sets whether to create compiled <see cref="Regex"/> instances when adapting regular expressions.
    /// When <see langword="true"/>, regex patterns are pre-compiled using <see cref="RegexOptions.Compiled"/>.
    /// When <see langword="false"/>, regex patterns are interpreted.
    /// When <see langword="null"/>, regex patterns are interpreted in the case of non-prepared scripts and modules,
    /// otherwise they are pre-compiled.
    /// Defaults to <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Patterns that require the custom QuickJS engine are always interpreted regardless of this setting.
    /// </remarks>
    bool? CompileRegex { get; init; }

    /// <summary>
    /// Gets or sets the default timeout for created <see cref="Regex"/> instances.
    /// Defaults to <see langword="null"/>, which means that in the case of non-prepared scripts and modules
    /// the <see cref="Options.ConstraintOptions.RegexTimeout"/> setting should apply,
    /// otherwise the default value of 10 seconds is used.
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

    /// <summary>
    /// Gets or sets whether to retain the full source text of parsed functions so that
    /// <see cref="Native.Function.Function.ToString"><c>Function.prototype.toString()</c></see>
    /// can return the original source.
    /// When <see langword="false"/> (the default), the source text is not kept and <c>toString()</c>
    /// returns a <c>function name() { [native code] }</c> placeholder. This avoids retaining the entire
    /// script source in memory, which can be significant for large and/or cached (prepared) scripts.
    /// </summary>
    bool RetainFunctionSourceText { get; init; }
}

public sealed record ScriptParsingOptions : IParsingOptions
{
    private static readonly ParserOptions _defaultParserOptions = Engine.BaseParserOptions with
    {
        AllowReturnOutsideFunction = true,
        AllowTopLevelUsing = true,
        OnRegExp = Engine.DefaultConvertRegExpHandler,
        // OnNode (source-text retention) is applied conditionally in ApplyTo based on RetainFunctionSourceText.
    };

    public static readonly ScriptParsingOptions Default = new();

    /// <summary>
    /// A <see cref="Default"/> variant that retains function source text. Used by the engine when
    /// <see cref="Options.RetainFunctionSourceText"/> is enabled to build its default parser.
    /// </summary>
    internal static readonly ScriptParsingOptions RetainingDefault = new() { RetainFunctionSourceText = true };

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

    /// <inheritdoc/>
    public bool RetainFunctionSourceText { get; init; }

    /// <summary>
    /// Gets or sets the source location offset to apply when parsing.
    /// This allows mapping error locations back to the original source file
    /// when the JavaScript code is embedded within a larger file (e.g., a JSON file).
    /// The offset is 1-based for lines and 0-based for columns, matching the <see cref="Position"/> convention.
    /// Defaults to <see langword="default"/> (no offset).
    /// </summary>
    public Position SourceOffset { get; init; }

    internal ParserOptions ApplyTo(ParserOptions baseOptions, bool fallbackCompileRegex, TimeSpan fallbackRegexTimeout) => baseOptions with
    {
        AllowReturnOutsideFunction = AllowReturnOutsideFunction,
        OnRegExp = GetOnRegExpHandler(fallbackCompileRegex, fallbackRegexTimeout),
        OnNode = RetainFunctionSourceText ? Engine.DefaultNodeHandler : null,
        Tolerant = Tolerant,
    };

    private OnRegExpHandler? GetOnRegExpHandler(bool fallbackCompileRegex, TimeSpan fallbackRegexTimeout)
    {
        // Explicit RegexTimeout takes priority, then engine's configured timeout
        var timeout = RegexTimeout ?? fallbackRegexTimeout;

        if (CompileRegex ?? fallbackCompileRegex)
        {
            return timeout == Engine.DefaultRegexTimeout
                ? Engine.DefaultCompileRegExpHandler
                : Engine.CreateRegExpHandler(compiled: true, timeout);
        }
        else
        {
            return timeout == Engine.DefaultRegexTimeout
                ? Engine.DefaultConvertRegExpHandler
                : Engine.CreateRegExpHandler(compiled: false, timeout);
        }
    }

    internal ParserOptions GetParserOptions() => ReferenceEquals(this, Default)
        ? _defaultParserOptions
        : ApplyTo(_defaultParserOptions, fallbackCompileRegex: false, Engine.DefaultRegexTimeout);

    internal ParserOptions GetParserOptions(Options engineOptions)
        => ApplyTo(_defaultParserOptions, fallbackCompileRegex: false, engineOptions.Constraints.RegexTimeout);
}

public sealed record class ModuleParsingOptions : IParsingOptions
{
    private static readonly ParserOptions _defaultParserOptions = Engine.BaseParserOptions with
    {
        OnRegExp = Engine.DefaultConvertRegExpHandler,
        // OnNode (source-text retention) is applied conditionally in ApplyTo based on RetainFunctionSourceText.
    };

    public static readonly ModuleParsingOptions Default = new();

    /// <summary>
    /// A <see cref="Default"/> variant that retains function source text. Used by the engine when
    /// <see cref="Options.RetainFunctionSourceText"/> is enabled to build its default module parser.
    /// </summary>
    internal static readonly ModuleParsingOptions RetainingDefault = new() { RetainFunctionSourceText = true };

    /// <inheritdoc/>
    public bool? CompileRegex { get; init; }

    /// <inheritdoc/>
    public TimeSpan? RegexTimeout { get; init; }

    /// <inheritdoc/>
    public bool Tolerant { get; init; } = _defaultParserOptions.Tolerant;

    /// <inheritdoc/>
    public bool RetainFunctionSourceText { get; init; }

    internal ParserOptions ApplyTo(ParserOptions baseOptions, bool fallbackCompileRegex, TimeSpan fallbackRegexTimeout) => baseOptions with
    {
        OnRegExp = GetOnRegExpHandler(fallbackCompileRegex, fallbackRegexTimeout),
        OnNode = RetainFunctionSourceText ? Engine.DefaultNodeHandler : null,
        Tolerant = Tolerant,
    };

    private OnRegExpHandler? GetOnRegExpHandler(bool fallbackCompileRegex, TimeSpan fallbackRegexTimeout)
    {
        // Explicit RegexTimeout takes priority, then engine's configured timeout
        var timeout = RegexTimeout ?? fallbackRegexTimeout;

        if (CompileRegex ?? fallbackCompileRegex)
        {
            return timeout == Engine.DefaultRegexTimeout
                ? Engine.DefaultCompileRegExpHandler
                : Engine.CreateRegExpHandler(compiled: true, timeout);
        }
        else
        {
            return timeout == Engine.DefaultRegexTimeout
                ? Engine.DefaultConvertRegExpHandler
                : Engine.CreateRegExpHandler(compiled: false, timeout);
        }
    }

    internal ParserOptions GetParserOptions() => ReferenceEquals(this, Default)
        ? _defaultParserOptions
        : ApplyTo(_defaultParserOptions, fallbackCompileRegex: false, Engine.DefaultRegexTimeout);

    internal ParserOptions GetParserOptions(Options engineOptions)
        => ApplyTo(_defaultParserOptions, fallbackCompileRegex: false, engineOptions.Constraints.RegexTimeout);
}
