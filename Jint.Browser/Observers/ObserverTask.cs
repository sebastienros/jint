using Jint.Browser.Runtime;
using Jint.Native;
using Jint.WebApi.Timers;

namespace Jint.Browser.Observers;

/// <summary>
/// Queues work as a <em>task</em> rather than as a microtask: a zero-delay entry on the engine's timer queue.
/// </summary>
/// <remarks>
/// <para>
/// The intersection and resize observers deliver from the "update the rendering" steps, which are a task and
/// not a microtask — so a page that observes a target and then awaits a promise sees the promise settle
/// first, exactly as it does in a browser. A microtask would deliver too early and would let an observer that
/// re-observes from its own callback starve the queue.
/// </para>
/// <para>
/// The timer queue is the engine's only task queue, so a zero-delay entry is the whole of it. That also makes
/// the delivery visible to <c>Page.WaitForIdleAsync</c>, which is what lets a test await it without polling.
/// </para>
/// </remarks>
internal static class ObserverTask
{
    /// <summary>Runs <paramref name="work"/> on the page's own thread, in a later turn of the loop.</summary>
    internal static void Post(PageRuntime runtime, Action work)
    {
        var engine = runtime.Engine;
        var timers = engine._webApi?.Timers;

        if (timers is null)
        {
            // The timer feature is off, which a host can do through ConfigureEngine. The job queue is then
            // the only queue there is, so the delivery becomes a microtask: earlier than a browser's, and
            // still after the script that asked for it.
            engine.AddToEventLoop(work);
            return;
        }

        timers.Schedule(new TimerEntry(
            timers,
            new Job(work),
            [],
            requestedDelay: 0,
            repeat: false,
            engine.CaptureEventLoopRegistration()));
    }

    /// <summary>What the timer queue calls: a CLR action wearing the queue's callback interface.</summary>
    private sealed class Job : ICallable
    {
        private readonly Action _work;

        internal Job(Action work)
        {
            _work = work;
        }

        public JsValue Call(JsValue thisObject, params JsValue[] arguments)
        {
            _work();
            return JsValue.Undefined;
        }
    }
}
