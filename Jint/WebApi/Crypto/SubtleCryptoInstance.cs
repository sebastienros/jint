#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Jint.Native;
using Jint.Native.ArrayBuffer;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.DomException;
using Jint.WebApi.Encoding;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The object <c>crypto.subtle</c> answers with — an instance of the <c>SubtleCrypto</c> interface.
/// <para>
/// https://w3c.github.io/webcrypto/#subtlecrypto-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b><c>digest</c> is the whole of the interface here.</b> Every other operation the IDL declares —
/// <c>encrypt</c>, <c>decrypt</c>, <c>sign</c>, <c>verify</c>, <c>generateKey</c>, <c>deriveKey</c>,
/// <c>deriveBits</c>, <c>importKey</c>, <c>exportKey</c>, <c>wrapKey</c> and <c>unwrapKey</c> — is
/// <b>absent</b> rather than present-and-throwing, and <c>CryptoKey</c> does not exist at all. Digest is the
/// one operation that needs no key material, no key store and no <c>CryptoKey</c> object, which is what makes
/// it shippable on its own; a library that checks <c>typeof crypto.subtle.sign === 'function'</c> before
/// reaching for it gets the truthful answer and takes its fallback path, exactly as it does for
/// <c>crypto.subtle</c> itself in an engine without the crypto feature.
/// </para>
/// <para>
/// <b>Nothing here ever throws to its caller.</b> WebIDL turns an exception out of a promise-returning
/// operation into a rejection of that promise — https://webidl.spec.whatwg.org/#dfn-create-operation-function:
/// "And then, if an exception E was thrown: If op has a return type that is a promise type, then return
/// ! Call(%Promise.reject%, %Promise%, «E»)". That wrapper encloses the brand check and the argument
/// conversions too, not merely the method's own steps, so a receiver that is not a <c>SubtleCrypto</c> and a
/// <c>data</c> that is not a buffer source are both <i>rejections</i>. The implementation gets that for free
/// by writing the steps as throws and catching them, which is also how the failures a script raises inside
/// (a <c>name</c> getter that throws) reach the same place. What is caught is exactly the engine's
/// script-visible error exceptions; an execution constraint or a cancellation still erupts, because a
/// constraint that became a rejection would no longer bound anything.
/// </para>
/// <para>
/// The promise is already resolved when it is handed back: hashing a byte sequence already in memory is
/// synchronous CPU work, so there is nothing for an event-loop turn to wait for. Step 7's "perform the
/// remaining steps in parallel" exists so that a browser's main thread is not blocked by a slow key
/// operation, and observing the difference needs a second thread to mutate something in between — which an
/// engine that runs script on one thread does not have. It is still a real promise: <c>await</c> works, and
/// the value arrives on the microtask turn a <c>then</c> would give it.
/// </para>
/// <para>
/// Two documented simplifications against WebIDL, both of which <c>console</c>, <c>crypto</c> and
/// <c>performance</c> carry too. There is no <c>SubtleCrypto</c> interface object and no
/// <c>SubtleCrypto.prototype</c>, so <c>digest</c> and the <c>@@toStringTag</c> are own properties of this
/// object with the attributes an ECMAScript built-in method has rather than those of a WebIDL interface
/// prototype's operations; <c>Object.keys(crypto.subtle)</c> answers the empty array here exactly as it does
/// in a browser, where the operation lives one level up. And <c>[SecureContext]</c> has no meaning for an
/// embedded engine — there is no origin, no transport and no browsing context — so the operation is exposed
/// unconditionally, which is the same reading Node and workerd take.
/// </para>
/// </remarks>
[JsObject]
internal sealed partial class SubtleCryptoInstance : BuiltinShapeObject
{
    /// <summary>
    /// https://w3c.github.io/webcrypto/#sha-registration — "The recognized algorithm names are
    /// <c>SHA-1</c>, <c>SHA-256</c>, <c>SHA-384</c>, and <c>SHA-512</c> for the respective SHA algorithms",
    /// each registered for the <c>digest</c> operation.
    /// </summary>
    private const string Sha1 = "SHA-1";
    private const string Sha256 = "SHA-256";
    private const string Sha384 = "SHA-384";
    private const string Sha512 = "SHA-512";

    /// <summary>
    /// The associative container "stored at the <c>op</c> key of supportedAlgorithms" for <c>op</c> =
    /// "digest". Written as an explicit list rather than derived from anything, because an algorithm added to
    /// the BCL later must not become reachable from script until someone has read this specification again.
    /// </summary>
    private static readonly string[] RegisteredDigestAlgorithms = [Sha1, Sha256, Sha384, Sha512];

    private static readonly JsString _nameKey = new("name");

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString SubtleCryptoToStringTag = new("SubtleCrypto");

    private readonly Realm _realm;

    internal SubtleCryptoInstance(Engine engine, Realm realm, ObjectPrototype objectPrototype) : base(engine)
    {
        _realm = realm;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#SubtleCrypto-method-digest, whose IDL is
    /// <c>Promise&lt;ArrayBuffer&gt; digest(AlgorithmIdentifier algorithm, BufferSource data)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order the failures come in is WebIDL's, not the algorithm's: the brand check, then the conversion
    /// of <c>algorithm</c> to <c>(object or DOMString)</c>, then the conversion of <c>data</c> to
    /// <c>BufferSource</c>, and only then step 2's normalization. So <c>digest('nonsense', 42)</c> rejects
    /// with the <c>TypeError</c> the second argument earns rather than the <c>NotSupportedError</c> the first
    /// one would — argument conversion runs before a single step of the method body does.
    /// </para>
    /// <para>
    /// Step 4 says to <i>copy</i> the bytes. Nothing here copies them: the digest is computed from a window
    /// onto the engine's own backing array before this method returns, and the copy exists only so that a
    /// mutation from another agent cannot be observed halfway through — see
    /// <see cref="BufferSource"/>. The bytes are read once, in order, and never again.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "digest", Length = 2)]
    private JsValue Digest(JsValue thisObject, JsValue algorithm, JsValue data)
    {
        var capability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);

        try
        {
            if (thisObject is not SubtleCryptoInstance)
            {
                Throw.TypeError(_realm, "Failed to execute 'digest' on 'SubtleCrypto': illegal invocation, receiver is not a SubtleCrypto object.");
            }

            // `AlgorithmIdentifier` is `typedef (object or DOMString)`, so an object stays an object and
            // everything else is stringified here — which is where a symbol raises its TypeError, and why
            // `digest(null, …)` normalizes the name "null" rather than failing differently.
            var algorithmObject = algorithm as ObjectInstance;
            var algorithmName = algorithmObject is null ? TypeConverter.ToString(algorithm) : null;

            // `BufferSource data`: converted before the method body runs, hence before normalization.
            var message = GetMessageBytes(data);

            // Steps 2 and 3: normalize an algorithm, with op set to "digest".
            var normalizedAlgorithm = algorithmObject is not null
                ? NormalizeAlgorithm(algorithmObject)
                : MatchRegisteredAlgorithm(algorithmName!);

            // Steps 9 to 12: the digest operation, then an ArrayBuffer over its bytes.
            var digest = ComputeDigest(normalizedAlgorithm, message);

            capability.Resolve(new JsArrayBuffer(_engine, digest)
            {
                _prototype = _realm.Intrinsics.ArrayBuffer.PrototypeObject,
            });
        }
        // Every failure of a promise-returning operation is a rejection, and these three are the whole of
        // what a failure here can arrive as. They are the script-visible arm of
        // `JintStatementList.ShouldCatch` — the exceptions the interpreter itself turns into a throw
        // completion — minus SyntaxErrorException, which nothing on this path can raise because nothing on
        // it parses. Everything else a JintException covers is deliberately not caught: an execution
        // constraint, a cancellation or a stack-depth overflow is not a value a script may catch, and a
        // constraint that became a rejection would no longer bound anything.
        catch (JavaScriptException e)
        {
            capability.Reject(e.Error);
        }
        catch (TypeErrorException e)
        {
            // TypeConverter raises this shape when no engine is at hand — `ToString` of a symbol, which is
            // reachable both as the algorithm identifier and as an algorithm object's `name` member.
            capability.Reject(_realm.Intrinsics.TypeError.Construct(e.Message));
        }
        catch (RangeErrorException e)
        {
            capability.Reject(_realm.Intrinsics.RangeError.Construct(e.Message));
        }

        return capability.PromiseInstance;
    }

    /// <summary>
    /// WebIDL's conversion of the <c>data</c> argument to <c>BufferSource</c>, which is
    /// <c>(ArrayBufferView or ArrayBuffer)</c> — https://webidl.spec.whatwg.org/#BufferSource.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A view onto a <c>SharedArrayBuffer</c> is refused, and so is a <c>SharedArrayBuffer</c> itself: the
    /// IDL says <c>BufferSource</c> and not <c>AllowSharedBufferSource</c>, and WebIDL refuses a shared
    /// buffer for any type not carrying <c>[AllowShared]</c>. It is the rule <c>crypto.getRandomValues</c>
    /// refuses one under, one object away. <see cref="BufferSource"/> itself is deliberately permissive
    /// there, because its first caller — <c>TextDecoder.decode</c>, whose IDL <i>is</i>
    /// <c>AllowSharedBufferSource</c> — may take one; the stricter half of this conversion belongs to the
    /// operation that declares it, so it sits here rather than in the shared helper.
    /// </para>
    /// <para>
    /// One deliberate divergence, in the permissive direction: a <b>resizable</b> <c>ArrayBuffer</c>, or a
    /// view onto one, is accepted. WebIDL's conversion refuses those too for a type without
    /// <c>[AllowResizable]</c>, which <c>BufferSource</c> does not carry — but the bytes hashed are exactly
    /// the ones the view spans at the moment of the call, so the answer is right rather than merely
    /// tolerated, and no engine an embedder is likely to be replacing refuses it.
    /// </para>
    /// </remarks>
    private ReadOnlySpan<byte> GetMessageBytes(JsValue data)
    {
        if (!BufferSource.TryGetBytes(data, out var bytes))
        {
            Throw.TypeError(_realm, "Failed to execute 'digest' on 'SubtleCrypto': parameter 2 is not of type 'BufferSource'.");
        }

        var buffer = data switch
        {
            JsTypedArray typedArray => typedArray._viewedArrayBuffer,
            JsDataView dataView => dataView._viewedArrayBuffer,
            _ => data as JsArrayBuffer,
        };

        if (buffer is not null && buffer.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Failed to execute 'digest' on 'SubtleCrypto': parameter 2 is backed by a SharedArrayBuffer, which this operation does not accept.");
        }

        return bytes;
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#dfn-normalize-an-algorithm, the branch for an <c>alg</c> that is an
    /// object: convert it to the IDL dictionary type <c>Algorithm</c>, whose one member is
    /// <c>required DOMString name</c>, and match that name against the algorithms registered for the
    /// operation.
    /// </summary>
    /// <remarks>
    /// The single <c>Get</c> is the whole of the dictionary conversion, and it may run script — a getter on
    /// <c>name</c> is called exactly once, and whatever it throws becomes the rejection. An absent member
    /// reads as <c>undefined</c>, which for a required member is the <c>TypeError</c> WebIDL raises rather
    /// than a <c>NotSupportedError</c> for the name "undefined".
    /// </remarks>
    private string NormalizeAlgorithm(ObjectInstance algorithm)
    {
        var name = algorithm.Get(_nameKey);
        if (name.IsUndefined())
        {
            Throw.TypeError(_realm, "Failed to execute 'digest' on 'SubtleCrypto': Algorithm: required member name is undefined.");
        }

        return MatchRegisteredAlgorithm(TypeConverter.ToString(name));
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
    /// rather than the caller's spelling, because the digest operation below matches its name
    /// <i>case-sensitively</i> — normalization is what makes <c>'sha-256'</c> reach SHA-256 at all.
    /// </remarks>
    private string MatchRegisteredAlgorithm(string algName)
    {
        foreach (var registered in RegisteredDigestAlgorithms)
        {
            if (Ascii.EqualsIgnoreCase(algName, registered))
            {
                return registered;
            }
        }

        ThrowDomException(
            DomExceptionNames.NotSupported,
            "Failed to execute 'digest' on 'SubtleCrypto': the algorithm name '" + algName + "' is not one this engine registers for the digest operation (SHA-1, SHA-256, SHA-384, SHA-512).");
        return null!;
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#sha-operations-digest — the digest operation of the SHA algorithms,
    /// which is the hash function of [FIPS-180-4] applied to the message. The name is matched
    /// case-sensitively there, which is why normalization above returns the registered spelling.
    /// </summary>
    /// <remarks>
    /// The one-shot <c>HashData</c> statics rather than <see cref="IncrementalHash"/>: the message is one
    /// contiguous span that is already entirely in memory, so there is nothing to feed in chunks, and the
    /// one-shot form allocates only the result array — no hash object, no <c>IDisposable</c> to get wrong,
    /// and on every platform it is the path that reaches the accelerated implementation directly.
    /// <see cref="IncrementalHash"/> earns its keep where the input arrives in pieces, which is a shape this
    /// operation cannot be in.
    /// </remarks>
    private static byte[] ComputeDigest(string normalizedAlgorithm, ReadOnlySpan<byte> message)
    {
        switch (normalizedAlgorithm)
        {
            case Sha1:
                // SHA-1 is one of the four names the specification registers for this operation and every
                // browser implements it, so refusing it here would be Jint deciding what a script may hash —
                // and a script asking for it has already chosen. It is reached only through that name, is
                // never a default, and nothing in the engine picks it for anybody.
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms -- the caller named SHA-1, we did not choose it
                return SHA1.HashData(message);
#pragma warning restore CA5350
            case Sha256:
                return SHA256.HashData(message);
            case Sha384:
                return SHA384.HashData(message);
            case Sha512:
                return SHA512.HashData(message);
            default:
                // Unreachable: the name came from RegisteredDigestAlgorithms one call ago.
                Throw.InvalidOperationException("Unhandled digest algorithm '" + normalizedAlgorithm + "'.");
                return null!;
        }
    }

    [DoesNotReturn]
    private void ThrowDomException(string name, string message)
    {
        var exception = _realm.Intrinsics.DomException.CreateException(name, message);
        var location = _engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(_engine, exception, in location);
    }
}
#endif
