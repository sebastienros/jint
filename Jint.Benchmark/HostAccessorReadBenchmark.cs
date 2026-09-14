#nullable enable

using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// What it costs to <em>invoke</em> a host accessor once its descriptor is in hand, isolated from everything
/// that finds the descriptor. An Ultra capture of a DOM-property-read loop on sebastienros/jint#4013 put
/// <c>ObjectInstance.UnwrapFromGetter</c>'s subtree at 29.1% of the page-loop thread, and 17.0% of that
/// subtree — the single largest item after the <c>Engine.Call</c> frame itself — in
/// <c>StackGuard.ProbeStackHeadroom</c>, because a getter reached through <c>Engine.Call</c> probes the
/// native stack twice: once in <c>Engine.CallNativeFunction</c> and once at the top of
/// <c>ClrFunction.Call</c>. <see cref="BrowserPropertyReadBenchmark"/> measures that path with the whole DOM
/// and a <c>Page.EvaluateAsync</c> round trip around it; this class measures the same invocation with
/// neither, so a change to the invocation itself is not diluted by a document.
/// <para>
/// It still probes twice, and this class is why that is a settled question rather than an open one. Skipping
/// the dispatcher's probe for a callee that probes for itself measured as no change here and no change on
/// every <see cref="BrowserPropertyReadBenchmark"/> DOM row over six paired rounds, so it did not ship — the
/// table, and the calibration artefact <see cref="DataPropertyRead"/> caught while it was being taken, are in
/// <c>Jint.Benchmark/AGENTS.md</c>. The rows stay because the path is worth a row whatever is done to it.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>What each row is.</b> <see cref="AccessorRead"/> is the subject: a read of a host <em>accessor</em>
/// declared on a <see cref="JsObjectShape"/> prototype — the same public mechanism the generated DOM
/// bindings use, so the getter materializes as the same <c>ClrFunction</c> and the read lands in the same
/// <c>UnwrapFromGetter</c> → <c>Engine.Call</c> path the capture shows. <see cref="MethodCall"/> is the
/// control, and a deliberately sharp one: the <em>same</em> callee body on the <em>same</em> prototype
/// shape, invoked as <c>host.read()</c> instead. A call expression dispatches through
/// <c>JintCallExpression</c>'s own native branch, which never had the dispatcher-side probe, so this row
/// has always probed once — anything that moves it moved the callee, not the dispatch.
/// <see cref="DataPropertyRead"/> is the floor: the identical loop reading an inherited data property, which
/// invokes nothing at all, so the gap between it and <see cref="AccessorRead"/> is the whole cost of
/// projecting a read through a host getter.
/// </para>
/// <para>
/// <b>Pass count.</b> Each row loops its read <see cref="Passes"/> times inside one script, so a row's
/// number is dominated by the read rather than by <c>Evaluate</c>'s own entry, and the accumulated total is
/// returned so nothing in the loop can be elided. A single host-accessor read is tens of nanoseconds; 200k
/// passes puts every row in the milliseconds.
/// </para>
/// <para>
/// <b>Engine isolation.</b> One <see cref="Engine"/> per row, built in <c>[GlobalSetup]</c> by the row's own
/// factory and warmed with that row's script and nothing else (<see cref="IsolatedScript"/>) — a class whose
/// whole subject is one dispatch path must not let one row warm another's call-site caches. Engine
/// construction, the prototype instantiation and the warm-up stay outside the measurement; the rows measure
/// warm dispatch, which is what an embedder's steady state is.
/// </para>
/// <para>
/// <b>Public surface only.</b> <c>Jint.Benchmark</c> holds an <c>InternalsVisibleTo</c> grant, but
/// everything here — <see cref="JsObjectShape.Builder"/>, <see cref="JsObjectShape.Instantiate(Engine)"/>,
/// <see cref="Engine.SetValue(string, JsValue)"/> — is reachable by an embedder in an unrelated assembly,
/// so the row measures a host somebody could actually write.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class HostAccessorReadBenchmark
{
    /// <summary>
    /// Reads per invocation. A host accessor read costs tens of nanoseconds and the data-property floor
    /// costs single digits, so one count keeps every row in the milliseconds without making the floor
    /// dominated by <c>Evaluate</c>'s own entry.
    /// </summary>
    private const int Passes = 200_000;

    /// <summary>
    /// One prototype's worth of members, declared once and instantiated per engine exactly as a binding
    /// generator would: <c>value</c> as an accessor, <c>read</c> as an operation with the identical body.
    /// The body returns a cached small integer, so it is as close to free as a callee can be and whatever
    /// the row reports is the dispatch around it.
    /// </summary>
    private static readonly JsObjectShape HostPrototypeShape = new JsObjectShape.Builder()
        .Accessor("value", static (_, _) => JsNumber.Create(1))
        .Method("read", static (_, _) => JsNumber.Create(1))
        .Build();

    private IsolatedScript _accessorRead;
    private IsolatedScript _methodCall;
    private IsolatedScript _dataPropertyRead;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _accessorRead = IsolatedScript.Warm(Engine.PrepareScript(Loop("host.value")), CreateHostEngine);
        _methodCall = IsolatedScript.Warm(Engine.PrepareScript(Loop("host.read()")), CreateHostEngine);
        _dataPropertyRead = IsolatedScript.Warm(Engine.PrepareScript(Loop("host.value")), CreatePlainEngine);
    }

    /// <summary>
    /// An engine whose <c>host</c> is an ordinary object inheriting from the shaped host prototype — the
    /// arrangement a DOM wrapper has, where the members live on the interface prototype and the read
    /// arrives with the instance as its receiver.
    /// </summary>
    private static Engine CreateHostEngine()
    {
        var engine = new Engine();
        engine.SetValue("hostPrototype", HostPrototypeShape.Instantiate(engine));
        engine.Execute("var host = Object.create(hostPrototype);");
        return engine;
    }

    /// <summary>
    /// The floor's engine: the same two-level arrangement and the same property name, with <c>value</c> an
    /// inherited data property, so the row differs from <see cref="AccessorRead"/> in the invocation and
    /// nothing else it can help.
    /// </summary>
    private static Engine CreatePlainEngine()
    {
        var engine = new Engine();
        engine.Execute("var host = Object.create({ value: 1 });");
        return engine;
    }

    private static string Loop(string expression) => $$"""
        (function () {
            var total = 0;
            for (var i = 0; i < {{Passes}}; i++) {
                total += {{expression}};
            }
            return total;
        })()
        """;

    [Benchmark]
    public JsValue AccessorRead() => _accessorRead.Run();

    [Benchmark]
    public JsValue MethodCall() => _methodCall.Run();

    [Benchmark(Baseline = true)]
    public JsValue DataPropertyRead() => _dataPropertyRead.Run();

    [GlobalCleanup]
    public void Cleanup()
    {
        _accessorRead.Engine.Dispose();
        _methodCall.Engine.Dispose();
        _dataPropertyRead.Engine.Dispose();
    }
}
