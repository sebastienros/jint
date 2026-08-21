#if NET8_0_OR_GREATER
using System.Security.Cryptography;
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The <c>generateKey</c>, <c>importKey</c>, <c>exportKey</c> and <c>get key length</c> operations of the four
/// AES algorithms — AES-CTR (https://w3c.github.io/webcrypto/#aes-ctr), AES-CBC
/// (https://w3c.github.io/webcrypto/#aes-cbc), AES-GCM (https://w3c.github.io/webcrypto/#aes-gcm) and AES-KW
/// (https://w3c.github.io/webcrypto/#aes-kw).
/// </summary>
/// <remarks>
/// <para>
/// <b>All four write the same four operations out four times.</b> Read
/// https://w3c.github.io/webcrypto/#aes-ctr-operations-import-key beside
/// https://w3c.github.io/webcrypto/#aes-cbc-operations-import-key and the difference is two strings: the
/// name the resulting <c>AesKeyAlgorithm</c> carries, and the JWK <c>alg</c> suffix the key material's length
/// is spelled with (<c>A128CTR</c> against <c>A128CBC</c>). So the steps live here once, parameterized by the
/// algorithm name, and each algorithm's own file holds only what is actually different about it — its
/// cipher. The one place a third value is needed is step 1's usage set, which AES-KW alone narrows.
/// </para>
/// <para>
/// <b>AES-KW's usage set is the reason step 1 is not shared as a constant.</b> Its registration lists
/// <c>wrapKey</c> and <c>unwrapKey</c> and nothing else, where the three ciphers list all four — so
/// <c>generateKey({ name: 'AES-KW', length: 128 }, false, ['encrypt'])</c> is a <c>SyntaxError</c> and the
/// same request against AES-CBC is an ordinary key. That asymmetry is normative and is what
/// <see cref="AllowedUsages"/> spells out.
/// </para>
/// <para>
/// The key material is the raw bytes and nothing else: unlike every asymmetric algorithm here, an AES key has
/// no DER form to hold, so the <c>[[handle]]</c> is what <c>importKey('raw', …)</c> was given and what
/// <c>exportKey('raw', …)</c> hands back — a fresh copy, because script may write into what it is given.
/// </para>
/// </remarks>
internal static class AesKeyManagement
{
    /// <summary>The usages an AES cipher key may carry — the set AES-CTR, AES-CBC and AES-GCM all name.</summary>
    private const KeyUsage CipherUsages = KeyUsage.Encrypt | KeyUsage.Decrypt | KeyUsage.WrapKey | KeyUsage.UnwrapKey;

    /// <summary>
    /// The usages an AES-KW key may carry — "If usages contains an entry which is not one of 'wrapKey' or
    /// 'unwrapKey', then throw a SyntaxError."
    /// </summary>
    private const KeyUsage KeyWrapUsages = KeyUsage.WrapKey | KeyUsage.UnwrapKey;

    /// <summary>
    /// https://w3c.github.io/webcrypto/#aes-ctr-operations-generate-key and its three siblings, which differ
    /// only in step 1's usage set and in the name the <c>AesKeyAlgorithm</c> carries.
    /// </summary>
    internal static (byte[] Handle, CryptoKeyAlgorithm Algorithm) GenerateKey(
        CryptoContext context,
        string name,
        NormalizedAlgorithm normalized,
        KeyUsage usages,
        string what)
    {
        // Step 1.
        RequireUsagesWithin(context, usages, name, what);

        // Step 2: "If the length member of normalizedAlgorithm is not equal to one of 128, 192 or 256, then
        // throw an OperationError."
        var length = normalized.Length!.Value;
        if (!IsValidKeyLength(length))
        {
            context.ThrowOperationError(what + ": " + length + " is not a valid AES key length (128, 192 or 256 bits).");
        }

        var handle = new byte[length / 8];
        RandomNumberGenerator.Fill(handle);

        return (handle, new CryptoKeyAlgorithm(name, length, HashName: null));
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#aes-ctr-operations-import-key and its three siblings.
    /// </summary>
    internal static (byte[] Handle, CryptoKeyAlgorithm Algorithm) ImportKey(
        CryptoContext context,
        string name,
        KeyFormat format,
        byte[]? rawData,
        JsonWebKeyData? jwk,
        bool extractable,
        KeyUsage usages,
        string what)
    {
        // Step 1.
        RequireUsagesWithin(context, usages, name, what);

        byte[] data;

        switch (format)
        {
            case KeyFormat.Raw:
                data = rawData!;
                if (!IsValidKeyLength((uint) data.Length * 8))
                {
                    context.ThrowDataError(
                        what + ": " + ((uint) data.Length * 8) + " is not a valid AES key length (128, 192 or 256 bits).");
                }

                break;

            case KeyFormat.Jwk:
                data = jwk!.RequireOctAndDecodeKey(context, what);

                // "If the length in bits of data is 128 / 192 / 256: if the alg field of jwk is present and
                // is not A128CTR / A192CTR / A256CTR, then throw a DataError. Otherwise: throw a DataError."
                // The final "otherwise" is what catches a key of any other length, so the length check and
                // the alg check are one step here rather than two.
                if (!IsValidKeyLength((uint) data.Length * 8))
                {
                    context.ThrowDataError(
                        what + ": " + ((uint) data.Length * 8) + " is not a valid AES key length (128, 192 or 256 bits).");
                }

                var expectedAlg = JwkAlgorithm(name, (uint) data.Length * 8);
                if (jwk.Alg is not null && !string.Equals(jwk.Alg, expectedAlg, StringComparison.Ordinal))
                {
                    context.ThrowDataError(
                        what + ": the alg field of the JSON Web Key is '" + jwk.Alg + "' rather than '" + expectedAlg
                        + "', which is what a " + (data.Length * 8) + "-bit " + name + " key requires.");
                }

                jwk.ValidateUseKeyOpsAndExt(context, usages, extractable, "enc", what);
                break;

            default:
                context.ThrowNotSupportedError(
                    what + ": an " + name + " key cannot be imported from the '" + KeyFormats.NameOf(format) + "' format.");
                return default;
        }

        return (data, new CryptoKeyAlgorithm(name, (uint) data.Length * 8, HashName: null));
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#aes-ctr-operations-get-key-length and its three siblings — "If the
    /// length member of normalizedDerivedKeyAlgorithm is not 128, 192 or 256, then throw an OperationError.
    /// Return the length member of normalizedDerivedKeyAlgorithm."
    /// </summary>
    /// <remarks>
    /// The registered parameters are <c>AesDerivedKeyParams</c>, which is <c>AesKeyGenParams</c> declared a
    /// second time under another name — one <c>required [EnforceRange] unsigned short length</c> — so the
    /// member is already read and range-checked by the time this is called, and the three lengths AES has are
    /// the whole of what is left to say. Nothing about it depends on which of the four algorithms asked, which
    /// is why this one takes no name.
    /// </remarks>
    internal static uint GetKeyLength(CryptoContext context, NormalizedAlgorithm normalized, string what)
    {
        var length = normalized.Length!.Value;

        if (!IsValidKeyLength(length))
        {
            context.ThrowOperationError(what + ": " + length + " is not a valid AES key length (128, 192 or 256 bits).");
        }

        return length;
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#aes-ctr-operations-export-key and its three siblings.
    /// </summary>
    internal static JsValue ExportKey(CryptoContext context, JsCryptoKey key, KeyFormat format, string what)
    {
        var name = key.Algorithm.Name;

        switch (format)
        {
            case KeyFormat.Raw:
                // Copied on the way out: what a script is handed is mutable, and the key is not.
                return context.CreateArrayBuffer(key.Handle.ToArray());

            case KeyFormat.Jwk:
                return JsonWebKeyData.CreateOctExport(
                    context.Engine,
                    key.Handle,
                    JwkAlgorithm(name, key.Algorithm.Length),
                    key.Usages,
                    key.Extractable);

            default:
                context.ThrowNotSupportedError(
                    what + ": an " + name + " key cannot be exported to the '" + KeyFormats.NameOf(format) + "' format.");
                return JsValue.Undefined;
        }
    }

    /// <summary>The three key lengths AES has, in bits.</summary>
    internal static bool IsValidKeyLength(uint bits) => bits is 128 or 192 or 256;

    /// <summary>
    /// Step 1 of both <c>generateKey</c> and <c>importKey</c>: "If usages contains an entry which is not one
    /// of … then throw a SyntaxError."
    /// </summary>
    private static void RequireUsagesWithin(CryptoContext context, KeyUsage usages, string name, string what)
    {
        var allowed = AllowedUsages(name);
        var extra = usages & ~allowed;

        if (extra != KeyUsage.None)
        {
            context.ThrowSyntaxError(
                what + ": an " + name + " key supports the usages " + KeyUsages.Describe(allowed) + ", not "
                + KeyUsages.Describe(extra) + ".");
        }
    }

    /// <summary>
    /// The usage set step 1 of each algorithm's <c>generateKey</c> and <c>importKey</c> names — see the
    /// remarks on this class for why AES-KW's is the narrow one.
    /// </summary>
    /// <remarks>
    /// Every registered AES name is spelled out and the default throws rather than one of them standing in as
    /// the fallback, which is the convention every table in this folder follows: an algorithm added without a
    /// case here would silently inherit whichever set was chosen as the default, and for a usage set that
    /// means silently granting a use its registration does not.
    /// </remarks>
    private static KeyUsage AllowedUsages(string name)
    {
        switch (name)
        {
            case AlgorithmNormalization.AesCtr:
            case AlgorithmNormalization.AesCbc:
            case AlgorithmNormalization.AesGcm:
                return CipherUsages;
            case AlgorithmNormalization.AesKw:
                return KeyWrapUsages;
            default:
                Throw.InvalidOperationException("Unhandled AES algorithm '" + name + "'.");
                return default;
        }
    }

    /// <summary>
    /// The JWK <c>alg</c> field naming an AES key of a given algorithm and length —
    /// https://www.rfc-editor.org/rfc/rfc7518#section-4.7 for the ciphers and #section-4.4 for AES-KW, which
    /// is where the twelve names <c>A128CBC</c> … <c>A256KW</c> come from.
    /// </summary>
    /// <remarks>
    /// Both halves are tables with a throwing default rather than a computation over the name: a key of a
    /// length AES does not have, labelled <c>A256GCM</c>, would be a JWK naming an algorithm it is not, and an
    /// algorithm whose suffix nobody chose would be labelled with whichever one happened to be last. Every
    /// length has been checked before a key exists, so neither arm is reachable.
    /// </remarks>
    private static string JwkAlgorithm(string name, uint bits) => string.Concat("A", KeySizeName(bits), JwkSuffix(name));

    private static string KeySizeName(uint bits)
    {
        switch (bits)
        {
            case 128:
                return "128";
            case 192:
                return "192";
            case 256:
                return "256";
            default:
                Throw.InvalidOperationException("Unhandled AES key length of " + bits + " bits.");
                return null!;
        }
    }

    private static string JwkSuffix(string name)
    {
        switch (name)
        {
            case AlgorithmNormalization.AesCtr:
                return "CTR";
            case AlgorithmNormalization.AesCbc:
                return "CBC";
            case AlgorithmNormalization.AesGcm:
                return "GCM";
            case AlgorithmNormalization.AesKw:
                return "KW";
            default:
                Throw.InvalidOperationException("Unhandled AES algorithm '" + name + "'.");
                return null!;
        }
    }
}
#endif
