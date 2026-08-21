#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The <c>CryptoKey</c> interface object.
/// <para>
/// https://w3c.github.io/webcrypto/#cryptokey-interface
/// </para>
/// </summary>
/// <remarks>
/// The interface declares no constructor operation, which in WebIDL means the interface object exists and is
/// a function but refuses to construct anything — https://webidl.spec.whatwg.org/#es-interface-call. A key
/// can therefore only come from <c>generateKey</c> or <c>importKey</c>, which is what makes
/// <c>key instanceof CryptoKey</c> a statement about provenance rather than about shape.
/// </remarks>
internal sealed class CryptoKeyConstructor : Constructor
{
    private static readonly JsString _functionName = new("CryptoKey");

    internal CryptoKeyConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new CryptoKeyPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal CryptoKeyPrototype PrototypeObject { get; }

    /// <summary>
    /// An interface without a constructor operation is not constructible.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }

    /// <summary>
    /// Builds a key in this realm. The one place a <see cref="JsCryptoKey"/> is created, so the prototype
    /// cannot be forgotten on some path.
    /// </summary>
    internal JsCryptoKey CreateKey(
        byte[] handle,
        string keyType,
        CryptoKeyAlgorithm algorithm,
        bool extractable,
        KeyUsage usages)
    {
        return new JsCryptoKey(_engine, handle, keyType, algorithm, extractable, usages)
        {
            _prototype = PrototypeObject,
        };
    }
}
#endif
