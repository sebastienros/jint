#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// The opt-in backstop against unbounded script recursion
/// (<see cref="Options.ConstraintOptions.StackOverflowGuard"/>). Without it every script below ends the
/// host process with a native stack overflow, which no <c>catch</c> can see and no test can assert on —
/// so the value of these tests is that they run at all.
/// </summary>
/// <remarks>
/// <para>
/// Every engine here is built with the guard on, because it is off by default: the benchmark gate priced
/// the probe at 1.7–2.3% on the recursion rows, above the 1% the decision rule allowed for shipping it on
/// by default. <c>Jint.Tests.PublicInterface.HostStackOverflowGuardTests</c> owns the other half of that
/// verdict, the pin that a default engine really does not probe.
/// </para>
/// <para>
/// Every body runs on a dedicated thread with an explicit 1 MB stack: the platform default the guard has
/// to work on, small enough to reach in well under a second, and deliberately not
/// <see cref="DedicatedThread.LargeStackSize"/>, whose 16 MB is what has been hiding this whole class of
/// failure from the suite. Running in-process is safe precisely because the guard works; if it regresses,
/// the symptom is a dead test host, which is the honest report.
/// </para>
/// </remarks>
public class StackOverflowGuardTests
{
    private const int SmallStack = 1024 * 1024;

    private static Engine Guarded(Action<Options>? configure = null) => new(options =>
    {
        options.Constraints.StackOverflowGuard = true;
        configure?.Invoke(options);
    });

    /// <summary>
    /// One entry per route into a function body that does not go through a call expression. Every one of
    /// them kills the process unguarded, and four of them do so even with
    /// <see cref="Options.ConstraintOptions.MaxExecutionStackCount"/> configured, because the only probe
    /// that option arms sits in <c>JintCallExpression</c>. All of them are sloppy-mode deliberately:
    /// proper tail calls apply to strict functions only, so none of these is trampolined and every one
    /// really does grow the native stack.
    /// </summary>
    public static TheoryData<string, string> UnboundedRecursions => new()
    {
        { "call", "function f() { return f(); } f();" },
        { "new", "function F() { new F(); } new F();" },
        { "accessor", "var o = { get x() { return o.x; } }; o.x;" },
        { "coercion", "var o = { valueOf: function () { return o + 1; } }; o + 1;" },
        { "proxy trap", "var p = new Proxy({}, { get: function (t, k) { return p[k]; } }); p.a;" },
        { "method", "var o = { m: function () { return this.m(); } }; o.m();" },
        { "apply", "function f() { return f.apply(null, []); } f();" },
        { "class field", "class C { x = new C(); } new C();" },
    };

    [Theory]
    [MemberData(nameof(UnboundedRecursions))]
    public void UnboundedRecursionThrowsARangeErrorInsteadOfKillingTheProcess(string route, string script)
    {
        _ = route;

        DedicatedThread.Run(
            () =>
            {
                var engine = Guarded();

                var exception = Assert.Throws<JavaScriptException>(() => engine.Execute(script));
                exception.Message.Should().Be("Maximum call stack size exceeded");
                exception.Error.Get("name").AsString().Should().Be("RangeError");
            },
            maxStackSize: SmallStack);
    }

    [Theory]
    [MemberData(nameof(UnboundedRecursions))]
    public void ScriptCanCatchTheRangeErrorItself(string route, string script)
    {
        _ = route;

        DedicatedThread.Run(
            () =>
            {
                var engine = Guarded();
                var caught = engine.Evaluate($"(function () {{ try {{ {script} }} catch (e) {{ return e instanceof RangeError; }} }})()");
                caught.AsBoolean().Should().BeTrue();
            },
            maxStackSize: SmallStack);
    }

    /// <summary>
    /// The recovery test the guard lives or dies by: catching near-exhaustion has to leave the engine in
    /// the state it was in before, not merely alive. Firing at the very same depth on three successive
    /// runs is the strongest single signal available here — a frame, an execution context or a native
    /// stack slot left behind by the unwind would move the third run's number.
    /// </summary>
    [Fact]
    public void TheEngineIsUnchangedAfterTheGuardFires()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = Guarded();
                engine.Execute("var depth = 0; function f() { depth++; return f(); }");
                engine.Execute("function fib(n) { return n < 2 ? n : fib(n - 1) + fib(n - 2); }");

                var depths = new List<double>();
                for (var round = 0; round < 3; round++)
                {
                    engine.Evaluate("depth = 0");
                    Assert.Throws<JavaScriptException>(() => engine.Evaluate("f()"));

                    depths.Add(engine.Evaluate("depth").AsNumber());

                    // the interpreter still works, and the call stack it works on is the one it started with
                    engine.Evaluate("fib(20)").AsNumber().Should().Be(6765);
                    engine.CallStack.Count.Should().Be(0);
                    engine.IsEvaluationInProgress.Should().BeFalse();
                }

                depths.Should().AllBeEquivalentTo(depths[0], "an unwind that left anything behind would shorten the next run");
                depths[0].Should().BeGreaterThan(100, "a guard that fires this early would be a limit, not a backstop");
            },
            maxStackSize: SmallStack);
    }

    /// <summary>
    /// A throw out of a callee skips the pool returns the normal path performs — that is true of every
    /// JavaScript <c>throw</c> and is not new here — so what matters is that the pools stay <em>usable</em>:
    /// a balanced workload run afterwards must settle back at the pool's capacity instead of allocating a
    /// fresh instance per rent forever.
    /// </summary>
    [Fact]
    public void ThePoolsStillRecycleAfterTheGuardFires()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = Guarded();
                engine.Execute("function f(a, b, c) { return f(a, b, c); }");
                engine.Execute("function add(a, b, c) { return a + b + c; }");

                Assert.Throws<JavaScriptException>(() => engine.Evaluate("f(1, 2, 3)"));

                // warm, then measure: a balanced rent/return workload must not create a single new instance
                engine.Evaluate("(function () { var t = 0; for (var i = 0; i < 1000; i++) { t += add(i, 1, 2); } return t; })()");
                var createdBefore = engine._referencePool.CreatedCount;
                var total = engine.Evaluate("(function () { var t = 0; for (var i = 0; i < 10000; i++) { t += add(i, 1, 2); } return t; })()");

                total.AsNumber().Should().Be(50025000);
                engine._referencePool.CreatedCount.Should().Be(createdBefore, "the reference pool must recycle again once the unwind is over");
            },
            maxStackSize: SmallStack);
    }

    /// <summary>
    /// The guard is a backstop, not a policy. <see cref="Options.ConstraintOptions.MaxRecursionDepth"/>
    /// counts frames and is checked before the callee is entered, so it is what a host configuring both
    /// gets to see.
    /// </summary>
    [Fact]
    public void ARecursionLimitStillFiresFirst()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = Guarded(options => options.LimitRecursion(20));
                Assert.Throws<RecursionDepthOverflowException>(() => engine.Execute("function f() { return f(); } f();"));
            },
            maxStackSize: SmallStack);
    }

    /// <summary>
    /// A recursion limit set well above what the native stack can hold is a limit in name only — the
    /// process dies before the count is reached. The backstop is what answers instead, which is the point
    /// of it being a backstop rather than a replacement.
    /// </summary>
    [Fact]
    public void AnUnreachableRecursionLimitFallsThroughToTheGuard()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = Guarded(options => options.LimitRecursion(1_000_000));
                var exception = Assert.Throws<JavaScriptException>(() => engine.Execute("function f() { return f(); } f();"));
                exception.Message.Should().Be("Maximum call stack size exceeded");
            },
            maxStackSize: SmallStack);
    }

    /// <summary>
    /// A proper tail call replaces the caller's frame instead of stacking one on top of it, so a strict
    /// tail recursion consumes no native stack however deep it goes and there is nothing for the guard to
    /// fire on. That is why the probe sits on the entry points that add a frame — <c>Call</c>,
    /// <c>CallWithStackFrame</c>, <c>CallFromRegisters</c>, <c>Construct</c> — and not inside
    /// <c>CallOnce</c>/<c>CallCore</c>, which the tail-call trampoline re-enters from a loop. Put it there
    /// and this recursion would pay a probe per hop for a stack that never moves.
    /// </summary>
    [Fact]
    public void AProperTailCallIsNotGuardedBecauseItGrowsNoStack()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = Guarded();

                var result = engine.Evaluate(
                    """
                    "use strict";
                    function sum(n, total) {
                        return n === 0 ? total : sum(n - 1, total + n);
                    }
                    sum(100000, 0);
                    """);

                result.AsNumber().Should().Be(5000050000);
            },
            maxStackSize: SmallStack);
    }

    /// <summary>
    /// The other half of the pair: strict mode alone buys nothing, because <c>1 + f(n - 1)</c> is not a
    /// tail position. This recursion does grow the stack, and the guard is what answers.
    /// </summary>
    [Fact]
    public void AStrictRecursionOutOfTailPositionStillReachesTheGuard()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = Guarded();

                var exception = Assert.Throws<JavaScriptException>(() => engine.Execute(
                    """
                    "use strict";
                    function f(n) { return 1 + f(n - 1); }
                    f(1);
                    """));

                exception.Message.Should().Be("Maximum call stack size exceeded");
            },
            maxStackSize: SmallStack);
    }

    /// <summary>
    /// <see cref="Options.ConstraintOptions.MaxExecutionStackCount"/> selects the older lane, which
    /// continues the call chain on a fresh thread rather than throwing, and it takes precedence over the
    /// guard even when a host asks for both — the two answer the same question differently, and the guard,
    /// sitting a few frames deeper, would always reach the condition first and leave the older lane
    /// nothing to hop with. This pins that the older lane still behaves as it did.
    /// </summary>
    [Fact]
    public void MaxExecutionStackCountTakesPrecedenceOverTheGuard()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = Guarded(options => options.Constraints.MaxExecutionStackCount = 2500);

                // deeper than a 1 MB stack holds: the older lane hops threads instead of throwing
                engine.Execute("var depth = 0; function f(n) { depth++; return n === 0 ? depth : f(n - 1); }");
                engine.Evaluate("f(2000)").AsNumber().Should().Be(2001);

                // and it still throws its RangeError once the configured count is passed
                var exception = Assert.Throws<JavaScriptException>(() => engine.Evaluate("(function g() { return g(); })()"));
                exception.Message.Should().Be("Maximum call stack size exceeded");
            },
            maxStackSize: SmallStack);
    }
}
