using System.Runtime.CompilerServices;
using AngleSharp;
using AngleSharp.Dom;
using Jint.Browser.Runtime;

namespace Jint.Browser.DevTools;

/// <summary>
/// The two identifiers the <c>DOM</c> domain addresses a node by, and the mutations that keep them current.
/// </summary>
/// <remarks>
/// <para>
/// <b>A <c>nodeId</c> is a document's and a <c>backendNodeId</c> is a page's.</b> The first is minted when a
/// node is sent to a client and is thrown away with the document, which is Chrome's own split; the second is
/// keyed on the AngleSharp object in a <see cref="ConditionalWeakTable{TKey,TValue}"/>, so a node keeps the
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
/// <b>Mutations arrive through AngleSharp and are delivered on the engine's queue.</b> One
/// <c>MutationObserver</c> over the whole document, registered when a client first enables the domain and
/// again for each document after it, parks its records; one job per batch turns them into
/// <c>childNodeInserted</c>, <c>childNodeRemoved</c>, <c>attributeModified</c> and their kind. That is the
/// same lane <c>Observers/MutationObserverLane</c> delivers a page's own observers on, so a client and a
/// page's <c>MutationObserver</c> see one document at the same checkpoint.
/// </para>
/// <para>
/// Everything here runs on the page loop.
/// </para>
/// </remarks>
internal sealed class DomNodeTracker
{
    private static int _serial;

    private readonly ConditionalWeakTable<INode, Identifier> _backendIds = new();
    private readonly Dictionary<int, INode> _byNodeId = [];
    private readonly Dictionary<INode, int> _nodeIds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, INode> _byBackendId = [];
    private readonly List<IMutationRecord> _records = [];
    private readonly Action _deliver;

    private readonly object _domainGate = new();
    private DomDomain[] _domains = [];

    private MutationObserver? _observer;
    private PageRuntime? _runtime;
    private IDocument? _observed;
    private bool _scheduled;

    internal DomNodeTracker()
    {
        _deliver = Deliver;
    }

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
    internal int BackendIdOf(INode node)
    {
        if (!_backendIds.TryGetValue(node, out var identifier))
        {
            identifier = new Identifier(Interlocked.Increment(ref _serial));
            _backendIds.Add(node, identifier);
        }

        _byBackendId[identifier.Value] = node;
        return identifier.Value;
    }

    /// <summary>The identifier <paramref name="node"/> is addressed by in this document, minting one.</summary>
    internal int IdOf(INode node)
    {
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
    internal int KnownIdOf(INode node) => _nodeIds.TryGetValue(node, out var id) ? id : 0;

    /// <summary>The node <paramref name="nodeId"/> names, or <see langword="null"/>.</summary>
    internal INode? ByNodeId(int nodeId) => _byNodeId.TryGetValue(nodeId, out var node) ? node : null;

    /// <summary>The node <paramref name="backendNodeId"/> names, or <see langword="null"/>.</summary>
    internal INode? ByBackendId(int backendNodeId)
        => _byBackendId.TryGetValue(backendNodeId, out var node) ? node : null;

    /// <summary>
    /// Throws away everything about the document that has gone, which committing a new one does.
    /// </summary>
    internal void DocumentReplaced()
    {
        _byNodeId.Clear();
        _nodeIds.Clear();
        _byBackendId.Clear();
        _records.Clear();

        _observer?.Disconnect();
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

        _observer?.Disconnect();

        // Every flag on, because a client that enabled the domain wants the whole document: AngleSharp
        // resolves each of them itself and none of its own defaulting runs when all are passed.
        _observer = new MutationObserver(OnRecords);
        _observer.Connect(
            document,
            childList: true,
            subtree: true,
            attributes: true,
            characterData: true,
            attributeOldValue: false,
            characterDataOldValue: false,
            attributeFilter: null);

        _observed = document;
    }

    /// <summary>
    /// What AngleSharp calls, synchronously, from inside the mutation. Nothing but bookkeeping happens here —
    /// no script runs, and no protocol event is written — so re-entering the DOM operation that is still
    /// running is safe.
    /// </summary>
    private void OnRecords(IEnumerable<IMutationRecord> records, MutationObserver source)
    {
        foreach (var record in records)
        {
            _records.Add(record);
        }

        if (_records.Count == 0 || _scheduled || _runtime is not { } runtime)
        {
            return;
        }

        _scheduled = true;
        runtime.Engine.AddToEventLoop(_deliver);
    }

    private void Deliver()
    {
        _scheduled = false;

        if (_records.Count == 0)
        {
            return;
        }

        // A copy, because a listener a domain's event reaches cannot mutate the DOM — nothing here runs
        // script — but a record arriving while this runs belongs to the next batch all the same.
        var batch = _records.ToArray();
        _records.Clear();

        foreach (var domain in Volatile.Read(ref _domains))
        {
            domain.Mutated(batch);
        }
    }

    /// <summary>One node's backend identifier, boxed so the weak table can hold it.</summary>
    private sealed class Identifier(int value)
    {
        internal int Value { get; } = value;
    }
}
