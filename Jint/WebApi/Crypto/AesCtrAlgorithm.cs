#if NET8_0_OR_GREATER
using System.Security.Cryptography;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The AES-CTR <c>encrypt</c> and <c>decrypt</c> operations — https://w3c.github.io/webcrypto/#aes-ctr,
/// "encryption and decryption using AES in Counter mode, as described in [NIST-SP800-38A]". Its key
/// management is <see cref="AesKeyManagement"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Encryption and decryption are one operation.</b> CTR turns the block cipher into a keystream generator
/// and the data is exclusive-ored with it, so the two directions differ in nothing at all — the specification
/// writes them out twice with the words "plaintext" and "ciphertext" exchanged, and
/// <see cref="Transform"/> is both.
/// </para>
/// <para>
/// <b>The counter is hand-rolled because the BCL has no CTR mode</b>, and it has to be: the increment is over
/// <i>the rightmost <c>length</c> bits</i> of the block, "the rest of the counter block is for the nonce" and
/// stays fixed, and the counter field wraps modulo 2^length rather than carrying into the nonce. .NET's
/// <see cref="Aes"/> offers ECB, CBC and CFB; the keystream is therefore built by encrypting successive
/// counter blocks with <see cref="PaddingMode.None"/> ECB, which is exactly the "forward cipher function"
/// Section 6.5 of [NIST-SP800-38A] applies. Nothing here is ECB <i>encryption</i> of the caller's data — the
/// data never enters the cipher.
/// </para>
/// <para>
/// <b>The wrap is required, not merely tolerated.</b> A <c>length</c> of 32 with a counter field at
/// <c>0xFFFFFFFF</c> continues at zero, leaving the leftmost 96 bits untouched, and the specification says so
/// by pointing at Appendix B.1 of [NIST-SP800-38A] — the standard incrementing function is defined modulo
/// 2^m. The web-platform tests pin exactly that case, so an implementation that carried into the nonce, or
/// that treated the whole block as one integer, would produce a keystream nobody else produces.
/// </para>
/// <para>
/// <b>Nothing stops a caller from reusing a counter block</b>, and nothing can: CTR's security rests entirely
/// on never encrypting two messages under one (key, counter) pair, and the counter is a caller-supplied
/// parameter. Neither this engine nor a browser tracks it. The same is true of a message long enough to
/// exhaust a short counter field and wrap back onto its own keystream, which is a real hazard of a small
/// <c>length</c> and is the caller's to avoid.
/// </para>
/// </remarks>
internal static class AesCtrAlgorithm
{
    /// <summary>
    /// https://w3c.github.io/webcrypto/#aes-ctr-operations-encrypt
    /// </summary>
    internal static byte[] Encrypt(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        JsCryptoKey key,
        ReadOnlySpan<byte> plaintext,
        string what)
        => Transform(context, normalized, key, plaintext, what);

    /// <summary>
    /// https://w3c.github.io/webcrypto/#aes-ctr-operations-decrypt, whose steps are the encrypt steps with
    /// the two words exchanged — see the remarks on this class.
    /// </summary>
    internal static byte[] Decrypt(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        JsCryptoKey key,
        ReadOnlySpan<byte> ciphertext,
        string what)
        => Transform(context, normalized, key, ciphertext, what);

    /// <summary>
    /// "The CTR Encryption operation described in Section 6.5 of [NIST-SP800-38A] using AES as the block
    /// cipher, the counter member of normalizedAlgorithm as the initial value of the counter block, the
    /// length member of normalizedAlgorithm as the input parameter m to the standard counter block
    /// incrementing function defined in Appendix B.1".
    /// </summary>
    private static byte[] Transform(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        JsCryptoKey key,
        ReadOnlySpan<byte> data,
        string what)
    {
        // Step 1: "If the counter member of normalizedAlgorithm does not have a length of 16 bytes, then
        // throw an OperationError."
        var counter = normalized.Counter!;
        if (counter.Length != AesCiphers.BlockSize)
        {
            context.ThrowOperationError(
                what + ": the counter is " + counter.Length
                + " bytes long, and an AES-CTR counter block is exactly 16 bytes — one AES block.");
        }

        // Step 2: "If the length member of normalizedAlgorithm is zero or is greater than 128, then throw an
        // OperationError." The member's IDL type is `octet`, so 256 was already a TypeError during
        // normalization; 0 and 129 are inside an octet and are refused here.
        var counterLength = normalized.CounterLength!.Value;
        if (counterLength is 0 or > 128)
        {
            context.ThrowOperationError(
                what + ": " + counterLength
                + " is not a valid AES-CTR counter length; the counter field is between 1 and 128 bits of the counter block.");
        }

        var result = new byte[data.Length];
        if (data.Length == 0)
        {
            // A zero-length message needs no keystream at all, and asking for one would encrypt a counter
            // block whose only use would be to be discarded.
            return result;
        }

        using var aes = AesCiphers.Create(key);

        Span<byte> block = stackalloc byte[AesCiphers.BlockSize];
        Span<byte> keystream = stackalloc byte[AesCiphers.BlockSize];
        counter.CopyTo(block);

        try
        {
            for (var offset = 0; offset < data.Length; offset += AesCiphers.BlockSize)
            {
                // The forward cipher function applied to the counter block. PaddingMode.None is what makes
                // this one block in and one block out rather than a padded two.
                aes.EncryptEcb(block, keystream, PaddingMode.None);

                var count = Math.Min(AesCiphers.BlockSize, data.Length - offset);
                for (var i = 0; i < count; i++)
                {
                    result[offset + i] = (byte) (data[offset + i] ^ keystream[i]);
                }

                Increment(block, counterLength);
            }
        }
        catch (Exception e) when (AesCiphers.IsCipherFailure(e))
        {
            CryptographicOperations.ZeroMemory(result);
            context.ThrowOperationError(what + ": the data could not be transformed.");
            return null!;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keystream);
        }

        return result;
    }

    /// <summary>
    /// The standard incrementing function of Appendix B.1 of [NIST-SP800-38A], applied to the rightmost
    /// <paramref name="counterLength"/> bits of <paramref name="block"/>: "the counter bits are interpreted
    /// as a big-endian integer and incremented by one", modulo 2^m, with everything to their left left alone.
    /// </summary>
    /// <remarks>
    /// The window rarely lands on a byte boundary, so the last byte it reaches is only partly its own: with
    /// <c>length</c> 12 the counter is the low four bits of byte 14 and all of byte 15, and the high four
    /// bits of byte 14 are nonce. The loop therefore carries through the whole bytes first and finishes with
    /// a masked add on the partial one — the carry out of the counter field is dropped rather than propagated
    /// into the nonce, which is what makes the field wrap modulo 2^m.
    /// </remarks>
    private static void Increment(Span<byte> block, int counterLength)
    {
        var wholeBytes = counterLength / 8;
        var partialBits = counterLength % 8;

        // The whole bytes at the right-hand end, least significant first.
        for (var i = 0; i < wholeBytes; i++)
        {
            var index = block.Length - 1 - i;
            block[index]++;
            if (block[index] != 0)
            {
                // No carry out of this byte, so nothing above it changes.
                return;
            }
        }

        if (partialBits == 0)
        {
            // The field is a whole number of bytes and every one of them wrapped: the counter is back at
            // zero, and the carry stops here rather than reaching the nonce.
            return;
        }

        // The partial byte: increment only the low `partialBits` of it, and put the untouched high bits back.
        var partialIndex = block.Length - 1 - wholeBytes;
        var mask = (byte) ((1 << partialBits) - 1);
        var incremented = (byte) ((block[partialIndex] + 1) & mask);
        block[partialIndex] = (byte) ((block[partialIndex] & ~mask) | incremented);
    }
}
#endif
