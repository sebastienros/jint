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

            _names = _accessor.SupportedNames(DomTarget);
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

        return _accessor.TryGetNamed(DomRealm, DomTarget, name, out value);
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
