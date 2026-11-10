using System.Runtime.CompilerServices;
using Jint.Native.RegExp;

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
        OnRegExp = Engine.DeferredCompiledRegExpHandler,
    };

    /// <summary>
    /// The derived <see cref="ParserOptions"/> of every <see cref="ParsingOptions"/> instance seen so far, so that
    /// preparing repeatedly under one set of options does not clone a <see cref="ParserOptions"/> — plus a regexp
    /// handler and its state object — on every call. Keying on the parsing options rather than on the preparation
    /// options is exact, because <see cref="GetParserOptions"/> derives its result from nothing else: the base
    /// options and the compilation mode it applies are constants, and the timeout is deliberately left
    /// unresolved for the executing engine to supply.
    /// <para>
    /// Static, not an instance field, and deliberately so. A private field on a record is part of the compiler's
    /// synthesized equality — warming it would flip <c>==</c> and change <see cref="object.GetHashCode"/> on a
    /// public type — and <c>with</c> copies it into the clone, where it would answer for parsing options the clone
    /// no longer has. A weak-keyed table sidesteps both: no per-instance state to copy or compare, and an entry
    /// dies with the parsing options that keyed it.
    /// </para>
    /// </summary>
    private static readonly ConditionalWeakTable<ScriptParsingOptions, ParserOptions> _parserOptionsCache = new();

    private static readonly ConditionalWeakTable<ScriptParsingOptions, ParserOptions>.CreateValueCallback _createParserOptions =
        static parsingOptions => parsingOptions.ApplyTo(_defaultParserOptions, RegexCompilation.Compiled, fallbackRegexTimeout: null);

    public static readonly ScriptPreparationOptions Default = new();

    public ScriptParsingOptions ParsingOptions { get; init; } = ScriptParsingOptions.Default;

    /// <inheritdoc/>
    public bool FoldConstants { get; init; } = Engine.FoldConstantsOnPrepareByDefault;

    /// <summary>
    /// Whether to collect, during preparation, the set of identifier names the script references but does not itself
    /// declare — the names that will resolve in the global scope (or fail) at run time. The result is exposed as
    /// <see cref="Prepared{TProgram}.ReferencedGlobals"/>, which documents exactly what is and is not reported.
    /// Defaults to <see langword="false"/>.
    /// <para>
    /// The intended use is letting a host build only the ambient API a script actually asks for. When
    /// <see langword="false"/> nothing is collected, preparation cost is unchanged, and
    /// <see cref="Prepared{TProgram}.ReferencedGlobals"/> is <see langword="null"/> — which is distinguishable from
    /// an empty set, meaning "the script references no free names".
    /// </para>
    /// </summary>
    public bool CollectReferencedGlobals { get; init; }

    /// <summary>
    /// Whether preparation runs the static-analysis pass that pre-publishes interpreter state onto the parsed AST.
    /// Defaults to <see langword="true"/>, which is what preparation has always done.
    /// <para>
    /// The pass visits every node the parser produced and publishes, onto the tree itself, what the interpreter
    /// would otherwise work out on its own: folded constant expressions, the per-function definition state, the
    /// per-block scope state, a binding name per identifier, the resolved property name of a non-computed member
    /// access, and, for a script root, the hoisting scope that global declaration instantiation reads. It is a
    /// second full walk of the source on top of the parse, and it is most of what makes preparing a script cost
    /// noticeably more than parsing it.
    /// </para>
    /// <para>
    /// Turning it off brings preparation back down to roughly the cost of a plain parse — measured about twice as
    /// cheap. The trade is aimed at a host that prepares once and shares the result across a pool of engines: the
    /// analysis is paid once, by whichever thread prepares, but most of what it precomputes is work the first
    /// engine to reach a node would do anyway, so paying for all of it up front is a tax the solitary-engine case
    /// is the only one to earn back in full.
    /// </para>
    /// <para>
    /// What is forfeited is the pre-publication, not the state. Each engine rebuilds lazily what the analyzer would
    /// have published, and function definition state, block state and literal values are published back onto the
    /// shared tree by the first engine that needs them, then reused by every engine after it — they are
    /// engine-neutral, which is the same property that lets a prepared program be shared at all. Identifier binding
    /// names, the member-access fast-path value and a script root's hoisting scope are not published that way and
    /// are rebuilt per engine; constant folding of unary and binary expressions likewise happens per engine, since
    /// the interpreter performs it on its own when it finds nothing prepared. Measured on
    /// <c>ModuleGraphEmbeddingBenchmark</c>'s ten-module graph, medians of five runs: preparation cost 47.6%
    /// less time and 27.5% less allocation, while each engine materializing that graph from a shared cache
    /// paid 4.8% more time and 9.5% more allocation - break-even at about ten engines on time, two on
    /// allocation, so a long-lived pool should leave this alone.
    /// </para>
    /// <para>
    /// Composes with <see cref="CollectReferencedGlobals"/>, which is gathered by a visitor of its own and is
    /// unaffected by this setting.
    /// </para>
    /// </summary>
    public bool StaticAnalysis { get; init; } = true;

    internal ParserOptions GetParserOptions() => ReferenceEquals(this, Default)
        ? _defaultParserOptions
        : _parserOptionsCache.GetValue(ParsingOptions, _createParserOptions);
}

public sealed record class ModulePreparationOptions : IPreparationOptions<ModuleParsingOptions>
{
    private static readonly ParserOptions _defaultParserOptions = ModuleParsingOptions.Default.GetParserOptions() with
    {
        OnRegExp = Engine.DeferredCompiledRegExpHandler,
    };

    /// <summary>
    /// The derived <see cref="ParserOptions"/> of every <see cref="ParsingOptions"/> instance seen so far. Static
    /// and weak-keyed for the same reasons the script-side one is; see <see cref="ScriptPreparationOptions"/>.
    /// </summary>
    private static readonly ConditionalWeakTable<ModuleParsingOptions, ParserOptions> _parserOptionsCache = new();

    private static readonly ConditionalWeakTable<ModuleParsingOptions, ParserOptions>.CreateValueCallback _createParserOptions =
        static parsingOptions => parsingOptions.ApplyTo(_defaultParserOptions, RegexCompilation.Compiled, fallbackRegexTimeout: null);

    public static readonly ModulePreparationOptions Default = new();

    public ModuleParsingOptions ParsingOptions { get; init; } = ModuleParsingOptions.Default;

    /// <inheritdoc/>
    public bool FoldConstants { get; init; } = Engine.FoldConstantsOnPrepareByDefault;

    /// <summary>
    /// Whether to collect, during preparation, the set of identifier names the module references but does not itself
    /// declare — the names that will resolve in the global scope (or fail) at run time. The result is exposed as
    /// <see cref="Prepared{TProgram}.ReferencedGlobals"/>, which documents exactly what is and is not reported.
    /// Defaults to <see langword="false"/>.
    /// <para>
    /// Import and export bindings are module-scope declarations, so an imported name is bound and never reported.
    /// When <see langword="false"/> nothing is collected, preparation cost is unchanged, and
    /// <see cref="Prepared{TProgram}.ReferencedGlobals"/> is <see langword="null"/> — which is distinguishable from
    /// an empty set, meaning "the module references no free names".
    /// </para>
    /// </summary>
    public bool CollectReferencedGlobals { get; init; }

    /// <summary>
    /// Whether preparation runs the static-analysis pass that pre-publishes interpreter state onto the parsed AST.
    /// Defaults to <see langword="true"/>, which is what preparation has always done.
    /// <para>
    /// The pass visits every node the parser produced and publishes, onto the tree itself, what the interpreter
    /// would otherwise work out on its own: folded constant expressions, the per-function definition state, the
    /// per-block scope state, a binding name per identifier, and the resolved property name of a non-computed
    /// member access. It is a second full walk of the source on top of the parse, and it is most of what makes
    /// preparing a module cost noticeably more than parsing it.
    /// </para>
    /// <para>
    /// Turning it off brings preparation back down to roughly the cost of a plain parse — measured about twice as
    /// cheap. The trade is aimed at a host that prepares once and shares the result across a pool of engines: the
    /// analysis is paid once, by whichever thread prepares, but most of what it precomputes is work the first
    /// engine to reach a node would do anyway, so paying for all of it up front is a tax the solitary-engine case
    /// is the only one to earn back in full.
    /// </para>
    /// <para>
    /// What is forfeited is the pre-publication, not the state. Each engine rebuilds lazily what the analyzer would
    /// have published, and function definition state, block state and literal values are published back onto the
    /// shared tree by the first engine that needs them, then reused by every engine after it — they are
    /// engine-neutral, which is the same property that lets a prepared program be shared at all. Identifier binding
    /// names and the member-access fast-path value are not published that way and are rebuilt per engine; constant
    /// folding of unary and binary expressions likewise happens per engine, since the interpreter performs it on
    /// its own when it finds nothing prepared. Measured on <c>ModuleGraphEmbeddingBenchmark</c>'s ten-module graph,
    /// medians of five runs: preparation cost 47.6% less time and 27.5% less allocation, while each engine
    /// materializing that graph from a shared cache paid 4.8% more time and 9.5% more allocation - break-even
    /// at about ten engines on time, two on allocation, so a long-lived pool should leave this alone.
    /// </para>
    /// <para>
    /// Composes with <see cref="CollectReferencedGlobals"/>, which is gathered by a visitor of its own and is
    /// unaffected by this setting.
    /// </para>
    /// </summary>
    public bool StaticAnalysis { get; init; } = true;

    internal ParserOptions GetParserOptions() => ReferenceEquals(this, Default)
        ? _defaultParserOptions
        : _parserOptionsCache.GetValue(ParsingOptions, _createParserOptions);
}
