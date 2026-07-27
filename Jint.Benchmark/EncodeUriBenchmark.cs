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
/// Every row runs a tight loop over one prepared script on one long-lived engine, so the measurement is the
/// encode, not the parse. <c>[MemoryDiagnoser]</c> matters for the clean row: with an early-out it should
/// allocate the result string only, without one it builds a whole second buffer to produce a copy of its
/// input.
/// </para>
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

    private Engine _engine = null!;

    private Prepared<Script> _cleanUri;
    private Prepared<Script> _cleanComponent;
    private Prepared<Script> _longClean;
    private Prepared<Script> _mixedUri;
    private Prepared<Script> _mixedComponent;
    private Prepared<Script> _dirtyUri;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _engine = new Engine();
        _engine.Execute(Setup);

        _cleanUri = Prepare("encodeURI(clean)");
        _cleanComponent = Prepare("encodeURIComponent(clean)");
        _longClean = Prepare("encodeURI(longClean)");
        _mixedUri = Prepare("encodeURI(mixed)");
        _mixedComponent = Prepare("encodeURIComponent(mixed)");
        _dirtyUri = Prepare("encodeURI(dirty)");

        _engine.Evaluate(_cleanUri);
        _engine.Evaluate(_cleanComponent);
        _engine.Evaluate(_longClean);
        _engine.Evaluate(_mixedUri);
        _engine.Evaluate(_mixedComponent);
        _engine.Evaluate(_dirtyUri);
    }

    private static Prepared<Script> Prepare(string call)
        => Engine.PrepareScript("var r; for (var n = 0; n < " + OperationsPerInvoke + "; n++) { r = " + call + "; } r");

    /// <summary>Nothing to escape: no buffer needs building at all.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue EncodeUri_Clean() => _engine.Evaluate(_cleanUri);

    /// <summary>
    /// The same input through <c>encodeURIComponent</c>, whose allowed set excludes the URI reserved
    /// characters, so this one does escape - the control that the clean row is about the input, not the call.
    /// </summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue EncodeUriComponent_Clean() => _engine.Evaluate(_cleanComponent);

    /// <summary>Eight times the clean input: the early-out's win should scale with the length it skips.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue EncodeUri_LongClean() => _engine.Evaluate(_longClean);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue EncodeUri_Mixed() => _engine.Evaluate(_mixedUri);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue EncodeUriComponent_Mixed() => _engine.Evaluate(_mixedComponent);

    /// <summary>Almost every character escapes: no clean runs to copy, so this row is the floor.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue EncodeUri_Dirty() => _engine.Evaluate(_dirtyUri);
}
