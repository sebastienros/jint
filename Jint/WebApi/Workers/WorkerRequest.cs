#if NET8_0_OR_GREATER
using System.Threading;

namespace Jint.WebApi;

/// <summary>
/// One <c>new Worker(...)</c> call, as the host sees it: what is being asked for, what it would cost, and the
/// pre-wired <see cref="Options"/> a provider should usually start from. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// Created by the engine and handed to <see cref="WorkerProvider.CreateWorkerEngine"/> on the parent's thread,
/// while the parent's script is suspended inside the constructor. It is valid for the duration of that call
/// and describes nothing that changes during it.
/// </remarks>
public sealed class WorkerRequest
{
    /// <summary>
    /// The flags a worker never inherits from its parent, whatever the parent carries.
    /// </summary>
    /// <remarks>
    /// The first six are the grants the feature set documents as given only by name and never by a closure —
    /// outbound network (<see cref="WebApiFeatures.Fetch"/>, <see cref="WebApiFeatures.EventSource"/>,
    /// <see cref="WebApiFeatures.WebSocket"/>), persistent state (<see cref="WebApiFeatures.Storage"/>,
    /// <see cref="WebApiFeatures.CacheApi"/>) and inbound request routing
    /// (<see cref="WebApiFeatures.FetchEvents"/>) — and "the parent could reach the network so the worker may
    /// too" is exactly the reasoning <see cref="WebApiFeatures.Default"/> exists to refuse. The seventh is
    /// <see cref="WebApiFeatures.Workers"/> itself: a worker that can spawn workers is a grant, by
    /// implication, of the capability that manufactures engines, and a per-engine worker cap bounds the
    /// branching factor of a tree whose depth nothing would then bound.
    /// </remarks>
    private const WebApiFeatures NeverInheritedByAWorker =
        WebApiFeatures.Fetch
        | WebApiFeatures.EventSource
        | WebApiFeatures.WebSocket
        | WebApiFeatures.Storage
        | WebApiFeatures.CacheApi
        | WebApiFeatures.FetchEvents
        | WebApiFeatures.Workers;

    internal WorkerRequest(
        Engine parent,
        string specifier,
        string? referencingLocation,
        WorkerType type,
        string name,
        int depth,
        int liveWorkerCount,
        CancellationToken terminationToken)
    {
        Parent = parent;
        Specifier = specifier;
        ReferencingLocation = referencingLocation;
        Type = type;
        Name = name;
        Depth = depth;
        LiveWorkerCount = liveWorkerCount;
        TerminationToken = terminationToken;
    }

    /// <summary>
    /// The engine that ran <c>new Worker(...)</c>. It is suspended mid-statement: read it, do not run it.
    /// </summary>
    /// <remarks>
    /// <c>Parent.HostDefined</c> is how a provider shared by a pool of engines reaches the state
    /// belonging to <i>this</i> request — the tenant, the loader root, the remaining budget.
    /// </remarks>
    public Engine Parent { get; }

    /// <summary>
    /// The first argument to <c>new Worker(...)</c>, verbatim and unresolved.
    /// </summary>
    /// <remarks>
    /// Jint does not URL-parse it, so the specification's <c>SyntaxError</c> for an unparseable URL becomes
    /// whatever the worker's own module loader reports later, as a startup failure.
    /// </remarks>
    public string Specifier { get; }

    /// <summary>
    /// <c>Module.Location</c> of the module the constructor was reached from, or <see langword="null"/> when
    /// it was not reached from a module.
    /// </summary>
    /// <remarks>
    /// It travels so that a provider can resolve a relative <see cref="Specifier"/> the way <c>import()</c>
    /// would have. <c>ModuleFactory.LocationOf</c> is public for exactly this: a host naming a module itself
    /// has to reproduce the engine's own answer.
    /// </remarks>
    public string? ReferencingLocation { get; }

    /// <summary>
    /// The <c>type</c> option. <see cref="WorkerType.Module"/> is the only value — a classic worker is
    /// refused with a <c>TypeError</c> before a provider is ever called.
    /// </summary>
    public WorkerType Type { get; }

    /// <summary>
    /// The <c>name</c> option, or the empty string.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// How deep in a worker tree this one would sit: <c>0</c> for a worker created by a top-level engine, and
    /// greater only when a provider deliberately opted into nesting.
    /// </summary>
    /// <remarks>
    /// Nesting is off by default — <see cref="CreateDefaultOptions"/> neither enables
    /// <see cref="WebApiFeatures.Workers"/> nor copies the provider — so a provider that sees a non-zero value
    /// here is seeing the consequence of its own opt-in, and this together with
    /// <see cref="LiveWorkerCount"/> and its own process-wide counter is what bounds the tree.
    /// </remarks>
    public int Depth { get; }

    /// <summary>
    /// How many live worker connections the parent engine already has.
    /// </summary>
    /// <remarks>
    /// <c>Options.WebApi.Workers.MaxWorkers</c> is a per-engine backstop the engine enforces before this
    /// request is built; this value is what lets a provider apply a policy of its own — a process-wide budget,
    /// a per-tenant cap — which is the only kind that can bound a whole tree.
    /// </remarks>
    public int LiveWorkerCount { get; }

    /// <summary>
    /// Cancelled when anything but the worker itself ends the connection — <c>terminate()</c>,
    /// <see cref="WorkerConnection.End"/>, a <c>RestoreGlobalSnapshot</c> or <c>Dispose</c> on either side. A
    /// worker's own <c>close()</c> ends the connection <i>without</i> cancelling it: its teardown runs as a
    /// job on the worker's own loop — the very thread that pumps — so the pump loop observes
    /// <see cref="WorkerConnection.IsEnded"/> on its next iteration anyway, and cancelling would make any
    /// straggler job a host pumps afterwards erupt as <c>ExecutionCanceledException</c> from a close that
    /// deserved a quiet end.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is minted before the provider is called precisely so that it can be registered on options the
    /// provider has not built yet, which is what <see cref="CreateDefaultOptions"/> does. A worker engine that
    /// does not observe it is refused at construction: a worker that is deaf and mute but still burning a
    /// thread is the most dangerous silent failure this feature could have, so it is a construction-time error
    /// instead.
    /// </para>
    /// <para>
    /// It is also the token a pump loop should wait on, so that a <c>terminate()</c> from the parent's thread
    /// wakes the worker's thread rather than leaving it parked until its own ceiling elapses.
    /// </para>
    /// </remarks>
    public CancellationToken TerminationToken { get; }

    /// <summary>
    /// Builds a <b>fresh</b> <see cref="Options"/> instance pre-wired for this worker — never the parent's,
    /// and a new one on every call, so a provider may keep, mutate or discard the result freely.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What it does, in order:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// registers <see cref="TerminationToken"/> as a cancellation constraint, so <c>terminate()</c> stops a
    /// running worker rather than only closing its ports;
    /// </description></item>
    /// <item><description>
    /// replays every constraint <b>factory</b> the parent registered, so the worker is bounded the way the
    /// parent is while each engine gets its <i>own</i> constraint instances;
    /// </description></item>
    /// <item><description>
    /// copies the parent's restrictive security posture (<c>Options.CopySecurityPosture</c>: the seven
    /// <see cref="Options.ConstraintOptions"/> value settings, <c>Host.StringCompilationAllowed</c>,
    /// <see cref="Options.AgentCanSuspend"/>, <c>Json.MaxParseDepth</c>, the parser bounds
    /// (<c>Parsing.MaxSourceLength</c>, <c>Parsing.MaxNodeCount</c>), the four module-graph limits and
    /// <see cref="Options.ResultLimits"/>);
    /// </description></item>
    /// <item><description>
    /// sets <c>WebApi.Features</c> to the parent's set minus every grant a worker never inherits, plus
    /// <see cref="WebApiFeatures.Messaging"/> and <see cref="WebApiFeatures.GlobalEvents"/>, which the worker
    /// global is built out of;
    /// </description></item>
    /// <item><description>
    /// installs <see cref="DiagnosticsSink.Null"/>, which is what turns an exception escaping a worker's timer
    /// callback or event listener from something that erupts from the host's pump into something that is
    /// reported and survived — HTML's model for those callbacks — and what the parent-side error relay reads.
    /// A provider that installs a sink of its own keeps that behaviour; a provider that clears it gets a worker
    /// whose callback errors erupt from the host's pump and never reach the parent.
    /// </description></item>
    /// </list>
    /// <para>
    /// <b>Restrictions travel; grants never travel by implication.</b> Whatever narrows what script may do is
    /// copied, on the rule that a worker may not be a way around a hardened parent — <c>new Worker()</c> would
    /// otherwise be an <c>eval</c> escape hatch the moment a parent turned <c>StringCompilationAllowed</c>
    /// off. Whatever <i>grants</i> a capability is not: the network, storage, routing and CLR interop are the
    /// provider's to give deliberately, and so is <c>Strict</c>, the module loader and — see
    /// <see cref="Depth"/> — the ability to create further workers, which is why
    /// <c>WebApi.Workers.Provider</c> is deliberately left <see langword="null"/> here.
    /// </para>
    /// <para>
    /// <b>Constraint instances are never copied</b>, only factories. A <c>Constraint</c> carries
    /// per-execution state (a statement counter, an allocation baseline, a deadline) and is documented
    /// single-engine-only, so sharing one between a parent and a worker on two threads would share one counter
    /// and let either engine's reset rewind the other's. That is no longer merely a documented rule for the
    /// one constraint where sharing corrupts an accounting rather than a count:
    /// <see cref="Jint.Constraints.MemoryLimitConstraint"/> refuses a second engine outright — its
    /// <c>Attach</c> throws <c>InvalidOperationException</c> ("can only be registered with one Engine …
    /// register a constraint factory when Options is shared") — so an implementation that copied instances
    /// here would fail loudly on the second engine rather than silently share a budget. A parent that
    /// registered an instance directly therefore gets an <i>unbounded</i> worker; the fix is one line —
    /// register a factory instead (<c>options.AddConstraint(() =&gt; new MyConstraint())</c>), which every
    /// built-in constraint extension already does.
    /// </para>
    /// <para>
    /// <b>What a replayed constraint is actually worth on an engine that is only ever pumped.</b>
    /// <c>ProcessTasks</c> runs jobs raw rather than through <c>ExecuteWithConstraints</c>, so nothing calls
    /// <c>ResetConstraints()</c> between them: a replayed <c>TimeoutInterval</c> <b>never fires</b> (its
    /// deadline stays at the unarmed sentinel) and <c>MaxStatements</c> becomes a <i>lifetime</i> budget that
    /// eventually throws forever rather than a per-run one. <b>Memory is the exception</b>: every event-loop
    /// job runs inside an allocation segment and is checked as it completes, carrying the operation state
    /// captured when the job was registered across continuations and thread hops, so a replayed
    /// <c>LimitMemory</c> genuinely bounds each job chain on a pumped worker. The worker budget is therefore a
    /// pair — <see cref="Jint.Constraints.OperationDeadlineConstraint"/> for wall-clock and
    /// <see cref="Jint.Constraints.MemoryLimitConstraint"/> for allocations, both armed once by the host with
    /// <c>Begin</c>/<c>End</c> and both surviving every per-entry reset — while the cancellation constraint
    /// registered above handles termination.
    /// </para>
    /// <para>
    /// It is a convenience, not a security boundary — see <see cref="WorkerProvider"/>.
    /// </para>
    /// </remarks>
    /// <returns>A new <see cref="Options"/> instance, owned by the caller.</returns>
    public Options CreateDefaultOptions()
    {
        var parentOptions = Parent.Options;
        var options = new Options();

        // First, so that it is the registration Constraints.Find<CancellationConstraint>() answers with: the
        // construction-time validation looks for exactly this token, and the idle waits observe the first
        // cancellation constraint they find. A cancellation constraint the parent registered itself is
        // replayed below and sits beside it — a parent that cancels its own token stops its workers too,
        // which is a restriction travelling and not a grant.
        options.ObserveCancellation(TerminationToken);

        // Factories, never instances. ConstraintFactories is internal, so this is the only place the replay
        // can happen at all: a provider cannot do it for itself, which is one reason the request offers it.
        var factories = parentOptions.Constraints.ConstraintFactories;
        for (var i = 0; i < factories.Count; i++)
        {
            options.Constraints.ConstraintFactories.Add(factories[i]);
        }

        Options.CopySecurityPosture(parentOptions, options);

        // The engine's own closure rather than the options' set: what a worker inherits is what its parent
        // actually carries, which a live WebApi.Enable call may have grown.
        options.WebApi.Features =
            (Parent.WebApi.Features & ~NeverInheritedByAWorker)
            | WebApiFeatures.Messaging
            | WebApiFeatures.GlobalEvents;

        options.WebApi.Diagnostics.Sink = DiagnosticsSink.Null;

        return options;
    }
}

/// <summary>
/// The kind of script a worker runs. Requires .NET 8 or higher.
/// </summary>
public enum WorkerType
{
    /// <summary>
    /// An ES module, loaded by the worker engine's own <c>IModuleLoader</c> — <c>{ type: 'module' }</c>, and
    /// the only kind Jint runs.
    /// </summary>
    /// <remarks>
    /// The HTML Standard's own default is <c>'classic'</c> and Jint refuses it, which is policy rather than a
    /// licence the standard grants: there is no classic-script loader to run one with, a synchronous
    /// fetch-and-execute inside a statement is the one thing this feature family refuses, and every
    /// non-browser runtime that ships workers at all has converged on module workers. A host that must run a
    /// legacy classic worker installs its own <c>importScripts</c> and owns the blocking read.
    /// </remarks>
    Module,
}
#endif
