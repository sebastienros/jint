using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Measures ObjectWrapper churn: a CLR member returning the SAME object reference repeatedly.
/// With the default TrackObjectWrapperIdentity=false every crossing allocates a fresh
/// ObjectWrapper; with the opt-in identity flag the ConditionalWeakTable returns the cached one.
/// The two benchmarks bracket the design space for cheaper wrapper reuse.
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
    }

    public sealed class Payload
    {
        public int Value { get; set; }
    }

    private Engine _engineDefault = null!;
    private Engine _engineIdentity = null!;
    private Engine _engineRecentCache = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var holder = new Holder(new Payload { Value = 42 });

        _engineDefault = new Engine();
        _engineDefault.SetValue("h", holder);
        _engineDefault.Execute("h.Payload.Value");

        _engineIdentity = new Engine(cfg => cfg.Interop.TrackObjectWrapperIdentity = true);
        _engineIdentity.SetValue("h", holder);
        _engineIdentity.Execute("h.Payload.Value");

        _engineRecentCache = new Engine(cfg => cfg.Interop.CacheRecentObjectWrappers = true);
        _engineRecentCache.SetValue("h", holder);
        _engineRecentCache.Execute("h.Payload.Value");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void SameObjectReturnedInLoop_DefaultFlag()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineDefault.Execute("h.Payload.Value");
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
}
