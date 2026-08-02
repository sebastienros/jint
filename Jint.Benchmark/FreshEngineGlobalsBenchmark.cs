#nullable enable

using System.Globalization;
using BenchmarkDotNet.Attributes;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Benchmark;

/// <summary>
/// The cost profile of a thin embedder: a <b>fresh engine per execution</b>, a globals table wired
/// up from scratch on it, and a tiny prepared script on top. Nothing else in this suite covers
/// that combination — <see cref="EngineConstructionBenchmark"/> builds a fresh engine with no
/// globals at all, and <see cref="InteropBenchmark"/> reuses one engine with a handful of globals
/// registered once — so registration cost has never been on a measured path even though it is
/// most of what a per-request embedding does before any script runs.
///
/// <para><b>What each parameter proves</b></para>
/// <list type="bullet">
/// <item><description>
/// <see cref="GlobalCount"/> — 1 / 10 / 40 spans "one entry point" to "a full host API surface".
/// The per-global slope is the number that matters: subtract the <c>GlobalCount=1</c> row from the
/// <c>GlobalCount=40</c> row and divide by 39. A flat slope means registration is free; a steep
/// one means every added host global taxes every execution. Read <c>Allocated</c> alongside
/// <c>Mean</c> — a per-global allocation slope is a per-request GC cost.
/// </description></item>
/// <item><description>
/// <see cref="Kind"/> — <see cref="GlobalKind.Delegate"/> goes through the delegate wrapper, which
/// reflects over the delegate's <c>Invoke</c> signature; <see cref="GlobalKind.ClrFunction"/> is
/// the hand-written escape hatch that skips that. The gap between the two rows is the price of the
/// convenient API, and it is only visible with a fresh engine per operation — on a reused engine
/// it is amortized to nothing. <see cref="GlobalKind.Lazy"/> registers the very same
/// <see cref="ClrFunction"/> bodies through <see cref="OptionsExtensions.AddLazyGlobal"/>, so the
/// Lazy-vs-ClrFunction gap is exactly what deferring the value construction buys or costs — the
/// function objects being built are identical, only <i>when</i> and <i>whether</i> differs.
/// </description></item>
/// </list>
///
/// <para><b>Keeping the lazy row honest</b></para>
/// <para>
/// A lazy global is only cheaper if its factory never runs, so a workload that reads every
/// registered global would measure the eager cost plus an indirection and the row would say
/// nothing. The script here calls <c>fn0</c> and nothing else, so exactly <b>one</b> of the
/// <see cref="GlobalCount"/> registered globals is ever materialized. The
/// <see cref="GlobalCount"/> axis then spans both ends of the honesty question on its own:
/// at <c>GlobalCount=1</c> the single global is read, which is the worst case where laziness is
/// pure overhead and the row must not win; at <c>GlobalCount=40</c> one of forty is read, which is
/// the case the API exists for. A Lazy row that beats <c>ClrFunction</c> at <c>GlobalCount=1</c>
/// would mean the comparison had drifted, not that laziness got faster.
/// </para>
///
/// <para>
/// The delegates are built by a factory <b>per engine</b> and several of them are capturing
/// closures, so every operation hands the engine a fresh delegate instance. That is what an
/// embedder that builds its host API per request actually does, and it is the case that matters
/// for any cache keyed on something coarser than the delegate instance (a <c>MethodInfo</c>, say):
/// a fresh closure instance still shares its <c>MethodInfo</c>, a fresh lambda body does not.
/// Reusing one cached delegate array across operations would measure a different, easier problem.
/// The Lazy row is the one place this cannot apply, and deliberately so: registration lives on
/// <see cref="Options"/>, which the engine replays per construction, so the factory delegates are
/// created once and the per-engine work is the descriptor installation the API actually charges.
/// Re-registering per operation would grow the shared <see cref="Options"/> without bound and
/// measure a leak rather than a feature.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class FreshEngineGlobalsBenchmark
{
    /// <summary>Longest matrix entry; names are precomputed so name formatting is not in the measurement.</summary>
    private const int MaxGlobals = 40;

    private static readonly string[] _names = BuildNames();

    private readonly double[] _sink = new double[1];

    private Options _sharedOptions = null!;
    private Prepared<Script> _script;

    [Params(1, 10, 40)]
    public int GlobalCount { get; set; }

    [Params(GlobalKind.Delegate, GlobalKind.ClrFunction, GlobalKind.Lazy, GlobalKind.LazyPerEngine, GlobalKind.LazyPerEngineWithState)]
    public GlobalKind Kind { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // One Options instance shared by every constructed engine: an embedder configures interop
        // policy once at start-up and only the globals differ per engine.
        _sharedOptions = new Options();

        if (Kind == GlobalKind.Lazy)
        {
            // Lazy globals are declared on the options rather than on the engine, and the engine
            // replays that declaration as it is constructed — so this loop runs once for the whole
            // benchmark and every operation still gets its own descriptors and its own (at most one
            // per engine) factory invocation. The factory produces the same ClrFunction the eager
            // ClrFunction row hands over directly, so the two rows differ only in when it is built.
            for (var i = 0; i < GlobalCount; i++)
            {
                var name = _names[i];
                var index = i;
                _sharedOptions.AddLazyGlobal(name, engine => CreateClrFunction(engine, name, index));
            }
        }

        // One statement, and it calls into the host so global resolution is on the measured path.
        // It reads fn0 only: that single read is what keeps the Lazy row a per-*used*-global cost
        // instead of a restatement of the eager one. See "Keeping the lazy row honest" above.
        _script = Engine.PrepareScript("fn0(1) + fn0(2);");

        // Prove the lane before it is measured.
        RunOnce();
    }

    [Benchmark]
    public JsValue FreshEngineWithGlobals() => RunOnce();

    private JsValue RunOnce()
    {
        var engine = new Engine(_sharedOptions);
        RegisterGlobals(engine);
        return engine.Evaluate(_script);
    }

    private void RegisterGlobals(Engine engine)
    {
        if (Kind == GlobalKind.Lazy)
        {
            // Already installed: constructing the engine replayed the options' registrations.
            return;
        }

        var count = GlobalCount;
        for (var i = 0; i < count; i++)
        {
            var name = _names[i];
            if (Kind == GlobalKind.Delegate)
            {
                engine.SetValue(name, CreateDelegate(i));
            }
            else if (Kind == GlobalKind.LazyPerEngine)
            {
                // Declared on this engine, so the factory may close over per-request state. The closure is
                // built here rather than hoisted for the same reason the Delegate row builds its delegates
                // here: a host whose globals depend on the request has nothing to hoist.
                var index = i;
                engine.Advanced.AddLazyGlobal(name, e => CreateClrFunction(e, name, index));
            }
            else if (Kind == GlobalKind.LazyPerEngineWithState)
            {
                // The same registration with the per-request data passed through instead of captured, so the
                // factory is static and the descriptor is the only thing allocated.
                engine.Advanced.AddLazyGlobal(
                    name,
                    (Owner: this, Name: name, Index: i),
                    static (e, s) => s.Owner.CreateClrFunction(e, s.Name, s.Index));
            }
            else
            {
                engine.SetValue(name, CreateClrFunction(engine, name, i));
            }
        }
    }

    /// <summary>
    /// A mix of arities and return shapes, most of them capturing closures — the realistic output of
    /// a per-engine host API factory rather than a table of cached static lambdas.
    /// </summary>
    private Delegate CreateDelegate(int index)
    {
        var salt = index + 1;
        var sink = _sink;
        return (index % 5) switch
        {
            0 => new Func<double, double>(x => x * salt),
            1 => new Func<double, double, double>((x, y) => x + y + salt),
            2 => new Func<string, string>(s => s + salt.ToString(CultureInfo.InvariantCulture)),
            3 => new Func<double>(() => salt),
            _ => new Action<double>(x => sink[0] += x + salt),
        };
    }

    /// <summary>The same host API expressed the manual way, bypassing delegate signature reflection.</summary>
    private ClrFunction CreateClrFunction(Engine engine, string name, int index)
    {
        var salt = index + 1;
        var sink = _sink;
        return (index % 5) switch
        {
            0 => new ClrFunction(engine, name, (_, args) => JsNumber.Create(args.At(0).AsNumber() * salt), length: 1),
            1 => new ClrFunction(engine, name, (_, args) => JsNumber.Create(args.At(0).AsNumber() + args.At(1).AsNumber() + salt), length: 2),
            2 => new ClrFunction(engine, name, (_, args) => new JsString(args.At(0).ToString() + salt.ToString(CultureInfo.InvariantCulture)), length: 1),
            3 => new ClrFunction(engine, name, (_, _) => JsNumber.Create(salt)),
            _ => new ClrFunction(engine, name, (_, args) =>
            {
                sink[0] += args.At(0).AsNumber() + salt;
                return JsValue.Undefined;
            }, length: 1),
        };
    }

    private static string[] BuildNames()
    {
        var names = new string[MaxGlobals];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = "fn" + i.ToString(CultureInfo.InvariantCulture);
        }

        return names;
    }
}

/// <summary>How a host function is handed to a fresh engine.</summary>
public enum GlobalKind
{
    /// <summary>A CLR delegate — the convenient API, wrapped by the engine.</summary>
    Delegate,

    /// <summary>A hand-written <see cref="ClrFunction"/> — no delegate signature reflection.</summary>
    ClrFunction,

    /// <summary>
    /// The same <see cref="ClrFunction"/> bodies, declared through
    /// <see cref="OptionsExtensions.AddLazyGlobal"/>: the property is installed on every engine but
    /// its value is only built if the script reads it. Turns the per-global registration slope into
    /// a per-<i>used</i>-global one.
    /// </summary>
    Lazy,

    /// <summary>
    /// The same declaration made on the engine itself through
    /// <see cref="Engine.AdvancedOperations.AddLazyGlobal"/>, which is what a host must use when the value
    /// depends on per-request state a shared <see cref="Options"/> cannot hold — a scoped service provider,
    /// a request, a workflow context.
    /// <para>
    /// It is a genuinely different cost from <see cref="Lazy"/> rather than a restatement of it. The options
    /// registration happens once for the process and the engine only replays it, so its factory delegates are
    /// created once; this one runs the registration itself on every engine, so anything the API allocates per
    /// call is a per-request cost. That makes this the row that decides whether declaring a per-request global
    /// is cheaper than simply building it.
    /// </para>
    /// </summary>
    LazyPerEngine,

    /// <summary>
    /// <see cref="LazyPerEngine"/> registered through the state-taking overload, so the factory is a
    /// <see langword="static"/> lambda rather than a closure. The gap between the two rows is exactly what a
    /// capturing factory costs a host that registers per engine — nothing else about the two differs, the
    /// same function objects are built at the same moment.
    /// </summary>
    LazyPerEngineWithState,
}
