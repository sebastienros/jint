#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Constraints;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi.Abort;
using Jint.WebApi.Timers;

namespace Jint.WebApi.Scheduling;

/// <summary>
/// One engine's scheduler: the task queues behind <c>scheduler.postTask()</c> and <c>scheduler.yield()</c>,
/// and the single event-loop job that drains them in priority order.
/// <para>
/// https://wicg.github.io/scheduling-apis/#sec-scheduling-tasks-processing-model
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Engine-thread only, and deliberately lock-free</b>, like the timer queue beside it: every member is
/// reached from the engine's pump or from a scheduler method running on it.
/// </para>
/// <para>
/// <b>The engine's pump is not modified.</b> HTML would have the event loop itself choose between its task
/// queues and the scheduler's; Jint's pump has one job queue, so the scheduler puts <i>one</i> job on it —
/// <see cref="RunNextTask"/> — which runs the highest-priority pending task and re-enqueues itself while any
/// remain. Three ordering guarantees follow, and they are what the tests pin:
/// </para>
/// <list type="number">
/// <item><description>
/// <b>Every microtask runs before the next task.</b> A drain job that finds other jobs still queued re-queues
/// itself behind them rather than running a task, which is HTML's microtask checkpoint: both
/// <c>Promise.resolve().then(f); postTask(g)</c> and <c>postTask(g); Promise.resolve().then(f)</c> run
/// <c>f</c> first, whatever priority <c>g</c> has.
/// </description></item>
/// <item><description>
/// Everything a task queues — promise reactions, <c>queueMicrotask</c> — likewise runs before the <i>next</i>
/// task, because the drain job re-enqueues itself behind them.
/// </description></item>
/// <item><description>
/// Among tasks pending at the moment one is chosen, the one with the highest
/// <see cref="SchedulerTaskQueue.EffectivePriority"/> wins, ties going to the oldest — which is exactly
/// https://wicg.github.io/scheduling-apis/#select-the-next-scheduler-task-queue-from-all-schedulers, reduced
/// to the one scheduler an engine has. A <c>user-blocking</c> task posted while <c>user-visible</c> ones are
/// still queued therefore overtakes them.
/// </description></item>
/// </list>
/// <para>
/// <b>One deliberate divergence, in the other direction:</b> every runnable scheduler task runs before any due
/// timer. A timer is promoted only when the job queue has run dry (see <c>Engine.TryPromoteDueTimerJob</c>) and
/// the pump job keeps it non-empty while tasks remain, so even a <c>background</c> task beats a
/// <c>setTimeout(f, 0)</c> that is already due. HTML makes this choice implementation-defined — "selecting
/// between the next Scheduler task and the next task from an event loop's task queues is
/// implementation-defined" — and a browser would typically run the timer first for a background task. An
/// unbroken chain of scheduler tasks can starve the timers exactly as an unbroken chain of promise reactions
/// already can.
/// </para>
/// <para>
/// That has one consequence worth knowing for the <c>delay</c> option, which is a timer like any other: a
/// delayed task joins the queues only once the tasks ahead of it have drained, so it cannot overtake them
/// however urgent it is, and two delayed tasks that come due together run in due-time order rather than
/// priority order. Priority orders the tasks that are <i>pending together</i>, which is what a delay defers a
/// task from being.
/// </para>
/// </remarks>
internal sealed class SchedulerQueue
{
    private readonly Engine _engine;

    /// <summary>
    /// Every non-empty scheduler task queue: the static ones, keyed by (priority, is continuation), and the
    /// dynamic ones, keyed by (signal, is continuation). The specification keeps two maps; one list is the
    /// same thing for the handful of queues an engine has, and it is what the selection walks.
    /// </summary>
    private readonly List<SchedulerTaskQueue> _queues = new();

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#event-loop-next-enqueue-order — "a strictly increasing number
    /// that is used to determine task execution order across scheduler task queues of the same TaskPriority".
    /// </summary>
    private long _nextEnqueueOrder = 1;

    private Action? _pumpJob;
    private bool _pumpScheduled;

    internal SchedulerQueue(Engine engine)
    {
        _engine = engine;
    }

    internal Engine Engine => _engine;

    /// <summary>
    /// The scheduling state of the task currently running, or <see langword="null"/> outside one —
    /// https://wicg.github.io/scheduling-apis/#get-the-current-scheduling-state, which is what
    /// <c>scheduler.yield()</c> inherits its priority and its abort signal from.
    /// </summary>
    /// <remarks>
    /// <b>Synchronous inheritance only.</b> HTML carries the state into every job callback created while a
    /// task runs (its <c>HostMakeJobCallback</c> / <c>HostCallJobCallback</c> patches), so a
    /// <c>scheduler.yield()</c> after an <c>await</c> still inherits. Jint has no such hook, so the state is
    /// ambient for the synchronous part of a task only: the first <c>yield()</c> a callback makes inherits,
    /// and one made after an <c>await</c> — including the second hop of an
    /// <c>await scheduler.yield()</c> chain — falls back to a <c>user-visible</c> continuation with no abort
    /// source. Nothing silently inherits the <i>wrong</i> state, which is why the fallback is a default rather
    /// than a guess.
    /// </remarks>
    internal SchedulingState? CurrentState { get; set; }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#schedule-a-task-to-invoke-an-algorithm: give the task the next
    /// enqueue order, append it to the queue selected for its scheduling state, and make sure the pump job is
    /// on the event loop.
    /// </summary>
    internal void Enqueue(SchedulerTask task)
    {
        var queue = SelectQueue(task.State, task.IsContinuation);
        task.EnqueueOrder = _nextEnqueueOrder++;
        queue.Append(task);
        SchedulePump();
    }

    /// <summary>
    /// Forgets every pending task. Called from <c>Engine.ResetTransientEvaluationState</c>, so a task
    /// scheduled by one evaluation cycle can never run against the globals a <c>RestoreGlobalSnapshot</c> put
    /// back — and, like a timer, its promise simply never settles, which is what that API documents for every
    /// promise registered before a restore.
    /// </summary>
    internal void Clear()
    {
        foreach (var queue in _queues)
        {
            queue.Cancel();
        }

        _queues.Clear();

        // The pump job that is still on the event loop belongs to the ended cycle and is dropped at dequeue by
        // the generation fence; clearing the flag is what lets the next cycle schedule a fresh one.
        _pumpScheduled = false;
        CurrentState = null;
    }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#select-the-scheduler-task-queue, with the two maps flattened
    /// into one list.
    /// </summary>
    private SchedulerTaskQueue SelectQueue(in SchedulingState state, bool isContinuation)
    {
        var signal = state.PrioritySource;

        // "If signal does not have fixed priority": its tasks need a queue of their own, whose priority moves
        // when the signal's does. Everything else shares the queue for its (priority, is continuation) pair.
        if (signal is not null && !signal.HasFixedPriority)
        {
            foreach (var candidate in _queues)
            {
                if (ReferenceEquals(candidate.Signal, signal) && candidate.IsContinuation == isContinuation)
                {
                    return candidate;
                }
            }

            var dynamicQueue = new SchedulerTaskQueue(signal.Priority, isContinuation, signal);

            // The priority change algorithm: re-prioritizing the queue is all a priority change has to do, so
            // the tasks in it never move. That is the specification's own note on why the queues are keyed by
            // signal.
            var algorithm = new Action(() => dynamicQueue.Priority = signal.Priority);
            dynamicQueue.PriorityChangeAlgorithm = algorithm;
            signal.AddPriorityChangeAlgorithm(algorithm);

            _queues.Add(dynamicQueue);
            return dynamicQueue;
        }

        var priority = signal?.Priority ?? state.FixedPriority;
        foreach (var candidate in _queues)
        {
            if (candidate.Signal is null && candidate.Priority == priority && candidate.IsContinuation == isContinuation)
            {
                return candidate;
            }
        }

        var staticQueue = new SchedulerTaskQueue(priority, isContinuation, signal: null);
        _queues.Add(staticQueue);
        return staticQueue;
    }

    /// <summary>
    /// Puts the drain job on the event loop, unless one is already there. One job at a time is what keeps the
    /// microtask checkpoint between two tasks — see the class remarks.
    /// </summary>
    private void SchedulePump()
    {
        if (_pumpScheduled)
        {
            return;
        }

        _pumpScheduled = true;

        // The current generation: the job is registered and queued in one act, so there is no window in
        // which the cycle could have ended in between. No memory state, though: one pump turn runs whichever
        // task is next, and those come from different operations — RunNextTask switches to the task's own
        // captured state around Run() instead.
        _engine.AddToEventLoop(
            _pumpJob ??= RunNextTask,
            _engine.EventLoopGeneration);
    }

    /// <summary>
    /// The drain job: run one task, then queue itself again if anything is left.
    /// </summary>
    private void RunNextTask()
    {
        _pumpScheduled = false;

        // The microtask checkpoint: HTML runs a task only once the microtask queue has drained, and Jint's
        // single job queue is that microtask queue. So a drain job that finds anything queued behind it steps
        // to the back of the line instead of running a task. It converges — each pass runs the jobs that were
        // ahead of it, and a job queue that never empties starves the tasks exactly as it starves everything
        // else — and it is what makes `postTask(f); Promise.resolve().then(g)` run g first, as a browser does.
        if (_engine.HasPendingEventLoopJobs)
        {
            SchedulePump();
            return;
        }

        var task = TakeNextTask();
        if (task is null)
        {
            // Everything left had been aborted; TakeNextTask dropped the empty queues on its way through.
            return;
        }

        try
        {
            _engine.RunWithMemoryAccounting(task.MemoryState, task.Run);
        }
        finally
        {
            // Even when the callback threw something that is not a JavaScript exception — a constraint, a
            // cancellation — the tasks behind it are still scheduled, exactly as the promise reactions behind
            // a throwing one are.
            if (_queues.Count > 0)
            {
                SchedulePump();
            }
        }
    }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#select-the-next-scheduler-task-queue-from-all-schedulers, for
    /// the one scheduler an engine has: the oldest task of the highest-effective-priority queue that has one.
    /// </summary>
    private SchedulerTask? TakeNextTask()
    {
        SchedulerTaskQueue? bestQueue = null;
        SchedulerTask? bestTask = null;

        // Backwards so that a queue found empty can be removed without disturbing the walk.
        for (var i = _queues.Count - 1; i >= 0; i--)
        {
            var queue = _queues[i];
            var head = queue.PeekRunnable();
            if (head is null)
            {
                // "If queue is empty, then run queue's removal steps."
                RemoveQueueAt(i);
                continue;
            }

            if (bestQueue is null
                || queue.EffectivePriority > bestQueue.EffectivePriority
                || (queue.EffectivePriority == bestQueue.EffectivePriority && head.EnqueueOrder < bestTask!.EnqueueOrder))
            {
                bestQueue = queue;
                bestTask = head;
            }
        }

        if (bestQueue is null)
        {
            return null;
        }

        bestQueue.RemoveHead();
        if (bestQueue.IsEmpty)
        {
            RemoveQueueAt(_queues.IndexOf(bestQueue));
        }

        return bestTask;
    }

    private void RemoveQueueAt(int index)
    {
        var queue = _queues[index];
        _queues.RemoveAt(index);

        // The removal steps of a dynamic queue also take its priority change algorithm off the signal, which
        // the specification leaves to garbage collection. A long-lived TaskSignal would otherwise accumulate
        // one algorithm per queue it ever governed.
        if (queue.Signal is { } signal && queue.PriorityChangeAlgorithm is { } algorithm)
        {
            signal.RemovePriorityChangeAlgorithm(algorithm);
            queue.PriorityChangeAlgorithm = null;
        }
    }
}

/// <summary>
/// One scheduler task queue: https://wicg.github.io/scheduling-apis/#scheduler-task-queue — a priority, an
/// is-continuation flag and the tasks themselves, which are in enqueue order because they are only ever
/// appended.
/// </summary>
internal sealed class SchedulerTaskQueue
{
    private readonly Queue<SchedulerTask> _tasks = new();

    internal SchedulerTaskQueue(SchedulerTaskPriority priority, bool isContinuation, JsTaskSignal? signal)
    {
        Priority = priority;
        IsContinuation = isContinuation;
        Signal = signal;
    }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#scheduler-task-queue-priority. Mutable: a dynamic queue's
    /// priority is what a <c>TaskSignal</c>'s priority change updates.
    /// </summary>
    internal SchedulerTaskPriority Priority { get; set; }

    /// <summary>https://wicg.github.io/scheduling-apis/#scheduler-task-queue-is-continuation.</summary>
    internal bool IsContinuation { get; }

    /// <summary>
    /// The signal whose priority this queue follows, or <see langword="null"/> for a queue whose priority is
    /// fixed. It is the key the dynamic queues are found by.
    /// </summary>
    internal JsTaskSignal? Signal { get; }

    /// <summary>The algorithm registered on <see cref="Signal"/>, so it can be taken off again.</summary>
    internal Action? PriorityChangeAlgorithm { get; set; }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#scheduler-task-queue-effective-priority, whose table this
    /// reproduces exactly: <c>background</c> 0/1, <c>user-visible</c> 2/3, <c>user-blocking</c> 4/5, the
    /// higher of each pair being the continuation queue. Higher runs first.
    /// </summary>
    internal int EffectivePriority => ((int) Priority * 2) + (IsContinuation ? 1 : 0);

    internal bool IsEmpty => _tasks.Count == 0;

    internal void Append(SchedulerTask task) => _tasks.Enqueue(task);

    /// <summary>
    /// The queue's first runnable task, discarding aborted ones on the way — which is what makes an abort
    /// O(1): the abort steps only mark the task and settle its promise, and the queue drops it when it
    /// surfaces.
    /// </summary>
    internal SchedulerTask? PeekRunnable()
    {
        while (_tasks.TryPeek(out var candidate))
        {
            if (!candidate.Cancelled)
            {
                return candidate;
            }

            _tasks.Dequeue();
        }

        return null;
    }

    internal void RemoveHead() => _tasks.Dequeue();

    /// <summary>Marks every task as gone, for <see cref="SchedulerQueue.Clear"/>.</summary>
    internal void Cancel()
    {
        foreach (var task in _tasks)
        {
            task.Drop();
        }

        _tasks.Clear();
    }
}

/// <summary>
/// One scheduler task: https://wicg.github.io/scheduling-apis/#scheduler-task, together with the
/// https://wicg.github.io/scheduling-apis/#task-handle that owns its promise and its abort steps.
/// </summary>
/// <remarks>
/// A <c>postTask</c> task calls its callback and settles the promise with the result; a <c>yield</c>
/// continuation has no callback and simply resolves. The two differ in that one field and in the
/// is-continuation flag, which is what puts them in different queues.
/// </remarks>
internal sealed class SchedulerTask
{
    private readonly SchedulerQueue _scheduler;
    private readonly PromiseCapability _capability;
    private readonly ICallable? _callback;
    private readonly JsAbortSignal? _abortSource;
    private readonly MemoryLimitConstraint.OperationState? _memoryState;

    private Action? _abortAlgorithm;
    private TimerQueue? _timers;
    private int _timerId;

    internal SchedulerTask(
        SchedulerQueue scheduler,
        PromiseCapability capability,
        ICallable? callback,
        in SchedulingState state,
        bool isContinuation)
    {
        _scheduler = scheduler;
        _capability = capability;
        _callback = callback;
        _abortSource = state.AbortSource;
        _memoryState = scheduler.Engine.CaptureMemoryLimitState();
        State = state;
        IsContinuation = isContinuation;
    }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#scheduling-state — the abort source and the priority source
    /// this task was scheduled with, and what a <c>yield()</c> inside it inherits.
    /// </summary>
    internal SchedulingState State { get; }

    /// <summary>Whether this is a <c>yield()</c> continuation, which outranks a task of the same priority.</summary>
    internal bool IsContinuation { get; }

    /// <summary>https://wicg.github.io/scheduling-apis/#scheduler-task-enqueue-order.</summary>
    internal long EnqueueOrder { get; set; }

    internal MemoryLimitConstraint.OperationState? MemoryState => _memoryState;

    /// <summary>
    /// Whether the task has been aborted or dropped. A queue discards a marked task rather than running it,
    /// and a delayed task that is marked before its delay elapses is never enqueued at all.
    /// </summary>
    internal bool Cancelled { get; private set; }

    /// <summary>
    /// "If signal is not null, then add handle's abort steps to signal" — step 10 of
    /// https://wicg.github.io/scheduling-apis/#schedule-a-posttask-task. Aborting a signal therefore rejects
    /// every task still pending on it, wherever in the schedule it sits.
    /// </summary>
    internal void RegisterAbortSteps()
    {
        if (_abortSource is null)
        {
            return;
        }

        _abortAlgorithm = Abort;
        _abortSource.AddAbortAlgorithm(_abortAlgorithm);
    }

    /// <summary>
    /// Remembers the timer a delayed <c>postTask</c> is waiting on, so that aborting the task frees the
    /// timer's slot instead of leaving it to expire into nothing.
    /// </summary>
    internal void SetDelayTimer(TimerQueue timers, int timerId)
    {
        _timers = timers;
        _timerId = timerId;
    }

    /// <summary>
    /// The abort steps of https://wicg.github.io/scheduling-apis/#create-a-task-handle: reject the promise
    /// with the signal's reason and take the task out of its queue — lazily here, since a marked task is
    /// discarded when it surfaces.
    /// </summary>
    private void Abort()
    {
        if (Cancelled)
        {
            return;
        }

        Cancelled = true;
        CancelDelayTimer();
        _capability.Reject(_abortSource!.Reason);
    }

    /// <summary>
    /// Drops the task without settling its promise, which is what a <c>RestoreGlobalSnapshot</c> does to
    /// everything the ended cycle scheduled.
    /// </summary>
    internal void Drop()
    {
        Cancelled = true;
        CancelDelayTimer();
        RemoveAbortSteps();
    }

    /// <summary>
    /// The steps https://wicg.github.io/scheduling-apis/#schedule-a-posttask-task queues: run the callback,
    /// settle the promise with what it returned or threw, then the task complete steps.
    /// </summary>
    internal void Run()
    {
        // "Set the current scheduling state for scheduler to state" — restored rather than nulled afterwards,
        // so that a host re-entering the engine from inside a task cannot lose the outer task's state.
        var previousState = _scheduler.CurrentState;
        _scheduler.CurrentState = State;

        try
        {
            if (_callback is null)
            {
                // A yield continuation: "schedule a task to invoke an algorithm ... that performs: resolve
                // result".
                _capability.Resolve(JsValue.Undefined);
                return;
            }

            JsValue result;
            try
            {
                result = _callback.Call(JsValue.Undefined);
            }
            catch (JavaScriptException ex)
            {
                // "If that threw an exception, then reject result with that." Only a JavaScript exception:
                // a constraint, a cancellation or a stack-depth failure is not something a script may catch as
                // a rejection, and erupts from the pump like any other job's would.
                _capability.Reject(ex.Error);
                return;
            }

            // A callback that aborted its own signal has already rejected the promise; settling is once-only,
            // so this is then the no-op the specification's "reject, then resolve" ordering also produces.
            _capability.Resolve(result);
        }
        finally
        {
            _scheduler.CurrentState = previousState;

            // The task complete steps: "if signal is not null, then remove handle's abort steps from signal".
            RemoveAbortSteps();
        }
    }

    private void RemoveAbortSteps()
    {
        if (_abortAlgorithm is { } algorithm)
        {
            _abortSource?.RemoveAbortAlgorithm(algorithm);
            _abortAlgorithm = null;
        }
    }

    private void CancelDelayTimer()
    {
        if (_timerId != 0)
        {
            _timers?.Cancel(_timerId);
            _timerId = 0;
            _timers = null;
        }
    }
}

/// <summary>
/// https://wicg.github.io/scheduling-apis/#scheduling-state — the abort source and priority source a task
/// carries, and what <c>scheduler.yield()</c> inherits from the task it is called in.
/// </summary>
/// <param name="AbortSource">
/// The <c>signal</c> option, or <see langword="null"/>. May be a plain <c>AbortSignal</c>: cancellation does
/// not need a task signal.
/// </param>
/// <param name="PrioritySource">
/// The <c>TaskSignal</c> whose priority the task follows, or <see langword="null"/> when the priority is
/// fixed.
/// </param>
/// <param name="FixedPriority">
/// The priority when <paramref name="PrioritySource"/> is <see langword="null"/>: the <c>priority</c> option
/// if one was given, and <c>user-visible</c> otherwise. The specification spells this as a "fixed priority
/// unabortable task signal" and notes that implementations may cache them; not creating one at all is the
/// same thing without the object.
/// </param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct SchedulingState(
    JsAbortSignal? AbortSource,
    JsTaskSignal? PrioritySource,
    SchedulerTaskPriority FixedPriority);
#endif
