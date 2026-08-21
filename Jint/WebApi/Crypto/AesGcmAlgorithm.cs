#if NET8_0_OR_GREATER
using System.Security.Cryptography;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The AES-GCM <c>encrypt</c> and <c>decrypt</c> operations — https://w3c.github.io/webcrypto/#aes-gcm,
/// "authenticated encryption and decryption using AES in Galois/Counter Mode mode, as described in
/// [NIST-SP800-38D]". Its key management is <see cref="AesKeyManagement"/>, shared with the three other AES
/// algorithms whose steps are the same four to the letter.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ciphertext carries its tag.</b> The encrypt operation's last step is "Let ciphertext be equal to
/// C | T, where '|' denotes concatenation", and decrypt takes "the last tagLength bits of ciphertext" back
/// off again. That layout is the Web Cryptography API's own, not GCM's — a host decrypting the same bytes
/// with <see cref="AesGcm"/> directly has to split them itself, and a host encrypting for a script has to
/// append the tag.
/// </para>
/// <para>
/// <b>Two limits of the platform's AES-GCM are visible here</b>, and both are reported as the
/// <c>OperationError</c> the algorithm's own steps end in rather than pretended away.
/// <see cref="AesGcm"/> accepts a 96-bit nonce and nothing else (<see cref="AesGcm.NonceByteSizes"/> is
/// 12 to 12), where the specification allows an <c>iv</c> "up to 2^64-1 bytes long"; and it accepts a tag of
/// 96 to 128 bits (<see cref="AesGcm.TagByteSizes"/> is 12 to 16), where the specification's list also
/// contains 32 and 64. A 96-bit IV is what [NIST-SP800-38D] itself recommends — it is the only length GCM
/// uses directly rather than folding through GHASH — and a tag shorter than 96 bits carries a forgery
/// probability the same document restricts to a short list of applications, so the shapes refused here are
/// the ones a new protocol should not be choosing anyway. Reading data another implementation produced with
/// them is the case this cannot serve, and the message says so.
/// </para>
/// <para>
/// Truncating a 128-bit tag on the way out would have covered the 32- and 64-bit cases for encryption, since
/// a GCM tag of <c>t</c> bits is by definition the first <c>t</c> bits of the full one. It is deliberately
/// not done: verification cannot be built the same way, because the platform will not check a tag it
/// considers too short and the full tag cannot be recomputed without the plaintext, so the result would be an
/// engine that encrypts what it cannot decrypt.
/// </para>
/// </remarks>
internal static class AesGcmAlgorithm
{
    /// <summary>The only nonce length <see cref="AesGcm"/> accepts, in bytes.</summary>
    private const int SupportedIvLength = 12;

    /// <summary>The default tag length in bits, "If the tagLength member of normalizedAlgorithm is not present".</summary>
    private const int DefaultTagLength = 128;

    /// <summary>
    /// https://w3c.github.io/webcrypto/#aes-gcm-operations-encrypt
    /// </summary>
    internal static byte[] Encrypt(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        JsCryptoKey key,
        ReadOnlySpan<byte> plaintext,
        string what)
    {
        // Steps 1, 2 and 4 — the length ceilings on iv, additionalData and plaintext — cannot be reached: a
        // byte sequence in this engine comes from an ArrayBuffer, whose length is an int.
        var tagLength = ResolveTagLength(context, normalized, what);
        var iv = RequireSupportedIv(context, normalized, what);
        var additionalData = normalized.AdditionalData ?? [];

        var tagBytes = tagLength / 8;
        var ciphertext = new byte[plaintext.Length + tagBytes];

        using var aes = CreateAes(context, key, tagBytes, what);

        // Steps 6 and 7 in one write: the authenticated encryption function's C goes to the front of the
        // buffer and its T straight after it, which is the concatenation the operation returns.
        aes.Encrypt(iv, plaintext, ciphertext.AsSpan(0, plaintext.Length), ciphertext.AsSpan(plaintext.Length), additionalData);

        return ciphertext;
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#aes-gcm-operations-decrypt
    /// </summary>
    internal static byte[] Decrypt(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        JsCryptoKey key,
        ReadOnlySpan<byte> ciphertext,
        string what)
    {
        // Step 1 comes before the iv checks here as it does there, so a bad tagLength outranks a bad iv.
        var tagLength = ResolveTagLength(context, normalized, what);
        var iv = RequireSupportedIv(context, normalized, what);
        var additionalData = normalized.AdditionalData ?? [];

        var tagBytes = tagLength / 8;

        // Step 4: "If ciphertext has a length in bits less than tagLength, then throw an OperationError."
        if (ciphertext.Length < tagBytes)
        {
            context.ThrowOperationError(
                what + ": the ciphertext is " + (ciphertext.Length * 8) + " bits long, which is shorter than the " + tagLength + "-bit authentication tag it must end with.");
        }

        // Steps 5 and 6.
        var actualCiphertext = ciphertext.Slice(0, ciphertext.Length - tagBytes);
        var tag = ciphertext.Slice(ciphertext.Length - tagBytes);

        var plaintext = new byte[actualCiphertext.Length];

        using var aes = CreateAes(context, key, tagBytes, what);

        try
        {
            aes.Decrypt(iv, actualCiphertext, tag, plaintext, additionalData);
        }
        catch (CryptographicException)
        {
            // Step 7: "If the result of the algorithm is the indication of inauthenticity, 'FAIL': throw an
            // OperationError." One message for every way the decryption can fail, carrying nothing about
            // which part of the input was wrong — the whole value of an authenticated cipher is that a
            // ciphertext, a tag, an IV or an additionalData that does not belong are one answer, not four.
            // .NET clears the plaintext buffer before it throws, so nothing unauthenticated survives either.
            context.ThrowOperationError(what + ": the data could not be decrypted.");
        }

        return plaintext;
    }

    /// <summary>
    /// Step 3 of both operations: the tag length is 128 bits when the member is absent, one of the seven
    /// listed values when it is present, and an <c>OperationError</c> otherwise.
    /// </summary>
    private static int ResolveTagLength(CryptoContext context, NormalizedAlgorithm normalized, string what)
    {
        if (normalized.TagLength is not { } tagLength)
        {
            return DefaultTagLength;
        }

        if (tagLength is not (32 or 64 or 96 or 104 or 112 or 120 or 128))
        {
            context.ThrowOperationError(
                what + ": " + tagLength + " is not a valid AES-GCM tag length (32, 64, 96, 104, 112, 120 or 128 bits).");
        }

        // What the platform's own implementation accepts, which is narrower than the specification's list
        // everywhere and differs per OS: OpenSSL and CNG take 96..128 bits, Apple's CryptoKit takes 128 and
        // nothing else. Asking rather than assuming is what keeps this an OperationError instead of a raw
        // ArgumentException erupting out of a promise-returning operation.
        if (AesGcm.IsSupported && !IsSupportedTagSize(tagLength / 8))
        {
            context.ThrowOperationError(
                what + ": a " + tagLength + "-bit authentication tag is not supported by this platform's AES-GCM implementation.");
        }

        return tagLength;
    }

    private static bool IsSupportedTagSize(int tagBytes)
    {
        var sizes = AesGcm.TagByteSizes;
        if (tagBytes < sizes.MinSize || tagBytes > sizes.MaxSize)
        {
            return false;
        }

        return sizes.SkipSize == 0 || (tagBytes - sizes.MinSize) % sizes.SkipSize == 0;
    }

    /// <summary>
    /// The <c>iv</c>, checked against what <see cref="AesGcm"/> accepts. See the remarks on this class for
    /// why an IV of another length is refused rather than folded through GHASH.
    /// </summary>
    private static byte[] RequireSupportedIv(CryptoContext context, NormalizedAlgorithm normalized, string what)
    {
        var iv = normalized.Iv!;
        if (iv.Length != SupportedIvLength)
        {
            context.ThrowOperationError(
                what + ": the iv is " + (iv.Length * 8) + " bits long, and this platform's AES-GCM implementation accepts only the 96-bit iv that NIST SP 800-38D recommends.");
        }

        return iv;
    }

    private static AesGcm CreateAes(CryptoContext context, JsCryptoKey key, int tagBytes, string what)
    {
        if (!AesGcm.IsSupported)
        {
            context.ThrowOperationError(what + ": this platform has no AES-GCM implementation.");
        }

        return new AesGcm(key.Handle, tagBytes);
    }
}
#endif
