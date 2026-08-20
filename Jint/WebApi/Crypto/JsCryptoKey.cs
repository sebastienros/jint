#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The <c>[[algorithm]]</c> internal slot of a <see cref="JsCryptoKey"/>: a <c>KeyAlgorithm</c>, or one of
/// the two dictionaries derived from it that the algorithms here produce.
/// <para>
/// https://w3c.github.io/webcrypto/#key-algorithm-dictionary
/// </para>
/// </summary>
/// <param name="Name">The recognized algorithm name — <c>HMAC</c> or <c>AES-GCM</c>.</param>
/// <param name="Length">The length of the key in bits, which both dictionaries carry.</param>
/// <param name="HashName">
/// The <c>hash</c> member of an <c>HmacKeyAlgorithm</c>, or <see langword="null"/> for an
/// <c>AesKeyAlgorithm</c>, which has none.
/// </param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct CryptoKeyAlgorithm(string Name, uint Length, string? HashName);

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

    private readonly byte[] _handle;

    private ObjectInstance? _algorithmCached;
    private JsArray? _usagesCached;

    internal JsCryptoKey(
        Engine engine,
        byte[] handle,
        CryptoKeyAlgorithm algorithm,
        bool extractable,
        KeyUsage usages) : base(engine, ObjectClass.Object)
    {
        _handle = handle;
        Algorithm = algorithm;
        Extractable = extractable;
        Usages = usages;
    }

    /// <summary>
    /// The <c>[[handle]]</c> internal slot. Never handed out: the two callers that need the bytes —
    /// <c>sign</c>/<c>verify</c> and <c>encrypt</c>/<c>decrypt</c> — read them and produce something else,
    /// and <c>exportKey</c> copies.
    /// </summary>
    internal ReadOnlySpan<byte> Handle => _handle;

    /// <summary>
    /// The <c>[[type]]</c> internal slot. Every key this engine can build is a symmetric one, so it is always
    /// <c>"secret"</c>; the attribute is nevertheless read from here rather than hard-coded at the accessor,
    /// so that a public or private key added later cannot silently answer wrongly. It is not called
    /// <c>Type</c> because <see cref="JsValue.Type"/> already is.
    /// </summary>
    internal string KeyType { get; } = "secret";

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
        var length = JsNumber.Create(Algorithm.Length);

        if (Algorithm.HashName is null)
        {
            _algorithmCached = JsObject.Create(_engine, _aesKeyAlgorithmLayout, [name, length]);
        }
        else
        {
            var hash = JsObject.Create(_engine, _keyAlgorithmLayout, [JsString.Create(Algorithm.HashName)]);
            _algorithmCached = JsObject.Create(_engine, _hmacKeyAlgorithmLayout, [name, hash, length]);
        }

        return _algorithmCached;
    }

    /// <summary>
    /// The cached ECMAScript object for the <c>[[usages]]</c> slot.
    /// </summary>
    internal JsArray UsagesObject() => _usagesCached ??= KeyUsages.ToArray(_engine, Usages);
}
#endif
