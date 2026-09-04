#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Engine.Dispose</c> is a signal a host can subscribe to and a state it can ask about, rather than
/// something only discoverable by catching whatever the next call happens to throw:
/// https://github.com/sebastienros/jint/issues/3684.
/// </summary>
public class EngineDisposalSignalTests
{
    [Test]
    public void AFreshEngineIsNotDisposed()
    {
        using var engine = new Engine();

        engine.IsDisposed.Should().BeFalse();
    }

    [Test]
    public void DisposedFiresOnceAndTheEngineAlreadyReportsItself()
    {
        var engine = new Engine();
        var raised = 0;
        var seen = (bool?) null;
        object? sender = null;

        engine.Disposed += (s, _) =>
        {
            raised++;
            sender = s;
            seen = engine.IsDisposed;
        };

        engine.Dispose();

        raised.Should().Be(1);
        seen.Should().BeTrue("the event says the engine is going away, not that it might");
        sender.Should().BeSameAs(engine);
        engine.IsDisposed.Should().BeTrue();
    }

    [Test]
    public void ASecondDisposeRaisesNothing()
    {
        var engine = new Engine();
        var raised = 0;
        engine.Disposed += (_, _) => raised++;

        engine.Dispose();
        engine.Dispose();
        engine.Dispose();

        raised.Should().Be(1);
    }

    [Test]
    public void ADisposeThatRunsNoHandlerIsStillIdempotent()
    {
        var engine = new Engine();

        var exception = Caught.Exception(() =>
        {
            engine.Dispose();
            engine.Dispose();
        });

        exception.Should().BeNull();
    }

    [Test]
    public void AHandlerThatThrowsStillLeavesTheEngineReleasedAndReachesTheCaller()
    {
        var engine = new Engine();
        engine.Disposed += (_, _) => throw new InvalidOperationException("handler");

        var exception = Caught.Exception(engine.Dispose);

        exception.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("handler");
        engine.IsDisposed.Should().BeTrue();

        // The release ran in the finally, so the engine is not left half-disposed waiting for a second call
        // that would raise the same handler again.
        var second = Caught.Exception(engine.Dispose);
        second.Should().BeNull();
    }

    [Test]
    public void PostOnALiveEngineStillQueuesAndRuns()
    {
        using var engine = new Engine();
        var ran = false;

        engine.Tasks.Post(() => ran = true);
        ran.Should().BeFalse("a post is queued, never run on the caller's thread");

        engine.Tasks.ProcessTasks();
        ran.Should().BeTrue();
    }

    [Test]
    public void PostOnADisposedEngineIsRefusedByName()
    {
        var engine = new Engine();
        engine.Dispose();

        var exception = Caught.Exception(() => engine.Tasks.Post(() => { }));

        exception.Should().BeOfType<ObjectDisposedException>()
            .Which.ObjectName.Should().Be(nameof(Engine));
    }

    [Test]
    public void ADisposedEngineRunsNothingThatWasRefused()
    {
        var engine = new Engine();
        var tasks = engine.Tasks;
        engine.Dispose();

        var ran = false;
        Caught.Exception(() => tasks.Post(() => ran = true));

        tasks.ProcessTasks();
        ran.Should().BeFalse();
    }

    [Test]
    public void AHandlerCanUnsubscribeFromTheEngineItIsToldAboutFromAnotherThread()
    {
        var engine = new Engine();
        var disposingThread = 0;

        engine.Disposed += (_, _) => disposingThread = Environment.CurrentManagedThreadId;

        var thread = new Thread(engine.Dispose);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(10)).Should().BeTrue();

        disposingThread.Should().Be(thread.ManagedThreadId, "the event runs on whichever thread calls Dispose");
        engine.IsDisposed.Should().BeTrue();
    }
}
