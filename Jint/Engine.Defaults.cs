using Jint.Native.RegExp;
using Jint.Runtime.RegExp;

namespace Jint;

public partial class Engine
{
    internal const bool FoldConstantsOnPrepareByDefault = true;

    internal static readonly TimeSpan DefaultRegexTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Creates an OnRegExp handler with a caller-specified timeout (interpreted .NET Regex).
    /// </summary>
    internal static OnRegExpHandler CreateConvertRegExpHandler(TimeSpan timeout) => (in RegExpParsingContext ctx) =>
    {
        ctx.Validate();
        if (!RegExpConstructor.NeedCustomEngine(ctx.Pattern, ctx.Flags)
            && JsRegExpConverter.TryConvert(ctx.Pattern, ctx.Flags, timeout, out var regex, out var groupCount))
        {
            return RegExpParseResult.ForSuccess(regex, groupCount);
        }

        return RegExpParseResult.ForSuccess();
    };

    /// <summary>
    /// Cached OnRegExp handler for <see cref="DefaultRegexTimeout"/> (interpreted .NET Regex).
    /// </summary>
    internal static OnRegExpHandler DefaultConvertRegExpHandler => CreateConvertRegExpHandler(DefaultRegexTimeout);

    /// <summary>
    /// Creates an OnRegExp handler with a caller-specified timeout (compiled .NET Regex).
    /// </summary>
    internal static OnRegExpHandler CreateCompileRegExpHandler(TimeSpan timeout) => (in RegExpParsingContext ctx) =>
    {
        ctx.Validate();
        if (!RegExpConstructor.NeedCustomEngine(ctx.Pattern, ctx.Flags)
            && JsRegExpConverter.TryConvert(ctx.Pattern, ctx.Flags, timeout, out var regex, out var groupCount, compiled: true))
        {
            return RegExpParseResult.ForSuccess(regex, groupCount);
        }

        return RegExpParseResult.ForSuccess();
    };

    /// <summary>
    /// Cached OnRegExp handler for <see cref="DefaultRegexTimeout"/> (interpreted .NET Regex).
    /// </summary>
    internal static OnRegExpHandler DefaultCompileRegExpHandler => CreateCompileRegExpHandler(DefaultRegexTimeout);

    internal static readonly ParserOptions BaseParserOptions = ParserOptions.Default with
    {
        EcmaVersion = EcmaVersion.ES2026,
        ExperimentalESFeatures = ExperimentalESFeatures.Decorators,
    };
}
