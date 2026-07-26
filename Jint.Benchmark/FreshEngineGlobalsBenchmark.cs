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
/// it is amortized to nothing.
/// </description></item>
/// </list>
///
/// <para>
/// The delegates are built by a factory <b>per engine</b> and several of them are capturing
/// closures, so every operation hands the engine a fresh delegate instance. That is what an
/// embedder that builds its host API per request actually does, and it is the case that matters
/// for any cache keyed on something coarser than the delegate instance (a <c>MethodInfo</c>, say):
/// a fresh closure instance still shares its <c>MethodInfo</c>, a fresh lambda body does not.
/// Reusing one cached delegate array across operations would measure a different, easier problem.
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

    // GlobalKind.Lazy is intentionally absent from [Params] — see GlobalSetup.
    [Params(GlobalKind.Delegate, GlobalKind.ClrFunction)]
    public GlobalKind Kind { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (Kind == GlobalKind.Lazy)
        {
            // TODO: enable once lazy global registration lands — the embedder hands over a name and
            // a factory, and the engine only builds the function object if the script reads the
            // name. That turns the per-global slope measured here into a per-*used*-global slope.
            throw new NotSupportedException($"{Kind} awaits a Jint feature that does not exist yet; see the TODO in {nameof(GlobalSetup)}.");
        }

        // One Options instance shared by every constructed engine: an embedder configures interop
        // policy once at start-up and only the globals differ per engine.
        _sharedOptions = new Options();

        // One statement, and it calls into the host so global resolution is on the measured path.
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
        var count = GlobalCount;
        for (var i = 0; i < count; i++)
        {
            var name = _names[i];
            if (Kind == GlobalKind.Delegate)
            {
                engine.SetValue(name, CreateDelegate(i));
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

    /// <summary>Awaits lazy global registration. Not runnable yet.</summary>
    Lazy,
}
