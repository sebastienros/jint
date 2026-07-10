namespace Jint.Tests.Runtime;

/// <summary>
/// Pins SlotLocationCache behavior for handler trees shared across function instances
/// (per-engine definition reuse): a node whose cached environment is unreachable from the
/// current chain must re-resolve against it — declining forever would silently disable every
/// slot-backed fast lane from the second re-evaluation of a prepared script onward.
/// </summary>
public class SlotLocationCacheTests
{
    [Fact]
    public void RepeatedPreparedEvaluationsKeepProducingCorrectResults()
    {
        var engine = new Engine();
        var prepared = Engine.PrepareScript("""
            function f() {
                var n = 0;
                for (var i = 0; i < 1000; i++) { n += i; }
                return n;
            }
            f();
            """);

        // the cached-definition handler tree serves a fresh function instance (fresh env) per
        // evaluation; each must resolve slots against its own chain
        for (var e = 0; e < 5; e++)
        {
            Assert.Equal(499500, engine.Evaluate(prepared).AsNumber());
        }
    }

    [Fact]
    public void AlternatingFunctionInstancesSharingOneTreeStayCorrect()
    {
        var engine = new Engine();
        // two instances of the same nested declaration alternate calls: the shared tree's
        // slot caches flip between the instances' environments and must stay correct
        var result = engine.Evaluate("""
            (function () {
                function make(base) {
                    function inner() {
                        var acc = 0;
                        for (var i = 0; i < 100; i++) { acc += base; }
                        return acc;
                    }
                    return inner;
                }
                var a = make(1), b = make(1000);
                var sum = 0;
                for (var r = 0; r < 4; r++) { sum += a() + b(); }
                return sum;
            })()
            """).AsNumber();

        Assert.Equal(4 * (100 + 100_000), result);
    }

#if NET
    [Fact]
    public void SlotLanesSurviveRepeatedPreparedEvaluations()
    {
        var engine = new Engine();
        var prepared = Engine.PrepareScript("""
            function f() {
                var n = 0;
                for (var i = 0; i < 20000; i++) { n += 1; }
                return n;
            }
            f();
            """);

        // evaluation 1 uses the first-build tree, 2 the freshly cached tree; from 3 on the
        // cached tree runs under a different environment than the one that populated its
        // slot caches — the lanes must re-resolve and stay unboxed instead of falling to
        // the materializing generic path (which allocates a JsNumber per iteration here)
        for (var e = 0; e < 3; e++)
        {
            engine.Evaluate(prepared);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        engine.Evaluate(prepared);
        var perEvaluation = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(
            perEvaluation < 150_000,
            $"expected slot lanes to stay live across re-evaluations; allocated {perEvaluation} bytes in one evaluation");
    }
#endif
}
