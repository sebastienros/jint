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

    [Fact]
    public void AlternatingClosureInstancesReadingOuterBindingsStayCorrect()
    {
        var engine = new Engine();
        // hops-1 closure reads through the chain memo: two closure instances of the same
        // definition alternate at ONE callsite, so the shared node's pinned chain flips
        // between the instances' environments on every call and must re-validate, not
        // serve the other instance's binding
        var result = engine.Evaluate("""
            (function () {
                function make(table) {
                    return function () {
                        var acc = '';
                        for (var i = 0; i < 8; i++) { acc += table[i % table.length]; }
                        return acc;
                    };
                }
                var a = make(['A']), b = make(['B']);
                var got = '';
                for (var r = 0; r < 3; r++) { got += a() + '|' + b() + ';'; }
                return got;
            })()
            """).AsString();

        Assert.Equal("AAAAAAAA|BBBBBBBB;AAAAAAAA|BBBBBBBB;AAAAAAAA|BBBBBBBB;", result);
    }

    [Fact]
    public void SloppyDirectEvalShadowingVarInvalidatesChainMemoMidLoop()
    {
        var engine = new Engine();
        // sloppy direct eval injects `captured` into the enclosing FUNCTION env mid-loop:
        // the pre-eval iterations pin a chain memo to the outer binding; post-injection the
        // epoch bump must force the walk to see the new shadowing binding immediately
        var result = engine.Evaluate("""
            (function () {
                var captured = 'outer';
                function reader() {
                    var seen = '';
                    for (var i = 0; i < 6; i++) {
                        seen += captured;
                        if (i === 2) { eval("var captured = 'inner';"); }
                        seen += ',';
                    }
                    return seen;
                }
                return reader() + '|' + captured;
            })()
            """).AsString();

        // i=0..2 read the outer binding (the eval at i===2 runs AFTER the read); the
        // injected var initializes to 'inner' and shadows from the i=3 read onward, and
        // the outer binding must remain untouched
        Assert.Equal("outer,outer,outer,inner,inner,inner,|outer", result);
    }

    [Fact]
    public void SloppyDirectEvalHoistedVarShadowsFromInjectionOn()
    {
        var engine = new Engine();
        // hoisting-only variant: `var captured;` without initializer still creates the
        // shadowing binding (initialized to undefined by EvalDeclarationInstantiation),
        // flipping subsequent memo-guarded reads from 'outer' to undefined
        var result = engine.Evaluate("""
            (function () {
                var captured = 'outer';
                function reader() {
                    var seen = '';
                    for (var i = 0; i < 4; i++) {
                        seen += captured + ',';
                        if (i === 1) { eval("var captured;"); }
                    }
                    return seen;
                }
                return reader();
            })()
            """).AsString();

        Assert.Equal("outer,outer,undefined,undefined,", result);
    }

    [Fact]
    public void WithBlockAroundClosureReadIsNeverSkipped()
    {
        var engine = new Engine();
        // the same identifier node runs both under a with-ObjectEnvironment and without it:
        // a chain pinned outside the with must never validate inside it (the with-object can
        // shadow dynamically), and property addition mid-loop must be honored immediately
        var result = engine.Evaluate("""
            (function () {
                var captured = 'free';
                var obj = {};
                function reader(useWith) {
                    var seen = '';
                    if (useWith) {
                        with (obj) {
                            for (var i = 0; i < 4; i++) {
                                seen += captured + ',';
                                if (i === 1) { obj.captured = 'shadowed'; }
                            }
                        }
                    } else {
                        for (var j = 0; j < 2; j++) { seen += captured + ','; }
                    }
                    return seen;
                }
                // warm the memo without the with-block, then run under it, then without again
                return reader(false) + '|' + reader(true) + '|' + reader(false);
            })()
            """).AsString();

        Assert.Equal("free,free,|free,free,shadowed,shadowed,|free,free,", result);
    }

    [Fact]
    public void DeeplyNestedClosureChainsResolveCorrectly()
    {
        var engine = new Engine();
        // reads at hop distances beyond MaxChainDepth must keep falling through to full
        // resolution and stay correct; distances within the bound exercise the memo
        var result = engine.Evaluate("""
            (function () {
                var deep = 'root';
                function l1() {
                    var a1 = 1;
                    function l2() {
                        var a2 = 2;
                        function l3() {
                            var a3 = 3;
                            function l4() {
                                var a4 = 4;
                                function l5() {
                                    var acc = '';
                                    for (var i = 0; i < 3; i++) {
                                        acc += deep + (a1 + a2 + a3 + a4);
                                    }
                                    return acc;
                                }
                                return l5();
                            }
                            return l4();
                        }
                        return l3();
                    }
                    return l2();
                }
                return l1() + '|' + l1();
            })()
            """).AsString();

        Assert.Equal("root10root10root10|root10root10root10", result);
    }

    [Fact]
    public void PooledLoopEnvironmentsReattachedAcrossEntriesKeepMemoValid()
    {
        var engine = new Engine();
        // let-bound loop bodies get per-node pooled block environments re-attached across
        // loop entries: the pinned chain's start link is the SAME instance on a fresh entry,
        // so the memo re-validates against current outer pointers and must stay correct
        // across function instance switches too
        var result = engine.Evaluate("""
            (function () {
                function make(base) {
                    return function () {
                        var acc = 0;
                        for (let i = 0; i < 50; i++) {
                            const step = 1;
                            acc += base + step;
                        }
                        return acc;
                    };
                }
                var a = make(10), b = make(1000);
                return a() + b() + a();
            })()
            """).AsNumber();

        Assert.Equal(50 * 11 + 50 * 1001 + 50 * 11, result);
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
