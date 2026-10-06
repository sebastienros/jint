using System.Runtime.CompilerServices;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// Wraps an indexed DOM collection using the interface-specific generated collection accessor.
/// </summary>
/// <remarks>
/// <b>One class per interface family, and that is a performance contract as much as a tidiness one.</b> Every
/// indexed read a page makes on any DOM collection arrives at the interpreter's array-like lane as one
/// virtual <c>ArrayLikeObject.TryGetIndex</c>, and the JIT devirtualizes and inlines it from a class profile
/// holding a single guess. The wrapper classes a page's collections produce are therefore in direct
/// competition for that guess, and adding one for a shape an existing wrapper already served is not free for
/// the shapes that were already there: whichever collection warms the call site first wins, and the others
/// read every element through a cold indirect call with nothing inlined. Verified by disassembly on
/// <a href="https://github.com/sebastienros/jint/pull/4027">#4027</a>, where a second wrapper for the
/// <b>static</b> <c>NodeList</c> — a shape the live <c>NodeList</c> shares this class with — put exactly that
/// outcome within reach of a page that reads a selector match before it reads a <c>childNodes</c>. So a
/// collection wanting a different read strategy gets a branch inside the class that already serves its
/// interface, not a sibling of it; <see cref="TryGetStaticIndex"/> is the one such branch so far.
/// </remarks>
internal sealed class DomCollectionObject : DomCollectionBase, INamedPropertySupport
{
    private readonly DomCollectionAccessor _accessor;

    // Recomputed whenever an enumeration starts (which is always at NameCount) and read by NameAt, so that a
    // key list costs one walk of the collection rather than one per key. Deliberately not a cache with a
    // lifetime: a named getter is live, and a question asked outside an enumeration goes straight to the
    // accessor.
    private IReadOnlyList<string> _names = [];

    // A DomStaticNodeList target's nodes, and null for every other target -- which is the whole of what
    // "this collection is live" means here, and the one test the read path below makes. A field rather than
    // `DomTarget is DomStaticNodeList` so that the test the live lane pays is a load and a null branch,
    // rather than a null branch and a type-handle compare.
    private readonly HtmlParser.Node[]? _nodes;

    // One slot per index of that snapshot, filled on the index's first read. Allocated on the first indexed
    // read, so a match a page only takes the `length` of costs nothing for it. A null slot means "not read
    // yet", never "no element": every index below Length of a static list has a node.
    private ObjectInstance?[]? _elements;

    internal DomCollectionObject(DomRealm realm, DomInterfaceDefinition definition, object target, DomCollectionAccessor accessor)
        : base(realm, definition, target)
    {
        _accessor = accessor;
        _nodes = (target as DomStaticNodeList)?.Nodes;
    }

    /// <inheritdoc />
    public override uint Length => _nodes is not null ? (uint) _nodes.Length
        : DomTarget is DomNodeList list ? (uint) list.ReadLength(DomRealm.NativeReadCheckpoint, DomRealm.CancellationToken)
        : _accessor.Length(DomRealm, DomTarget);

    /// <inheritdoc />
    public override bool TryGetIndex(uint index, out JsValue value)
    {
        var nodes = _nodes;
        if (nodes is not null)
        {
            return TryGetStaticIndex(nodes, index, out value);
        }

        return _accessor.TryGetIndex(DomRealm, DomTarget, index, out value);
    }

    /// <summary>
    /// The element read of the one collection whose membership cannot change: the <b>static</b>
    /// <c>NodeList</c> of <a href="https://dom.spec.whatwg.org/#dom-parentnode-queryselectorall">DOM
    /// §4.2.6</a>, which therefore keeps the one element wrapper each of its indices answers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it caches, and why that is not a second identity.</b> The cached value <i>is</i> the canonical
    /// wrapper — the one <c>DomRealm.WrapNode</c> answers, which is the one the realm's
    /// <c>ConditionalWeakTable</c> holds for that node and the one every other route to the node returns. So
    /// <c>list[0] === document.querySelector(…)</c> and <c>list[0] === otherList[0]</c> stay true by
    /// construction: the array is a memo of a lookup, never a source of objects. What it removes is the
    /// lookup — the weak-table probe (<c>FindEntry</c>) the profile in
    /// <a href="https://github.com/sebastienros/jint/issues/4013">sebastienros/jint#4013</a> measured on the
    /// element read of <c>for (j = 0; j &lt; list.length; j++) if (list[j] === el)</c> — and, on a hit, the
    /// accessor's <c>object</c>-to-<c>INodeList</c> cast with it.
    /// </para>
    /// <para>
    /// <b>Why it adds no retention.</b> The snapshot holds its nodes strongly, and a live node's wrapper is
    /// already kept alive by the weak table keyed on it. The cache therefore names objects this wrapper's own
    /// <c>DomTarget</c> was keeping alive anyway, and it dies with the wrapper. Removing a cached node from
    /// the document changes nothing: the node is still in this static list, which is precisely what "static"
    /// means, and it still has one wrapper.
    /// </para>
    /// <para>
    /// <b>Why a live collection may not have one.</b> A per-index memo over a membership that moves would
    /// answer the wrong node, and a removed node it had cached would be pinned for the collection's life.
    /// That is why the lane is entered from <see cref="DomStaticNodeList"/> — a type the binding constructs
    /// for itself — rather than from anything read off an <c>INodeList</c>, which says nothing at all
    /// about whether it is live.
    /// </para>
    /// <para>
    /// Deliberately not inlined: this body's only caller is <see cref="TryGetIndex"/>, whose own body is what
    /// the interpreter's array-like lane devirtualizes and inlines on every <c>list[i]</c> of every
    /// collection. Keeping it out of that body leaves the live lane's inlinee one field load and one
    /// predicted branch larger than it was before this existed, rather than a memo's worth larger.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryGetStaticIndex(HtmlParser.Node[] nodes, uint index, out JsValue value)
    {
        if (index >= (uint) nodes.Length)
        {
            value = JsValue.Undefined;
            return false;
        }

        var elements = _elements ??= new ObjectInstance?[nodes.Length];
        var wrapper = elements[index];

        if (wrapper is null)
        {
            // Not memoized before the wrap returns, so a projection the node budget refused leaves the slot
            // empty and the next read asks again -- and gets the same wrapper, because the realm's table took
            // it before the RangeError was raised.
            wrapper = DomRealm.WrapNode(nodes[index]);
            elements[index] = wrapper;
        }

        value = wrapper;
        return true;
    }

    /// <inheritdoc />
    protected override bool HasIndex(uint index) => index < Length;

    /// <inheritdoc />
    protected override int NameCount
    {
        get
        {
            if (!_accessor.HasNamedGetter)
            {
                return 0;
            }

            var names = SupportedNames();
            if (ReferenceEquals(Definition, DomInterfaces.NamedNodeMap))
            {
                var visible = new List<string>(names.Count);
                foreach (var name in names)
                {
                    if (IsNamedPropertyVisible(name))
                    {
                        visible.Add(name);
                    }
                }

                names = visible;
            }

            _names = names;
            return _names.Count;
        }
    }

    /// <inheritdoc />
    protected override string NameAt(int index) => _names[index];

    /// <inheritdoc />
    protected override bool TryGetNamedValue(string name, out JsValue value)
    {
        if (!_accessor.HasNamedGetter)
        {
            value = JsValue.Undefined;
            return false;
        }

        if (ReferenceEquals(Definition, DomInterfaces.NamedNodeMap))
        {
            // WebIDL tests membership and visibility before it invokes the named getter. Besides preserving
            // that observable order around Proxy prototypes, this keeps a hidden Attr from being wrapped and
            // charged to the page's node budget merely because script read a prototype member of the same name.
            if (!HasSupportedName(name) || !IsNamedPropertyVisible(name))
            {
                value = JsValue.Undefined;
                return false;
            }

            return _accessor.TryGetNamed(DomRealm, DomTarget, name, out value);
        }

        return _accessor.TryGetNamed(DomRealm, DomTarget, name, out value);
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#dfn-named-property-visibility. NamedNodeMap declares
    /// LegacyUnenumerableNamedProperties, but not LegacyOverrideBuiltIns: an ordinary own property or a
    /// prototype property hides an attribute from both lookup and enumeration. The explicit getNamedItem
    /// operation and the indexed getter do not use this filter.
    /// </summary>
    private bool IsNamedPropertyVisible(string name)
    {
        // Read only the ordinary property bag. GetOwnProperty would call the named projection again.
        if (base.TryGetProperty(name, out _))
        {
            return false;
        }

        for (var prototype = Prototype; prototype is not null; prototype = prototype.Prototype)
        {
            // WebIDL skips the own-property check for a named properties object, such as the one behind
            // Window.prototype, but keeps walking: a property farther up the chain still hides the name.
            // HasOwnProperty inspects the descriptor without invoking an accessor's getter.
            if (prototype is not WindowNamedProperties && prototype.HasOwnProperty(name))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-namednodemap-item — the collection's
    /// <a href="https://webidl.spec.whatwg.org/#dfn-supported-property-names">supported property names</a>,
    /// which for <c>NamedNodeMap</c> are not simply the attributes' qualified names.
    /// </summary>
    // The native map accessor owns unique, case-filtered supported names. Do not copy,
    // re-filter or quadratically de-duplicate its authoritative result in the wrapper.
    private IReadOnlyList<string> SupportedNames() => _accessor.SupportedNames(DomRealm, DomTarget);

    private bool HasSupportedName(string name)
    {
        if (!_accessor.HasNamedGetter)
        {
            return false;
        }

        return _accessor.HasSupportedName(DomRealm, DomTarget, name);
    }

    bool INamedPropertySupport.HasSupportedName(string name) => HasSupportedName(name);

    /// <inheritdoc />
    protected override bool IsNameEnumerable(string name) => _accessor.AreNamesEnumerable;

    /// <inheritdoc />
    protected override bool IsNameWritable(string name) => _accessor.IsNameWritable;

    /// <inheritdoc />
    protected override bool TrySetNamedValue(string name, JsValue value)
    {
        using var mutation = DomRealm.MutateLayout();
        return _accessor.TrySetNamed(DomRealm, DomTarget, name, value);
    }

    /// <inheritdoc />
    protected override bool TryDeleteName(string name)
    {
        using var mutation = DomRealm.MutateLayout();
        return _accessor.TryDeleteNamed(DomTarget, name);
    }
}
