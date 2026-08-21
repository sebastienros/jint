#if NET8_0_OR_GREATER
using System.Security.Cryptography;
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The HMAC operations — https://w3c.github.io/webcrypto/#hmac, which calculates and verifies hash-based
/// message authentication codes according to [FIPS-198-1] using the SHA hash functions this specification
/// defines.
/// </summary>
/// <remarks>
/// <para>
/// The key material is whole bytes here, which is the one place this implementation is narrower than the
/// algorithm's prose. <c>importKey</c> follows the specification exactly, including a <c>length</c> that is
/// not a multiple of eight: the constraint the steps impose on it — greater than <c>length - 8</c> and at
/// most <c>length</c> — means the byte count never changes, so the bits the key is short by are recorded on
/// the key and the MAC is still computed over the bytes that were imported, exactly as every other
/// implementation does with them. <c>generateKey</c> is where the difference is visible: a length that is
/// not a multiple of eight is refused with an <c>OperationError</c> ("If the key generation step fails, then
/// throw an OperationError"), because generating a key of, say, 57 bits would mean handing back an eight-byte
/// key whose last seven bits are a lie. Node's WebCrypto refuses the same shape for the same reason.
/// </para>
/// </remarks>
internal static class HmacAlgorithm
{
    /// <summary>
    /// The block size, in bits, of each hash function — [FIPS-180-4] §1: 512 bits for SHA-1 and SHA-256,
    /// 1024 for SHA-384 and SHA-512. It is the "recommended length" an <c>HmacKeyGenParams</c> without a
    /// <c>length</c> member asks for.
    /// </summary>
    /// <remarks>
    /// Every registered hash is spelled out and the default throws rather than one of them standing in as
    /// the fallback: a hash registered later would otherwise be silently given SHA-512's block size.
    /// </remarks>
    internal static uint BlockSizeInBits(string hashName)
    {
        switch (hashName)
        {
            case AlgorithmNormalization.Sha1:
            case AlgorithmNormalization.Sha256:
                return 512;
            case AlgorithmNormalization.Sha384:
            case AlgorithmNormalization.Sha512:
                return 1024;
            default:
                Throw.InvalidOperationException("Unhandled HMAC hash algorithm '" + hashName + "'.");
                return 0;
        }
    }

    /// <summary>
    /// The JWK <c>alg</c> field naming an HMAC key with a given inner hash —
    /// https://www.rfc-editor.org/rfc/rfc7518#section-3.1, plus <c>HS1</c>, which the Web Cryptography API
    /// names for SHA-1.
    /// </summary>
    /// <remarks>
    /// The default throws for the reason <see cref="BlockSizeInBits"/> gives, and here the consequence is
    /// worse: a hash silently labelled <c>HS512</c> would produce a JWK that names the wrong algorithm.
    /// </remarks>
    internal static string JwkAlgorithm(string hashName)
    {
        switch (hashName)
        {
            case AlgorithmNormalization.Sha1:
                return "HS1";
            case AlgorithmNormalization.Sha256:
                return "HS256";
            case AlgorithmNormalization.Sha384:
                return "HS384";
            case AlgorithmNormalization.Sha512:
                return "HS512";
            default:
                Throw.InvalidOperationException("Unhandled HMAC hash algorithm '" + hashName + "'.");
                return null!;
        }
    }

    /// <summary>The usages an HMAC key may carry: "sign" and "verify".</summary>
    private const KeyUsage AllowedUsages = KeyUsage.Sign | KeyUsage.Verify;

    /// <summary>
    /// The ceiling on a generated HMAC key, in bits — 8 KiB of key material, which is more than a hundred
    /// times the largest block size any registered hash has.
    /// </summary>
    private const uint MaxKeyLengthInBits = 65536;

    /// <summary>
    /// https://w3c.github.io/webcrypto/#hmac-operations-generate-key
    /// </summary>
    internal static (byte[] Handle, CryptoKeyAlgorithm Algorithm) GenerateKey(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        KeyUsage usages,
        string what)
    {
        // Step 1: "If usages contains an entry which is not 'sign' or 'verify', then throw a SyntaxError."
        if ((usages & ~AllowedUsages) != KeyUsage.None)
        {
            context.ThrowSyntaxError(
                what + ": an HMAC key supports the usages sign and verify, not " + KeyUsages.Describe(usages & ~AllowedUsages) + ".");
        }

        var hashName = normalized.HashName!;

        // Step 2: an absent length means the hash's block size, a present non-zero one is taken as it is, and
        // a present zero is an OperationError.
        uint length;
        if (normalized.Length is null)
        {
            length = BlockSizeInBits(hashName);
        }
        else if (normalized.Length.Value != 0)
        {
            length = normalized.Length.Value;
        }
        else
        {
            context.ThrowOperationError(what + ": a key length of zero bits was requested.");
            return default;
        }

        // Steps 3 and 4: generate a key of `length` bits, and report a generation that fails as an
        // OperationError. See the remarks on this class for why a length that is not a whole number of bytes
        // is one of the ways it fails here.
        if (length % 8 != 0)
        {
            context.ThrowOperationError(
                what + ": a key length of " + length + " bits is not a whole number of bytes, which this engine's key generation cannot produce.");
        }

        // A key large enough to be a denial of service rather than a key: the algorithm has no ceiling of its
        // own, but a 4 GiB `length` would otherwise be an allocation request from one line of script.
        if (length > MaxKeyLengthInBits)
        {
            context.ThrowOperationError(
                what + ": a key length of " + length + " bits exceeds the " + MaxKeyLengthInBits + " bits this engine will generate.");
        }

        var handle = new byte[length / 8];
        RandomNumberGenerator.Fill(handle);

        return (handle, new CryptoKeyAlgorithm(AlgorithmNormalization.Hmac, length, hashName));
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#hmac-operations-import-key
    /// </summary>
    internal static (byte[] Handle, CryptoKeyAlgorithm Algorithm) ImportKey(
        CryptoContext context,
        KeyFormat format,
        byte[]? rawData,
        JsonWebKeyData? jwk,
        NormalizedAlgorithm normalized,
        bool extractable,
        KeyUsage usages,
        string what)
    {
        // Step 1: "If the length member of normalizedAlgorithm is present and is zero, then throw a
        // DataError." Note the difference from generateKey, where the same value is an OperationError.
        if (normalized.Length == 0)
        {
            context.ThrowDataError(what + ": a key length of zero bits was requested.");
        }

        // Step 3.
        if ((usages & ~AllowedUsages) != KeyUsage.None)
        {
            context.ThrowSyntaxError(
                what + ": an HMAC key supports the usages sign and verify, not " + KeyUsages.Describe(usages & ~AllowedUsages) + ".");
        }

        var hashName = normalized.HashName!;
        byte[] data;

        switch (format)
        {
            case KeyFormat.Raw:
                data = rawData!;
                break;

            case KeyFormat.Jwk:
                data = jwk!.RequireOctAndDecodeKey(context, what);

                // "If the alg field of jwk is present and is not <HS1|HS256|HS384|HS512>, then throw a
                // DataError" — one branch per registered hash, which is the same table JwkAlgorithm holds.
                var expectedAlg = JwkAlgorithm(hashName);
                if (jwk.Alg is not null && !string.Equals(jwk.Alg, expectedAlg, StringComparison.Ordinal))
                {
                    context.ThrowDataError(
                        what + ": the alg field of the JSON Web Key is '" + jwk.Alg + "' rather than '" + expectedAlg + "', which is what " + hashName + " requires.");
                }

                jwk.ValidateUseKeyOpsAndExt(context, usages, extractable, "sig", what);
                break;

            default:
                // "Otherwise: throw a NotSupportedError" — spki and pkcs8 describe asymmetric keys.
                context.ThrowNotSupportedError(what + ": an HMAC key cannot be imported from the '" + KeyFormats.NameOf(format) + "' format.");
                return default;
        }

        // Steps 7 and 8: the length of the imported material, which may not be zero.
        var length = (uint) data.Length * 8;
        if (length == 0)
        {
            context.ThrowDataError(what + ": the imported key is empty.");
        }

        // Step 9: a requested length must name the same bytes — at most what arrived, and more than a whole
        // byte less, so that the material is neither truncated to fewer bytes nor padded.
        if (normalized.Length is { } requested)
        {
            if (requested > length)
            {
                context.ThrowDataError(
                    what + ": a key length of " + requested + " bits was requested, but the imported key is only " + length + " bits long.");
            }

            if (requested <= length - 8)
            {
                context.ThrowDataError(
                    what + ": a key length of " + requested + " bits was requested, which discards whole bytes of the " + length + "-bit imported key.");
            }

            length = requested;
        }

        return (data, new CryptoKeyAlgorithm(AlgorithmNormalization.Hmac, length, hashName));
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#hmac-operations-export-key
    /// </summary>
    internal static JsValue ExportKey(CryptoContext context, JsCryptoKey key, KeyFormat format, string what)
    {
        switch (format)
        {
            case KeyFormat.Raw:
                // The bytes are copied on the way out: what a script is handed is mutable, and the key is not.
                return context.CreateArrayBuffer(key.Handle.ToArray());

            case KeyFormat.Jwk:
                return JsonWebKeyData.CreateOctExport(
                    context.Engine,
                    key.Handle,
                    JwkAlgorithm(key.Algorithm.HashName!),
                    key.Usages,
                    key.Extractable);

            default:
                context.ThrowNotSupportedError(what + ": an HMAC key cannot be exported to the '" + KeyFormats.NameOf(format) + "' format.");
                return JsValue.Undefined;
        }
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#hmac-operations-sign — "the MAC Generation operation described in
    /// Section 4 of [FIPS-198-1] using the key represented by the [[handle]] internal slot of key, the hash
    /// function identified by the hash attribute … and message as the input data text".
    /// </summary>
    /// <remarks>
    /// The one-shot <c>HashData</c> statics rather than an <see cref="HMAC"/> instance: the message is one
    /// contiguous span already in memory, so there is nothing to feed in chunks, and the one-shot form
    /// allocates only the result — no hash object and no <c>IDisposable</c> to get wrong.
    /// </remarks>
    internal static byte[] Sign(JsCryptoKey key, ReadOnlySpan<byte> message)
    {
        var handle = key.Handle;

        switch (key.Algorithm.HashName)
        {
            case AlgorithmNormalization.Sha1:
                // SHA-1 is one of the four hashes the specification registers, every browser offers HMAC over
                // it, and HMAC-SHA-1 is not broken by the collision attacks that retired plain SHA-1 — it is
                // what TOTP and a great deal of existing infrastructure is built on. A script asking for it
                // has already chosen; nothing in the engine picks it for anybody.
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms -- the caller named SHA-1, we did not choose it
                return HMACSHA1.HashData(handle, message);
#pragma warning restore CA5350
            case AlgorithmNormalization.Sha256:
                return HMACSHA256.HashData(handle, message);
            case AlgorithmNormalization.Sha384:
                return HMACSHA384.HashData(handle, message);
            case AlgorithmNormalization.Sha512:
                return HMACSHA512.HashData(handle, message);
            default:
                // Unreachable: the hash name was matched against the registry when the key was made. It is
                // spelled out rather than folded into the last case so that a hash registered later cannot
                // silently be MAC'd with the wrong one.
                Throw.InvalidOperationException("Unhandled HMAC hash algorithm '" + key.Algorithm.HashName + "'.");
                return null!;
        }
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#hmac-operations-verify — the MAC is generated again and compared
    /// with the signature, and "this comparison must be performed in constant-time".
    /// </summary>
    /// <remarks>
    /// <see cref="CryptographicOperations.FixedTimeEquals"/> is the BCL's constant-time comparison, and the
    /// only correct way to spell this step: an ordinary comparison returns as soon as two bytes differ, which
    /// tells an attacker how much of a forged MAC was right and turns a forgery into a byte-at-a-time search.
    /// It answers <see langword="false"/> for lengths that differ, which the specification's "true if mac is
    /// equal to signature" also does.
    /// </remarks>
    internal static bool Verify(JsCryptoKey key, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> message)
    {
        var mac = Sign(key, message);
        return CryptographicOperations.FixedTimeEquals(mac, signature);
    }
}
#endif
