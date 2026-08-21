#if NET8_0_OR_GREATER
using System.Security.Cryptography;
using Jint.Runtime;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The PBKDF2 operations — https://w3c.github.io/webcrypto/#pbkdf2, "key derivation using the PKCS#5
/// password-based key derivation function version 2, as defined in [RFC8018] using HMAC as the pseudo-random
/// function, using the SHA hash functions defined in this specification".
/// </summary>
/// <remarks>
/// <para>
/// <b>A PBKDF2 key is <c>importKey</c>-only</b>, exactly as an HKDF key is and for the same reasons — the
/// registration lists <c>deriveBits</c>, <c>importKey</c> and <c>get key length</c>, the import steps refuse
/// any <c>extractable</c> that is not <see langword="false"/>, and the <c>[[handle]]</c> is the password
/// verbatim, so there is deliberately no way to read it back. The <c>[[algorithm]]</c> is the bare
/// <c>KeyAlgorithm</c>: the hash, the salt and the iteration count all belong to each derivation, so one
/// imported password derives under SHA-256 and SHA-512 alike.
/// </para>
/// <para>
/// <b>The iteration count is bounded by this engine, and the bound is a real one.</b> PBKDF2 is a loop whose
/// only purpose is to be slow, its trip count comes straight from script, and the whole of it happens inside
/// one BCL call — so no execution constraint can interrupt it, not a timeout, not a statement budget, not a
/// cancellation token. <c>iterations: 2 ** 40</c> is one line of script that takes days. The ceiling is
/// <see cref="MaxIterations"/> = 2^22 = 4,194,304, which is above every OWASP 2023 recommendation for this
/// function (1,300,000 for SHA-1, 600,000 for SHA-256, 210,000 for SHA-512) and bounds one call to roughly
/// 1.7 seconds at the ceiling itself — measured 0.35 s for SHA-256, 1.5 s for SHA-1 and 1.7 s for SHA-512 —
/// and the refusal names the restriction. It is the same reasoning as the 8192-bit ceiling on RSA key
/// generation, one file over.
/// </para>
/// </remarks>
internal static class Pbkdf2Algorithm
{
    /// <summary>
    /// The usages a PBKDF2 key may carry — "If usages contains a value that is not 'deriveKey' or
    /// 'deriveBits', then throw a SyntaxError".
    /// </summary>
    private const KeyUsage AllowedUsages = KeyUsage.DeriveKey | KeyUsage.DeriveBits;

    /// <summary>
    /// The largest iteration count this engine will run. See the remarks on this class for why the algorithm
    /// having no ceiling of its own is not a reason for this engine to have none either.
    /// </summary>
    private const uint MaxIterations = 4_194_304;

    /// <summary>
    /// https://w3c.github.io/webcrypto/#pbkdf2-operations — "Derive Bits".
    /// </summary>
    internal static byte[] DeriveBits(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        JsCryptoKey key,
        uint? length,
        string what)
    {
        // Step 1: "If length is null or is not a multiple of 8, then throw an OperationError."
        if (length is not { } bits)
        {
            context.ThrowOperationError(
                what + ": PBKDF2 has no output length of its own, so deriveBits needs one — a length of null, which is what an omitted argument means, cannot be derived.");
            return null!;
        }

        if (bits % 8 != 0)
        {
            context.ThrowOperationError(
                what + ": a length of " + bits + " bits is not a whole number of bytes, which this operation requires.");
        }

        // Step 2: "If the iterations member of normalizedAlgorithm is zero, then throw an OperationError."
        var iterations = normalized.Iterations!.Value;
        if (iterations == 0)
        {
            context.ThrowOperationError(what + ": an iteration count of zero was requested.");
        }

        // This engine's own ceiling, after the algorithm's own two checks so that they still outrank it.
        if (iterations > MaxIterations)
        {
            context.ThrowOperationError(
                what + ": an iteration count of " + iterations + " exceeds the " + MaxIterations
                + " this engine will run, because the loop is a single uninterruptible call that no execution constraint can bound.");
        }

        // Step 3: "If length is zero, return an empty byte sequence." Spelled out by this algorithm where
        // HKDF's steps leave it implied.
        if (bits == 0)
        {
            return [];
        }

        // Steps 4 and 5: PBKDF2 with HMAC as the pseudo-random function, the key's own bytes as the password,
        // and length divided by 8 as dkLen.
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                key.Handle,
                normalized.Salt!,
                (int) iterations,
                HashName(normalized.HashName!),
                (int) (bits / 8));
        }
        catch (Exception e) when (e is CryptographicException or ArgumentException)
        {
            // Step 6: "If the key derivation operation fails, then throw an OperationError." The catch is
            // wider than CryptographicException for the reason HKDF's is: an argument the platform will not
            // accept arrives as an ArgumentException, and a CLR exception must never escape a
            // promise-returning operation.
            context.ThrowOperationError(what + ": the key derivation failed.");
            return null!;
        }
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#pbkdf2-operations — "Import key".
    /// </summary>
    /// <remarks>
    /// The <c>[[handle]]</c> of a PBKDF2 key <i>is</i> the password, so an extractable one would be a way to
    /// read a password back out of a key; the import steps refuse it, and the algorithm has no
    /// <c>exportKey</c> registration either, so the two together leave the bytes no door out.
    /// </remarks>
    internal static (byte[] Handle, string KeyType, CryptoKeyAlgorithm Algorithm) ImportKey(
        CryptoContext context,
        KeyFormat format,
        byte[]? rawData,
        bool extractable,
        KeyUsage usages,
        string what)
    {
        // Step 1: "If format is not 'raw', throw a NotSupportedError."
        if (format != KeyFormat.Raw)
        {
            context.ThrowNotSupportedError(
                what + ": a PBKDF2 key can only be imported from the 'raw' format, not from '" + KeyFormats.NameOf(format) + "'.");
        }

        // Step 2.
        if ((usages & ~AllowedUsages) != KeyUsage.None)
        {
            context.ThrowSyntaxError(
                what + ": a PBKDF2 key supports the usages deriveKey and deriveBits, not "
                + KeyUsages.Describe(usages & ~AllowedUsages) + ".");
        }

        // Step 3.
        if (extractable)
        {
            context.ThrowSyntaxError(what + ": a PBKDF2 key must be imported with extractable false.");
        }

        return (rawData!, CryptoKeyTypes.Secret, new CryptoKeyAlgorithm(AlgorithmNormalization.Pbkdf2, Length: 0, HashName: null));
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#pbkdf2-operations — "Get key length": "Return null." A PBKDF2 key is
    /// a password and has no length of its own.
    /// </summary>
    internal static uint? GetKeyLength() => null;

    private static HashAlgorithmName HashName(string hashName)
    {
        switch (hashName)
        {
            case AlgorithmNormalization.Sha1:
                // PBKDF2-HMAC-SHA-1 is what a great deal of existing data was derived with, and reading that
                // data back is the case this exists for. A script asking for it has already chosen; nothing
                // here picks it for anybody.
                return HashAlgorithmName.SHA1;
            case AlgorithmNormalization.Sha256:
                return HashAlgorithmName.SHA256;
            case AlgorithmNormalization.Sha384:
                return HashAlgorithmName.SHA384;
            case AlgorithmNormalization.Sha512:
                return HashAlgorithmName.SHA512;
            default:
                // Unreachable: the hash was matched against the digest registry during normalization.
                Throw.InvalidOperationException("Unhandled PBKDF2 hash algorithm '" + hashName + "'.");
                return default;
        }
    }
}
#endif
