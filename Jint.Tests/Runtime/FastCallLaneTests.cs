using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// Invariants of the built-in fast-call lane (<c>Function.GetFastCallShape</c> / <c>CallFast</c>).
///
/// The lane caches a callee per call site and may skip the pooled argument array and, for calls it
/// can prove reach no user code, the call-stack frame. Every test here asserts behaviour that must
/// hold whether or not the lane engages, so they are a live regression baseline as built-ins are
/// progressively annotated: none of them should ever need to change.
/// </summary>
public class FastCallLaneTests
{
    /// <summary>
    /// Re-assigning a built-in swaps the value inside the existing PropertyDescriptor without
    /// bumping any version counter, so nothing weaker than a callee identity check notices. The
    /// site must also recover when the original is restored — a one-way deopt would be a silent
    /// permanent slowdown.
    /// </summary>
    [Fact]
    public void ReassigningABuiltinDeoptimizesTheCallSiteAndRestoringItReoptimizes()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function f(x) { return Math.abs(x); }
            const original = Math.abs;
            const before = f(-5);
            Math.abs = function () { return 999; };
            const patched = f(-5);
            Math.abs = original;
            const restored = f(-5);
            before + "," + patched + "," + restored;
            """);

        result.AsString().Should().Be("5,999,5");
    }

    [Fact]
    public void DeletingABuiltinIsObservedByAWarmedCallSite()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function f(x) { return Math.abs(x); }
            f(-1); f(-1); f(-1);
            delete Math.abs;
            let threw = false;
            try { f(-1); } catch (e) { threw = e instanceof TypeError; }
            threw;
            """);

        result.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ReplacingTheWholeNamespaceIsObservedByAWarmedCallSite()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function f(x) { return Math.abs(x); }
            f(-1); f(-1);
            globalThis.Math = { abs: function () { return 42; } };
            f(-7);
            """);

        result.AsNumber().Should().Be(42);
    }

    /// <summary>
    /// A lexical binding shadows the global, so a warmed site must resolve to the shadowing value.
    /// </summary>
    [Fact]
    public void AShadowingLocalBindingWins()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function f(x) { return Math.abs(x); }
            const warm = f(-3);
            function shadowed(x) {
                const Math = { abs: function () { return -1; } };
                return Math.abs(x);
            }
            warm + "," + shadowed(-3);
            """);

        result.AsString().Should().Be("3,-1");
    }

    /// <summary>
    /// The single most important invariant: a built-in's own frame is observable in error.stack.
    /// An object argument coerces through user <c>valueOf</c>, so the frame must NOT be elided —
    /// the fast lane's argument-shape guard exists precisely to keep this case on the framed path.
    /// </summary>
    [Fact]
    public void TheBuiltinsOwnFrameStaysInErrorStackWhenAnArgumentCoercesThroughUserCode()
    {
        var engine = new Engine();
        var stack = engine.Evaluate("""
            function boom() {
                return Math.floor({ valueOf: function () { throw new Error("x"); } });
            }
            let captured = "";
            try { boom(); } catch (e) { captured = e.stack; }
            captured;
            """).AsString();

        stack.Should().Contain("at floor", "the built-in frame must survive; user valueOf can read it");
    }

    /// <summary>
    /// Same guard from the receiver side: a non-primitive receiver coerces via ToJsString/ToObject,
    /// which can run user code, so such calls must keep their frame and their exact semantics.
    /// </summary>
    [Fact]
    public void ABoxedReceiverProducesTheSameResultAsAPrimitiveOne()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function take(s) { return s.charCodeAt(1) + "|" + s.charAt(1) + "|" + s.substring(0, 2); }
            take("abc") === take(new String("abc"));
            """);

        result.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void NonNumericArgumentsStillCoerceExactlyOnce()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            let calls = 0;
            const pos = { valueOf: function () { calls++; return 1; } };
            const c = "abc".charCodeAt(pos);
            c + "," + calls;
            """);

        result.AsString().Should().Be("98,1");
    }

    /// <summary>
    /// A Prepared&lt;Script&gt; shares its handler tree across engines, so a call-site cache populated
    /// by one engine must never be honoured for another engine's built-in instances.
    /// </summary>
    [Fact]
    public void APreparedScriptReusedAcrossEnginesDoesNotLeakItsCachedCallee()
    {
        var prepared = Engine.PrepareScript("""
            function f(x) { return Math.abs(x); }
            f(-1); f(-2);
            f(-3);
            """);

        var first = new Engine();
        first.Evaluate(prepared).AsNumber().Should().Be(3);

        // Second engine patches its own Math before running the very same prepared nodes.
        var second = new Engine();
        second.Execute("Math.abs = function () { return 123; };");
        second.Evaluate(prepared).AsNumber().Should().Be(123);

        // First engine must be unaffected by the second engine's patch.
        first.Evaluate(prepared).AsNumber().Should().Be(3);
    }

    [Fact]
    public void ArgumentsAreEvaluatedLeftToRightExactlyOnce()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const order = [];
            function a() { order.push("a"); return 0; }
            function b() { order.push("b"); return 2; }
            const out = "hello".substring(a(), b());
            out + "|" + order.join(",");
            """);

        result.AsString().Should().Be("he|a,b");
    }

    /// <summary>
    /// Spread cannot be served by a fixed-arity lane; it must fall through and stay correct.
    /// </summary>
    [Fact]
    public void SpreadArgumentsStillWork()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const args = [1, 3];
            "abcdef".substring(...args) + "," + Math.max(...[1, 9, 4]);
            """);

        result.AsString().Should().Be("bc,9");
    }

    [Fact]
    public void OptionalCallAndOptionalChainingStillWork()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const s = "abc";
            const missing = undefined;
            (s?.charAt(1) ?? "?") + "," + (missing?.charAt(1) ?? "?");
            """);

        result.AsString().Should().Be("b,?");
    }

    /// <summary>
    /// Generator frames suspend mid-argument-list, which requires ExpressionCache's resume buffer;
    /// the fast lane must decline there rather than evaluating arguments straight into locals.
    /// </summary>
    [Fact]
    public void ArgumentsContainingYieldStillResumeCorrectly()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function* g() {
                const r = "abcdef".substring(yield 1, yield 2);
                return r;
            }
            const it = g();
            it.next();
            it.next(1);
            it.next(4);
            """);

        result.Get("value").AsString().Should().Be("bcd");
    }

    [Fact]
    public void CallAndApplyAndBindAreUnaffected()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const cc = String.prototype.charCodeAt;
            const bound = cc.bind("abc");
            cc.call("abc", 0) + "," + cc.apply("abc", [1]) + "," + bound(2);
            """);

        result.AsString().Should().Be("97,98,99");
    }

    [Fact]
    public void ZeroArgumentDateGettersAgreeWithTheirFramedForm()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const d = new Date(Date.UTC(2021, 4, 17, 8, 30, 15));
            function viaSite(x) { return x.getUTCFullYear(); }
            viaSite(d); viaSite(d);
            const direct = Date.prototype.getUTCFullYear.call(d);
            viaSite(d) === direct && direct === 2021;
            """);

        result.AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// A receiver of the wrong brand must still produce the spec TypeError, with the built-in's own
    /// frame present, rather than being silently served by a receiver-guarded fast path.
    /// </summary>
    [Fact]
    public void AWrongBrandReceiverStillThrowsTypeError()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            let message = "";
            try { Date.prototype.getUTCFullYear.call({}); }
            catch (e) { message = e.constructor.name; }
            message;
            """);

        result.AsString().Should().Be("TypeError");
    }
}
