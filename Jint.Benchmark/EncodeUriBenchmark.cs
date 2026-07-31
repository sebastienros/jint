using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The encode twin of <see cref="DecodeUriBenchmark"/>. <c>encodeURI</c>/<c>encodeURIComponent</c> walk the
/// input deciding per character whether it needs escaping, so the rows separate the three inputs that walk
/// differs on: one that needs no escaping at all (the common case for an already-safe URI, and the one an
/// early-out answers without touching a buffer), one that is mostly clean with a few escapes spread through
/// it (where copying whole clean runs pays), and one where nearly every character escapes (the floor - all
/// work, no runs to copy).
/// <para>
/// Every row runs a tight loop over one prepared script on its own long-lived engine, so the measurement is
/// the encode, not the parse. <c>[MemoryDiagnoser]</c> matters for the clean row: with an early-out it should
/// allocate the result string only, without one it builds a whole second buffer to produce a copy of its
/// input.
/// </para>
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and nothing else
/// (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all six scripts, so a row was
/// measured on an engine carrying the other five rows' globals (every script declares <c>r</c> and <c>n</c>)
/// and their handler-tree and per-call-site state — which makes a row's number depend on what a change did to
/// its siblings. The input fixture the rows share is still shared, but now by being re-run per engine rather
/// than by the engine being re-used. The rows still measure warm encoding, and engine construction and
/// warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not
/// comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class EncodeUriBenchmark
{
    private const int OperationsPerInvoke = 1_000;

    private const string Setup = """
        var clean = 'https://example.com/path/to/resource-name_v2.html';
        var mixed = 'https://example.com/search?q=hello world&lang=en#some fragment';
        var dirty = '中文 éèê / 中文 éèê';
        var longClean = clean + clean + clean + clean + clean + clean + clean + clean;
        """;

    private IsolatedScript _cleanUri;
    private IsolatedScript _cleanComponent;
    private IsolatedScript _longClean;
    private IsolatedScript _mixedUri;
    private IsolatedScript _mixedComponent;
    private IsolatedScript _dirtyUri;

    /// <summary>Builds a fresh engine carrying the input fixture every row reads from.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute(Setup);
        return engine;
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _cleanUri = IsolatedScript.Warm(Prepare("encodeURI(clean)"), CreateEngine);
        _cleanComponent = IsolatedScript.Warm(Prepare("encodeURIComponent(clean)"), CreateEngine);
        _longClean = IsolatedScript.Warm(Prepare("encodeURI(longClean)"), CreateEngine);
        _mixedUri = IsolatedScript.Warm(Prepare("encodeURI(mixed)"), CreateEngine);
        _mixedComponent = IsolatedScript.Warm(Prepare("encodeURIComponent(mixed)"), CreateEngine);
        _dirtyUri = IsolatedScript.Warm(Prepare("encodeURI(dirty)"), CreateEngine);
    }

    private static Prepared<Script> Prepare(string call)
        => Engine.PrepareScript("var r; for (var n = 0; n < " + OperationsPerInvoke + "; n++) { r = " + call + "; } r");

    /// <summary>Nothing to escape: no buffer needs building at all.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue EncodeUri_Clean() => _cleanUri.Run();

    /// <summary>
    /// The same input through <c>encodeURIComponent</c>, whose allowed set excludes the URI reserved
    /// characters, so this one does escape - the control that the clean row is about the input, not the call.
    /// </summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue EncodeUriComponent_Clean() => _cleanComponent.Run();

    /// <summary>Eight times the clean input: the early-out's win should scale with the length it skips.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue EncodeUri_LongClean() => _longClean.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue EncodeUri_Mixed() => _mixedUri.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue EncodeUriComponent_Mixed() => _mixedComponent.Run();

    /// <summary>Almost every character escapes: no clean runs to copy, so this row is the floor.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue EncodeUri_Dirty() => _dirtyUri.Run();
}
