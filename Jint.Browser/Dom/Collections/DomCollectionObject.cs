using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// The wrapper for every DOM collection with an indexed getter that is not an <c>HTMLCollection</c> —
/// <c>NodeList</c>, <c>DOMTokenList</c>, <c>NamedNodeMap</c>, <c>DOMStringList</c>, <c>CSSRuleList</c>,
/// <c>StyleSheetList</c>, <c>CSSStyleDeclaration</c>, <c>FileList</c>, the media track lists. One class,
/// because the interface-specific half is a <see cref="DomCollectionAccessor"/> the generator wrote from
/// AngleSharp's <c>[DomAccessor]</c> metadata.
/// </summary>
internal sealed class DomCollectionObject : DomCollectionBase, INamedPropertySupport
{
    private readonly DomCollectionAccessor _accessor;

    // Recomputed whenever an enumeration starts (which is always at NameCount) and read by NameAt, so that a
    // key list costs one walk of the collection rather than one per key. Deliberately not a cache with a
    // lifetime: a named getter is live, and a question asked outside an enumeration goes straight to the
    // accessor.
    private IReadOnlyList<string> _names = [];

    internal DomCollectionObject(DomRealm realm, DomInterfaceDefinition definition, object target, DomCollectionAccessor accessor)
        : base(realm, definition, target)
    {
        _accessor = accessor;
    }

    /// <inheritdoc />
    public override uint Length => _accessor.Length(DomTarget);

    /// <inheritdoc />
    public override bool TryGetIndex(uint index, out JsValue value)
        => _accessor.TryGetIndex(DomRealm, DomTarget, index, out value);

    /// <inheritdoc />
    protected override bool HasIndex(uint index) => index < _accessor.Length(DomTarget);

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
    /// <remarks>
    /// DOM §4.9.1 states two steps the generated accessor cannot: duplicates are omitted, and <b>when the
    /// map's element is in the HTML namespace and its node document is an HTML document, a name that is not
    /// its own ASCII lowercase is removed</b>. That second step is what keeps
    /// <c>el.setAttributeNS("foo", "A:B", "")</c> from putting an <c>A:B</c> own property on
    /// <c>el.attributes</c>, where an HTML parse could never have produced one; the attribute is still there
    /// and still reachable by index, by <c>getAttributeNodeNS</c> and by <c>getNamedItemNS</c>, because none
    /// of those is a named property. The element is read from the first attribute rather than from the map,
    /// which has no owner in AngleSharp's surface — and an empty map has no names to filter.
    /// </remarks>
    private IReadOnlyList<string> SupportedNames()
    {
        var names = _accessor.SupportedNames(DomTarget);

        if (!ReferenceEquals(Definition, DomInterfaces.NamedNodeMap) || names.Count == 0)
        {
            return names;
        }

        var lowercaseOnly = DomTarget is INamedNodeMap { Length: > 0 } map
                            && map[0] is { OwnerElement: { } owner }
                            && string.Equals(owner.NamespaceUri, NamespaceNames.HtmlUri, StringComparison.Ordinal)
                            && owner.Owner is IHtmlDocument;

        var supported = new List<string>(names.Count);

        foreach (var name in names)
        {
            if (lowercaseOnly && !IsAsciiLowercase(name))
            {
                continue;
            }

            if (!supported.Contains(name, StringComparer.Ordinal))
            {
                supported.Add(name);
            }
        }

        return supported;
    }

    /// <summary>Whether <paramref name="name"/> ASCII-lowercased is <paramref name="name"/>.</summary>
    private static bool IsAsciiLowercase(string name)
    {
        foreach (var character in name)
        {
            if (character is >= 'A' and <= 'Z')
            {
                return false;
            }
        }

        return true;
    }

    private bool HasSupportedName(string name)
    {
        foreach (var supportedName in SupportedNames())
        {
            if (string.Equals(supportedName, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    bool INamedPropertySupport.HasSupportedName(string name) => HasSupportedName(name);

    /// <inheritdoc />
    protected override bool IsNameEnumerable(string name) => _accessor.AreNamesEnumerable;

    /// <inheritdoc />
    protected override bool IsNameWritable(string name) => _accessor.IsNameWritable;

    /// <inheritdoc />
    protected override bool TrySetNamedValue(string name, JsValue value)
        => _accessor.TrySetNamed(DomRealm, DomTarget, name, value);

    /// <inheritdoc />
    protected override bool TryDeleteName(string name) => _accessor.TryDeleteNamed(DomTarget, name);
}
