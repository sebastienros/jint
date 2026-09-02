using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Browser.Dom;

/// <summary>
/// The interface object of an interface the runtime owns rather than the generator — <c>MutationObserver</c>,
/// <c>DOMParser</c>, <c>Selection</c> and their kind.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DomInterfaceObject"/> is the same thing for a generated interface, and the two are separate
/// because a generated one takes its prototype, its parent and its constants from a
/// <see cref="DomInterfaceDefinition"/> and every one of them refuses construction. These are the ones a page
/// really does call <c>new</c> on, so the constructor body is a delegate, and <see langword="null"/> is what
/// makes an interface that cannot be constructed — <c>Selection</c>, <c>IntersectionObserverEntry</c> —
/// answer WebIDL's <c>Illegal constructor</c>.
/// </para>
/// <para>
/// It derives from <c>Constructor</c> for the reason <see cref="DomInterfaceObject"/> does:
/// <c>x instanceof MutationObserver</c> reads <c>prototype</c> off the interface object and walks the chain.
/// </para>
/// </remarks>
internal sealed class HostInterfaceObject : Constructor
{
    private readonly string _name;
    private readonly Func<JsValue[], ObjectInstance>? _construct;

    internal HostInterfaceObject(
        Engine engine,
        Realm realm,
        string name,
        ObjectInstance prototype,
        int length,
        Func<JsValue[], ObjectInstance>? construct = null,
        ObjectInstance? parent = null)
        : base(engine, realm, new JsString(name))
    {
        _name = name;
        _construct = construct;
        _prototype = parent ?? realm.Intrinsics.Function.PrototypeObject;
        _length = new PropertyDescriptor(JsNumber.Create(length), PropertyFlag.Configurable);

        // https://webidl.spec.whatwg.org/#interface-object — { writable: false, enumerable: false,
        // configurable: false }.
        _prototypeDescriptor = new PropertyDescriptor(prototype, PropertyFlag.AllForbidden);
    }

    /// <summary>https://webidl.spec.whatwg.org/#es-interface-call — an interface object is never callable.</summary>
    /// <remarks>
    /// An interface that cannot be constructed at all says so rather than pointing at <c>new</c>, which would
    /// only send the caller round a second time.
    /// </remarks>
    protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
    {
        Throw.TypeError(
            _realm,
            _construct is null
                ? "Illegal constructor"
                : "Failed to construct '" + _name + "': Please use the 'new' operator, this DOM object constructor cannot be called as a function.");

        return JsValue.Undefined;
    }

    /// <inheritdoc />
    public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
    {
        if (_construct is null)
        {
            Throw.TypeError(_realm, "Illegal constructor");
        }

        return _construct!(arguments);
    }

    public override string ToString() => "function " + _name + "() { [native code] }";
}
