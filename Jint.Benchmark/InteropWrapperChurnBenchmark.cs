using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Measures ObjectWrapper churn: a CLR member returning the SAME object reference repeatedly.
/// Since 4.14 the default engine keeps a small bounded cache of recently wrapped objects
/// (CacheRecentObjectWrappers), so repeated crossings reuse the wrapper; the NoCache rows opt out
/// to measure the fresh-wrapper-per-crossing path, and the identity-flag rows measure the
/// ConditionalWeakTable variant. Together they bracket the design space for wrapper reuse.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, in its own scenario's interop
/// configuration, warmed with that row's script and nothing else. It used to be five engines for ten
/// rows: each configuration's object-churn row and its array-traversal row(s) shared one engine, and
/// every one of those engines was warmed with <c>h.Payload.Value</c> — the churn row's script — so the
/// array rows were measured with the <c>Payload</c> wrapper already resident in the very cache
/// (recent-wrapper ring or CWT) the row exists to characterize. The default-configuration engine carried
/// three rows, whose scripts also collide on the globals <c>s</c> and <c>i</c>. The rows still measure
/// warm crossings, and engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside the
/// measurement. <b>Numbers from this class are not comparable to any published before the harness
/// changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class InteropWrapperChurnBenchmark
{
    private const int OperationsPerInvoke = 1_000;

    public sealed class Holder
    {
        public Holder(Payload payload)
        {
            Payload = payload;
        }

        public Payload Payload { get; }

        public int[] Numbers { get; } = Enumerable.Range(0, 100).ToArray();
    }

    public sealed class Payload
    {
        public int Value { get; set; }
    }

    private Engine _engineChurnDefault = null!;
    private Engine _engineChurnNoCache = null!;
    private Engine _engineChurnIdentity = null!;
    private Engine _engineChurnRecentCache = null!;
    private Engine _engineArrayDefault = null!;
    private Engine _engineArrayNoCache = null!;
    private Engine _engineArrayHoistedDefault = null!;
    private Engine _engineArrayCopy = null!;
    private Engine _engineArrayIdentity = null!;
    private Engine _engineArrayLiveView = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var holder = new Holder(new Payload { Value = 42 });

        // the out-of-the-box engine: ArrayConversion defaults to Copy and
        // CacheRecentObjectWrappers defaults to true (repeated crossings reuse the snapshot)
        Engine CreateDefault()
        {
            var engine = new Engine();
            engine.SetValue("h", holder);
            return engine;
        }

        // opt out of the recent-wrapper cache: every crossing builds a fresh wrapper
        Engine CreateNoCache()
        {
            var engine = new Engine(cfg =>
            {
                cfg.Interop.ArrayConversion = ArrayConversionMode.LiveView;
                cfg.Interop.CacheRecentObjectWrappers = false;
            });
            engine.SetValue("h", holder);
            return engine;
        }

        // the identity flags are most interesting on top of Copy, where they let the otherwise
        // per-read deep-copied JsArray snapshot be reused across crossings (pin Copy so the array
        // traversal rows below measure exactly that; the object-churn rows are array-mode agnostic).
        // The recent cache is disabled in the CWT lane so each row measures a single mechanism.
        Engine CreateIdentity()
        {
            var engine = new Engine(cfg =>
            {
                cfg.Interop.ArrayConversion = ArrayConversionMode.Copy;
                cfg.Interop.TrackObjectWrapperIdentity = true;
                cfg.Interop.CacheRecentObjectWrappers = false;
            });
            engine.SetValue("h", holder);
            return engine;
        }

        Engine CreateRecentCache()
        {
            var engine = new Engine(cfg =>
            {
                cfg.Interop.ArrayConversion = ArrayConversionMode.Copy;
                cfg.Interop.CacheRecentObjectWrappers = true;
            });
            engine.SetValue("h", holder);
            return engine;
        }

        Engine CreateLiveView()
        {
            var engine = new Engine(cfg => cfg.Interop.ArrayConversion = ArrayConversionMode.LiveView);
            engine.SetValue("h", holder);
            return engine;
        }

        // Every array crossing deep-copies into a fresh JsArray snapshot (the cache opt-out keeps
        // this row measuring the copy itself).
        Engine CreateCopy()
        {
            var engine = new Engine(cfg =>
            {
                cfg.Interop.ArrayConversion = ArrayConversionMode.Copy;
                cfg.Interop.CacheRecentObjectWrappers = false;
            });
            engine.SetValue("h", holder);
            return engine;
        }

        // Each row's engine is warmed with that row's own script, so no row starts with another
        // row's wrapper already in the cache it is measuring.
        _engineChurnDefault = CreateDefault();
        _engineChurnDefault.Execute("h.Payload.Value");

        _engineChurnNoCache = CreateNoCache();
        _engineChurnNoCache.Execute("h.Payload.Value");

        _engineChurnIdentity = CreateIdentity();
        _engineChurnIdentity.Execute("h.Payload.Value");

        _engineChurnRecentCache = CreateRecentCache();
        _engineChurnRecentCache.Execute("h.Payload.Value");

        _engineArrayDefault = CreateDefault();
        _engineArrayDefault.Execute(_arrayTraversal);

        _engineArrayNoCache = CreateNoCache();
        _engineArrayNoCache.Execute(_arrayTraversal);

        _engineArrayHoistedDefault = CreateDefault();
        _engineArrayHoistedDefault.Execute(_arrayHoistedTraversal);

        _engineArrayCopy = CreateCopy();
        _engineArrayCopy.Execute(_arrayTraversal);

        _engineArrayIdentity = CreateIdentity();
        _engineArrayIdentity.Execute(_arrayTraversal);

        _engineArrayLiveView = CreateLiveView();
        _engineArrayLiveView.Execute(_arrayTraversal);
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void SameObjectReturnedInLoop_DefaultFlag()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineChurnDefault.Execute("h.Payload.Value");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void SameObjectReturnedInLoop_NoCache()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineChurnNoCache.Execute("h.Payload.Value");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void SameObjectReturnedInLoop_IdentityFlag()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineChurnIdentity.Execute("h.Payload.Value");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void SameObjectReturnedInLoop_RecentCache()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineChurnRecentCache.Execute("h.Payload.Value");
    }

    // A CLR array member read repeatedly inside the loop. The default Copy mode reuses its first
    // snapshot through the recent-wrapper cache; the explicit LiveView row avoids a deep copy, and
    // the uncached Copy row below measures a fresh snapshot on each crossing.
    private static readonly Prepared<Script> _arrayTraversal = Engine.PrepareScript(
        "var s = 0; for (var i = 0; i < 100; i++) { s += h.Numbers[i]; }");

    [Benchmark]
    public void ArrayMemberTraversal_DefaultCopy()
    {
        _engineArrayDefault.Execute(_arrayTraversal);
    }

    [Benchmark]
    public void ArrayMemberTraversal_LiveViewNoCache()
    {
        _engineArrayNoCache.Execute(_arrayTraversal);
    }

    // The README-recommended pattern: hoist the collection into a local so the wrapper is created
    // once and the loop measures pure element-read cost (index dispatch + item conversion).
    private static readonly Prepared<Script> _arrayHoistedTraversal = Engine.PrepareScript(
        "var arr = h.Numbers; var s = 0; for (var pass = 0; pass < 100; pass++) { for (var i = 0; i < 100; i++) { s += arr[i]; } }");

    [Benchmark]
    public void ArrayHoistedTraversal_DefaultCopy()
    {
        _engineArrayHoistedDefault.Execute(_arrayHoistedTraversal);
    }

    [Benchmark]
    public void ArrayMemberTraversal_CopyNoCache()
    {
        _engineArrayCopy.Execute(_arrayTraversal);
    }

    [Benchmark]
    public void ArrayMemberTraversal_CopyIdentityFlag()
    {
        _engineArrayIdentity.Execute(_arrayTraversal);
    }

    [Benchmark]
    public void ArrayMemberTraversal_LiveView()
    {
        _engineArrayLiveView.Execute(_arrayTraversal);
    }
}
