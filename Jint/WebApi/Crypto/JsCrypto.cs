#if NET8_0_OR_GREATER
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The <c>crypto</c> object — the realm's one instance of the <c>Crypto</c> interface.
/// <para>
/// https://w3c.github.io/webcrypto/#crypto-interface
/// </para>
/// </summary>
/// <remarks>
/// It carries no state at all: every member is <see cref="CryptoPrototype"/>'s, the randomness comes from
/// <see cref="System.Security.Cryptography.RandomNumberGenerator"/> and <c>subtle</c> answers with the realm's
/// <c>SubtleCrypto</c>. What the object <i>is</i> is the brand — the thing every member checks its receiver
/// against, and what <c>crypto instanceof Crypto</c> asks about.
/// </remarks>
internal sealed class JsCrypto : ObjectInstance
{
    private JsCrypto(Engine engine) : base(engine, ObjectClass.Object)
    {
    }

    internal static JsCrypto Create(Engine engine, Realm realm) => new(engine)
    {
        _prototype = realm.Intrinsics.Crypto.PrototypeObject,
    };
}
#endif
