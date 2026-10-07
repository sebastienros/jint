using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins that an <c>*Async</c> entry honours the token it was handed: refused before any script runs when it
/// is already cancelled, and observed inside the interpreter — on the amortized cadence, so the tight-loop
/// lane stays armed — for exactly the duration of the call.
/// </summary>
/// <remarks>
/// Nothing here is timed. Every cancellation is issued from the engine thread by a <see cref="ClrFunction"/>,
/// which is deliberately not a host-boundary check point, so what stops the loop that follows is the
/// interpreter's own cadence and nothing else; and every loop is bounded, so a build that does not observe
/// the token fails an assertion rather than hanging.
/// </remarks>
public class AsyncEntryCancellationTests
{
    private const int LoopBound = 1_000_000;

    // The cadence is EvaluationContext.AmortizedConstraintCheckInterval (64) statements; a loop that ran
    // anywhere near LoopBound after the cancel was not observing the token at all.
    private const int CadenceSlack = 1_000;

    [Test]
    public async Task APreCancelledTokenRunsNoScript_EvaluateAsync()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (engine, touched) = CreateEngineCountingTouches();

        var exception = await Caught.ExceptionAsync(() => engine.EvaluateAsync("touch(); 1", cancellationToken: cts.Token));

        AssertCanceledBy(exception, cts.Token);
        touched().Should().Be(0);
    }

    [Test]
    public async Task APreCancelledTokenRunsNoScript_EvaluateAsyncPrepared()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (engine, touched) = CreateEngineCountingTouches();
        var prepared = Engine.PrepareScript("touch(); 1");

        var exception = await Caught.ExceptionAsync(() => engine.EvaluateAsync(prepared, cts.Token));

        AssertCanceledBy(exception, cts.Token);
        touched().Should().Be(0);
    }

    [Test]
    public async Task APreCancelledTokenRunsNoScript_ExecuteAsync()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (engine, touched) = CreateEngineCountingTouches();

        var exception = await Caught.ExceptionAsync(() => engine.ExecuteAsync("touch();", cancellationToken: cts.Token));

        AssertCanceledBy(exception, cts.Token);
        touched().Should().Be(0);
    }

    [Test]
    public async Task APreCancelledTokenRunsNoScript_ExecuteAsyncPrepared()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (engine, touched) = CreateEngineCountingTouches();
        var prepared = Engine.PrepareScript("touch();");

        var exception = await Caught.ExceptionAsync(() => engine.ExecuteAsync(prepared, cts.Token));

        AssertCanceledBy(exception, cts.Token);
        touched().Should().Be(0);
    }

    [Test]
    public async Task APreCancelledTokenRunsNoScript_InvokeAsync()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (engine, touched) = CreateEngineCountingTouches();
        engine.Execute("function f() { touch(); return 1; }");

        var exception = await Caught.ExceptionAsync(() => engine.InvokeAsync("f", cts.Token));

        AssertCanceledBy(exception, cts.Token);
        touched().Should().Be(0);
    }

    [Test]
    public async Task APreCancelledTokenRunsNoScript_ImportAsync()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (engine, touched) = CreateEngineCountingTouches();
        engine.Modules.Add("m", "touch(); export const x = 1;");

        var exception = await Caught.ExceptionAsync(() => engine.Modules.ImportAsync("m", cts.Token));

        AssertCanceledBy(exception, cts.Token);
        touched().Should().Be(0);
    }

    [Test]
    public async Task APreCancelledTokenLeavesTheEngineFreeForTheNextCall()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var engine = new Engine();

        AssertCanceledBy(await Caught.ExceptionAsync(() => engine.EvaluateAsync("1", cancellationToken: cts.Token)), cts.Token);

        (await engine.EvaluateAsync("2")).Should().Be(2);
    }

    [TestCase("for (var i = 0; i < 1000000; i++) { }")]
    [TestCase("for (var i = 0; i < 1000000; i++) ;")]
    [TestCase("var i = 0; while (i < 1000000) i++;")]
    [TestCase("var i = 0; do { i++; } while (i < 1000000);")]
    [TestCase("var i = 0; (function () { for (; i < 1000000; i++) { } })();")]
    public async Task ACancelDuringTheSynchronousRunStopsTheInterpreter(string loop)
    {
        using var cts = new CancellationTokenSource();
        var engine = CreateEngineWithCancel(cts);

        var exception = await Caught.ExceptionAsync(() => engine.EvaluateAsync("cancel(); " + loop + " i", cancellationToken: cts.Token));

        AssertCanceledBy(exception, cts.Token);
        IterationsReached(engine, "i").Should().BeLessThan(CadenceSlack);
    }

    [Test]
    public async Task ACancelDuringTheSynchronousRunStopsTheInterpreter_ExecuteAsync()
    {
        using var cts = new CancellationTokenSource();
        var engine = CreateEngineWithCancel(cts);

        var exception = await Caught.ExceptionAsync(() => engine.ExecuteAsync("cancel(); for (var i = 0; i < 1000000; i++) { }", cancellationToken: cts.Token));

        AssertCanceledBy(exception, cts.Token);
        IterationsReached(engine, "i").Should().BeLessThan(CadenceSlack);
    }

    [Test]
    public async Task ACancelDuringTheSynchronousRunStopsTheInterpreter_InvokeAsync()
    {
        using var cts = new CancellationTokenSource();
        var engine = CreateEngineWithCancel(cts);
        engine.Execute("var i = 0; function f() { cancel(); for (; i < 1000000; i++) { } return i; }");

        var exception = await Caught.ExceptionAsync(() => engine.InvokeAsync("f", cts.Token));

        AssertCanceledBy(exception, cts.Token);
        IterationsReached(engine, "i").Should().BeLessThan(CadenceSlack);
    }

    [Test]
    public async Task ACancelInsideAHostDelegateIsObservedWhenItReturns()
    {
        using var cts = new CancellationTokenSource();
        var engine = new Engine();
        engine.SetValue("cancel", new Action(cts.Cancel));

        var exception = await Caught.ExceptionAsync(() => engine.EvaluateAsync("var i = 0; cancel(); for (; i < 1000000; i++) { } i", cancellationToken: cts.Token));

        AssertCanceledBy(exception, cts.Token);
        IterationsReached(engine, "i").Should().BeLessThan(CadenceSlack);
    }

    [Test]
    public async Task ACancelInsideAContinuationRunOnTheFastPathStopsIt()
    {
        using var cts = new CancellationTokenSource();
        var engine = CreateEngineWithCancel(cts);

        var exception = await Caught.ExceptionAsync(() => engine.EvaluateAsync("""
            var i = 0;
            (async () => {
                await null;
                cancel();
                for (; i < 1000000; i++) { }
                return i;
            })()
            """, cancellationToken: cts.Token));

        AssertCanceledBy(exception, cts.Token);
        IterationsReached(engine, "i").Should().BeLessThan(CadenceSlack);
    }

    [Test]
    public async Task ACancelInsideAContinuationResumedAfterAPendingAwaitStopsIt()
    {
        using var cts = new CancellationTokenSource();
        var engine = CreateEngineWithCancel(cts);
        var io = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SetValue("io", new Func<Task<int>>(() => io.Task));

        var pending = engine.EvaluateAsync("""
            var i = 0;
            (async () => {
                await io();
                cancel();
                for (; i < 1000000; i++) { }
                return i;
            })()
            """, cancellationToken: cts.Token);

        pending.IsCompleted.Should().BeFalse("the script is parked on a host task the test has not completed");
        io.SetResult(1);

        var exception = await Caught.ExceptionAsync(() => pending);

        AssertCanceledBy(exception, cts.Token);
        IterationsReached(engine, "i").Should().BeLessThan(CadenceSlack);
    }

    [Test]
    public async Task ACancelWhileAwaitingCarriesTheCallersToken()
    {
        // The default PromiseTimeout links the caller's token with a timeout source for the wait; what the
        // caller gets back must still be its own token, as every other cancellation of the entry is.
        var engine = new Engine();
        var io = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SetValue("io", new Func<Task<int>>(() => io.Task));
        using var cts = new CancellationTokenSource();

        var pending = engine.EvaluateAsync("(async () => await io())()", cancellationToken: cts.Token);
        pending.IsCompleted.Should().BeFalse("the script is parked on a host task the test has not completed");
        cts.Cancel();

        AssertCanceledBy(await Caught.ExceptionAsync(() => pending), cts.Token);
        io.TrySetCanceled();
        (await engine.EvaluateAsync("1")).Should().Be(1);
    }

    [Test]
    public async Task ANestedSynchronousReentryObservesTheOperationToken()
    {
        using var cts = new CancellationTokenSource();
        var engine = CreateEngineWithCancel(cts);
        engine.SetValue("nested", new ClrFunction(engine, "nested", (_, _) =>
            engine.Evaluate("var j = 0; cancel(); for (; j < 1000000; j++) { } j")));

        var exception = await Caught.ExceptionAsync(() => engine.EvaluateAsync("nested(); 'outer finished'", cancellationToken: cts.Token));

        AssertCanceledBy(exception, cts.Token);
        IterationsReached(engine, "j").Should().BeLessThan(CadenceSlack);
    }

    [Test]
    public async Task ALiveTokenChangesNoResult()
    {
        using var cts = new CancellationTokenSource();
        var engine = new Engine();

        (await engine.EvaluateAsync("var s = 0; for (var i = 0; i < 100000; i++) { s += i; } s", cancellationToken: cts.Token))
            .Should().Be(4999950000d);
        (await engine.EvaluateAsync("(async () => { await null; return 42; })()", cancellationToken: cts.Token))
            .Should().Be(42);
    }

    [Test]
    public async Task OnlyACancellableTokenArmsTheCadenceAndOnlyForTheCall()
    {
        // An engine with no constraints runs no amortized checks; the token is what arms them, so a token
        // that can never be cancelled must leave the interpreter on exactly the path it ran before.
        var engine = new Engine();
        var observed = new List<bool>();
        engine.SetValue("probe", new ClrFunction(engine, "probe", (_, _) =>
        {
            observed.Add(engine._evaluationContext.RunsAmortizedChecks);
            return JsValue.Undefined;
        }));

        engine._evaluationContext.RunsAmortizedChecks.Should().BeFalse();

        await engine.EvaluateAsync("probe()");
        await engine.EvaluateAsync("probe()", cancellationToken: CancellationToken.None);
        using (var cts = new CancellationTokenSource())
        {
            await engine.EvaluateAsync("probe()", cancellationToken: cts.Token);
            await engine.EvaluateAsync("(async () => { await null; probe(); })()", cancellationToken: cts.Token);
        }

        observed.Should().Equal(false, false, true, true);
        engine._evaluationContext.RunsAmortizedChecks.Should().BeFalse();
    }

    [Test]
    public async Task ACancelledCallLeavesNothingBehindForTheNextOne()
    {
        using var cts = new CancellationTokenSource();
        var engine = CreateEngineWithCancel(cts);

        AssertCanceledBy(
            await Caught.ExceptionAsync(() => engine.EvaluateAsync("cancel(); for (var i = 0; i < 1000000; i++) { } i", cancellationToken: cts.Token)),
            cts.Token);

        engine._evaluationContext.RunsAmortizedChecks.Should().BeFalse();

        // The token is still cancelled, but it belonged to the call that ended: neither a synchronous entry
        // nor an async one with a token of its own may observe it.
        engine.Evaluate("for (var k = 0; k < 100000; k++) { } k").Should().Be(100000);
        (await engine.EvaluateAsync("for (var k = 0; k < 100000; k++) { } k")).Should().Be(100000);
        using var fresh = new CancellationTokenSource();
        (await engine.EvaluateAsync("for (var k = 0; k < 100000; k++) { } k", cancellationToken: fresh.Token)).Should().Be(100000);

        // ...and handing it back in again is refused up front.
        AssertCanceledBy(await Caught.ExceptionAsync(() => engine.EvaluateAsync("1", cancellationToken: cts.Token)), cts.Token);
    }

    [Test]
    public async Task ARegisteredAmortizedConstraintKeepsTheCadenceAfterTheCall()
    {
        using var observed = new CancellationTokenSource();
        var engine = new Engine(options => options.ObserveCancellation(observed.Token));
        engine._evaluationContext.RunsAmortizedChecks.Should().BeTrue();

        using (var cts = new CancellationTokenSource())
        {
            await engine.EvaluateAsync("1", cancellationToken: cts.Token);
        }

        engine._evaluationContext.RunsAmortizedChecks.Should().BeTrue();
    }

    [Test]
    public async Task AHostThatAlsoRegisteredObserveCancellationKeepsItsException()
    {
        // The registered constraint is checked first, so a host that already caught ExecutionCanceledException
        // for the same token passed both ways sees no change once the script is running.
        using var cts = new CancellationTokenSource();
        var engine = new Engine(options => options.ObserveCancellation(cts.Token));
        engine.SetValue("cancel", new ClrFunction(engine, "cancel", (_, _) =>
        {
            cts.Cancel();
            return JsValue.Undefined;
        }));

        var exception = await Caught.ExceptionAsync(() => engine.EvaluateAsync("cancel(); for (var i = 0; i < 1000000; i++) { } i", cancellationToken: cts.Token));

        exception.Should().BeOfType<ExecutionCanceledException>();
    }

    // Options.Host.JobCallbacks wraps every reaction in Enter/Exit (HostCallJobCallback), so a cancel observed
    // inside one unwinds through the hook. Both reaction lanes are covered: a then-handler
    // (HostDefinedReactionHandlers) and a resumed async body (HostDefinedContinuation).
    [TestCase("Promise.resolve().then(() => { cancel(); for (; i < 1000000; i++) { } return i; })")]
    [TestCase("(async () => { await null; cancel(); for (; i < 1000000; i++) { } return i; })()")]
    public async Task ACancelInsideAReactionRunUnderJobCallbackHooksStopsItAndExitsTheHook(string reaction)
    {
        using var cts = new CancellationTokenSource();
        var hooks = new CountingJobCallbackHooks();
        var engine = new Engine(options => options.Host.JobCallbacks = hooks);
        engine.SetValue("cancel", new ClrFunction(engine, "cancel", (_, _) =>
        {
            cts.Cancel();
            return JsValue.Undefined;
        }));

        var exception = await Caught.ExceptionAsync(() => engine.EvaluateAsync("var i = 0; " + reaction, cancellationToken: cts.Token));

        AssertCanceledBy(exception, cts.Token);
        IterationsReached(engine, "i").Should().BeLessThan(CadenceSlack);
        hooks.Enters.Should().BeGreaterThan(0, "the cancelled reaction ran under the hook");
        hooks.Exits.Should().Be(hooks.Enters, "the cancellation unwound through HostCallJobCallback's Exit");
        hooks.Depth.Should().Be(0);

        // The engine, and the hooks with it, serve the next call as if nothing had happened.
        (await engine.EvaluateAsync("(async () => { await null; return 7; })()")).Should().Be(7);
        hooks.Exits.Should().Be(hooks.Enters);
        hooks.Depth.Should().Be(0);
    }

    [Test]
    public async Task APreCancelledTokenCapturesNoJobCallback()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var hooks = new CountingJobCallbackHooks();
        var engine = new Engine(options => options.Host.JobCallbacks = hooks);

        var exception = await Caught.ExceptionAsync(() => engine.EvaluateAsync("(async () => { await null; return 1; })()", cancellationToken: cts.Token));

        AssertCanceledBy(exception, cts.Token);
        hooks.Captures.Should().Be(0);
        hooks.Enters.Should().Be(0);
    }

    /// <summary>Captures a flow for every registration and counts how deep inside Enter/Exit the engine is.</summary>
    private sealed class CountingJobCallbackHooks : JobCallbackHooks
    {
        internal int Captures;
        internal int Enters;
        internal int Exits;
        internal int Depth;

        protected internal override object Capture(Engine engine)
        {
            Captures++;
            return "flow";
        }

        protected internal override object Enter(Engine engine, object hostDefined)
        {
            Enters++;
            Depth++;
            return null;
        }

        protected internal override void Exit(Engine engine, object token)
        {
            Exits++;
            Depth--;
        }
    }

    private static (Engine Engine, Func<int> Touched) CreateEngineCountingTouches()
    {
        var count = 0;
        var engine = new Engine();
        engine.SetValue("touch", new ClrFunction(engine, "touch", (_, _) =>
        {
            count++;
            return JsValue.Undefined;
        }));
        return (engine, () => count);
    }

    // A ClrFunction rather than a delegate: delegate dispatch re-checks the amortized constraints when the
    // host code returns, and this has to be the interpreter's own cadence noticing the cancel.
    private static Engine CreateEngineWithCancel(CancellationTokenSource cts)
    {
        var engine = new Engine();
        engine.SetValue("cancel", new ClrFunction(engine, "cancel", (_, _) =>
        {
            cts.Cancel();
            return JsValue.Undefined;
        }));
        return engine;
    }

    private static double IterationsReached(Engine engine, string name) => engine.GetValue(name).AsNumber();

    private static void AssertCanceledBy(Exception exception, CancellationToken token)
    {
        exception.Should().BeAssignableTo<OperationCanceledException>();
        ((OperationCanceledException) exception).CancellationToken.Should().Be(token);
    }
}
