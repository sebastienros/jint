using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Browser.Dom;

/// <summary>
/// A WebIDL legacy factory function such as HTML's <c>Image</c>: a constructor with the generated
/// interface's prototype, but a different global name and constructor algorithm.
/// </summary>
internal sealed class DomLegacyFactoryFunction : Constructor
{
    private readonly DomRealm _domRealm;
    private readonly DomLegacyFactoryDefinition _definition;

    internal DomLegacyFactoryFunction(DomRealm realm, DomLegacyFactoryDefinition definition)
        : base(realm.Engine, realm.PrincipalRealm, new JsString(definition.Name))
    {
        _domRealm = realm;
        _definition = definition;
        _prototype = realm.PrincipalRealm.Intrinsics.Function.PrototypeObject;
        _length = new PropertyDescriptor(JsNumber.Create(definition.Length), PropertyFlag.Configurable);

        // https://webidl.spec.whatwg.org/#legacy-factory-functions — the legacy factory's prototype is the
        // original interface prototype, with { writable: false, enumerable: false, configurable: false }.
        _prototypeDescriptor = new PropertyDescriptor(
            realm.PrototypeOf(definition.Interface),
            PropertyFlag.AllForbidden);
    }

    /// <inheritdoc />
    public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
    {
        var instance = _definition.Construct(_domRealm, arguments);

        // WebIDL internally creates the implementing object with NewTarget. The direct `new Image()` path
        // already has the generated interface prototype; a derived constructor supplies its own instead.
        if (!ReferenceEquals(newTarget, this) && newTarget is ObjectInstance constructor)
        {
            instance.Prototype = constructor.Get("prototype") as ObjectInstance
                ?? _domRealm.PrototypeOf(_definition.Interface);
        }

        return instance;
    }

    public override string ToString() => "function " + _definition.Name + "() { [native code] }";
}

/// <summary>One hand-written <c>[LegacyFactoryFunction]</c> the AngleSharp metadata cannot express.</summary>
internal readonly record struct DomLegacyFactoryDefinition(
    string Name,
    DomInterfaceDefinition Interface,
    int Length,
    Func<DomRealm, JsValue[], ObjectInstance> Construct)
{
    internal DomLegacyFactoryFunction Create(DomRealm realm) => new(realm, this);
}
