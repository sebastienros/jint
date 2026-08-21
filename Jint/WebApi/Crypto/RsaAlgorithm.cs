#if NET8_0_OR_GREATER
using System.Numerics;
using System.Security.Cryptography;
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The three RSA algorithms — RSASSA-PKCS1-v1_5 (https://w3c.github.io/webcrypto/#rsassa-pkcs1), RSA-PSS
/// (https://w3c.github.io/webcrypto/#rsa-pss) and RSA-OAEP (https://w3c.github.io/webcrypto/#rsa-oaep) —
/// which share every operation but <c>sign</c>/<c>verify</c> against <c>encrypt</c>/<c>decrypt</c>, and share
/// all of <c>generateKey</c>, <c>importKey</c> and <c>exportKey</c> down to the usage sets and the JWK
/// <c>alg</c> table.
/// </summary>
/// <remarks>
/// <para>
/// <b>The key's <c>[[handle]]</c> is DER, never a live <see cref="RSA"/>.</b> A public key holds the bytes of
/// a <c>SubjectPublicKeyInfo</c> and a private key those of a <c>PrivateKeyInfo</c>, and each operation
/// rehydrates an <see cref="RSA"/> inside one <c>using</c>. That is what keeps <see cref="JsCryptoKey"/> free
/// of <see cref="IDisposable"/>: a script-reachable object holding a native key handle would tie an operating
/// system resource to a garbage-collection schedule, and <c>CryptoKey</c> has no <c>close</c> for a script to
/// call. The cost is one key import per operation, which is small beside the modular exponentiation that
/// follows it, and the DER is the canonical re-encoding the platform itself produced, so it is also exactly
/// what <c>exportKey</c> hands back.
/// </para>
/// <para>
/// <b>Three limits of the platform are visible</b>, each reported as an <c>OperationError</c> naming the
/// restriction rather than pretended away:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>RSA-PSS salt length.</b> <see cref="RSASignaturePadding.Pss"/> takes no salt-length parameter and .NET
/// fixes the salt at the hash's own output length, where <c>RsaPssParams.saltLength</c> is a caller-supplied
/// value. The member is read and honoured for the one length the platform can produce, and any other length
/// is refused — rather than silently signing with a salt the caller did not ask for, which would produce a
/// signature that verifies nowhere the caller expects.
/// </item>
/// <item>
/// <b>RSA-OAEP label.</b> <see cref="RSAEncryptionPadding"/> carries a hash and no label, so the only label
/// this engine can honour is the empty one. An absent or empty <c>label</c> works; a non-empty one is
/// refused, because encrypting with the empty label instead would produce a ciphertext the intended
/// recipient cannot decrypt.
/// </item>
/// <item>
/// <b>generateKey public exponent.</b> <see cref="RSA.Create(int)"/> takes a key size and nothing else; .NET
/// exposes no way to choose <c>e</c>, and every implementation it can reach uses 65537. Any other exponent is
/// refused.
/// </item>
/// </list>
/// <para>
/// A fourth restriction belongs to key <i>import</i> rather than to an operation: .NET's
/// <see cref="RSAParameters"/> describes a private key by its CRT form, so a JWK carrying <c>d</c> without
/// <c>p</c>, <c>q</c>, <c>dp</c>, <c>dq</c> and <c>qi</c> — which Section 6.3.2 of JSON Web Algorithms
/// permits — cannot be imported and is a <c>DataError</c>. Recovering the primes from <c>(n, e, d)</c> is
/// possible but is a probabilistic factoring routine, and writing one here would be this engine inventing
/// cryptography rather than calling it.
/// </para>
/// </remarks>
internal static class RsaAlgorithm
{
    /// <summary>The usages a signature key pair may be asked for — RSASSA-PKCS1-v1_5 and RSA-PSS.</summary>
    private const KeyUsage SignatureUsages = KeyUsage.Sign | KeyUsage.Verify;

    /// <summary>The usages an RSA-OAEP key pair may be asked for.</summary>
    private const KeyUsage CipherUsages = KeyUsage.Encrypt | KeyUsage.Decrypt | KeyUsage.WrapKey | KeyUsage.UnwrapKey;

    /// <summary>"The usage intersection of usages and [ 'verify' ]".</summary>
    private const KeyUsage PublicSignatureUsages = KeyUsage.Verify;

    /// <summary>"The usage intersection of usages and [ 'sign' ]".</summary>
    private const KeyUsage PrivateSignatureUsages = KeyUsage.Sign;

    /// <summary>"The usage intersection of usages and [ 'encrypt', 'wrapKey' ]".</summary>
    private const KeyUsage PublicCipherUsages = KeyUsage.Encrypt | KeyUsage.WrapKey;

    /// <summary>"The usage intersection of usages and [ 'decrypt', 'unwrapKey' ]".</summary>
    private const KeyUsage PrivateCipherUsages = KeyUsage.Decrypt | KeyUsage.UnwrapKey;

    /// <summary>
    /// The largest modulus this engine will <i>generate</i>, in bits. The algorithm has no ceiling of its
    /// own and the platform's goes to 16384, but RSA key generation is a prime search whose cost grows far
    /// faster than the modulus does — a 16384-bit key is minutes of CPU inside one synchronous operation,
    /// which no execution constraint can interrupt because it is a single BCL call. 8192 is comfortably
    /// above every key size in use and bounds that to seconds. It bounds nothing about <i>importing</i> a
    /// key, where the work is a parse.
    /// </summary>
    private const uint MaxGeneratedModulusLength = 8192;

    /// <summary>The four registered hashes, in the order a JWK <c>alg</c> table lists them.</summary>
    private static readonly string[] _hashes =
    [
        AlgorithmNormalization.Sha1,
        AlgorithmNormalization.Sha256,
        AlgorithmNormalization.Sha384,
        AlgorithmNormalization.Sha512,
    ];

    // -------------------------------------------------------------------------------------------------
    // generateKey
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// https://w3c.github.io/webcrypto/#rsassa-pkcs1-operations-generate-key,
    /// https://w3c.github.io/webcrypto/#rsa-pss-operations-generate-key and
    /// https://w3c.github.io/webcrypto/#rsa-oaep-operations-generate-key, which differ only in step 1's
    /// usage set and in the name the resulting dictionary carries.
    /// </summary>
    internal static AsymmetricKeyPairMaterial GenerateKey(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        KeyUsage usages,
        string what)
    {
        var isCipher = string.Equals(normalized.Name, AlgorithmNormalization.RsaOaep, StringComparison.Ordinal);

        // Step 1: "If usages contains an entry which is not …, then throw a SyntaxError."
        RequireUsagesWithin(context, usages, isCipher ? CipherUsages : SignatureUsages, normalized.Name, what);

        var modulusLength = normalized.ModulusLength!.Value;
        var publicExponent = normalized.PublicExponent!;

        // This engine's own ceiling comes first, ahead of step 2, so that the arithmetic step 2 performs on
        // 2^modulusLength is bounded by something before it is performed. Both failures are the same
        // OperationError, so nothing but the message can tell the order.
        if (modulusLength > MaxGeneratedModulusLength)
        {
            context.ThrowOperationError(
                what + ": a modulus length of " + modulusLength + " bits exceeds the " + MaxGeneratedModulusLength
                + " bits this engine will generate.");
        }

        // Step 2: "Perform the validate RSA key generation parameters algorithm with normalizedAlgorithm."
        ValidateKeyGenerationParameters(context, modulusLength, publicExponent, what);

        RequireSupportedPublicExponent(context, publicExponent, what);
        RequireGeneratableModulusLength(context, modulusLength, what);

        byte[] publicHandle;
        byte[] privateHandle;

        // Steps 3 and 4: generate the pair, and report a generation that fails as an OperationError. Every
        // way RSA.Create can refuse a size arrives as a CryptographicException, which must not escape a
        // promise-returning operation as a CLR exception.
        try
        {
            using var rsa = RSA.Create((int) modulusLength);
            publicHandle = rsa.ExportSubjectPublicKeyInfo();
            privateHandle = rsa.ExportPkcs8PrivateKey();
        }
        catch (CryptographicException)
        {
            context.ThrowOperationError(what + ": the RSA key pair could not be generated.");
            return default;
        }

        // Steps 5 to 9: one RsaHashedKeyAlgorithm, shared by both halves. The exponent is stored in the
        // minimal form the BigInteger definition asks values read from the API to have, rather than with
        // whatever leading zeros the caller's array carried.
        var algorithm = new CryptoKeyAlgorithm(
            normalized.Name,
            Length: 0,
            normalized.HashName,
            modulusLength,
            Minimal(publicExponent));

        return new AsymmetricKeyPairMaterial(
            publicHandle,
            usages & (isCipher ? PublicCipherUsages : PublicSignatureUsages),
            privateHandle,
            usages & (isCipher ? PrivateCipherUsages : PrivateSignatureUsages),
            algorithm);
    }

    /// <summary>
    /// "Validate RSA key generation parameters" —
    /// https://w3c.github.io/webcrypto/#rsa-key-generation-parameter-validation: "If modulusLength is less
    /// than 4, or if publicExponent is less than 3, is even, or is greater than or equal to
    /// 2^modulusLength - 1, then throw an OperationError."
    /// </summary>
    private static void ValidateKeyGenerationParameters(
        CryptoContext context,
        uint modulusLength,
        byte[] publicExponent,
        string what)
    {
        // "Let publicExponent be the result of converting the publicExponent member to a non-negative
        // integer" — the array is the big-endian magnitude, and an empty one is zero.
        var exponent = new BigInteger(publicExponent, isUnsigned: true, isBigEndian: true);

        if (modulusLength < 4)
        {
            context.ThrowOperationError(what + ": a modulus length of " + modulusLength + " bits is less than the 4 bits an RSA modulus must have.");
        }

        if (exponent < 3 || exponent.IsEven || IsAtLeastAllOnes(exponent, modulusLength))
        {
            context.ThrowOperationError(
                what + ": " + exponent + " is not a valid RSA public exponent — it must be an odd integer at least 3 and less than 2^"
                + modulusLength + " - 1.");
        }
    }

    /// <summary>
    /// Whether <paramref name="value"/> is at least <c>2^modulusLength - 1</c>, without materializing that
    /// number unless the bit lengths make it necessary. A value with more bits than the modulus is above it
    /// outright; one with fewer is below it; and one with exactly as many is at least <c>2^m - 1</c> only if
    /// every one of those bits is set, which is the one case worth building the comparand for.
    /// </summary>
    private static bool IsAtLeastAllOnes(BigInteger value, uint modulusLength)
    {
        var bits = value.GetBitLength();

        if (bits != modulusLength)
        {
            return bits > modulusLength;
        }

        return value >= (BigInteger.One << (int) modulusLength) - BigInteger.One;
    }

    /// <summary>
    /// The public exponent restriction. See the remarks on this class: .NET's RSA key generation takes a key
    /// size and nothing else.
    /// </summary>
    private static void RequireSupportedPublicExponent(CryptoContext context, byte[] publicExponent, string what)
    {
        var minimal = Minimal(publicExponent);

        // 65537 = 0x01 0x00 0x01, the F4 exponent every implementation .NET can reach generates with.
        if (minimal is not [0x01, 0x00, 0x01])
        {
            context.ThrowOperationError(
                what + ": this engine generates RSA keys with the public exponent 65537 only, and .NET's RSA key generation offers no way to choose another.");
        }
    }

    /// <summary>
    /// What the platform's own RSA implementation will generate, which is narrower than the algorithm's
    /// "any modulus length" and differs per operating system — CNG takes 512 to 16384 bits in steps of 64,
    /// OpenSSL 512 to 16384 in steps of 8. Asking rather than assuming is what keeps this an
    /// <c>OperationError</c> naming the restriction instead of a <see cref="CryptographicException"/>
    /// erupting out of a promise-returning operation.
    /// </summary>
    private static void RequireGeneratableModulusLength(CryptoContext context, uint modulusLength, string what)
    {
        using var probe = RSA.Create();
        var legal = probe.LegalKeySizes;

        foreach (var sizes in legal)
        {
            if (modulusLength < (uint) sizes.MinSize || modulusLength > (uint) sizes.MaxSize)
            {
                continue;
            }

            if (sizes.SkipSize == 0 ? modulusLength == (uint) sizes.MinSize : (modulusLength - sizes.MinSize) % sizes.SkipSize == 0)
            {
                return;
            }
        }

        context.ThrowOperationError(
            what + ": a modulus length of " + modulusLength + " bits is not one this platform's RSA implementation will generate ("
            + DescribeKeySizes(legal) + ").");
    }

    private static string DescribeKeySizes(KeySizes[] legal)
    {
        var parts = new List<string>(legal.Length);
        foreach (var sizes in legal)
        {
            parts.Add(sizes.SkipSize == 0
                ? sizes.MinSize + " bits"
                : sizes.MinSize + " to " + sizes.MaxSize + " bits in steps of " + sizes.SkipSize);
        }

        return string.Join(", ", parts);
    }

    // -------------------------------------------------------------------------------------------------
    // importKey
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// https://w3c.github.io/webcrypto/#rsassa-pkcs1-operations-import-key and its two siblings, which
    /// differ only in the usage set each format's first step checks, in the JWK <c>use</c> value and in the
    /// JWK <c>alg</c> table.
    /// </summary>
    /// <remarks>
    /// The usage check is the <i>first</i> step of each format's branch, before a single byte is parsed —
    /// so <c>importKey('spki', garbage, …, ['sign'])</c> is the <c>SyntaxError</c> the usages earn rather
    /// than the <c>DataError</c> the bytes would.
    /// </remarks>
    internal static (byte[] Handle, string KeyType, CryptoKeyAlgorithm Algorithm) ImportKey(
        CryptoContext context,
        KeyFormat format,
        byte[]? rawData,
        JsonWebKeyData? jwk,
        NormalizedAlgorithm normalized,
        bool extractable,
        KeyUsage usages,
        string what)
    {
        var isCipher = string.Equals(normalized.Name, AlgorithmNormalization.RsaOaep, StringComparison.Ordinal);

        switch (format)
        {
            case KeyFormat.Spki:
                RequireUsagesWithin(context, usages, isCipher ? PublicCipherUsages : PublicSignatureUsages, normalized.Name, what);
                return ImportSpki(context, rawData!, normalized, what);

            case KeyFormat.Pkcs8:
                RequireUsagesWithin(context, usages, isCipher ? PrivateCipherUsages : PrivateSignatureUsages, normalized.Name, what);
                return ImportPkcs8(context, rawData!, normalized, what);

            case KeyFormat.Jwk:
                return ImportJwk(context, jwk!, normalized, isCipher, extractable, usages, what);

            default:
                // "Otherwise: throw a NotSupportedError" — raw describes a symmetric key.
                context.ThrowNotSupportedError(
                    what + ": an " + normalized.Name + " key cannot be imported from the '" + KeyFormats.NameOf(format) + "' format.");
                return default;
        }
    }

    /// <summary>
    /// The <c>spki</c> branch: parse a <c>SubjectPublicKeyInfo</c>, refuse anything that is not one, and keep
    /// the platform's own canonical re-encoding as the handle.
    /// </summary>
    /// <remarks>
    /// <see cref="RSA.ImportSubjectPublicKeyInfo"/> is the whole of "parse a subjectPublicKeyInfo", the
    /// <c>rsaEncryption</c> object identifier check and "parse an ASN.1 structure … as the RSAPublicKey
    /// structure": it refuses a structure that is not one, and it refuses one whose algorithm identifier
    /// names anything but RSA. <c>exactData set to true</c> is the <c>bytesRead</c> comparison — trailing
    /// bytes after a well-formed structure are a <c>DataError</c> rather than something to ignore.
    /// </remarks>
    private static (byte[] Handle, string KeyType, CryptoKeyAlgorithm Algorithm) ImportSpki(
        CryptoContext context,
        byte[] data,
        NormalizedAlgorithm normalized,
        string what)
    {
        using var rsa = RSA.Create();

        try
        {
            rsa.ImportSubjectPublicKeyInfo(data, out var read);
            if (read != data.Length)
            {
                context.ThrowDataError(
                    what + ": the SubjectPublicKeyInfo is followed by " + (data.Length - read) + " trailing byte(s).");
            }
        }
        catch (CryptographicException)
        {
            context.ThrowDataError(what + ": the data is not a valid RSA SubjectPublicKeyInfo structure.");
        }

        return (Export(context, rsa, CryptoKeyTypes.Public, what), CryptoKeyTypes.Public, DescribeKey(context, rsa, normalized, what));
    }

    /// <summary>
    /// The <c>pkcs8</c> branch, whose shape is the <c>spki</c> one with <c>PrivateKeyInfo</c> in place of
    /// <c>SubjectPublicKeyInfo</c>.
    /// </summary>
    private static (byte[] Handle, string KeyType, CryptoKeyAlgorithm Algorithm) ImportPkcs8(
        CryptoContext context,
        byte[] data,
        NormalizedAlgorithm normalized,
        string what)
    {
        using var rsa = RSA.Create();

        try
        {
            rsa.ImportPkcs8PrivateKey(data, out var read);
            if (read != data.Length)
            {
                context.ThrowDataError(
                    what + ": the PrivateKeyInfo is followed by " + (data.Length - read) + " trailing byte(s).");
            }
        }
        catch (CryptographicException)
        {
            context.ThrowDataError(what + ": the data is not a valid RSA PrivateKeyInfo structure.");
        }

        return (Export(context, rsa, CryptoKeyTypes.Private, what), CryptoKeyTypes.Private, DescribeKey(context, rsa, normalized, what));
    }

    /// <summary>
    /// The <c>jwk</c> branch, whose steps run in the specification's own order: the usage check that depends
    /// on whether <c>d</c> is present, then <c>kty</c>, then <c>use</c>, <c>key_ops</c> and <c>ext</c>, then
    /// the <c>alg</c> to hash mapping and its agreement with the requested hash, and only then the key
    /// itself.
    /// </summary>
    private static (byte[] Handle, string KeyType, CryptoKeyAlgorithm Algorithm) ImportJwk(
        CryptoContext context,
        JsonWebKeyData jwk,
        NormalizedAlgorithm normalized,
        bool isCipher,
        bool extractable,
        KeyUsage usages,
        string what)
    {
        var isPrivate = jwk.D is not null;

        var allowed = (isPrivate, isCipher) switch
        {
            (true, true) => PrivateCipherUsages,
            (true, false) => PrivateSignatureUsages,
            (false, true) => PublicCipherUsages,
            (false, false) => PublicSignatureUsages,
        };

        RequireUsagesWithin(context, usages, allowed, normalized.Name, what);

        jwk.RequireKeyType(context, "RSA", what);
        jwk.ValidateUseKeyOpsAndExt(context, usages, extractable, isCipher ? "enc" : "sig", what);

        // "Let hash be a string whose initial value is undefined" and the alg table that follows it: an
        // absent alg leaves the hash undefined and asserts nothing, a recognized one must agree with the
        // hash the import asked for, and an unrecognized one is a DataError.
        if (jwk.Alg is { } alg)
        {
            if (!TryHashFromJwkAlgorithm(normalized.Name, alg, out var hashName))
            {
                context.ThrowDataError(
                    what + ": the alg field of the JSON Web Key is '" + alg + "', which names no hash this engine registers for " + normalized.Name + ".");
            }

            if (!string.Equals(hashName, normalized.HashName, StringComparison.Ordinal))
            {
                context.ThrowDataError(
                    what + ": the alg field of the JSON Web Key names " + hashName + " where the import asks for " + normalized.HashName + ".");
            }
        }

        var parameters = ReadJwkParameters(context, jwk, isPrivate, what);

        using var rsa = RSA.Create();

        try
        {
            rsa.ImportParameters(parameters);
        }
        catch (CryptographicException)
        {
            context.ThrowDataError(
                what + ": the JSON Web Key does not describe a valid RSA " + (isPrivate ? "private" : "public") + " key.");
        }

        var keyType = isPrivate ? CryptoKeyTypes.Private : CryptoKeyTypes.Public;
        return (Export(context, rsa, keyType, what), keyType, DescribeKey(context, rsa, normalized, what));
    }

    /// <summary>
    /// "Let privateKey represent the RSA private key identified by interpreting jwk according to Section
    /// 6.3.2 of JSON Web Algorithms", or the public key of Section 6.3.1 when <c>d</c> is absent.
    /// </summary>
    /// <remarks>
    /// The lengths are what makes this more than a base64url decode. JSON Web Algorithms encodes each value
    /// with "the minimum number of octets needed to represent" it, so <c>dp</c>, <c>dq</c> and <c>qi</c>
    /// legitimately arrive shorter than <c>p</c> and <c>q</c>, while <see cref="RSAParameters"/> wants
    /// <c>D</c> at the modulus' length and the five CRT values at exactly half of it. Left-padding with
    /// zeros is the conversion between the two, and it is value-preserving because both are big-endian
    /// magnitudes.
    /// </remarks>
    private static RSAParameters ReadJwkParameters(CryptoContext context, JsonWebKeyData jwk, bool isPrivate, string what)
    {
        var modulus = Minimal(JsonWebKeyData.RequireBase64UrlField(context, jwk.N, "n", what));
        var exponent = Minimal(JsonWebKeyData.RequireBase64UrlField(context, jwk.E, "e", what));

        if (modulus.Length == 0 || exponent.Length == 0)
        {
            context.ThrowDataError(what + ": the n and e fields of the JSON Web Key must describe non-zero values.");
        }

        if (!isPrivate)
        {
            return new RSAParameters { Modulus = modulus, Exponent = exponent };
        }

        // Section 6.3.2 permits a private key described by d alone, without the CRT parameters. See the
        // remarks on this class for why this engine cannot import one.
        if (jwk.P is null && jwk.Q is null && jwk.Dp is null && jwk.Dq is null && jwk.Qi is null)
        {
            context.ThrowDataError(
                what + ": the JSON Web Key describes a private key by d alone, and this engine can only import one that also carries the CRT parameters p, q, dp, dq and qi.");
        }

        var half = (modulus.Length + 1) / 2;

        return new RSAParameters
        {
            Modulus = modulus,
            Exponent = exponent,
            D = AlignTo(context, JsonWebKeyData.RequireBase64UrlField(context, jwk.D, "d", what), modulus.Length, "d", what),
            P = AlignTo(context, JsonWebKeyData.RequireBase64UrlField(context, jwk.P, "p", what), half, "p", what),
            Q = AlignTo(context, JsonWebKeyData.RequireBase64UrlField(context, jwk.Q, "q", what), half, "q", what),
            DP = AlignTo(context, JsonWebKeyData.RequireBase64UrlField(context, jwk.Dp, "dp", what), half, "dp", what),
            DQ = AlignTo(context, JsonWebKeyData.RequireBase64UrlField(context, jwk.Dq, "dq", what), half, "dq", what),
            InverseQ = AlignTo(context, JsonWebKeyData.RequireBase64UrlField(context, jwk.Qi, "qi", what), half, "qi", what),
        };
    }

    /// <summary>
    /// The big-endian magnitude <paramref name="value"/> as exactly <paramref name="length"/> bytes, which
    /// is the fixed-width form <see cref="RSAParameters"/> is described in. A value that does not fit even
    /// after its leading zeros are dropped is not a parameter of a key of this size.
    /// </summary>
    private static byte[] AlignTo(CryptoContext context, byte[] value, int length, string field, string what)
    {
        var minimal = Minimal(value);

        if (minimal.Length > length)
        {
            context.ThrowDataError(
                what + ": the " + field + " field of the JSON Web Key is " + (minimal.Length * 8)
                + " bits long, which is too long for a key with this modulus.");
        }

        if (minimal.Length == length)
        {
            return minimal;
        }

        var padded = new byte[length];
        minimal.CopyTo(padded.AsSpan(length - minimal.Length));
        return padded;
    }

    // -------------------------------------------------------------------------------------------------
    // exportKey
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// https://w3c.github.io/webcrypto/#rsassa-pkcs1-operations-export-key and its two siblings.
    /// </summary>
    internal static JsValue ExportKey(CryptoContext context, JsCryptoKey key, KeyFormat format, string what)
    {
        switch (format)
        {
            case KeyFormat.Spki:
                RequireKeyType(context, key, CryptoKeyTypes.Public, what);

                // The handle already is the DER encoding the steps describe — the platform's own, produced
                // when the key was made. It is copied on the way out: what a script is handed is mutable.
                return context.CreateArrayBuffer(key.Handle.ToArray());

            case KeyFormat.Pkcs8:
                RequireKeyType(context, key, CryptoKeyTypes.Private, what);
                return context.CreateArrayBuffer(key.Handle.ToArray());

            case KeyFormat.Jwk:
                return ExportJwk(context, key, what);

            default:
                context.ThrowNotSupportedError(
                    what + ": an " + key.Algorithm.Name + " key cannot be exported to the '" + KeyFormats.NameOf(format) + "' format.");
                return JsValue.Undefined;
        }
    }

    private static JsValue ExportJwk(CryptoContext context, JsCryptoKey key, string what)
    {
        var isPrivate = string.Equals(key.KeyType, CryptoKeyTypes.Private, StringComparison.Ordinal);
        var alg = JwkAlgorithm(key.Algorithm.Name, key.Algorithm.HashName!);

        using var rsa = CreateRsa(context, key, what);

        RSAParameters parameters;
        try
        {
            parameters = rsa.ExportParameters(includePrivateParameters: isPrivate);
        }
        catch (CryptographicException)
        {
            context.ThrowOperationError(what + ": the key material could not be accessed.");
            return JsValue.Undefined;
        }

        if (!isPrivate)
        {
            return JsonWebKeyData.CreateRsaPublicExport(
                context.Engine,
                alg,
                Minimal(parameters.Modulus),
                Minimal(parameters.Exponent),
                key.Usages,
                key.Extractable);
        }

        var fields = new RsaJwkPrivateFields(
            Minimal(parameters.Modulus),
            Minimal(parameters.Exponent),
            Minimal(parameters.D),
            Minimal(parameters.P),
            Minimal(parameters.Q),
            Minimal(parameters.DP),
            Minimal(parameters.DQ),
            Minimal(parameters.InverseQ));

        return JsonWebKeyData.CreateRsaPrivateExport(context.Engine, alg, in fields, key.Usages, key.Extractable);
    }

    // -------------------------------------------------------------------------------------------------
    // sign, verify, encrypt and decrypt
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// https://w3c.github.io/webcrypto/#rsassa-pkcs1-operations-sign — "the signature generation operation
    /// defined in Section 8.2 of [RFC3447]" — and
    /// https://w3c.github.io/webcrypto/#rsa-pss-operations-sign, which is Section 8.1 with MGF1 and the
    /// caller's salt length.
    /// </summary>
    internal static byte[] Sign(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        JsCryptoKey key,
        ReadOnlySpan<byte> message,
        string what)
    {
        // Step 1: "If the [[type]] internal slot of key is not 'private', then throw an InvalidAccessError."
        RequireKeyType(context, key, CryptoKeyTypes.Private, what);

        var padding = SignaturePadding(context, normalized, key, what);

        using var rsa = CreateRsa(context, key, what);

        try
        {
            return rsa.SignData(message.ToArray(), HashName(key.Algorithm.HashName!), padding);
        }
        catch (CryptographicException)
        {
            // Step 3: "If performing the operation results in an error, then throw an OperationError." A
            // modulus too short to carry the encoded hash is the way this is actually reached.
            context.ThrowOperationError(what + ": the signature could not be produced.");
            return null!;
        }
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#rsassa-pkcs1-operations-verify and
    /// https://w3c.github.io/webcrypto/#rsa-pss-operations-verify: "Let result be a boolean with value true
    /// if the result of the operation was 'valid signature' and the value false otherwise."
    /// </summary>
    /// <remarks>
    /// There is no error step here, which is why the platform's refusal to look at a malformed signature —
    /// one of the wrong length, say — is <see langword="false"/> rather than an <c>OperationError</c>:
    /// "otherwise" covers every way a signature can fail to be the valid one.
    /// </remarks>
    internal static bool Verify(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        JsCryptoKey key,
        ReadOnlySpan<byte> signature,
        ReadOnlySpan<byte> message,
        string what)
    {
        // Step 1: "If the [[type]] internal slot of key is not 'public', then throw an InvalidAccessError."
        RequireKeyType(context, key, CryptoKeyTypes.Public, what);

        var padding = SignaturePadding(context, normalized, key, what);

        using var rsa = CreateRsa(context, key, what);

        try
        {
            return rsa.VerifyData(message, signature, HashName(key.Algorithm.HashName!), padding);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#rsa-oaep-operations-encrypt — "the encryption operation defined in
    /// Section 7.1 of [RFC3447]" with MGF1 and the key's own hash.
    /// </summary>
    internal static byte[] Encrypt(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        JsCryptoKey key,
        ReadOnlySpan<byte> plaintext,
        string what)
    {
        // Step 1: "If the [[type]] internal slot of key is not 'public', then throw an InvalidAccessError."
        RequireKeyType(context, key, CryptoKeyTypes.Public, what);

        // Step 2: "Let label be the label member … or the empty byte sequence if … not present."
        RequireEmptyLabel(context, normalized, what);

        using var rsa = CreateRsa(context, key, what);

        try
        {
            return rsa.Encrypt(plaintext.ToArray(), EncryptionPadding(key.Algorithm.HashName!));
        }
        catch (CryptographicException)
        {
            // Step 4: "If performing the operation results in an error, then throw an OperationError." A
            // message longer than the modulus can carry with OAEP padding is how this is reached.
            context.ThrowOperationError(what + ": the data could not be encrypted.");
            return null!;
        }
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#rsa-oaep-operations-decrypt.
    /// </summary>
    internal static byte[] Decrypt(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        JsCryptoKey key,
        ReadOnlySpan<byte> ciphertext,
        string what)
    {
        // Step 1: "If the [[type]] internal slot of key is not 'private', then throw an InvalidAccessError."
        RequireKeyType(context, key, CryptoKeyTypes.Private, what);

        RequireEmptyLabel(context, normalized, what);

        using var rsa = CreateRsa(context, key, what);

        try
        {
            return rsa.Decrypt(ciphertext.ToArray(), EncryptionPadding(key.Algorithm.HashName!));
        }
        catch (CryptographicException)
        {
            // Step 4 again. One message for every way the decryption can fail, carrying nothing about which
            // part of the input was wrong: OAEP's padding check is what a chosen-ciphertext attack probes,
            // and a distinguishable failure is the whole of that attack.
            context.ThrowOperationError(what + ": the data could not be decrypted.");
            return null!;
        }
    }

    // -------------------------------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// Rehydrates the <see cref="RSA"/> a key's <c>[[handle]]</c> describes. See the remarks on this class
    /// for why the handle is DER and this happens per operation.
    /// </summary>
    private static RSA CreateRsa(CryptoContext context, JsCryptoKey key, string what)
    {
        var rsa = RSA.Create();

        try
        {
            if (string.Equals(key.KeyType, CryptoKeyTypes.Private, StringComparison.Ordinal))
            {
                rsa.ImportPkcs8PrivateKey(key.Handle, out _);
            }
            else
            {
                rsa.ImportSubjectPublicKeyInfo(key.Handle, out _);
            }
        }
        catch (CryptographicException)
        {
            rsa.Dispose();

            // "If the underlying cryptographic key material represented by the [[handle]] internal slot of
            // key cannot be accessed, then throw an OperationError." Unreachable in practice — the bytes are
            // the platform's own canonical encoding of a key it built — but a CryptographicException that
            // escaped here would erupt as a CLR exception out of a promise-returning operation.
            context.ThrowOperationError(what + ": the key material could not be accessed.");
        }

        return rsa;
    }

    /// <summary>
    /// The canonical DER a key's <c>[[handle]]</c> holds, taken from a freshly imported
    /// <see cref="RSA"/> — which is also exactly what <c>exportKey</c>'s <c>spki</c> and <c>pkcs8</c>
    /// branches describe, so the two can never disagree.
    /// </summary>
    private static byte[] Export(CryptoContext context, RSA rsa, string keyType, string what)
    {
        try
        {
            return string.Equals(keyType, CryptoKeyTypes.Private, StringComparison.Ordinal)
                ? rsa.ExportPkcs8PrivateKey()
                : rsa.ExportSubjectPublicKeyInfo();
        }
        catch (CryptographicException)
        {
            context.ThrowDataError(what + ": the key could not be read back after being imported.");
            return null!;
        }
    }

    /// <summary>
    /// The <c>RsaHashedKeyAlgorithm</c> an import ends in: the name and hash the caller asked for, and the
    /// modulus length and public exponent read off the key that arrived.
    /// </summary>
    private static CryptoKeyAlgorithm DescribeKey(CryptoContext context, RSA rsa, NormalizedAlgorithm normalized, string what)
    {
        RSAParameters parameters;
        try
        {
            parameters = rsa.ExportParameters(includePrivateParameters: false);
        }
        catch (CryptographicException)
        {
            context.ThrowDataError(what + ": the modulus and public exponent could not be read from the key.");
            return default;
        }

        return new CryptoKeyAlgorithm(
            normalized.Name,
            Length: 0,
            normalized.HashName,
            (uint) rsa.KeySize,
            Minimal(parameters.Exponent));
    }

    /// <summary>
    /// The padding a signature operation uses, and the point at which RSA-PSS's salt length is checked
    /// against what the platform can produce. See the remarks on this class.
    /// </summary>
    private static RSASignaturePadding SignaturePadding(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        JsCryptoKey key,
        string what)
    {
        if (!string.Equals(normalized.Name, AlgorithmNormalization.RsaPss, StringComparison.Ordinal))
        {
            return RSASignaturePadding.Pkcs1;
        }

        var requested = normalized.SaltLength!.Value;
        var supported = HashLengthInBytes(key.Algorithm.HashName!);

        if (requested != supported)
        {
            context.ThrowOperationError(
                what + ": a salt length of " + requested + " bytes was requested, and .NET's RSA-PSS implementation always uses the hash's own length, which for "
                + key.Algorithm.HashName + " is " + supported + " bytes.");
        }

        return RSASignaturePadding.Pss;
    }

    /// <summary>
    /// Step 2 of RSA-OAEP's encrypt and decrypt, as far as this engine can honour it. See the remarks on
    /// this class.
    /// </summary>
    private static void RequireEmptyLabel(CryptoContext context, NormalizedAlgorithm normalized, string what)
    {
        if (normalized.Label is { Length: > 0 } label)
        {
            context.ThrowOperationError(
                what + ": a label of " + label.Length + " byte(s) was given, and .NET's RSA-OAEP implementation accepts only the empty label.");
        }
    }

    /// <summary>
    /// "If usages contains an entry which is not …, then throw a SyntaxError" — the first step of every
    /// generate and import branch, differing only in the set it names.
    /// </summary>
    private static void RequireUsagesWithin(CryptoContext context, KeyUsage usages, KeyUsage allowed, string algorithmName, string what)
    {
        if ((usages & ~allowed) != KeyUsage.None)
        {
            context.ThrowSyntaxError(
                what + ": this " + algorithmName + " key supports the usages " + KeyUsages.Describe(allowed)
                + ", not " + KeyUsages.Describe(usages & ~allowed) + ".");
        }
    }

    /// <summary>
    /// "If the [[type]] internal slot of key is not <c>expected</c>, then throw an InvalidAccessError" — the
    /// first step of every RSA operation, and the one that makes a public key refuse to sign.
    /// </summary>
    private static void RequireKeyType(CryptoContext context, JsCryptoKey key, string expected, string what)
    {
        if (!string.Equals(key.KeyType, expected, StringComparison.Ordinal))
        {
            context.ThrowInvalidAccessError(
                what + ": this operation needs a " + expected + " key, and the key given is a " + key.KeyType + " key.");
        }
    }

    /// <summary>
    /// The JWK <c>alg</c> field naming an RSA key with a given inner hash — the tables of
    /// https://www.rfc-editor.org/rfc/rfc7518#section-3.1 (RS*, PS*) and
    /// https://www.rfc-editor.org/rfc/rfc7518#section-4.3 (RSA-OAEP*), plus <c>RS1</c> and <c>PS1</c>, which
    /// the Web Cryptography API names for SHA-1.
    /// </summary>
    internal static string JwkAlgorithm(string algorithmName, string hashName)
    {
        switch (algorithmName)
        {
            case AlgorithmNormalization.RsassaPkcs1V15:
                return "RS" + JwkHashSuffix(hashName);
            case AlgorithmNormalization.RsaPss:
                return "PS" + JwkHashSuffix(hashName);
            case AlgorithmNormalization.RsaOaep:
                return string.Equals(hashName, AlgorithmNormalization.Sha1, StringComparison.Ordinal)
                    ? "RSA-OAEP"
                    : "RSA-OAEP-" + JwkHashSuffix(hashName);
            default:
                // Unreachable: the name came from the registry, and only the three RSA algorithms reach here.
                Throw.InvalidOperationException("Unhandled RSA algorithm '" + algorithmName + "'.");
                return null!;
        }
    }

    /// <summary>
    /// The number a JWK <c>alg</c> ends in, which is the hash's output length in bits for every hash but
    /// SHA-1 — spelled <c>1</c> rather than <c>160</c>.
    /// </summary>
    private static string JwkHashSuffix(string hashName)
    {
        switch (hashName)
        {
            case AlgorithmNormalization.Sha1:
                return "1";
            case AlgorithmNormalization.Sha256:
                return "256";
            case AlgorithmNormalization.Sha384:
                return "384";
            case AlgorithmNormalization.Sha512:
                return "512";
            default:
                // Unreachable: the hash was matched against the digest registry when the key was made. It is
                // spelled out rather than folded into a last case so that a hash registered later cannot
                // silently be labelled with another one's name.
                Throw.InvalidOperationException("Unhandled RSA hash algorithm '" + hashName + "'.");
                return null!;
        }
    }

    /// <summary>
    /// The inverse of <see cref="JwkAlgorithm"/>: the hash a JWK's <c>alg</c> names, or
    /// <see langword="false"/> for an <c>alg</c> that names none of this engine's — which the caller reports
    /// as the <c>DataError</c> the specification's "Otherwise: … throw a DataError" arm gives it.
    /// </summary>
    private static bool TryHashFromJwkAlgorithm(string algorithmName, string alg, out string hashName)
    {
        foreach (var candidate in _hashes)
        {
            if (string.Equals(alg, JwkAlgorithm(algorithmName, candidate), StringComparison.Ordinal))
            {
                hashName = candidate;
                return true;
            }
        }

        hashName = null!;
        return false;
    }

    private static HashAlgorithmName HashName(string hashName)
    {
        switch (hashName)
        {
            case AlgorithmNormalization.Sha1:
                // SHA-1 is one of the four hashes the specification registers for these algorithms, every
                // browser offers them over it, and a script asking for it has already chosen; nothing in the
                // engine picks it for anybody.
                return HashAlgorithmName.SHA1;
            case AlgorithmNormalization.Sha256:
                return HashAlgorithmName.SHA256;
            case AlgorithmNormalization.Sha384:
                return HashAlgorithmName.SHA384;
            case AlgorithmNormalization.Sha512:
                return HashAlgorithmName.SHA512;
            default:
                Throw.InvalidOperationException("Unhandled RSA hash algorithm '" + hashName + "'.");
                return default;
        }
    }

    private static RSAEncryptionPadding EncryptionPadding(string hashName)
    {
        switch (hashName)
        {
            case AlgorithmNormalization.Sha1:
                return RSAEncryptionPadding.OaepSHA1;
            case AlgorithmNormalization.Sha256:
                return RSAEncryptionPadding.OaepSHA256;
            case AlgorithmNormalization.Sha384:
                return RSAEncryptionPadding.OaepSHA384;
            case AlgorithmNormalization.Sha512:
                return RSAEncryptionPadding.OaepSHA512;
            default:
                Throw.InvalidOperationException("Unhandled RSA hash algorithm '" + hashName + "'.");
                return null!;
        }
    }

    /// <summary>The output length of each hash function, in bytes — [FIPS-180-4].</summary>
    private static uint HashLengthInBytes(string hashName)
    {
        switch (hashName)
        {
            case AlgorithmNormalization.Sha1:
                return 20;
            case AlgorithmNormalization.Sha256:
                return 32;
            case AlgorithmNormalization.Sha384:
                return 48;
            case AlgorithmNormalization.Sha512:
                return 64;
            default:
                Throw.InvalidOperationException("Unhandled RSA hash algorithm '" + hashName + "'.");
                return 0;
        }
    }

    /// <summary>
    /// A big-endian magnitude with its leading zero bytes dropped — "minimal typed array length … except the
    /// value 0 which shall have length 8 bits", https://w3c.github.io/webcrypto/#big-integer. It is also
    /// what JSON Web Algorithms asks of every value it encodes: "the minimum number of octets needed to
    /// represent the value".
    /// </summary>
    private static byte[] Minimal(byte[]? value)
    {
        if (value is null || value.Length == 0)
        {
            return [];
        }

        var start = 0;
        while (start < value.Length - 1 && value[start] == 0)
        {
            start++;
        }

        return start == 0 ? value : value[start..];
    }
}
#endif
