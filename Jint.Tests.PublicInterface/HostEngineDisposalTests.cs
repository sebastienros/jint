#nullable enable

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The seam a host holding a reference to somebody else's engine needs: whether that engine is gone
/// (<see cref="Engine.IsDisposed"/>) and a signal that it is going (<see cref="Engine.Disposed"/>).
/// </summary>
/// <remarks>
/// The shape it exists for is a component that subscribes to an engine it does not own — a rejection
/// tracker, a console sink, a debugger — and has to let go of it. Without the two members every release step
/// was a <c>try</c> around a call that might reach a disposed engine, which is guessing rather than asking
/// (https://github.com/sebastienros/jint/issues/3684).
/// </remarks>
public class HostEngineDisposalTests
{
    [Test]
    public void AnEngineReportsItselfLiveUntilItIsDisposed()
    {
        var engine = new Engine();

        engine.IsDisposed.Should().BeFalse();

        engine.Dispose();

        engine.IsDisposed.Should().BeTrue();
    }

    /// <summary>
    /// The event says "this engine is going away", not "it might": the state has already flipped when a
    /// handler runs, so a handler that consults it is never told the engine is live.
    /// </summary>
    [Test]
    public void DisposedFiresOnceWithTheEngineAlreadyReportingItself()
    {
        var engine = new Engine();
        var raised = 0;
        var seen = (bool?) null;

        engine.Disposed += (_, _) =>
        {
            raised++;
            seen = engine.IsDisposed;
        };

        engine.Dispose();
        engine.Dispose();

        raised.Should().Be(1);
        seen.Should().BeTrue();
    }

    /// <summary>
    /// What a subscriber actually does with it: drop its hold on an engine whose owner disposed it, without
    /// a <c>catch</c> around every unsubscription.
    /// </summary>
    [Test]
    public void ASubscriberCanReleaseItsHoldFromTheEvent()
    {
        var engine = new Engine();
        var released = false;

        void OnRejection(object? sender, PromiseRejectionTrackerEventArgs args)
        {
        }

        engine.Tasks.PromiseRejectionTracker += OnRejection;
        engine.Disposed += (_, _) =>
        {
            engine.Tasks.PromiseRejectionTracker -= OnRejection;
            released = true;
        };

        Invoking(engine.Dispose).Should().NotThrow();

        released.Should().BeTrue();
    }

    /// <summary>
    /// A release step that fails does not leave the engine half-disposed, and the failure is not swallowed:
    /// the caller of <see cref="Engine.Dispose"/> sees it.
    /// </summary>
    [Test]
    public void AHandlerThatThrowsStillLeavesTheEngineDisposed()
    {
        var engine = new Engine();
        engine.Disposed += (_, _) => throw new InvalidOperationException("subscriber");

        Invoking(engine.Dispose).Should().Throw<InvalidOperationException>().WithMessage("subscriber");

        engine.IsDisposed.Should().BeTrue();
        Invoking(engine.Dispose).Should().NotThrow();
    }

    /// <summary>
    /// The event runs on the disposing thread rather than on the one that last ran script, which is what a
    /// handler needing the engine's thread has to marshal for itself.
    /// </summary>
    [Test]
    public void DisposedRunsOnTheThreadThatDisposes()
    {
        var engine = new Engine();
        engine.Evaluate("var x = 1;");

        var observed = 0;
        engine.Disposed += (_, _) => Volatile.Write(ref observed, Environment.CurrentManagedThreadId);

        var thread = new Thread(engine.Dispose);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(30)).Should().BeTrue();

        Volatile.Read(ref observed).Should().Be(thread.ManagedThreadId);
    }
}
