#nullable enable

using System.Reflection;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class EngineRetirementTests
{
    // The lifecycle monitor is private and has no accessor of its own; reading it by reflection keeps the
    // product surface unchanged for what is purely a test concern.
    private static readonly FieldInfo _lifecycleLockField =
        typeof(Engine).GetField("_lifecycleLock", BindingFlags.Instance | BindingFlags.NonPublic)!;

    // An open-instance delegate over the private getter, so a racer's call costs what the engine's own call
    // does; reflection's per-call overhead would spread the two first uses too far apart to ever meet.
    private static readonly Func<Engine, object> _lifecycleLockOf = (Func<Engine, object>) Delegate.CreateDelegate(
        typeof(Func<Engine, object>),
        typeof(Engine).GetProperty("LifecycleLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetMethod!);

    [Test]
    public void RetirementFencesBothTaskAndMicrotaskQueues()
    {
        using var engine = new Engine();
        engine.Tasks.ConfigureTaskBudget(new TaskBudget());
        var calls = 0;
        engine.SetValue("retire", new Action(() => engine.Advanced.Retire()));
        engine.SetValue("record", new Action(() => calls++));
        engine.Tasks.Post(() => engine.Execute("Promise.resolve().then(() => retire()); Promise.resolve().then(() => record())"));
        engine.Tasks.Post(() => engine.Execute("record()"));
        engine.Tasks.ProcessTasks();
        calls.Should().Be(0);
    }

    /// <summary>
    /// Construction is the cost every host pays, and most engines are never retired, so the monitor
    /// <c>Retire</c> and <c>Dispose</c> serialize on is created by the first of them to need it.
    /// </summary>
    [Test]
    public void TheLifecycleMonitorIsCreatedByTheFirstLifecycleCallNotByTheConstructor()
    {
        var disposed = new Engine();
        disposed.Execute("var x = 1; Promise.resolve().then(() => x++);");
        _lifecycleLockField.GetValue(disposed).Should().BeNull();
        disposed.Dispose();
        _lifecycleLockField.GetValue(disposed).Should().NotBeNull();

        using var retired = new Engine();
        _lifecycleLockField.GetValue(retired).Should().BeNull();
        retired.Advanced.Retire();
        _lifecycleLockField.GetValue(retired).Should().NotBeNull();
    }

    /// <summary>
    /// Retire and Dispose may reach the monitor from two threads on an engine that has never created it. Each
    /// trial releases both first uses at once; the publication must hand them one object, or neither call is
    /// serialized against the other.
    /// </summary>
    /// <remarks>
    /// Both racers spin, rather than block, until the test thread releases them: a blocked waiter wakes
    /// microseconds late, and the window a non-atomic publication loses in is a few nanoseconds wide.
    /// </remarks>
    [Test]
    public async Task TwoThreadsRacingTheFirstUseOfTheLifecycleMonitorLockTheSameObject()
    {
        for (var trial = 0; trial < 200; trial++)
        {
            var engine = new Engine();
            var start = new SpinStart();
            object? first = null;
            object? second = null;

            var firstRacer = DedicatedThread.RunAsync(() =>
            {
                start.ArriveAndSpin();
                first = _lifecycleLockOf(engine);
            });
            var secondRacer = DedicatedThread.RunAsync(() =>
            {
                start.ArriveAndSpin();
                second = _lifecycleLockOf(engine);
            });
            start.ReleaseWhenBothArrived();
            await Task.WhenAll(firstRacer, secondRacer);

            first.Should().NotBeNull();
            second.Should().BeSameAs(first, $"trial {trial} must publish one monitor");
            _lifecycleLockField.GetValue(engine).Should().BeSameAs(first);
        }
    }

    private sealed class SpinStart
    {
        private int _arrived;
        private int _released;

        public void ArriveAndSpin()
        {
            Interlocked.Increment(ref _arrived);
            var deadline = DateTime.UtcNow + TestBudgets.WedgeCeiling;
            while (Volatile.Read(ref _released) == 0)
            {
                Thread.SpinWait(8);
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("the racers were never released");
                }
            }
        }

        public void ReleaseWhenBothArrived()
        {
            SpinWait.SpinUntil(() => Volatile.Read(ref _arrived) == 2, TestBudgets.WedgeCeiling).Should().BeTrue();
            Volatile.Write(ref _released, 1);
        }
    }

    private sealed class TaskBudget : IEventLoopTaskBudget
    {
        public bool BeginTask(bool isTask) => true;
        public void EndTask() { }
    }
}
