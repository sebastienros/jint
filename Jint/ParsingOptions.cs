
namespace Jint;

public interface IParsingOptions
{
    /// <summary>
    /// Gets or sets the match timeout for the regular expressions this source creates.
    /// Defaults to <see langword="null"/>, meaning the engine running the source decides through
    /// <see cref="Options.ConstraintOptions.RegexTimeout"/>.
    /// </summary>
    /// <remarks>
    /// A value here is the host having chosen, and outranks
    /// <see cref="Options.ConstraintOptions.RegexTimeout"/> for every regular expression this source
    /// produces — one written as a literal and one it builds at run time alike.
    /// <para>
    /// <see langword="null"/> means the same thing for a prepared script or module as for any other source.
    /// Preparation happens where there is no engine, and through 4.16.x it therefore baked in Jint's own
    /// ten-second default: a host that had tightened the constraint for security and then adopted
    /// <see cref="Engine.PrepareScript(string, string, bool, ScriptPreparationOptions)"/> ran at ten seconds and nothing said so. The timeout is now left
    /// unresolved at prepare time and read from the executing engine instead, so one prepared program
    /// shared across engines observes each engine's own budget.
    /// </para>
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

internal interface IParsingLimitOptions
{
    int? MaxSourceLength { get; }
    int? MaxNodeCount { get; }
}

public sealed record ScriptParsingOptions : IParsingOptions, IParsingLimitOptions
{
    private static readonly ParserOptions _defaultParserOptions = Engine.BaseParserOptions with
    {
        AllowReturnOutsideFunction = true,
        AllowTopLevelUsing = true,
        OnRegExp = Engine.DeferredRegExpHandler,
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

    /// <summary>
    /// Gets or sets the maximum number of UTF-16 code units the parser may receive.
    /// <see langword="null"/> (the default) means no limit.
    /// Source-offset padding also counts.
    /// </summary>
    public int? MaxSourceLength { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of AST nodes the parser may produce.
    /// <see langword="null"/> (the default) means no limit.
    /// </summary>
    public int? MaxNodeCount { get; init; }

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

    internal ParserOptions ApplyTo(ParserOptions baseOptions, TimeSpan? fallbackRegexTimeout) => baseOptions with
    {
        AllowReturnOutsideFunction = AllowReturnOutsideFunction,
        OnRegExp = GetOnRegExpHandler(fallbackRegexTimeout),
        OnNode = RetainFunctionSourceText ? Engine.DefaultNodeHandler : null,
        Tolerant = Tolerant,
    };

    private OnRegExpHandler? GetOnRegExpHandler(TimeSpan? fallbackRegexTimeout)
    {
        // An explicit RegexTimeout is the host having chosen, and outranks the fallback. The fallback is the
        // engine's constraint where the parse happens on an engine, and nothing at all where it does not -
        // a preparation — in which case the timeout stays unresolved and every engine running the result
        // resolves it against its own Options.Constraints.RegexTimeout.
        var chosen = RegexTimeout ?? fallbackRegexTimeout;
        if (chosen is null)
        {
            return Engine.DeferredRegExpHandler;
        }

        // Normalized here rather than at either source, so that "a limit that cannot be reached is not a
        // limit" holds for the per-parse override and the engine setting alike.
        return Engine.CreateRegExpHandler(Options.ConstraintOptions.NormalizeRegexTimeout(chosen.Value));
    }

    internal ParserOptions GetParserOptions() => ReferenceEquals(this, Default)
        ? _defaultParserOptions
        : ApplyTo(_defaultParserOptions, fallbackRegexTimeout: null);

    internal ParserOptions GetParserOptions(Options engineOptions)
        => ApplyTo(_defaultParserOptions, engineOptions.Constraints.RegexTimeout);
}

public sealed record class ModuleParsingOptions : IParsingOptions, IParsingLimitOptions
{
    private static readonly ParserOptions _defaultParserOptions = Engine.BaseParserOptions with
    {
        OnRegExp = Engine.DeferredRegExpHandler,
        // OnNode (source-text retention) is applied conditionally in ApplyTo based on RetainFunctionSourceText.
    };

    public static readonly ModuleParsingOptions Default = new();

    /// <summary>
    /// A <see cref="Default"/> variant that retains function source text. Used by the engine when
    /// <see cref="Options.RetainFunctionSourceText"/> is enabled to build its default module parser.
    /// </summary>
    internal static readonly ModuleParsingOptions RetainingDefault = new() { RetainFunctionSourceText = true };

    /// <summary>
    /// Gets or sets the maximum number of UTF-16 code units the parser may receive.
    /// <see langword="null"/> (the default) means no limit.
    /// </summary>
    public int? MaxSourceLength { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of AST nodes the parser may produce.
    /// <see langword="null"/> (the default) means no limit.
    /// </summary>
    public int? MaxNodeCount { get; init; }

    /// <inheritdoc/>
    public TimeSpan? RegexTimeout { get; init; }

    /// <inheritdoc/>
    public bool Tolerant { get; init; } = _defaultParserOptions.Tolerant;

    /// <inheritdoc/>
    public bool RetainFunctionSourceText { get; init; }

    internal ParserOptions ApplyTo(ParserOptions baseOptions, TimeSpan? fallbackRegexTimeout) => baseOptions with
    {
        OnRegExp = GetOnRegExpHandler(fallbackRegexTimeout),
        OnNode = RetainFunctionSourceText ? Engine.DefaultNodeHandler : null,
        Tolerant = Tolerant,
    };

    private OnRegExpHandler? GetOnRegExpHandler(TimeSpan? fallbackRegexTimeout)
    {
        // An explicit RegexTimeout is the host having chosen, and outranks the fallback. The fallback is the
        // engine's constraint where the parse happens on an engine, and nothing at all where it does not -
        // a preparation — in which case the timeout stays unresolved and every engine running the result
        // resolves it against its own Options.Constraints.RegexTimeout.
        var chosen = RegexTimeout ?? fallbackRegexTimeout;
        if (chosen is null)
        {
            return Engine.DeferredRegExpHandler;
        }

        // Normalized here rather than at either source, so that "a limit that cannot be reached is not a
        // limit" holds for the per-parse override and the engine setting alike.
        return Engine.CreateRegExpHandler(Options.ConstraintOptions.NormalizeRegexTimeout(chosen.Value));
    }

    internal ParserOptions GetParserOptions() => ReferenceEquals(this, Default)
        ? _defaultParserOptions
        : ApplyTo(_defaultParserOptions, fallbackRegexTimeout: null);

    internal ParserOptions GetParserOptions(Options engineOptions)
        => ApplyTo(_defaultParserOptions, engineOptions.Constraints.RegexTimeout);
}
