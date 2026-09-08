using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Native;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// The wrapper for an <c>HTMLCollection</c> and its refinements (<c>HTMLFormControlsCollection</c>,
/// <c>HTMLOptionsCollection</c>, <c>HTMLAllCollection</c>).
/// </summary>
/// <remarks>
/// It is the one collection the generated <see cref="DomCollectionAccessor"/> scheme cannot serve, because
/// AngleSharp's <see cref="IHtmlCollection{T}"/> is generic and <b>invariant</b>: an
/// <c>IHtmlCollection&lt;IHtmlOptionElement&gt;</c> is not an <c>IHtmlCollection&lt;IElement&gt;</c>, so one
/// non-generic accessor could not reach the indexer at all. A generated member instead names its declared
/// element type at the call site — <c>realm.WrapCollection&lt;IHtmlOptionElement&gt;(…)</c> — which keeps the
/// path free of reflection and of a generic instantiation nothing static can see.
/// </remarks>
internal sealed class DomHtmlCollectionObject<T> : DomCollectionBase where T : class, IElement
{
    private readonly IHtmlCollection<T> _collection;
    private List<string> _names = [];

    internal DomHtmlCollectionObject(DomRealm realm, DomInterfaceDefinition definition, IHtmlCollection<T> collection)
        : base(realm, definition, collection)
    {
        _collection = collection;
    }

    /// <inheritdoc />
    public override uint Length => (uint) _collection.Length;

    /// <inheritdoc />
    protected override bool IgnoreNamedPropertiesInSet => true;

    /// <inheritdoc />
    public override bool TryGetIndex(uint index, out JsValue value)
    {
        if (index >= (uint) _collection.Length)
        {
            value = JsValue.Undefined;
            return false;
        }

        value = DomRealm.Wrap(_collection[(int) index]);
        return true;
    }

    /// <inheritdoc />
    protected override bool HasIndex(uint index) => index < (uint) _collection.Length;

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
        if (name.Length == 0)
        {
            return JsValue.Null;
        }

        foreach (var element in _collection)
        {
            if (string.Equals(element.Id, name, StringComparison.Ordinal)
                || (element is IHtmlElement && string.Equals(element.GetAttribute("name"), name, StringComparison.Ordinal)))
            {
                return DomRealm.Wrap(element);
            }
        }

        return JsValue.Null;
    }

    private List<string> VisibleNames()
    {
        var names = new List<string>();
        foreach (var element in _collection)
        {
            Add(names, element.Id);

            if (element is IHtmlElement)
            {
                Add(names, element.GetAttribute("name"));
            }
        }

        return names;

        void Add(List<string> names, string? candidate)
        {
            // The base class lists an ordinary own property itself, in property-bag order. Do not also
            // advertise a projected name for it, or enumeration and lookup would disagree.
            if (!string.IsNullOrEmpty(candidate)
                && !names.Contains(candidate!, StringComparer.Ordinal)
                && !HasStoredProperty(candidate!))
            {
                names.Add(candidate!);
            }
        }
    }
}
