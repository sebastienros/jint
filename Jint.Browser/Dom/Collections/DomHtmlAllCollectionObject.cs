using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Native;
using Jint.Native.Array;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// The wrapper for <c>document.all</c> —
/// <a href="https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#htmlallcollection">HTML's
/// <c>HTMLAllCollection</c></a>.
/// </summary>
/// <remarks>
/// <para>
/// It is not a refinement of <see cref="DomHtmlCollectionObject{T}"/> even though AngleSharp models
/// <c>IHtmlAllCollection</c> as an <c>IHtmlCollection&lt;IElement&gt;</c>, because four things about this one
/// interface are its own and none of them is expressible as an <c>HTMLCollection</c>: its named lookup answers
/// an <em>element or a collection</em> rather than an element, its <c>item</c> takes a name <em>or</em> an
/// index, it has a legacy caller — <c>document.all('x')</c> — and it carries ECMAScript
/// <a href="https://tc39.es/ecma262/#sec-IsHTMLDDA-internal-slot">Annex B.3.6</a>'s <c>[[IsHTMLDDA]]</c>
/// internal slot, which is what makes <c>typeof document.all</c> answer <c>"undefined"</c>. The prototype
/// chain is its own too: <c>overrides.json</c>'s manual entry roots it at <c>Object.prototype</c>, where the
/// CLR hierarchy would have put <c>HTMLCollection.prototype</c>.
/// </para>
/// <para>
/// <c>[LegacyUnenumerableNamedProperties]</c> and the ordinary-own-property visibility rule are
/// <see cref="DomHtmlCollectionObject{T}"/>'s, unchanged: a supported name is a non-enumerable own property,
/// an expando of the same name wins, and <c>namedItem</c> looks through both.
/// </para>
/// </remarks>
internal sealed class DomHtmlAllCollectionObject : DomCollectionBase, ICallable
{
    /// <summary>
    /// https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#all-named-elements — the fourteen
    /// elements whose <c>name</c> content attribute contributes a supported property name. Every other
    /// element contributes its <c>id</c> and nothing else, which is why <c>document.all.divwithname</c> is
    /// <c>undefined</c> for a <c>&lt;div name="divwithname"&gt;</c>.
    /// </summary>
    private static readonly HashSet<string> _allNamed = new(StringComparer.Ordinal)
    {
        "a", "button", "embed", "form", "frame", "frameset", "iframe", "img", "input", "map", "meta",
        "object", "select", "textarea",
    };

    private readonly IHtmlAllCollection _collection;
    private List<string> _names = [];

    // The one name the *property* lane has built a sub-collection for, and that collection. See
    // NamedElements: an own read has to answer the same value twice, and a getter that manufactured a new
    // collection per call would disagree with itself. One entry, because it only has to survive the second
    // read of the same key, and it never goes stale: the collection it holds is a live filter over the name.
    private string? _memoizedName;
    private JsValue? _memoizedCollection;

    internal DomHtmlAllCollectionObject(DomRealm realm, DomInterfaceDefinition definition, IHtmlAllCollection collection)
        : base(realm, definition, collection)
    {
        _collection = collection;

        // The two internal slots this one object has and no other wrapper does. Declared here, before the
        // object can be reached from script, because the engine reads both off _type without re-checking.
        DeclareIsHtmlDda();
        DeclareCallable();
    }

    /// <inheritdoc />
    public override uint Length => (uint) _collection.Length;

    /// <inheritdoc />
    protected override bool IgnoreNamedPropertiesInSet => true;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#concept-get-all-indexed — the
    /// <c>index</c>th element, or nothing at all when there is no such element.
    /// </summary>
    /// <remarks>
    /// One walk, not two: AngleSharp's <c>HtmlAllCollection</c> is a lazy view over the document's element
    /// descendants, so <c>Length</c> runs the whole query and the indexer runs it again. Running out of
    /// elements <i>is</i> the bounds answer, which is the same reason
    /// <see cref="DomHtmlCollectionObject{T}.TryGetIndex"/> stopped asking for a length first.
    /// </remarks>
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

    /// <summary>The <paramref name="index"/>th element of the collection, in one pass, or <see langword="null"/>.</summary>
    private IElement? ElementAt(uint index)
    {
        var remaining = index;

        foreach (var candidate in _collection)
        {
            if (remaining == 0)
            {
                return candidate;
            }

            remaining--;
        }

        return null;
    }

    /// <summary>
    /// The supported property names: every element's non-empty <c>id</c> and every "all"-named element's
    /// non-empty <c>name</c>, in tree order, an element's id before its own name, later duplicates ignored.
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
    /// <a href="https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#htmlallcollection">HTMLAllCollection</a>
    /// carries <c>[LegacyUnenumerableNamedProperties]</c>: <c>'root' in document.all</c> is true and
    /// <c>Object.keys(document.all)</c> is the indices alone.
    /// </summary>
    protected override bool IsNameEnumerable(string name) => false;

    /// <summary>
    /// https://webidl.spec.whatwg.org/#dfn-named-property-visibility: an ordinary own property wins over a
    /// supported name, including when the matching element appeared after the property was created.
    /// </summary>
    protected override bool TryGetNamedValue(string name, out JsValue value)
    {
        if (HasStoredProperty(name))
        {
            value = JsValue.Undefined;
            return false;
        }

        value = NamedElements(name, memoize: true);
        if (value.IsNull())
        {
            value = JsValue.Undefined;
            return false;
        }

        return true;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#dom-htmlallcollection-nameditem —
    /// the element, a live <c>HTMLCollection</c> when several match, or <c>null</c>.
    /// </summary>
    /// <remarks>
    /// The operation looks through expandos, exactly as <c>HTMLCollection.namedItem</c> does; only property
    /// lookup applies the visibility check above.
    /// </remarks>
    internal override JsValue NamedItem(string name) => NamedElements(name, memoize: false);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#dom-htmlallcollection-item and the
    /// legacy caller behind it: <see langword="null"/> when the argument was not provided, then
    /// <a href="https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#concept-get-all-indexed-or-named">get
    /// the "all"-indexed or named element(s)</a> — an array index property name reads the index and answers
    /// nothing for one out of range, and everything else is a name.
    /// </summary>
    internal JsValue Item(string? nameOrIndex)
    {
        if (nameOrIndex is null)
        {
            return JsValue.Null;
        }

        // The same parse the engine's named projection uses to decide which supported names are unreachable
        // as properties, so `document.all("42")` and `document.all[42]` cannot disagree about what 42 is.
        // uint.MaxValue is both "not a canonical index" and 2^32-1, which is not an array index either.
        var index = ArrayInstance.ParseArrayIndex(nameOrIndex);
        if (index != uint.MaxValue)
        {
            return TryGetIndex(index, out var element) ? element : JsValue.Null;
        }

        return NamedElements(nameOrIndex, memoize: false);
    }

    /// <summary>
    /// The legacy caller: <c>document.all(nameOrIndex)</c> behaves as <c>item</c> does, and ignores its
    /// <c>this</c> value entirely.
    /// </summary>
    JsValue ICallable.Call(JsValue thisObject, params JsValue[] arguments)
        => Item(DomConvert.OptionalText(arguments, 0, null));

    // Query only ObjectInstance's ordinary property bag: GetOwnProperty would recurse through this projection.
    private bool HasStoredProperty(string name) => base.TryGetProperty(name, out _);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#concept-get-all-named — one element,
    /// a live sub-collection when several match, or <see cref="JsValue.Null"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="memoize"/> is the one place this deviates from a browser, and the deviation is the
    /// engine's host-object contract rather than a choice. An own read is answered by
    /// <c>TryGetOwnPropertyValue</c> and verified against <c>GetOwnProperty</c> for the same key, so the
    /// <em>property</em> lane may not manufacture a new object per call — <c>document.all.foo</c> twice
    /// answers one collection here and two in a browser. <c>namedItem</c>, <c>item</c> and the legacy caller
    /// build a new one per call, which is what upstream's corpus actually checks.
    /// </remarks>
    private JsValue NamedElements(string name, bool memoize)
    {
        if (name.Length == 0)
        {
            return JsValue.Null;
        }

        IElement? first = null;
        var count = 0;
        foreach (var element in _collection)
        {
            if (!Matches(element, name))
            {
                continue;
            }

            first ??= element;
            if (++count > 1)
            {
                break;
            }
        }

        if (count == 0)
        {
            return JsValue.Null;
        }

        if (count == 1)
        {
            return DomRealm.Wrap(first!);
        }

        // "an HTMLCollection object rooted at the same Document as collection, whose filter matches ..." —
        // the filter is re-evaluated against the tree on every read, so the result is live however it is
        // reached, and every operation hands back one nothing else has seen.
        if (!memoize)
        {
            return NewSubCollection(name);
        }

        if (_memoizedCollection is null || !string.Equals(_memoizedName, name, StringComparison.Ordinal))
        {
            _memoizedName = name;
            _memoizedCollection = NewSubCollection(name);
        }

        return _memoizedCollection;
    }

    /// <remarks>
    /// The source is this collection rather than a tree root, which is the one live collection in the
    /// surface that is not "the element descendants of a node": <c>document.all</c>'s own membership is
    /// already the filter's domain, and the wrapper has no reference to the document to root a walk at.
    /// </remarks>
    private JsValue NewSubCollection(string name)
        => DomRealm.WrapCollection<IElement>(new DomLiveHtmlCollection(_collection, new AllNamedFilter(name)));

    /// <summary>The filter of the sub-collection above: an id always, a <c>name</c> only on an "all"-named element.</summary>
    private sealed class AllNamedFilter(string name) : DomElementFilter
    {
        internal override bool Matches(IElement element) => DomHtmlAllCollectionObject.Matches(element, name);
    }

    private static bool Matches(IElement element, string name)
        => string.Equals(element.Id, name, StringComparison.Ordinal)
           || (IsAllNamed(element) && string.Equals(element.GetAttribute("name"), name, StringComparison.Ordinal));

    private static bool IsAllNamed(IElement element)
        => element is IHtmlElement && _allNamed.Contains(element.LocalName);

    /// <remarks>
    /// The duplicate check is a set for the reason <c>DomHtmlCollectionObject.VisibleNames</c> gives: the
    /// <c>Contains</c> overload taking a comparer is LINQ's, linear and one enumerator per candidate, and
    /// <c>document.all</c> is the collection with the most of them.
    /// </remarks>
    private List<string> VisibleNames()
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var element in _collection)
        {
            Add(names, element.Id);

            if (IsAllNamed(element))
            {
                Add(names, element.GetAttribute("name"));
            }
        }

        return names;

        void Add(List<string> names, string? candidate)
        {
            if (string.IsNullOrEmpty(candidate)
                || !seen.Add(candidate!)
                // A supported name that spells a canonical array index is unreachable as a property — the
                // indexed half answers first and stops — so WebIDL leaves it out of [[OwnPropertyKeys]] and
                // ArrayLikeObject refuses to advertise it. namedItem still finds it.
                || ArrayInstance.ParseArrayIndex(candidate!) != uint.MaxValue
                // The base class lists an ordinary own property itself, in property-bag order. Do not also
                // advertise a projected name for it, or enumeration and lookup would disagree.
                || HasStoredProperty(candidate!))
            {
                return;
            }

            names.Add(candidate!);
        }
    }
}
