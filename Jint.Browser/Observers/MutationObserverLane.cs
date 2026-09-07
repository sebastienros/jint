using AngleSharp.Dom;
using Jint.Browser.Runtime;
using Jint.Runtime;

namespace Jint.Browser.Observers;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#queue-a-mutation-observer-microtask">Queue a mutation observer
/// microtask</a>: one job on the engine's queue per batch of mutations, however many records the batch holds.
/// </summary>
/// <remarks>
/// <para>
/// The engine's single job queue <em>is</em> the microtask queue, so the ordering DOM asks for comes out of a
/// plain enqueue: a <c>then</c> callback registered before the first mutation of a turn runs before the
/// observer's, and one registered after it runs after. The compound-microtask-queue flag is
/// <see cref="_scheduled"/>, set when the job is queued and cleared when it starts — so a mutation made from
/// inside a callback queues the next checkpoint rather than joining the one that is running.
/// </para>
/// <para>
/// An observer joins the notify set when a record arrives and leaves it when its records are taken, whether
/// by delivery, by <c>takeRecords()</c> or by <c>disconnect()</c>. That set is the only strong reference this
/// package keeps to an observer, which matches DOM's own rule that an observer with a non-empty record queue
/// is reachable: one that a page dropped and that has nothing queued is collectable together with its
/// callback.
/// </para>
/// </remarks>
internal sealed class MutationObserverLane
{
    private readonly PageRuntime _runtime;
    private readonly List<JsMutationObserver> _notify = [];
    private readonly Action _notifyJob;
    private readonly Stack<ReplaceAllScope> _replaceAll = [];
    private bool _scheduled;

    internal MutationObserverLane(PageRuntime runtime)
    {
        _runtime = runtime;
        _notifyJob = Notify;
    }

    /// <summary>Adds <paramref name="observer"/> to the notify set and queues the checkpoint if it is not queued.</summary>
    internal void Enlist(JsMutationObserver observer)
    {
        if (!_notify.Contains(observer))
        {
            _notify.Add(observer);
        }

        if (_scheduled)
        {
            return;
        }

        _scheduled = true;
        _runtime.Engine.AddToEventLoop(_notifyJob, EventLoopJobKind.Microtask);
    }

    /// <summary>Takes <paramref name="observer"/> out of the notify set; its queue is empty.</summary>
    internal void Withdraw(JsMutationObserver observer) => _notify.Remove(observer);

    /// <summary>
    /// Starts one replace-all operation. AngleSharp queues one record for every remove and insert; DOM queues
    /// one record containing both snapshots, so the target's raw records are held until the operation ends.
    /// </summary>
    internal void BeginReplaceAll(INode target) => _replaceAll.Push(new ReplaceAllScope(target));

    /// <summary>Consumes a raw target record while replace-all is in progress.</summary>
    internal bool CaptureReplaceAll(JsMutationObserver observer, IMutationRecord record)
    {
        if (record.Type != "childList")
        {
            return false;
        }

        foreach (var scope in _replaceAll)
        {
            if (ReferenceEquals(record.Target, scope.Target))
            {
                if (!scope.Observers.Contains(observer))
                {
                    scope.Observers.Add(observer);
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>Queues the one record DOM assigns to a completed replace-all operation.</summary>
    internal void CompleteReplaceAll(INode target, INode[] added, INode[] removed)
    {
        var scope = TakeReplaceAll(target);
        foreach (var observer in scope.Observers)
        {
            observer.Queue(new ReplaceAllMutationRecord(target, added, removed));
        }
    }

    /// <summary>Clears a failed replace-all operation without replacing its records.</summary>
    internal void CancelReplaceAll(INode target) => _ = TakeReplaceAll(target);

    private ReplaceAllScope TakeReplaceAll(INode target)
    {
        if (_replaceAll.Count == 0)
        {
            throw new InvalidOperationException("No replace-all operation is active.");
        }

        var scope = _replaceAll.Peek();
        if (!ReferenceEquals(scope.Target, target))
        {
            throw new InvalidOperationException("A different replace-all operation is active.");
        }

        return _replaceAll.Pop();
    }

    private void Notify()
    {
        _scheduled = false;

        if (_notify.Count == 0)
        {
            return;
        }

        // A copy, because a callback may mutate the DOM and put its own observer straight back into the set;
        // those records belong to the next checkpoint.
        var batch = _notify.ToArray();
        _notify.Clear();

        foreach (var observer in batch)
        {
            observer.Deliver();
        }
    }

    private sealed class ReplaceAllScope(INode target)
    {
        internal INode Target { get; } = target;

        internal List<JsMutationObserver> Observers { get; } = [];
    }
}
