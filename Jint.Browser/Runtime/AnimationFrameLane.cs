using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Timers;

namespace Jint.Browser.Runtime;

/// <summary>
/// <c>requestAnimationFrame</c>: callbacks collected into a batch that the engine's timer queue fires roughly
/// sixty times a second.
/// </summary>
/// <remarks>
/// <para>
/// There is nothing to paint, so a frame is a scheduling concept only — but it is the scheduling concept a
/// page's animation, layout-measurement and "wait for the next tick" code is written against, and a page
/// whose <c>requestAnimationFrame</c> never fires simply stops. One timer entry per batch rather than one per
/// callback, so a page that requests a frame from inside a frame costs one timer, not one per callback.
/// </para>
/// <para>
/// A callback that throws is recorded and the rest of the batch still runs, which is what a browser does: the
/// callbacks of one frame are independent of each other — and each of them returns to a microtask checkpoint,
/// for the reason <see cref="Engine.CleanUpAfterRunningScript"/> gives.
/// </para>
/// <para>
/// The interval is fixed at 16 ms. A frame rate that tracked how long the callbacks took would be a rendering
/// decision, and there is no rendering to make it from.
/// </para>
/// </remarks>
internal sealed class AnimationFrameLane
{
    private const long FrameIntervalMilliseconds = 16;

    private readonly PageRuntime _runtime;
    private readonly List<Entry> _pending = [];
    private readonly HashSet<int> _cancelledDuringFrame = [];
    private int _nextId;
    private bool _scheduled;
    private bool _running;

    internal AnimationFrameLane(PageRuntime runtime)
    {
        _runtime = runtime;
    }

    /// <summary>Queues <paramref name="callback"/> for the next frame and answers its cancellation handle.</summary>
    internal int Request(ICallable callback)
    {
        var id = ++_nextId;
        _pending.Add(new Entry(id, callback));
        Schedule();
        return id;
    }

    /// <summary>Drops a queued callback. An unknown handle is ignored, as the specification requires.</summary>
    /// <remarks>
    /// A cancellation from inside the frame that is running counts too: HTML's "run the animation frame
    /// callbacks" skips an id cancelled by an earlier callback of the same frame, so the running batch is
    /// filtered as it goes rather than settled before it starts.
    /// </remarks>
    internal void Cancel(int id)
    {
        if (_running)
        {
            _cancelledDuringFrame.Add(id);
        }

        for (var i = 0; i < _pending.Count; i++)
        {
            if (_pending[i].Id == id)
            {
                _pending.RemoveAt(i);
                return;
            }
        }
    }

    private void Schedule()
    {
        if (_scheduled)
        {
            return;
        }

        var timers = _runtime.Engine._webApi?.Timers;
        if (timers is null)
        {
            // The timer feature is off, which a host can do through ConfigureEngine. Nothing can fire, so
            // nothing is queued; the callback stays pending rather than being dropped silently, and the next
            // request with timers on runs it.
            return;
        }

        // After the queue accepted it, not before: a schedule that threw — a page over the active-timer
        // limit, say — would otherwise leave the lane believing a frame is coming and never ask for another.
        timers.Schedule(new TimerEntry(
            timers,
            new Batch(this),
            [],
            FrameIntervalMilliseconds,
            repeat: false,
            _runtime.Engine.CaptureEventLoopRegistration()));

        _scheduled = true;
    }

    private void Run()
    {
        _scheduled = false;

        if (_pending.Count == 0)
        {
            return;
        }

        // A copy, because a callback requesting the next frame appends to the list it is being read from, and
        // the frame it requested is the next one rather than this one.
        var batch = _pending.ToArray();
        _pending.Clear();
        _cancelledDuringFrame.Clear();

        var global = _runtime.Engine._mainRealm.GlobalObject;
        JsValue[] arguments = [JsNumber.Create(_runtime.Now)];

        _running = true;

        try
        {
            foreach (var entry in batch)
            {
                if (_cancelledDuringFrame.Contains(entry.Id))
                {
                    continue;
                }

                try
                {
                    try
                    {
                        entry.Callback.Call(global, arguments);
                    }
                    finally
                    {
                        // HTML invokes an animation frame callback the way it invokes an event listener, so
                        // the same cleanup is owed: the whole batch is one job, and each callback therefore
                        // returns to an empty JavaScript execution context stack, which is a microtask
                        // checkpoint. Without it a reaction the first callback queued would run after the
                        // last callback of the frame.
                        // https://html.spec.whatwg.org/multipage/webappapis.html#clean-up-after-running-script
                        _runtime.Engine.CleanUpAfterRunningScript();
                    }
                }
                catch (JavaScriptException exception)
                {
                    _runtime.Recorder.Add(new PageError(
                        PageErrorKind.UncaughtCallbackError,
                        PageRecorder.Diagnostics.Describe(exception.Error, exception),
                        "AnimationFrame"));
                }
            }
        }
        finally
        {
            _running = false;
            _cancelledDuringFrame.Clear();
        }
    }

    private readonly record struct Entry(int Id, ICallable Callback);

    /// <summary>
    /// What the timer queue calls: one entry per frame, holding the lane rather than any one callback.
    /// </summary>
    private sealed class Batch : ICallable
    {
        private readonly AnimationFrameLane _lane;

        internal Batch(AnimationFrameLane lane)
        {
            _lane = lane;
        }

        public JsValue Call(JsValue thisObject, params JsValue[] arguments)
        {
            _lane.Run();
            return JsValue.Undefined;
        }
    }
}
