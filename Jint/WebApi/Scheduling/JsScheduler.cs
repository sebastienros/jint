#if NET8_0_OR_GREATER
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Timers;

namespace Jint.WebApi.Scheduling;

/// <summary>
/// The <c>scheduler</c> object — the realm's one instance of the <c>Scheduler</c> interface, and the owner of
/// the queues its two operations put work on.
/// <para>
/// https://wicg.github.io/scheduling-apis/#sec-scheduler
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The split against <see cref="SchedulerPrototype"/> is the one WebIDL draws: the <i>members</i> are the
/// interface's and live on the prototype, while the queues a posted task lands on are state of this object.
/// The prototype's members brand-check their receiver and then operate on it, so an extracted
/// <c>postTask</c> is exactly as usable as a browser's.
/// </para>
/// <para>
/// Both queues belong to the <i>engine</i> rather than to this object, which is why they are read from
/// <c>Engine._webApi</c> once, here, instead of on every call: the task queue is the engine's event loop's,
/// and the timer queue a delayed <c>postTask</c> waits on is the very one <c>setTimeout</c> uses — so a
/// delayed task occupies one of the engine's timer slots while it waits.
/// </para>
/// </remarks>
internal sealed class JsScheduler : ObjectInstance
{
    private JsScheduler(Engine engine, SchedulerQueue tasks, TimerQueue timers) : base(engine, ObjectClass.Object)
    {
        Tasks = tasks;
        Timers = timers;
    }

    /// <summary>The prioritized task queues, https://wicg.github.io/scheduling-apis/#scheduler-task-queue.</summary>
    internal SchedulerQueue Tasks { get; }

    /// <summary>The engine's timer queue, which is what a <c>delay</c> waits on.</summary>
    internal TimerQueue Timers { get; }

    internal static JsScheduler Create(Engine engine, Realm realm)
    {
        var state = engine._webApi;
        if (state?.Scheduler is not { } tasks || state.Timers is not { } timers)
        {
            // Unreachable: the global that reaches this property is installed only where both queues were
            // created, in the same block of WebApiRegistration.
            Throw.InvalidOperationException("The scheduler object was reached on an engine that has no scheduler queue.");
            return null!;
        }

        return new JsScheduler(engine, tasks, timers)
        {
            _prototype = realm.Intrinsics.Scheduler.PrototypeObject,
        };
    }
}
#endif
