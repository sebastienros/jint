using Jint.DevTools;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// Releasing a runtime whose engine somebody else already disposed, which is the ordinary case for a page:
/// the loop disposes the outgoing engine and the target learns of the swap afterwards.
/// </summary>
/// <remarks>
/// <para>
/// Every release step used to sit inside a blanket <c>catch (Exception)</c>, because there was no way to ask
/// an engine whether it was gone. <see cref="Engine.IsDisposed"/> is that question
/// (https://github.com/sebastienros/jint/issues/3684), so the two steps that reach into the engine are
/// skipped and the rest run unguarded — a failure in one is now a bug that reports itself.
/// </para>
/// <para>
/// Every wait is bounded, as everywhere else in this suite.
/// </para>
/// </remarks>
[NonParallelizable]
public class TargetRuntimeDisposalTests
{
    [Test]
    public async Task ATargetWhoseEngineWasDisposedUnderneathItStillReleasesCleanly()
    {
        var engine = new Engine(options => options.UseDevTools());
        var target = new NavigableTarget(engine);

        // Whoever owns the engine disposes it, on the thread that owns it, without telling the target.
        await target.PostAsync(e => e.Dispose()).WaitAsync(TimeSpan.FromSeconds(30));

        engine.IsDisposed.Should().BeTrue();

        var runtime = target.Runtime;
        runtime.IsDisposed.Should().BeFalse("the target has not been told anything yet");

        // No blanket catch stands between a release step and this assertion any more, so a step that reached
        // into the dead engine would report itself here rather than being swallowed.
        var closing = target.CloseAsync().AsTask();
        (await Task.WhenAny(closing, Task.Delay(TimeSpan.FromSeconds(30)))).Should().BeSameAs(closing);
        await closing;

        runtime.IsDisposed.Should().BeTrue();

        // Closing a target is idempotent, and so is releasing its runtime.
        await target.CloseAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
    }

    [Test]
    public async Task ARuntimeOverALiveEngineIsReleasedTheSameWay()
    {
        var engine = new Engine(options => options.UseDevTools());
        var target = new NavigableTarget(engine);

        await target.PostAsync(e => e.Evaluate("1 + 1")).WaitAsync(TimeSpan.FromSeconds(30));

        var runtime = target.Runtime;
        engine.IsDisposed.Should().BeFalse();

        await target.CloseAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));

        runtime.IsDisposed.Should().BeTrue();

        engine.Dispose();
    }
}
