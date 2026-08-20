#if NET8_0_OR_GREATER
using System.Security.Cryptography;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
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
/// <b>Eight of the twelve operations exist</b>: <c>digest</c>, <c>sign</c>, <c>verify</c>, <c>encrypt</c>,
/// <c>decrypt</c>, <c>generateKey</c>, <c>importKey</c> and <c>exportKey</c>, over the algorithms
/// <c>HMAC</c> (SHA-1, SHA-256, SHA-384, SHA-512) and <c>AES-GCM</c> (128, 192 and 256 bits), plus the four
/// SHA hashes for <c>digest</c>. <c>deriveKey</c>, <c>deriveBits</c>, <c>wrapKey</c> and <c>unwrapKey</c> are
/// <b>absent</b> rather than present-and-throwing, and so is every asymmetric algorithm, so a library that
/// checks <c>typeof crypto.subtle.deriveBits === 'function'</c> before reaching for it gets the truthful
/// answer and takes its fallback path — the same promise <c>crypto.subtle</c> itself makes to an engine
/// without the crypto feature. An algorithm that is absent for a <i>particular</i> operation is a
/// <c>NotSupportedError</c>, which is what the specification says a name that is not registered for an
/// operation is: <c>sign</c> with <c>AES-GCM</c> fails that way, and so does <c>encrypt</c> with
/// <c>HMAC</c>.
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
/// The promise is already resolved when it is handed back: every operation here is synchronous CPU work over
/// bytes that are already in memory, so there is nothing for an event-loop turn to wait for. "Return promise
/// and perform the remaining steps in parallel" exists so that a browser's main thread is not blocked by a
/// slow key operation, and observing the difference needs a second thread to mutate something in between —
/// which an engine that runs script on one thread does not have. It is still a real promise: <c>await</c>
/// works, and the value arrives on the microtask turn a <c>then</c> would give it.
/// </para>
/// <para>
/// Two documented simplifications against WebIDL, both of which <c>console</c>, <c>crypto</c> and
/// <c>performance</c> carry too. There is no <c>SubtleCrypto</c> interface object and no
/// <c>SubtleCrypto.prototype</c>, so the operations and the <c>@@toStringTag</c> are own properties of this
/// object with the attributes an ECMAScript built-in method has rather than those of a WebIDL interface
/// prototype's operations; <c>Object.keys(crypto.subtle)</c> answers the empty array here exactly as it does
/// in a browser, where the operations live one level up. And <c>[SecureContext]</c> has no meaning for an
/// embedded engine — there is no origin, no transport and no browsing context — so the operations are
/// exposed unconditionally, which is the same reading Node and workerd take.
/// </para>
/// </remarks>
[JsObject]
internal sealed partial class SubtleCryptoInstance : BuiltinShapeObject
{
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

    private CryptoContext Context => new(_engine, _realm);

    /// <summary>
    /// https://w3c.github.io/webcrypto/#SubtleCrypto-method-digest, whose IDL is
    /// <c>Promise&lt;ArrayBuffer&gt; digest(AlgorithmIdentifier algorithm, BufferSource data)</c>.
    /// </summary>
    /// <remarks>
    /// The order the failures come in is WebIDL's, not the algorithm's: the brand check, then the conversion
    /// of <c>algorithm</c> to <c>(object or DOMString)</c>, then the conversion of <c>data</c> to
    /// <c>BufferSource</c>, and only then step 2's normalization. So <c>digest('nonsense', 42)</c> rejects
    /// with the <c>TypeError</c> the second argument earns rather than the <c>NotSupportedError</c> the first
    /// one would — argument conversion runs before a single step of the method body does.
    /// </remarks>
    [JsFunction(Name = "digest", Length = 2)]
    private JsValue Digest(JsValue thisObject, JsValue algorithm, JsValue data)
    {
        return Perform(thisObject, CryptoOperation.Digest, what =>
        {
            var identifier = AlgorithmNormalization.ConvertIdentifier(algorithm);
            var message = GetBufferSourceBytes(data, what, "parameter 2");

            var normalized = AlgorithmNormalization.Normalize(Context, identifier, CryptoOperation.Digest, what);

            return Context.CreateArrayBuffer(ComputeDigest(normalized.Name, message));
        });
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#SubtleCrypto-method-sign, whose IDL is
    /// <c>Promise&lt;ArrayBuffer&gt; sign(AlgorithmIdentifier algorithm, CryptoKey key, BufferSource data)</c>.
    /// </summary>
    [JsFunction(Name = "sign", Length = 3)]
    private JsValue Sign(JsValue thisObject, JsValue algorithm, JsValue key, JsValue data)
    {
        return Perform(thisObject, CryptoOperation.Sign, what =>
        {
            var identifier = AlgorithmNormalization.ConvertIdentifier(algorithm);
            var cryptoKey = RequireCryptoKey(key, what, "parameter 2");
            var message = GetBufferSourceBytes(data, what, "parameter 3");

            var normalized = AlgorithmNormalization.Normalize(Context, identifier, CryptoOperation.Sign, what);

            RequireKeyFor(normalized, cryptoKey, KeyUsage.Sign, "sign", what);

            // HMAC is the only algorithm registered for this operation, and the check above has just proved
            // the key was made for the algorithm that normalization returned — so the dispatch is decided.
            return Context.CreateArrayBuffer(HmacAlgorithm.Sign(cryptoKey, message));
        });
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#SubtleCrypto-method-verify, whose IDL is
    /// <c>Promise&lt;boolean&gt; verify(AlgorithmIdentifier algorithm, CryptoKey key, BufferSource signature,
    /// BufferSource data)</c>.
    /// </summary>
    [JsFunction(Name = "verify", Length = 4)]
    private JsValue Verify(JsValue thisObject, JsValue algorithm, JsValue key, JsValue signature, JsValue data)
    {
        return Perform(thisObject, CryptoOperation.Verify, what =>
        {
            var identifier = AlgorithmNormalization.ConvertIdentifier(algorithm);
            var cryptoKey = RequireCryptoKey(key, what, "parameter 2");
            var signatureBytes = GetBufferSourceBytes(signature, what, "parameter 3");
            var message = GetBufferSourceBytes(data, what, "parameter 4");

            var normalized = AlgorithmNormalization.Normalize(Context, identifier, CryptoOperation.Verify, what);

            RequireKeyFor(normalized, cryptoKey, KeyUsage.Verify, "verify", what);

            return HmacAlgorithm.Verify(cryptoKey, signatureBytes, message) ? JsBoolean.True : JsBoolean.False;
        });
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#SubtleCrypto-method-encrypt, whose IDL is
    /// <c>Promise&lt;ArrayBuffer&gt; encrypt(AlgorithmIdentifier algorithm, CryptoKey key, BufferSource data)</c>.
    /// </summary>
    [JsFunction(Name = "encrypt", Length = 3)]
    private JsValue Encrypt(JsValue thisObject, JsValue algorithm, JsValue key, JsValue data)
    {
        return Perform(thisObject, CryptoOperation.Encrypt, what =>
        {
            var identifier = AlgorithmNormalization.ConvertIdentifier(algorithm);
            var cryptoKey = RequireCryptoKey(key, what, "parameter 2");
            var plaintext = GetBufferSourceBytes(data, what, "parameter 3");

            var normalized = AlgorithmNormalization.Normalize(Context, identifier, CryptoOperation.Encrypt, what);

            RequireKeyFor(normalized, cryptoKey, KeyUsage.Encrypt, "encrypt", what);

            // AES-GCM is the only algorithm registered for this operation, so — as in sign above — the check
            // that the key was made for the normalized algorithm is what decides the dispatch.
            return Context.CreateArrayBuffer(AesGcmAlgorithm.Encrypt(Context, normalized, cryptoKey, plaintext, what));
        });
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#SubtleCrypto-method-decrypt, whose IDL is
    /// <c>Promise&lt;ArrayBuffer&gt; decrypt(AlgorithmIdentifier algorithm, CryptoKey key, BufferSource data)</c>.
    /// </summary>
    [JsFunction(Name = "decrypt", Length = 3)]
    private JsValue Decrypt(JsValue thisObject, JsValue algorithm, JsValue key, JsValue data)
    {
        return Perform(thisObject, CryptoOperation.Decrypt, what =>
        {
            var identifier = AlgorithmNormalization.ConvertIdentifier(algorithm);
            var cryptoKey = RequireCryptoKey(key, what, "parameter 2");
            var ciphertext = GetBufferSourceBytes(data, what, "parameter 3");

            var normalized = AlgorithmNormalization.Normalize(Context, identifier, CryptoOperation.Decrypt, what);

            RequireKeyFor(normalized, cryptoKey, KeyUsage.Decrypt, "decrypt", what);

            return Context.CreateArrayBuffer(AesGcmAlgorithm.Decrypt(Context, normalized, cryptoKey, ciphertext, what));
        });
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#SubtleCrypto-method-generateKey, whose IDL is
    /// <c>Promise&lt;(CryptoKey or CryptoKeyPair)&gt; generateKey(AlgorithmIdentifier algorithm,
    /// boolean extractable, sequence&lt;KeyUsage&gt; keyUsages)</c>.
    /// </summary>
    /// <remarks>
    /// Both algorithms here produce a single secret key, never a pair, so the union's second arm — and with
    /// it <c>CryptoKeyPair</c>, which has no interface object in this engine — is unreachable.
    /// </remarks>
    [JsFunction(Name = "generateKey", Length = 3)]
    private JsValue GenerateKey(JsValue thisObject, JsValue algorithm, JsValue extractable, JsValue keyUsages)
    {
        return Perform(thisObject, CryptoOperation.GenerateKey, what =>
        {
            var identifier = AlgorithmNormalization.ConvertIdentifier(algorithm);
            var isExtractable = TypeConverter.ToBoolean(extractable);
            var usages = KeyUsages.ReadSequence(Context, keyUsages, what);

            var normalized = AlgorithmNormalization.Normalize(Context, identifier, CryptoOperation.GenerateKey, what);

            var material = string.Equals(normalized.Name, AlgorithmNormalization.Hmac, StringComparison.Ordinal)
                ? HmacAlgorithm.GenerateKey(Context, normalized, usages, what)
                : AesGcmAlgorithm.GenerateKey(Context, normalized, usages, what);

            // "If result is a CryptoKey object: If the [[type]] internal slot of result is 'secret' or
            // 'private' and usages is empty, then throw a SyntaxError." A secret key nobody may use is a
            // mistake, not a key, and it is caught here rather than inside the algorithm because it is the
            // same mistake whichever algorithm made the key.
            RequireNonEmptyUsages(usages, what);

            return _realm.Intrinsics.CryptoKey.CreateKey(material.Handle, material.Algorithm, isExtractable, usages);
        });
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#SubtleCrypto-method-importKey, whose IDL is
    /// <c>Promise&lt;CryptoKey&gt; importKey(KeyFormat format, (BufferSource or JsonWebKey) keyData,
    /// AlgorithmIdentifier algorithm, boolean extractable, sequence&lt;KeyUsage&gt; keyUsages)</c>.
    /// </summary>
    /// <remarks>
    /// The union's arms are told apart the way WebIDL tells them apart — https://webidl.spec.whatwg.org/#es-union:
    /// a buffer source is one, and anything else that is an object (or <c>null</c>, or <c>undefined</c>)
    /// becomes a <c>JsonWebKey</c> dictionary, which is read <i>here</i>, with the other arguments, and not
    /// when the algorithm's steps get to it. So the getters on a JWK run before the algorithm is even
    /// normalized. Step 4 then rejects the mismatches: a JWK with a format that is not <c>"jwk"</c>, and a
    /// buffer source with the format that is.
    /// </remarks>
    [JsFunction(Name = "importKey", Length = 5)]
    private JsValue ImportKey(JsValue thisObject, JsValue format, JsValue keyData, JsValue algorithm, JsValue extractable, JsValue keyUsages)
    {
        return Perform(thisObject, CryptoOperation.ImportKey, what =>
        {
            var keyFormat = ReadKeyFormat(format, what);
            var (rawData, jwk) = ReadKeyData(keyData, what);
            var identifier = AlgorithmNormalization.ConvertIdentifier(algorithm);
            var isExtractable = TypeConverter.ToBoolean(extractable);
            var usages = KeyUsages.ReadSequence(Context, keyUsages, what);

            var normalized = AlgorithmNormalization.Normalize(Context, identifier, CryptoOperation.ImportKey, what);

            // Step 4, after normalization as the numbering says.
            if (keyFormat == KeyFormat.Jwk && jwk is null)
            {
                Context.ThrowTypeError(what + ": the 'jwk' format needs a JsonWebKey object, not a buffer source.");
            }

            if (keyFormat != KeyFormat.Jwk && jwk is not null)
            {
                Context.ThrowTypeError(what + ": the '" + KeyFormats.NameOf(keyFormat) + "' format needs a buffer source, not a JsonWebKey object.");
            }

            var material = string.Equals(normalized.Name, AlgorithmNormalization.Hmac, StringComparison.Ordinal)
                ? HmacAlgorithm.ImportKey(Context, keyFormat, rawData, jwk, normalized, isExtractable, usages, what)
                : AesGcmAlgorithm.ImportKey(Context, keyFormat, rawData, jwk, isExtractable, usages, what);

            RequireNonEmptyUsages(usages, what);

            return _realm.Intrinsics.CryptoKey.CreateKey(material.Handle, material.Algorithm, isExtractable, usages);
        });
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#SubtleCrypto-method-exportKey, whose IDL is
    /// <c>Promise&lt;(ArrayBuffer or JsonWebKey)&gt; exportKey(KeyFormat format, CryptoKey key)</c>.
    /// </summary>
    /// <remarks>
    /// This is the only door the key material has, and it is shut by two checks before the algorithm is
    /// asked for anything: the key's algorithm must be registered for the export operation, and the key must
    /// be extractable. Neither is a property of the request — both are properties of the key, decided when it
    /// was made.
    /// </remarks>
    [JsFunction(Name = "exportKey", Length = 2)]
    private JsValue ExportKey(JsValue thisObject, JsValue format, JsValue key)
    {
        return Perform(thisObject, CryptoOperation.ExportKey, what =>
        {
            var keyFormat = ReadKeyFormat(format, what);
            var cryptoKey = RequireCryptoKey(key, what, "parameter 2");

            // "If the name member of the [[algorithm]] internal slot of key does not identify a registered
            // algorithm that supports the export key operation, then throw a NotSupportedError."
            if (Array.IndexOf(AlgorithmNormalization.RegisteredFor(CryptoOperation.ExportKey), cryptoKey.Algorithm.Name) < 0)
            {
                Context.ThrowNotSupportedError(
                    what + ": " + cryptoKey.Algorithm.Name + " is not registered for the exportKey operation.");
            }

            // "If the [[extractable]] internal slot of key is false, then throw an InvalidAccessError."
            if (!cryptoKey.Extractable)
            {
                Context.ThrowInvalidAccessError(what + ": the key is not extractable.");
            }

            return string.Equals(cryptoKey.Algorithm.Name, AlgorithmNormalization.Hmac, StringComparison.Ordinal)
                ? HmacAlgorithm.ExportKey(Context, cryptoKey, keyFormat, what)
                : AesGcmAlgorithm.ExportKey(Context, cryptoKey, keyFormat, what);
        });
    }

    /// <summary>
    /// Runs one operation's steps and settles a promise with whatever they produce — the whole of what
    /// https://webidl.spec.whatwg.org/#dfn-create-operation-function does around a promise-returning
    /// operation, including the brand check.
    /// </summary>
    /// <remarks>
    /// The three exceptions caught are the whole of what a failure here can arrive as. They are the
    /// script-visible arm of <c>JintStatementList.ShouldCatch</c> — the exceptions the interpreter itself
    /// turns into a throw completion — minus <c>SyntaxErrorException</c>, which nothing on this path can
    /// raise because nothing on it parses. Everything else a <c>JintException</c> covers is deliberately not
    /// caught: an execution constraint, a cancellation or a stack-depth overflow is not a value a script may
    /// catch, and a constraint that became a rejection would no longer bound anything.
    /// </remarks>
    private JsValue Perform(JsValue thisObject, CryptoOperation operation, Func<string, JsValue> steps)
    {
        var capability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);
        var what = "Failed to execute '" + AlgorithmNormalization.NameOf(operation) + "' on 'SubtleCrypto'";

        try
        {
            if (thisObject is not SubtleCryptoInstance)
            {
                Throw.TypeError(_realm, what + ": illegal invocation, receiver is not a SubtleCrypto object.");
            }

            capability.Resolve(steps(what));
        }
        catch (JavaScriptException e)
        {
            capability.Reject(e.Error);
        }
        catch (TypeErrorException e)
        {
            // TypeConverter raises this shape when no engine is at hand — `ToString` of a symbol, which is
            // reachable as an algorithm identifier, as an algorithm object's `name` member, as a key format
            // and as a JWK field.
            capability.Reject(_realm.Intrinsics.TypeError.Construct(e.Message));
        }
        catch (RangeErrorException e)
        {
            capability.Reject(_realm.Intrinsics.RangeError.Construct(e.Message));
        }

        return capability.PromiseInstance;
    }

    /// <summary>
    /// The two <c>InvalidAccessError</c>s every keyed operation makes before doing any work: the key was made
    /// for this algorithm, and the key permits this use.
    /// </summary>
    private void RequireKeyFor(NormalizedAlgorithm normalized, JsCryptoKey key, KeyUsage usage, string usageName, string what)
    {
        if (!string.Equals(normalized.Name, key.Algorithm.Name, StringComparison.Ordinal))
        {
            Context.ThrowInvalidAccessError(
                what + ": the key was created for " + key.Algorithm.Name + ", not for " + normalized.Name + ".");
        }

        if (!key.Allows(usage))
        {
            Context.ThrowInvalidAccessError(
                what + ": the key's usages are " + KeyUsages.Describe(key.Usages) + ", which does not include '" + usageName + "'.");
        }
    }

    /// <summary>
    /// "If the [[type]] internal slot of result is 'secret' or 'private' and usages is empty, then throw a
    /// SyntaxError" — the step <c>generateKey</c> and <c>importKey</c> both end in. Every key this engine
    /// makes is a secret one.
    /// </summary>
    private void RequireNonEmptyUsages(KeyUsage usages, string what)
    {
        if (usages == KeyUsage.None)
        {
            Context.ThrowSyntaxError(what + ": a secret key must be created with at least one usage.");
        }
    }

    /// <summary>
    /// The WebIDL <c>CryptoKey</c> conversion: a platform object of that interface, or a <c>TypeError</c>.
    /// </summary>
    private JsCryptoKey RequireCryptoKey(JsValue value, string what, string parameter)
    {
        if (value is JsCryptoKey key)
        {
            return key;
        }

        Context.ThrowTypeError(what + ": " + parameter + " is not of type 'CryptoKey'.");
        return null!;
    }

    /// <summary>
    /// The WebIDL <c>KeyFormat</c> enumeration conversion, https://webidl.spec.whatwg.org/#es-enumeration:
    /// the value is stringified and matched <i>case-sensitively</i> against the four recognized values, and
    /// anything else is a <c>TypeError</c>. That is a different failure from asking a symmetric algorithm for
    /// <c>"spki"</c>, which is a recognized format the algorithm's own steps refuse with a
    /// <c>NotSupportedError</c>.
    /// </summary>
    private KeyFormat ReadKeyFormat(JsValue value, string what)
    {
        var name = TypeConverter.ToString(value);

        switch (name)
        {
            case KeyFormats.Raw:
                return KeyFormat.Raw;
            case KeyFormats.Spki:
                return KeyFormat.Spki;
            case KeyFormats.Pkcs8:
                return KeyFormat.Pkcs8;
            case KeyFormats.Jwk:
                return KeyFormat.Jwk;
            default:
                Context.ThrowTypeError(
                    what + ": '" + name + "' is not a valid value for the enumeration KeyFormat (raw, spki, pkcs8, jwk).");
                return default;
        }
    }

    /// <summary>
    /// The <c>(BufferSource or JsonWebKey)</c> union conversion. Exactly one of the two results is non-null.
    /// </summary>
    /// <remarks>
    /// <c>null</c> and <c>undefined</c> become a <c>JsonWebKey</c> with every member absent, which is what
    /// WebIDL does for a union containing a dictionary type; a number, a string or a boolean is a
    /// <c>TypeError</c>, because converting a non-object to a dictionary is one.
    /// </remarks>
    private (byte[]? Raw, JsonWebKeyData? Jwk) ReadKeyData(JsValue keyData, string what)
    {
        if (BufferSource.TryGetBytes(keyData, out var bytes))
        {
            if (AlgorithmNormalization.IsSharedBufferSource(keyData))
            {
                Context.ThrowTypeError(what + ": parameter 2 is backed by a SharedArrayBuffer, which this operation does not accept.");
            }

            return (bytes.ToArray(), null);
        }

        if (keyData.IsNull() || keyData.IsUndefined())
        {
            return (null, new JsonWebKeyData());
        }

        if (keyData is ObjectInstance source)
        {
            return (null, JsonWebKeyData.Read(Context, source, what));
        }

        Context.ThrowTypeError(what + ": parameter 2 is neither a BufferSource nor a JsonWebKey object.");
        return default;
    }

    /// <summary>
    /// WebIDL's conversion of a <c>BufferSource</c> argument, which is
    /// <c>(ArrayBufferView or ArrayBuffer)</c> — https://webidl.spec.whatwg.org/#BufferSource — followed by
    /// "getting a copy of the bytes" it holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The copy is real, and it has to be: the argument conversions run before the method body, and the
    /// first thing the body does is normalize an algorithm, which reads a <c>name</c> member and may
    /// therefore run a script's getter — with the buffer this argument came from in scope. A window onto the
    /// engine's own backing array would then be a window onto what that getter left behind, and the operation
    /// would run over bytes the caller never passed.
    /// </para>
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
    /// <c>[AllowResizable]</c>, which <c>BufferSource</c> does not carry — but the bytes taken are exactly
    /// the ones the view spans at the moment of the call, so the answer is right rather than merely
    /// tolerated, and no engine an embedder is likely to be replacing refuses it.
    /// </para>
    /// </remarks>
    private byte[] GetBufferSourceBytes(JsValue data, string what, string parameter)
    {
        if (!BufferSource.TryGetBytes(data, out var bytes))
        {
            Context.ThrowTypeError(what + ": " + parameter + " is not of type 'BufferSource'.");
        }

        if (AlgorithmNormalization.IsSharedBufferSource(data))
        {
            Context.ThrowTypeError(what + ": " + parameter + " is backed by a SharedArrayBuffer, which this operation does not accept.");
        }

        return bytes.ToArray();
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#sha-operations-digest — the digest operation of the SHA algorithms,
    /// which is the hash function of [FIPS-180-4] applied to the message. The name is matched
    /// case-sensitively there, which is why normalization returns the registered spelling.
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
            case AlgorithmNormalization.Sha1:
                // SHA-1 is one of the four names the specification registers for this operation and every
                // browser implements it, so refusing it here would be Jint deciding what a script may hash —
                // and a script asking for it has already chosen. It is reached only through that name, is
                // never a default, and nothing in the engine picks it for anybody.
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms -- the caller named SHA-1, we did not choose it
                return SHA1.HashData(message);
#pragma warning restore CA5350
            case AlgorithmNormalization.Sha256:
                return SHA256.HashData(message);
            case AlgorithmNormalization.Sha384:
                return SHA384.HashData(message);
            case AlgorithmNormalization.Sha512:
                return SHA512.HashData(message);
            default:
                // Unreachable: the name came from the registry one call ago.
                Throw.InvalidOperationException("Unhandled digest algorithm '" + normalizedAlgorithm + "'.");
                return null!;
        }
    }
}
#endif
