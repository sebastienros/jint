#if NET8_0_OR_GREATER
using System.Security.Cryptography;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The AES-KW <c>wrap key</c> and <c>unwrap key</c> operations — https://w3c.github.io/webcrypto/#aes-kw,
/// "key wrapping using AES, as described in [RFC3394]". Its key management is
/// <see cref="AesKeyManagement"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the only algorithm here registered for <c>wrapKey</c> and <c>unwrapKey</c>, and it is registered
/// for nothing else.</b> There is no <c>encrypt</c> and no <c>decrypt</c>: RFC 3394 wrapping takes a whole
/// number of 64-bit blocks, carries an integrity check of its own, and is not a general-purpose cipher — so
/// <c>encrypt({ name: 'AES-KW' }, …)</c> is a <c>NotSupportedError</c>, in a browser too. Every other way to
/// wrap a key in this engine — AES-GCM, AES-CBC, AES-CTR, RSA-OAEP — arrives through the <c>encrypt</c>
/// registry instead, which is what the <c>wrapKey</c> method's second normalization is for.
/// </para>
/// <para>
/// .NET 11 provides unpadded RFC 3394 key wrap directly. Earlier targets implement Sections 2.2.1 and
/// 2.2.2 over ECB one-shots; .NET 10's padded RFC 5649 API is a different algorithm and cannot replace it.
/// Both paths are checked against the published Section 4 vectors.
/// </para>
/// <para>
/// <b>The wrapped payload has a floor of two blocks as well as a step of one.</b> The Web Cryptography API's
/// own step is the multiple ("If plaintext is not a multiple of 64 bits in length, then throw an
/// OperationError"), and the floor is the wrapping algorithm's: NIST SP 800-38F §6.1 defines KW over a
/// plaintext of "2 ≤ n" semiblocks, and RFC 3394's 6n rounds degenerate for a single one. So the smallest
/// wrappable payload is 16 bytes and the smallest unwrappable ciphertext is 24 — both reported as the
/// <c>OperationError</c> the operation's own steps end in.
/// </para>
/// <para>
/// <b>An unwrap that fails says only that it failed.</b> The integrity check is a comparison of the recovered
/// <c>A</c> against the constant initial value, and it is made with
/// <see cref="CryptographicOperations.FixedTimeEquals(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/> against one message that carries nothing about how
/// close the ciphertext came — the same discipline RSA-OAEP's decrypt keeps, and for the same reason: a
/// distinguishable failure is an oracle. The recovered register file is zeroed before the throw, so nothing
/// that failed its check survives the frame.
/// </para>
/// </remarks>
internal static class AesKwAlgorithm
{
    /// <summary>The size of a 64-bit register — RFC 3394's <c>A</c> and each <c>R[i]</c> — in bytes.</summary>
    private const int SemiBlockSize = 8;

#if !NET11_0_OR_GREATER
    /// <summary>
    /// "The default initial value (IV) is defined to be the hexadecimal constant A6A6A6A6A6A6A6A6" —
    /// https://www.rfc-editor.org/rfc/rfc3394#section-2.2.3.1, which is the value the Web Cryptography API's
    /// steps name for both directions.
    /// </summary>
    private static ReadOnlySpan<byte> DefaultIv => [0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6];
#endif

    /// <summary>
    /// https://w3c.github.io/webcrypto/#aes-kw-operations-wrap-key — "the Key Wrap operation described in
    /// Section 2.2.1 of [RFC3394] with plaintext as the plaintext to be wrapped and using the default Initial
    /// Value defined in Section 2.2.3.1".
    /// </summary>
    internal static byte[] WrapKey(CryptoContext context, JsCryptoKey key, ReadOnlySpan<byte> plaintext, string what)
    {
        // Step 1, plus the floor the wrapping algorithm itself has — see the remarks on this class.
        if (plaintext.Length % SemiBlockSize != 0 || plaintext.Length < 2 * SemiBlockSize)
        {
            context.ThrowOperationError(
                what + ": the data to wrap is " + plaintext.Length
                + " bytes long, and AES-KW wraps a whole number of 64-bit blocks, at least two of them.");
        }

#if NET11_0_OR_GREATER
        using var aes = AesCiphers.Create(key);
        try
        {
            return aes.EncryptKeyWrap(plaintext);
        }
        catch (Exception e) when (AesCiphers.IsCipherFailure(e))
        {
            context.ThrowOperationError(what + ": the key could not be wrapped.");
            return null!;
        }
#else
        var n = plaintext.Length / SemiBlockSize;

        // C[0] is A and C[1..n] are the registers, laid out in the buffer the operation returns so that the
        // whole of step 2 runs in place — the index-based form of the algorithm, which RFC 3394 gives
        // precisely so that no rotation is needed.
        var buffer = new byte[plaintext.Length + SemiBlockSize];
        DefaultIv.CopyTo(buffer);
        plaintext.CopyTo(buffer.AsSpan(SemiBlockSize));

        using var aes = AesCiphers.Create(key);

        Span<byte> input = stackalloc byte[AesCiphers.BlockSize];
        Span<byte> output = stackalloc byte[AesCiphers.BlockSize];

        try
        {
            for (var j = 0; j < 6; j++)
            {
                for (var i = 1; i <= n; i++)
                {
                    // B = AES(K, A | R[i])
                    buffer.AsSpan(0, SemiBlockSize).CopyTo(input);
                    buffer.AsSpan(i * SemiBlockSize, SemiBlockSize).CopyTo(input.Slice(SemiBlockSize));
                    aes.EncryptEcb(input, output, PaddingMode.None);

                    // A = MSB(64, B) ^ t, where t = (n * j) + i.
                    output.Slice(0, SemiBlockSize).CopyTo(buffer);
                    XorCounter(buffer.AsSpan(0, SemiBlockSize), (long) n * j + i);

                    // R[i] = LSB(64, B)
                    output.Slice(SemiBlockSize).CopyTo(buffer.AsSpan(i * SemiBlockSize));
                }
            }
        }
        catch (Exception e) when (AesCiphers.IsCipherFailure(e))
        {
            CryptographicOperations.ZeroMemory(buffer);
            context.ThrowOperationError(what + ": the key could not be wrapped.");
            return null!;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
            CryptographicOperations.ZeroMemory(output);
        }

        return buffer;
#endif
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#aes-kw-operations-unwrap-key — "the Key Unwrap operation described in
    /// Section 2.2.2 of [RFC3394] … If the Key Unwrap operation returns an error, then throw an
    /// OperationError."
    /// </summary>
    internal static byte[] UnwrapKey(CryptoContext context, JsCryptoKey key, ReadOnlySpan<byte> ciphertext, string what)
    {
        // A wrapped payload is (n + 1) blocks for n ≥ 2, so anything shorter than three blocks, or not a
        // whole number of them, is an input the unwrap algorithm has no reading of at all. It is the same
        // OperationError an integrity failure earns, because a ciphertext of the wrong shape and one of the
        // right shape that does not verify are equally "this did not unwrap".
        if (ciphertext.Length % SemiBlockSize != 0 || ciphertext.Length < 3 * SemiBlockSize)
        {
            context.ThrowOperationError(
                what + ": the wrapped data is " + ciphertext.Length
                + " bytes long, and an AES-KW ciphertext is a whole number of 64-bit blocks, at least three of them.");
        }

#if NET11_0_OR_GREATER
        using var aes = AesCiphers.Create(key);
        try
        {
            return aes.DecryptKeyWrap(ciphertext);
        }
        catch (Exception e) when (AesCiphers.IsCipherFailure(e))
        {
            context.ThrowOperationError(what + ": the key could not be unwrapped.");
            return null!;
        }
#else
        var n = ciphertext.Length / SemiBlockSize - 1;

        var buffer = ciphertext.ToArray();

        using var aes = AesCiphers.Create(key);

        Span<byte> input = stackalloc byte[AesCiphers.BlockSize];
        Span<byte> output = stackalloc byte[AesCiphers.BlockSize];

        try
        {
            for (var j = 5; j >= 0; j--)
            {
                for (var i = n; i >= 1; i--)
                {
                    // B = AES-1(K, (A ^ t) | R[i]), where t = (n * j) + i.
                    buffer.AsSpan(0, SemiBlockSize).CopyTo(input);
                    XorCounter(input.Slice(0, SemiBlockSize), (long) n * j + i);
                    buffer.AsSpan(i * SemiBlockSize, SemiBlockSize).CopyTo(input.Slice(SemiBlockSize));
                    aes.DecryptEcb(input, output, PaddingMode.None);

                    // A = MSB(64, B); R[i] = LSB(64, B)
                    output.Slice(0, SemiBlockSize).CopyTo(buffer);
                    output.Slice(SemiBlockSize).CopyTo(buffer.AsSpan(i * SemiBlockSize));
                }
            }
        }
        catch (Exception e) when (AesCiphers.IsCipherFailure(e))
        {
            CryptographicOperations.ZeroMemory(buffer);
            context.ThrowOperationError(what + ": the key could not be unwrapped.");
            return null!;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
            CryptographicOperations.ZeroMemory(output);
        }

        // Step 3: "If A is an appropriate initial value … Else Return an error." See the remarks on this
        // class for why the comparison and the message are both indistinguishable.
        if (!CryptographicOperations.FixedTimeEquals(buffer.AsSpan(0, SemiBlockSize), DefaultIv))
        {
            CryptographicOperations.ZeroMemory(buffer);
            context.ThrowOperationError(what + ": the key could not be unwrapped.");
        }

        return buffer.AsSpan(SemiBlockSize).ToArray();
#endif
    }

#if !NET11_0_OR_GREATER
    /// <summary>
    /// <c>A ^ t</c>, where <c>t</c> is the round counter taken as a 64-bit big-endian integer — the one place
    /// the algorithm's round number reaches the data, and what makes the 6n rounds distinguishable from one
    /// another.
    /// </summary>
    private static void XorCounter(Span<byte> register, long t)
    {
        for (var k = 0; k < SemiBlockSize; k++)
        {
            register[SemiBlockSize - 1 - k] ^= (byte) (t >>> (8 * k));
        }
    }
#endif
}
#endif
