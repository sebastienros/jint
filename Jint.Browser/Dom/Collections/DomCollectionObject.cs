using Jint.Browser.Runtime;
using Jint.Native;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// The wrapper for every DOM collection with an indexed getter that is not an <c>HTMLCollection</c> —
/// <c>NodeList</c>, <c>DOMTokenList</c>, <c>NamedNodeMap</c>, <c>DOMStringList</c>, <c>CSSRuleList</c>,
/// <c>StyleSheetList</c>, <c>CSSStyleDeclaration</c>, <c>FileList</c>, the media track lists. One class,
/// because the interface-specific half is a <see cref="DomCollectionAccessor"/> the generator wrote from
/// AngleSharp's <c>[DomAccessor]</c> metadata.
/// </summary>
internal sealed class DomCollectionObject : DomCollectionBase
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

            var names = _accessor.SupportedNames(DomTarget);
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

        // The supported-name check comes first, before a prototype (possibly a Proxy) can observe a probe.
        if (!_accessor.TryGetNamed(DomRealm, DomTarget, name, out value))
        {
            return false;
        }

        if (ReferenceEquals(Definition, DomInterfaces.NamedNodeMap) && !IsNamedPropertyVisible(name))
        {
            value = JsValue.Undefined;
            return false;
        }

        return true;
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
            // WebIDL skips named properties objects, such as the one behind Window.prototype.
            if (prototype is WindowNamedProperties)
            {
                return true;
            }

            // HasOwnProperty inspects the descriptor without invoking an accessor's getter.
            if (prototype.HasOwnProperty(name))
            {
                return false;
            }
        }

        return true;
    }

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
