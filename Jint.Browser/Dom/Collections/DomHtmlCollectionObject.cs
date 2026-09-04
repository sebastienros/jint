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
    /// half of the rule <see cref="TryGetNamedValue"/> carries the other half of.
    /// </summary>
    protected override int NameCount
    {
        get
        {
            _names = SupportedNames();
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
    /// <a href="https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#dom-htmlcollection-nameditem">HTML's
    /// <c>namedItem</c></a>, whose first step is the empty string and whose second is the element lookup.
    /// </summary>
    /// <remarks>
    /// <b>The empty string is refused before anything is searched, and it has to be here rather than left to
    /// the search.</b> An element may carry <c>id=""</c> or <c>name=""</c> — the HTML parser builds both, and
    /// AngleSharp's own named lookup matches them — so without step 1 a collection answered for a name
    /// <see cref="NameCount"/> had already declined to list, which is the projection's three hooks
    /// disagreeing at the same instant. It is one refusal for all three views, because <c>HasName</c> derives
    /// from this and so does <c>NamedItem</c>: <c>'' in collection</c>, <c>collection['']</c> and
    /// <c>collection.namedItem('')</c> are one answer.
    /// </remarks>
    protected override bool TryGetNamedValue(string name, out JsValue value)
    {
        // Step 1.
        if (name.Length == 0)
        {
            value = JsValue.Undefined;
            return false;
        }

        // Step 2.
        var item = _collection[name];
        if (item is null)
        {
            value = JsValue.Undefined;
            return false;
        }

        value = DomRealm.Wrap(item);
        return true;
    }

    private List<string> SupportedNames()
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

        static void Add(List<string> names, string? candidate)
        {
            if (!string.IsNullOrEmpty(candidate) && !names.Contains(candidate!, StringComparer.Ordinal))
            {
                names.Add(candidate!);
            }
        }
    }
}
