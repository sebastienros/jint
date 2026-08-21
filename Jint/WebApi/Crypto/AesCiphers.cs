#if NET8_0_OR_GREATER
using System.Security.Cryptography;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The two things AES-CBC, AES-CTR and AES-KW each need from the block cipher itself: an <see cref="Aes"/>
/// carrying a key's material, and one predicate deciding which exceptions the platform's cipher may raise.
/// </summary>
/// <remarks>
/// <para>
/// <b>The exception predicate is deliberately wider than <see cref="CryptographicException"/>.</b> This
/// folder has already learned twice that guessing which exception type a platform picks — and which step it
/// picks it at — is how a CLR exception ends up erupting out of a promise-returning operation:
/// <see cref="EcAlgorithm"/> records <see cref="PlatformNotSupportedException"/> for a point that is not on
/// its curve and <see cref="ArgumentException"/> for a key-size mismatch, neither of which is a
/// <see cref="CryptographicException"/>. The one-shot <see cref="SymmetricAlgorithm"/> methods used here
/// report a length they will not accept as an <see cref="ArgumentException"/> and a key they will not accept
/// as a <see cref="CryptographicException"/>, and every such condition is checked before the call — so this
/// catches nothing on any measured path and exists as the backstop rather than the mechanism.
/// </para>
/// <para>
/// What it must <b>not</b> catch is everything else a <see cref="Exception"/> can be: an execution
/// constraint, a cancellation or a stack-depth overflow is not a failure of the cipher and turning one into
/// an <c>OperationError</c> would mean a constraint that no longer bounds anything. Naming the three types
/// rather than catching broadly is what keeps that true.
/// </para>
/// </remarks>
internal static class AesCiphers
{
    /// <summary>The AES block size in bytes — the one constant all three modes are written against.</summary>
    internal const int BlockSize = 16;

    /// <summary>
    /// An <see cref="Aes"/> holding the key's material. The bytes are copied because
    /// <see cref="SymmetricAlgorithm.Key"/> is a <c>byte[]</c> property that keeps what it is given, and the
    /// <c>[[handle]]</c> of a <see cref="JsCryptoKey"/> is not something to hand to anything that outlives
    /// the call — the object is disposed inside the operation that created it.
    /// </summary>
    internal static Aes Create(JsCryptoKey key)
    {
        var aes = Aes.Create();
        aes.Key = key.Handle.ToArray();
        return aes;
    }

    /// <summary>See the remarks on this class: the backstop, not the mechanism.</summary>
    internal static bool IsCipherFailure(Exception exception)
        => exception is CryptographicException or ArgumentException or PlatformNotSupportedException;
}
#endif
