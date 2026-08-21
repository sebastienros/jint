#if NET8_0_OR_GREATER
using System.Security.Cryptography;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The AES-CBC <c>encrypt</c> and <c>decrypt</c> operations — https://w3c.github.io/webcrypto/#aes-cbc,
/// "encryption and decryption using AES in Cipher Block Chaining mode, as described in [NIST-SP800-38A]".
/// Its key management is <see cref="AesKeyManagement"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The padding is always there and is always PKCS#7.</b> "In the Web Crypto API, the only padding mode
/// that is supported is that of PKCS#7", and the encrypt steps add it unconditionally — so a plaintext that
/// is already a whole number of blocks gains a <i>whole extra block</i> of <c>0x10</c> bytes, and the
/// ciphertext of the four NIST SP 800-38A example blocks is five blocks long here. There is no way to ask for
/// an unpadded CBC through this API, which is what makes <c>decrypt</c> able to recover the exact length.
/// </para>
/// <para>
/// <b>The padding check is written out rather than left to the platform.</b>
/// <see cref="SymmetricAlgorithm.DecryptCbc(ReadOnlySpan{byte}, ReadOnlySpan{byte}, PaddingMode)"/> with
/// <see cref="PaddingMode.PKCS7"/> would strip the padding itself, but what it does with padding that is
/// <i>wrong</i> is the platform's business — .NET raises a <see cref="CryptographicException"/> whose message
/// differs per implementation, and which of the two failures a given build reports first is not something to
/// pin. So the decryption is asked for with <see cref="PaddingMode.None"/> and steps 4 to 6 are performed
/// here, exactly as the specification numbers them: read the last octet, check every one of the last <c>p</c>
/// octets against it, and remove them. That also makes the failure the specification's own
/// <c>OperationError</c> rather than a CLR exception erupting out of a promise-returning operation.
/// </para>
/// <para>
/// <b>A padding failure says nothing about which block was wrong.</b> One message covers every way the
/// unpadding can fail, for the reason AES-GCM's decrypt gives: an attacker who can tell "the padding was
/// malformed" from "the padding was fine" has a padding oracle, and CBC without an authentication tag is
/// exactly the shape that attack was invented for. This engine cannot make CBC authenticated — the
/// specification's algorithm is unauthenticated by construction — but it can decline to be the oracle's
/// distinguisher, and the check runs over the whole padding rather than stopping at the first bad octet.
/// </para>
/// </remarks>
internal static class AesCbcAlgorithm
{
    /// <summary>The AES block size in bytes, which is the <c>iv</c> length and the padding modulus <c>k</c>.</summary>
    private const int BlockSize = AesCiphers.BlockSize;

    /// <summary>
    /// https://w3c.github.io/webcrypto/#aes-cbc-operations-encrypt
    /// </summary>
    internal static byte[] Encrypt(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        JsCryptoKey key,
        ReadOnlySpan<byte> plaintext,
        string what)
    {
        var iv = RequireIv(context, normalized, what);

        using var aes = AesCiphers.Create(key);

        try
        {
            // Steps 2 and 3 in one call: the BCL's own PKCS#7 padding is "the procedure defined in Section
            // 10.3 of [RFC2315], step 2, with a value of k of 16" — k octets each holding k, and a whole
            // block of them when the plaintext already ends on a boundary.
            return aes.EncryptCbc(plaintext, iv, PaddingMode.PKCS7);
        }
        catch (Exception e) when (AesCiphers.IsCipherFailure(e))
        {
            context.ThrowOperationError(what + ": the data could not be encrypted.");
            return null!;
        }
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#aes-cbc-operations-decrypt
    /// </summary>
    internal static byte[] Decrypt(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        JsCryptoKey key,
        ReadOnlySpan<byte> ciphertext,
        string what)
    {
        var iv = RequireIv(context, normalized, what);

        // Step 2: "If the length of ciphertext is zero or is not a multiple of 16 bytes, then throw an
        // OperationError." The empty ciphertext is refused rather than answered with the empty plaintext,
        // because an AES-CBC encryption of anything is at least one padding block long.
        if (ciphertext.Length == 0 || ciphertext.Length % BlockSize != 0)
        {
            context.ThrowOperationError(
                what + ": the ciphertext is " + ciphertext.Length
                + " bytes long, and an AES-CBC ciphertext is a non-zero whole number of 16-byte blocks.");
        }

        using var aes = AesCiphers.Create(key);

        byte[] paddedPlaintext;

        try
        {
            // Step 3, with the unpadding left to steps 4 to 6 below — see the remarks on this class.
            paddedPlaintext = aes.DecryptCbc(ciphertext, iv, PaddingMode.None);
        }
        catch (Exception e) when (AesCiphers.IsCipherFailure(e))
        {
            // The same message the padding check below gives, so that a platform which refuses something
            // here and a padding that is merely wrong are one answer rather than two.
            context.ThrowOperationError(what + ": the data could not be decrypted.");
            return null!;
        }

        // Steps 4 and 5: "Let p be the value of the last octet of paddedPlaintext. If p is zero or greater
        // than 16, or if any of the last p octets of paddedPlaintext have a value which is not p, then throw
        // an OperationError."
        var p = paddedPlaintext[paddedPlaintext.Length - 1];
        var valid = p is > 0 and <= BlockSize;

        // Every octet of the claimed padding is examined even once one has already failed, so that the time
        // taken does not report how much of the padding was right.
        for (var i = 1; i <= BlockSize; i++)
        {
            var inPadding = i <= p;
            var octet = paddedPlaintext[paddedPlaintext.Length - i];
            valid &= !inPadding || octet == p;
        }

        if (!valid)
        {
            CryptographicOperations.ZeroMemory(paddedPlaintext);
            context.ThrowOperationError(what + ": the data could not be decrypted.");
        }

        // Step 6: "Let plaintext be the result of removing p octets from the end of paddedPlaintext."
        var plaintext = paddedPlaintext.AsSpan(0, paddedPlaintext.Length - p).ToArray();
        CryptographicOperations.ZeroMemory(paddedPlaintext);
        return plaintext;
    }

    /// <summary>
    /// Step 1 of both operations: "If the iv member of normalizedAlgorithm does not have a length of 16
    /// bytes, then throw an OperationError." Unlike AES-GCM's, this restriction is the algorithm's own and
    /// not the platform's — CBC's IV is one block by definition.
    /// </summary>
    private static byte[] RequireIv(CryptoContext context, NormalizedAlgorithm normalized, string what)
    {
        var iv = normalized.Iv!;
        if (iv.Length != BlockSize)
        {
            context.ThrowOperationError(
                what + ": the iv is " + iv.Length + " bytes long, and an AES-CBC iv is exactly 16 bytes — one AES block.");
        }

        return iv;
    }
}
#endif
