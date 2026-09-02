using Jint.Native;
using Jint.Native.Object;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// The wrapper for a DOM object whose whole property model is a named getter and nothing indexed —
/// <c>DOMStringMap</c>, which is <c>element.dataset</c>.
/// </summary>
/// <remarks>
/// <c>NamedPropertyObject</c> derives the seven mutually-consistent hooks a projection needs from the three
/// this class supplies, and because it overrides <c>TryGetOwnPropertyValue</c> every read costs neither a
/// descriptor nor a probe. <c>dataset.foo = 'x'</c> reaches <c>TrySetNamedValue</c> ahead of the prototype
/// chain — the WebIDL named-property-setter shape — and creates the name, which is exactly what
/// <c>[[Set]]</c> on a <c>DOMStringMap</c> does.
/// </remarks>
internal sealed class DomNamedMapObject : NamedPropertyObject, IDomWrapper
{
    private readonly DomCollectionAccessor _accessor;
    private IReadOnlyList<string> _names = [];

    internal DomNamedMapObject(DomRealm realm, DomInterfaceDefinition definition, object target, DomCollectionAccessor accessor)
        : base(realm.Engine)
    {
        DomRealm = realm;
        Definition = definition;
        DomTarget = target;
        _accessor = accessor;
        Prototype = realm.PrototypeOf(definition);
    }

    /// <inheritdoc />
    public object DomTarget { get; }

    /// <inheritdoc />
    public DomRealm DomRealm { get; }

    /// <summary>The interface whose prototype this wrapper was given.</summary>
    internal DomInterfaceDefinition Definition { get; }

    /// <inheritdoc />
    public override int NameCount
    {
        get
        {
            _names = _accessor.SupportedNames(DomTarget);
            return _names.Count;
        }
    }

    /// <inheritdoc />
    public override string NameAt(int index) => _names[index];

    /// <inheritdoc />
    public override bool TryGetNamedValue(string name, out JsValue value)
        => _accessor.TryGetNamed(DomRealm, DomTarget, name, out value);

    /// <inheritdoc />
    protected override bool IsNameWritable(string name) => _accessor.IsNameWritable;

    /// <inheritdoc />
    protected override bool TrySetNamedValue(string name, JsValue value)
        => _accessor.TrySetNamed(DomRealm, DomTarget, name, value);

    /// <inheritdoc />
    protected override bool TryDeleteName(string name) => _accessor.TryDeleteNamed(DomTarget, name);

    public override string ToString() => "[object " + Definition.Name + "]";
}
