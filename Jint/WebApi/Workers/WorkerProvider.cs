#if NET8_0_OR_GREATER
// The folder is the feature's, the namespace is the host's: every host-facing extension point in this
// subtree — StorageProvider, CacheStorageProvider, BroadcastChannelBroker, ConsoleSink, DiagnosticsSink —
// lives in Jint.WebApi so that one using directive reaches all of them, while the internal, JS-facing types
// a feature is built from take the per-feature namespace (Jint.WebApi.Messaging, Jint.WebApi.Timers, …).
namespace Jint.WebApi;

/// <summary>
/// The host's answer to <c>new Worker(...)</c>: it decides whether a worker may exist at all, builds the
/// <see cref="Engine"/> that runs it, and — because Jint never starts a thread — decides which thread pumps
/// it. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// Nearly everything a worker needs already exists in the engine: a second isolated global is a second
/// <see cref="Engine"/>, the channel between them is a cross-engine <c>MessagePort</c> pair, and delivery on
/// the receiver's own thread is its event loop. The one thing that does not exist, and must not, is
/// <b>the thread</b>: <i>Jint never starts a thread to run script</i> is load-bearing across the whole web-API
/// family, so <c>new Worker()</c> is only implementable if the host supplies the execution resource. That is
/// what this class is. The engine owns the specification-shaped parts — port entanglement, the worker global,
/// message and error plumbing, ordering, <c>terminate()</c> semantics — and the host owns every thread, every
/// pump, and the worker engine's configuration.
/// </para>
/// <para>
/// An abstract class rather than an interface or a delegate, for the reason
/// <see cref="StorageProvider"/> gives verbatim: later revisions can add members without breaking the hosts
/// that implement it today, and a <c>Func&lt;&gt;</c> can never grow a parameter.
/// </para>
/// <para>
/// <b>Pooled hosts: one provider per process, per-request policy from <c>HostDefined</c>.</b> The provider is
/// normally the same object for every engine in a pool; everything that varies per request — tenant, loader
/// root, budget — is read inside <see cref="CreateWorkerEngine"/> from
/// <c>request.Parent.HostDefined</c>, which is per engine, never read by the engine, and survives
/// <c>RestoreGlobalSnapshot</c>. Setting the provider through
/// <c>engine.WebApi.Enable(…, w =&gt; w.Workers.Provider = …)</c> reaches that one engine and no other —
/// the callback is handed a copy of the web-API settings that the engine takes for itself — so it is a way
/// to give one rented engine a provider of its own, not a way to configure the pool.
/// </para>
/// <para>
/// <b>Thread-safety.</b> A provider is called from the parent's thread
/// (<see cref="CreateWorkerEngine"/>, <see cref="OnWorkerStarted"/>) and from whichever thread ended a
/// connection (<see cref="OnWorkerEnded"/>) — which is frequently not the same one, and may be a worker's.
/// A provider shared by concurrently running engines is called from all of their threads and must be
/// thread-safe.
/// </para>
/// <para>
/// <b><see cref="WorkerRequest.CreateDefaultOptions"/> is a convenience, not a security boundary.</b> It
/// copies the parent's restrictive posture and withholds its grants, but a provider that builds the worker's
/// <see cref="Options"/> from scratch is the one place a hardened parent can be un-hardened. A host with a
/// hardened profile builds the worker's options from the same hardening helper it built the parent's from.
/// </para>
/// </remarks>
public abstract class WorkerProvider
{
    /// <summary>
    /// Initializes a new provider.
    /// </summary>
    protected WorkerProvider()
    {
    }

    /// <summary>
    /// Builds the engine for one <c>new Worker(...)</c>, or returns <see langword="null"/> to refuse.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs on the <b>parent's</b> thread, synchronously, while the parent's script is suspended inside the
    /// constructor. It must not run script, must not block, and must <b>not</b> fetch the worker's script —
    /// that is the worker's own <c>IModuleLoader</c>'s job, on the worker's own pump.
    /// </para>
    /// <para>
    /// Returning <see langword="null"/> is a policy refusal and reaches the script as a synchronous
    /// <c>SecurityError</c> <c>DOMException</c>. Anything this method <i>throws</i> propagates to the script's
    /// caller unchanged; nothing here is translated.
    /// </para>
    /// <para>
    /// The engine returned must be quiescent — no other thread inside it, no asynchronous host operation
    /// pending — because the engine mutates it (entangles a port, installs the worker global, queues the start
    /// job) before handing it over. That is validated rather than trusted: a pre-warmed engine another thread
    /// is already pumping gets the engine's own admission error at <c>new Worker()</c> rather than silent
    /// corruption. It must also carry <see cref="WebApiFeatures.Messaging"/>, not be the parent, not already
    /// be connected, and observe <see cref="WorkerRequest.TerminationToken"/> —
    /// <see cref="WorkerRequest.CreateDefaultOptions"/> arranges all of that.
    /// </para>
    /// </remarks>
    /// <param name="request">What is being asked for, and the pre-wired options to start from.</param>
    /// <returns>The engine that will run the worker, or <see langword="null"/> to refuse this worker.</returns>
    public abstract Engine? CreateWorkerEngine(WorkerRequest request);

    /// <summary>
    /// The ports are entangled, the worker global is installed and the start job is queued on the
    /// <b>worker's</b> loop. This is where the host starts pumping.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs on the parent's thread, before <c>new Worker(...)</c> returns. <b>Register the connection before
    /// you start pumping it</b>: the moment a pump runs the worker may load, evaluate, call <c>close()</c> and
    /// end, so <see cref="OnWorkerEnded"/> may be invoked concurrently with this method — including before it
    /// returns. A host that starts the thread and then adds the connection to its own list can therefore
    /// observe a remove before the add.
    /// </para>
    /// <para>
    /// <b>The hand-off has a memory-ordering edge.</b> Everything the engine wrote happens-before this method
    /// returns; <c>Thread.Start()</c>, or publication through any concurrent collection, gives the host its
    /// own edge to the first pump. A connection stashed in a plain field for an existing loop to pick up does
    /// not.
    /// </para>
    /// </remarks>
    /// <param name="connection">The live connection, and the engine to pump.</param>
    public virtual void OnWorkerStarted(WorkerConnection connection)
    {
    }

    /// <summary>
    /// The connection ended — a <b>signal only</b>, on whichever thread ended it (frequently <i>not</i> the
    /// worker's).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Do not touch <see cref="WorkerConnection.Worker"/> from here beyond reading immutable properties:
    /// no <c>Dispose()</c>, no <c>ProcessTasks()</c>.</b> In the thread-per-worker shape a <c>terminate()</c>
    /// ends the connection on the <i>parent's</i> thread while the worker thread sits inside
    /// <c>ProcessTasks</c>, and either call from here is the engine's concurrent-use exception thrown out of
    /// the middle of the parent's script. Signal your pump loop instead; it observes
    /// <see cref="WorkerConnection.IsEnded"/> (or wakes on
    /// <see cref="WorkerConnection.TerminationToken"/>), leaves, and disposes the engine on the thread that
    /// was pumping it.
    /// </para>
    /// <para>
    /// Invoked exactly once per connection, on the thread that ended it, and never while a lock this feature
    /// holds is taken. It may run concurrently with <see cref="OnWorkerStarted"/>, including before that
    /// method returns.
    /// </para>
    /// </remarks>
    /// <param name="connection">The connection that ended. <see cref="WorkerConnection.IsEnded"/> is already
    /// <see langword="true"/>, and <see cref="WorkerConnection.EndReason"/> already answers
    /// <paramref name="reason"/>.</param>
    /// <param name="reason">Why it ended.</param>
    public virtual void OnWorkerEnded(WorkerConnection connection, WorkerEndReason reason)
    {
    }
}
#endif
