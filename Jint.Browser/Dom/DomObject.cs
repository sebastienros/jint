using Jint.Native.Object;

namespace Jint.Browser.Dom;

/// <summary>
/// Wraps a native DOM object with the prototype and realm selected by the binding contract.
/// </summary>
/// <remarks>
/// Generated members read the native object at call time; wrappers do not keep a second projected DOM state.
/// </remarks>
internal class DomObject : ObjectInstance, IDomWrapper
{
    internal DomObject(DomRealm realm, DomInterfaceDefinition definition, object target) : base(realm.Engine)
    {
        DomRealm = realm;
        Definition = definition;
        DomTarget = target;
        Prototype = realm.PrototypeOf(definition);
    }

    /// <inheritdoc />
    public object DomTarget { get; }

    /// <inheritdoc />
    public DomRealm DomRealm { get; }

    /// <summary>The interface whose prototype this wrapper was given.</summary>
    internal DomInterfaceDefinition Definition { get; }

    public override string ToString() => "[object " + Definition.Name + "]";
}
