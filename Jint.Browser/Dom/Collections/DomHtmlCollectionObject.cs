using Jint.HtmlParser;
using Jint.Native;
using Jint.Native.Array;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// The wrapper for an <c>HTMLCollection</c> and its refinements (<c>HTMLFormControlsCollection</c>,
/// <c>HTMLOptionsCollection</c>, <c>HTMLAllCollection</c>).
/// </summary>
/// <remarks>
/// It is the one collection the generated <see cref="DomCollectionAccessor"/> scheme cannot serve, because
/// the former generic HTML collection is generic and <b>invariant</b>: an
/// <c>IHtmlCollection&lt;IHtmlOptionElement&gt;</c> is not an <c>IHtmlCollection&lt;Element&gt;</c>, so one
/// non-generic accessor could not reach the indexer at all. A generated member instead names its declared
/// element type at the call site — <c>realm.WrapCollection&lt;IHtmlOptionElement&gt;(…)</c> — which keeps the
/// path free of reflection and of a generic instantiation nothing static can see.
/// </remarks>
internal sealed class DomHtmlCollectionObject<T> : DomCollectionBase where T : Node
{
    private readonly DomHtmlCollection<T> _collection;
    private List<string> _names = [];
    private WeakReference<Document>? _countOwner;
    private ulong _countStamp;
    private uint _count;

    internal DomHtmlCollectionObject(DomRealm realm, DomInterfaceDefinition definition, DomHtmlCollection<T> collection)
        : base(realm, definition, collection)
    {
        _collection = collection;
    }

    /// <inheritdoc />
    public override uint Length
    {
        get
        {
            while (true)
            {
                DomRealm.CancellationToken.ThrowIfCancellationRequested();
                DomRealm.Engine.Constraints.Check();
                DomRealm.CancellationToken.ThrowIfCancellationRequested();
                if (!_collection.TryGetCountWitness(out var owner, out var stamp))
                    return (uint) _collection.GetLength(DomRealm);

                if (_countOwner is not null && _countOwner.TryGetTarget(out var cachedOwner)
                    && ReferenceEquals(owner, cachedOwner) && stamp == _countStamp)
                {
                    var count = _count;
                    DomRealm.Engine.Constraints.Check();
                    DomRealm.CancellationToken.ThrowIfCancellationRequested();
                    if (_collection.TryGetCountWitness(out var afterOwner, out var afterStamp)
                        && ReferenceEquals(owner, afterOwner) && stamp == afterStamp)
                        return count;
                    continue;
                }

                _countOwner = null;
                var computed = (uint) _collection.GetLength(DomRealm);
                DomRealm.CancellationToken.ThrowIfCancellationRequested();
                if (!_collection.TryGetCountWitness(out var finalOwner, out var finalStamp)) return computed;
                // Host checks may mutate or reenter. Publish only a count measured under one witness;
                // adoption changes the identity even if the two documents happen to have equal stamps.
                if (!ReferenceEquals(owner, finalOwner) || stamp != finalStamp) continue;
                _countOwner = new WeakReference<Document>(owner!);
                _countStamp = stamp;
                _count = computed;
                return computed;
            }
        }
    }

    /// <inheritdoc />
    protected override bool IgnoreNamedPropertiesInSet => true;

    /// <inheritdoc />
    public override bool TryGetIndex(uint index, out JsValue value)
    {
        var element = ElementAt(index);

        if (element is null)
        {
            value = JsValue.Undefined;
            return false;
        }

        value = DomRealm.Wrap(element);
        return true;
    }

    /// <inheritdoc />
    protected override bool HasIndex(uint index) => ElementAt(index) is not null;

    /// <summary>
    /// The <paramref name="index"/>th element, in <b>one</b> pass over the collection, or
    /// <see langword="null"/> when it has none there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why no <c>Length</c> check stands in front of it.</b> Every implementation of
    /// <c>IHtmlCollection&lt;T&gt;</c> this wrapper is given is a lazy view over a tree walk rather than a
    /// list. AngleSharp's <c>HtmlCollection&lt;T&gt;</c> — <c>children</c>, and the snapshot collections —
    /// holds an <c>IEnumerable&lt;T&gt;</c> whose <c>Length</c> is <c>Count()</c> and whose indexer is a
    /// linear <c>GetItemByIndex</c>; its <c>HtmlFormControlsCollection</c> (<c>form.elements</c>) is a
    /// <c>Where</c> over the document's form-control descendants; and the binding's own
    /// <see cref="DomLiveHtmlCollection"/> re-runs its filter. So the bounds pre-check this method replaced
    /// ran the entire query a second time on every element read — 36.3% of the <c>GetDescendantsAndSelf</c>
    /// subtree in the profile on
    /// <a href="https://github.com/sebastienros/jint/issues/4013">sebastienros/jint#4013</a>. Running out of
    /// elements <i>is</i> the bounds answer, and it comes free with the walk that had to happen anyway.
    /// </para>
    /// <para>
    /// The collections that really can be indexed in constant time — <c>childNodes</c>, <c>attributes</c>, a
    /// token list — are not these. They reach <see cref="DomCollectionObject"/> and its generated accessor,
    /// whose length probe is a field read, and it stays where it is.
    /// </para>
    /// </remarks>
    private Element? ElementAt(uint index) => _collection.GetItem(DomRealm, index) as Element;

    /// <summary>
    /// https://dom.spec.whatwg.org/#interface-htmlcollection — the supported property names are every
    /// element's non-empty <c>id</c> plus every HTML element's non-empty <c>name</c>, in tree order, without
    /// duplicates. The standard says "neither the empty string nor already in result" of both, which is the
    /// half of the rule <see cref="TryGetNamedValue"/> carries the other half of. An ordinary own property
    /// is listed by the base class instead of being projected here.
    /// </summary>
    protected override int NameCount
    {
        get
        {
            _names = VisibleNames();
            return _names.Count;
        }
    }

    /// <inheritdoc />
    protected override string NameAt(int index) => _names[index];

    /// <summary>
    /// <c>HTMLCollection</c> and <c>HTMLFormControlsCollection</c> both carry
    /// <a href="https://webidl.spec.whatwg.org/#LegacyUnenumerableNamedProperties">[LegacyUnenumerableNamedProperties]</a>
    /// in their own specifications: <c>'username' in form.elements</c> is true and
    /// <c>Object.keys(form.elements)</c> is the indices alone.
    /// </summary>
    protected override bool IsNameEnumerable(string name) => false;

    /// <summary>
    /// https://webidl.spec.whatwg.org/#dfn-named-property-visibility: an ordinary own property wins over
    /// a supported name, including when the matching element appeared after the property was created.
    /// </summary>
    protected override bool TryGetNamedValue(string name, out JsValue value)
    {
        if (HasStoredProperty(name))
        {
            value = JsValue.Undefined;
            return false;
        }

        value = NamedItem(name);
        if (value.IsNull())
        {
            value = JsValue.Undefined;
            return false;
        }

        return true;
    }

    // Query only ObjectInstance's ordinary property bag: GetOwnProperty would recurse through this projection.
    private bool HasStoredProperty(string name) => base.TryGetProperty(name, out _);

    /// <summary>
    /// <a href="https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#dom-htmlcollection-nameditem">HTML's
    /// <c>namedItem</c></a>, whose first step is the empty string and whose second is the element lookup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The search is written out rather than delegated to AngleSharp's <c>this[string]</c>, because HTML's
    /// second step is "the <b>first</b> element for which <i>either</i> its ID is key, <i>or</i> it is in the
    /// HTML namespace and its <c>name</c> content attribute is key" — one pass in tree order, and the
    /// <c>name</c> half restricted to HTML elements. AngleSharp matches <c>name</c> on any element and does
    /// so in a second pass after every id, so <c>document.createElementNS("", "img")</c> with
    /// <c>name="qux"</c> answered from a collection that must not expose it. That is what made this operation
    /// and <see cref="VisibleNames"/> disagree about one object, which
    /// <c>dom/nodes/Element-children.html</c> asserts they never do.
    /// </para>
    /// <para>
    /// The empty-name check is HTML's first step, and it comes first because an element carrying
    /// <c>id=""</c> or <c>name=""</c> would otherwise match. This operation looks through expandos; only
    /// property lookup applies the visibility check.
    /// </para>
    /// </remarks>
    internal override JsValue NamedItem(string name)
    {
        if (_collection.GetNamedItem(DomRealm, name) is { } specialized) return specialized;
        if (name.Length == 0)
        {
            return JsValue.Null;
        }

        var work = new DomReadWork(DomRealm.NativeReadCheckpoint, DomRealm.CancellationToken);
        work.Check();
        foreach (var candidate in _collection.Read(DomRealm))
        {
            var element = (Element) (Node) candidate;
            if (work.Equal(work.Attribute(element, "id"), name)
                || (element.NamespaceUri == Namespaces.Html && work.Equal(work.Attribute(element, "name"), name)))
            {
                work.Check();
                return DomRealm.Wrap(element);
            }
        }
        work.Check();
        return JsValue.Null;
    }

    /// <remarks>
    /// The duplicate check is a set rather than <c>names.Contains(…, StringComparer.Ordinal)</c>, which is
    /// the LINQ overload: it is linear in the names found so far <em>and</em> allocates an enumerator per
    /// candidate, so listing the names of a collection of <i>n</i> named elements cost O(n²) comparisons and
    /// 2n allocations. The list is still what carries the order, which is the one thing the set cannot.
    /// </remarks>
    private List<string> VisibleNames()
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var work = new DomReadWork(DomRealm.NativeReadCheckpoint, DomRealm.CancellationToken);
        work.Check();

        foreach (var candidate in _collection.Read(DomRealm))
        {
            var element = (Element) (Node) candidate;
            Add(names, work.Attribute(element, "id"));

            if (element.NamespaceUri == Namespaces.Html)
            {
                Add(names, work.Attribute(element, "name"));
            }
        }

        work.Check();
        return names;

        void Add(List<string> names, string? candidate)
        {
            if (candidate is not null) foreach (var unused in candidate) work.Step();
            // The base class lists an ordinary own property itself, in property-bag order. Do not also
            // advertise a projected name for it, or enumeration and lookup would disagree.
            if (!string.IsNullOrEmpty(candidate)
                && seen.Add(candidate!)
                // A supported name spelling a canonical array index is unreachable as a property: the indexed
                // half of the model answers that key and stops, which is why WebIDL leaves such a name out of
                // [[OwnPropertyKeys]] and why ArrayLikeObject refuses to advertise one. namedItem still finds
                // it. Without this, <div id="0"> made Object.keys() of a collection raise under
                // host-contract verification, and listed a key that read as the element at index 0 without.
                && ArrayInstance.ParseArrayIndex(candidate!) == uint.MaxValue
                && !HasStoredProperty(candidate!))
            {
                names.Add(candidate!);
            }
        }
    }
}
