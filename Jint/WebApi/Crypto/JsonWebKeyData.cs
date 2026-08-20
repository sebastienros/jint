#if NET8_0_OR_GREATER
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The members of a <c>JsonWebKey</c> dictionary that a symmetric key uses —
/// https://w3c.github.io/webcrypto/#JsonWebKey-dictionary, whose fields are those of
/// https://www.rfc-editor.org/rfc/rfc7517 and https://www.rfc-editor.org/rfc/rfc7518.
/// </summary>
/// <remarks>
/// <para>
/// A documented simplification: the dictionary declares eighteen members and this reads the six an
/// <c>oct</c> key can be described by (<c>alg</c>, <c>ext</c>, <c>k</c>, <c>key_ops</c>, <c>kty</c>,
/// <c>use</c>). A getter on one of the twelve asymmetric-key members — <c>crv</c>, <c>n</c>, <c>d</c> and the
/// rest — is therefore not invoked, where a browser's WebIDL conversion would invoke it and could raise a
/// <c>TypeError</c> from converting its value. Nothing else can observe the difference: the specification's
/// own note says that "fields that are not explicitly referred to in the key import procedures for an
/// algorithm are ignored".
/// </para>
/// <para>
/// The six that <i>are</i> read are read in lexicographical order, which is the order WebIDL converts a
/// dictionary's members in, so a JWK built out of getters sees them run in the order a browser runs them.
/// </para>
/// </remarks>
internal sealed class JsonWebKeyData
{
    private static readonly JsString _algKey = new("alg");
    private static readonly JsString _extKey = new("ext");
    private static readonly JsString _kKey = new("k");
    private static readonly JsString _keyOpsKey = new("key_ops");
    private static readonly JsString _ktyKey = new("kty");
    private static readonly JsString _useKey = new("use");

    /// <summary>
    /// The shape <c>exportKey("jwk")</c> answers with. Both algorithms set every one of the five, and WebIDL
    /// converts a dictionary to an object in lexicographical order of its members —
    /// https://webidl.spec.whatwg.org/#es-dictionary — so one layout describes both and
    /// <c>Object.keys(jwk)</c> is stable.
    /// </summary>
    private static readonly JsObjectLayout _exportLayout = JsObjectLayout.CreateBuilder()
        .Add("alg")
        .Add("ext")
        .Add("k")
        .Add("key_ops")
        .Add("kty")
        .Build();

    /// <summary>The <c>alg</c> field, or <see langword="null"/> when it is not present.</summary>
    internal string? Alg { get; private set; }

    /// <summary>The <c>ext</c> field, or <see langword="null"/> when it is not present.</summary>
    internal bool? Ext { get; private set; }

    /// <summary>The <c>k</c> field, or <see langword="null"/> when it is not present.</summary>
    internal string? K { get; private set; }

    /// <summary>The <c>key_ops</c> field, or <see langword="null"/> when it is not present.</summary>
    internal List<string>? KeyOps { get; private set; }

    /// <summary>The <c>kty</c> field, or <see langword="null"/> when it is not present.</summary>
    internal string? Kty { get; private set; }

    /// <summary>The <c>use</c> field, or <see langword="null"/> when it is not present.</summary>
    internal string? Use { get; private set; }

    /// <summary>
    /// Converts the ECMAScript object a script passed as <c>keyData</c> to the dictionary.
    /// </summary>
    internal static JsonWebKeyData Read(CryptoContext context, ObjectInstance source, string what)
    {
        var jwk = new JsonWebKeyData
        {
            Alg = ReadString(source, _algKey),
            Ext = ReadBoolean(source, _extKey),
            K = ReadString(source, _kKey),
            KeyOps = ReadStringSequence(context, source, what),
            Kty = ReadString(source, _ktyKey),
            Use = ReadString(source, _useKey),
        };

        return jwk;
    }

    private static string? ReadString(ObjectInstance source, JsString key)
    {
        var value = source.Get(key);
        return value.IsUndefined() ? null : TypeConverter.ToString(value);
    }

    private static bool? ReadBoolean(ObjectInstance source, JsString key)
    {
        var value = source.Get(key);
        return value.IsUndefined() ? null : TypeConverter.ToBoolean(value);
    }

    /// <summary>
    /// <c>sequence&lt;DOMString&gt; key_ops</c>: iterated with the iterator protocol, so a value that is not
    /// iterable is the <c>TypeError</c> WebIDL raises rather than a <c>DataError</c>.
    /// </summary>
    private static List<string>? ReadStringSequence(CryptoContext context, ObjectInstance source, string what)
    {
        var value = source.Get(_keyOpsKey);
        if (value.IsUndefined())
        {
            return null;
        }

        var result = new List<string>();
        var iterator = value.GetIterator(context.Realm);

        try
        {
            while (iterator.TryIteratorStepValue(out var element))
            {
                result.Add(TypeConverter.ToString(element));
            }
        }
        catch
        {
            iterator.CloseIfNotDone(CompletionType.Throw);
            throw;
        }

        return result;
    }

    /// <summary>
    /// The first three JWK steps both algorithms share: the key type must be <c>oct</c>, the key must meet
    /// "the requirements of Section 6.4 of JSON Web Algorithms" — which for an <c>oct</c> key is that
    /// <c>k</c> is present, https://www.rfc-editor.org/rfc/rfc7518#section-6.4.1 saying "This member MUST be
    /// present" — and <c>k</c> is then decoded.
    /// </summary>
    internal byte[] RequireOctAndDecodeKey(CryptoContext context, string what)
    {
        if (!string.Equals(Kty, "oct", StringComparison.Ordinal))
        {
            context.ThrowDataError(what + ": the kty field of the JSON Web Key is " + Describe(Kty) + " rather than 'oct'.");
        }

        if (K is null)
        {
            context.ThrowDataError(what + ": the JSON Web Key has no k field, which Section 6.4.1 of JSON Web Algorithms requires for a key of type 'oct'.");
        }

        return DecodeBase64Url(context, K, what);
    }

    /// <summary>
    /// "Let data be the byte sequence obtained by decoding the k field of jwk" — decoded as the base64url
    /// encoding of https://www.rfc-editor.org/rfc/rfc7515#section-2, which is the alphabet <c>A-Za-z0-9-_</c>
    /// "with all trailing '=' characters omitted".
    /// </summary>
    /// <remarks>
    /// The alphabet is checked here rather than left to the decoder: <see cref="WebEncoders"/> maps
    /// <c>-</c> and <c>_</c> back to the standard alphabet and then hands the whole string to
    /// <see cref="Convert"/>, which accepts <c>+</c>, <c>/</c>, padding and whitespace — all of which are
    /// exactly what a base64url string may not contain. Accepting them would mean importing a key from a
    /// document that no other implementation would read.
    /// </remarks>
    private static byte[] DecodeBase64Url(CryptoContext context, string k, string what)
    {
        foreach (var c in k)
        {
            var valid = char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_';
            if (!valid)
            {
                context.ThrowDataError(what + ": the k field of the JSON Web Key contains '" + c + "', which is not a base64url character.");
            }
        }

        // A base64url string of length 4n+1 encodes a fractional byte and cannot be decoded at all.
        if (k.Length % 4 == 1)
        {
            context.ThrowDataError(what + ": the k field of the JSON Web Key has a length of " + k.Length + ", which no base64url encoding has.");
        }

        try
        {
            return WebEncoders.Base64UrlDecode(k.AsSpan());
        }
        catch (FormatException)
        {
            context.ThrowDataError(what + ": the k field of the JSON Web Key is not a valid base64url encoding.");
            return null!;
        }
    }

    /// <summary>
    /// The last three JWK steps both algorithms share, in the order they appear in both: <c>use</c>,
    /// <c>key_ops</c> and <c>ext</c>. <c>expectedUse</c> is "sig" for a signing key and "enc" for an
    /// encryption key, which is the only part of these three steps that differs between the two.
    /// </summary>
    internal void ValidateUseKeyOpsAndExt(
        CryptoContext context,
        KeyUsage usages,
        bool extractable,
        string expectedUse,
        string what)
    {
        // "If usages is non-empty and the use field of jwk is present and is not <expectedUse>, then throw a
        // DataError." An empty usages list makes the field irrelevant, because nothing is being asked of the
        // key.
        if (usages != KeyUsage.None && Use is not null && !string.Equals(Use, expectedUse, StringComparison.Ordinal))
        {
            context.ThrowDataError(
                what + ": the use field of the JSON Web Key is '" + Use + "' rather than '" + expectedUse + "', so it cannot be imported with these usages.");
        }

        // "If the key_ops field of jwk is present, and is invalid according to the requirements of JSON Web
        // Key or does not contain all of the specified usages values, then throw a DataError."
        if (KeyOps is not null)
        {
            // https://www.rfc-editor.org/rfc/rfc7517#section-4.3: "Duplicate key operation values MUST NOT be
            // present in the array." Nothing else about the array is checkable here — an unrecognized
            // operation name is allowed to be there, it simply cannot match a usage.
            for (var i = 0; i < KeyOps.Count; i++)
            {
                for (var j = i + 1; j < KeyOps.Count; j++)
                {
                    if (string.Equals(KeyOps[i], KeyOps[j], StringComparison.Ordinal))
                    {
                        context.ThrowDataError(
                            what + ": the key_ops field of the JSON Web Key lists '" + KeyOps[i] + "' more than once, which JSON Web Key forbids.");
                    }
                }
            }

            var missing = MissingFromKeyOps(usages);
            if (missing != KeyUsage.None)
            {
                context.ThrowDataError(
                    what + ": the key_ops field of the JSON Web Key does not contain the requested usage(s) " + KeyUsages.Describe(missing) + ".");
            }
        }

        // "If the ext field of jwk is present and has the value false and extractable is true, then throw a
        // DataError." A key marked non-extractable cannot be imported as an extractable one, which is the one
        // promise a JWK can make about its own future.
        if (Ext == false && extractable)
        {
            context.ThrowDataError(what + ": the JSON Web Key has ext: false, so it cannot be imported as an extractable key.");
        }
    }

    private KeyUsage MissingFromKeyOps(KeyUsage usages)
    {
        var present = KeyUsage.None;
        foreach (var name in KeyOps!)
        {
            if (KeyUsages.TryParse(name, out var usage))
            {
                present |= usage;
            }
        }

        return usages & ~present;
    }

    /// <summary>
    /// The object <c>exportKey("jwk")</c> resolves with: the five fields both algorithms set, with <c>k</c>
    /// carrying the key material "encoded according to Section 6.4 of JSON Web Algorithms" — base64url with
    /// no padding.
    /// </summary>
    internal static JsObject CreateExport(
        Engine engine,
        ReadOnlySpan<byte> keyMaterial,
        string alg,
        KeyUsage usages,
        bool extractable)
    {
        return JsObject.Create(
            engine,
            _exportLayout,
            [
                JsString.Create(alg),
                extractable ? JsBoolean.True : JsBoolean.False,
                JsString.Create(WebEncoders.Base64UrlEncode(keyMaterial, omitPadding: true)),
                KeyUsages.ToArray(engine, usages),
                JsString.Create("oct"),
            ]);
    }

    private static string Describe(string? value) => value is null ? "absent" : "'" + value + "'";
}
#endif
