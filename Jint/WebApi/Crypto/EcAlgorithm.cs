#if NET8_0_OR_GREATER
using System.Formats.Asn1;
using System.Security.Cryptography;
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The two elliptic-curve algorithms — ECDSA (https://w3c.github.io/webcrypto/#ecdsa) and ECDH
/// (https://w3c.github.io/webcrypto/#ecdh) — over the three curves this specification names, <c>P-256</c>,
/// <c>P-384</c> and <c>P-521</c>. They share every key operation and differ only in the usage sets, in the
/// JSON Web Key <c>use</c> value and <c>alg</c> table, and in what they can then be asked to <i>do</i>:
/// ECDSA signs and verifies, and ECDH derives.
/// </summary>
/// <remarks>
/// <para>
/// <b>The key's <c>[[handle]]</c> is DER, never a live key object</b>, exactly as it is for the RSA family
/// and for the reason given there: a public key holds the bytes of a <c>SubjectPublicKeyInfo</c> and a
/// private key those of a <c>PrivateKeyInfo</c>, and each operation rehydrates a key inside one <c>using</c>
/// so that a script-reachable <c>CryptoKey</c> never owns a native key handle. Which class is rehydrated
/// follows the algorithm the key was made for — <see cref="ECDsa"/> for an ECDSA key and
/// <see cref="ECDiffieHellman"/> for an ECDH one. The DER is identical either way (both are
/// <c>id-ecPublicKey</c> with a named curve, and this was verified against the platform), and the split is
/// what lets <c>deriveBits</c> below cast straight to the <see cref="ECDiffieHellman"/> the ECDH primitive
/// needs.
/// </para>
/// <para>
/// <b>A signature is <c>r || s</c> at fixed field width.</b> "Convert r to a byte sequence of length n and
/// append it to result. Convert s to a byte sequence of length n and append it to result", where <c>n</c> is
/// the field size in octets — 32, 48 and 66 for the three curves, so a signature is 64, 96 or 132 bytes.
/// That is <see cref="DSASignatureFormat.IeeeP1363FixedFieldConcatenation"/>, which is also what
/// <see cref="ECDsa.SignData(byte[], HashAlgorithmName)"/> produces by default; the overload naming the
/// format is used anyway, so the one thing that would silently make every signature unreadable by every
/// other implementation cannot be changed by a default moving underneath this code.
/// </para>
/// <para>
/// <b>The curve a DER structure names is read out of the DER, not off the imported key.</b> The platform
/// will happily import a P-384 <c>SubjectPublicKeyInfo</c> into a key an import asked for P-256, and what it
/// reports afterwards through <c>ECParameters.Curve.Oid</c> differs between operating systems — Windows
/// fills in the friendly name, other platforms the dotted value. So the algorithm identifier is parsed with
/// <see cref="AsnReader"/>, which is what the specification's own steps describe ("If params is equivalent
/// to the secp256r1 object identifier … set namedCurve 'P-256'"), gives the same answer everywhere, and
/// makes the trailing-bytes check the same <c>exactData</c> the RSA formats get.
/// </para>
/// <para>
/// <b>A bad point does not arrive as a <see cref="CryptographicException"/> on Windows.</b> Importing
/// parameters whose <c>Q</c> is not on the named curve raises <see cref="PlatformNotSupportedException"/>
/// there ("The specified curve 'nistP256' or its parameters are not valid for this platform"), where the
/// same input on a platform backed by OpenSSL raises <see cref="CryptographicException"/>. Both are caught,
/// at every site, by <see cref="IsCryptographicFailure"/>: one escaping would be a CLR exception erupting
/// out of a promise-returning operation, which is the one thing this API must never do. The message is
/// misleading rather than informative — every one of the three curves is supported on every platform Jint
/// ships a net8 target for — so it is not repeated to script; the <c>DataError</c> says what was actually
/// wrong.
/// </para>
/// <para>
/// <b>Elliptic-curve JSON Web Key fields are fixed-width</b>, unlike every RSA one. Section 6.2.1.2 of JSON
/// Web Algorithms says of <c>x</c> that "The length of this octet string MUST be the full size of a
/// coordinate for the curve specified in the 'crv' parameter", and 6.2.2.1 says the same of <c>d</c>. So a
/// value with a leading zero byte keeps it, on the way in and on the way out, and a value of the wrong
/// length is a <c>DataError</c> rather than something to left-pad — which is exactly what the RSA branch
/// does with its minimal-length integers, and the difference is deliberate on both sides.
/// </para>
/// <para>
/// <b>ECDH is the only one of the two that derives.</b> Its <c>deriveBits</c> operation is below; ECDSA has
/// none, which is why <c>deriveBits({ name: 'ECDSA', … }, …)</c> is a <c>NotSupportedError</c> from the
/// registry rather than anything this class decides. An ECDH key still carries no <c>sign</c> or
/// <c>verify</c> usage and an ECDSA key no <c>deriveKey</c> or <c>deriveBits</c> one, so the split the
/// generate and import steps make is what keeps the two apart from the first line of script.
/// </para>
/// </remarks>
internal static class EcAlgorithm
{
    /// <summary>The usages an ECDSA key pair may be asked for.</summary>
    private const KeyUsage SignatureUsages = KeyUsage.Sign | KeyUsage.Verify;

    /// <summary>The usages an ECDH key pair may be asked for.</summary>
    private const KeyUsage DerivationUsages = KeyUsage.DeriveKey | KeyUsage.DeriveBits;

    /// <summary>"The usage intersection of usages and [ 'verify' ]".</summary>
    private const KeyUsage PublicSignatureUsages = KeyUsage.Verify;

    /// <summary>"The usage intersection of usages and [ 'sign' ]".</summary>
    private const KeyUsage PrivateSignatureUsages = KeyUsage.Sign;

    /// <summary>
    /// "Set the [[usages]] internal slot of publicKey to be the empty list" — an ECDH public key carries no
    /// usages at all, which is not an intersection but a flat rule, and is why importing one with any usage
    /// is a <c>SyntaxError</c>. A public key is one half of an agreement; the deriving is the private half's.
    /// </summary>
    private const KeyUsage PublicDerivationUsages = KeyUsage.None;

    /// <summary>"The usage intersection of usages and [ 'deriveKey', 'deriveBits' ]".</summary>
    private const KeyUsage PrivateDerivationUsages = KeyUsage.DeriveKey | KeyUsage.DeriveBits;

    /// <summary>
    /// <c>id-ecPublicKey</c>, https://www.rfc-editor.org/rfc/rfc5480#section-2.1.1 — the one algorithm
    /// identifier both DER formats may carry here. It names an elliptic-curve key and not what the key is
    /// for, which is why one <c>SubjectPublicKeyInfo</c> can be imported as ECDSA or as ECDH.
    /// </summary>
    private const string IdEcPublicKey = "1.2.840.10045.2.1";

    /// <summary>The curve object identifiers of https://www.rfc-editor.org/rfc/rfc5480#section-2.1.1.1.</summary>
    private const string Secp256r1 = "1.2.840.10045.3.1.7";
    private const string Secp384r1 = "1.3.132.0.34";
    private const string Secp521r1 = "1.3.132.0.35";

    /// <summary>The uncompressed point format's leading byte, [SEC1] 2.3.3.</summary>
    private const byte UncompressedPointMarker = 0x04;

    // -------------------------------------------------------------------------------------------------
    // generateKey
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// https://w3c.github.io/webcrypto/#ecdsa-operations-generate-key and
    /// https://w3c.github.io/webcrypto/#ecdh-operations-generate-key, which differ only in step 1's usage
    /// set, in the name the resulting dictionary carries, and in what the public half's usages are.
    /// </summary>
    internal static AsymmetricKeyPairMaterial GenerateKey(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        KeyUsage usages,
        string what)
    {
        var isEcdh = IsEcdh(normalized.Name);

        // Step 1: "If usages contains an entry which is not one of 'sign' or 'verify', then throw a
        // SyntaxError" — and, for ECDH, "'deriveKey' or 'deriveBits'".
        RequireUsagesWithin(context, usages, isEcdh ? DerivationUsages : SignatureUsages, normalized.Name, what);

        // Step 2's "Otherwise: throw a NotSupportedError", which is the whole of what an unrecognized curve
        // name earns — the argument conversion let it through because NamedCurve is a DOMString.
        var namedCurve = RequireSupportedCurve(context, normalized.NamedCurve, normalized.Name, what);

        byte[] publicHandle;
        byte[] privateHandle;

        // Steps 2 and 3: generate the pair, and report a generation that fails as an OperationError. The
        // curve is one of three the platform has always had, so this is unreachable in practice; a failure
        // escaping as a CLR exception out of a promise-returning operation would not be.
        try
        {
            using var algorithm = CreateAlgorithm(normalized.Name, NamedCurveOf(namedCurve));
            publicHandle = algorithm.ExportSubjectPublicKeyInfo();
            privateHandle = algorithm.ExportPkcs8PrivateKey();
        }
        catch (Exception e) when (IsCryptographicFailure(e))
        {
            context.ThrowOperationError(
                what + ": an " + normalized.Name + " key pair could not be generated on the curve " + namedCurve + ".");
            return default;
        }

        // Steps 4 to 8: one EcKeyAlgorithm, shared by both halves.
        var algorithmDictionary = new CryptoKeyAlgorithm(
            normalized.Name,
            Length: 0,
            HashName: null,
            NamedCurve: namedCurve);

        return new AsymmetricKeyPairMaterial(
            publicHandle,
            usages & (isEcdh ? PublicDerivationUsages : PublicSignatureUsages),
            privateHandle,
            usages & (isEcdh ? PrivateDerivationUsages : PrivateSignatureUsages),
            algorithmDictionary);
    }

    // -------------------------------------------------------------------------------------------------
    // importKey
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// https://w3c.github.io/webcrypto/#ecdsa-operations-import-key and
    /// https://w3c.github.io/webcrypto/#ecdh-operations-import-key, whose four format branches this follows
    /// step for step.
    /// </summary>
    /// <remarks>
    /// The curve check is step 1 of both, <i>before</i> the format is even looked at, so an unrecognized
    /// curve is a <c>NotSupportedError</c> whatever else is wrong with the request. Each format branch then
    /// opens with its own usage check, before a single byte is parsed, so
    /// <c>importKey('spki', garbage, …, ['sign'])</c> is the <c>SyntaxError</c> the usages earn rather than
    /// the <c>DataError</c> the bytes would.
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
        var isEcdh = IsEcdh(normalized.Name);

        // Step 1: "If the namedCurve member of normalizedAlgorithm is not one of 'P-256', 'P-384' or
        // 'P-521' … then throw a NotSupportedError."
        var namedCurve = RequireSupportedCurve(context, normalized.NamedCurve, normalized.Name, what);

        switch (format)
        {
            case KeyFormat.Spki:
                // "If usages contains an entry which is not 'verify' then throw a SyntaxError" for ECDSA;
                // "If usages is not empty then throw a SyntaxError" for ECDH, which is the same statement
                // written against an empty permitted set.
                RequireUsagesWithin(context, usages, isEcdh ? PublicDerivationUsages : PublicSignatureUsages, normalized.Name, what);
                return ImportDer(context, rawData!, normalized.Name, namedCurve, CryptoKeyTypes.Public, what);

            case KeyFormat.Pkcs8:
                RequireUsagesWithin(context, usages, isEcdh ? PrivateDerivationUsages : PrivateSignatureUsages, normalized.Name, what);
                return ImportDer(context, rawData!, normalized.Name, namedCurve, CryptoKeyTypes.Private, what);

            case KeyFormat.Raw:
                // The raw branch's own first step — "If the namedCurve member of normalizedAlgorithm is not
                // a named curve, then throw a DataError" — is unreachable, because step 1 above already
                // refused every name that is not one, and with the NotSupportedError the steps there give it.
                RequireUsagesWithin(context, usages, isEcdh ? PublicDerivationUsages : PublicSignatureUsages, normalized.Name, what);
                return ImportRaw(context, rawData!, normalized.Name, namedCurve, what);

            case KeyFormat.Jwk:
                return ImportJwk(context, jwk!, normalized.Name, namedCurve, isEcdh, extractable, usages, what);

            default:
                // "Otherwise: throw a NotSupportedError" — unreachable, every KeyFormat is above.
                context.ThrowNotSupportedError(
                    what + ": an " + normalized.Name + " key cannot be imported from the '" + KeyFormats.NameOf(format) + "' format.");
                return default;
        }
    }

    /// <summary>
    /// The <c>spki</c> and <c>pkcs8</c> branches, which are the same steps over two structures: read the
    /// curve the DER names, check it against the one the import asked for, and let the platform parse the
    /// rest. What is kept as the handle is the platform's own canonical re-encoding, which is also exactly
    /// what <c>exportKey</c> hands back.
    /// </summary>
    /// <remarks>
    /// "The rest" includes two checks the <c>pkcs8</c> steps spell out and this code does not repeat: that
    /// the optional <c>parameters</c> field <i>inside</i> the <c>ECPrivateKey</c> names the same curve as the
    /// outer algorithm identifier, and that its <c>publicKey</c> field is the point the private key value
    /// produces. Both were measured, and both are refused by the platform's parser — as a
    /// <see cref="CryptographicException"/>, so both become the <c>DataError</c> below. The re-encoding is
    /// where that field goes: .NET's own encoder omits it, so a structure that carries it comes back out
    /// without it, which is a different encoding of the same key rather than a lossy one.
    /// </remarks>
    private static (byte[] Handle, string KeyType, CryptoKeyAlgorithm Algorithm) ImportDer(
        CryptoContext context,
        byte[] data,
        string algorithmName,
        string namedCurve,
        string keyType,
        string what)
    {
        var isPrivate = string.Equals(keyType, CryptoKeyTypes.Private, StringComparison.Ordinal);
        var structure = isPrivate ? "PrivateKeyInfo" : "SubjectPublicKeyInfo";

        var derCurve = ReadNamedCurveFromDer(context, data, isPrivate, structure, what);

        // "If namedCurve is defined, and not equal to the namedCurve member of normalizedAlgorithm, throw a
        // DataError." The platform would import the key regardless, so this is the only thing standing
        // between a script and a P-384 key that calls itself a P-256 one.
        if (!string.Equals(derCurve, namedCurve, StringComparison.Ordinal))
        {
            context.ThrowDataError(
                what + ": the " + structure + " names the curve " + derCurve + " where the import asks for " + namedCurve + ".");
        }

        using var algorithm = CreateAlgorithm(algorithmName);

        try
        {
            if (isPrivate)
            {
                algorithm.ImportPkcs8PrivateKey(data, out _);
            }
            else
            {
                algorithm.ImportSubjectPublicKeyInfo(data, out _);
            }

            return (
                isPrivate ? algorithm.ExportPkcs8PrivateKey() : algorithm.ExportSubjectPublicKeyInfo(),
                keyType,
                Describe(algorithmName, namedCurve));
        }
        catch (Exception e) when (IsCryptographicFailure(e))
        {
            // "If a decode error occurs or an identity point is found, throw a DataError", and "If the
            // public key value is not a valid point on the Elliptic Curve … throw a DataError".
            context.ThrowDataError(
                what + ": the " + structure + " does not describe a valid " + namedCurve + " key.");
            return default;
        }
    }

    /// <summary>
    /// The <c>raw</c> branch: "Let Q be the elliptic curve point … identified by performing the conversion
    /// steps defined in Section 2.3.4 of [SEC1] on keyData", which for the uncompressed format is the single
    /// byte <c>0x04</c> followed by the two coordinates at the curve's field width.
    /// </summary>
    /// <remarks>
    /// "The uncompressed point format MUST be supported. If the implementation does not support the
    /// compressed point format and a compressed point is provided, throw a DataError" — this engine does not,
    /// so a <c>0x02</c> or <c>0x03</c> point is refused by the very step that anticipates it, and the message
    /// says which format was given rather than calling the bytes malformed. The point at infinity, whose
    /// encoding is the single byte <c>0x00</c>, is "an identity point" and is refused with it.
    /// </remarks>
    private static (byte[] Handle, string KeyType, CryptoKeyAlgorithm Algorithm) ImportRaw(
        CryptoContext context,
        byte[] data,
        string algorithmName,
        string namedCurve,
        string what)
    {
        var fieldSize = FieldSizeInBytes(namedCurve);
        var expected = 1 + (2 * fieldSize);

        if (data.Length > 0 && (data[0] == 0x02 || data[0] == 0x03))
        {
            context.ThrowDataError(
                what + ": the key data is a compressed elliptic-curve point, which this engine does not support — only the uncompressed format, which begins with 0x04.");
        }

        if (data.Length != expected || data[0] != UncompressedPointMarker)
        {
            context.ThrowDataError(
                what + ": an uncompressed " + namedCurve + " point is 0x04 followed by " + (2 * fieldSize)
                + " bytes, and the key data is " + data.Length + " byte(s) long.");
        }

        var parameters = new ECParameters
        {
            Curve = NamedCurveOf(namedCurve),
            Q = new ECPoint
            {
                X = data[1..(1 + fieldSize)],
                Y = data[(1 + fieldSize)..],
            },
        };

        return (
            ImportParameters(context, parameters, algorithmName, isPrivate: false, namedCurve, what),
            CryptoKeyTypes.Public,
            Describe(algorithmName, namedCurve));
    }

    /// <summary>
    /// The <c>jwk</c> branch, whose steps run in the specification's own order: the usage check that depends
    /// on whether <c>d</c> is present, then <c>kty</c>, then <c>use</c>, <c>key_ops</c> and <c>ext</c>, then
    /// <c>crv</c> and — for ECDSA alone — the <c>alg</c> table, and only then the key itself.
    /// </summary>
    private static (byte[] Handle, string KeyType, CryptoKeyAlgorithm Algorithm) ImportJwk(
        CryptoContext context,
        JsonWebKeyData jwk,
        string algorithmName,
        string namedCurve,
        bool isEcdh,
        bool extractable,
        KeyUsage usages,
        string what)
    {
        var isPrivate = jwk.D is not null;

        var allowed = (isPrivate, isEcdh) switch
        {
            (true, true) => PrivateDerivationUsages,
            (true, false) => PrivateSignatureUsages,
            (false, true) => PublicDerivationUsages,
            (false, false) => PublicSignatureUsages,
        };

        RequireUsagesWithin(context, usages, allowed, algorithmName, what);

        jwk.RequireKeyType(context, "EC", what);

        // ECDSA is a signing key and ECDH an encryption one, which is the only difference these three steps
        // have between the two algorithms.
        jwk.ValidateUseKeyOpsAndExt(context, usages, extractable, isEcdh ? "enc" : "sig", what);

        // "Let namedCurve be a string whose value is equal to the crv field of jwk. If namedCurve is not
        // equal to the namedCurve member of normalizedAlgorithm, throw a DataError." An absent crv is not
        // equal to anything, so it lands here rather than in a missing-field message of its own.
        if (!string.Equals(jwk.Crv, namedCurve, StringComparison.Ordinal))
        {
            context.ThrowDataError(
                what + ": the crv field of the JSON Web Key is " + Describe(jwk.Crv) + " rather than '" + namedCurve + "'.");
        }

        // The alg table, which ECDSA has and ECDH does not: the ECDH import steps read no alg at all, so a
        // JWK carrying one is imported with it ignored rather than checked.
        if (!isEcdh && jwk.Alg is { } alg)
        {
            if (!TryCurveFromJwkAlgorithm(alg, out var algNamedCurve))
            {
                context.ThrowDataError(
                    what + ": the alg field of the JSON Web Key is '" + alg + "', which names no curve this engine registers for ECDSA (ES256, ES384, ES512).");
            }

            if (!string.Equals(algNamedCurve, namedCurve, StringComparison.Ordinal))
            {
                context.ThrowDataError(
                    what + ": the alg field of the JSON Web Key is '" + alg + "', which names the curve " + algNamedCurve
                    + " where the key says " + namedCurve + ".");
            }
        }

        // "If jwk does not meet the requirements of Section 6.2.1 [or 6.2.2] of JSON Web Algorithms, then
        // throw a DataError" — which for an EC key is x, y and, for a private key, d, each present and each
        // at the curve's own field width.
        var fieldSize = FieldSizeInBytes(namedCurve);

        var parameters = new ECParameters
        {
            Curve = NamedCurveOf(namedCurve),
            Q = new ECPoint
            {
                X = ReadFixedWidthField(context, jwk.X, "x", fieldSize, namedCurve, what),
                Y = ReadFixedWidthField(context, jwk.Y, "y", fieldSize, namedCurve, what),
            },
            D = isPrivate ? ReadFixedWidthField(context, jwk.D, "d", fieldSize, namedCurve, what) : null,
        };

        var keyType = isPrivate ? CryptoKeyTypes.Private : CryptoKeyTypes.Public;

        return (
            ImportParameters(context, parameters, algorithmName, isPrivate, namedCurve, what),
            keyType,
            Describe(algorithmName, namedCurve));
    }

    /// <summary>
    /// A JSON Web Key field that must be present, must decode as base64url, and must be exactly the curve's
    /// field size in octets — see the remarks on this class for why the width is checked rather than padded.
    /// </summary>
    private static byte[] ReadFixedWidthField(
        CryptoContext context,
        string? value,
        string field,
        int fieldSize,
        string namedCurve,
        string what)
    {
        var bytes = JsonWebKeyData.RequireBase64UrlField(context, value, field, what);

        if (bytes.Length != fieldSize)
        {
            context.ThrowDataError(
                what + ": the " + field + " field of the JSON Web Key is " + bytes.Length + " byte(s) long, and JSON Web Algorithms requires the full "
                + fieldSize + " bytes a " + namedCurve + " value has.");
        }

        return bytes;
    }

    // -------------------------------------------------------------------------------------------------
    // exportKey
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// https://w3c.github.io/webcrypto/#ecdsa-operations-export-key and
    /// https://w3c.github.io/webcrypto/#ecdh-operations-export-key, which are the same steps — an EC key
    /// exports to all four formats, where an RSA one has no <c>raw</c> and a symmetric one no <c>spki</c> or
    /// <c>pkcs8</c>.
    /// </summary>
    internal static JsValue ExportKey(CryptoContext context, JsCryptoKey key, KeyFormat format, string what)
    {
        var namedCurve = key.Algorithm.NamedCurve!;

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

            case KeyFormat.Raw:
                // "Let data be a byte sequence representing the Elliptic Curve point Q … according to [SEC1]
                // 2.3.3 using the uncompressed format."
                RequireKeyType(context, key, CryptoKeyTypes.Public, what);
                return context.CreateArrayBuffer(UncompressedPoint(ReadKeyParameters(context, key, includePrivateParameters: false, what), namedCurve));

            case KeyFormat.Jwk:
                return ExportJwk(context, key, namedCurve, what);

            default:
                // "Otherwise: throw a NotSupportedError" — unreachable, every KeyFormat is above.
                context.ThrowNotSupportedError(
                    what + ": an " + key.Algorithm.Name + " key cannot be exported to the '" + KeyFormats.NameOf(format) + "' format.");
                return JsValue.Undefined;
        }
    }

    private static JsObject ExportJwk(CryptoContext context, JsCryptoKey key, string namedCurve, string what)
    {
        var isPrivate = string.Equals(key.KeyType, CryptoKeyTypes.Private, StringComparison.Ordinal);
        var parameters = ReadKeyParameters(context, key, isPrivate, what);
        var fieldSize = FieldSizeInBytes(namedCurve);

        // The platform hands back coordinates already at the field width, which is the width JSON Web
        // Algorithms asks for; the alignment is what makes that a promise rather than an observation.
        var x = AtFieldWidth(parameters.Q.X, fieldSize);
        var y = AtFieldWidth(parameters.Q.Y, fieldSize);

        if (!isPrivate)
        {
            return JsonWebKeyData.CreateEcPublicExport(context.Engine, namedCurve, x, y, key.Usages, key.Extractable);
        }

        return JsonWebKeyData.CreateEcPrivateExport(
            context.Engine, namedCurve, x, y, AtFieldWidth(parameters.D, fieldSize), key.Usages, key.Extractable);
    }

    // -------------------------------------------------------------------------------------------------
    // sign and verify
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// https://w3c.github.io/webcrypto/#ecdsa-operations-sign — "Perform the ECDSA signing process, as
    /// specified in [RFC6090], Section 5.4.2", with the hash taken from <c>EcdsaParams</c> rather than from
    /// the key.
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

        using var algorithm = CreateFromHandle(context, key, what);

        // The dispatch proved the key was made for ECDSA before this was called, and an ECDSA key is
        // rehydrated as an ECDsa — see the remarks on this class.
        var ecdsa = (ECDsa) algorithm;

        try
        {
            return ecdsa.SignData(
                message.ToArray(),
                HashName(normalized.HashName!),
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception e) when (IsCryptographicFailure(e))
        {
            // The signing steps name no error of their own, unlike RSA's — but a promise-returning operation
            // reports every failure as a rejection, and OperationError, "the operation failed for an
            // operation-specific reason", is what its siblings use for exactly this.
            context.ThrowOperationError(what + ": the signature could not be produced.");
            return null!;
        }
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#ecdsa-operations-verify: "Let result be a boolean with the value
    /// true if the signature is valid and the value false otherwise."
    /// </summary>
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

        // "If signature does not have a length of n * 2 bytes, then return false" — a step of its own, and
        // one of the two places this algorithm answers false rather than failing.
        //
        // Nothing observable depends on it: the platform's own verification answers false for a signature of
        // the wrong length too, which was measured rather than assumed. It is kept because it is the
        // specification's step and because it runs *before* the key is rehydrated, so a signature that
        // cannot be the right one costs no key import — and because "it happens to agree today" is a thin
        // reason to leave a normative step to somebody else's implementation.
        if (signature.Length != 2 * FieldSizeInBytes(key.Algorithm.NamedCurve!))
        {
            return false;
        }

        using var algorithm = CreateFromHandle(context, key, what);
        var ecdsa = (ECDsa) algorithm;

        try
        {
            return ecdsa.VerifyData(
                message,
                signature,
                HashName(normalized.HashName!),
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception e) when (IsCryptographicFailure(e))
        {
            // There is no error step here either, so the platform's refusal to look at a signature is
            // "otherwise", which covers every way a signature can fail to be the valid one.
            return false;
        }
    }

    // -------------------------------------------------------------------------------------------------
    // deriveBits
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// https://w3c.github.io/webcrypto/#ecdh-operations-derive-bits — "Perform the ECDH primitive specified
    /// in [RFC6090] Section 4 … Let secret be a byte sequence containing the result of applying the field
    /// element to octet string conversion defined in Section 6.2 of [RFC6090] to the output of the ECDH
    /// primitive."
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The secret is the raw x-coordinate, not a hashed or KDF'd derivative</b>, which is what
    /// <see cref="ECDiffieHellman.DeriveRawSecretAgreement"/> produces and what the every-other
    /// <c>DeriveKeyFrom*</c> method on that class deliberately does not — those apply a KDF of their own,
    /// which would be this engine inventing a derivation the specification does not describe. The raw method
    /// arrived in .NET 8, which is this feature area's floor anyway.
    /// </para>
    /// <para>
    /// <b>Steps 4 and 5 deliberately do not run where the prose puts them.</b> The specification derives
    /// <i>maximumLength</i> from the <b>public</b> key's domain parameters (step 4) and raises its
    /// <c>OperationError</c> for an over-long <c>length</c> (step 5) <i>before</i> steps 7 and 8 have
    /// established that the two keys are a pair at all — so read literally, a P-521 base key handed a P-256
    /// public key and asked for 528 bits is refused for its length rather than for the mismatch. Chrome,
    /// Firefox, Safari and Node all answer the <c>InvalidAccessError</c> of the later step instead, and the
    /// web-platform-tests pin exactly that: the <c>P-384 mismatched curves</c> and <c>P-521 mismatched
    /// curves</c> rows of <c>WebCryptoAPI/derive_bits_keys/ecdh_bits.https.any.js</c> ask for eight times the
    /// <b>base</b> key's field width in bits — 384 and 528 against a P-256 public key — and assert
    /// <c>InvalidAccessError</c>, which no implementation obeying step 5 where it is written can produce.
    /// (Their <c>P-256</c> sibling passes either way only because the curve it is mismatched against is the
    /// wider one.) Chrome, Firefox and Safari all report that file 40/40 on wpt.fyi, so the browser answer is
    /// not one vendor's reading. This engine follows them: every key-agreement check runs first, and the
    /// length ceiling is measured only once the pair is known to be one. The disagreement is
    /// filed as https://github.com/w3c/webcrypto/issues/560 (from
    /// https://github.com/sebastienros/jint/issues/3180); if the specification reorders its steps, what goes
    /// is this comment and not the code.
    /// </para>
    /// <para>
    /// <b>The order is observable, which is why the rest of it is spelled out below.</b> A caller passing a
    /// private key as <c>public</c>, an ECDSA key as <c>public</c>, a public key as <c>baseKey</c>, a
    /// mismatched curve and an over-long <c>length</c> all earn different errors, and which one a request
    /// carrying several of those mistakes gets is decided here: the checks on the <c>public</c> member
    /// (steps 2 and 3), then the checks on <c>baseKey</c> (steps 6, 7 and 8), then the length ceiling
    /// (steps 4 and 5), then the agreement itself.
    /// </para>
    /// </remarks>
    internal static byte[] DeriveBits(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        JsCryptoKey key,
        uint? length,
        string what)
    {
        // Step 1: "Let publicKey be the public member of normalizedAlgorithm."
        var publicKey = normalized.PublicKey!;

        // Step 2: "If the [[type]] internal slot of publicKey is not 'public', then throw an
        // InvalidAccessError."
        if (!string.Equals(publicKey.KeyType, CryptoKeyTypes.Public, StringComparison.Ordinal))
        {
            context.ThrowInvalidAccessError(
                what + ": the public member of the algorithm is a " + publicKey.KeyType + " key, and this operation needs a public one.");
        }

        // Step 3: "If the name attribute of the [[algorithm]] internal slot of publicKey is not equal to the
        // name member of normalizedAlgorithm, then throw an InvalidAccessError." An ECDSA key over the very
        // same curve lands here — the two algorithms share a key encoding and not a purpose.
        if (!string.Equals(publicKey.Algorithm.Name, normalized.Name, StringComparison.Ordinal))
        {
            context.ThrowInvalidAccessError(
                what + ": the public member of the algorithm is an " + publicKey.Algorithm.Name + " key, not an " + normalized.Name + " one.");
        }

        // Step 6: "If the [[type]] internal slot of key is not 'private', then throw an InvalidAccessError."
        RequireKeyType(context, key, CryptoKeyTypes.Private, what);

        // Step 7 is already true: the deriveBits method proved the base key's algorithm name equals
        // normalizedAlgorithm's, and step 3 proved the public key's does, so the two are the same string. It
        // is written out anyway because the operation is defined to make it, and because that reasoning stops
        // holding the moment anything reaches these steps by another route.
        if (!string.Equals(publicKey.Algorithm.Name, key.Algorithm.Name, StringComparison.Ordinal))
        {
            context.ThrowInvalidAccessError(
                what + ": the public member of the algorithm is an " + publicKey.Algorithm.Name + " key and the base key is an "
                + key.Algorithm.Name + " one.");
        }

        // Step 8: "If the namedCurve attribute of the [[algorithm]] internal slot of publicKey is not equal
        // to the namedCurve property of the [[algorithm]] internal slot of key, then throw an
        // InvalidAccessError." This is the check that stands between a script and a platform
        // ArgumentException — .NET reports two keys of different sizes that way, which was measured.
        if (!string.Equals(publicKey.Algorithm.NamedCurve, key.Algorithm.NamedCurve, StringComparison.Ordinal))
        {
            context.ThrowInvalidAccessError(
                what + ": the public key is on the curve " + publicKey.Algorithm.NamedCurve + " and the base key on "
                + key.Algorithm.NamedCurve + "; an agreement needs one curve.");
        }

        // Steps 4 and 5, run here rather than where the prose writes them — see the remarks above: "Let
        // maximumLength be the length in bits of the output of the field element to octet string conversion …
        // for the EC domain parameters associated with publicKey … If length is not null and is greater than
        // maximumLength, then throw an OperationError." Step 8 has
        // just proved the two curves are one, so measuring the ceiling off the public key's domain parameters
        // and off the base key's are now the same measurement, and the only request whose answer this move
        // changes is one that was never going to derive anything: a mismatched pair, which browsers and the
        // corpus's mismatched-curve rows say earns the InvalidAccessError above. The conversion pads to whole
        // octets, so P-521's maximum is 528 rather than 521.
        var maximumLength = 8 * FieldSizeInBytes(publicKey.Algorithm.NamedCurve!);

        if (length is { } requested && requested > maximumLength)
        {
            context.ThrowOperationError(
                what + ": a length of " + requested + " bits was asked for, and a shared secret on "
                + publicKey.Algorithm.NamedCurve + " is " + maximumLength + " bits long.");
        }

        byte[] secret;

        // Steps 9 and 10: the ECDH primitive, and "If performing the operation results in an error, then
        // throw an OperationError".
        using (var privateAlgorithm = CreateFromHandle(context, key, what))
        using (var publicAlgorithm = CreateFromHandle(context, publicKey, what))
        {
            // Both keys were made for ECDH — steps 3 and 7 — and an ECDH key is rehydrated as an
            // ECDiffieHellman; see the remarks on this class.
            var privateEcdh = (ECDiffieHellman) privateAlgorithm;

            using var peer = ((ECDiffieHellman) publicAlgorithm).PublicKey;

            try
            {
                secret = privateEcdh.DeriveRawSecretAgreement(peer);
            }
            catch (Exception e) when (IsDerivationFailure(e))
            {
                context.ThrowOperationError(what + ": the shared secret could not be derived.");
                return null!;
            }
        }

        // Step 11: "If length is null: return secret. Otherwise: if the length in bits of secret is less than
        // length, throw an OperationError; otherwise return a byte sequence containing the first length bits
        // of secret."
        if (length is not { } bits)
        {
            return secret;
        }

        if (8L * secret.Length < bits)
        {
            // Unreachable: step 8 proved the two keys share a curve, and step 5 then measured that very
            // curve's field width. It is the specification's step and it costs nothing.
            context.ThrowOperationError(
                what + ": a length of " + bits + " bits was asked for, and the shared secret is " + (8 * secret.Length) + " bits long.");
        }

        return FirstBits(secret, bits);
    }

    /// <summary>
    /// "A byte sequence containing the first <c>length</c> bits of secret" — <c>ceil(length / 8)</c> bytes,
    /// with the bits past <c>length</c> in the last one cleared.
    /// </summary>
    /// <remarks>
    /// A length that is not a whole number of bytes is deliberately not refused: the ECDH steps impose no
    /// such restriction, where HKDF's and PBKDF2's step 1 both do, and truncating to a bit is what the step
    /// says. Clearing the tail rather than leaving it is what makes the answer a function of
    /// <c>length</c> alone — the same 230-bit prefix of one secret must be one byte sequence however it was
    /// asked for.
    /// </remarks>
    private static byte[] FirstBits(byte[] secret, uint bits)
    {
        var wholeBytes = (int) (bits / 8);
        var remainder = (int) (bits % 8);

        if (remainder == 0)
        {
            return secret.AsSpan(0, wholeBytes).ToArray();
        }

        var truncated = secret.AsSpan(0, wholeBytes + 1).ToArray();
        truncated[wholeBytes] &= (byte) (0xFF << (8 - remainder));
        return truncated;
    }

    // -------------------------------------------------------------------------------------------------
    // Curves
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// Step 1 of both import operations and the "Otherwise" arm of both generate operations: the only names
    /// this engine implements are the three the specification defines, matched case-sensitively.
    /// </summary>
    /// <remarks>
    /// The curves an "applicable specification" may add — secp256k1, and the Edwards and Montgomery curves
    /// of the Secure Curves draft — are deliberately not among them: each has its own registration with its
    /// own algorithm names, and reaching one of them through this member would be Jint inventing a curve
    /// registry of its own.
    /// </remarks>
    private static string RequireSupportedCurve(CryptoContext context, string? namedCurve, string algorithmName, string what)
    {
        switch (namedCurve)
        {
            case AlgorithmNormalization.P256:
            case AlgorithmNormalization.P384:
            case AlgorithmNormalization.P521:
                return namedCurve;
            default:
                context.ThrowNotSupportedError(
                    what + ": " + Describe(namedCurve) + " is not a curve this engine registers for " + algorithmName
                    + " (" + AlgorithmNormalization.P256 + ", " + AlgorithmNormalization.P384 + ", " + AlgorithmNormalization.P521 + ").");
                return null!;
        }
    }

    /// <summary>
    /// The algorithm identifier of a <c>SubjectPublicKeyInfo</c> or a <c>PrivateKeyInfo</c>, read as the
    /// specification's own steps enumerate it. See the remarks on this class for why the DER is parsed here
    /// rather than the imported key questioned afterwards.
    /// </summary>
    private static string ReadNamedCurveFromDer(
        CryptoContext context,
        byte[] data,
        bool isPrivate,
        string structure,
        string what)
    {
        string curveOid;

        try
        {
            var reader = new AsnReader(data, AsnEncodingRules.DER);
            var contents = reader.ReadSequence();

            // "parse an ASN.1 structure … with exactData set to true": a structure that is followed by
            // anything is not the structure that was asked for, however well-formed its prefix is.
            if (reader.HasData)
            {
                context.ThrowDataError(what + ": the " + structure + " is followed by trailing byte(s).");
            }

            if (isPrivate)
            {
                // PrivateKeyInfo's own version field, which precedes the algorithm identifier.
                contents.ReadInteger();
            }

            var algorithmIdentifier = contents.ReadSequence();

            // "If the algorithm object identifier field … is not equal to the id-ecPublicKey object
            // identifier defined in [RFC5480], then throw a DataError."
            var algorithmOid = algorithmIdentifier.ReadObjectIdentifier();
            if (!string.Equals(algorithmOid, IdEcPublicKey, StringComparison.Ordinal))
            {
                context.ThrowDataError(
                    what + ": the " + structure + " names the algorithm " + algorithmOid + " rather than id-ecPublicKey (" + IdEcPublicKey + ").");
            }

            // "If the parameters field … is absent, then throw a DataError", and then "If params is not an
            // instance of the ECParameters ASN.1 type … that specifies a namedCurve, then throw a
            // DataError" — which is what reading an object identifier here says.
            if (!algorithmIdentifier.HasData)
            {
                context.ThrowDataError(what + ": the " + structure + " carries no curve parameters.");
            }

            curveOid = algorithmIdentifier.ReadObjectIdentifier();
        }
        catch (AsnContentException)
        {
            // "If an error occurred while parsing, then throw a DataError."
            context.ThrowDataError(what + ": the data is not a valid " + structure + " structure.");
            return null!;
        }

        switch (curveOid)
        {
            case Secp256r1:
                return AlgorithmNormalization.P256;
            case Secp384r1:
                return AlgorithmNormalization.P384;
            case Secp521r1:
                return AlgorithmNormalization.P521;
            default:
                // The "Otherwise" arm, whose end is "If an error occurred or there are no applicable
                // specifications, throw a DataError" — there are none here.
                context.ThrowDataError(
                    what + ": the " + structure + " names the curve " + curveOid + ", which is not one this engine implements.");
                return null!;
        }
    }

    /// <summary>The <see cref="ECCurve"/> a recognized curve name denotes.</summary>
    /// <remarks>
    /// Every name is spelled out and the default throws rather than one of them standing in as a fallback: a
    /// curve added to the registry without a case here would silently be built on another curve's domain
    /// parameters, which is the one mistake in this file that would produce keys instead of errors.
    /// </remarks>
    private static ECCurve NamedCurveOf(string namedCurve)
    {
        switch (namedCurve)
        {
            case AlgorithmNormalization.P256:
                return ECCurve.NamedCurves.nistP256;
            case AlgorithmNormalization.P384:
                return ECCurve.NamedCurves.nistP384;
            case AlgorithmNormalization.P521:
                return ECCurve.NamedCurves.nistP521;
            default:
                Throw.InvalidOperationException("Unhandled named curve '" + namedCurve + "'.");
                return default;
        }
    }

    /// <summary>
    /// The length of one coordinate, and of the private key value, in octets — "the full size of a
    /// coordinate for the curve", https://www.rfc-editor.org/rfc/rfc7518#section-6.2.1.2.
    /// </summary>
    /// <remarks>
    /// It is the field size rounded <i>up</i> to whole octets, which for P-521 is 66 rather than 65: 521
    /// bits is 65.125 bytes, and the RFC spells that case out — "if the value of 'crv' is 'P-521', the octet
    /// string must be 66 octets long". It is also the <c>n</c> of the signature steps, "the smallest integer
    /// such that n * 8 is greater than the logarithm to base 2 of the order of the base point", so the same
    /// number decides a JSON Web Key field's width and a signature's length.
    /// </remarks>
    private static int FieldSizeInBytes(string namedCurve)
    {
        switch (namedCurve)
        {
            case AlgorithmNormalization.P256:
                return 32;
            case AlgorithmNormalization.P384:
                return 48;
            case AlgorithmNormalization.P521:
                return 66;
            default:
                Throw.InvalidOperationException("Unhandled named curve '" + namedCurve + "'.");
                return 0;
        }
    }

    /// <summary>
    /// The <c>alg</c> table of the ECDSA import steps: "If the alg field is equal to the string 'ES256':
    /// let algNamedCurve be the string 'P-256'", and so on.
    /// </summary>
    /// <remarks>
    /// <c>ES512</c> names <b>P-521</b>, not a curve of 512 bits — the number in a JOSE <c>alg</c> is the
    /// hash's output length, and ECDSA over P-521 is paired with SHA-512 by
    /// https://www.rfc-editor.org/rfc/rfc7518#section-3.4. It is the one row of this table that cannot be
    /// derived from the curve's own name, which is why the table is written out.
    /// </remarks>
    private static bool TryCurveFromJwkAlgorithm(string alg, out string namedCurve)
    {
        switch (alg)
        {
            case "ES256":
                namedCurve = AlgorithmNormalization.P256;
                return true;
            case "ES384":
                namedCurve = AlgorithmNormalization.P384;
                return true;
            case "ES512":
                namedCurve = AlgorithmNormalization.P521;
                return true;
            default:
                namedCurve = null!;
                return false;
        }
    }

    // -------------------------------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// The two exception types the platform reports a key it will not accept by. See the remarks on this
    /// class: on Windows a point that is not on its curve arrives as a
    /// <see cref="PlatformNotSupportedException"/>, which is not a <see cref="CryptographicException"/> and
    /// would otherwise escape as a CLR exception out of a promise-returning operation.
    /// </summary>
    private static bool IsCryptographicFailure(Exception exception)
        => exception is CryptographicException or PlatformNotSupportedException;

    /// <summary>
    /// The same two, plus <see cref="ArgumentException"/>, which is how .NET reports two keys of different
    /// sizes to <see cref="ECDiffieHellman.DeriveRawSecretAgreement"/> ("The keys from both parties must be
    /// the same size to generate a secret agreement", measured on Windows). The specification's step 8 makes
    /// that unreachable from <c>deriveBits</c> — but the lesson this file already records for
    /// <see cref="PlatformNotSupportedException"/> is that guessing which exception type a platform picks is
    /// how a CLR exception ends up erupting out of a promise-returning operation, and the catch costs nothing.
    /// </summary>
    private static bool IsDerivationFailure(Exception exception)
        => IsCryptographicFailure(exception) || exception is ArgumentException;

    private static bool IsEcdh(string algorithmName)
        => string.Equals(algorithmName, AlgorithmNormalization.Ecdh, StringComparison.Ordinal);

    /// <summary>
    /// An empty key object of the class the algorithm names — see the remarks on this class for why ECDSA
    /// and ECDH are kept apart even though their DER is the same.
    /// </summary>
    private static AsymmetricAlgorithm CreateAlgorithm(string algorithmName)
        => IsEcdh(algorithmName) ? ECDiffieHellman.Create() : (AsymmetricAlgorithm) ECDsa.Create();

    /// <summary>A freshly generated key on <paramref name="curve"/>, of the class the algorithm names.</summary>
    private static AsymmetricAlgorithm CreateAlgorithm(string algorithmName, ECCurve curve)
        => IsEcdh(algorithmName) ? ECDiffieHellman.Create(curve) : (AsymmetricAlgorithm) ECDsa.Create(curve);

    /// <summary>
    /// Rehydrates the key a <c>[[handle]]</c> describes. See the remarks on this class for why the handle is
    /// DER and this happens per operation.
    /// </summary>
    private static AsymmetricAlgorithm CreateFromHandle(CryptoContext context, JsCryptoKey key, string what)
    {
        var algorithm = CreateAlgorithm(key.Algorithm.Name);

        try
        {
            if (string.Equals(key.KeyType, CryptoKeyTypes.Private, StringComparison.Ordinal))
            {
                algorithm.ImportPkcs8PrivateKey(key.Handle, out _);
            }
            else
            {
                algorithm.ImportSubjectPublicKeyInfo(key.Handle, out _);
            }

            return algorithm;
        }
        catch (Exception e) when (IsCryptographicFailure(e))
        {
            algorithm.Dispose();

            // "If the underlying cryptographic key material represented by the [[handle]] internal slot of
            // key cannot be accessed, then throw an OperationError." Unreachable in practice — the bytes are
            // the platform's own canonical encoding of a key it built.
            context.ThrowOperationError(what + ": the key material could not be accessed.");
            return null!;
        }
    }

    /// <summary>
    /// Builds a key from parameters a <c>jwk</c> or a <c>raw</c> import produced and keeps the platform's own
    /// canonical DER as the handle — which is exactly what <c>exportKey</c>'s <c>spki</c> and <c>pkcs8</c>
    /// branches describe, so the two can never disagree.
    /// </summary>
    private static byte[] ImportParameters(
        CryptoContext context,
        ECParameters parameters,
        string algorithmName,
        bool isPrivate,
        string namedCurve,
        string what)
    {
        using var algorithm = CreateAlgorithm(algorithmName);

        try
        {
            switch (algorithm)
            {
                case ECDiffieHellman ecdh:
                    ecdh.ImportParameters(parameters);
                    break;
                case ECDsa ecdsa:
                    ecdsa.ImportParameters(parameters);
                    break;
                default:
                    // Unreachable: CreateAlgorithm produces one of exactly those two.
                    Throw.InvalidOperationException("Unhandled elliptic-curve algorithm '" + algorithmName + "'.");
                    break;
            }

            return isPrivate ? algorithm.ExportPkcs8PrivateKey() : algorithm.ExportSubjectPublicKeyInfo();
        }
        catch (Exception e) when (IsCryptographicFailure(e))
        {
            // "If the key value is not a valid point on the Elliptic Curve identified by the namedCurve
            // member of normalizedAlgorithm throw a DataError."
            context.ThrowDataError(
                what + ": the key value is not a valid point on the curve " + namedCurve + ".");
            return null!;
        }
    }

    /// <summary>
    /// The <see cref="ECParameters"/> of the key a <c>[[handle]]</c> describes — what the <c>jwk</c> and
    /// <c>raw</c> export branches are written in terms of.
    /// </summary>
    private static ECParameters ReadKeyParameters(
        CryptoContext context,
        JsCryptoKey key,
        bool includePrivateParameters,
        string what)
    {
        using var algorithm = CreateFromHandle(context, key, what);

        try
        {
            switch (algorithm)
            {
                case ECDiffieHellman ecdh:
                    return ecdh.ExportParameters(includePrivateParameters);
                case ECDsa ecdsa:
                    return ecdsa.ExportParameters(includePrivateParameters);
                default:
                    // Unreachable: CreateFromHandle produces one of exactly those two.
                    Throw.InvalidOperationException("Unhandled elliptic-curve algorithm '" + key.Algorithm.Name + "'.");
                    return default;
            }
        }
        catch (Exception e) when (IsCryptographicFailure(e))
        {
            context.ThrowOperationError(what + ": the key material could not be accessed.");
            return default;
        }
    }

    /// <summary>
    /// The uncompressed encoding of a public point, [SEC1] 2.3.3: <c>0x04</c> followed by the two
    /// coordinates, each at the curve's field width.
    /// </summary>
    private static byte[] UncompressedPoint(ECParameters parameters, string namedCurve)
    {
        var fieldSize = FieldSizeInBytes(namedCurve);
        var point = new byte[1 + (2 * fieldSize)];

        point[0] = UncompressedPointMarker;
        AtFieldWidth(parameters.Q.X, fieldSize).CopyTo(point.AsSpan(1));
        AtFieldWidth(parameters.Q.Y, fieldSize).CopyTo(point.AsSpan(1 + fieldSize));

        return point;
    }

    /// <summary>
    /// A big-endian value at exactly <paramref name="fieldSize"/> bytes, left-padded with zeros if the
    /// platform handed back fewer. It never does for the three named curves — <see cref="ECParameters"/>
    /// describes a named curve's values at the field width, and that was verified — so this is the promise
    /// rather than a conversion, and it is what keeps a coordinate whose leading byte is zero from being
    /// exported one byte short of what JSON Web Algorithms requires.
    /// </summary>
    private static byte[] AtFieldWidth(byte[]? value, int fieldSize)
    {
        if (value is null)
        {
            return new byte[fieldSize];
        }

        if (value.Length == fieldSize)
        {
            return value;
        }

        var padded = new byte[fieldSize];
        var offset = fieldSize - value.Length;

        // A value longer than the field cannot describe a point on it, and the platform cannot produce one;
        // the trailing bytes are the ones kept so that the result is still the value's low-order end.
        if (offset < 0)
        {
            value.AsSpan(-offset).CopyTo(padded);
            return padded;
        }

        value.CopyTo(padded.AsSpan(offset));
        return padded;
    }

    /// <summary>The <c>EcKeyAlgorithm</c> a generate or an import ends in.</summary>
    private static CryptoKeyAlgorithm Describe(string algorithmName, string namedCurve)
        => new(algorithmName, Length: 0, HashName: null, NamedCurve: namedCurve);

    /// <summary>
    /// "If usages contains an entry which is not …, then throw a SyntaxError" — the first step of every
    /// generate and import branch, differing only in the set it names.
    /// </summary>
    private static void RequireUsagesWithin(CryptoContext context, KeyUsage usages, KeyUsage allowed, string algorithmName, string what)
    {
        if ((usages & ~allowed) == KeyUsage.None)
        {
            return;
        }

        if (allowed == KeyUsage.None)
        {
            // ECDH's public key, whose steps say "If usages is not empty" rather than naming a set — so the
            // message says that too, rather than offering an empty list of permitted usages.
            context.ThrowSyntaxError(
                what + ": an " + algorithmName + " public key carries no usages at all, and " + KeyUsages.Describe(usages) + " was asked for.");
        }

        context.ThrowSyntaxError(
            what + ": this " + algorithmName + " key supports the usages " + KeyUsages.Describe(allowed)
            + ", not " + KeyUsages.Describe(usages & ~allowed) + ".");
    }

    /// <summary>
    /// "If the [[type]] internal slot of key is not <c>expected</c>, then throw an InvalidAccessError" — the
    /// first step of both operations, and the one that makes a public key refuse to sign.
    /// </summary>
    private static void RequireKeyType(CryptoContext context, JsCryptoKey key, string expected, string what)
    {
        if (!string.Equals(key.KeyType, expected, StringComparison.Ordinal))
        {
            context.ThrowInvalidAccessError(
                what + ": this operation needs a " + expected + " key, and the key given is a " + key.KeyType + " key.");
        }
    }

    private static HashAlgorithmName HashName(string hashName)
    {
        switch (hashName)
        {
            case AlgorithmNormalization.Sha1:
                // SHA-1 is one of the four hashes the digest registry carries, so an EcdsaParams may name it
                // and a script asking for it has already chosen; nothing in the engine picks it for anybody.
                return HashAlgorithmName.SHA1;
            case AlgorithmNormalization.Sha256:
                return HashAlgorithmName.SHA256;
            case AlgorithmNormalization.Sha384:
                return HashAlgorithmName.SHA384;
            case AlgorithmNormalization.Sha512:
                return HashAlgorithmName.SHA512;
            default:
                // Unreachable: the hash was matched against the digest registry during normalization.
                Throw.InvalidOperationException("Unhandled ECDSA hash algorithm '" + hashName + "'.");
                return default;
        }
    }

    private static string Describe(string? value) => value is null ? "an absent curve name" : "'" + value + "'";
}
#endif
