#nullable enable

using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins where a failure of an <c>*Async</c> engine entry is delivered.
/// <para>
/// The rule: a usage error — a <see langword="null"/> argument, a <see cref="Prepared{TProgram}"/> that did
/// not come from <c>PrepareScript</c>, and the refusal because the engine is already in use — is thrown out
/// of the call itself, because it means the operation never started. Everything the operation does —
/// parsing, running, an execution constraint tripping, a rejected promise — is delivered through the
/// returned <see cref="Task"/>, so a host that wraps only its <c>await</c> sees all of it.
/// </para>
/// <para>
/// Before this was true, which channel a constraint failure took was decided by a thread race: a charge
/// landed by a host callback on another thread could trip the post-script check while the engine thread was
/// still inside the synchronous phase, where no task exists yet, and the exception erupted from the call.
/// <see cref="AChargeLandedByAnotherThreadBeforeThePostScriptCheckStillFaultsTheTask"/> forces exactly that
/// ordering, which is why it needs no repetition to be meaningful.
/// </para>
/// </summary>
public class HostAsyncFailureChannelTests
{
    private const string ConcurrentUseMessage = "*already in use by another thread or has an asynchronous operation in progress*";
    private const int AllocationSize = 2_000_000;
    private const int SingleAllocationBudget = 3_500_000;

    private static readonly TimeSpan HandoffCeiling = TimeSpan.FromSeconds(10);

    // ---------------------------------------------------------------------------------------------
    // Through the task: everything the operation itself does.
    // ---------------------------------------------------------------------------------------------

    [Test]
    public async Task AStatementBudgetReachesTheTaskRatherThanTheCall()
    {
        var engine = new Engine(options => options.LimitStatements(10));

        var pending = StartWithoutThrowing(() => engine.EvaluateAsync("for (var i = 0; i < 1000; i++) { }"));

        var exception = await Caught.ExceptionAsync(() => pending);
        exception.Should().BeOfType<StatementsCountOverflowException>();
    }

    [Test]
    public async Task AWallClockBudgetReachesTheTaskRatherThanTheCall()
    {
        var engine = new Engine(options => options.LimitExecutionTime(TimeSpan.FromMilliseconds(50)));

        var pending = StartWithoutThrowing(() => engine.EvaluateAsync("while (true) { }"));

        var exception = await Caught.ExceptionAsync(() => pending);
        exception.Should().BeOfType<TimeoutException>();
    }

    [Test]
    public async Task AnAllocationBudgetReachesTheTaskRatherThanTheCall()
    {
        var allocations = new List<byte[]>();
        var engine = CreateMemoryLimitedEngine(allocations);

        var pending = StartWithoutThrowing(() => engine.EvaluateAsync("allocate(); allocate();"));

        var exception = await Caught.ExceptionAsync(() => pending);
        exception.Should().BeOfType<MemoryLimitExceededException>();
    }

    [Test]
    public async Task ARecursionLimitReachesTheTaskRatherThanTheCall()
    {
        var engine = new Engine(options => options.Constraints.MaxRecursionDepth = 5);

        var pending = StartWithoutThrowing(() => engine.EvaluateAsync("function f() { return f(); } f();"));

        var exception = await Caught.ExceptionAsync(() => pending);
        exception.Should().BeOfType<RecursionDepthOverflowException>();
    }

    [Test]
    public async Task ACancellationConstraintReachesTheTaskRatherThanTheCall()
    {
        using var cts = new CancellationTokenSource();
        var engine = new Engine(options => options.ObserveCancellation(cts.Token));
        engine.SetValue("cancel", new Action(cts.Cancel));

        var pending = StartWithoutThrowing(() => engine.EvaluateAsync("cancel(); for (var i = 0; i < 1000000; i++) { }"));

        var exception = await Caught.ExceptionAsync(() => pending);
        exception.Should().BeOfType<ExecutionCanceledException>();
    }

    [Test]
    public async Task ASyntaxErrorReachesTheTaskRatherThanTheCall()
    {
        var engine = new Engine();

        var pending = StartWithoutThrowing(() => engine.EvaluateAsync("var ="));

        var exception = await Caught.ExceptionAsync(() => pending);
        exception.Should().BeOfType<JavaScriptException>();
        exception!.Message.Should().Contain("Unexpected token");
    }

    [Test]
    public async Task AScriptThrowReachesTheTaskRatherThanTheCall()
    {
        var engine = new Engine();

        var pending = StartWithoutThrowing(() => engine.EvaluateAsync("throw new Error('boom')"));

        var exception = await Caught.ExceptionAsync(() => pending);
        exception.Should().BeOfType<JavaScriptException>();
        exception!.Message.Should().Be("boom");
    }

    [Test]
    public async Task ThePreparedScriptOverloadReportsItsBudgetThroughTheTaskToo()
    {
        var engine = new Engine(options => options.LimitStatements(10));
        var prepared = Engine.PrepareScript("for (var i = 0; i < 1000; i++) { }");

        var pending = StartWithoutThrowing(() => engine.EvaluateAsync(prepared));

        var exception = await Caught.ExceptionAsync(() => pending);
        exception.Should().BeOfType<StatementsCountOverflowException>();
    }

    [Test]
    public async Task ExecuteAsyncReportsItsBudgetThroughTheTaskToo()
    {
        var engine = new Engine(options => options.LimitStatements(10));

        Task<Engine>? pending = null;
        Invoking(() => { pending = engine.ExecuteAsync("for (var i = 0; i < 1000; i++) { }"); })
            .Should().NotThrow("a budget failure belongs on the returned task");

        var exception = await Caught.ExceptionAsync(() => pending!);
        exception.Should().BeOfType<StatementsCountOverflowException>();
    }

    [Test]
    public async Task InvokeAsyncReportsItsBudgetThroughTheTaskToo()
    {
        var engine = new Engine(options => options.LimitStatements(10));
        engine.Execute("function work() { for (var i = 0; i < 1000; i++) { } }");

        var pending = StartWithoutThrowing(() => engine.InvokeAsync("work"));

        var exception = await Caught.ExceptionAsync(() => pending);
        exception.Should().BeOfType<StatementsCountOverflowException>();
    }

    [Test]
    public async Task InvokeAsyncReportsANonFunctionThroughTheTaskToo()
    {
        var engine = new Engine();

        var pending = StartWithoutThrowing(() => engine.InvokeAsync("missing"));

        var exception = await Caught.ExceptionAsync(() => pending);
        exception.Should().BeOfType<JavaScriptException>();
        exception!.Message.Should().Be("Can only invoke functions");
    }

    [Test]
    public async Task ImportAsyncReportsItsBudgetThroughTheTaskToo()
    {
        var engine = new Engine(options => options.LimitStatements(10));
        engine.Modules.Add("module", builder => builder.AddSource("for (var i = 0; i < 1000; i++) { }"));

        Task<ObjectInstance>? pending = null;
        Invoking(() => { pending = engine.Modules.ImportAsync("module"); })
            .Should().NotThrow("a budget failure belongs on the returned task");

        var exception = await Caught.ExceptionAsync(() => pending!);
        exception.Should().BeOfType<StatementsCountOverflowException>();
    }

    // ---------------------------------------------------------------------------------------------
    // The race the issue reports, forced into a deterministic ordering.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The mechanism behind <c>HostMemoryLimitTests.TransferredHostCallbackCountsAllocationsOnItsThread</c>:
    /// a host callback running on another thread is charged to the same operation, and if its charge lands
    /// before the engine thread reaches <c>ScriptEvaluation</c>'s post-script check, the check trips inside
    /// the synchronous phase. Joining the other thread inside the host function forces that ordering on every
    /// run, so this fails deterministically against an engine that reports such a failure synchronously.
    /// </summary>
    [Test]
    public async Task AChargeLandedByAnotherThreadBeforeThePostScriptCheckStillFaultsTheTask()
    {
        var allocations = new List<byte[]>();
        var engine = CreateMemoryLimitedEngine(allocations);
        engine.SetValue("schedule", new Func<Func<int>, Task<int>>(callback =>
        {
            // The flaky test hands the pool thread's task straight back and lets the two threads race.
            // Waiting for it here is the whole of the difference: the pool thread's allocation is charged
            // to this operation before the engine thread returns, so it is always the post-script check
            // that trips, and always inside the synchronous phase.
            var scheduled = Task.Run(callback);
            scheduled.Wait();
            return scheduled;
        }));

        var pending = StartWithoutThrowing(() => engine.EvaluateAsync("""
            (async () => {
                allocate();
                return await schedule(() => {
                    allocate();
                    return 42;
                });
            })()
            """));

        var exception = await Caught.ExceptionAsync(() => pending);
        exception.Should().BeOfType<MemoryLimitExceededException>();
    }

    // ---------------------------------------------------------------------------------------------
    // The constraint is unchanged: same type, same message, still fatal, and the engine recovers.
    // ---------------------------------------------------------------------------------------------

    [Test]
    public async Task TheTaskCarriesTheSameExceptionTheSynchronousEntryThrows()
    {
        const string Script = "globalThis.marker = 1; for (var i = 0; i < 1000; i++) { } globalThis.finished = true;";

        var synchronous = new Engine(options => options.LimitStatements(10));
        var fromEvaluate = Caught.Exception(() => synchronous.Evaluate(Script));

        var asynchronous = new Engine(options => options.LimitStatements(10));
        var fromEvaluateAsync = await Caught.ExceptionAsync(() => asynchronous.EvaluateAsync(Script));

        fromEvaluate.Should().BeOfType<StatementsCountOverflowException>();
        fromEvaluateAsync.Should().BeOfType<StatementsCountOverflowException>();
        fromEvaluateAsync!.Message.Should().Be(fromEvaluate!.Message);

        // Still fatal, not something the script rides through: the statement after the loop never ran.
        asynchronous.Evaluate("typeof globalThis.marker").AsString().Should().Be("number");
        asynchronous.Evaluate("typeof globalThis.finished").AsString().Should().Be("undefined");
    }

    [Test]
    public async Task TheEngineIsUsableAgainAfterATaskFaultedBudgetFailure()
    {
        var engine = new Engine(options => options.LimitStatements(50));

        var first = await Caught.ExceptionAsync(() => engine.EvaluateAsync("for (var i = 0; i < 1000; i++) { }"));
        first.Should().BeOfType<StatementsCountOverflowException>();

        // The reservation is released by the body's finally even when the body never reached an await, so a
        // faulted task does not leave the engine claimed for ever.
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
        var second = await engine.EvaluateAsync("2 + 2");
        second.AsNumber().Should().Be(4);
    }

    [Test]
    public async Task TheBudgetStillFiresWhenTheFailureOnlyReachesTheTask()
    {
        var engine = new Engine(options => options.LimitStatements(10));

        // The shape the issue names as the one a host actually writes: the call is not wrapped, only the
        // await is. The budget must still fire, and it must land here.
        var pending = engine.EvaluateAsync("for (var i = 0; i < 1000; i++) { globalThis.ran = i; }");

        var caught = false;
        try
        {
            await pending;
        }
        catch (StatementsCountOverflowException)
        {
            caught = true;
        }

        caught.Should().BeTrue("the constraint must still be fatal, only delivered elsewhere");
        engine.Evaluate("globalThis.ran").AsNumber().Should().BeLessThan(1000);
    }

    // ---------------------------------------------------------------------------------------------
    // Out of the call: usage errors, which say the operation never started.
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void ANullScriptIsRefusedSynchronously()
    {
        var engine = new Engine();

        Invoking(() => { _ = engine.EvaluateAsync(null!); })
            .Should().Throw<ArgumentNullException>().WithParameterName("code");
        Invoking(() => { _ = engine.ExecuteAsync(null!); })
            .Should().Throw<ArgumentNullException>().WithParameterName("code");

        // Nothing was reserved, so the engine is still free.
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    [Test]
    public void ANullInvokeArgumentIsRefusedSynchronously()
    {
        var engine = new Engine();
        engine.Execute("function work() { }");

        Invoking(() => { _ = engine.InvokeAsync(null!); })
            .Should().Throw<ArgumentNullException>().WithParameterName("propertyName");
        Invoking(() => { _ = engine.InvokeAsync("work", (object?[]) null!); })
            .Should().Throw<ArgumentNullException>().WithParameterName("arguments");

        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    [Test]
    public void AnUnpreparedScriptIsRefusedSynchronously()
    {
        var engine = new Engine();

        Invoking(() => { _ = engine.EvaluateAsync(default(Prepared<Script>)); })
            .Should().Throw<ArgumentException>().WithParameterName("preparedScript");

        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    [Test]
    public async Task EveryAsyncEntryRefusesAConcurrentCallSynchronously()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engine = new Engine();
        engine.Modules.Add("module", builder => builder.AddSource("export const value = 1;"));
        engine.SetValue("block", new Action(() =>
        {
            entered.Set();
            release.Wait();
        }));
        engine.Execute("function work() { }");
        var prepared = Engine.PrepareScript("1");

        var running = DedicatedThread.RunAsync(() => engine.Execute("block()"));
        entered.Wait(HandoffCeiling).Should().BeTrue("the owning thread did not enter block()");
        try
        {
            Invoking(() => { _ = engine.EvaluateAsync("1"); })
                .Should().Throw<InvalidOperationException>().WithMessage(ConcurrentUseMessage);
            Invoking(() => { _ = engine.EvaluateAsync(prepared); })
                .Should().Throw<InvalidOperationException>().WithMessage(ConcurrentUseMessage);
            Invoking(() => { _ = engine.ExecuteAsync("1"); })
                .Should().Throw<InvalidOperationException>().WithMessage(ConcurrentUseMessage);
            Invoking(() => { _ = engine.InvokeAsync("work"); })
                .Should().Throw<InvalidOperationException>().WithMessage(ConcurrentUseMessage);
            Invoking(() => { _ = engine.Modules.ImportAsync("module"); })
                .Should().Throw<InvalidOperationException>().WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    private static Task<JsValue> StartWithoutThrowing(Func<Task<JsValue>> start)
    {
        Task<JsValue>? pending = null;
        Invoking(() => { pending = start(); })
            .Should().NotThrow("the failure of the operation belongs on the returned task, not on the call");
        return pending!;
    }

    private static Engine CreateMemoryLimitedEngine(List<byte[]> allocations)
    {
        var engine = new Engine(options => options.LimitMemory(SingleAllocationBudget));
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));
        return engine;
    }

}
