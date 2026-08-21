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
/// <b>Ten of the twelve operations exist</b>: <c>digest</c>, <c>sign</c>, <c>verify</c>, <c>encrypt</c>,
/// <c>decrypt</c>, <c>generateKey</c>, <c>importKey</c>, <c>exportKey</c>, <c>deriveBits</c> and
/// <c>deriveKey</c>, over the algorithms <c>HMAC</c>, <c>AES-GCM</c> (128, 192 and 256 bits),
/// <c>RSASSA-PKCS1-v1_5</c>, <c>RSA-PSS</c>, <c>RSA-OAEP</c>, <c>ECDSA</c>, <c>ECDH</c>, <c>HKDF</c> and
/// <c>PBKDF2</c> — each of the hashed ones over SHA-1, SHA-256, SHA-384 and SHA-512, and each of the
/// elliptic-curve ones over P-256, P-384 and P-521 — plus those four SHA hashes for <c>digest</c>, and the
/// key formats <c>raw</c>, <c>spki</c>, <c>pkcs8</c> and <c>jwk</c>.
/// <c>wrapKey</c> and <c>unwrapKey</c> are <b>absent</b> rather than present-and-throwing, so a library that
/// checks <c>typeof crypto.subtle.wrapKey === 'function'</c> before reaching for it gets the truthful answer
/// and takes its fallback path — the same promise <c>crypto.subtle</c> itself makes to an engine without the
/// crypto feature. An algorithm that is absent for a <i>particular</i> operation is a
/// <c>NotSupportedError</c>, which is what the specification says a name that is not registered for an
/// operation is: <c>sign</c> with <c>AES-GCM</c> fails that way, and so does <c>encrypt</c> with
/// <c>HMAC</c> and <c>generateKey</c> with <c>PBKDF2</c>.
/// </para>
/// <para>
/// The registries are per operation and are not symmetric. <c>HKDF</c> and <c>PBKDF2</c> register
/// <c>importKey</c>, <c>deriveBits</c> and the internal <c>get key length</c> and nothing else — there is
/// nothing to <c>generateKey</c> and, their keys being non-extractable by construction, nothing to
/// <c>exportKey</c>. <c>ECDH</c> registers <c>deriveBits</c> and never <c>sign</c>; <c>ECDSA</c> the reverse.
/// An AES-GCM key may still carry the <c>wrapKey</c> and <c>unwrapKey</c> usages that no operation here
/// consumes, which is the one remaining case of a usage bit without an operation behind it.
/// </para>
/// <para>
/// <c>deriveKey</c> is a composition rather than an algorithm: it derives bits with one algorithm and imports
/// them with another, so the key it hands back is exactly the key <c>importKey</c> would have made from the
/// same bytes in the <c>raw</c> format.
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

    /// <summary>
    /// <c>CryptoKeyPair</c> — https://w3c.github.io/webcrypto/#keypair. It is a <i>dictionary</i>, not an
    /// interface, so what <c>generateKey</c> resolves with for an asymmetric algorithm is an ordinary object
    /// with two own data properties and <c>Object.prototype</c> behind it: there is no <c>CryptoKeyPair</c>
    /// interface object to be found on the global, in a browser as much as here. The member order is
    /// WebIDL's own for converting a dictionary to an object,
    /// https://webidl.spec.whatwg.org/#es-dictionary — lexicographic, so <c>privateKey</c> comes first.
    /// </summary>
    private static readonly JsObjectLayout _cryptoKeyPairLayout = JsObjectLayout.CreateBuilder()
        .Add("privateKey")
        .Add("publicKey")
        .Build();

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

            // The check above has just proved the key was made for the algorithm normalization returned, so
            // the key's own name and this one are the same string and either could decide the dispatch.
            switch (normalized.Name)
            {
                case AlgorithmNormalization.Hmac:
                    return Context.CreateArrayBuffer(HmacAlgorithm.Sign(cryptoKey, message));
                case AlgorithmNormalization.RsassaPkcs1V15:
                case AlgorithmNormalization.RsaPss:
                    return Context.CreateArrayBuffer(RsaAlgorithm.Sign(Context, normalized, cryptoKey, message, what));
                case AlgorithmNormalization.Ecdsa:
                    return Context.CreateArrayBuffer(EcAlgorithm.Sign(Context, normalized, cryptoKey, message, what));
                default:
                    return UnhandledAlgorithm(normalized.Name, CryptoOperation.Sign);
            }
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

            switch (normalized.Name)
            {
                case AlgorithmNormalization.Hmac:
                    return HmacAlgorithm.Verify(cryptoKey, signatureBytes, message) ? JsBoolean.True : JsBoolean.False;
                case AlgorithmNormalization.RsassaPkcs1V15:
                case AlgorithmNormalization.RsaPss:
                    return RsaAlgorithm.Verify(Context, normalized, cryptoKey, signatureBytes, message, what)
                        ? JsBoolean.True
                        : JsBoolean.False;
                case AlgorithmNormalization.Ecdsa:
                    return EcAlgorithm.Verify(Context, normalized, cryptoKey, signatureBytes, message, what)
                        ? JsBoolean.True
                        : JsBoolean.False;
                default:
                    return UnhandledAlgorithm(normalized.Name, CryptoOperation.Verify);
            }
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

            switch (normalized.Name)
            {
                case AlgorithmNormalization.AesGcm:
                    return Context.CreateArrayBuffer(AesGcmAlgorithm.Encrypt(Context, normalized, cryptoKey, plaintext, what));
                case AlgorithmNormalization.RsaOaep:
                    return Context.CreateArrayBuffer(RsaAlgorithm.Encrypt(Context, normalized, cryptoKey, plaintext, what));
                default:
                    return UnhandledAlgorithm(normalized.Name, CryptoOperation.Encrypt);
            }
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

            switch (normalized.Name)
            {
                case AlgorithmNormalization.AesGcm:
                    return Context.CreateArrayBuffer(AesGcmAlgorithm.Decrypt(Context, normalized, cryptoKey, ciphertext, what));
                case AlgorithmNormalization.RsaOaep:
                    return Context.CreateArrayBuffer(RsaAlgorithm.Decrypt(Context, normalized, cryptoKey, ciphertext, what));
                default:
                    return UnhandledAlgorithm(normalized.Name, CryptoOperation.Decrypt);
            }
        });
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#SubtleCrypto-method-generateKey, whose IDL is
    /// <c>Promise&lt;(CryptoKey or CryptoKeyPair)&gt; generateKey(AlgorithmIdentifier algorithm,
    /// boolean extractable, sequence&lt;KeyUsage&gt; keyUsages)</c>.
    /// </summary>
    /// <remarks>
    /// Which arm of the union is returned is the algorithm's business: the symmetric algorithms produce a
    /// single secret <c>CryptoKey</c>, and the three RSA algorithms and the two elliptic-curve ones produce a
    /// <c>CryptoKeyPair</c>. The public half is always extractable, whatever <c>extractable</c> asked for — a
    /// public key is public — and the two halves split the requested usages between them by the usage
    /// intersection each algorithm's steps name, which for an ECDH public key is the empty list.
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

            switch (normalized.Name)
            {
                case AlgorithmNormalization.Hmac:
                    return CreateSecretKey(HmacAlgorithm.GenerateKey(Context, normalized, usages, what), isExtractable, usages, what);

                case AlgorithmNormalization.AesGcm:
                    return CreateSecretKey(AesGcmAlgorithm.GenerateKey(Context, normalized, usages, what), isExtractable, usages, what);

                case AlgorithmNormalization.RsassaPkcs1V15:
                case AlgorithmNormalization.RsaPss:
                case AlgorithmNormalization.RsaOaep:
                    return CreateKeyPair(RsaAlgorithm.GenerateKey(Context, normalized, usages, what), isExtractable, what);

                case AlgorithmNormalization.Ecdsa:
                case AlgorithmNormalization.Ecdh:
                    return CreateKeyPair(EcAlgorithm.GenerateKey(Context, normalized, usages, what), isExtractable, what);

                default:
                    return UnhandledAlgorithm(normalized.Name, CryptoOperation.GenerateKey);
            }
        });
    }

    /// <summary>
    /// The tail of <c>generateKey</c> for an algorithm that produced one secret key: "If result is a
    /// CryptoKey object: If the [[type]] internal slot of result is 'secret' or 'private' and usages is
    /// empty, then throw a SyntaxError." A secret key nobody may use is a mistake, not a key, and it is
    /// caught here rather than inside the algorithm because it is the same mistake whichever algorithm made
    /// the key.
    /// </summary>
    private JsCryptoKey CreateSecretKey(
        (byte[] Handle, CryptoKeyAlgorithm Algorithm) material,
        bool extractable,
        KeyUsage usages,
        string what)
    {
        RequireUsableKey(CryptoKeyTypes.Secret, usages, what);
        return _realm.Intrinsics.CryptoKey.CreateKey(material.Handle, CryptoKeyTypes.Secret, material.Algorithm, extractable, usages);
    }

    /// <summary>
    /// The same tail for a key an asymmetric algorithm's <c>importKey</c> produced, where the type is the
    /// algorithm's answer rather than a foregone <c>"secret"</c> — which is exactly what decides whether the
    /// empty usages list is a mistake.
    /// </summary>
    private JsCryptoKey CreateImportedKey(
        (byte[] Handle, string KeyType, CryptoKeyAlgorithm Algorithm) imported,
        bool extractable,
        KeyUsage usages,
        string what)
    {
        RequireUsableKey(imported.KeyType, usages, what);
        return _realm.Intrinsics.CryptoKey.CreateKey(imported.Handle, imported.KeyType, imported.Algorithm, extractable, usages);
    }

    /// <summary>
    /// The tail of <c>generateKey</c> for an algorithm that produced a pair: "If result is a CryptoKeyPair
    /// object: If the [[usages]] internal slot of the privateKey attribute of result is the empty sequence,
    /// then throw a SyntaxError." It is the private half alone that has to be usable — a pair generated with
    /// <c>['verify']</c> is a pair nobody can sign with, which is the very mistake this catches, while a pair
    /// generated with <c>['sign']</c> has a public half carrying no usages and is perfectly ordinary.
    /// </summary>
    private JsObject CreateKeyPair(in AsymmetricKeyPairMaterial material, bool extractable, string what)
    {
        RequireUsableKey(CryptoKeyTypes.Private, material.PrivateUsages, what);

        // "Set the [[extractable]] internal slot of publicKey to true" — the public half of a pair is always
        // extractable, because there is nothing about it to protect.
        var publicKey = _realm.Intrinsics.CryptoKey.CreateKey(
            material.PublicHandle, CryptoKeyTypes.Public, material.Algorithm, extractable: true, material.PublicUsages);

        var privateKey = _realm.Intrinsics.CryptoKey.CreateKey(
            material.PrivateHandle, CryptoKeyTypes.Private, material.Algorithm, extractable, material.PrivateUsages);

        return JsObject.Create(_engine, _cryptoKeyPairLayout, [privateKey, publicKey]);
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

            switch (normalized.Name)
            {
                case AlgorithmNormalization.Hmac:
                    return CreateSecretKey(
                        HmacAlgorithm.ImportKey(Context, keyFormat, rawData, jwk, normalized, isExtractable, usages, what),
                        isExtractable,
                        usages,
                        what);

                case AlgorithmNormalization.AesGcm:
                    return CreateSecretKey(
                        AesGcmAlgorithm.ImportKey(Context, keyFormat, rawData, jwk, isExtractable, usages, what),
                        isExtractable,
                        usages,
                        what);

                case AlgorithmNormalization.RsassaPkcs1V15:
                case AlgorithmNormalization.RsaPss:
                case AlgorithmNormalization.RsaOaep:
                    return CreateImportedKey(
                        RsaAlgorithm.ImportKey(Context, keyFormat, rawData, jwk, normalized, isExtractable, usages, what),
                        isExtractable,
                        usages,
                        what);

                case AlgorithmNormalization.Ecdsa:
                case AlgorithmNormalization.Ecdh:
                    return CreateImportedKey(
                        EcAlgorithm.ImportKey(Context, keyFormat, rawData, jwk, normalized, isExtractable, usages, what),
                        isExtractable,
                        usages,
                        what);

                case AlgorithmNormalization.Hkdf:
                    return CreateImportedKey(
                        HkdfAlgorithm.ImportKey(Context, keyFormat, rawData, isExtractable, usages, what),
                        isExtractable,
                        usages,
                        what);

                case AlgorithmNormalization.Pbkdf2:
                    return CreateImportedKey(
                        Pbkdf2Algorithm.ImportKey(Context, keyFormat, rawData, isExtractable, usages, what),
                        isExtractable,
                        usages,
                        what);

                default:
                    return UnhandledAlgorithm(normalized.Name, CryptoOperation.ImportKey);
            }
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

            switch (cryptoKey.Algorithm.Name)
            {
                case AlgorithmNormalization.Hmac:
                    return HmacAlgorithm.ExportKey(Context, cryptoKey, keyFormat, what);
                case AlgorithmNormalization.AesGcm:
                    return AesGcmAlgorithm.ExportKey(Context, cryptoKey, keyFormat, what);
                case AlgorithmNormalization.RsassaPkcs1V15:
                case AlgorithmNormalization.RsaPss:
                case AlgorithmNormalization.RsaOaep:
                    return RsaAlgorithm.ExportKey(Context, cryptoKey, keyFormat, what);
                case AlgorithmNormalization.Ecdsa:
                case AlgorithmNormalization.Ecdh:
                    return EcAlgorithm.ExportKey(Context, cryptoKey, keyFormat, what);
                default:
                    return UnhandledAlgorithm(cryptoKey.Algorithm.Name, CryptoOperation.ExportKey);
            }
        });
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#SubtleCrypto-method-deriveBits, whose IDL is
    /// <c>Promise&lt;ArrayBuffer&gt; deriveBits(AlgorithmIdentifier algorithm, CryptoKey baseKey,
    /// optional [EnforceRange] unsigned long? length = null)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The declared length is 2, not 3</b>, because WebIDL's <c>length</c> counts the <i>required</i>
    /// arguments and <c>length</c> has been optional-and-nullable since the specification stopped requiring
    /// a bit count that ECDH does not need. A browser predating that change answers 3; Node's WebCrypto,
    /// which has taken the change, answers 2, and so does this.
    /// </para>
    /// <para>
    /// What a null <c>length</c> means is the algorithm's business and the three differ: ECDH returns the
    /// whole shared secret, and HKDF and PBKDF2 have no natural output size, so their first step is to refuse
    /// it with an <c>OperationError</c>. A length that is not a multiple of 8 is likewise an
    /// <c>OperationError</c> for those two and a <i>bit</i>-exact truncation for ECDH, whose steps impose no
    /// such restriction.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "deriveBits", Length = 2)]
    private JsValue DeriveBits(JsValue thisObject, JsValue algorithm, JsValue baseKey, JsValue length)
    {
        return Perform(thisObject, CryptoOperation.DeriveBits, what =>
        {
            var identifier = AlgorithmNormalization.ConvertIdentifier(algorithm);
            var cryptoKey = RequireCryptoKey(baseKey, what, "parameter 2");
            var bits = AlgorithmNormalization.ConvertOptionalLength(Context, length, what, "parameter 3");

            var normalized = AlgorithmNormalization.Normalize(Context, identifier, CryptoOperation.DeriveBits, what);

            RequireKeyFor(normalized, cryptoKey, KeyUsage.DeriveBits, "deriveBits", what);

            return Context.CreateArrayBuffer(Derive(normalized, cryptoKey, bits, what));
        });
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#SubtleCrypto-method-deriveKey, whose IDL is
    /// <c>Promise&lt;CryptoKey&gt; deriveKey(AlgorithmIdentifier algorithm, CryptoKey baseKey,
    /// AlgorithmIdentifier derivedKeyType, boolean extractable, sequence&lt;KeyUsage&gt; keyUsages)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is a composition and not an operation of its own: <b>three</b> normalizations, then a derivation,
    /// then an import. The <c>algorithm</c> is normalized for <c>deriveBits</c> — there is no "deriveKey"
    /// registry — and the <c>derivedKeyType</c> is normalized <i>twice</i>, once for <c>importKey</c> and
    /// once for the internal <c>get key length</c> operation. That second reading is observable: a
    /// <c>derivedKeyType</c> whose members are getters has each of them called twice, in that order, before
    /// anything else happens, which is what the specification's steps 4 and 6 say and is the same reason
    /// every conversion in this file runs before a single algorithm step does.
    /// </para>
    /// <para>
    /// <c>extractable</c> and <c>keyUsages</c> belong to the <b>derived</b> key alone. The base key is
    /// checked for the <c>deriveKey</c> usage — not <c>deriveBits</c>, even though the bits are what the
    /// derivation produces — and its own extractability is never consulted, which is what lets a
    /// non-extractable PBKDF2 password produce an extractable AES-GCM key.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "deriveKey", Length = 5)]
    private JsValue DeriveKey(
        JsValue thisObject,
        JsValue algorithm,
        JsValue baseKey,
        JsValue derivedKeyType,
        JsValue extractable,
        JsValue keyUsages)
    {
        return Perform(thisObject, CryptoOperation.DeriveKey, what =>
        {
            var identifier = AlgorithmNormalization.ConvertIdentifier(algorithm);
            var cryptoKey = RequireCryptoKey(baseKey, what, "parameter 2");
            var derivedIdentifier = AlgorithmNormalization.ConvertIdentifier(derivedKeyType);
            var isExtractable = TypeConverter.ToBoolean(extractable);
            var usages = KeyUsages.ReadSequence(Context, keyUsages, what);

            // Steps 2 to 7, in the specification's order and all three before any step of any algorithm.
            var normalized = AlgorithmNormalization.Normalize(Context, identifier, CryptoOperation.DeriveBits, what);
            var normalizedImport = AlgorithmNormalization.Normalize(Context, derivedIdentifier, CryptoOperation.ImportKey, what);
            var normalizedLength = AlgorithmNormalization.Normalize(Context, derivedIdentifier, CryptoOperation.GetKeyLength, what);

            // Steps 12 and 13: the base key was made for this algorithm, and it permits deriveKey.
            RequireKeyFor(normalized, cryptoKey, KeyUsage.DeriveKey, "deriveKey", what);

            // Step 14, then step 15.
            var bits = GetKeyLength(normalizedLength, what);
            var secret = Derive(normalized, cryptoKey, bits, what);

            // Steps 16 to 19: the derived bytes are imported as though a script had handed them to importKey
            // in the 'raw' format, so a derived key is exactly as ordinary as an imported one.
            return ImportDerivedKey(normalizedImport, secret, isExtractable, usages, what);
        });
    }

    /// <summary>
    /// "The derive bits operation specified by normalizedAlgorithm using baseKey, algorithm and length" —
    /// the one step <c>deriveBits</c> and <c>deriveKey</c> share.
    /// </summary>
    private byte[] Derive(NormalizedAlgorithm normalized, JsCryptoKey key, uint? length, string what)
    {
        switch (normalized.Name)
        {
            case AlgorithmNormalization.Ecdh:
                return EcAlgorithm.DeriveBits(Context, normalized, key, length, what);
            case AlgorithmNormalization.Hkdf:
                return HkdfAlgorithm.DeriveBits(Context, normalized, key, length, what);
            case AlgorithmNormalization.Pbkdf2:
                return Pbkdf2Algorithm.DeriveBits(Context, normalized, key, length, what);
            default:
                UnhandledAlgorithm(normalized.Name, CryptoOperation.DeriveBits);
                return null!;
        }
    }

    /// <summary>
    /// "The get key length algorithm specified by normalizedDerivedKeyAlgorithmLength using derivedKeyType" —
    /// the internal operation https://w3c.github.io/webcrypto/#algorithm-normalization-internal registers
    /// alongside the script-visible ones.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> is a real answer and not a failure: it is what HKDF and PBKDF2 return, and it
    /// is then handed to the derive operation as the <c>length</c> argument — so
    /// <c>deriveKey(ecdhParams, priv, 'HKDF', false, ['deriveBits'])</c> derives the whole shared secret into
    /// an HKDF key, which is the specification's own worked example, while the same request against a PBKDF2
    /// base key is the <c>OperationError</c> PBKDF2's first derive step gives a null length.
    /// </remarks>
    private uint? GetKeyLength(NormalizedAlgorithm normalized, string what)
    {
        switch (normalized.Name)
        {
            case AlgorithmNormalization.Hmac:
                return HmacAlgorithm.GetKeyLength(Context, normalized, what);
            case AlgorithmNormalization.AesGcm:
                return AesGcmAlgorithm.GetKeyLength(Context, normalized, what);
            case AlgorithmNormalization.Hkdf:
                return HkdfAlgorithm.GetKeyLength();
            case AlgorithmNormalization.Pbkdf2:
                return Pbkdf2Algorithm.GetKeyLength();
            default:
                UnhandledAlgorithm(normalized.Name, CryptoOperation.GetKeyLength);
                return null;
        }
    }

    /// <summary>
    /// "The import key operation specified by normalizedDerivedKeyAlgorithmImport using 'raw' as format,
    /// secret as keyData …" — the tail of <c>deriveKey</c>, which is the body of <c>importKey</c> with the
    /// format fixed and the key data supplied by the derivation rather than by the caller.
    /// </summary>
    /// <remarks>
    /// Only the four algorithms that also register <c>get key length</c> can arrive here, and not the seven
    /// that register <c>importKey</c>: step 6 normalizes the same <c>derivedKeyType</c> for that operation
    /// first, so <c>deriveKey(…, { name: 'RSA-OAEP', hash: 'SHA-256' }, …)</c> is already a
    /// <c>NotSupportedError</c> by the time anything is derived. An asymmetric key cannot be derived at all,
    /// which is the registry saying so rather than any step refusing it. Nothing is special-cased for the
    /// derived case otherwise, so a derived key is the same object an imported one is.
    /// </remarks>
    private JsCryptoKey ImportDerivedKey(NormalizedAlgorithm normalized, byte[] secret, bool extractable, KeyUsage usages, string what)
    {
        switch (normalized.Name)
        {
            case AlgorithmNormalization.Hmac:
                return CreateSecretKey(
                    HmacAlgorithm.ImportKey(Context, KeyFormat.Raw, secret, jwk: null, normalized, extractable, usages, what),
                    extractable,
                    usages,
                    what);

            case AlgorithmNormalization.AesGcm:
                return CreateSecretKey(
                    AesGcmAlgorithm.ImportKey(Context, KeyFormat.Raw, secret, jwk: null, extractable, usages, what),
                    extractable,
                    usages,
                    what);

            case AlgorithmNormalization.Hkdf:
                return CreateImportedKey(
                    HkdfAlgorithm.ImportKey(Context, KeyFormat.Raw, secret, extractable, usages, what),
                    extractable,
                    usages,
                    what);

            case AlgorithmNormalization.Pbkdf2:
                return CreateImportedKey(
                    Pbkdf2Algorithm.ImportKey(Context, KeyFormat.Raw, secret, extractable, usages, what),
                    extractable,
                    usages,
                    what);

            default:
                UnhandledAlgorithm(normalized.Name, CryptoOperation.ImportKey);
                return null!;
        }
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
    /// SyntaxError" — the step <c>generateKey</c> and <c>importKey</c> both end in.
    /// </summary>
    /// <remarks>
    /// The two types the step names are exactly the two that carry material nobody else has: a key nobody may
    /// use is then a mistake rather than a key, because it can never be anything else. A <b>public</b> key is
    /// deliberately outside the check and may carry no usages at all —
    /// <c>importKey('spki', …, false, [])</c> succeeds, which is how a script imports a certificate's key to
    /// read its <c>algorithm</c> without granting it a use, and it is also what makes the public half of a
    /// pair generated with <c>['sign']</c> alone a perfectly ordinary key.
    /// </remarks>
    private void RequireUsableKey(string keyType, KeyUsage usages, string what)
    {
        if (usages != KeyUsage.None || string.Equals(keyType, CryptoKeyTypes.Public, StringComparison.Ordinal))
        {
            return;
        }

        Context.ThrowSyntaxError(what + ": a " + keyType + " key must be created with at least one usage.");
    }

    /// <summary>
    /// The arm of every dispatch below that cannot be reached: normalization matched the name against the
    /// very registry that decides which algorithms an operation has, one call earlier. It throws rather than
    /// falling through to a neighbouring algorithm, so that registering a name without implementing it fails
    /// loudly instead of quietly running the wrong cipher.
    /// </summary>
    private static JsValue UnhandledAlgorithm(string name, CryptoOperation operation)
    {
        Throw.InvalidOperationException(
            "Unhandled algorithm '" + name + "' for the " + AlgorithmNormalization.NameOf(operation) + " operation.");
        return null!;
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
