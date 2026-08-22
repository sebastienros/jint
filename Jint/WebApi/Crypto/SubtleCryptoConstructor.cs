#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The <c>SubtleCrypto</c> interface object.
/// <para>
/// https://w3c.github.io/webcrypto/#subtlecrypto-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The interface declares no constructor operation, so the interface object exists and is a function but
/// refuses to construct anything — https://webidl.spec.whatwg.org/#es-interface-call. The one instance is
/// what <c>crypto.subtle</c> answers with, and it stays reachable only through that attribute; naming the
/// interface is for <c>crypto.subtle instanceof SubtleCrypto</c> and for the feature detection a library
/// performing cryptography opens with.
/// </para>
/// <para>
/// A browser exposes this interface object only in a secure context, which an embedded engine has no way to
/// be: there is no origin and no transport for the bit to describe. It is therefore exposed whenever the
/// crypto feature is, the same reading <c>SubtleCryptoPrototype</c> takes of <c>[SecureContext]</c> on the
/// operations themselves — and the same one Node and workerd take.
/// </para>
/// </remarks>
internal sealed class SubtleCryptoConstructor : Constructor
{
    private static readonly JsString _functionName = new("SubtleCrypto");

    internal SubtleCryptoConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new SubtleCryptoPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal SubtleCryptoPrototype PrototypeObject { get; }

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
