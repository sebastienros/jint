#nullable enable

using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Debugger;
using Jint.Runtime.Modules;

using Module = Jint.Runtime.Modules.Module;

namespace Jint.Tests.PublicInterface;

public class HostEngineConcurrencyTests
{
    private const string ConcurrentUseMessage = "*already in use by another thread or has an asynchronous operation in progress*";

    [Fact]
    public async Task ConcurrentExecuteIsRejectedAndTheEngineRecovers()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engine = CreateBlockingEngine(entered, release);
        var running = Task.Run(() => engine.Execute("block()"));

        entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
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
        var running = Task.Run(() => engine.Evaluate("block()"));

        entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
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
        var running = Task.Run(() => engine.Invoke("waitForHost"));

        entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
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
        var running = Task.Run(() => engine.Execute("block()"));

        entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
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
        var running = Task.Run(() => engine.Execute("block(); globalThis.finished = true"));

        entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
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
    public async Task CanonicalJsCallDelegateCannotBypassOwnership()
    {
        Func<JsValue, JsValue[], JsValue>? callback = null;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engine = CreateBlockingEngine(entered, release);
        engine.SetValue("capture", new Action<Func<JsValue, JsValue[], JsValue>>(value => callback = value));
        engine.Execute("capture(() => 42)");
        var running = Task.Run(() => engine.Execute("block()"));

        entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
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
        var running = Task.Run(() => engineA.Execute("block()"));

        entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
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
            release.Wait();
        }));
        var running = Task.Run(() => engine.Execute("block()"));

        entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
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
            release.Wait();
        }));
        var running = Task.Run(() => engine.Execute("block()"));

        entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
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
            release.Wait();
        }));
        var operation = engine.Modules.StartImport("module");
        loader.Completion!.SetSource("block(); export const value = 42;");
        var running = Task.Run(engine.Advanced.ProcessTasks);

        entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
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
            release.Wait();
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
}
