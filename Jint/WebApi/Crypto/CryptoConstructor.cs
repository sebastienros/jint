#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The <c>Crypto</c> interface object.
/// <para>
/// https://w3c.github.io/webcrypto/#crypto-interface
/// </para>
/// </summary>
/// <remarks>
/// The interface declares no constructor operation, so the interface object exists and is a function but
/// refuses to construct anything — https://webidl.spec.whatwg.org/#es-interface-call. The one instance is the
/// <c>crypto</c> global, which is what <c>crypto instanceof Crypto</c> is written against; WinterTC's Minimum
/// Common API §5.1 lists this interface, and it is a global because that list is the surface a non-browser
/// runtime is asked to carry.
/// </remarks>
internal sealed class CryptoConstructor : Constructor
{
    private static readonly JsString _functionName = new("Crypto");

    internal CryptoConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new CryptoPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal CryptoPrototype PrototypeObject { get; }

    /// <summary>
    /// An interface without a constructor operation is not constructible.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }
}
#endif
