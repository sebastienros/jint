using Jint.DevTools;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// Which thread runs a target's engine, and what a client is told when nothing does.
/// </summary>
public class ThreadModeTests
{
    /// <summary>
    /// What a wait is allowed to take before the test calls it a hang. Generous on purpose — see
    /// <see cref="Transport.DevToolsClient"/> for why a tight bound here measures the runner, not the server.
    /// </summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(120);

    /// <summary>
    /// The diagnostic a host that forgot to pump needs. Everything else about a host-owned target looks
    /// exactly like a slow one, so the message is what tells the two apart.
    /// </summary>
    [Test]
    public async Task ACommandOnAnUnpumpedHostOwnedTargetSaysTheEngineIsNotBeingPumped()
    {
        await using var session = ProtocolSession.Create(options: new DevToolsServerOptions
        {
            CommandTimeout = TimeSpan.FromMilliseconds(300),
        });

        var target = session.AddTarget();
        target.ThreadMode.Should().Be(ThreadMode.HostOwned, "that is the default, and the shape most embedders are already in");

        var sessionId = await session.AttachAsync(target);
        var error = (await session.SendAsync("Runtime.getIsolateId", sessionId: sessionId)).GetProperty("error");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Engine is not being pumped");
        error.GetProperty("data").GetString().Should().Contain("ProcessTasks");
    }

    /// <summary>
    /// A command that timed out is still queued, so the host's next turn answers it — the timeout bounds the
    /// client's wait rather than cancelling the work.
    /// </summary>
    [Test]
    public async Task AHostOwnedTargetAnswersOnceItIsPumped()
    {
        await using var session = ProtocolSession.Create();
        var target = session.AddTarget();
        var sessionId = await session.AttachAsync(target);

        var pending = session.SendAsync("Runtime.evaluate", """{"expression":"6*7","returnByValue":true}""", sessionId);

        var deadline = DateTime.UtcNow + Bound;
        while (!pending.IsCompleted && DateTime.UtcNow < deadline)
        {
            target.Pump();
            await Task.Delay(5);
        }

        var reply = await pending.WaitAsync(Bound);
        reply.GetProperty("result").GetProperty("result").GetProperty("value").GetInt32().Should().Be(42);
    }

    [Test]
    public async Task PumpIsRefusedOnALibraryOwnedTarget()
    {
        await using var target = new EngineTarget(new Engine(), new EngineTargetOptions { ThreadMode = ThreadMode.LibraryOwned });

        var thrown = Assert.Throws<InvalidOperationException>(() => target.Pump());
        thrown!.Message.Should().Contain("pumps itself");
    }

    [Test]
    public async Task PostAsyncRunsHostWorkOnTheEnginesOwnThread()
    {
        await using var target = new EngineTarget(new Engine(), new EngineTargetOptions { ThreadMode = ThreadMode.LibraryOwned });

        var answer = await target.PostAsync(engine => engine.Evaluate("1 + 1").AsNumber()).WaitAsync(Bound);
        answer.Should().Be(2);

        var thread = await target.PostAsync(_ => Environment.CurrentManagedThreadId).WaitAsync(Bound);
        thread.Should().NotBe(Environment.CurrentManagedThreadId, "the whole point of the mode is that the library owns the thread");
    }

    [Test]
    public async Task PostAsyncSurfacesWhatTheWorkThrew()
    {
        await using var target = new EngineTarget(new Engine(), new EngineTargetOptions { ThreadMode = ThreadMode.LibraryOwned });

        var thrown = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await target.PostAsync(_ => throw new InvalidOperationException("from the host")).WaitAsync(Bound));

        thrown!.Message.Should().Be("from the host");
    }

    /// <summary>
    /// A library-owned target that has to wait for a debugger holds host work and answers protocol commands,
    /// which is the only arrangement in which the command that ends the wait can ever be answered.
    /// </summary>
    [Test]
    public async Task WaitForDebuggerOnStartHoldsHostWorkUntilAClientReleasesIt()
    {
        await using var session = ProtocolSession.Create();
        var target = session.AddTarget(new EngineTargetOptions
        {
            ThreadMode = ThreadMode.LibraryOwned,
            WaitForDebuggerOnStart = true,
        });

        var ran = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        target.Post(_ => ran.TrySetResult(1));

        target.IsWaitingForDebugger.Should().BeTrue();
        await Task.Delay(150);
        ran.Task.IsCompleted.Should().BeFalse("the first posted work is held until a client says to run it");

        var sessionId = await session.AttachAsync(target);
        session.EventsOf("Target.attachedToTarget")[0]
            .GetProperty("params").GetProperty("waitingForDebugger").GetBoolean()
            .Should().BeFalse("attachToTarget carries no waitForDebuggerOnStart flag; only auto-attach asks for one");

        var released = await session.SendAsync("Runtime.runIfWaitingForDebugger", sessionId: sessionId);
        released.GetProperty("result").GetRawText().Should().Be("{}");

        (await ran.Task.WaitAsync(Bound)).Should().Be(1);
        target.IsWaitingForDebugger.Should().BeFalse();
    }

    /// <summary>
    /// A host-owned target waits by pumping, because the command that ends the wait is answered on the very
    /// thread that is waiting.
    /// </summary>
    [Test]
    public async Task WaitForDebuggerPumpsSoThatTheReleaseCanArrive()
    {
        await using var session = ProtocolSession.Create();
        var target = session.AddTarget(new EngineTargetOptions { WaitForDebuggerOnStart = true });
        var sessionId = await session.AttachAsync(target);

        var waiting = Task.Run(() => target.WaitForDebugger(Bound));

        var released = await session.SendAsync("Runtime.runIfWaitingForDebugger", sessionId: sessionId);
        released.TryGetProperty("error", out _).Should().BeFalse();

        (await waiting.WaitAsync(Bound)).Should().BeTrue();
        target.IsWaitingForDebugger.Should().BeFalse();
    }

    [Test]
    public async Task WaitForDebuggerGivesUpWhenNobodyEverConnects()
    {
        await using var target = new EngineTarget(new Engine(), new EngineTargetOptions { WaitForDebuggerOnStart = true });

        var waited = await Task.Run(() => target.WaitForDebugger(TimeSpan.FromMilliseconds(200)));

        waited.Should().BeFalse("a host that would otherwise block forever needs the bound it passed to mean something");
        target.IsWaitingForDebugger.Should().BeTrue();
    }

    [Test]
    public async Task AutoAttachReportsWaitingForDebuggerWhenBothSidesAskedForIt()
    {
        await using var session = ProtocolSession.Create();
        session.AddTarget(new EngineTargetOptions
        {
            ThreadMode = ThreadMode.LibraryOwned,
            WaitForDebuggerOnStart = true,
        });

        await session.SendAsync(
            "Target.setAutoAttach",
            """{"autoAttach":true,"waitForDebuggerOnStart":true,"flatten":true}""");

        session.EventsOf("Target.attachedToTarget")[0]
            .GetProperty("params").GetProperty("waitingForDebugger").GetBoolean()
            .Should().BeTrue();
    }

    [Test]
    public async Task ATargetThatIsNotHoldingAnythingIsNotReportedAsWaiting()
    {
        await using var session = ProtocolSession.Create();
        session.AddTarget(new EngineTargetOptions { ThreadMode = ThreadMode.LibraryOwned });

        await session.SendAsync(
            "Target.setAutoAttach",
            """{"autoAttach":true,"waitForDebuggerOnStart":true,"flatten":true}""");

        session.EventsOf("Target.attachedToTarget")[0]
            .GetProperty("params").GetProperty("waitingForDebugger").GetBoolean()
            .Should().BeFalse();
    }

    [Test]
    public async Task ADisposedTargetRefusesEverything()
    {
        var target = new EngineTarget(new Engine(), new EngineTargetOptions { ThreadMode = ThreadMode.LibraryOwned });
        await target.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => target.Post(_ => { }));
        Assert.Throws<ObjectDisposedException>(() => target.Pump());
    }
}
