using Jint.Native.RegExp;
using Jint.Runtime.RegExp;

namespace Jint;

public partial class Engine
{
    internal const bool FoldConstantsOnPrepareByDefault = true;

    private static readonly TimeSpan DefaultPrepareRegexTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// OnRegExp callback that validates regex syntax at parse time without converting.
    /// Conversion is done at runtime in RegExpInitialize with fallback to the custom engine.
    /// </summary>
    internal static readonly OnRegExpHandler ValidateRegExpHandler = static (in RegExpParsingContext ctx) =>
    {
        ctx.Validate();
        return RegExpParseResult.ForSuccess();
    };

    /// <summary>
    /// OnRegExp callback that validates and converts regex at parse time (interpreted .NET Regex).
    /// Used during script/module preparation when CompileRegex is false.
    /// </summary>
    internal static readonly OnRegExpHandler ConvertRegExpHandler = static (in RegExpParsingContext ctx) =>
    {
        ctx.Validate();
        // Skip pre-compilation for patterns that need the custom QuickJS engine at runtime
        if (!RegExpConstructor.NeedCustomEngine(ctx.Pattern, ctx.Flags)
            && JsRegExpConverter.TryConvert(ctx.Pattern, ctx.Flags, DefaultPrepareRegexTimeout, out var regex, out var groupCount))
        {
            return RegExpParseResult.ForSuccess(regex, groupCount);
        }

        return RegExpParseResult.ForSuccess();
    };

    /// <summary>
    /// OnRegExp callback that validates and converts regex at parse time (compiled .NET Regex).
    /// Used during script/module preparation when CompileRegex is true (default for preparation).
    /// </summary>
    internal static readonly OnRegExpHandler CompileRegExpHandler = static (in RegExpParsingContext ctx) =>
    {
        ctx.Validate();
        // Skip pre-compilation for patterns that need the custom QuickJS engine at runtime
        if (!RegExpConstructor.NeedCustomEngine(ctx.Pattern, ctx.Flags)
            && JsRegExpConverter.TryConvert(ctx.Pattern, ctx.Flags, DefaultPrepareRegexTimeout, out var regex, out var groupCount, compiled: true))
        {
            return RegExpParseResult.ForSuccess(regex, groupCount);
        }

        return RegExpParseResult.ForSuccess();
    };

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

    internal static readonly ParserOptions BaseParserOptions = ParserOptions.Default with
    {
        EcmaVersion = EcmaVersion.ES2026,
        ExperimentalESFeatures = ExperimentalESFeatures.Decorators,
        OnRegExp = ValidateRegExpHandler,
        Tolerant = false,
    };
}
