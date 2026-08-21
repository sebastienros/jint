#if NET8_0_OR_GREATER
using System.Security.Cryptography;
using Jint.Runtime;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The HKDF operations — https://w3c.github.io/webcrypto/#hkdf, "key derivation using the
/// extraction-then-expansion approach described in [RFC5869] and using the SHA hash functions defined in this
/// specification".
/// </summary>
/// <remarks>
/// <para>
/// <b>An HKDF key is a key only in the sense that <c>importKey</c> makes one.</b> The registration lists
/// <c>deriveBits</c>, <c>importKey</c> and <c>get key length</c> and nothing else — there is no
/// <c>generateKey</c> (the input keying material is something the caller already has) and no
/// <c>exportKey</c>, which the import steps make unreachable anyway by refusing any <c>extractable</c> that
/// is not <see langword="false"/>. The <c>[[handle]]</c> is the input keying material verbatim, and the
/// <c>[[algorithm]]</c> is the bare <c>KeyAlgorithm</c> — a <c>name</c> and nothing else, no <c>hash</c> and
/// no <c>length</c>, because for HKDF both belong to the derivation rather than to the key.
/// </para>
/// <para>
/// <b>The 255 &#215; <i>HashLen</i> ceiling is RFC 5869's own</b>, from the definition of HKDF-Expand: the
/// expansion counter <c>T(1) … T(N)</c> is a single octet, so <c>N &#8804; 255</c> and the output cannot
/// exceed 255 hash blocks. The specification's step 3 says exactly that ("If length is greater than
/// 255 * hashLength, then throw an OperationError"), and the check is made <i>here</i> rather than left to
/// the platform: <see cref="HKDF.DeriveKey(HashAlgorithmName, byte[], int, byte[], byte[])"/> reports an
/// over-long request as an <see cref="ArgumentOutOfRangeException"/>, which is a CLR exception erupting out
/// of a promise-returning operation — the one thing this API must never do. The <c>catch</c> below is the
/// backstop for the same reason, not the mechanism.
/// </para>
/// <para>
/// <b>A zero length is the empty byte sequence, and the platform will not produce it.</b> HKDF-Expand with
/// <c>L = 0</c> runs zero iterations and yields the empty string, so a zero-bit derivation is well defined
/// even though this algorithm's steps — unlike PBKDF2's, which spells the case out — never mention it. The
/// BCL refuses <c>outputLength</c> of zero with an <see cref="ArgumentOutOfRangeException"/> (measured), so
/// the case is answered before the call rather than through it.
/// </para>
/// </remarks>
internal static class HkdfAlgorithm
{
    /// <summary>
    /// The usages an HKDF key may carry — "If usages contains a value that is not 'deriveKey' or
    /// 'deriveBits', then throw a SyntaxError".
    /// </summary>
    private const KeyUsage AllowedUsages = KeyUsage.DeriveKey | KeyUsage.DeriveBits;

    /// <summary>
    /// https://w3c.github.io/webcrypto/#hkdf-operations — "Derive Bits".
    /// </summary>
    internal static byte[] DeriveBits(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        JsCryptoKey key,
        uint? length,
        string what)
    {
        // Step 1: "If length is null or is not a multiple of 8, then throw an OperationError." An absent
        // length argument defaults to null, so `deriveBits(hkdfParams, key)` lands here — HKDF has no
        // natural output size to fall back on, unlike ECDH.
        if (length is not { } bits)
        {
            context.ThrowOperationError(
                what + ": HKDF has no output length of its own, so deriveBits needs one — a length of null, which is what an omitted argument means, cannot be derived.");
            return null!;
        }

        if (bits % 8 != 0)
        {
            context.ThrowOperationError(
                what + ": a length of " + bits + " bits is not a whole number of bytes, which this operation requires.");
        }

        // Steps 2 and 3: the ceiling of the expansion step. See the remarks on this class.
        var hashLength = OutputLengthInBits(normalized.HashName!);
        var maximumLength = 255 * hashLength;

        if (bits > maximumLength)
        {
            context.ThrowOperationError(
                what + ": a length of " + bits + " bits exceeds the " + maximumLength + " bits HKDF can expand to with "
                + normalized.HashName + " (255 * " + hashLength + ").");
        }

        // HKDF-Expand with L = 0 is the empty byte sequence; the BCL declines to say so. See the remarks.
        if (bits == 0)
        {
            return [];
        }

        // Steps 4 and 5: extract, then expand, with the key's own bytes as the input keying material. The
        // span overload is the one that takes the key's [[handle]] without copying it out first.
        var result = new byte[bits / 8];

        try
        {
            HKDF.DeriveKey(HashName(normalized.HashName!), key.Handle, result, normalized.Salt!, normalized.Info!);
            return result;
        }
        catch (Exception e) when (e is CryptographicException or ArgumentException)
        {
            // Step 6: "If the key derivation operation fails, then throw an OperationError." The catch is
            // deliberately wider than CryptographicException — the platform reports a length it will not
            // produce as an ArgumentOutOfRangeException, which is an ArgumentException and is not a
            // CryptographicException, and a CLR exception must never escape a promise-returning operation.
            context.ThrowOperationError(what + ": the key derivation failed.");
            return null!;
        }
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#hkdf-operations — "Import key", whose whole content is that the key
    /// may only come from <c>raw</c> bytes and may never be extractable.
    /// </summary>
    /// <remarks>
    /// The <c>extractable</c> check is a real restriction rather than a formality: the <c>[[handle]]</c> of
    /// an HKDF key is the input keying material verbatim — very often a shared secret or a password — so a
    /// key that could be exported would be a way to read back what was imported. That is also why HKDF has no
    /// <c>exportKey</c> registration at all; the two together mean the bytes have no door out.
    /// </remarks>
    internal static (byte[] Handle, string KeyType, CryptoKeyAlgorithm Algorithm) ImportKey(
        CryptoContext context,
        KeyFormat format,
        byte[]? rawData,
        bool extractable,
        KeyUsage usages,
        string what)
    {
        // "If format is 'raw': … Otherwise: throw a NotSupportedError." The format is step 1 for HKDF, where
        // PBKDF2 writes the same refusal as its own first step — the two orders agree.
        if (format != KeyFormat.Raw)
        {
            context.ThrowNotSupportedError(
                what + ": an HKDF key can only be imported from the 'raw' format, not from '" + KeyFormats.NameOf(format) + "'.");
        }

        // "If usages contains a value that is not 'deriveKey' or 'deriveBits', then throw a SyntaxError."
        if ((usages & ~AllowedUsages) != KeyUsage.None)
        {
            context.ThrowSyntaxError(
                what + ": an HKDF key supports the usages deriveKey and deriveBits, not "
                + KeyUsages.Describe(usages & ~AllowedUsages) + ".");
        }

        // "If extractable is not false, then throw a SyntaxError." See the remarks on this method.
        if (extractable)
        {
            context.ThrowSyntaxError(what + ": an HKDF key must be imported with extractable false.");
        }

        return (rawData!, CryptoKeyTypes.Secret, new CryptoKeyAlgorithm(AlgorithmNormalization.Hkdf, Length: 0, HashName: null));
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#hkdf-operations — "Get key length": "Return null." An HKDF key has
    /// no length of its own, so <c>deriveKey(…, 'HKDF', …)</c> derives the whole of whatever the base
    /// algorithm produces.
    /// </summary>
    internal static uint? GetKeyLength() => null;

    /// <summary>
    /// "The length in bits of the output of the hash function identified by the hash member" — [FIPS-180-4]:
    /// 160 for SHA-1, and the number in the name for the SHA-2 family.
    /// </summary>
    /// <remarks>
    /// Every registered hash is spelled out and the default throws rather than one of them standing in as the
    /// fallback, which is the convention every table in this folder follows: a hash registered later would
    /// otherwise silently inherit SHA-512's ceiling.
    /// </remarks>
    private static int OutputLengthInBits(string hashName)
    {
        switch (hashName)
        {
            case AlgorithmNormalization.Sha1:
                return 160;
            case AlgorithmNormalization.Sha256:
                return 256;
            case AlgorithmNormalization.Sha384:
                return 384;
            case AlgorithmNormalization.Sha512:
                return 512;
            default:
                // Unreachable: the hash was matched against the digest registry during normalization.
                Throw.InvalidOperationException("Unhandled HKDF hash algorithm '" + hashName + "'.");
                return 0;
        }
    }

    private static HashAlgorithmName HashName(string hashName)
    {
        switch (hashName)
        {
            case AlgorithmNormalization.Sha1:
                // SHA-1 is one of the four hashes the digest registry carries, so an HkdfParams may name it.
                // HKDF's security rests on HMAC, which the collision attacks that retired plain SHA-1 do not
                // break, and a script asking for it has already chosen; nothing here picks it for anybody.
                return HashAlgorithmName.SHA1;
            case AlgorithmNormalization.Sha256:
                return HashAlgorithmName.SHA256;
            case AlgorithmNormalization.Sha384:
                return HashAlgorithmName.SHA384;
            case AlgorithmNormalization.Sha512:
                return HashAlgorithmName.SHA512;
            default:
                Throw.InvalidOperationException("Unhandled HKDF hash algorithm '" + hashName + "'.");
                return default;
        }
    }
}
#endif
