using Jint.Native.Function;
using Jint.Native.RegExp;

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

    /// <summary>
    /// OnRegExp handler that leaves the match timeout to the engine executing the parsed source
    /// (interpreted .NET Regex).
    /// </summary>
    internal static OnRegExpHandler DeferredInterpretedRegExpHandler
        => CreateRegExpHandler(RegexCompilation.Interpreted, timeout: null);

    /// <summary>
    /// OnRegExp handler that leaves the match timeout to the engine executing the parsed source
    /// (eagerly compiled .NET Regex).
    /// </summary>
    internal static OnRegExpHandler DeferredCompiledRegExpHandler
        => CreateRegExpHandler(RegexCompilation.Compiled, timeout: null);

    /// <summary>
    /// OnRegExp handler that leaves the match timeout to the engine executing the parsed source
    /// (interpreted .NET Regex, upgraded to compiled on reuse; see <see cref="RegexCompilation.Adaptive"/>).
    /// </summary>
    internal static OnRegExpHandler DeferredAdaptiveRegExpHandler
        => CreateRegExpHandler(RegexCompilation.Adaptive, timeout: null);

    /// <summary>
    /// Creates an OnRegExp handler with a caller-specified timeout, or with none at all — see
    /// <see cref="RegexConversionOptions.Timeout"/>.
    /// </summary>
    internal static OnRegExpHandler CreateRegExpHandler(RegexCompilation compilation, TimeSpan? timeout)
        => new RegexConversionOptions(compilation, timeout).HandleOnRegExp;

    /// <summary>
    /// Both halves of how one parsed source's regular expressions are adapted: how they are code-generated,
    /// and how long a match of one may run. Hung off <see cref="ParserOptions.OnRegExp"/> and read back from
    /// the parse result of every pattern that source produces, literal and run-time-built alike.
    /// </summary>
    internal sealed class RegexConversionOptions(RegexCompilation compilation, TimeSpan? timeout)
    {
        public RegexCompilation Compilation { get; } = compilation;

        /// <summary>
        /// The already-normalized match timeout this source's patterns run under, or <see langword="null"/>
        /// when nobody chose one and the engine executing the source decides through
        /// <see cref="Jint.Options.ConstraintOptions.RegexTimeout"/>.
        /// </summary>
        /// <remarks>
        /// Preparation is what produces <see langword="null"/>: there is no engine at prepare time, and
        /// baking Jint's own ten-second default in instead meant a host that had tightened the constraint
        /// for security and then adopted <see cref="PrepareScript"/> silently ran at ten seconds
        /// (sebastienros/jint#3442). A <see cref="TimeSpan"/> resolved per engine at the point of use keeps
        /// this object — which is published onto a shared AST — engine-neutral.
        /// </remarks>
        public TimeSpan? Timeout { get; } = timeout;

        /// <summary>
        /// Resolves <see cref="Timeout"/> against the engine that is about to run the pattern.
        /// </summary>
        public TimeSpan ResolveTimeout(Engine engine)
            => Timeout ?? Jint.Options.ConstraintOptions.NormalizeRegexTimeout(engine.Options.Constraints.RegexTimeout);

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
        // Class nodes are stamped too, because a class constructor's source text is the whole
        // ClassDeclaration/ClassExpression and a class without an explicit constructor has no function
        // node of its own to read it from - the engine synthesizes one shared constructor AST for all
        // of them. https://tc39.es/ecma262/#sec-class-definitions-runtime-semantics-evaluation
        if (node.Type is NodeType.ArrowFunctionExpression or NodeType.FunctionDeclaration or NodeType.FunctionExpression
            or NodeType.ClassDeclaration or NodeType.ClassExpression)
        {
            node.UserData = ctx.Input;
        }
    };
}
