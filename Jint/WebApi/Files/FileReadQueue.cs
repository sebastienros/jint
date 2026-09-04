#if NET8_0_OR_GREATER
using Jint.Runtime;

namespace Jint.WebApi.Files;

/// <summary>
/// The engine's <i>file reading task source</i>: the reads in flight, and the one event-loop job that steps
/// them.
/// <para>
/// https://w3c.github.io/FileAPI/#fileReadingTaskSource
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>One job for every read, not one per read, and that is what makes the microtask checkpoint work.</b>
/// HTML runs a task only once the microtask queue has drained, and Jint's single job queue <i>is</i> that
/// queue — so a step that finds anything queued behind it has to step to the back of the line, exactly as
/// <c>SchedulerQueue.RunNextTask</c> does. With a job per read that rule deadlocks the moment two readers are
/// active: each finds the other's job pending, each defers, and neither ever sees an empty queue. A single
/// pump cannot see itself, so what it finds pending really is other work.
/// </para>
/// <para>
/// One step per turn, round-robin, which is also what a browser does: two readers started together interleave
/// their events rather than one running to completion first. A read whose reader has moved on — aborted, or
/// started a second read — is dropped here rather than skipped, which is <c>abort()</c>'s "remove those tasks
/// from that task queue" step arriving at the queue it names.
/// </para>
/// <para>
/// <b>A restore drops the reads and leaves their readers in <c>loading</c>.</b> That is the contract every
/// other piece of pending work has across <c>RestoreGlobalSnapshot</c> — a promise registered before it never
/// settles, a timer never fires — and it is the honest one here too: the events would be dispatched to
/// listeners closing over an evaluation cycle that has ended.
/// </para>
/// </remarks>
internal sealed class FileReadQueue(Engine engine)
{
    private readonly Engine _engine = engine;
    private readonly List<FileReadOperation> _operations = new();

    private bool _pumpScheduled;
    private Action? _pumpJob;

    /// <summary>Adds a read that has just started, and makes sure the pump is on the loop.</summary>
    internal void Enqueue(FileReadOperation operation)
    {
        _operations.Add(operation);
        SchedulePump();
    }

    /// <summary>
    /// Forgets every read in flight. Called from <c>WebApiEngineState.ResetTransientState</c>; see the class
    /// remarks for what that leaves behind.
    /// </summary>
    internal void Clear()
    {
        _operations.Clear();

        // The pump job still on the event loop belongs to the ended cycle and is dropped at dequeue by the
        // generation fence; clearing the flag is what lets the next cycle schedule a fresh one.
        _pumpScheduled = false;
    }

    private void SchedulePump()
    {
        if (_pumpScheduled)
        {
            return;
        }

        _pumpScheduled = true;
        _engine.AddToEventLoop(_pumpJob ??= RunNextStep, _engine.EventLoopGeneration, EventLoopJobKind.Task);
    }

    private void RunNextStep()
    {
        _pumpScheduled = false;

        // The microtask checkpoint; see the class remarks.
        if (_engine.HasPendingEventLoopJobs)
        {
            SchedulePump();
            return;
        }

        FileReadOperation? next = null;
        while (_operations.Count > 0)
        {
            var candidate = _operations[0];
            _operations.RemoveAt(0);
            if (candidate.IsLive)
            {
                next = candidate;
                break;
            }
        }

        if (next is null)
        {
            return;
        }

        try
        {
            next.RunStep();
        }
        finally
        {
            // Even when a listener threw something that is not a JavaScript exception — a constraint, a
            // cancellation — the reads behind this one are still scheduled, exactly as the promise reactions
            // behind a throwing one are.
            if (next.IsLive)
            {
                _operations.Add(next);
            }

            if (_operations.Count > 0)
            {
                SchedulePump();
            }
        }
    }
}
#endif
