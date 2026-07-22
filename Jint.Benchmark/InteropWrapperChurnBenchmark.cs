using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Measures ObjectWrapper churn: a CLR member returning the SAME object reference repeatedly.
/// Since 4.14 the default engine keeps a small bounded cache of recently wrapped objects
/// (CacheRecentObjectWrappers), so repeated crossings reuse the wrapper; the NoCache rows opt out
/// to measure the fresh-wrapper-per-crossing path, and the identity-flag rows measure the
/// ConditionalWeakTable variant. Together they bracket the design space for wrapper reuse.
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

    private Engine _engineDefault = null!;
    private Engine _engineNoCache = null!;
    private Engine _engineIdentity = null!;
    private Engine _engineRecentCache = null!;
    private Engine _engineCopy = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var holder = new Holder(new Payload { Value = 42 });

        // the out-of-the-box engine: since 4.14 ArrayConversion defaults to LiveView and
        // CacheRecentObjectWrappers defaults to true (repeated crossings reuse the wrapper)
        _engineDefault = new Engine();
        _engineDefault.SetValue("h", holder);
        _engineDefault.Execute("h.Payload.Value");

        // opt out of the recent-wrapper cache: every crossing builds a fresh wrapper
        _engineNoCache = new Engine(cfg => cfg.Interop.CacheRecentObjectWrappers = false);
        _engineNoCache.SetValue("h", holder);
        _engineNoCache.Execute("h.Payload.Value");

        // the identity flags are most interesting on top of Copy, where they let the otherwise
        // per-read deep-copied JsArray snapshot be reused across crossings (pin Copy so the array
        // traversal rows below measure exactly that; the object-churn rows are array-mode agnostic).
        // The recent cache is disabled in the CWT lane so each row measures a single mechanism.
        _engineIdentity = new Engine(cfg =>
        {
            cfg.Interop.ArrayConversion = ArrayConversionMode.Copy;
            cfg.Interop.TrackObjectWrapperIdentity = true;
            cfg.Interop.CacheRecentObjectWrappers = false;
        });
        _engineIdentity.SetValue("h", holder);
        _engineIdentity.Execute("h.Payload.Value");

        _engineRecentCache = new Engine(cfg =>
        {
            cfg.Interop.ArrayConversion = ArrayConversionMode.Copy;
            cfg.Interop.CacheRecentObjectWrappers = true;
        });
        _engineRecentCache.SetValue("h", holder);
        _engineRecentCache.Execute("h.Payload.Value");

        // the pre-4.14 default: every array crossing deep-copies into a fresh JsArray snapshot
        // (the cache opt-out keeps this row measuring the copy itself)
        _engineCopy = new Engine(cfg =>
        {
            cfg.Interop.ArrayConversion = ArrayConversionMode.Copy;
            cfg.Interop.CacheRecentObjectWrappers = false;
        });
        _engineCopy.SetValue("h", holder);
        _engineCopy.Execute("h.Payload.Value");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void SameObjectReturnedInLoop_DefaultFlag()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineDefault.Execute("h.Payload.Value");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void SameObjectReturnedInLoop_NoCache()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineNoCache.Execute("h.Payload.Value");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void SameObjectReturnedInLoop_IdentityFlag()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineIdentity.Execute("h.Payload.Value");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void SameObjectReturnedInLoop_RecentCache()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineRecentCache.Execute("h.Payload.Value");
    }

    // A CLR array member read repeatedly inside the loop. Under the default (LiveView) each h.Numbers
    // read wraps the array in a live fixed-size view without a per-read deep copy; the Copy-mode rows
    // below deep-copy into a fresh JsArray unless an identity flag reuses the snapshot.
    private static readonly Prepared<Script> _arrayTraversal = Engine.PrepareScript(
        "var s = 0; for (var i = 0; i < 100; i++) { s += h.Numbers[i]; }");

    [Benchmark]
    public void ArrayMemberTraversal_DefaultLiveView()
    {
        _engineDefault.Execute(_arrayTraversal);
    }

    [Benchmark]
    public void ArrayMemberTraversal_LiveViewNoCache()
    {
        _engineNoCache.Execute(_arrayTraversal);
    }

    // The README-recommended pattern: hoist the collection into a local so the wrapper is created
    // once and the loop measures pure element-read cost (index dispatch + item conversion).
    private static readonly Prepared<Script> _arrayHoistedTraversal = Engine.PrepareScript(
        "var arr = h.Numbers; var s = 0; for (var pass = 0; pass < 100; pass++) { for (var i = 0; i < 100; i++) { s += arr[i]; } }");

    [Benchmark]
    public void ArrayHoistedTraversal_DefaultLiveView()
    {
        _engineDefault.Execute(_arrayHoistedTraversal);
    }

    [Benchmark]
    public void ArrayMemberTraversal_CopyMode()
    {
        _engineCopy.Execute(_arrayTraversal);
    }

    [Benchmark]
    public void ArrayMemberTraversal_CopyIdentityFlag()
    {
        _engineIdentity.Execute(_arrayTraversal);
    }

    [Benchmark]
    public void ArrayMemberTraversal_CopyRecentCache()
    {
        _engineRecentCache.Execute(_arrayTraversal);
    }
}
