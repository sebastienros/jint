#if NET8_0_OR_GREATER
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The object <c>crypto.subtle</c> answers with — the realm's one instance of the <c>SubtleCrypto</c>
/// interface.
/// <para>
/// https://w3c.github.io/webcrypto/#subtlecrypto-interface
/// </para>
/// </summary>
/// <remarks>
/// It carries no state: the twelve operations are <see cref="SubtleCryptoPrototype"/>'s and every one of them
/// works over its arguments alone. What the object is, is the brand — one per realm, returned by reference by
/// the <c>subtle</c> attribute, which is what makes <c>crypto.subtle === crypto.subtle</c> hold.
/// </remarks>
internal sealed class JsSubtleCrypto : ObjectInstance
{
    private JsSubtleCrypto(Engine engine) : base(engine, ObjectClass.Object)
    {
    }

    internal static JsSubtleCrypto Create(Engine engine, Realm realm) => new(engine)
    {
        _prototype = realm.Intrinsics.SubtleCrypto.PrototypeObject,
    };
}
#endif
