#if NET8_0_OR_GREATER
using System.Threading;
using Jint.WebApi.Locks;

namespace Jint.WebApi;

/// <summary>
/// The lock state a set of engines coordinate through — what the Web Locks API calls a
/// <see href="https://w3c.github.io/web-locks/#lock-managers">lock manager</see>, and what
/// <c>navigator.locks</c> is the handle on. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// A browser scopes locking by two things Jint does not have: an <i>agent cluster</i> (a page and the
/// workers that share its event loop family) and a <i>storage bucket</i> (an origin). This object is Jint's
/// answer to both at once — <b>one manager is one agent cluster and one origin</b>. Engines that share a
/// manager queue behind each other's locks; engines that do not, cannot, however equal their resource names.
/// </para>
/// <para>
/// <b>You only ever construct one and hand it over.</b> It has no members of its own: everything it does
/// happens through <c>navigator.locks</c>. Assign one to
/// <see cref="Options.WebLocksOptions.Manager"/> on an <see cref="Options"/> instance several engines are
/// built from — or assign the same instance to several <see cref="Options"/> instances — and those engines
/// share one lock space. Say nothing and each engine gets a private manager of its own, so a script still
/// serializes against itself and nothing crosses an engine boundary.
/// </para>
/// <para>
/// <b>A worker never inherits one.</b> <c>WorkerRequest.CreateDefaultOptions</c> leaves this setting unset,
/// exactly as it leaves <see cref="Options.MessagingOptions.Broker"/> unset: a worker is a second engine,
/// and putting it in its parent's agent cluster is a decision a provider makes in one visible line rather
/// than one it inherits. A host that wants a window and its workers to be one agent cluster — which is what
/// a browser gives them — assigns this manager to the options its <c>WorkerProvider</c> builds the worker
/// from.
/// </para>
/// <para>
/// <b>Threading.</b> This type is thread-safe, which is the whole reason it is a class rather than a field
/// on the engine. What crosses between engines is never a <c>JsValue</c>: a request carries a resource name,
/// a mode and a client id, and a grant is enqueued onto the receiving engine's event loop — the one part of
/// an engine any thread may touch. The callback, the <c>Lock</c> object and every promise settle on
/// whichever thread pumps that engine, so an engine nobody pumps never takes a grant, exactly as for timers
/// and message ports. <b>An engine nobody pumps therefore holds its locks forever</b>, which is the same
/// stall a browser tab that stops running script produces, and what the <c>steal</c> option exists for.
/// </para>
/// <para>
/// <b>It holds its requests and its locks strongly.</b> A pending request keeps its engine reachable until
/// it is granted, aborted or terminated, and a held lock until it is released — a lock nobody is holding a
/// reference to is still a live lock, so collecting it would silently let somebody else in. Two things
/// release everything an engine has here: <c>Engine.Advanced.RestoreGlobalSnapshot</c> and
/// <see cref="Engine.Dispose"/>, which are Jint's reading of
/// <see href="https://w3c.github.io/web-locks/#termination-of-locks">terminate remaining locks and
/// requests</see> for an agent. A host that shares one manager between long-lived engines and short-lived
/// ones therefore disposes or restores the short-lived ones; there is no finalizer-driven cleanup and none
/// is planned.
/// </para>
/// </remarks>
public sealed class LockManager
{
    /// <summary>
    /// Jint's reading of the specification's <see href="https://w3c.github.io/web-locks/#lock-task-queue">lock
    /// task queue</see>, which upstream starts as a parallel queue.
    /// </summary>
    /// <remarks>
    /// Every algorithm the specification enqueues there runs inside this critical section instead, on the
    /// thread of whichever engine started it. That keeps the two properties the parallel queue has and costs
    /// no thread: one engine's state changes happen in the order that engine made them, and no two engines
    /// can interleave inside one of them. Nothing JavaScript-visible happens in here — every grant, miss and
    /// rejection leaves through <see cref="LockAgent.Schedule"/> — so the section can never run user code,
    /// and enqueueing under it is safe for the reason <see cref="BroadcastChannelBroker"/> gives: the event
    /// loop's enqueue appends to a concurrent queue and completes its waiters asynchronously.
    /// </remarks>
    private readonly Lock _gate = new();

    /// <summary>
    /// https://w3c.github.io/web-locks/#lock-request-queue-map — the pending requests of each resource name,
    /// in request order.
    /// </summary>
    /// <remarks>
    /// A name whose queue empties is removed outright, which the specification's map does not do: it says
    /// only "if queueMap[name] does not exist, set queueMap[name] to a new empty lock request queue". An
    /// absent queue and an empty one are indistinguishable to every algorithm that reads one — <i>grantable</i>
    /// asks whether it is empty and <i>process</i> walks it — so removing it changes nothing but the fact
    /// that a script cycling through resource names cannot grow this map. It is the same bound
    /// <see cref="BroadcastChannelBroker"/> puts on its channel names.
    /// </remarks>
    private readonly Dictionary<string, List<LockRequestEntry>> _queues = new(StringComparer.Ordinal);

    /// <summary>https://w3c.github.io/web-locks/#held-lock-set, in grant order.</summary>
    private readonly List<HeldLockEntry> _held = [];

    /// <summary>
    /// Creates a manager: one agent cluster's worth of Web Locks state, empty to begin with.
    /// </summary>
    public LockManager()
    {
    }

    /// <summary>
    /// How many locks are held right now, across every engine sharing this manager.
    /// </summary>
    /// <remarks>
    /// Deliberately <see langword="internal"/>: a host has no business branching on it, and script reads the
    /// same figure through <c>navigator.locks.query()</c>. It exists because the in-repo tests have no other
    /// way to pin what a restore and a dispose release.
    /// </remarks>
    internal int HeldCount
    {
        get
        {
            lock (_gate)
            {
                return _held.Count;
            }
        }
    }

    /// <inheritdoc cref="HeldCount" />
    internal int PendingCount
    {
        get
        {
            lock (_gate)
            {
                var total = 0;
                foreach (var queue in _queues.Values)
                {
                    total += queue.Count;
                }

                return total;
            }
        }
    }

    /// <summary>
    /// How many resource names currently have at least one pending request — the leak guard above, which is
    /// invisible from script by construction.
    /// </summary>
    internal int ActiveNameCount
    {
        get
        {
            lock (_gate)
            {
                return _queues.Count;
            }
        }
    }

    /// <summary>
    /// The lock-task-queue half of
    /// <see href="https://w3c.github.io/web-locks/#request-a-lock">request a lock</see>, steps 3.4 to 3.6:
    /// steal, or the <c>ifAvailable</c> miss, or enqueue — and then process the queue.
    /// </summary>
    /// <remarks>
    /// The abort algorithm (step 2) is registered by the caller, before this runs, because it is a
    /// <c>JsValue</c>'s business.
    /// </remarks>
    internal void Request(LockRequestEntry request, bool ifAvailable, bool steal)
    {
        lock (_gate)
        {
            if (steal)
            {
                StealLocked(request);
            }
            else if (ifAvailable && !IsGrantableLocked(request))
            {
                // Step 3.5.1: "Let r be the result of invoking callback with null as the only argument",
                // enqueued on the requesting engine's own event loop. The request never enters the queue, so
                // there is nothing to process and nothing to abort.
                request.Agent.Schedule(request.Generation, request.Operation.InvokeWithoutLock);
                return;
            }
            else
            {
                GetQueueLocked(request.Name).Add(request);
            }

            ProcessQueueLocked(request.Name);
        }
    }

    /// <summary>
    /// https://w3c.github.io/web-locks/#release-a-lock — remove the lock from the held set, then process the
    /// queue its name waits on.
    /// </summary>
    internal void Release(HeldLockEntry held)
    {
        lock (_gate)
        {
            _held.Remove(held);
            ProcessQueueLocked(held.Name);
        }
    }

    /// <summary>
    /// https://w3c.github.io/web-locks/#abort-a-request — remove the request from its queue, then process
    /// that queue. Idempotent, and a no-op for a request that has already been granted, which is what makes
    /// an abort racing a grant harmless: the grant steps re-read the signal before they invoke anything.
    /// </summary>
    internal void AbortRequest(LockRequestEntry request)
    {
        lock (_gate)
        {
            if (_queues.TryGetValue(request.Name, out var queue))
            {
                queue.Remove(request);
            }

            ProcessQueueLocked(request.Name);
        }
    }

    /// <summary>
    /// https://w3c.github.io/web-locks/#termination-of-locks — "to terminate remaining locks and requests
    /// with agent": abort every request that agent made and release every lock it holds.
    /// </summary>
    /// <remarks>
    /// It settles nothing. The specification's steps do not either — <i>abort the request</i> removes a
    /// request from a queue and <i>release the lock</i> removes a lock from the held set, and neither touches
    /// a promise — and here the reason is sharper still: the promises belong to an evaluation cycle that has
    /// just ended, or to an engine that has just been disposed.
    /// </remarks>
    internal void Terminate(LockAgent agent)
    {
        lock (_gate)
        {
            List<string>? touched = null;

            foreach (var pair in _queues)
            {
                if (pair.Value.RemoveAll(request => ReferenceEquals(request.Agent, agent)) > 0)
                {
                    (touched ??= []).Add(pair.Key);
                }
            }

            for (var i = _held.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_held[i].Agent, agent))
                {
                    continue;
                }

                (touched ??= []).Add(_held[i].Name);
                _held.RemoveAt(i);
            }

            if (touched is null)
            {
                return;
            }

            // After the walk rather than during it: processing a queue can remove it from the map.
            foreach (var name in touched)
            {
                ProcessQueueLocked(name);
            }
        }
    }

    /// <summary>
    /// https://w3c.github.io/web-locks/#snapshot-the-lock-state — the held and pending lists, as plain data.
    /// </summary>
    /// <remarks>
    /// Taken whole under the gate, so the two lists describe one instant. The pending requests of a given
    /// name come out in the order they were made, which the specification requires; the order <i>across</i>
    /// names is the map's, which it explicitly does not.
    /// </remarks>
    internal LockStateSnapshot Snapshot()
    {
        lock (_gate)
        {
            var held = new List<LockInfo>(_held.Count);
            foreach (var entry in _held)
            {
                held.Add(new LockInfo(entry.Name, entry.Mode, entry.ClientId));
            }

            var pending = new List<LockInfo>();
            foreach (var queue in _queues.Values)
            {
                foreach (var request in queue)
                {
                    pending.Add(new LockInfo(request.Name, request.Mode, request.ClientId));
                }
            }

            return new LockStateSnapshot(held, pending);
        }
    }

    /// <summary>
    /// Step 3.4: "For each lock of held: if lock's name is name, remove it and reject its released promise
    /// with an <c>AbortError</c> <c>DOMException</c>" — then "prepend request in queue", which is what
    /// preempts everything already waiting.
    /// </summary>
    private void StealLocked(LockRequestEntry request)
    {
        List<HeldLockEntry>? stolen = null;
        foreach (var entry in _held)
        {
            if (string.Equals(entry.Name, request.Name, StringComparison.Ordinal))
            {
                (stolen ??= []).Add(entry);
            }
        }

        if (stolen is not null)
        {
            foreach (var entry in stolen)
            {
                _held.Remove(entry);

                // Under the gate rather than in the job below: see LockOperation's remarks on the flag.
                entry.Operation.MarkStolen();
            }

            // In held order, and only after every one of them has left the set: the rejection is a job on
            // another engine's loop, and a stolen lock must not still be in the set when it runs.
            foreach (var entry in stolen)
            {
                entry.Agent.Schedule(entry.Generation, entry.Operation.RejectReleasedWithAbortError);
            }
        }

        GetQueueLocked(request.Name).Insert(0, request);
    }

    /// <summary>
    /// https://w3c.github.io/web-locks/#process-the-lock-request-queue — grant from the front of the queue
    /// until one is not grantable, which by the note there means none of the rest is either.
    /// </summary>
    private void ProcessQueueLocked(string name)
    {
        if (!_queues.TryGetValue(name, out var queue))
        {
            return;
        }

        while (queue.Count > 0)
        {
            var request = queue[0];
            if (!IsGrantableLocked(request))
            {
                return;
            }

            queue.RemoveAt(0);

            var held = new HeldLockEntry(request);
            _held.Add(held);

            // The grant steps themselves are the engine's: a Lock object is built, the signal is re-read and
            // the callback is invoked, all on the thread that owns the request.
            request.Agent.Schedule(request.Generation, () => request.Operation.Grant(held));
        }

        _queues.Remove(name);
    }

    /// <summary>
    /// https://w3c.github.io/web-locks/#lock-request-grantable.
    /// </summary>
    /// <remarks>
    /// Called for a request that is the head of its queue, and for one that is not in a queue at all — which
    /// is the <c>ifAvailable</c> case, where "request is not the first item in queue" is true of any
    /// non-empty queue and the request is therefore refused behind anything already waiting.
    /// </remarks>
    private bool IsGrantableLocked(LockRequestEntry request)
    {
        if (_queues.TryGetValue(request.Name, out var queue)
            && queue.Count > 0
            && !ReferenceEquals(queue[0], request))
        {
            return false;
        }

        foreach (var entry in _held)
        {
            if (!string.Equals(entry.Name, request.Name, StringComparison.Ordinal))
            {
                continue;
            }

            // "If mode is exclusive, return true if no lock in held has name equal to name"; otherwise the
            // mode is shared and only an exclusive holder blocks it.
            if (request.Mode == LockMode.Exclusive || entry.Mode == LockMode.Exclusive)
            {
                return false;
            }
        }

        return true;
    }

    private List<LockRequestEntry> GetQueueLocked(string name)
    {
        if (!_queues.TryGetValue(name, out var queue))
        {
            queue = [];
            _queues[name] = queue;
        }

        return queue;
    }
}
#endif
