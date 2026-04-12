namespace Jint;

public partial class Engine
{
    internal const bool FoldConstantsOnPrepareByDefault = true;

    internal static readonly ParserOptions BaseParserOptions = ParserOptions.Default with
    {
        EcmaVersion = EcmaVersion.ES2026,
        ExperimentalESFeatures = ExperimentalESFeatures.Decorators,
    };

    internal static readonly TimeSpan DefaultRegexTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Cached OnRegExp handler for <see cref="DefaultRegexTimeout"/> (interpreted .NET Regex).
    /// </summary>
    internal static OnRegExpHandler DefaultConvertRegExpHandler
        => CreateRegExpHandler(compiled: false, DefaultRegexTimeout);

    /// <summary>
    /// Cached OnRegExp handler for <see cref="DefaultRegexTimeout"/> (compiled .NET Regex).
    /// </summary>
    internal static OnRegExpHandler DefaultCompileRegExpHandler
        => CreateRegExpHandler(compiled: true, DefaultRegexTimeout);

    /// <summary>
    /// Creates an OnRegExp handler with a caller-specified timeout.
    /// </summary>
    internal static OnRegExpHandler CreateRegExpHandler(bool compiled, TimeSpan timeout)
        => new RegexConversionOptions(compiled, timeout).HandleOnRegExp;

    internal sealed class RegexConversionOptions(bool compiled, TimeSpan timeout)
    {
        public bool Compiled { get; } = compiled;
        public TimeSpan Timeout { get; } = timeout;

        internal RegExpParseResult HandleOnRegExp(in RegExpParsingContext ctx)
        {
            // In the course of parsing, we only validate the pattern and defer conversion until execution
            // (see JintLiteralExpression.ResolveValue).

            ctx.Validate();

            return RegExpParseResult.ForSuccess(additionalData: this);
        }
    }
}
