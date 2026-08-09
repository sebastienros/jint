#nullable enable

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using Jint.Native;
using Jint.Runtime.Modules;

// Jint.Benchmark does not inherit the repository-root Directory.Build.props, so the AstModule alias
// it declares globally for the engine's own projects has to be spelled out here. Both names are
// needed in this file: the loaders cache Acornima's parsed Module and return Jint's runtime Module.
using AstModule = Acornima.Ast.Module;
using Module = Jint.Runtime.Modules.Module;

namespace Jint.Benchmark;

/// <summary>
/// What an <b>ES module graph</b> costs a pooled-engine embedder — a server-side view engine whose templates
/// are modules, whose engines are built per request (or drawn from a pool refilled in bursts), and whose
/// module sources never change between requests. Nothing else in this suite touches the module system at all:
/// there is no <c>Engine.Modules</c>, no <see cref="IModuleLoader"/> and no <see cref="Engine.PrepareModule"/>
/// anywhere in <c>Jint.Benchmark</c>, so every cost below — specifier resolution, per-engine module-record
/// construction, linking, evaluation of ten module bodies — has never been on a measured path.
///
/// <para><b>The fixture</b></para>
/// <para>
/// Ten modules in memory, keyed by absolute <c>app:///</c> uris so the loader's key and
/// <see cref="ResolvedSpecifier.Uri"/> shapes match what <see cref="DefaultModuleLoader"/> produces from disk
/// (it keys by <see cref="Uri.AbsoluteUri"/>) without a file system in the measurement. One entry module
/// importing eight leaf components plus a shared utility module, and every leaf importing that same utility —
/// a diamond, so the module map is doing real work rather than walking a chain. Every leaf's
/// <c>render(data)</c> loops over the same ten-item data object and concatenates a small HTML string, which is
/// what the steady-state row actually executes.
/// </para>
///
/// <para><b>What each row gates</b></para>
/// <list type="bullet">
/// <item><description>
/// <see cref="PrepareModuleGraph"/> — preparation alone, no engine. The denominator for every claim about
/// what caching prepared modules buys.
/// </description></item>
/// <item><description>
/// <see cref="ColdImport_SourcePerEngine"/> vs <see cref="ColdImport_PreparedShared"/> — the solitary cold
/// start, parsing per engine against materializing from a warm shared cache. The gap is the whole reason an
/// embedder writes a caching loader.
/// </description></item>
/// <item><description>
/// <see cref="ColdImport_PreparedPerOp"/> — the diagnostic that separates <i>shared across engines</i> from
/// <i>prepared-tree shape</i>: same prepared trees, built fresh inside the operation and thrown away.
/// <see cref="PreparedAnomalyBenchmarks"/> carries the same arm for scripts, and for the same reason.
/// </description></item>
/// <item><description>
/// <see cref="PoolFill_PreparedShared"/> — filling a pool of <see cref="PoolSize"/> engines from one prepared
/// cache. <c>Allocated</c> is this row's primary gate column: it answers whether K engines materializing the
/// same graph duplicate the shared work K times or merely the per-engine module records.
/// </description></item>
/// <item><description>
/// <see cref="WarmRedraw_ReusedEngine"/> — steady state, the campaign's canary. One long-lived engine whose
/// graph is already linked and evaluated, re-rendering. Nothing here should move unless something in the
/// interpreter did.
/// </description></item>
/// </list>
///
/// <para><b>Engine construction is inside the measurement on the four cold rows, deliberately.</b></para>
/// <para>
/// Each of them needs an engine that has never seen this graph, and building it in an
/// <c>[IterationSetup]</c> would force <c>InvocationCount=1</c> and leak tiered-JIT warmup into the measured
/// iterations — the failure <see cref="DromaeoBenchmark"/> documents at length, where identical code reported
/// 2.489 ms and 9.811 ms in different runs. Building the engine inside the benchmark method instead lets
/// BenchmarkDotNet auto-scale the invocation count, so tiering finishes during the pilot. Engine construction
/// therefore counts: roughly 0.1-0.3 ms per engine, constant across revisions, so A/B deltas over these rows
/// stay valid even though the absolute numbers carry that floor. <see cref="PoolFill_PreparedShared"/> carries
/// it <see cref="PoolSize"/> times over, which is exactly what a pool refill pays.
/// </para>
///
/// <para><b>The pool fill is sequential on purpose.</b></para>
/// <para>
/// A concurrent burst would put thread-pool ramp and scheduler jitter on top of the code under test, and it
/// would not measure anything extra: the property this row exists for — K engines materializing from one
/// prepared cache without duplicating the shared work — is exercised identically whether the engines are built
/// one after another or at once. Thread <i>safety</i> of a shared <c>Prepared&lt;T&gt;</c> across concurrent
/// engines is <c>Jint.Tests.CommonScripts/ConcurrencyTest.cs</c>'s job, not a benchmark's.
/// </para>
///
/// <para><b>Restricted to the public surface deliberately.</b></para>
/// <para>
/// <c>Jint.Benchmark</c> has <c>InternalsVisibleTo</c>, so a loader written here could reach members no real
/// embedder has. Both loaders below are limited to exactly what a third-party pooled host composes:
/// <see cref="IModuleLoader"/>, <see cref="ResolvedSpecifier"/>, <see cref="SpecifierType"/>, both
/// <c>ModuleFactory.BuildSourceTextModule</c> overloads, <see cref="Engine.PrepareModule"/>,
/// <c>Options.EnableModules(IModuleLoader)</c> and <c>engine.Modules.Import</c>. They implement
/// <see cref="IModuleLoader"/> directly rather than deriving
/// from <see cref="ModuleLoader"/>, because that base class funnels every load through
/// <c>LoadModuleContents(...): string</c> and re-parses the returned source on every engine — there is no
/// prepared-AST seam in it, so the shared-cache lane cannot be expressed through it at all.
/// </para>
/// <para>
/// Two places the restriction bites, both worth keeping. An import always pays specifier resolution plus a
/// module-map lookup per operation, even when the loader could answer from a single field: a hand-rolled
/// internal harness could hand the engine a module record directly, a real host cannot, so that cost stays in
/// every cold row. And the parse counter that <see cref="GlobalSetup"/> asserts on is the loader's own
/// <see cref="Interlocked"/> counter rather than any engine-side statistic, because a third party has no
/// visibility into how often the engine asked for a module.
/// </para>
///
/// <para><b>No <c>[Params]</c>.</b></para>
/// <para>
/// The graph is fixed at <see cref="ModuleCount"/> modules and the pool at <see cref="PoolSize"/> engines.
/// Both axes would multiply the standing cost of a gate that already builds up to eight engines per operation,
/// and neither produces a signal the campaign needs: the questions are all about the <i>shape</i> of a
/// lifecycle, not about how the cost scales with graph size, and a slope in module count is already visible by
/// dividing <see cref="PrepareModuleGraph"/> by ten.
/// </para>
///
/// <para><b>How to read a delta from this class: allocation is the signal, time needs medians.</b></para>
/// <para>
/// Every row here builds engines and walks a module graph, so its time is far noisier between processes than
/// BenchmarkDotNet's own <c>StdDev</c> column suggests — that column measures one warm process, which these
/// rows are not. Thirty consecutive default-job runs of one unmodified binary, serial on an idle machine,
/// gave this envelope, where <i>p95 pairwise</i> is the 95th percentile of the absolute difference between
/// two runs of <b>identical code</b> — i.e. what a single before/after pair can report from nothing at all:
/// </para>
/// <list type="table">
/// <listheader><term>Row</term><description>p95 pairwise / full spread (time)</description></listheader>
/// <item><term><see cref="WarmRedraw_ReusedEngine"/></term><description>6.8% / 9.2%</description></item>
/// <item><term><see cref="PrepareModuleGraph"/></term><description>5.3% / 6.7%</description></item>
/// <item><term><see cref="ColdImport_PreparedPerOp"/></term><description>5.3% / 7.4%</description></item>
/// <item><term><see cref="ColdImport_PreparedShared"/></term><description>3.3% / 5.0%</description></item>
/// <item><term><see cref="ColdImport_SourcePerEngine"/></term><description>3.3% / 5.2%</description></item>
/// <item><term><see cref="PoolFill_PreparedShared"/></term><description>2.5% / 3.6%</description></item>
/// </list>
/// <para>
/// So a single pair's time delta below those numbers carries no information, and a gate threshold under them
/// fires on noise. Two controls make the point concretely: adding a semantically inert internal class to the
/// engine, and adding an unused method to <c>Throw</c>, each moved <see cref="WarmRedraw_ReusedEngine"/> by
/// about 3% without changing a single executed instruction. To see a smaller effect than the envelope, compare
/// the <b>median of five runs per side</b> rather than one pair.
/// </para>
/// <para>
/// The <c>Allocated</c> column has no such problem: it was byte-identical across all thirty runs, on every
/// row. Where a change should show up in allocation, that column is the one to gate on and the one to quote.
/// </para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ModuleGraphEmbeddingBenchmark
{
    /// <summary>Entry + util + eight leaf components.</summary>
    private const int ModuleCount = 10;

    /// <summary>Engines built per <see cref="PoolFill_PreparedShared"/> operation.</summary>
    private const int PoolSize = 8;

    private const string EntryKey = "app:///main.js";
    private const string UtilKey = "app:///util.js";
    private const string ComponentPrefix = "app:///components/";

    /// <summary>
    /// A leaf component: the module file name (also its BEM-ish class), the HTML element it renders, and the
    /// local name the entry module imports its <c>render</c> under.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct ComponentSpec(string Name, string Tag, string Alias);

    private static readonly ComponentSpec[] Components =
    [
        new("nav-bar", "nav", "navBar"),
        new("card", "section", "card"),
        new("list-item", "li", "listItem"),
        new("avatar", "figure", "avatar"),
        new("badge", "em", "badge"),
        new("button", "button", "button"),
        new("tool-tip", "aside", "toolTip"),
        new("footer", "footer", "footer"),
    ];

    /// <summary>Every module key, in a deterministic order, entry first.</summary>
    private static readonly string[] Keys = BuildKeys();

    private static readonly Dictionary<string, string> Sources = BuildSources();

    /// <summary>
    /// The host-supplied render input, as an object literal. Prepared once and shared: a
    /// <c>Prepared&lt;Script&gt;</c> is documented as reusable and thread-safe across engines, so evaluating it
    /// per engine costs a tiny evaluation rather than a parse, and the data object every row renders is
    /// identical by construction.
    /// </summary>
    private static readonly Prepared<Script> DataScript = Engine.PrepareScript(
        """
        ({
            title: 'Dashboard & Reports <live>',
            theme: 'compact',
            items: ['alpha', 'beta', 'gamma & delta', 'epsilon', 'zeta',
                    'eta <b>', 'theta', 'iota', 'kappa', 'lambda']
        })
        """,
        "app:///data.js");

    /// <summary>The one warm shared cache the two sharing rows draw from.</summary>
    private SharedPreparedLoader _sharedLoader = null!;

    /// <summary>
    /// Hoisted so the shared rows do not allocate a configuration closure per engine — the pool row's
    /// <c>Allocated</c> column is a gate, and a closure per engine would be noise inside it.
    /// </summary>
    private Action<Options> _configureShared = null!;

    private Action<Options> _configureSourcePerEngine = null!;

    private Engine _warmEngine = null!;
    private JsValue _warmRender = null!;
    private JsValue _warmData = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _sharedLoader = new SharedPreparedLoader();
        _configureShared = options => options.EnableModules(_sharedLoader);

        // Stateless, so one instance serves every engine — which is also what a host registers.
        var sourceLoader = new SourcePerEngineLoader();
        _configureSourcePerEngine = options => options.EnableModules(sourceLoader);

        // Prove the shared lane before it is measured. The first throwaway engine populates the cache;
        // the second must find all ten entries already there, which is the actual claim the two sharing
        // rows rest on — not merely "the cache was written to", but "a second engine reused it".
        ImportAndRender(new Engine(_configureShared));
        AssertParsed(_sharedLoader, "shared prepared cache, first engine");

        ImportAndRender(new Engine(_configureShared));
        AssertParsed(_sharedLoader, "shared prepared cache, second engine");

        // The steady-state row's engine gets its own loader and is warmed with exactly this row's workload
        // and nothing else: one import, one export lookup, one data object, one render. Warming it with any
        // other row's script would hand it that row's globals and handler-tree state (AGENTS.md, "Never warm
        // one engine with more than one row's workload").
        var warmLoader = new SharedPreparedLoader();
        _warmEngine = new Engine(options => options.EnableModules(warmLoader));
        var warmNamespace = _warmEngine.Modules.Import(EntryKey);
        _warmRender = warmNamespace.Get("render");
        _warmData = _warmEngine.Evaluate(DataScript);
        _warmEngine.Invoke(_warmRender, _warmData);
        AssertParsed(warmLoader, "steady-state engine's own cache");
    }

    /// <summary>
    /// Preparation alone: ten <see cref="Engine.PrepareModule"/> calls, no engine anywhere. The list is
    /// returned so BenchmarkDotNet consumes it and nothing is eliminated as dead.
    /// </summary>
    [Benchmark]
    public List<Prepared<AstModule>> PrepareModuleGraph()
    {
        var prepared = new List<Prepared<AstModule>>(ModuleCount);
        foreach (var key in Keys)
        {
            prepared.Add(Engine.PrepareModule(Sources[key], key));
        }

        return prepared;
    }

    /// <summary>
    /// Solitary cold start with no cross-engine sharing at all: a fresh engine parses all ten modules itself,
    /// links, evaluates and renders once. The baseline an embedder starts from before writing a caching
    /// loader.
    /// </summary>
    [Benchmark]
    public JsValue ColdImport_SourcePerEngine() => ImportAndRender(new Engine(_configureSourcePerEngine));

    /// <summary>
    /// The same cold start against the warm shared cache: the engine parses nothing, and pays only specifier
    /// resolution, per-engine module-record construction, linking and evaluation. Against
    /// <see cref="ColdImport_SourcePerEngine"/>, this is what a caching loader buys.
    /// </summary>
    [Benchmark]
    public JsValue ColdImport_PreparedShared() => ImportAndRender(new Engine(_configureShared));

    /// <summary>
    /// Diagnostic arm: the same prepared trees, built fresh inside the operation and never shared. Separates
    /// "shared across engines" from "prepared-tree shape" as the cause of any gap between the two rows above —
    /// this row should land near <see cref="PrepareModuleGraph"/> plus
    /// <see cref="ColdImport_PreparedShared"/>, and any surplus is the cost of a cold prepared tree rather
    /// than of parsing.
    /// </summary>
    [Benchmark]
    public JsValue ColdImport_PreparedPerOp()
    {
        var loader = new SharedPreparedLoader();
        loader.PrepareAll();
        return ImportAndRender(new Engine(options => options.EnableModules(loader)));
    }

    /// <summary>
    /// A pool refill: <see cref="PoolSize"/> engines built one after another, each importing and rendering the
    /// same graph from the one warm shared cache. <c>Allocated</c> is the primary gate column — it says
    /// whether the K engines duplicate the shared work or only their own module records. Sequential by design;
    /// see the class remarks.
    /// </summary>
    [Benchmark]
    public JsValue PoolFill_PreparedShared()
    {
        JsValue result = JsValue.Undefined;
        for (var i = 0; i < PoolSize; i++)
        {
            result = ImportAndRender(new Engine(_configureShared));
        }

        return result;
    }

    /// <summary>
    /// Steady state on a long-lived engine whose graph is already linked and evaluated: one call into the
    /// entry module's <c>render</c>. No module machinery is on this path at all, which is the point — it is
    /// the canary that says whether a change aimed at the cold rows moved the warm one.
    /// </summary>
    [Benchmark]
    public JsValue WarmRedraw_ReusedEngine() => _warmEngine.Invoke(_warmRender, _warmData);

    private static JsValue ImportAndRender(Engine engine)
    {
        var ns = engine.Modules.Import(EntryKey);
        var render = ns.Get("render");
        var data = engine.Evaluate(DataScript);
        return engine.Invoke(render, data);
    }

    private static void AssertParsed(SharedPreparedLoader loader, string what)
    {
        if (loader.ModulesParsed != ModuleCount)
        {
            throw new InvalidOperationException(
                $"Module-graph fixture is broken: {what} parsed {loader.ModulesParsed} modules, expected {ModuleCount}. " +
                "Every row that draws on a prepared cache would be measuring something other than sharing.");
        }
    }

    /// <summary>
    /// Resolution shared by both loaders, so the two lanes differ only in how a module is <i>built</i>.
    /// Relative specifiers resolve against the referencing module's location the way a browser or
    /// <see cref="DefaultModuleLoader"/> does, and the resulting absolute uri is both the
    /// <see cref="ResolvedSpecifier.Key"/> and the key into the in-memory source table.
    /// </summary>
    private static ResolvedSpecifier ResolveInGraph(string? referencingModuleLocation, ModuleRequest moduleRequest)
    {
        var specifier = moduleRequest.Specifier;

        Uri uri;
        if (Uri.TryCreate(specifier, UriKind.Absolute, out var absolute))
        {
            uri = absolute;
        }
        else if (referencingModuleLocation is not null
            && Uri.TryCreate(referencingModuleLocation, UriKind.Absolute, out var baseUri)
            && Uri.TryCreate(baseUri, specifier, out var relative))
        {
            uri = relative;
        }
        else
        {
            throw new InvalidOperationException(
                $"Cannot resolve module specifier '{specifier}' from '{referencingModuleLocation ?? "<entry>"}'.");
        }

        var key = uri.AbsoluteUri;
        if (!Sources.ContainsKey(key))
        {
            throw new InvalidOperationException(
                $"Unknown module '{key}' (specifier '{specifier}' from '{referencingModuleLocation ?? "<entry>"}').");
        }

        return new ResolvedSpecifier(moduleRequest, key, uri, SpecifierType.RelativeOrAbsolute);
    }

    private static string SourceFor(string key)
    {
        if (!Sources.TryGetValue(key, out var code))
        {
            throw new InvalidOperationException($"No module source registered under '{key}'.");
        }

        return code;
    }

    /// <summary>
    /// The caching shape a pooled embedder writes: one <see cref="Prepared{TProgram}"/> per module, built
    /// once and handed to every engine that asks. Modelled on <c>CachedModuleLoader</c> in
    /// <c>Jint.Tests.PublicInterface/ModuleLoaderTests.cs</c>, which is the documented example of this
    /// pattern.
    /// </summary>
    private sealed class SharedPreparedLoader : IModuleLoader
    {
        private readonly ConcurrentDictionary<string, Prepared<AstModule>> _prepared = new(StringComparer.Ordinal);
        private int _modulesParsed;

        /// <summary>
        /// How many modules this loader has actually parsed. The only sharing evidence available to a third
        /// party, and what <see cref="GlobalSetup"/> asserts on.
        /// </summary>
        public int ModulesParsed => Volatile.Read(ref _modulesParsed);

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => ResolveInGraph(referencingModuleLocation, moduleRequest);

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
        {
            var prepared = GetOrPrepare(resolved.Key);
            return ModuleFactory.BuildSourceTextModule(engine, in prepared);
        }

        /// <summary>Warms the whole graph up front, the way a host does at start-up.</summary>
        public void PrepareAll()
        {
            foreach (var key in Keys)
            {
                GetOrPrepare(key);
            }
        }

        private Prepared<AstModule> GetOrPrepare(string key)
            => _prepared.GetOrAdd(key, static (k, self) => self.Prepare(k), this);

        private Prepared<AstModule> Prepare(string key)
        {
            var prepared = Engine.PrepareModule(SourceFor(key), key);
            Interlocked.Increment(ref _modulesParsed);
            return prepared;
        }
    }

    /// <summary>
    /// The no-cache shape: every engine re-parses every module it imports. This is what the base
    /// <see cref="ModuleLoader"/> does for a host that only overrides <c>LoadModuleContents</c>, expressed
    /// directly so the two lanes share one <c>Resolve</c> and differ in nothing else.
    /// </summary>
    private sealed class SourcePerEngineLoader : IModuleLoader
    {
        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => ResolveInGraph(referencingModuleLocation, moduleRequest);

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
        {
            // ModuleParsingOptions.Default, not a fresh instance: GetParserOptions() short-circuits on
            // ReferenceEquals(this, Default), so handing it a new instance would clone a ParserOptions per
            // module load — an allocation the base ModuleLoader (which funnels null to Default) never pays,
            // and pure noise against what this row measures.
            return ModuleFactory.BuildSourceTextModule(engine, resolved, SourceFor(resolved.Key), ModuleParsingOptions.Default);
        }
    }

    private static string[] BuildKeys()
    {
        var keys = new string[ModuleCount];
        keys[0] = EntryKey;
        keys[1] = UtilKey;
        for (var i = 0; i < Components.Length; i++)
        {
            keys[i + 2] = ComponentPrefix + Components[i].Name + ".js";
        }

        return keys;
    }

    private static Dictionary<string, string> BuildSources()
    {
        var sources = new Dictionary<string, string>(ModuleCount, StringComparer.Ordinal)
        {
            [EntryKey] = BuildEntrySource(),
            [UtilKey] = UtilSource,
        };

        foreach (var component in Components)
        {
            sources[ComponentPrefix + component.Name + ".js"] = BuildComponentSource(component);
        }

        return sources;
    }

    private static string BuildEntrySource()
    {
        var builder = new StringBuilder();
        foreach (var component in Components)
        {
            builder.Append("import { render as ").Append(component.Alias)
                .Append(" } from './components/").Append(component.Name).Append(".js';\n");
        }

        builder.Append("import { esc } from './util.js';\n\n");
        builder.Append("export function render(data) {\n");
        builder.Append("    var out = '<div class=\"page\" title=\"' + esc(data.title) + '\">';\n");
        foreach (var component in Components)
        {
            builder.Append("    out += ").Append(component.Alias).Append("(data);\n");
        }

        builder.Append("    return out + '</div>';\n");
        builder.Append("}\n");

        return builder.ToString();
    }

    /// <summary>
    /// Deliberately regex-free. <c>String.prototype.replaceAll</c> with a string pattern keeps this a plain
    /// string replace; a regex literal here would be compiled at <see cref="Engine.PrepareModule"/> time
    /// (<c>ModulePreparationOptions</c> uses the compiling RegExp handler), which would put .NET
    /// <c>Regex</c> IL emission inside <see cref="PrepareModuleGraph"/> and
    /// <see cref="ColdImport_PreparedPerOp"/> and inside neither of the rows they are compared against.
    /// </summary>
    private const string UtilSource = """
        export function esc(s) {
            return ('' + s).replaceAll('&', '&amp;').replaceAll('<', '&lt;');
        }

        export function cls(name, mod) {
            return mod ? name + ' ' + name + '--' + mod : name;
        }
        """;

    /// <summary>
    /// Every leaf shares one shape and differs only in element and class name, so the graph's cost is eight
    /// times one component rather than a mixture that would have to be apportioned. Each imports the shared
    /// utility module, which is what makes the graph a diamond.
    /// </summary>
    private static string BuildComponentSource(ComponentSpec component) => $$"""
        import { esc, cls } from '../util.js';

        export function render(data) {
            var out = '<{{component.Tag}} class="' + cls('{{component.Name}}', data.theme) + '">';
            for (var i = 0; i < data.items.length; i++) {
                out += '<span>' + esc(data.items[i]) + '</span>';
            }
            return out + '</{{component.Tag}}>';
        }
        """;
}
