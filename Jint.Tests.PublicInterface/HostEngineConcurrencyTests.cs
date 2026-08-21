#nullable enable

using System.Diagnostics;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Debugger;
using Jint.Runtime.Modules;
using Xunit.Sdk;

using Module = Jint.Runtime.Modules.Module;

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
    /// no amount of runner load can lose the race and only a hang can spend two minutes here.
    /// </summary>
    private static readonly TimeSpan HandoffCeiling = TimeSpan.FromMinutes(2);

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

    [Fact]
    public void DelayedCrossThreadCallbackReleasesBlockingPromiseOwnership()
    {
        var engine = new Engine();
        engine.SetValue("later", new Action<Action>(callback =>
            Task.Delay(50).ContinueWith(_ => callback(), TaskScheduler.Default)));

        engine.Evaluate("new Promise(resolve => later(() => resolve(42)))")
            .UnwrapIfPromise()
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
        var engine = new Engine(options => options.EnableModules(loader));

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
        var engine = new Engine(options => options.EnableModules(new RejectingModuleLoader()));
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
        var engine = new Engine(options => options.EnableModules(loader));
        engine.SetValue("block", new Action(() =>
        {
            entered.Set();
            release.Wait(HandoffCeiling);
        }));
        var operation = engine.Modules.StartImport("module");
        loader.Completion!.SetSource("block(); export const value = 42;");
        var running = StartOwningThread(engine.Advanced.ProcessTasks);

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

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The asynchronous path should be used.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
            Completion = completion;
        }
    }

    private sealed class RejectingModuleLoader : IModuleLoader
    {
        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => throw new ModuleResolutionException("blocked", moduleRequest.Specifier, referencingModuleLocation, filePath: null);

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
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
