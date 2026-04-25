using Jint.Native.Function;

namespace Jint;

public partial class Engine
{
    internal const bool FoldConstantsOnPrepareByDefault = true;

    // NOTE: Do not enable ParserOptions.PreserveParens here without first revisiting the
    // CoverParenthesizedExpression detection in JintAssignmentExpression (NamedEvaluation +
    // SimpleAssignmentExpression.Initialize). That code relies on Acornima stripping parens
    // by default and uses the position offset between the AssignmentExpression and its inner
    // Identifier to detect "(fn) = ..." — a wrapped ParenthesizedExpression node would
    // bypass the Identifier-type guard and silently break the IsIdentifierRef semantics.
    internal static readonly ParserOptions BaseParserOptions = ParserOptions.Default with
    {
        EcmaVersion = EcmaVersion.ES2026,
        ExperimentalESFeatures = ExperimentalESFeatures.Decorators
            | ExperimentalESFeatures.SourcePhaseImports
            | ExperimentalESFeatures.DeferImportEvaluation,
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

    /// <summary>
    /// Cached OnNode callback that stores the source text being parsed in <see cref="Node.UserData"/> of function nodes
    /// to support <see cref="Function.ToString"><c>Function.prototype.toString()</c> implementation</see>.
    /// </summary>
    internal static readonly OnNodeHandler DefaultNodeHandler = static (node, in ctx) =>
    {
        if (node.Type is NodeType.ArrowFunctionExpression or NodeType.FunctionDeclaration or NodeType.FunctionExpression)
        {
            node.UserData = ctx.Input;
        }
    };
}
