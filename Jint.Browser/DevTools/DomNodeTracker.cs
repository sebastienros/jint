using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.Browser.Runtime;
using Jint.Runtime;

namespace Jint.Browser.DevTools;

/// <summary>
/// The two identifiers the <c>DOM</c> domain addresses a node by, and the mutations that keep them current.
/// </summary>
/// <remarks>
/// <para>
/// <b>A <c>nodeId</c> is a document's and a <c>backendNodeId</c> is a page's.</b> The first is minted when a
/// node is sent to a client and is thrown away with the document, which is Chrome's own split; the second is
/// keyed on the native node or attribute object in a <see cref="ConditionalWeakTable{TKey,TValue}"/>, so a node keeps the
/// same identifier for as long as it exists whether or not anything else remembers it.
/// </para>
/// <para>
/// <b>The reverse lookups are strong, and that is the same promise a handle makes.</b> A client that was
/// given an identifier can come back with it, so the tables that turn one back into a node hold the node —
/// exactly as <c>RemoteObjectTable</c> holds a value. They are emptied when a document commits, so what one
/// page can accumulate is bounded by the document it is showing rather than by everything it has ever shown,
/// and a client that walked a whole tree with <c>getDocument(-1)</c> is holding that tree on purpose.
/// </para>
/// <para>
/// <b>One table per page, shared by every attachment</b>, which is the decision
/// <c>Jint.DevTools/Domains/AGENTS.md</c> already made for <c>RemoteObjectTable</c>: two clients addressing
/// one document address it by the same identifiers. What is <i>not</i> shared is which nodes a client has
/// been <i>sent</i> — that is each <see cref="DomDomain"/>'s own set, and it is what decides which mutation
/// events reach it.
/// </para>
/// <para>
/// <b>Identifiers keep climbing across documents.</b> The counters are process-wide, so a <c>nodeId</c> from
/// the document before last fails to resolve rather than landing on a node of the one that replaced it — the
/// same reason the remote-object table is seeded from a serial.
/// </para>
/// <para>
/// <b>Mutations arrive through native subscriptions and are delivered on the engine's queue.</b> One
/// <c>MutationSubscription</c> over the whole document, registered when a client first enables the domain and
/// again for each document after it, parks its records; one job per batch turns them into
/// <c>childNodeInserted</c>, <c>childNodeRemoved</c>, <c>attributeModified</c> and their kind. That is the
/// same lane <c>Observers/MutationObserverLane</c> delivers a page's own observers on, so a client and a
/// page's <c>MutationObserver</c> see one document at the same checkpoint.
/// </para>
/// <para>
/// Everything here runs on the page loop.
/// </para>
/// </remarks>
internal sealed class DomNodeTracker : IDisposable
{
    private static int _serial;

    private readonly ConditionalWeakTable<object, Identifier> _backendIds = new();
    private readonly Dictionary<int, object> _byNodeId = [];
    private readonly Dictionary<object, int> _nodeIds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, object> _byBackendId = [];

    private readonly object _domainGate = new();
    private DomDomain[] _domains = [];

    private MutationSubscription? _observer;
    private PageRuntime? _runtime;
    private Document? _observed;
    private bool _scheduled;

    void IDisposable.Dispose() => DocumentReplaced();

    /// <summary>Whether at least one attachment has the domain enabled, which is what arms the observer.</summary>
    private bool Wanted
    {
        get
        {
            foreach (var domain in Volatile.Read(ref _domains))
            {
                if (domain.IsEnabled)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Registers one attachment's domain, so it hears the mutations of nodes it has been sent.</summary>
    internal void Add(DomDomain domain)
    {
        lock (_domainGate)
        {
            _domains = [.. _domains, domain];
        }
    }

    /// <summary>Stops telling one attachment's domain anything, which detaching does.</summary>
    internal void Remove(DomDomain domain)
    {
        lock (_domainGate)
        {
            _domains = [.. _domains.Where(candidate => !ReferenceEquals(candidate, domain))];
        }
    }

    /// <summary>The identifier <paramref name="node"/> keeps for as long as it exists.</summary>
    /// <remarks>
    /// Both kinds of identifier come from one process-wide sequence, so a <c>nodeId</c> and a
    /// <c>backendNodeId</c> are never the same number — which makes a client that confused the two fail
    /// loudly rather than resolve to the wrong node.
    /// </remarks>
    internal int BackendIdOf(object node)
    {
        if (node is not (Node or Attr)) throw new ArgumentException("Expected a native node or attribute.", nameof(node));
        if (!_backendIds.TryGetValue(node, out var identifier))
        {
            identifier = new Identifier(Interlocked.Increment(ref _serial));
            _backendIds.Add(node, identifier);
        }

        _byBackendId[identifier.Value] = node;
        return identifier.Value;
    }

    /// <summary>The identifier <paramref name="node"/> is addressed by in this document, minting one.</summary>
    internal int IdOf(object node)
    {
        if (node is not (Node or Attr)) throw new ArgumentException("Expected a native node or attribute.", nameof(node));
        if (_nodeIds.TryGetValue(node, out var existing))
        {
            return existing;
        }

        var id = Interlocked.Increment(ref _serial);
        _nodeIds[node] = id;
        _byNodeId[id] = node;
        BackendIdOf(node);
        return id;
    }

    /// <summary>The identifier <paramref name="node"/> already has, or zero when it has none.</summary>
    /// <remarks>
    /// Zero is Chrome's own answer for a node the front end has not been sent, and <c>DOM.describeNode</c> is
    /// what answers it: describing a node is not the same as pushing one, so a client that only ever
    /// describes never grows a node table.
    /// </remarks>
    internal int KnownIdOf(object node) => _nodeIds.TryGetValue(node, out var id) ? id : 0;

    /// <summary>The node <paramref name="nodeId"/> names, or <see langword="null"/>.</summary>
    internal object? ByNodeId(int nodeId) => _byNodeId.TryGetValue(nodeId, out var node) ? node : null;

    /// <summary>The node <paramref name="backendNodeId"/> names, or <see langword="null"/>.</summary>
    internal object? ByBackendId(int backendNodeId)
        => _byBackendId.TryGetValue(backendNodeId, out var node) ? node : null;

    /// <summary>
    /// Throws away everything about the document that has gone, which committing a new one does.
    /// </summary>
    internal void DocumentReplaced()
    {
        _byNodeId.Clear();
        _nodeIds.Clear();
        _byBackendId.Clear();
        _scheduled = false;

        _observer?.Dispose();
        _observer = null;
        _observed = null;
        _runtime = null;
    }

    /// <summary>
    /// Watches <paramref name="runtime"/>'s document for mutations, if a client wants them and it is not
    /// already watched.
    /// </summary>
    internal void Watch(PageRuntime runtime)
    {
        _runtime = runtime;

        if (!Wanted || runtime.Document is not { } document || ReferenceEquals(document, _observed))
        {
            return;
        }

        _observer?.Dispose();
        _scheduled = false;
        _observer = new MutationSubscription { PendingRecord = OnRecords };
        _observer.Observe(document, new MutationObserverOptions
        {
            ChildList = true,
            Subtree = true,
            Attributes = true,
            CharacterData = true,
            AttributeOldValue = false,
            CharacterDataOldValue = false,
        });

        _observed = document;
    }

    /// <summary>
    /// What the native subscription calls, synchronously, from inside the mutation. Nothing but bookkeeping happens here —
    /// no script runs, and no protocol event is written — so re-entering the DOM operation that is still
    /// running is safe.
    /// </summary>
    private void OnRecords(MutationSubscription source)
    {
        if (_scheduled || _runtime is not { } runtime || !ReferenceEquals(source, _observer)) return;
        _scheduled = true;
        // DOM's observer checkpoint, shared with script observers. A queued job for a replaced
        // document must neither drain the new subscription nor clear its scheduling flag.
        runtime.Engine.AddToEventLoop(() => Deliver(source), EventLoopJobKind.Microtask);
    }

    private void Deliver(MutationSubscription source)
    {
        if (!ReferenceEquals(source, _observer)) return;
        _scheduled = false;
        var batch = source.TakeRecordsForDelivery();
        foreach (var domain in Volatile.Read(ref _domains)) domain.Mutated(batch);
    }

    /// <summary>One node's backend identifier, boxed so the weak table can hold it.</summary>
    private sealed class Identifier(int value)
    {
        internal int Value { get; } = value;
    }
}
