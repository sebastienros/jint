#if NET8_0_OR_GREATER
using Jint.Runtime;
using Jint.WebApi.Abort;
using Jint.WebApi.DomException;

namespace Jint.WebApi.Scheduling;

/// <summary>
/// A <c>TaskSignal</c> instance: an <see cref="JsAbortSignal"/> that also carries a
/// <see cref="SchedulerTaskPriority"/> the tasks scheduled with it follow.
/// <para>
/// https://wicg.github.io/scheduling-apis/#tasksignal
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Everything an <c>AbortSignal</c> does it does here unchanged — a <c>TaskSignal</c> is accepted wherever one
/// is, which is the whole reason the specification made it a subclass. What it adds is the priority half:
/// <see cref="SignalPriorityChange"/> is what <c>TaskController.setPriority()</c> runs, and a scheduler
/// registers a <i>priority change algorithm</i> so that the queue holding this signal's pending tasks is
/// re-prioritized without the tasks moving.
/// </para>
/// <para>
/// The <i>source signal</i> and <i>dependent signals</i> of the priority graph are strong references here, the
/// same deliberate divergence <see cref="JsAbortSignal"/> documents for the abort graph: the specification
/// makes them weak and adds a "must not be garbage collected while it has listeners" rule
/// (https://wicg.github.io/scheduling-apis/#sec-task-signal-garbage-collection) that .NET cannot express as
/// cheaply as a browser's collector can. A composite built by <c>TaskSignal.any()</c> is therefore retained by
/// the signal its priority follows.
/// </para>
/// </remarks>
internal sealed class JsTaskSignal : JsAbortSignal
{
    /// <summary>The event type a priority change fires, and the one <c>onprioritychange</c> handles.</summary>
    internal const string PriorityChangeEventType = "prioritychange";

    private List<Action>? _priorityChangeAlgorithms;
    private List<JsTaskSignal>? _dependentSignals;
    private JsTaskSignal? _sourceSignal;

    internal JsTaskSignal(Engine engine, Realm realm, SchedulerTaskPriority priority) : base(engine, realm)
    {
        Priority = priority;
    }

    /// <summary>https://wicg.github.io/scheduling-apis/#tasksignal-priority.</summary>
    internal SchedulerTaskPriority Priority { get; private set; }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#tasksignal-priority-changing. Guards
    /// <see cref="SignalPriorityChange"/> against a <c>prioritychange</c> listener that changes the priority
    /// again, which the specification makes a <c>NotAllowedError</c> rather than a recursion.
    /// </summary>
    private bool PriorityChanging { get; set; }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#tasksignal-has-fixed-priority — "a TaskSignal has fixed priority
    /// if it is a dependent signal with a null source signal". A scheduler reads it to decide whether the tasks
    /// this signal governs need a queue of their own, or can share the queue of everything else at that
    /// priority.
    /// </summary>
    internal bool HasFixedPriority => Dependent && _sourceSignal is null;

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#tasksignal-add-a-priority-change-algorithm.
    /// </summary>
    internal void AddPriorityChangeAlgorithm(Action algorithm)
    {
        (_priorityChangeAlgorithms ??= new List<Action>()).Add(algorithm);
    }

    /// <summary>
    /// Takes an algorithm off again. The specification has no such operation — its scheduler task queues are
    /// garbage collected instead — but a queue that empties is dropped here, and leaving its algorithm behind
    /// would make a long-lived signal retain every queue ever built from it.
    /// </summary>
    internal void RemovePriorityChangeAlgorithm(Action algorithm) => _priorityChangeAlgorithms?.Remove(algorithm);

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#tasksignal-signal-priority-change, the algorithm behind
    /// <c>TaskController.setPriority()</c>.
    /// </summary>
    /// <remarks>
    /// One deliberate difference from the numbered steps: the <i>priority changing</i> flag is cleared in a
    /// <c>finally</c> rather than as step 9. A <c>prioritychange</c> listener that throws erupts from here —
    /// which is what every event listener in this engine does, see <see cref="Events.JsEventTarget"/> — and
    /// leaving the flag set would make the signal refuse every later priority change with a
    /// <c>NotAllowedError</c>, which is not a state the specification can reach.
    /// </remarks>
    internal void SignalPriorityChange(SchedulerTaskPriority priority)
    {
        // Step 1.
        if (PriorityChanging)
        {
            var exception = _realm.Intrinsics.DomException.CreateException(
                DomExceptionNames.NotAllowed,
                "Failed to execute 'setPriority' on 'TaskController': the signal's priority is already changing.");

            var location = _engine._lastSyntaxElement?.Location ?? default;
            Throw.JavaScriptException(_engine, exception, in location);
        }

        // Step 2: setting the priority a signal already has is not a change, so it fires nothing.
        if (Priority == priority)
        {
            return;
        }

        // Steps 3 to 5.
        PriorityChanging = true;
        try
        {
            var previousPriority = Priority;
            Priority = priority;

            // Step 6. Over a snapshot: an algorithm belongs to a scheduler task queue, and a queue that runs
            // dry takes its algorithm off the signal, which would otherwise be a mutation mid-iteration.
            if (_priorityChangeAlgorithms is { Count: > 0 } algorithms)
            {
                foreach (var algorithm in algorithms.ToArray())
                {
                    algorithm();
                }
            }

            // Step 7. The event carries the priority the signal had; the new one is read from
            // event.target.priority, which is why this is dispatched after the assignment above.
            FirePriorityChangeEvent(previousPriority);

            // Step 8: a dependent signal follows this one, and its own listeners and algorithms run in turn.
            if (_dependentSignals is { Count: > 0 } dependents)
            {
                foreach (var dependent in dependents.ToArray())
                {
                    dependent.SignalPriorityChange(priority);
                }
            }
        }
        finally
        {
            // Step 9.
            PriorityChanging = false;
        }
    }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#create-a-dependent-task-signal, the algorithm behind
    /// <c>TaskSignal.any()</c>.
    /// </summary>
    /// <param name="engine">The engine the signal belongs to.</param>
    /// <param name="realm">The realm whose <c>TaskSignal.prototype</c> the result gets.</param>
    /// <param name="signals">The abort sources, which may be plain <c>AbortSignal</c>s.</param>
    /// <param name="priority">A fixed priority, when <c>init["priority"]</c> was a <c>TaskPriority</c>.</param>
    /// <param name="prioritySource">The signal to follow, when <c>init["priority"]</c> was a <c>TaskSignal</c>.</param>
    internal static JsTaskSignal CreateDependent(
        Engine engine,
        Realm realm,
        List<JsAbortSignal> signals,
        SchedulerTaskPriority priority,
        JsTaskSignal? prioritySource)
    {
        // Step 1: the abort half is exactly AbortSignal's, run against a TaskSignal instance.
        var result = InitializeDependent(
            new JsTaskSignal(engine, realm, priority)
            {
                _prototype = realm.Intrinsics.TaskSignal.PrototypeObject,
            },
            signals);

        // Step 2. Unconditionally, unlike the abort algorithm's step 3, which skips it when a source was
        // already aborted: "has fixed priority" is derived from this flag, and an aborted composite still has
        // a priority a script can read.
        result.Dependent = true;

        // Step 3: a fixed TaskPriority — the priority is already in place and nothing follows anything.
        if (prioritySource is null)
        {
            return result;
        }

        // Step 4.
        result.Priority = prioritySource.Priority;

        if (prioritySource.HasFixedPriority)
        {
            return result;
        }

        // Step 4.3.1: the graph is flattened to one level, so a change reaches every follower in one pass.
        var source = prioritySource.Dependent ? prioritySource._sourceSignal : prioritySource;
        if (source is null)
        {
            return result;
        }

        result._sourceSignal = source;
        (source._dependentSignals ??= new List<JsTaskSignal>()).Add(result);
        return result;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-fire with a <c>TaskPriorityChangeEvent</c> rather than a
    /// plain <c>Event</c>, which is what step 7 of signal priority change asks for.
    /// </summary>
    private void FirePriorityChangeEvent(SchedulerTaskPriority previousPriority)
    {
        var ev = _realm.Intrinsics.TaskPriorityChangeEvent.CreateTrustedEvent(previousPriority);
        DispatchEvent(ev);
    }
}
#endif
