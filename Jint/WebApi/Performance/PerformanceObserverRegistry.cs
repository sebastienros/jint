#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.Performance;

/// <summary>
/// https://w3c.github.io/performance-timeline/#dfn-registered-performance-observer — one observer plus the
/// <c>PerformanceObserverInit</c> dictionaries it is currently registered with.
/// </summary>
internal sealed class RegisteredPerformanceObserver(JsPerformanceObserver observer)
{
    /// <summary>The observer this registration delivers to.</summary>
    internal JsPerformanceObserver Observer { get; } = observer;

    /// <summary>
    /// The options list. One item for an <c>entryTypes</c> observer, which replaces it on every call; one per
    /// distinct <c>type</c> for a <c>type</c> observer, which stacks.
    /// </summary>
    internal List<PerformanceObserverOptions> Options { get; } = new();
}

/// <summary>
/// The engine's <i>list of registered performance observer objects</i> and its <i>performance observer task
/// queued flag</i>, plus the task that delivers to them.
/// <para>
/// https://w3c.github.io/performance-timeline/#queue-the-performance-observer-task
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>The registrations are engine state, and the entry buffer is not.</b> The specification hangs both off
/// the global; Jint splits them because a <c>RestoreGlobalSnapshot</c> has to treat them differently. The
/// entry buffer is data behind a restored binding and survives, which <see cref="JsPerformance"/> documents.
/// A registration is a live callback closing over the cycle that has ended — the same thing a global
/// <c>error</c> listener is — so it goes when that cycle does, and so does the queued flag, whose job the
/// generation fence has just thrown away and which would otherwise never be re-scheduled. That is why this
/// lives in <c>WebApiEngineState</c> and is reached through it.
/// </para>
/// <para>
/// <b>Delivery is a task, not a microtask, and the difference is what the re-queue below is for.</b> HTML
/// runs a task only once the microtask queue has drained, and Jint's single job queue <i>is</i> the microtask
/// queue — so a delivery job that finds anything queued behind it steps to the back of the line instead of
/// running the callbacks. Without that, <c>performance.mark('x'); Promise.resolve().then(f)</c> would call
/// the observer before <c>f</c>, where a browser calls <c>f</c> first. It converges for the reason
/// <c>SchedulerQueue.RunNextTask</c>'s does — each pass runs the jobs that were ahead of it — and a job queue
/// that never empties starves delivery exactly as it starves everything else.
/// </para>
/// </remarks>
internal sealed class PerformanceObserverRegistry(Engine engine)
{
    /// <summary>
    /// The shape of the callback's third argument when it carries a count, declared once so every such
    /// dictionary in an engine shares one hidden class.
    /// </summary>
    private static readonly JsObjectLayout _callbackOptionsLayout = JsObjectLayout.CreateBuilder()
        .Add("droppedEntriesCount")
        .Build();

    private readonly Engine _engine = engine;
    private readonly List<RegisteredPerformanceObserver> _observers = new();

    /// <summary>https://w3c.github.io/performance-timeline/#dfn-performance-observer-task-queued-flag.</summary>
    private bool _taskQueued;

    private Action? _deliverJob;

    /// <summary>
    /// The registration for <paramref name="observer"/>, or <see langword="null"/> when it has none — which is
    /// what <c>disconnect()</c> leaves behind and what an observer that has never observed starts with.
    /// </summary>
    internal RegisteredPerformanceObserver? Find(JsPerformanceObserver observer)
    {
        for (var i = 0; i < _observers.Count; i++)
        {
            if (ReferenceEquals(_observers[i].Observer, observer))
            {
                return _observers[i];
            }
        }

        return null;
    }

    /// <summary>Appends a registration for an observer that has none, and returns it.</summary>
    internal RegisteredPerformanceObserver Add(JsPerformanceObserver observer)
    {
        var registration = new RegisteredPerformanceObserver(observer);
        _observers.Add(registration);
        return registration;
    }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performanceobserver-disconnect, step 1: "Remove this
    /// from the list of registered performance observer objects".
    /// </summary>
    internal void Remove(JsPerformanceObserver observer)
    {
        for (var i = 0; i < _observers.Count; i++)
        {
            if (ReferenceEquals(_observers[i].Observer, observer))
            {
                _observers.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>
    /// Steps 4 to 8 of https://w3c.github.io/performance-timeline/#queue-a-performanceentry: give the entry to
    /// every observer interested in its type, and make sure a delivery task is on the loop.
    /// </summary>
    /// <remarks>
    /// The task is queued unconditionally, as the algorithm's last step is: an observer whose buffer is empty
    /// is skipped when the task runs, so a queue with nothing to deliver costs one job and no callback.
    /// </remarks>
    internal void QueuePerformanceEntry(JsPerformanceEntry entry)
    {
        var entryType = entry.EntryType.ToString();

        for (var i = 0; i < _observers.Count; i++)
        {
            var registration = _observers[i];
            var options = registration.Options;
            for (var j = 0; j < options.Count; j++)
            {
                if (options[j].Matches(entryType))
                {
                    registration.Observer.AppendToObserverBuffer(entry);
                    break;
                }
            }
        }

        QueueTask();
    }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#queue-the-performance-observer-task, steps 1 to 3.
    /// </summary>
    internal void QueueTask()
    {
        if (_taskQueued)
        {
            return;
        }

        _taskQueued = true;

        // The current generation: the flag and the job are set in one act, so there is no window in which the
        // cycle could have ended in between. No memory state, because a delivery turn runs whichever
        // callbacks are registered and those belong to whatever operation registered them — the same choice
        // the scheduler's pump job makes.
        _engine.AddToEventLoop(_deliverJob ??= Deliver, _engine.EventLoopGeneration, EventLoopJobKind.Task);
    }

    /// <summary>
    /// Forgets every registration and the queued flag. Called from
    /// <c>WebApiEngineState.ResetTransientState</c> — see the class remarks for why a registration does not
    /// survive a restore while the entry buffer does.
    /// </summary>
    internal void Clear()
    {
        _observers.Clear();

        // The delivery job still on the event loop belongs to the ended cycle and is dropped at dequeue by
        // the generation fence; clearing the flag is what lets the next cycle queue a fresh one.
        _taskQueued = false;
    }

    /// <summary>
    /// The delivery task, https://w3c.github.io/performance-timeline/#queue-the-performance-observer-task
    /// step 3.
    /// </summary>
    private void Deliver()
    {
        _taskQueued = false;

        // The microtask checkpoint; see the class remarks.
        if (_engine.HasPendingEventLoopJobs)
        {
            QueueTask();
            return;
        }

        if (_observers.Count == 0)
        {
            return;
        }

        // "Let notifyList be a copy of the list": a callback is free to observe, disconnect or construct
        // observers, and the walk must not see any of it.
        var notifyList = _observers.ToArray();

        foreach (var registration in notifyList)
        {
            var observer = registration.Observer;
            var entries = observer.TakeObserverBuffer();
            if (entries is null || entries.Count == 0)
            {
                // The step reads "If entries is empty, return", which would abandon every observer after the
                // first empty one. Every implementation continues instead, and the corpus requires it:
                // performance-timeline/multiple-buffered-flag-observers.any.js registers three observers and
                // waits for all of them, and its first is empty by the time the second is created.
                continue;
            }

            Invoke(registration, observer, entries);
        }
    }

    /// <summary>
    /// The last two steps: build the <c>PerformanceObserverEntryList</c> and the callback options, then invoke
    /// the callback with WebIDL's <c>"report"</c> exception behavior
    /// (https://webidl.spec.whatwg.org/#invoke-a-callback-function).
    /// </summary>
    /// <remarks>
    /// The fifth of the sites that turn a <c>JavaScriptException</c> escaping an engine-invoked callback into
    /// a <c>DiagnosticsSink</c> report — same shape, same reasons, and the same rule that only a
    /// <c>JavaScriptException</c> is caught, so a constraint still bounds. With no sink there is no catch at
    /// all and the throw erupts out of whatever is pumping; the observers behind it are delivered to either
    /// way only in the reported case, exactly as a throwing event listener leaves the listeners behind it.
    /// </remarks>
    private void Invoke(RegisteredPerformanceObserver registration, JsPerformanceObserver observer, List<JsPerformanceEntry> entries)
    {
        var realm = observer.Realm;
        var entryList = new JsPerformanceObserverEntryList(_engine, realm, entries)
        {
            _prototype = realm.Intrinsics.PerformanceObserverEntryList.PrototypeObject,
        };

        var callbackOptions = CreateCallbackOptions(registration, observer, realm);

        try
        {
            try
            {
                observer.Callback.Call(observer, [entryList, observer, callbackOptions]);
            }
            finally
            {
                // WebIDL's invoke a callback function ends in HTML's clean up after running script, which
                // performs a microtask checkpoint when the callback returned to an empty JavaScript execution
                // context stack — and the delivery is a job, so it always has. One delivery invokes every
                // observer whose buffer is non-empty, so without this a reaction the first callback queued
                // would wait behind every observer after it rather than running between the two, which is
                // the same defect #3733 fixed for two listeners of one dispatch. The inner finally is that
                // step's position: it runs whether the callback returned or threw, before the report below.
                _engine.CleanUpAfterRunningScript();
            }
        }
        catch (JavaScriptException exception) when (_engine._webApi?.Diagnostics is { } diagnostics)
        {
            _engine._webApi?.FireGlobalErrorEvent(exception);
            diagnostics.Report(DiagnosticEvent.ForUncaughtCallbackError(exception, DiagnosticCallbackSource.PerformanceObserver));
        }
    }

    /// <summary>
    /// The <c>PerformanceObserverCallbackOptions</c> third argument: a dictionary carrying
    /// <c>droppedEntriesCount</c> only while the observer's <i>requires dropped entries</i> is set, and the
    /// empty dictionary otherwise — which is what makes <c>options.droppedEntriesCount</c> read
    /// <c>undefined</c> on every callback after the first.
    /// </summary>
    private JsObject CreateCallbackOptions(RegisteredPerformanceObserver registration, JsPerformanceObserver observer, Realm realm)
    {
        if (!observer.RequiresDroppedEntries)
        {
            return new JsObject(_engine);
        }

        double dropped = 0;
        var options = registration.Options;
        for (var i = 0; i < options.Count; i++)
        {
            var item = options[i];
            if (item.Type is not null)
            {
                dropped += observer.Performance.DroppedEntriesCount(item.Type);
                continue;
            }

            var entryTypes = item.EntryTypes;
            if (entryTypes is null)
            {
                continue;
            }

            for (var j = 0; j < entryTypes.Length; j++)
            {
                dropped += observer.Performance.DroppedEntriesCount(entryTypes[j]);
            }
        }

        observer.RequiresDroppedEntries = false;

        return JsObject.Create(_engine, _callbackOptionsLayout, [JsNumber.Create(dropped)]);
    }
}
#endif
