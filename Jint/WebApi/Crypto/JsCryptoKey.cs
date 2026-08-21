#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Native.TypedArray;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The <c>[[type]]</c> internal slot's three values — https://w3c.github.io/webcrypto/#dfn-KeyType. They are
/// the strings the <c>type</c> attribute answers with, so they are spelled once here rather than at each
/// algorithm that decides one.
/// </summary>
internal static class CryptoKeyTypes
{
    internal const string Secret = "secret";
    internal const string Public = "public";
    internal const string Private = "private";
}

/// <summary>
/// The <c>[[algorithm]]</c> internal slot of a <see cref="JsCryptoKey"/>: a <c>KeyAlgorithm</c>, or one of
/// the dictionaries derived from it that the algorithms here produce.
/// <para>
/// https://w3c.github.io/webcrypto/#key-algorithm-dictionary
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// One struct rather than a type per dictionary, for the reason <see cref="NormalizedAlgorithm"/> is one
/// class: the union of the members across the dictionaries this engine produces is six fields, and which
/// dictionary a value describes is decided by which of them are filled in.
/// </para>
/// <para>
/// <b>Two of the six are discriminators</b>, and they are tested in this order, because the dictionaries
/// they name carry none of the other members at all:
/// </para>
/// <list type="number">
/// <item>
/// <see cref="NamedCurve"/> non-null means an <c>EcKeyAlgorithm</c>: <see cref="Length"/>,
/// <see cref="HashName"/>, <see cref="ModulusLength"/> and <see cref="PublicExponent"/> are all unused, an
/// EC key being described by its curve and nothing else. It is tested first precisely because it is the one
/// dictionary that fills in <i>only</i> its own member — an <c>AesKeyAlgorithm</c> is what a null
/// <see cref="HashName"/> would otherwise be read as.
/// </item>
/// <item>
/// <see cref="PublicExponent"/> non-null means an <c>RsaHashedKeyAlgorithm</c>: <see cref="Length"/> is
/// meaningless and <see cref="ModulusLength"/> is what describes the key.
/// </item>
/// </list>
/// <para>
/// With neither set the key is symmetric, and a null <see cref="HashName"/> then separates an
/// <c>AesKeyAlgorithm</c> from an <c>HmacKeyAlgorithm</c>.
/// </para>
/// </remarks>
/// <param name="Name">
/// The recognized algorithm name — <c>HMAC</c>, <c>AES-GCM</c>, <c>RSASSA-PKCS1-v1_5</c>, <c>RSA-PSS</c>,
/// <c>RSA-OAEP</c>, <c>ECDSA</c> or <c>ECDH</c>.
/// </param>
/// <param name="Length">
/// The length of a symmetric key in bits, which <c>HmacKeyAlgorithm</c> and <c>AesKeyAlgorithm</c> both
/// carry. It is zero and unused for an <c>RsaHashedKeyAlgorithm</c>, which has no such member.
/// </param>
/// <param name="HashName">
/// The <c>hash</c> member of an <c>HmacKeyAlgorithm</c> or an <c>RsaHashedKeyAlgorithm</c>, or
/// <see langword="null"/> for an <c>AesKeyAlgorithm</c>, which has none.
/// </param>
/// <param name="ModulusLength">
/// The <c>modulusLength</c> member of an <c>RsaKeyAlgorithm</c>, in bits.
/// </param>
/// <param name="PublicExponent">
/// The <c>publicExponent</c> member of an <c>RsaKeyAlgorithm</c>, held as the big-endian magnitude a
/// <c>BigInteger</c> is (https://w3c.github.io/webcrypto/#big-integer) and surfaced to script as a
/// <c>Uint8Array</c>. <see langword="null"/> for every symmetric algorithm.
/// </param>
/// <param name="NamedCurve">
/// The <c>namedCurve</c> member of an <c>EcKeyAlgorithm</c> — <c>P-256</c>, <c>P-384</c> or <c>P-521</c>, in
/// the registered spelling. <see langword="null"/> for every algorithm that is not an elliptic-curve one.
/// </param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct CryptoKeyAlgorithm(
    string Name,
    uint Length,
    string? HashName,
    uint ModulusLength = 0,
    byte[]? PublicExponent = null,
    string? NamedCurve = null);

/// <summary>
/// The two halves of a generated asymmetric key pair, with the usages each of them ends up carrying.
/// </summary>
/// <remarks>
/// One <see cref="CryptoKeyAlgorithm"/> for both halves, because every generate operation that produces a
/// pair builds exactly one key algorithm dictionary and sets the <c>[[algorithm]]</c> slot of both keys to
/// it — one <c>RsaHashedKeyAlgorithm</c> for the RSA family, one <c>EcKeyAlgorithm</c> for ECDSA and ECDH.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct AsymmetricKeyPairMaterial(
    byte[] PublicHandle,
    KeyUsage PublicUsages,
    byte[] PrivateHandle,
    KeyUsage PrivateUsages,
    CryptoKeyAlgorithm Algorithm);

/// <summary>
/// A <c>CryptoKey</c> — "an opaque reference to keying material".
/// <para>
/// https://w3c.github.io/webcrypto/#cryptokey-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>The key material never reaches script except through <c>exportKey</c> on an extractable key.</b> The
/// <c>[[handle]]</c> internal slot is the private <see cref="_handle"/> field: the object has no own
/// properties at all (<c>Object.getOwnPropertyNames(key)</c> is empty, as in a browser), the four attributes
/// live on <see cref="CryptoKeyPrototype"/> and none of them can reach the bytes, and <c>exportKey</c> hands
/// out a fresh copy so that a script writing into what it exported cannot write into the key. A
/// non-extractable key has no door at all: <c>exportKey</c> refuses it with an <c>InvalidAccessError</c>
/// before asking the algorithm for anything.
/// </para>
/// <para>
/// <c>algorithm</c> and <c>usages</c> are the specification's <i>cached</i> ECMAScript objects —
/// https://w3c.github.io/webcrypto/#dfn-cached-ecmascript-object: built on the first read and returned by
/// reference forever after, so <c>key.algorithm === key.algorithm</c> holds. They are ordinary objects, not
/// frozen ones, which is what WebIDL's conversion of a dictionary and of a sequence produces; a script that
/// mutates one changes only its own copy of the answer, because everything the engine decides is decided from
/// <see cref="Algorithm"/> and <see cref="Usages"/>, which are CLR state a script cannot reach.
/// </para>
/// </remarks>
internal sealed class JsCryptoKey : ObjectInstance
{
    /// <summary>
    /// <c>KeyAlgorithm</c>, https://w3c.github.io/webcrypto/#key-algorithm-dictionary — the one member every
    /// key algorithm dictionary inherits. It is the whole of the <c>hash</c> member of an
    /// <c>HmacKeyAlgorithm</c>.
    /// </summary>
    private static readonly JsObjectLayout _keyAlgorithmLayout = JsObjectLayout.CreateBuilder()
        .Add("name")
        .Build();

    /// <summary>
    /// <c>HmacKeyAlgorithm</c>, https://w3c.github.io/webcrypto/#HmacKeyAlgorithm-dictionary. WebIDL converts
    /// a dictionary to an object by walking the inherited dictionaries from least to most derived and each
    /// dictionary's own members in lexicographical order — https://webidl.spec.whatwg.org/#es-dictionary — so
    /// <c>name</c> (from <c>KeyAlgorithm</c>) comes first and then <c>hash</c> before <c>length</c>.
    /// </summary>
    private static readonly JsObjectLayout _hmacKeyAlgorithmLayout = JsObjectLayout.CreateBuilder()
        .Add("name")
        .Add("hash")
        .Add("length")
        .Build();

    /// <summary>
    /// <c>AesKeyAlgorithm</c>, https://w3c.github.io/webcrypto/#AesKeyAlgorithm-dictionary.
    /// </summary>
    private static readonly JsObjectLayout _aesKeyAlgorithmLayout = JsObjectLayout.CreateBuilder()
        .Add("name")
        .Add("length")
        .Build();

    /// <summary>
    /// <c>RsaHashedKeyAlgorithm</c>, https://w3c.github.io/webcrypto/#RsaHashedKeyAlgorithm-dictionary. The
    /// member order is the one WebIDL converts a dictionary in — the inherited dictionaries from least to
    /// most derived, each one's own members lexicographically — so <c>name</c> comes from
    /// <c>KeyAlgorithm</c>, then <c>modulusLength</c> and <c>publicExponent</c> from <c>RsaKeyAlgorithm</c>,
    /// and <c>hash</c> last because it is the derived dictionary's own.
    /// </summary>
    private static readonly JsObjectLayout _rsaHashedKeyAlgorithmLayout = JsObjectLayout.CreateBuilder()
        .Add("name")
        .Add("modulusLength")
        .Add("publicExponent")
        .Add("hash")
        .Build();

    /// <summary>
    /// <c>EcKeyAlgorithm</c>, https://w3c.github.io/webcrypto/#EcKeyAlgorithm-dictionary, whose one own
    /// member is <c>namedCurve</c> — so <c>name</c> comes first, from <c>KeyAlgorithm</c>. There is no
    /// <c>hash</c>: an ECDSA key's hash belongs to each sign and verify call, not to the key.
    /// </summary>
    private static readonly JsObjectLayout _ecKeyAlgorithmLayout = JsObjectLayout.CreateBuilder()
        .Add("name")
        .Add("namedCurve")
        .Build();

    private readonly byte[] _handle;

    private ObjectInstance? _algorithmCached;
    private JsArray? _usagesCached;

    internal JsCryptoKey(
        Engine engine,
        byte[] handle,
        string keyType,
        CryptoKeyAlgorithm algorithm,
        bool extractable,
        KeyUsage usages) : base(engine, ObjectClass.Object)
    {
        _handle = handle;
        KeyType = keyType;
        Algorithm = algorithm;
        Extractable = extractable;
        Usages = usages;
    }

    /// <summary>
    /// The <c>[[handle]]</c> internal slot. Never handed out: the callers that need the bytes —
    /// <c>sign</c>/<c>verify</c> and <c>encrypt</c>/<c>decrypt</c> — read them and produce something else,
    /// and <c>exportKey</c> copies.
    /// </summary>
    /// <remarks>
    /// What the bytes <i>are</i> is the algorithm's business: for a symmetric key they are the key material
    /// itself, and for every asymmetric key — RSA, ECDSA and ECDH alike — they are the DER encoding of a
    /// <c>SubjectPublicKeyInfo</c> or of a <c>PrivateKeyInfo</c>. Holding a serialized form rather than a
    /// live <see cref="System.Security.Cryptography.AsymmetricAlgorithm"/> is deliberate: one is
    /// <see cref="IDisposable"/> and would give a script-reachable object a native lifetime the garbage
    /// collector decides, where a byte array has none. Each operation rehydrates one, uses it and disposes it
    /// inside a single <c>using</c>, which costs a key import per call and buys a <c>CryptoKey</c> that is
    /// exactly as ordinary an object as an HMAC key is.
    /// </remarks>
    internal ReadOnlySpan<byte> Handle => _handle;

    /// <summary>
    /// The <c>[[type]]</c> internal slot — <c>"secret"</c>, <c>"public"</c> or <c>"private"</c>, one of
    /// <see cref="CryptoKeyTypes"/>. It is supplied by whichever algorithm made the key, because that is
    /// where the specification decides it: every step that sets it names the type it is setting. It is not
    /// called <c>Type</c> because <see cref="JsValue.Type"/> already is.
    /// </summary>
    internal string KeyType { get; }

    /// <summary>The <c>[[extractable]]</c> internal slot.</summary>
    internal bool Extractable { get; }

    /// <summary>The <c>[[usages]]</c> internal slot, already the "normalized value" of what was requested.</summary>
    internal KeyUsage Usages { get; }

    /// <summary>The <c>[[algorithm]]</c> internal slot.</summary>
    internal CryptoKeyAlgorithm Algorithm { get; }

    /// <summary>Whether the key permits <paramref name="usage"/> — the check every operation makes.</summary>
    internal bool Allows(KeyUsage usage) => (Usages & usage) != KeyUsage.None;

    /// <summary>
    /// The cached ECMAScript object for the <c>[[algorithm]]</c> slot.
    /// </summary>
    internal ObjectInstance AlgorithmObject()
    {
        if (_algorithmCached is not null)
        {
            return _algorithmCached;
        }

        var name = JsString.Create(Algorithm.Name);

        // The discriminators, in the order the remarks on CryptoKeyAlgorithm give them.
        if (Algorithm.NamedCurve is { } namedCurve)
        {
            _algorithmCached = JsObject.Create(_engine, _ecKeyAlgorithmLayout, [name, JsString.Create(namedCurve)]);
        }
        else if (Algorithm.PublicExponent is { } publicExponent)
        {
            var rsaHash = JsObject.Create(_engine, _keyAlgorithmLayout, [JsString.Create(Algorithm.HashName!)]);
            _algorithmCached = JsObject.Create(
                _engine,
                _rsaHashedKeyAlgorithmLayout,
                [name, JsNumber.Create(Algorithm.ModulusLength), CreateBigInteger(publicExponent), rsaHash]);
        }
        else if (Algorithm.HashName is null)
        {
            _algorithmCached = JsObject.Create(_engine, _aesKeyAlgorithmLayout, [name, JsNumber.Create(Algorithm.Length)]);
        }
        else
        {
            var hash = JsObject.Create(_engine, _keyAlgorithmLayout, [JsString.Create(Algorithm.HashName)]);
            _algorithmCached = JsObject.Create(_engine, _hmacKeyAlgorithmLayout, [name, hash, JsNumber.Create(Algorithm.Length)]);
        }

        return _algorithmCached;
    }

    /// <summary>
    /// A <c>BigInteger</c> — https://w3c.github.io/webcrypto/#big-integer, which is
    /// <c>typedef Uint8Array BigInteger</c> holding "an arbitrary magnitude unsigned integer in big-endian
    /// order".
    /// </summary>
    /// <remarks>
    /// The bytes are copied into the array rather than shared with the key's own: the algorithm object is an
    /// ordinary object a script may write into, and what the engine decides is decided from
    /// <see cref="Algorithm"/> alone. The realm is the engine's active one, which is the very realm
    /// <c>JsObject.Create</c> anchors the surrounding object to.
    /// </remarks>
    private JsTypedArray CreateBigInteger(ReadOnlySpan<byte> magnitude)
    {
        var uint8Array = _engine.Realm.Intrinsics.Uint8Array;
        var array = uint8Array.AllocateTypedArray(uint8Array, (uint) magnitude.Length);
        TypedArrayConstructor.FillTypedArrayInstance(array, magnitude);
        return array;
    }

    /// <summary>
    /// The cached ECMAScript object for the <c>[[usages]]</c> slot.
    /// </summary>
    internal JsArray UsagesObject() => _usagesCached ??= KeyUsages.ToArray(_engine, Usages);
}
#endif
