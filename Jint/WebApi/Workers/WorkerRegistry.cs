#if NET8_0_OR_GREATER
using System.Threading;

namespace Jint.WebApi.Workers;

/// <summary>
/// One engine's worker configuration and its live connections: the provider, the two per-engine caps, and the
/// list <c>MaxWorkers</c> is counted against.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is an explicit, deliberate carve-out from <c>WebApiEngineState</c>'s engine-thread-only rule</b>,
/// and the same carve-out <c>MessagePortEndpoint</c> documents. Every other list on that state — the fetches
/// in flight, the sockets, the broadcast channels — is added to and removed from by the engine's own thread
/// alone. A worker connection is not: it is created on the parent's thread and can end on the <i>worker's</i>,
/// from a <c>close()</c> that runs while the parent is inside an unrelated statement. So the list is guarded
/// by a lock and the count is a separate interlocked field, which is what lets
/// <see cref="LiveCount"/> be read from the parent's thread without taking it.
/// </para>
/// <para>
/// The three configuration values are read once, when the engine is built — the promise every setting in this
/// options subtree makes — so a host mutating <c>Options.WebApi.Workers</c> afterwards does not change an
/// engine that already exists.
/// </para>
/// </remarks>
internal sealed class WorkerRegistry
{
    private readonly Lock _gate = new();
    private readonly List<WorkerLink> _live = new();

    /// <summary>
    /// The same count as <c>_live.Count</c>, kept beside it so that the constructor's quota check — which runs
    /// on the parent's thread, in the middle of a statement — costs one interlocked read rather than a lock
    /// that a worker's <c>close()</c> on another thread may be holding.
    /// </summary>
    private int _liveCount;

    internal WorkerRegistry(WorkerProvider provider, int maxWorkers, int maxQueuedMessages)
    {
        Provider = provider;
        MaxWorkers = maxWorkers;
        MaxQueuedMessages = maxQueuedMessages;
    }

    /// <summary>The host's answer to <c>new Worker(...)</c>; never null, or this registry would not exist.</summary>
    internal WorkerProvider Provider { get; }

    /// <summary><c>Options.WebApi.Workers.MaxWorkers</c>, read once.</summary>
    internal int MaxWorkers { get; }

    /// <summary><c>Options.WebApi.Workers.MaxQueuedMessages</c>, read once.</summary>
    internal int MaxQueuedMessages { get; }

    /// <summary>
    /// How deep in a worker tree the engine owning this registry sits: 0 for a top-level engine, and greater
    /// only for one a provider deliberately granted the ability to create workers of its own. What
    /// <see cref="WorkerRequest.Depth"/> reports, plus one.
    /// </summary>
    internal int Depth { get; set; }

    /// <summary>How many connections this engine has live. Any thread.</summary>
    internal int LiveCount => Volatile.Read(ref _liveCount);

    internal void Add(WorkerLink link)
    {
        lock (_gate)
        {
            _live.Add(link);
            Volatile.Write(ref _liveCount, _live.Count);
        }
    }

    internal void Remove(WorkerLink link)
    {
        lock (_gate)
        {
            _live.Remove(link);
            Volatile.Write(ref _liveCount, _live.Count);
        }
    }

    /// <summary>
    /// The live connections, copied out under the lock so the caller can end them without holding it — ending
    /// one calls back into <see cref="Remove"/>, and into host code.
    /// </summary>
    /// <remarks>
    /// The seam a parent-side <c>RestoreGlobalSnapshot</c> and <c>Dispose</c> will end every connection
    /// through; those hooks land in their own change (wave 3), which is also where the reasons
    /// <c>ParentRestored</c> and <c>ParentDisposed</c> start being used.
    /// </remarks>
    internal WorkerLink[] Snapshot()
    {
        lock (_gate)
        {
            return _live.ToArray();
        }
    }
}
#endif
