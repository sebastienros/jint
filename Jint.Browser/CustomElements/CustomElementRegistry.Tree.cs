using AngleSharp.Dom;

namespace Jint.Browser.CustomElements;

/// <summary>
/// Where a reaction comes from: AngleSharp's mutation records for the tree, and its
/// <c>IAttributeObserver</c> service for an attribute.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two channels, because neither covers the other's half.</b> A mutation record is what says an element
/// entered or left the document — the observer is registered on the document, so a record only ever arrives
/// for a node in the document tree, which is exactly when connectedness changes. But
/// <c>Document.QueueMutation</c> walks a node's inclusive ancestors, so an attribute written on a
/// <i>detached</i> element produces no record at all, and <c>el.setAttribute</c> before insertion is the
/// commonest thing a component does. The <c>IAttributeObserver</c> service is called for every element with
/// an owner document, attached or not, which is what makes <c>attributeChangedCallback</c> answerable.
/// </para>
/// <para>
/// <b>Both are arrivals, never deliveries.</b> AngleSharp has no <c>IEventLoop</c> registered — the same
/// decision <c>Observers/JsMutationObserver</c> argues — so both callbacks run inline, inside the DOM
/// operation that caused them and on whichever thread that operation was on. They enqueue; the drain decides
/// whether anything runs now, and it refuses to run script on the parser's thread.
/// </para>
/// <para>
/// <b>Two gaps this leaves, both AngleSharp's and both recorded in <c>Jint.Browser/Dom/AGENTS.md</c>.</b> A
/// write through <c>classList</c> notifies neither channel, so an observed <c>class</c> attribute changed
/// that way reports nothing; and a namespaced <c>setAttributeNS</c> notifies only the record channel, so it
/// reports only for a connected element. Every ordinary attribute write — <c>setAttribute</c>,
/// <c>removeAttribute</c>, <c>id</c>, <c>className</c>, an attribute node — reaches the service.
/// </para>
/// </remarks>
internal sealed partial class CustomElementRegistry
{
    private MutationObserver? _tree;

    /// <summary>
    /// Starts watching <paramref name="document"/> for insertions and removals, once, and only once
    /// something has been defined.
    /// </summary>
    internal void EnsureWatching(IDocument document)
    {
        if (_tree is not null)
        {
            return;
        }

        _tree = new MutationObserver(OnTreeMutations);
        _tree.Connect(
            document,
            childList: true,
            subtree: true,
            attributes: false,
            characterData: false,
            attributeOldValue: false,
            characterDataOldValue: false,
            attributeFilter: null);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-node-insert and #concept-node-remove, as AngleSharp reports them:
    /// what entered the document is upgraded or connected, and what left it is disconnected.
    /// </summary>
    /// <remarks>
    /// A record only arrives for a target inside the observed document, so everything in
    /// <c>addedNodes</c> is connected now and everything in <c>removedNodes</c> was connected a moment ago —
    /// which is why neither list is re-tested.
    /// </remarks>
    private void OnTreeMutations(IEnumerable<IMutationRecord> records, MutationObserver source)
    {
        foreach (var record in records)
        {
            if (record.Removed is { } removed)
            {
                foreach (var node in removed)
                {
                    Walk(node, Disconnected);
                }
            }

            if (record.Added is { } added)
            {
                foreach (var node in added)
                {
                    Walk(node, Connected);
                }
            }
        }

        Drain();
    }

    private void Connected(IElement element)
    {
        if (TryGetRecord(element) is { State: CustomElementState.Custom } record)
        {
            EnqueueCallback(element, record, CustomElementReactionKind.Connected);
            return;
        }

        TryUpgrade(element);
    }

    private void Disconnected(IElement element)
    {
        if (TryGetRecord(element) is { State: CustomElementState.Custom } record)
        {
            EnqueueCallback(element, record, CustomElementReactionKind.Disconnected);
        }
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#handle-attribute-changes — what AngleSharp's <c>IAttributeObserver</c>
    /// reports, turned into an <c>attributeChangedCallback</c> reaction for an observed name.
    /// </summary>
    /// <remarks>
    /// The service reports the element, the local name and the <b>new</b> value; the old one comes from the
    /// record's snapshot, which the upgrade seeded and every call since has kept current. A namespace is
    /// always reported as <see langword="null"/>, because the service does not carry one — see this file's
    /// remarks.
    /// </remarks>
    internal void AttributeChanged(IElement element, string name, string? value)
    {
        if (_byName.Count == 0 || TryGetRecord(element) is not { State: CustomElementState.Custom } record)
        {
            return;
        }

        if (record.Definition is not { } definition || !definition.Observes(name))
        {
            return;
        }

        var values = record.Attributes ??= new Dictionary<string, string?>(StringComparer.Ordinal);
        values.TryGetValue(name, out var old);
        values[name] = value;

        EnqueueCallback(element, record, CustomElementReactionKind.AttributeChanged, name, old, value);
        Drain();
    }

    /// <summary>
    /// Upgrades whatever a member just created — a parsed fragment, a clone — then runs the reactions
    /// before that member returns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the <i>detached</i> half of the picture. A connected element's <c>innerHTML</c> produces a
    /// mutation record, which upgraded and connected everything before AngleSharp's own call returned; a
    /// detached one produces none, and HTML upgrades there too — <c>div.innerHTML = '&lt;my-el&gt;'</c> on an
    /// element that is nowhere runs the constructor. So the subtree is walked here as well, which is a
    /// second, idempotent pass for the connected case: every element it finds is already custom.
    /// </para>
    /// <para>
    /// The walk is skipped outright when nothing has been defined, which is what keeps <c>innerHTML</c> free
    /// for every page that has no custom elements.
    /// </para>
    /// </remarks>
    internal static void SubtreeCreated(Dom.DomRealm realm, INode? root)
    {
        if (root is null || Of(realm.Engine) is not { HasDefinitions: true } registry)
        {
            return;
        }

        registry.UpgradeSubtree(root);
        registry.Drain();
    }
}

/// <summary>
/// The <c>IAttributeObserver</c> the page's parser configuration registers, so that an attribute written on
/// any element of the document — connected or not — can become an <c>attributeChangedCallback</c>.
/// </summary>
/// <remarks>
/// <para>
/// It is registered with <c>.With&lt;IAttributeObserver&gt;</c>, which <b>adds</b> a service rather than
/// replacing one: AngleSharp's own <c>DefaultAttributeObserver</c> stays in the list and goes on doing what
/// it does. Measured against the pinned 1.7.2 rather than assumed.
/// </para>
/// <para>
/// <b>It never runs script.</b> The parser calls it while building the tree, on the parser's thread, so
/// everything it does is a dictionary write and an enqueue; the reaction lane decides when a callback runs
/// and refuses to run one off the page loop.
/// </para>
/// </remarks>
internal sealed class CustomElementAttributeObserver : IAttributeObserver
{
    private readonly Runtime.PageRuntime _runtime;

    internal CustomElementAttributeObserver(Runtime.PageRuntime runtime)
    {
        _runtime = runtime;
    }

    /// <inheritdoc />
    public void NotifyChange(IElement host, string name, string? value)
        => _runtime.CustomElementsIfCreated?.AttributeChanged(host, name, value);
}
