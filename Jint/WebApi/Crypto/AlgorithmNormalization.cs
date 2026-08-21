#if NET8_0_OR_GREATER
using System.Text;
using Jint.Native;
using Jint.Native.ArrayBuffer;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.WebApi.Encoding;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The operations <c>supportedAlgorithms</c> is keyed by —
/// https://w3c.github.io/webcrypto/#algorithm-normalization-internal — restricted to the ones this engine
/// implements. <c>deriveBits</c>, <c>wrapKey</c>, <c>unwrapKey</c> and <c>get key length</c> are absent
/// because no method that would reach them exists.
/// </summary>
internal enum CryptoOperation
{
    Digest,
    Encrypt,
    Decrypt,
    Sign,
    Verify,
    GenerateKey,
    ImportKey,
    ExportKey,
}

/// <summary>
/// The <c>KeyFormat</c> enumeration — https://w3c.github.io/webcrypto/#dfn-KeyFormat.
/// </summary>
/// <remarks>
/// All four values are recognized by the WebIDL conversion, and a fifth spelling is a <c>TypeError</c>
/// because the argument's IDL type is an enumeration. Which of them an algorithm accepts is then the
/// algorithm's own business, and a format it does not handle earns the <c>NotSupportedError</c> its steps
/// specify — <c>spki</c> and <c>pkcs8</c> describe an asymmetric key, so a symmetric algorithm refuses both,
/// and <c>raw</c> describes a symmetric one, so an RSA algorithm refuses it. That is what a browser answers
/// too.
/// </remarks>
internal enum KeyFormat
{
    Raw,
    Spki,
    Pkcs8,
    Jwk,
}

/// <summary>
/// The <c>KeyFormat</c> values as the specification spells them, which is what an error message names and
/// what the WebIDL enumeration conversion matches against.
/// </summary>
internal static class KeyFormats
{
    internal const string Raw = "raw";
    internal const string Spki = "spki";
    internal const string Pkcs8 = "pkcs8";
    internal const string Jwk = "jwk";

    /// <summary>
    /// Every value is spelled out and the default throws, rather than one of them standing in as the
    /// fallback: this string reaches a script in an error message naming the format it asked for, so a value
    /// added to <see cref="KeyFormat"/> without a case here must fail loudly rather than call itself
    /// <c>"jwk"</c>.
    /// </summary>
    internal static string NameOf(KeyFormat format)
    {
        switch (format)
        {
            case KeyFormat.Raw:
                return Raw;
            case KeyFormat.Spki:
                return Spki;
            case KeyFormat.Pkcs8:
                return Pkcs8;
            case KeyFormat.Jwk:
                return Jwk;
            default:
                // Unreachable: every KeyFormat is above, and the only producer of one is the WebIDL
                // enumeration conversion, which recognizes exactly those four.
                Throw.InvalidOperationException("Unhandled key format '" + format + "'.");
                return null!;
        }
    }
}

/// <summary>
/// The result of "normalizing an algorithm": the registered name, plus whichever members the IDL dictionary
/// registered for that (algorithm, operation) pair declares.
/// </summary>
/// <remarks>
/// One class rather than a dictionary type per pair, because the union of the members across the pairs this
/// engine registers is ten fields and nothing reads a field its own operation did not fill in. A member that
/// the specification calls "not present" is <see langword="null"/> here, which is the distinction several
/// steps turn on — <c>HmacKeyGenParams</c>'s absent <c>length</c> means the hash's block size, where a
/// present zero is an <c>OperationError</c>, and <c>RsaOaepParams</c>'s absent <c>label</c> means the empty
/// byte sequence.
/// </remarks>
internal sealed class NormalizedAlgorithm
{
    internal NormalizedAlgorithm(string name)
    {
        Name = name;
    }

    /// <summary>The <c>name</c> member, always the registered spelling rather than the caller's.</summary>
    internal string Name { get; }

    /// <summary>The <c>hash</c> member of <c>HmacKeyGenParams</c>/<c>HmacImportParams</c>, itself normalized.</summary>
    internal string? HashName { get; set; }

    /// <summary>The <c>length</c> member of <c>HmacKeyGenParams</c>, <c>HmacImportParams</c> or <c>AesKeyGenParams</c>.</summary>
    internal uint? Length { get; set; }

    /// <summary>The <c>iv</c> member of <c>AesGcmParams</c>, copied at normalization time as the specification says.</summary>
    internal byte[]? Iv { get; set; }

    /// <summary>The <c>additionalData</c> member of <c>AesGcmParams</c>.</summary>
    internal byte[]? AdditionalData { get; set; }

    /// <summary>The <c>tagLength</c> member of <c>AesGcmParams</c>, in bits.</summary>
    internal int? TagLength { get; set; }

    /// <summary>The <c>modulusLength</c> member of <c>RsaHashedKeyGenParams</c>, in bits.</summary>
    internal uint? ModulusLength { get; set; }

    /// <summary>
    /// The <c>publicExponent</c> member of <c>RsaHashedKeyGenParams</c> — a <c>BigInteger</c>, which is the
    /// big-endian magnitude of an unsigned integer.
    /// </summary>
    internal byte[]? PublicExponent { get; set; }

    /// <summary>The <c>saltLength</c> member of <c>RsaPssParams</c>, in <i>bytes</i>.</summary>
    internal uint? SaltLength { get; set; }

    /// <summary>
    /// The <c>label</c> member of <c>RsaOaepParams</c>, copied at normalization time, or
    /// <see langword="null"/> when it is not present — which the operation reads as the empty byte sequence.
    /// </summary>
    internal byte[]? Label { get; set; }

    /// <summary>
    /// The <c>namedCurve</c> member of <c>EcKeyGenParams</c> or <c>EcKeyImportParams</c>, the caller's string
    /// verbatim — the operation is what decides whether it names a curve at all.
    /// </summary>
    internal string? NamedCurve { get; set; }
}

/// <summary>
/// "Normalize an algorithm" — https://w3c.github.io/webcrypto/#algorithm-normalization-normalize-an-algorithm —
/// together with the registry it consults.
/// </summary>
/// <remarks>
/// <para>
/// The registry is written out as explicit lists rather than derived from anything, because an algorithm that
/// the BCL grows support for later must not become reachable from script until someone has read this
/// specification again. It is also what makes feature detection honest: an operation an algorithm is not
/// registered for is a <c>NotSupportedError</c>, never a half-working call.
/// </para>
/// <para>
/// The whole of it runs synchronously, before the operation does any work, which is the point the
/// specification makes in its own description: "Web IDL type mapping can occur before any control is returned
/// to the calling script, which would potentially allow the mutation of parameters or the script
/// environment". Every member is read exactly once, in the order WebIDL reads them, and never looked at
/// again.
/// </para>
/// </remarks>
internal static class AlgorithmNormalization
{
    /// <summary>
    /// https://w3c.github.io/webcrypto/#sha-registration — "The recognized algorithm names are
    /// <c>SHA-1</c>, <c>SHA-256</c>, <c>SHA-384</c>, and <c>SHA-512</c> for the respective SHA algorithms".
    /// </summary>
    internal const string Sha1 = "SHA-1";
    internal const string Sha256 = "SHA-256";
    internal const string Sha384 = "SHA-384";
    internal const string Sha512 = "SHA-512";

    /// <summary>https://w3c.github.io/webcrypto/#hmac-registration</summary>
    internal const string Hmac = "HMAC";

    /// <summary>https://w3c.github.io/webcrypto/#aes-gcm-registration</summary>
    internal const string AesGcm = "AES-GCM";

    /// <summary>https://w3c.github.io/webcrypto/#rsassa-pkcs1-registration</summary>
    internal const string RsassaPkcs1V15 = "RSASSA-PKCS1-v1_5";

    /// <summary>https://w3c.github.io/webcrypto/#rsa-pss-registration</summary>
    internal const string RsaPss = "RSA-PSS";

    /// <summary>https://w3c.github.io/webcrypto/#rsa-oaep-registration</summary>
    internal const string RsaOaep = "RSA-OAEP";

    /// <summary>https://w3c.github.io/webcrypto/#ecdsa-registration</summary>
    internal const string Ecdsa = "ECDSA";

    /// <summary>https://w3c.github.io/webcrypto/#ecdh-registration</summary>
    internal const string Ecdh = "ECDH";

    /// <summary>
    /// The three values <c>NamedCurve</c> takes — https://w3c.github.io/webcrypto/#dfn-NamedCurve, "NIST
    /// recommended curve P-256, also known as secp256r1" and its two siblings.
    /// </summary>
    /// <remarks>
    /// <c>NamedCurve</c> is <c>typedef DOMString</c> rather than a WebIDL enumeration, so nothing about the
    /// argument conversion restricts it and an unrecognized value reaches the operation intact. Each
    /// operation then matches it <i>case-sensitively</i> — "If the namedCurve member of normalizedAlgorithm
    /// is not one of 'P-256', 'P-384' or 'P-521' … throw a NotSupportedError" — which is why <c>'p-256'</c>
    /// is a <c>NotSupportedError</c> where the algorithm <i>name</i> one word to its left would have matched
    /// case-insensitively.
    /// </remarks>
    internal const string P256 = "P-256";
    internal const string P384 = "P-384";
    internal const string P521 = "P-521";

    private static readonly string[] _digestAlgorithms = [Sha1, Sha256, Sha384, Sha512];
    private static readonly string[] _signatureAlgorithms = [Hmac, RsassaPkcs1V15, RsaPss, Ecdsa];
    private static readonly string[] _cipherAlgorithms = [AesGcm, RsaOaep];
    private static readonly string[] _keyAlgorithms = [Hmac, AesGcm, RsassaPkcs1V15, RsaPss, RsaOaep, Ecdsa, Ecdh];

    private static readonly JsString _nameKey = new("name");
    private static readonly JsString _hashKey = new("hash");
    private static readonly JsString _lengthKey = new("length");
    private static readonly JsString _ivKey = new("iv");
    private static readonly JsString _additionalDataKey = new("additionalData");
    private static readonly JsString _tagLengthKey = new("tagLength");
    private static readonly JsString _modulusLengthKey = new("modulusLength");
    private static readonly JsString _publicExponentKey = new("publicExponent");
    private static readonly JsString _saltLengthKey = new("saltLength");
    private static readonly JsString _labelKey = new("label");
    private static readonly JsString _namedCurveKey = new("namedCurve");

    /// <summary>
    /// The associative container "stored at the <c>op</c> key of <c>supportedAlgorithms</c>".
    /// </summary>
    /// <remarks>
    /// Every operation is spelled out and the default throws rather than answering the empty registry: an
    /// operation added to <see cref="CryptoOperation"/> without an entry here would silently register nothing
    /// for it, so every algorithm name would be a <c>NotSupportedError</c> and the new operation would look
    /// implemented while doing nothing.
    /// </remarks>
    internal static string[] RegisteredFor(CryptoOperation operation)
    {
        switch (operation)
        {
            case CryptoOperation.Digest:
                return _digestAlgorithms;
            case CryptoOperation.Encrypt:
            case CryptoOperation.Decrypt:
                return _cipherAlgorithms;
            case CryptoOperation.Sign:
            case CryptoOperation.Verify:
                return _signatureAlgorithms;
            case CryptoOperation.GenerateKey:
            case CryptoOperation.ImportKey:
            case CryptoOperation.ExportKey:
                return _keyAlgorithms;
            default:
                Throw.InvalidOperationException("Unhandled crypto operation '" + operation + "'.");
                return null!;
        }
    }

    /// <summary>The name an error message calls the operation, which is also the method's own name.</summary>
    /// <remarks>
    /// Every operation is spelled out and the default throws, for the reason <see cref="RegisteredFor"/>
    /// gives: an operation without a case here would call itself <c>"exportKey"</c> in every message it
    /// produced.
    /// </remarks>
    internal static string NameOf(CryptoOperation operation)
    {
        switch (operation)
        {
            case CryptoOperation.Digest:
                return "digest";
            case CryptoOperation.Encrypt:
                return "encrypt";
            case CryptoOperation.Decrypt:
                return "decrypt";
            case CryptoOperation.Sign:
                return "sign";
            case CryptoOperation.Verify:
                return "verify";
            case CryptoOperation.GenerateKey:
                return "generateKey";
            case CryptoOperation.ImportKey:
                return "importKey";
            case CryptoOperation.ExportKey:
                return "exportKey";
            default:
                Throw.InvalidOperationException("Unhandled crypto operation '" + operation + "'.");
                return null!;
        }
    }

    /// <summary>
    /// The <c>AlgorithmIdentifier</c> conversion alone — <c>typedef (object or DOMString)</c>, so an object
    /// stays an object and everything else is stringified.
    /// </summary>
    /// <remarks>
    /// It is separated from <see cref="Normalize"/> because the two happen at different times: this is an
    /// argument conversion, which WebIDL runs before a single step of the method body, while normalization is
    /// the body's own step 2. Between them sits the conversion of the <i>later</i> arguments, which is why
    /// <c>digest('nonsense', 42)</c> is the <c>TypeError</c> the second argument earns rather than the
    /// <c>NotSupportedError</c> the first one would.
    /// </remarks>
    internal static JsValue ConvertIdentifier(JsValue alg)
    {
        return alg is ObjectInstance ? alg : JsString.Create(TypeConverter.ToString(alg));
    }

    /// <summary>
    /// Normalizes <paramref name="alg"/> for <paramref name="operation"/>.
    /// </summary>
    /// <remarks>
    /// <c>AlgorithmIdentifier</c> is <c>typedef (object or DOMString)</c>, so an object stays an object and
    /// everything else is stringified — which is where a symbol raises its <c>TypeError</c>, and why
    /// <c>digest(null, …)</c> normalizes the name "null" rather than failing differently. A string is then
    /// "a new Algorithm dictionary whose name attribute is alg", which is a dictionary with no other members
    /// at all: <c>generateKey('HMAC', …)</c> is a <c>TypeError</c> for the missing required <c>hash</c>, not
    /// a key with a default hash.
    /// </remarks>
    internal static NormalizedAlgorithm Normalize(CryptoContext context, JsValue alg, CryptoOperation operation, string what)
    {
        var algorithmObject = alg as ObjectInstance;
        var algName = algorithmObject is null ? TypeConverter.ToString(alg) : ReadRequiredName(context, algorithmObject, what);

        var normalized = new NormalizedAlgorithm(MatchRegistered(context, algName, operation, what));
        ReadMembers(context, normalized, algorithmObject, operation, what);
        return normalized;
    }

    /// <summary>
    /// The <c>Algorithm</c> dictionary conversion, whose one member is <c>required DOMString name</c>.
    /// </summary>
    /// <remarks>
    /// The single <c>Get</c> is the whole of that conversion, and it may run script — a getter on
    /// <c>name</c> is called exactly once, and whatever it throws becomes the rejection. An absent member
    /// reads as <c>undefined</c>, which for a required member is the <c>TypeError</c> WebIDL raises rather
    /// than a <c>NotSupportedError</c> for the name "undefined".
    /// </remarks>
    private static string ReadRequiredName(CryptoContext context, ObjectInstance algorithm, string what)
    {
        var name = algorithm.Get(_nameKey);
        if (name.IsUndefined())
        {
            context.ThrowTypeError(what + ": Algorithm: required member name is undefined.");
        }

        return TypeConverter.ToString(name);
    }

    /// <summary>
    /// The lookup at the heart of normalization: "If registeredAlgorithms contains a key that is a
    /// case-insensitive string match for algName: Set algName to the value of the matching key. …
    /// Otherwise: Return a new NotSupportedError".
    /// </summary>
    /// <remarks>
    /// "Case-insensitive" is defined by the specification, at
    /// https://w3c.github.io/webcrypto/#case-insensitive, as <i>ASCII</i> case-insensitive — so
    /// <see cref="Ascii.EqualsIgnoreCase(ReadOnlySpan{char}, ReadOnlySpan{char})"/> is the comparison that
    /// says exactly that and nothing else, rather than a string comparison whose treatment of the letters
    /// outside ASCII has to be reasoned about. It allocates nothing. The <i>registered</i> key is returned
    /// rather than the caller's spelling, because every operation below matches its name
    /// <i>case-sensitively</i> — normalization is what makes <c>'sha-256'</c> reach SHA-256 at all.
    /// </remarks>
    private static string MatchRegistered(CryptoContext context, string algName, CryptoOperation operation, string what)
    {
        var registeredAlgorithms = RegisteredFor(operation);

        foreach (var registered in registeredAlgorithms)
        {
            if (Ascii.EqualsIgnoreCase(algName, registered))
            {
                return registered;
            }
        }

        context.ThrowNotSupportedError(
            what + ": the algorithm name '" + algName + "' is not one this engine registers for the "
            + NameOf(operation) + " operation (" + string.Join(", ", registeredAlgorithms) + ").");
        return null!;
    }

    /// <summary>
    /// "Let normalizedAlgorithm be the result of converting the ECMAScript object represented by alg to the
    /// IDL dictionary type desiredType": the members the registered (algorithm, operation) pair declares,
    /// read from the very object the caller passed, in WebIDL's own order.
    /// </summary>
    /// <remarks>
    /// <paramref name="algorithm"/> is <see langword="null"/> when the caller passed a string, which is the
    /// synthesized <c>Algorithm</c> dictionary — every member is then absent, so a required one is a
    /// <c>TypeError</c> and an optional one takes its default.
    /// </remarks>
    private static void ReadMembers(
        CryptoContext context,
        NormalizedAlgorithm normalized,
        ObjectInstance? algorithm,
        CryptoOperation operation,
        string what)
    {
        switch (normalized.Name, operation)
        {
            // HmacKeyGenParams and HmacImportParams declare the same two members, and the difference between
            // them is entirely in what the two operations do with `length`.
            case (Hmac, CryptoOperation.GenerateKey):
            case (Hmac, CryptoOperation.ImportKey):
                normalized.HashName = ReadRequiredHash(context, algorithm, what);
                normalized.Length = ReadOptionalUnsignedLong(context, algorithm, _lengthKey, what);
                break;

            // AesKeyGenParams: `required [EnforceRange] unsigned short length`.
            case (AesGcm, CryptoOperation.GenerateKey):
                normalized.Length = ReadRequiredUnsignedShort(context, algorithm, _lengthKey, what);
                break;

            // AesGcmParams.
            case (AesGcm, CryptoOperation.Encrypt):
            case (AesGcm, CryptoOperation.Decrypt):
                normalized.Iv = ReadRequiredBufferSource(context, algorithm, _ivKey, what);
                normalized.AdditionalData = ReadOptionalBufferSource(context, algorithm, _additionalDataKey, what);
                normalized.TagLength = ReadOptionalOctet(context, algorithm, _tagLengthKey, what);
                break;

            // RsaHashedKeyGenParams, which all three RSA algorithms register for generateKey. The members are
            // read in WebIDL's own order — RsaKeyGenParams' `modulusLength` and `publicExponent` first
            // because it is the less derived dictionary, then RsaHashedKeyGenParams' own `hash`.
            case (RsassaPkcs1V15, CryptoOperation.GenerateKey):
            case (RsaPss, CryptoOperation.GenerateKey):
            case (RsaOaep, CryptoOperation.GenerateKey):
                normalized.ModulusLength = ReadRequiredUnsignedLong(context, algorithm, _modulusLengthKey, what);
                normalized.PublicExponent = ReadRequiredBigInteger(context, algorithm, _publicExponentKey, what);
                normalized.HashName = ReadRequiredHash(context, algorithm, what);
                break;

            // RsaHashedImportParams, whose one member is the hash.
            case (RsassaPkcs1V15, CryptoOperation.ImportKey):
            case (RsaPss, CryptoOperation.ImportKey):
            case (RsaOaep, CryptoOperation.ImportKey):
                normalized.HashName = ReadRequiredHash(context, algorithm, what);
                break;

            // RsaPssParams: `required [EnforceRange] unsigned long saltLength`, in bytes.
            case (RsaPss, CryptoOperation.Sign):
            case (RsaPss, CryptoOperation.Verify):
                normalized.SaltLength = ReadRequiredUnsignedLong(context, algorithm, _saltLengthKey, what);
                break;

            // RsaOaepParams: `BufferSource label`, which is optional and has no default.
            case (RsaOaep, CryptoOperation.Encrypt):
            case (RsaOaep, CryptoOperation.Decrypt):
                normalized.Label = ReadOptionalBufferSource(context, algorithm, _labelKey, what);
                break;

            // EcKeyGenParams and EcKeyImportParams are the same one-member dictionary, declared twice, and
            // ECDSA and ECDH register both of them for the same two operations.
            case (Ecdsa, CryptoOperation.GenerateKey):
            case (Ecdh, CryptoOperation.GenerateKey):
            case (Ecdsa, CryptoOperation.ImportKey):
            case (Ecdh, CryptoOperation.ImportKey):
                normalized.NamedCurve = ReadRequiredDomString(context, algorithm, _namedCurveKey, what);
                break;

            // EcdsaParams, whose one member is the hash — read per sign and per verify call, because for
            // ECDSA the hash belongs to the operation and not to the key. The key carries only its curve, so
            // one P-256 key signs under all four of them.
            case (Ecdsa, CryptoOperation.Sign):
            case (Ecdsa, CryptoOperation.Verify):
                normalized.HashName = ReadRequiredHash(context, algorithm, what);
                break;

            // Every remaining pair registers `None` as its parameters, so the Algorithm dictionary — the one
            // member of which has already been read — is the whole of it. RSASSA-PKCS1-v1_5's sign and
            // verify are in that set, as is every algorithm's exportKey.
            default:
                break;
        }
    }

    /// <summary>
    /// A <c>required HashAlgorithmIdentifier hash</c> member. Normalizing it with <c>op</c> set to "digest"
    /// is what the last step of normalization says to do for a member of that type, so an unregistered hash
    /// name is a <c>NotSupportedError</c> and the value stored is the registered spelling.
    /// </summary>
    private static string ReadRequiredHash(CryptoContext context, ObjectInstance? algorithm, string what)
    {
        var hash = algorithm?.Get(_hashKey) ?? JsValue.Undefined;
        if (hash.IsUndefined())
        {
            context.ThrowTypeError(what + ": required member hash is undefined.");
        }

        return Normalize(context, hash, CryptoOperation.Digest, what).Name;
    }

    /// <summary>
    /// A <c>required DOMString</c> member, which is <c>ToString</c> and nothing else — the conversion
    /// <c>NamedCurve</c> gets, being <c>typedef DOMString</c> rather than a WebIDL enumeration.
    /// </summary>
    /// <remarks>
    /// Nothing here restricts the value, so a curve name this engine does not implement arrives at the
    /// operation intact and earns the <c>NotSupportedError</c> the operation's own first step gives it. A
    /// WebIDL enumeration would instead have been a <c>TypeError</c> raised before the method body ran, which
    /// is what <c>KeyFormat</c> is and what this deliberately is not.
    /// </remarks>
    private static string ReadRequiredDomString(CryptoContext context, ObjectInstance? algorithm, JsString key, string what)
    {
        var value = algorithm?.Get(key) ?? JsValue.Undefined;
        if (value.IsUndefined())
        {
            context.ThrowTypeError(what + ": required member " + key + " is undefined.");
        }

        return TypeConverter.ToString(value);
    }

    private static byte[] ReadRequiredBufferSource(CryptoContext context, ObjectInstance? algorithm, JsString key, string what)
    {
        var value = algorithm?.Get(key) ?? JsValue.Undefined;
        if (value.IsUndefined())
        {
            context.ThrowTypeError(what + ": required member " + key + " is undefined.");
        }

        return CopyBufferSource(context, value, key, what);
    }

    private static byte[]? ReadOptionalBufferSource(CryptoContext context, ObjectInstance? algorithm, JsString key, string what)
    {
        var value = algorithm?.Get(key) ?? JsValue.Undefined;
        return value.IsUndefined() ? null : CopyBufferSource(context, value, key, what);
    }

    /// <summary>
    /// "If member is of the type BufferSource and is present: set the dictionary member … to the result of
    /// getting a copy of the bytes held by idlValue."
    /// </summary>
    /// <remarks>
    /// The copy is real here, unlike the one the message of a digest is read through: an <c>iv</c> is held
    /// across the rest of normalization, which can run a script's getter, so a window onto the engine's own
    /// backing array could be mutated — or detached — between being read and being used.
    /// </remarks>
    private static byte[] CopyBufferSource(CryptoContext context, JsValue value, JsString key, string what)
    {
        if (!BufferSource.TryGetBytes(value, out var bytes))
        {
            context.ThrowTypeError(what + ": member " + key + " is not of type 'BufferSource'.");
        }

        if (IsSharedBufferSource(value))
        {
            context.ThrowTypeError(what + ": member " + key + " is backed by a SharedArrayBuffer, which this operation does not accept.");
        }

        return bytes.ToArray();
    }

    /// <summary>
    /// A view onto a <c>SharedArrayBuffer</c>, and a <c>SharedArrayBuffer</c> itself, are refused wherever the
    /// IDL says <c>BufferSource</c> rather than <c>AllowSharedBufferSource</c>: WebIDL refuses a shared buffer
    /// for any type not carrying <c>[AllowShared]</c>, https://webidl.spec.whatwg.org/#es-buffer-source-types.
    /// </summary>
    internal static bool IsSharedBufferSource(JsValue value)
    {
        var buffer = value switch
        {
            JsTypedArray typedArray => typedArray._viewedArrayBuffer,
            JsDataView dataView => dataView._viewedArrayBuffer,
            _ => value as JsArrayBuffer,
        };

        return buffer is not null && buffer.IsSharedArrayBuffer;
    }

    /// <summary>
    /// An optional <c>[EnforceRange] unsigned long</c> member. A member whose value is <c>undefined</c> is
    /// "not present" — https://webidl.spec.whatwg.org/#es-dictionary — which is why an explicit
    /// <c>{ length: undefined }</c> means the same as omitting it.
    /// </summary>
    private static uint? ReadOptionalUnsignedLong(CryptoContext context, ObjectInstance? algorithm, JsString key, string what)
    {
        var value = algorithm?.Get(key) ?? JsValue.Undefined;
        if (value.IsUndefined())
        {
            return null;
        }

        return (uint) EnforceRange(context, value, key, what, uint.MaxValue);
    }

    private static uint ReadRequiredUnsignedLong(CryptoContext context, ObjectInstance? algorithm, JsString key, string what)
    {
        var value = algorithm?.Get(key) ?? JsValue.Undefined;
        if (value.IsUndefined())
        {
            context.ThrowTypeError(what + ": required member " + key + " is undefined.");
        }

        return (uint) EnforceRange(context, value, key, what, uint.MaxValue);
    }

    /// <summary>
    /// A <c>required BigInteger</c> member, which is <c>typedef Uint8Array BigInteger</c> —
    /// https://w3c.github.io/webcrypto/#big-integer. WebIDL's conversion for a specific typed array type
    /// accepts that type and nothing else (https://webidl.spec.whatwg.org/#es-buffer-source-types), so an
    /// <c>ArrayBuffer</c>, a <c>DataView</c> or an <c>Int8Array</c> is a <c>TypeError</c> here where a
    /// <c>BufferSource</c> member would have taken all three.
    /// </summary>
    /// <remarks>
    /// The copy is this engine's own rather than the normalization step's: that step names members "of the
    /// type BufferSource", which a <c>Uint8Array</c> typedef is not, so nothing in the specification requires
    /// one here. The hazard it exists for is the same one, though — normalization goes on to read a
    /// <c>hash</c> member, which may run a script's getter with this very array in scope — so the bytes are
    /// taken now.
    /// </remarks>
    private static byte[] ReadRequiredBigInteger(CryptoContext context, ObjectInstance? algorithm, JsString key, string what)
    {
        var value = algorithm?.Get(key) ?? JsValue.Undefined;
        if (value.IsUndefined())
        {
            context.ThrowTypeError(what + ": required member " + key + " is undefined.");
        }

        if (value is not JsTypedArray { _arrayElementType: TypedArrayElementType.Uint8 })
        {
            context.ThrowTypeError(what + ": member " + key + " is not of type 'Uint8Array'.");
        }

        if (IsSharedBufferSource(value))
        {
            context.ThrowTypeError(what + ": member " + key + " is backed by a SharedArrayBuffer, which this operation does not accept.");
        }

        BufferSource.TryGetBytes(value, out var bytes);
        return bytes.ToArray();
    }

    private static uint ReadRequiredUnsignedShort(CryptoContext context, ObjectInstance? algorithm, JsString key, string what)
    {
        var value = algorithm?.Get(key) ?? JsValue.Undefined;
        if (value.IsUndefined())
        {
            context.ThrowTypeError(what + ": required member " + key + " is undefined.");
        }

        return (uint) EnforceRange(context, value, key, what, ushort.MaxValue);
    }

    private static int? ReadOptionalOctet(CryptoContext context, ObjectInstance? algorithm, JsString key, string what)
    {
        var value = algorithm?.Get(key) ?? JsValue.Undefined;
        if (value.IsUndefined())
        {
            return null;
        }

        return (int) EnforceRange(context, value, key, what, byte.MaxValue);
    }

    /// <summary>
    /// The <c>[EnforceRange]</c> integer conversion, https://webidl.spec.whatwg.org/#abstract-opdef-converttoint:
    /// a value that is not a finite number, or whose truncated value falls outside the type, is a
    /// <c>TypeError</c> rather than a wrap or a clamp.
    /// </summary>
    private static double EnforceRange(CryptoContext context, JsValue value, JsString key, string what, double max)
    {
        var number = TypeConverter.ToNumber(value);
        if (!double.IsFinite(number))
        {
            context.ThrowTypeError(what + ": member " + key + " is not a finite number.");
        }

        var integer = double.Truncate(number);
        if (integer < 0 || integer > max)
        {
            context.ThrowTypeError(what + ": member " + key + " is outside the range [0, " + max + "].");
        }

        return integer;
    }
}
#endif
