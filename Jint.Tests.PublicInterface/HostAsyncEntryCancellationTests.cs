#nullable enable

using Jint.Constraints;
using Jint.Native;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins, from an embedder's side, that the token handed to an <c>*Async</c> entry is enough on its own to stop
/// the script that entry runs — the shape of a host whose <see cref="Options"/> are shared by every engine of
/// a tenant while its cancellation token is per request, so <c>ObserveCancellation</c>, fixed when the options
/// are built, cannot carry it.
/// </summary>
/// <remarks>
/// Before this held, such a host had to look up an <see cref="OperationDeadlineConstraint"/> it registered
/// through a per-engine factory for no other reason, arm it with <c>Begin(Timeout.InfiniteTimeSpan, token)</c>
/// around every call and <c>End()</c> it in a <c>finally</c>, and check the token up front itself. The last
/// test pins that a host which keeps that bracket is not broken by the engine now doing the same. Every
/// cancel is issued from the engine thread by a <see cref="ClrFunction"/>, and every loop is bounded, so a
/// build that ignores the token fails an assertion rather than hanging.
/// </remarks>
public class HostAsyncEntryCancellationTests
{
    private const string BoundedLoop = "var i = 0; cancel(); for (; i < 1000000; i++) { } i";

    [Test]
    public async Task ARequestTokenStopsTheScriptWithNothingRegisteredOnTheSharedOptions()
    {
        var shared = new Options();
        using var request = new CancellationTokenSource();
        var engine = new Engine(shared);
        engine.SetValue("cancel", Cancel(engine, request));

        var outcome = "completed";
        try
        {
            await engine.EvaluateAsync(BoundedLoop, cancellationToken: request.Token);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == request.Token)
        {
            outcome = "cancelled by the request";
        }

        outcome.Should().Be("cancelled by the request");
        engine.GetValue("i").AsNumber().Should().BeLessThan(1000);

        // The request's token lived for that call only: neither this engine nor another one built from the
        // same options observes it afterwards.
        engine.Evaluate("for (var k = 0; k < 100000; k++) { } k").Should().Be(100000);
        var sibling = new Engine(shared);
        (await sibling.EvaluateAsync("for (var k = 0; k < 100000; k++) { } k")).Should().Be(100000);
    }

    [Test]
    public async Task AnAlreadyCancelledRequestRunsNothing()
    {
        using var request = new CancellationTokenSource();
        request.Cancel();
        var engine = new Engine();
        var ran = false;
        engine.SetValue("touch", new ClrFunction(engine, "touch", (_, _) =>
        {
            ran = true;
            return JsValue.Undefined;
        }));

        var pending = engine.EvaluateAsync("touch(); 1", cancellationToken: request.Token);
        var exception = await Caught.ExceptionAsync(() => pending);

        pending.IsCanceled.Should().BeTrue();
        exception.Should().BeAssignableTo<OperationCanceledException>()
            .Which.CancellationToken.Should().Be(request.Token);
        ran.Should().BeFalse();
    }

    [Test]
    public async Task AnInvokedHandlerIsStoppedByItsRequestToken()
    {
        using var request = new CancellationTokenSource();
        var engine = new Engine();
        engine.SetValue("cancel", Cancel(engine, request));
        engine.Execute("var i = 0; function handle() { cancel(); for (; i < 1000000; i++) { } return i; }");

        var exception = await Caught.ExceptionAsync(() => engine.InvokeAsync("handle", request.Token));

        exception.Should().BeAssignableTo<OperationCanceledException>()
            .Which.CancellationToken.Should().Be(request.Token);
        engine.GetValue("i").AsNumber().Should().BeLessThan(1000);
    }

    [Test]
    public async Task AHostThatKeepsItsOperationDeadlineBracketStillGetsItsCancellation()
    {
        using var request = new CancellationTokenSource();
        var shared = new Options();
        shared.AddConstraint(() => new OperationDeadlineConstraint());
        var engine = new Engine(shared);
        engine.SetValue("cancel", Cancel(engine, request));
        var deadline = engine.Constraints.Find<OperationDeadlineConstraint>()!;

        Exception? exception;
        deadline.Begin(Timeout.InfiniteTimeSpan, request.Token);
        try
        {
            exception = await Caught.ExceptionAsync(() => engine.EvaluateAsync(BoundedLoop, cancellationToken: request.Token));
        }
        finally
        {
            deadline.End();
        }

        exception.Should().BeAssignableTo<OperationCanceledException>()
            .Which.CancellationToken.Should().Be(request.Token);
        engine.GetValue("i").AsNumber().Should().BeLessThan(1000);
    }

    /// <summary>
    /// A blocking <see cref="JsValue.UnwrapIfPromise()"/> reached from host code the script called is part of
    /// the call, so the call's token wakes it. What is asserted is how the wait itself ended, recorded where it
    /// returned: the host call boundary re-checks the token afterwards anyway, so the call's outcome alone
    /// cannot tell a woken wait from one that ran out <c>PromiseTimeout</c> — the wedge ceiling here.
    /// </summary>
    [Test]
    public async Task ANestedBlockingUnwrapIsWokenByTheCallsToken()
    {
        using var request = new CancellationTokenSource();
        var engine = new Engine(options => options.Constraints.PromiseTimeout = TestBudgets.WedgeCeiling);
        var wait = new WaitOutcome();
        engine.SetValue("block", new Func<JsValue, JsValue>(promise => wait.Record(() =>
        {
            // From another thread, once this one is already inside the wait.
            request.CancelAfter(TimeSpan.FromMilliseconds(50));
            return promise.UnwrapIfPromise();
        })));

        await Caught.ExceptionAsync(() => engine.EvaluateAsync("block(new Promise(() => { }))", cancellationToken: request.Token));

        wait.Failure.Should().BeAssignableTo<OperationCanceledException>()
            .Which.CancellationToken.Should().Be(request.Token);
        (await engine.EvaluateAsync("1")).Should().Be(1);
    }

    /// <summary>
    /// The same for a token that is already cancelled when the host code reaches the wait: the wait does not
    /// start at all.
    /// </summary>
    [Test]
    public async Task ANestedBlockingUnwrapAfterTheCallWasCancelledDoesNotWait()
    {
        using var request = new CancellationTokenSource();
        var engine = new Engine(options => options.Constraints.PromiseTimeout = TestBudgets.WedgeCeiling);
        var wait = new WaitOutcome();
        engine.SetValue("cancel", Cancel(engine, request));
        engine.SetValue("block", new Func<JsValue, JsValue>(promise => wait.Record(() => promise.UnwrapIfPromise())));

        var exception = await Caught.ExceptionAsync(() => engine.EvaluateAsync("cancel(); block(new Promise(() => { }))", cancellationToken: request.Token));

        wait.Failure.Should().BeAssignableTo<OperationCanceledException>()
            .Which.CancellationToken.Should().Be(request.Token);
        exception.Should().BeAssignableTo<OperationCanceledException>()
            .Which.CancellationToken.Should().Be(request.Token);
    }

    /// <summary>
    /// A blocking <c>Modules.Import</c> inside the call drains the same way, waiting on a loader that never
    /// answers.
    /// </summary>
    [Test]
    public async Task ANestedBlockingImportIsWokenByTheCallsToken()
    {
        using var request = new CancellationTokenSource();
        var loader = new NeverAnsweringLoader();
        var engine = new Engine(options =>
        {
            options.UseModules(loader);
            options.Constraints.PromiseTimeout = TestBudgets.WedgeCeiling;
        });
        var wait = new WaitOutcome();
        engine.SetValue("importBlocking", new Func<string, JsValue>(specifier => wait.Record<JsValue>(() =>
        {
            request.CancelAfter(TimeSpan.FromMilliseconds(50));
            return engine.Modules.Import(specifier);
        })));

        await Caught.ExceptionAsync(() => engine.EvaluateAsync("importBlocking('./never.js')", cancellationToken: request.Token));

        wait.Failure.Should().BeAssignableTo<OperationCanceledException>()
            .Which.CancellationToken.Should().Be(request.Token);
    }

    /// <summary>
    /// A park on <see cref="Engine.TaskOperations.WaitForScheduledWork"/> from host code inside the call is
    /// woken by the call's token too, rather than idling out its ceiling — the wedge ceiling here.
    /// </summary>
    [Test]
    public async Task ANestedParkIsWokenByTheCallsToken()
    {
        using var request = new CancellationTokenSource();
        var engine = new Engine();
        var wait = new WaitOutcome();
        engine.SetValue("cancel", Cancel(engine, request));
        engine.SetValue("park", new Func<bool>(() => wait.Record(() => engine.Tasks.WaitForScheduledWork(TestBudgets.WedgeCeiling))));

        await Caught.ExceptionAsync(() => engine.EvaluateAsync("cancel(); park()", cancellationToken: request.Token));

        wait.Failure.Should().BeAssignableTo<OperationCanceledException>()
            .Which.CancellationToken.Should().Be(request.Token);
    }

    /// <summary>
    /// <see cref="JsValue.UnwrapIfPromiseAsync"/> runs the continuations it waits for, so its token stops them
    /// the way an <c>EvaluateAsync</c> token stops the script.
    /// </summary>
    [Test]
    public async Task UnwrapIfPromiseAsyncStopsALoopingContinuation()
    {
        using var request = new CancellationTokenSource();
        var engine = new Engine();
        var io = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SetValue("io", new Func<Task<int>>(() => io.Task));
        engine.SetValue("cancel", Cancel(engine, request));

        var promise = engine.Evaluate("var i = 0; (async () => { await io(); cancel(); for (; i < 1000000; i++) { } return i; })()");
        var pending = promise.UnwrapIfPromiseAsync(request.Token);
        io.SetResult(1);

        var exception = await Caught.ExceptionAsync(() => pending);

        exception.Should().BeAssignableTo<OperationCanceledException>()
            .Which.CancellationToken.Should().Be(request.Token);
        engine.GetValue("i").AsNumber().Should().BeLessThan(1000);
    }

    /// <summary>
    /// <see cref="AsyncModuleLoader"/> hands its fetch a token the call's own token cancels, so one request
    /// token stops the script and the module loads it started — with nothing registered on the options.
    /// </summary>
    [Test]
    public async Task TheCallsTokenReachesTheLoadersFetch()
    {
        using var request = new CancellationTokenSource();
        var loader = new TokenCapturingLoader();
        var engine = new Engine(options => options.UseModules(loader));

        var import = engine.Modules.ImportAsync("./fetch.js", request.Token);
        request.Cancel();

        var exception = await Caught.ExceptionAsync(() => import);

        exception.Should().BeAssignableTo<OperationCanceledException>();
        loader.CapturedToken.Should().NotBeNull();
        loader.CapturedToken!.Value.IsCancellationRequested.Should().BeTrue("the fetch must be told the call was cancelled");
    }

    /// <summary>
    /// A loader that reports the call's cancellation through its completion stops the import with that
    /// cancellation, rather than turning it into a <c>Could not load module</c> rejection script could catch.
    /// </summary>
    [Test]
    public async Task ALoadersCancellationForTheCallsTokenPropagates()
    {
        using var request = new CancellationTokenSource();
        var loader = new CancellingLoader(request);
        var engine = new Engine(options => options.UseModules(loader));

        var exception = await Caught.ExceptionAsync(() => engine.Modules.ImportAsync("./aborted.js", request.Token));

        exception.Should().BeAssignableTo<OperationCanceledException>()
            .Which.CancellationToken.Should().Be(request.Token);
    }

    /// <summary>
    /// A cancelled call stops where it is, and what it had queued stays queued: the engine's next pump runs
    /// it. This is the behaviour the documentation tells a pooling host to clear.
    /// </summary>
    [Test]
    public async Task ACancelledCallLeavesItsQueuedJobsForTheNextPump()
    {
        using var request = new CancellationTokenSource();
        var engine = new Engine();
        engine.SetValue("cancel", Cancel(engine, request));

        await Caught.ExceptionAsync(() => engine.EvaluateAsync(
            "Promise.resolve().then(() => { globalThis.leftover = true; }); " + BoundedLoop,
            cancellationToken: request.Token));

        engine.Evaluate("typeof leftover").Should().Be("undefined");
        engine.Tasks.ProcessTasks();
        engine.Evaluate("leftover").Should().Be(true);
    }

    /// <summary>
    /// ...and restoring a global snapshot is what discards them, which is the remedy the documentation names.
    /// </summary>
    [Test]
    public async Task RestoringASnapshotDiscardsWhatACancelledCallQueued()
    {
        using var request = new CancellationTokenSource();
        var engine = new Engine();
        engine.SetValue("cancel", Cancel(engine, request));
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        await Caught.ExceptionAsync(() => engine.EvaluateAsync(
            "Promise.resolve().then(() => { globalThis.leftover = true; }); " + BoundedLoop,
            cancellationToken: request.Token));

        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("typeof leftover").Should().Be("undefined");
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// The settlement wait can be woken by the cancel itself while a turn that would settle the promise is
    /// already due. The cancel must win: the timer here is due on the engine's clock before the token is
    /// cancelled, and a timer coming due enqueues nothing, so the cancel is the only thing that wakes the wait.
    /// </summary>
    [Test]
    public async Task ACancelThatWakesTheWaitWinsOverATurnThatWouldSettle()
    {
        var clock = new ManualClock();
        using var request = new CancellationTokenSource();
        var engine = new Engine(options =>
        {
            options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock);
            options.Constraints.PromiseTimeout = TestBudgets.WedgeCeiling;
        });

        var pending = engine.EvaluateAsync(
            "new Promise(resolve => setTimeout(() => resolve('completed'), 600000))",
            cancellationToken: request.Token);
        pending.IsCompleted.Should().BeFalse("the timer is not due on the engine's clock");

        clock.Advance(600_000);
        request.Cancel();

        var exception = await Caught.ExceptionAsync(() => pending);

        exception.Should().BeAssignableTo<OperationCanceledException>()
            .Which.CancellationToken.Should().Be(request.Token);
    }

    /// <summary>
    /// The engine's timer clock, moved only by the test.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _timestamp);

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());

        internal void Advance(int milliseconds) => Interlocked.Add(ref _timestamp, milliseconds * TimeSpan.TicksPerMillisecond);
    }
#endif

    private static ClrFunction Cancel(Engine engine, CancellationTokenSource source)
        => new(engine, "cancel", (_, _) =>
        {
            source.Cancel();
            return JsValue.Undefined;
        });

    /// <summary>
    /// How a blocking wait made from host code ended, recorded before the host call boundary sees it.
    /// </summary>
    private sealed class WaitOutcome
    {
        public Exception? Failure { get; private set; }

        public T Record<T>(Func<T> wait)
        {
            try
            {
                return wait();
            }
            catch (Exception exception)
            {
                Failure = exception;
                throw;
            }
        }
    }

    /// <summary>
    /// Records the token the engine hands the fetch, which only ends when that token is cancelled.
    /// </summary>
    private sealed class TokenCapturingLoader : AsyncModuleLoader
    {
        public CancellationToken? CapturedToken { get; private set; }

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override async Task<string> LoadModuleContentsAsync(Engine engine, ResolvedSpecifier resolved, CancellationToken cancellationToken)
        {
            CapturedToken = cancellationToken;
            await Task.Delay(TestBudgets.WedgeCeiling, cancellationToken).ConfigureAwait(false);
            return string.Empty;
        }
    }

    /// <summary>
    /// A loader whose request is aborted while it loads: it cancels the host's request token, as a transport
    /// observing it would, and reports the cancellation through the completion.
    /// </summary>
    private sealed class CancellingLoader(CancellationTokenSource request) : IAsyncModuleLoader
    {
        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The engine must not take the synchronous path for an IAsyncModuleLoader.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
            request.Cancel();
            completion.SetError(new OperationCanceledException(request.Token));
        }
    }

    /// <summary>
    /// A loader that never answers, for a blocking import that can only end through cancellation.
    /// </summary>
    private sealed class NeverAnsweringLoader : IAsyncModuleLoader
    {
        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The engine must not take the synchronous path for an IAsyncModuleLoader.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
        }
    }
}
