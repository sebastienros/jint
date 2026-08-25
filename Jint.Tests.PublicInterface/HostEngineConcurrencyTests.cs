#nullable enable

using System.Diagnostics;
using System.Text;
using Jint.Native;
using Jint.Native.Json;
using Jint.Runtime;
using Jint.Runtime.Debugger;
using Jint.Runtime.Modules;
using Xunit.Sdk;


namespace Jint.Tests.PublicInterface;

/// <summary>
/// The engine's one-operation-at-a-time contract, from the outside: which public entries a second thread
/// is refused, which authorized hand-offs it is granted, and that a refusal leaves the engine usable.
/// </summary>
/// <remarks>
/// Every test here needs one thread parked inside the engine while another tries to enter, and none of
/// them measures how long that takes. So the thread doing the parking is one the test starts itself, and
/// the wait for it is released by a signal rather than by a clock — see <see cref="StartOwningThread"/>
/// and <see cref="WaitUntilOwned"/>, and <see cref="HandoffCeiling"/> for the one clock left.
/// </remarks>
public class HostEngineConcurrencyTests
{
    private const string ConcurrentUseMessage = "*already in use by another thread or has an asynchronous operation in progress*";

    /// <summary>
    /// Reached only by a genuine wedge. Every wait in this class is released by a thread the test owns, so
    /// no amount of runner load can lose the race and only a hang can spend two minutes here. It is also
    /// what an engine here is given as its <c>PromiseTimeout</c> when the settle it is waiting for is
    /// delivered by the thread pool rather than by the test — see <see cref="TestBudgets.WedgeCeiling"/>.
    /// </summary>
    private static readonly TimeSpan HandoffCeiling = TestBudgets.WedgeCeiling;

    /// <summary>
    /// Runs the call that takes the engine and parks inside <c>block()</c> on a thread of the test's own.
    /// Deliberately not <c>Task.Run</c>: this body occupies its thread for as long as the test keeps it
    /// blocked, and a saturated thread pool injects a worker at roughly one per 500 ms — which is what
    /// turned "has the other thread entered the engine yet" into a wall-clock race the ten-second budget
    /// could lose on a loaded runner (sebastienros/jint#3201).
    /// </summary>
    private static Task StartOwningThread(Action body) => DedicatedThread.RunAsync(body);

    /// <summary>
    /// Waits until <paramref name="running"/> is provably parked inside <c>block()</c>, which is when the
    /// engine is genuinely owned by that thread and the assertions below mean what they say. It ends on
    /// the signal, on that thread finishing without ever reaching <c>block()</c> — reporting whatever
    /// stopped it rather than a timeout — or on <see cref="HandoffCeiling"/>.
    /// </summary>
    private static async Task WaitUntilOwned(ManualResetEventSlim entered, Task running)
    {
        var elapsed = Stopwatch.StartNew();
        while (!entered.Wait(TimeSpan.FromMilliseconds(20)))
        {
            if (running.IsCompleted)
            {
                // Rethrows the owning thread's own exception, with its stack, when it had one.
                await running;
                throw new XunitException("the owning call returned without ever entering block()");
            }

            if (elapsed.Elapsed > HandoffCeiling)
            {
                throw new XunitException($"the owning thread did not enter block() within {HandoffCeiling}");
            }
        }
    }

    [Fact]
    public async Task ConcurrentExecuteIsRejectedAndTheEngineRecovers()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engine = CreateBlockingEngine(entered, release);
        var running = StartOwningThread(() => engine.Execute("block()"));

        await WaitUntilOwned(entered, running);
        try
        {
            Invoking(() => engine.Execute("globalThis.concurrent = true"))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
        engine.Evaluate("typeof concurrent").AsString().Should().Be("undefined");
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    [Fact]
    public async Task ConcurrentEvaluateIsRejected()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engine = CreateBlockingEngine(entered, release);
        var running = StartOwningThread(() => engine.Evaluate("block()"));

        await WaitUntilOwned(entered, running);
        try
        {
            Invoking(() => engine.Evaluate("40 + 2"))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
    }

    [Fact]
    public async Task ConcurrentInvokeIsRejected()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engine = CreateBlockingEngine(entered, release);
        engine.Execute("function waitForHost() { return block(); }");
        var running = StartOwningThread(() => engine.Invoke("waitForHost"));

        await WaitUntilOwned(entered, running);
        try
        {
            Invoking(() => engine.Invoke("waitForHost"))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
    }

    /// <summary>
    /// <c>ShadowRealm.ImportValue</c> drives module loading and evaluation, so it runs script and has to claim
    /// the engine for all of it — the load, and the continuations it drains afterwards. It used to claim it for
    /// neither, so a second thread was served in the middle of a load. sebastienros/jint#3324.
    /// </summary>
    [Fact]
    public async Task ConcurrentShadowRealmImportValueIsRejected()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engine = new Engine(options => options.UseModules(new BlockingModuleLoader(entered, release, HandoffCeiling)));
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();
        var running = StartOwningThread(() => shadowRealm.ImportValue("./blocked.js", "value"));

        await WaitUntilOwned(entered, running);
        try
        {
            Invoking(() => engine.Evaluate("40 + 2"))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
        engine.Evaluate("40 + 2").AsNumber().Should().Be(42);
    }

    [Fact]
    public async Task ConcurrentMutationIsRejectedBeforeItChangesState()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engine = CreateBlockingEngine(entered, release);
        var running = StartOwningThread(() => engine.Execute("block()"));

        await WaitUntilOwned(entered, running);
        try
        {
            Invoking(() => engine.SetValue("leaked", 42))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
            Invoking(() => JsValue.FromObject(engine, new object()))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
        engine.Evaluate("typeof leaked").AsString().Should().Be("undefined");
    }

    [Fact]
    public async Task ConcurrentAsyncEntryIsRejectedWithoutInterruptingTheOwner()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engine = CreateBlockingEngine(entered, release);
        var running = StartOwningThread(() => engine.Execute("block(); globalThis.finished = true"));

        await WaitUntilOwned(entered, running);
        try
        {
            Action concurrent = () => _ = engine.EvaluateAsync("1");
            Invoking(concurrent)
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
        engine.Evaluate("finished").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void SameThreadHostCallbackCanReenterAndMutateTheEngine()
    {
        var engine = new Engine();
        engine.SetValue("reenter", new Func<int>(() =>
        {
            engine.SetValue("nested", 21);
            return (int) engine.Evaluate("nested * 2").AsNumber();
        }));

        engine.Evaluate("reenter()").AsNumber().Should().Be(42);
    }

    [Fact]
    public void HostCanSynchronouslyDispatchAJsCallbackToAnotherThread()
    {
        var engine = new Engine();
        engine.SetValue("onOtherThread", new Func<Func<int, int>, int>(callback =>
        {
            Exception? error = null;
            var result = 0;
            var thread = new Thread(() =>
            {
                try
                {
                    result = callback(41);
                }
                catch (Exception exception)
                {
                    error = exception;
                }
            });
            thread.Start();
            thread.Join();
            if (error is not null)
            {
                throw error;
            }

            return result;
        }));

        engine.Evaluate("onOtherThread(x => x + 1)").AsNumber().Should().Be(42);
        engine.Evaluate("20 + 22").AsNumber().Should().Be(42);
    }

    [Fact]
    public void ReflectedHostMethodCanDispatchAJsCallbackToAnotherThread()
    {
        var engine = new Engine();
        engine.SetValue("host", new CallbackHost());

        engine.Evaluate("host.OnOtherThread(x => x + 1)").AsNumber().Should().Be(42);
        engine.Evaluate("20 + 22").AsNumber().Should().Be(42);
    }

    [Fact]
    public void ReflectedHostMethodCanDispatchAJsCallbackFromAParamsArray()
    {
        var engine = new Engine();
        engine.SetValue("host", new CallbackHost());

        engine.Evaluate("host.OnOtherThreadFromArray(() => 42)").AsNumber().Should().Be(42);
        engine.Evaluate("20 + 22").AsNumber().Should().Be(42);
    }

    [Fact]
    public void HostReceivingAJsCallbackCanReenterOnTheSameThread()
    {
        var engine = new Engine();
        engine.SetValue("reenter", new Func<Func<int>, int>(callback =>
            (int) engine.Evaluate("20 + 21").AsNumber() + callback()));

        engine.Evaluate("reenter(() => 1)").AsNumber().Should().Be(42);
    }

    [Fact]
    public void AsyncEntryFromAHostCallbackFailsBeforeStartingWork()
    {
        var engine = new Engine();
        engine.SetValue("completed", false);
        engine.SetValue("reenter", new Action(() =>
        {
            Action nested = () => _ = engine.EvaluateAsync("completed = true");
            Invoking(nested)
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }));

        engine.Execute("reenter()");
        engine.Evaluate("completed").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task OutstandingEvaluateAsyncOwnsTheEngineUntilItsTaskCompletes()
    {
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new Engine();
        engine.SetValue("hostWork", new Func<Task<int>>(() => gate.Task));

        var pending = engine.EvaluateAsync("(async () => (await hostWork()) + 1)()");
        pending.IsCompleted.Should().BeFalse();

        Invoking(() => engine.Evaluate("1"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage(ConcurrentUseMessage);
        Invoking(() => engine.SetValue("other", 1))
            .Should().Throw<InvalidOperationException>()
            .WithMessage(ConcurrentUseMessage);

        await Task.Run(() => gate.SetResult(41));
        (await pending).AsNumber().Should().Be(42);
        engine.Evaluate("6 * 7").AsNumber().Should().Be(42);
    }

    [Fact]
    public async Task AsyncHostMethodCanInvokeItsJsCallbackFromAnotherThread()
    {
        var callbackReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var invokeCallback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new Engine();
        engine.SetValue("schedule", new Func<Func<int>, Task<int>>(callback =>
        {
            callbackReady.SetResult(true);
            return Task.Run(async () =>
            {
                await invokeCallback.Task;
                return callback();
            });
        }));

        var pending = engine.EvaluateAsync("(async () => (await schedule(() => 42)))()");
        await callbackReady.Task;
        invokeCallback.SetResult(true);

        (await pending).AsNumber().Should().Be(42);
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    [Fact]
    public async Task ImmediateCrossThreadCallbacksDoNotLeakAsyncOwnership()
    {
        var engine = new Engine();
        engine.SetValue("schedule", new Func<Func<int>, Task<int>>(callback => Task.Run(callback)));

        for (var i = 0; i < 50; i++)
        {
            (await engine.EvaluateAsync("(async () => await schedule(() => 42))()"))
                .AsNumber().Should().Be(42);
        }

        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// How long a continuation running inside the drain keeps the engine thread, so that the callback
    /// dispatched from another thread provably arrives while the owner is on the engine rather than inside
    /// its blocking wait. This is the one place a clock is used to <em>occupy</em> rather than to wait for
    /// something: what is under test is precisely what happens to a callback that arrives then, so the
    /// arrival has to be inside a stretch this test controls. Nothing waits this out - the two tests below
    /// both finish as soon as their callback has been served.
    /// </summary>
    private static readonly TimeSpan DrainOccupancy = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Long enough that the continuation it settles runs from the drain rather than from the tail of
    /// <c>Evaluate</c>, which drains the microtask queue before it returns.
    /// </summary>
    private static readonly TimeSpan ContinuationDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// The window README's Thread-safety section and THREAT_MODEL.md TM-13 promise an authorized callback
    /// may wait in - "such an authorized transfer may wait for the current callback turn" - reached
    /// deterministically. An <c>async</c> host method returns at its first <c>await</c>, so by the time its
    /// captured callback is invoked from another thread the host call it was handed to is long gone; the
    /// owner thread is inside <c>UnwrapIfPromise</c>'s drain, running a continuation. The callback has to be
    /// served after the owner's turn, not destroyed. sebastienros/jint#3206.
    /// </summary>
    [Fact]
    public void AnAsyncHostMethodsCallbackIsServedWhileTheOwnerRunsTheDrain()
    {
        Func<int>? captured = null;
        using var dispatcher = new CallbackDispatcher(() => captured!());

        var engine = new Engine();
        engine.SetValue("register", new Func<Func<int>, Task>(async callback =>
        {
            captured = callback;
            await dispatcher.Attempted.ConfigureAwait(false);
        }));
        engine.SetValue("delay", new Func<Task>(() => Task.Delay(ContinuationDelay)));
        engine.SetValue("hold", new Action(() =>
        {
            dispatcher.Release();
            Thread.Sleep(DrainOccupancy);
        }));

        var result = engine.Evaluate("""
            async function main() {
                const pending = register(() => 42);
                await delay();
                hold();
                await pending;
                return 'done';
            }
            main();
            """).UnwrapIfPromise(HandoffCeiling);

        dispatcher.Failure.Should().BeNull("an authorized callback may wait for its turn, not be refused");
        dispatcher.Result.Should().Be(42);
        result.AsString().Should().Be("done");
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// The other half of sebastienros/jint#3206: the stretch between an <c>async</c> host method returning at
    /// its first <c>await</c> and the caller reaching anything that waits. Here the callback is dispatched
    /// while the engine thread is still running script for the very evaluation that handed it out, so there
    /// is no drain to be admitted into - the reservation the host call took has to outlive that call.
    /// </summary>
    [Fact]
    public void AnAsyncHostMethodsCallbackIsServedWhileTheOwnerFinishesTheEvaluation()
    {
        Func<int>? captured = null;
        using var dispatcher = new CallbackDispatcher(() => captured!());

        var engine = new Engine();
        engine.SetValue("register", new Func<Func<int>, Task<string>>(async callback =>
        {
            captured = callback;
            await dispatcher.Attempted.ConfigureAwait(false);
            return "done";
        }));
        engine.SetValue("hold", new Action(() =>
        {
            dispatcher.Release();
            Thread.Sleep(DrainOccupancy);
        }));

        var result = engine.Evaluate("""
            const pending = register(() => 42);
            hold();
            pending;
            """).UnwrapIfPromise(HandoffCeiling);

        dispatcher.Failure.Should().BeNull("an authorized callback may wait for its turn, not be refused");
        dispatcher.Result.Should().Be(42);
        result.AsString().Should().Be("done");
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// The same window, reached by the shape the second comment on sebastienros/jint#3206 widened the report
    /// to: any host API of the form "hand me a callback, I will call it later from my own thread", not only
    /// an <c>async</c> method. Here the host call that received the callback returned synchronously long
    /// before the callback is invoked, which is the shipped shape of
    /// <see cref="DelayedCrossThreadCallbackReleasesBlockingPromiseOwnership"/> with the timing pinned.
    /// </summary>
    [Fact]
    public void ACallbackStoredByAHostMethodIsServedWhileTheOwnerRunsTheDrain()
    {
        Action? captured = null;
        using var dispatcher = new CallbackDispatcher(() =>
        {
            captured!();
            return 0;
        });

        var engine = new Engine();
        engine.SetValue("later", new Action<Action>(callback => captured = callback));
        engine.SetValue("delay", new Func<Task>(() => Task.Delay(ContinuationDelay)));
        engine.SetValue("attempted", new Func<Task>(() => dispatcher.Attempted));
        engine.SetValue("hold", new Action(() =>
        {
            dispatcher.Release();
            Thread.Sleep(DrainOccupancy);
        }));

        var result = engine.Evaluate("""
            globalThis.marker = 0;
            async function main() {
                later(() => { globalThis.marker = 42; });
                await delay();
                hold();
                await attempted();
                return globalThis.marker;
            }
            main();
            """).UnwrapIfPromise(HandoffCeiling);

        dispatcher.Failure.Should().BeNull("an authorized callback may wait for its turn, not be refused");
        result.AsNumber().Should().Be(42);
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    [Fact]
    public void DelayedCrossThreadCallbackReleasesBlockingPromiseOwnership()
    {
        var engine = new Engine();
        engine.SetValue("later", new Action<Action>(callback =>
            Task.Delay(50).ContinueWith(_ => callback(), TaskScheduler.Default)));

        // The three siblings above already pass HandoffCeiling here; this call site was the one left on the
        // parameterless overload, whose ceiling is a hard-coded ten seconds that no engine option reaches -
        // and the release it is waiting for is a Task.Delay continuation, i.e. a thread-pool worker.
        engine.Evaluate("new Promise(resolve => later(() => resolve(42)))")
            .UnwrapIfPromise(HandoffCeiling)
            .AsNumber()
            .Should()
            .Be(42);

        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    [Fact]
    public async Task AsyncRejectionReleasesOwnership()
    {
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new Engine();
        engine.SetValue("hostWork", new Func<Task<int>>(() => gate.Task));

        var pending = engine.EvaluateAsync("(async () => await hostWork())()");
        gate.SetException(new InvalidOperationException("failed"));

        await Awaiting(() => pending).Should().ThrowAsync<PromiseRejectedException>();
        engine.Evaluate("20 + 22").AsNumber().Should().Be(42);
    }

    [Fact]
    public async Task AsyncCancellationReleasesOwnership()
    {
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var engine = new Engine();
        engine.SetValue("hostWork", new Func<Task<int>>(() => gate.Task));

        var pending = engine.EvaluateAsync("(async () => await hostWork())()", cancellationToken: cancellation.Token);
        cancellation.Cancel();

        await Awaiting(() => pending).Should().ThrowAsync<OperationCanceledException>();
        engine.Evaluate("20 + 22").AsNumber().Should().Be(42);
    }

    [Fact]
    public async Task BackgroundModuleCompletionCanFinishTheOwningImport()
    {
        var loader = new DeferredModuleLoader();
        // The settle is delivered by a thread-pool worker, so the engine's default ten-second budget would
        // make the pool's injection rate part of the outcome. HandoffCeiling instead.
        var engine = new Engine(options =>
        {
            options.UseModules(loader);
            options.Constraints.PromiseTimeout = HandoffCeiling;
        });

        var pending = engine.Modules.ImportAsync("module");
        pending.IsCompleted.Should().BeFalse();
        Invoking(() => engine.Execute("1"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage(ConcurrentUseMessage);

        await Task.Run(() => loader.Completion!.SetSource("export const value = 42;"));

        var module = await pending;
        module.Get("value").AsNumber().Should().Be(42);
        engine.Evaluate("1").AsNumber().Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsyncReportsInitialFailureThroughItsTask()
    {
        Task<Engine>? task = null;
        Action start = () => task = new Engine().ExecuteAsync("const =");

        Invoking(start)
            .Should().NotThrow();

        await Awaiting(() => task!).Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ImportAsyncReportsInitialFailureThroughItsTask()
    {
        var engine = new Engine(options => options.UseModules(new RejectingModuleLoader()));
        Task<Jint.Native.Object.ObjectInstance>? task = null;
        Action start = () => task = engine.Modules.ImportAsync("blocked");

        Invoking(start)
            .Should().NotThrow();

        await Awaiting(() => task!).Should().ThrowAsync<PromiseRejectedException>();
        engine.Evaluate("20 + 22").AsNumber().Should().Be(42);
    }

    [Fact]
    public async Task CanonicalJsCallDelegateCannotBypassOwnership()
    {
        Func<JsValue, JsValue[], JsValue>? callback = null;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engine = CreateBlockingEngine(entered, release);
        engine.SetValue("capture", new Action<Func<JsValue, JsValue[], JsValue>>(value => callback = value));
        engine.Execute("capture(() => 42)");
        var running = StartOwningThread(() => engine.Execute("block()"));

        await WaitUntilOwned(entered, running);
        try
        {
            Invoking(() => callback!(JsValue.Undefined, []))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
    }

    [Fact]
    public async Task SharedPreparedCallbackBinderUsesTheTargetFunctionsEngine()
    {
        var prepared = Engine.PrepareScript("capture(() => 42)");
        Func<int>? callbackA = null;
        Func<int>? callbackB = null;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engineA = CreateBlockingEngine(entered, release);
        var engineB = new Engine();
        engineA.SetValue("capture", new Action<Func<int>>(value => callbackA = value));
        engineB.SetValue("capture", new Action<Func<int>>(value => callbackB = value));
        engineA.Execute(prepared);
        engineB.Execute(prepared);
        var running = StartOwningThread(() => engineA.Execute("block()"));

        await WaitUntilOwned(entered, running);
        try
        {
            callbackB!().Should().Be(42);
            Invoking(() => callbackA!())
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
    }

    [Fact]
    public async Task DebuggerEvaluationCannotBypassOwnership()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engine = new Engine(options => options.Debugger.Enabled = true);
        engine.SetValue("block", new Action(() =>
        {
            entered.Set();
            release.Wait(HandoffCeiling);
        }));
        var running = StartOwningThread(() => engine.Execute("block()"));

        await WaitUntilOwned(entered, running);
        try
        {
            Invoking(() => engine.Debugger.Evaluate("1"))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
    }

    [Fact]
    public async Task BreakpointsCanBeAdministeredWhileTheEngineIsRunning()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engine = new Engine(options => options.Debugger.Enabled = true);
        engine.SetValue("block", new Action(() =>
        {
            entered.Set();
            release.Wait(HandoffCeiling);
        }));
        var running = StartOwningThread(() => engine.Execute("block()"));

        await WaitUntilOwned(entered, running);
        try
        {
            engine.Debugger.BreakPoints.Set(new BreakPoint(1, 0));
            engine.Debugger.BreakPoints.Count.Should().Be(1);
        }
        finally
        {
            release.Set();
        }

        await running;
    }

    [Fact]
    public async Task DisposeFailsFastWhileAnAsyncOperationOwnsTheEngine()
    {
        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new Engine();
        engine.SetValue("hostWork", new Func<Task<int>>(() => gate.Task));
        var pending = engine.EvaluateAsync("(async () => await hostWork())()");

        Invoking(engine.Dispose)
            .Should().Throw<InvalidOperationException>()
            .WithMessage(ConcurrentUseMessage);

        gate.SetResult(42);
        (await pending).AsNumber().Should().Be(42);
        engine.Dispose();
    }

    [Fact]
    public async Task ModuleImportPollingCannotRaceAnEngineTurn()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var loader = new DeferredModuleLoader();
        var engine = new Engine(options => options.UseModules(loader));
        engine.SetValue("block", new Action(() =>
        {
            entered.Set();
            release.Wait(HandoffCeiling);
        }));
        var operation = engine.Modules.StartImport("module");
        loader.Completion!.SetSource("block(); export const value = 42;");
        var running = StartOwningThread(engine.Tasks.ProcessTasks);

        await WaitUntilOwned(entered, running);
        try
        {
            Invoking(() => _ = operation.IsCompleted)
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
        operation.GetResult().Get("value").AsNumber().Should().Be(42);
    }

    /// <summary>
    /// A thread of the test's own, parked until <see cref="Release"/>, which then invokes the engine
    /// callback it was handed exactly once and records what came back.
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>Task.Run</c>. What the two tests above measure is <em>where</em> the invocation
    /// lands relative to the owner's turn, and a pool worker that has to be injected first lands wherever
    /// the pool feels like - which is the same wall-clock race <see cref="StartOwningThread"/> exists to
    /// avoid on the other side of the hand-off.
    /// </remarks>
    private sealed class CallbackDispatcher : IDisposable
    {
        private readonly ManualResetEventSlim _release = new();
        private readonly TaskCompletionSource<bool> _attempted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Thread _thread;

        internal CallbackDispatcher(Func<int> invoke)
        {
            _thread = new Thread(() =>
            {
                _release.Wait(HandoffCeiling);
                try
                {
                    Result = invoke();
                }
                catch (Exception exception)
                {
                    Failure = exception;
                }
                finally
                {
                    _attempted.TrySetResult(true);
                }
            })
            {
                IsBackground = true,
                Name = "jint-callback-dispatcher",
            };

            _thread.Start();
        }

        /// <summary>
        /// Completes once the callback has been invoked, whatever the outcome - so a script awaiting it
        /// makes progress on a refusal as well as on a success, and a regression reports rather than hangs.
        /// </summary>
        internal Task Attempted => _attempted.Task;

        internal Exception? Failure { get; private set; }

        internal int Result { get; private set; }

        internal void Release() => _release.Set();

        public void Dispose()
        {
            _thread.Join(HandoffCeiling);
            _release.Dispose();
        }
    }

    /// <summary>
    /// A JSON parse builds objects and arrays into the engine's realm for the whole document, so it is a
    /// host entry in the sense this contract means, and a second thread reaching it is refused.
    /// </summary>
    /// <remarks>
    /// It was the one conversion entry that was not. <c>JsonSerializer.Serialize</c> — its sibling, same
    /// namespace, same <c>(Engine)</c> constructor shape — has always been bracketed, so a host had no way to
    /// guess that one of the pair fails fast and the other builds concurrently into a busy engine. That
    /// asymmetry is what the next test pins from the other side.
    /// </remarks>
    [Fact]
    public async Task ConcurrentJsonParseIsRejectedAndTheEngineRecovers()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engine = CreateBlockingEngine(entered, release);
        var running = StartOwningThread(() => engine.Execute("block()"));

        await WaitUntilOwned(entered, running);
        try
        {
            Invoking(() => new JsonParser(engine).Parse("""{"a":[1,2,3],"b":{"c":"d"}}"""))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
        new JsonParser(engine).Parse("""{"a":1}""").AsObject().Get("a").AsNumber().Should().Be(1);
    }

    /// <summary>
    /// The span overloads take the same door. Both funnel through <c>Parse(ReadOnlySpan&lt;char&gt;)</c>, so
    /// one guard covers all three — which is worth pinning, because a host reaching for the allocation-free
    /// overload is exactly the host most likely to be doing so from a network or storage callback.
    /// </summary>
    [Fact]
    public async Task ConcurrentJsonParseIsRejectedForTheSpanOverloadsToo()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engine = CreateBlockingEngine(entered, release);
        var running = StartOwningThread(() => engine.Execute("block()"));

        await WaitUntilOwned(entered, running);
        try
        {
            Invoking(() => new JsonParser(engine).Parse("""{"a":1}""".AsSpan()))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);

            Invoking(() => new JsonParser(engine).Parse(Encoding.UTF8.GetBytes("""{"a":1}""").AsSpan()))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
    }

    /// <summary>
    /// The other half of the pair, so the two are pinned together and cannot drift apart again.
    /// </summary>
    [Fact]
    public async Task ConcurrentJsonSerializeIsRejected()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engine = CreateBlockingEngine(entered, release);
        var running = StartOwningThread(() => engine.Execute("block()"));

        await WaitUntilOwned(entered, running);
        try
        {
            Invoking(() => new JsonSerializer(engine).Serialize(JsNumber.Create(1)))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
    }

    private static Engine CreateBlockingEngine(ManualResetEventSlim entered, ManualResetEventSlim release)
    {
        var engine = new Engine();
        engine.SetValue("block", new Action(() =>
        {
            entered.Set();
            release.Wait(HandoffCeiling);
        }));
        return engine;
    }

    private sealed class DeferredModuleLoader : IAsyncModuleLoader
    {
        public ModuleLoadCompletion? Completion { get; private set; }

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The asynchronous path should be used.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
            Completion = completion;
        }
    }

    /// <summary>
    /// Parks inside <see cref="IModuleLoader.LoadModule"/> — the one point of a module load that is provably
    /// still inside the host entry that asked for it — until the test releases it.
    /// </summary>
    private sealed class BlockingModuleLoader(ManualResetEventSlim entered, ManualResetEventSlim release, TimeSpan ceiling) : IModuleLoader
    {
        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
        {
            entered.Set();
            release.Wait(ceiling);
            return ModuleFactory.BuildSourceTextModule(engine, Engine.PrepareModule("export const value = 42;", resolved.Key));
        }
    }

    private sealed class RejectingModuleLoader : IModuleLoader
    {
        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => throw new ModuleResolutionException("blocked", moduleRequest.Specifier, referencingModuleLocation, filePath: null);

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("Resolve should reject the import.");
    }

    private sealed class CallbackHost
    {
        public int OnOtherThread(Func<int, int> callback)
        {
            Exception? error = null;
            var result = 0;
            var thread = new Thread(() =>
            {
                try
                {
                    result = callback(41);
                }
                catch (Exception exception)
                {
                    error = exception;
                }
            });
            thread.Start();
            thread.Join();
            if (error is not null)
            {
                throw error;
            }

            return result;
        }

        public int OnOtherThreadFromArray(params Func<int>[] callbacks)
        {
            Exception? error = null;
            var result = 0;
            var thread = new Thread(() =>
            {
                try
                {
                    result = callbacks[0]();
                }
                catch (Exception exception)
                {
                    error = exception;
                }
            });
            thread.Start();
            thread.Join();
            if (error is not null)
            {
                throw error;
            }

            return result;
        }
    }
}
