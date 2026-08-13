using Jint;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// What an embedder running untrusted script gets from <c>options.Constraints.StackOverflowGuard</c> when
/// that script recurses without bound: the promise a browser makes, a catchable <c>RangeError</c> and an
/// engine that is still usable afterwards, instead of a process that disappears.
/// </summary>
/// <remarks>
/// <para>
/// The guard is opt-in, so every engine here but one asks for it. The exception is
/// <see cref="ADefaultEngineDoesNotProbeAtAll"/>, which pins the other half of the decision: a default
/// engine pays nothing and therefore protects nothing.
/// </para>
/// <para>
/// Every body runs on a dedicated thread with an explicit 1 MB stack, the size a thread-pool worker or an
/// IIS request thread actually has. The suite's other deep-recursion tests use 16 MB, which is generous
/// enough to hide the failure this file exists for.
/// </para>
/// </remarks>
public class HostStackOverflowGuardTests
{
    private const int SmallStack = 1024 * 1024;

    private static Engine Guarded() => new(options => options.Constraints.StackOverflowGuard = true);

    public static TheoryData<string, string> UnboundedRecursions => new()
    {
        { "call", "function f() { return f(); } f();" },
        { "new", "function F() { new F(); } new F();" },
        { "accessor", "var o = { get x() { return o.x; } }; o.x;" },
        { "coercion", "var o = { valueOf: function () { return o + 1; } }; o + 1;" },
        { "proxy trap", "var p = new Proxy({}, { get: function (t, k) { return p[k]; } }); p.a;" },
    };

    [Theory]
    [MemberData(nameof(UnboundedRecursions))]
    public void AnUnboundedRecursionIsACatchableError(string route, string script)
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

    [Fact]
    public void TheEngineKeepsWorkingAfterOne()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = Guarded();
                engine.SetValue("factor", 3);
                engine.Execute("function runaway() { return runaway(); }");

                for (var attempt = 0; attempt < 3; attempt++)
                {
                    Assert.Throws<JavaScriptException>(() => engine.Evaluate("runaway()"));

                    engine.Evaluate("[1, 2, 3].map(function (x) { return x * factor; }).join(',')")
                        .AsString().Should().Be("3,6,9");
                    engine.Evaluate("JSON.stringify({ ok: true })").AsString().Should().Be("{\"ok\":true}");
                }
            },
            maxStackSize: SmallStack);
    }

    /// <summary>
    /// A host callback that re-enters the engine is the shape an embedder is most likely to build a cycle
    /// out of by accident, and the one furthest from a call expression.
    /// </summary>
    [Fact]
    public void HostReEntryIsGuardedToo()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = Guarded();
                engine.SetValue("callBackIn", new Action(() => engine.Invoke("f")));
                engine.Execute("function f() { callBackIn(); }");

                Assert.Throws<JavaScriptException>(() => engine.Invoke("f"));

                engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
            },
            maxStackSize: SmallStack);
    }

    [Fact]
    public void ScriptCodeCanCatchItAndCarryOn()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = Guarded();
                var result = engine.Evaluate(
                    """
                    function runaway() { return runaway(); }
                    var caught = null;
                    try { runaway(); } catch (e) { caught = e; }
                    caught instanceof RangeError && caught.message === 'Maximum call stack size exceeded'
                        ? 'recovered: ' + [1, 2, 3].reduce(function (a, b) { return a + b; }, 0)
                        : 'wrong error: ' + caught;
                    """);

                result.AsString().Should().Be("recovered: 6");
            },
            maxStackSize: SmallStack);
    }

    /// <summary>
    /// The stack the engine is running on decides the depth, not a number the host had to guess, so a
    /// thread with more stack gets proportionally more frames. That is what makes the guard safe to enable
    /// at all: it never takes depth away from a host that provisioned for it.
    /// </summary>
    [Fact]
    public void TheDepthFollowsTheStackTheEngineRunsOn()
    {
        var small = MeasureGuardedDepth(SmallStack);
        var large = MeasureGuardedDepth(8 * 1024 * 1024);

        large.Should().BeGreaterThan(small * 4, "eight times the stack must buy substantially more depth");
    }

    /// <summary>
    /// The default is off, and this is the only way to say so from a test: a default engine is run past
    /// the depth at which a guarded engine on the same 1 MB stack would have thrown, and must complete
    /// the recursion normally instead. The run itself happens on a 16 MB stack, sixteen times what that
    /// depth was measured against, because the assertion is about the absence of the guard's
    /// <c>RangeError</c> and not about how much stack a default engine really has. Pinning the other
    /// direction is impossible in-process: what a default engine does past its own limit is end the
    /// process, and no test can assert on that.
    /// </summary>
    [Fact]
    public void ADefaultEngineDoesNotProbeAtAll()
    {
        var guardedDepth = (int) MeasureGuardedDepth(SmallStack);

        DedicatedThread.Run(
            () =>
            {
                var engine = new Engine();
                engine.Execute("function f(n) { return n === 0 ? 0 : 1 + f(n - 1); }");

                engine.Evaluate($"f({guardedDepth})").AsNumber().Should().Be(guardedDepth);
            },
            maxStackSize: DedicatedThread.LargeStackSize);
    }

    private static double MeasureGuardedDepth(int stackSize)
    {
        double depth = 0;
        DedicatedThread.Run(
            () =>
            {
                var engine = Guarded();
                engine.Execute("var depth = 0; function f() { depth++; return f(); }");
                Assert.Throws<JavaScriptException>(() => engine.Evaluate("f()"));
                depth = engine.Evaluate("depth").AsNumber();
            },
            maxStackSize: stackSize);
        return depth;
    }
}
