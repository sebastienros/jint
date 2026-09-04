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
}
