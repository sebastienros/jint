#if NET8_0_OR_GREATER
using System.Security.Cryptography;
using Jint.Native;
using Jint.Native.Json;
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
/// <b>All twelve operations exist</b>: <c>digest</c>, <c>sign</c>, <c>verify</c>, <c>encrypt</c>,
/// <c>decrypt</c>, <c>generateKey</c>, <c>importKey</c>, <c>exportKey</c>, <c>deriveBits</c>,
/// <c>deriveKey</c>, <c>wrapKey</c> and <c>unwrapKey</c>, over the algorithms <c>HMAC</c>, <c>AES-CTR</c>,
/// <c>AES-CBC</c>, <c>AES-GCM</c> and <c>AES-KW</c> (each at 128, 192 and 256 bits),
/// <c>RSASSA-PKCS1-v1_5</c>, <c>RSA-PSS</c>, <c>RSA-OAEP</c>, <c>ECDSA</c>, <c>ECDH</c>, <c>HKDF</c> and
/// <c>PBKDF2</c> — each of the hashed ones over SHA-1, SHA-256, SHA-384 and SHA-512, and each of the
/// elliptic-curve ones over P-256, P-384 and P-521 — plus those four SHA hashes for <c>digest</c>, and the
/// key formats <c>raw</c>, <c>spki</c>, <c>pkcs8</c> and <c>jwk</c>. An algorithm that is absent for a
/// <i>particular</i> operation is a <c>NotSupportedError</c>, which is what the specification says a name
/// that is not registered for an operation is: <c>sign</c> with <c>AES-GCM</c> fails that way, and so does
/// <c>encrypt</c> with <c>HMAC</c>, <c>generateKey</c> with <c>PBKDF2</c> and <c>encrypt</c> with
/// <c>AES-KW</c>.
/// </para>
/// <para>
/// The registries are per operation and are not symmetric. <c>HKDF</c> and <c>PBKDF2</c> register
/// <c>importKey</c>, <c>deriveBits</c> and the internal <c>get key length</c> and nothing else — there is
/// nothing to <c>generateKey</c> and, their keys being non-extractable by construction, nothing to
/// <c>exportKey</c>. <c>ECDH</c> registers <c>deriveBits</c> and never <c>sign</c>; <c>ECDSA</c> the reverse.
/// <c>AES-KW</c> is the only algorithm registered for <c>wrapKey</c> and <c>unwrapKey</c>, and it registers
/// neither <c>encrypt</c> nor <c>decrypt</c> — the exact reverse of every other cipher, and the reason those
/// two methods normalize twice. Every usage bit now has an operation behind it.
/// </para>
/// <para>
/// <c>deriveKey</c>, <c>wrapKey</c> and <c>unwrapKey</c> are compositions rather than algorithms.
/// <c>deriveKey</c> derives bits with one algorithm and imports them with another, so the key it hands back
/// is exactly the key <c>importKey</c> would have made from the same bytes in the <c>raw</c> format;
/// <c>wrapKey</c> is <c>exportKey</c> followed by a wrap or an encrypt, and <c>unwrapKey</c> is an unwrap or
/// a decrypt followed by <c>importKey</c>. All three reach the very same import and export dispatches the
/// script-visible methods do, so nothing about a key is special-cased for having arrived that way.
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
/// <b>Argument conversion and the method steps are two different times, and every <c>BufferSource</c>
/// parameter is split across both.</b> WebIDL converts the arguments first — that is where a <c>data</c>
/// which is not a buffer source, or one backed by a <c>SharedArrayBuffer</c>, earns its <c>TypeError</c>,
/// and it is why <c>digest('nonsense', 42)</c> rejects for the second argument rather than for the first.
/// <i>Taking the bytes</i> is not part of that: "Let data be the result of getting a copy of the bytes held
/// by the data parameter" is a numbered step of the method, and in every one of these methods it comes
/// <b>after</b> the algorithm has been normalized — step 4 where normalization is step 2 for <c>encrypt</c>,
/// <c>decrypt</c>, <c>sign</c>, <c>digest</c> and <c>importKey</c>, steps 4 and 5 for <c>verify</c>'s two
/// buffers, and step 6 for <c>unwrapKey</c>, which normalizes twice first. Normalization reads the
/// algorithm's <c>name</c>, so it can run a script's getter with the caller's buffer in scope; what that
/// getter leaves behind — rewritten bytes, or a transferred and therefore detached buffer — is what the
/// operation must run over. So <see cref="RequireBufferSource"/> is called with the other conversions and
/// <see cref="CopyBufferSourceBytes"/> at the step that copies, and the two are never collapsed back
/// together.
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
    /// one would — argument conversion runs before a single step of the method body does. The <i>bytes</i>
    /// are a different matter and are step 4's, taken after normalization; see the remarks on this class.
    /// </remarks>
    [JsFunction(Name = "digest", Length = 2)]
    private JsValue Digest(JsValue thisObject, JsValue algorithm, JsValue data)
    {
        return Perform(thisObject, CryptoOperation.Digest, what =>
        {
            var identifier = AlgorithmNormalization.ConvertIdentifier(algorithm);
            RequireBufferSource(data, what, "parameter 2");

            // Step 2.
            var normalized = AlgorithmNormalization.Normalize(Context, identifier, CryptoOperation.Digest, what);

            // Step 4: "Let data be the result of getting a copy of the bytes held by the data parameter
            // passed to the digest() method."
            var message = CopyBufferSourceBytes(data);

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
            RequireBufferSource(data, what, "parameter 3");

            // Step 2.
            var normalized = AlgorithmNormalization.Normalize(Context, identifier, CryptoOperation.Sign, what);

            // Step 4: "Let data be the result of getting a copy of the bytes held by the data parameter
            // passed to the sign() method."
            var message = CopyBufferSourceBytes(data);

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
    /// <remarks>
    /// The only method with <b>two</b> buffer parameters, and the specification gives each its own step:
    /// <c>signature</c> is copied at step 4 and <c>data</c> at step 5, both after normalization and in that
    /// order. Both copies are therefore downstream of a getter on the algorithm's <c>name</c>, so one that
    /// rewrites either buffer is honoured for that one and one that rewrites both is honoured for both. The
    /// order between the two is not observable from script — the copies are consecutive and nothing runs in
    /// between — but it is written the way the steps number it rather than the way the parameters happened to
    /// be converted.
    /// </remarks>
    [JsFunction(Name = "verify", Length = 4)]
    private JsValue Verify(JsValue thisObject, JsValue algorithm, JsValue key, JsValue signature, JsValue data)
    {
        return Perform(thisObject, CryptoOperation.Verify, what =>
        {
            var identifier = AlgorithmNormalization.ConvertIdentifier(algorithm);
            var cryptoKey = RequireCryptoKey(key, what, "parameter 2");
            RequireBufferSource(signature, what, "parameter 3");
            RequireBufferSource(data, what, "parameter 4");

            // Step 2.
            var normalized = AlgorithmNormalization.Normalize(Context, identifier, CryptoOperation.Verify, what);

            // Step 4: "Let signature be the result of getting a copy of the bytes held by the signature
            // parameter passed to the verify() method." Then step 5, the same for data.
            var signatureBytes = CopyBufferSourceBytes(signature);
            var message = CopyBufferSourceBytes(data);

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
            RequireBufferSource(data, what, "parameter 3");

            // Step 2.
            var normalized = AlgorithmNormalization.Normalize(Context, identifier, CryptoOperation.Encrypt, what);

            // Step 4: "Let data be the result of getting a copy of the bytes held by the data parameter
            // passed to the encrypt() method."
            var plaintext = CopyBufferSourceBytes(data);

            RequireKeyFor(normalized, cryptoKey, KeyUsage.Encrypt, "encrypt", what);

            return Context.CreateArrayBuffer(PerformEncrypt(normalized, cryptoKey, plaintext, what));
        });
    }

    /// <summary>
    /// "The encrypt operation specified by normalizedAlgorithm using key and algorithm" — the step
    /// <c>encrypt</c> shares with <c>wrapKey</c>, which reaches four of these five algorithms through its own
    /// second normalization.
    /// </summary>
    private byte[] PerformEncrypt(NormalizedAlgorithm normalized, JsCryptoKey key, byte[] plaintext, string what)
    {
        switch (normalized.Name)
        {
            case AlgorithmNormalization.AesCtr:
                return AesCtrAlgorithm.Encrypt(Context, normalized, key, plaintext, what);
            case AlgorithmNormalization.AesCbc:
                return AesCbcAlgorithm.Encrypt(Context, normalized, key, plaintext, what);
            case AlgorithmNormalization.AesGcm:
                return AesGcmAlgorithm.Encrypt(Context, normalized, key, plaintext, what);
            case AlgorithmNormalization.RsaOaep:
                return RsaAlgorithm.Encrypt(Context, normalized, key, plaintext, what);
            default:
                UnhandledAlgorithm(normalized.Name, CryptoOperation.Encrypt);
                return null!;
        }
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
            RequireBufferSource(data, what, "parameter 3");

            // Step 2.
            var normalized = AlgorithmNormalization.Normalize(Context, identifier, CryptoOperation.Decrypt, what);

            // Step 4: "Let data be the result of getting a copy of the bytes held by the data parameter
            // passed to the decrypt() method."
            var ciphertext = CopyBufferSourceBytes(data);

            RequireKeyFor(normalized, cryptoKey, KeyUsage.Decrypt, "decrypt", what);

            return Context.CreateArrayBuffer(PerformDecrypt(normalized, cryptoKey, ciphertext, what));
        });
    }

    /// <summary>
    /// "The decrypt operation specified by normalizedAlgorithm using key and algorithm" — the step
    /// <c>decrypt</c> shares with <c>unwrapKey</c>.
    /// </summary>
    private byte[] PerformDecrypt(NormalizedAlgorithm normalized, JsCryptoKey key, byte[] ciphertext, string what)
    {
        switch (normalized.Name)
        {
            case AlgorithmNormalization.AesCtr:
                return AesCtrAlgorithm.Decrypt(Context, normalized, key, ciphertext, what);
            case AlgorithmNormalization.AesCbc:
                return AesCbcAlgorithm.Decrypt(Context, normalized, key, ciphertext, what);
            case AlgorithmNormalization.AesGcm:
                return AesGcmAlgorithm.Decrypt(Context, normalized, key, ciphertext, what);
            case AlgorithmNormalization.RsaOaep:
                return RsaAlgorithm.Decrypt(Context, normalized, key, ciphertext, what);
            default:
                UnhandledAlgorithm(normalized.Name, CryptoOperation.Decrypt);
                return null!;
        }
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

                case AlgorithmNormalization.AesCtr:
                case AlgorithmNormalization.AesCbc:
                case AlgorithmNormalization.AesGcm:
                case AlgorithmNormalization.AesKw:
                    return CreateSecretKey(
                        AesKeyManagement.GenerateKey(Context, normalized.Name, normalized, usages, what),
                        isExtractable,
                        usages,
                        what);

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
    /// normalized.
    /// <para>
    /// Step 4 is where the two arms are held to the format, and — for the buffer-source arm alone — where the
    /// bytes are taken: "Otherwise: … Let keyData be the result of getting a copy of the bytes held by the
    /// keyData parameter". That is <i>after</i> normalization, so a getter on the algorithm's <c>name</c> that
    /// rewrites the key material is honoured and the key imported is the one those bytes describe. The JWK arm
    /// has no such step, because the dictionary was materialized at conversion time and holds no live window
    /// onto anything.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "importKey", Length = 5)]
    private JsValue ImportKey(JsValue thisObject, JsValue format, JsValue keyData, JsValue algorithm, JsValue extractable, JsValue keyUsages)
    {
        return Perform(thisObject, CryptoOperation.ImportKey, what =>
        {
            var keyFormat = ReadKeyFormat(format, what);
            var jwk = ConvertKeyData(keyData, what);
            var identifier = AlgorithmNormalization.ConvertIdentifier(algorithm);
            var isExtractable = TypeConverter.ToBoolean(extractable);
            var usages = KeyUsages.ReadSequence(Context, keyUsages, what);

            // Step 2.
            var normalized = AlgorithmNormalization.Normalize(Context, identifier, CryptoOperation.ImportKey, what);

            // Step 4, after normalization as the numbering says, and both of its arms in full.
            byte[]? rawData = null;

            if (keyFormat == KeyFormat.Jwk)
            {
                if (jwk is null)
                {
                    Context.ThrowTypeError(what + ": the 'jwk' format needs a JsonWebKey object, not a buffer source.");
                }
            }
            else
            {
                if (jwk is not null)
                {
                    Context.ThrowTypeError(what + ": the '" + KeyFormats.NameOf(keyFormat) + "' format needs a buffer source, not a JsonWebKey object.");
                }

                rawData = CopyBufferSourceBytes(keyData);
            }

            return PerformImportKey(normalized, keyFormat, rawData, jwk, isExtractable, usages, what);
        });
    }

    /// <summary>
    /// "The import key operation specified by normalizedAlgorithm using keyData, algorithm, format,
    /// extractable and usages" — the step <c>importKey</c> shares with <c>deriveKey</c> (whose format is
    /// fixed at <c>raw</c> and whose key data is the derivation's) and with <c>unwrapKey</c> (whose key data
    /// is whatever the unwrapping produced).
    /// </summary>
    private JsCryptoKey PerformImportKey(
        NormalizedAlgorithm normalized,
        KeyFormat format,
        byte[]? rawData,
        JsonWebKeyData? jwk,
        bool extractable,
        KeyUsage usages,
        string what)
    {
        switch (normalized.Name)
        {
            case AlgorithmNormalization.Hmac:
                return CreateSecretKey(
                    HmacAlgorithm.ImportKey(Context, format, rawData, jwk, normalized, extractable, usages, what),
                    extractable,
                    usages,
                    what);

            case AlgorithmNormalization.AesCtr:
            case AlgorithmNormalization.AesCbc:
            case AlgorithmNormalization.AesGcm:
            case AlgorithmNormalization.AesKw:
                return CreateSecretKey(
                    AesKeyManagement.ImportKey(Context, normalized.Name, format, rawData, jwk, extractable, usages, what),
                    extractable,
                    usages,
                    what);

            case AlgorithmNormalization.RsassaPkcs1V15:
            case AlgorithmNormalization.RsaPss:
            case AlgorithmNormalization.RsaOaep:
                return CreateImportedKey(
                    RsaAlgorithm.ImportKey(Context, format, rawData, jwk, normalized, extractable, usages, what),
                    extractable,
                    usages,
                    what);

            case AlgorithmNormalization.Ecdsa:
            case AlgorithmNormalization.Ecdh:
                return CreateImportedKey(
                    EcAlgorithm.ImportKey(Context, format, rawData, jwk, normalized, extractable, usages, what),
                    extractable,
                    usages,
                    what);

            case AlgorithmNormalization.Hkdf:
                return CreateImportedKey(
                    HkdfAlgorithm.ImportKey(Context, format, rawData, extractable, usages, what),
                    extractable,
                    usages,
                    what);

            case AlgorithmNormalization.Pbkdf2:
                return CreateImportedKey(
                    Pbkdf2Algorithm.ImportKey(Context, format, rawData, extractable, usages, what),
                    extractable,
                    usages,
                    what);

            default:
                UnhandledAlgorithm(normalized.Name, CryptoOperation.ImportKey);
                return null!;
        }
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

            RequireExportableKey(cryptoKey, what);

            return PerformExportKey(cryptoKey, keyFormat, what);
        });
    }

    /// <summary>
    /// The two checks <c>exportKey</c> makes before asking the algorithm for anything, which <c>wrapKey</c>
    /// makes too (its steps 9 and 10) because wrapping "effectively exports the key".
    /// </summary>
    private void RequireExportableKey(JsCryptoKey key, string what)
    {
        // "If the name member of the [[algorithm]] internal slot of key does not identify a registered
        // algorithm that supports the export key operation, then throw a NotSupportedError."
        if (Array.IndexOf(AlgorithmNormalization.RegisteredFor(CryptoOperation.ExportKey), key.Algorithm.Name) < 0)
        {
            Context.ThrowNotSupportedError(
                what + ": " + key.Algorithm.Name + " is not registered for the exportKey operation.");
        }

        // "If the [[extractable]] internal slot of key is false, then throw an InvalidAccessError."
        if (!key.Extractable)
        {
            Context.ThrowInvalidAccessError(what + ": the key is not extractable.");
        }
    }

    /// <summary>
    /// "The export key operation specified by the [[algorithm]] internal slot of key using key and format" —
    /// the step <c>exportKey</c> shares with <c>wrapKey</c>, which wraps whatever it produces.
    /// </summary>
    private JsValue PerformExportKey(JsCryptoKey key, KeyFormat format, string what)
    {
        switch (key.Algorithm.Name)
        {
            case AlgorithmNormalization.Hmac:
                return HmacAlgorithm.ExportKey(Context, key, format, what);
            case AlgorithmNormalization.AesCtr:
            case AlgorithmNormalization.AesCbc:
            case AlgorithmNormalization.AesGcm:
            case AlgorithmNormalization.AesKw:
                return AesKeyManagement.ExportKey(Context, key, format, what);
            case AlgorithmNormalization.RsassaPkcs1V15:
            case AlgorithmNormalization.RsaPss:
            case AlgorithmNormalization.RsaOaep:
                return RsaAlgorithm.ExportKey(Context, key, format, what);
            case AlgorithmNormalization.Ecdsa:
            case AlgorithmNormalization.Ecdh:
                return EcAlgorithm.ExportKey(Context, key, format, what);
            default:
                return UnhandledAlgorithm(key.Algorithm.Name, CryptoOperation.ExportKey);
        }
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
    /// https://w3c.github.io/webcrypto/#SubtleCrypto-method-wrapKey, whose IDL is
    /// <c>Promise&lt;ArrayBuffer&gt; wrapKey(KeyFormat format, CryptoKey key, CryptoKey wrappingKey,
    /// AlgorithmIdentifier wrapAlgorithm)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Like <c>deriveKey</c> it is a composition, and its parts are already here: it is <c>exportKey</c>
    /// followed by either a <b>wrap key</b> operation or an <b>encrypt</b> one. Which of the two is decided by
    /// the <b>double normalization</b> of step 2 — the <c>wrapAlgorithm</c> is normalized for <c>wrapKey</c>
    /// and, "if an error occurred", for <c>encrypt</c> instead. AES-KW takes the first route because that is
    /// the only operation its registration names; AES-GCM, AES-CBC, AES-CTR and RSA-OAEP take the second,
    /// because theirs name <c>encrypt</c> and never <c>wrapKey</c>. A name that is in neither registry is
    /// therefore reported against the <i>encrypt</i> registry, which is the normalization that ran last, and
    /// an algorithm object whose members are getters has them read twice for the same reason
    /// <c>deriveKey</c>'s <c>derivedKeyType</c> is read twice.
    /// </para>
    /// <para>
    /// <b>Wrapping is exporting</b>, so the two checks <c>exportKey</c> makes are made here too: the wrapped
    /// key's algorithm has to be registered for <c>exportKey</c>, and the key has to be extractable. The
    /// specification's own note is worth repeating — "this API cannot create a wrapped JWK key that is marked
    /// as non-extractable using the ext JWK member. However, the unwrapKey method does support the ext JWK
    /// member", which is what lets a server hand a script a wrapped key it can use and cannot export.
    /// </para>
    /// <para>
    /// <b>The <c>jwk</c> format is serialized with this engine's own <c>JSON.stringify</c></b> — "the result
    /// of representing exportedKey as a UTF-16 string conforming to the JSON grammar; for example, by
    /// executing the JSON.stringify algorithm specified in [ECMA-262] in the context of a new global object",
    /// then UTF-8 encoded. The member order is the export layout's own (<see cref="JsonWebKeyData"/> builds
    /// every JWK from a fixed <see cref="JsObjectLayout"/>, and a layout's order is its enumeration order), so
    /// the bytes a given key wraps to are deterministic and equal to
    /// <c>new TextEncoder().encode(JSON.stringify(await crypto.subtle.exportKey('jwk', key)))</c>. The one
    /// divergence from the quoted step is the phrase "a new global object": the serialization runs in this
    /// realm, so a <c>toJSON</c> a script has installed on <c>Object.prototype</c> participates in it where a
    /// browser's would not. What that can do is confined to the script's own keys — it changes bytes the same
    /// script then fails to unwrap — and it is named here rather than papered over.
    /// </para>
    /// <para>
    /// <b>A JWK wrapped under AES-KW is padded with spaces to a multiple of 8 bytes.</b> AES-KW wraps a whole
    /// number of 64-bit blocks and a JSON document is whatever length it is, which is exactly the case the
    /// specification's note anticipates: "implementations may choose to adapt the serialization to the
    /// constraints of the wrapping algorithm. This is why JSON.stringify is not normatively required, as
    /// otherwise it would prohibit implementations from introducing added padding." The convention every
    /// implementation follows — and the one the web-platform tests compute their expectation with, in
    /// <c>WebCryptoAPI/wrapKey_unwrapKey/wrapKey_unwrapKey.https.any.js</c> — is
    /// <c>jwk.slice(0, -1) + ' '.repeat(pad) + '}'</c>: the spaces go immediately before the closing brace,
    /// where JSON's grammar allows insignificant whitespace, so the padded document parses back to the very
    /// same object and <c>unwrapKey</c> needs to know nothing about the padding at all. It is applied to the
    /// UTF-8 bytes rather than to the UTF-16 string, which is the same thing for every JWK this engine
    /// exports — all of them are ASCII — and is the count AES-KW actually constrains.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "wrapKey", Length = 4)]
    private JsValue WrapKey(JsValue thisObject, JsValue format, JsValue key, JsValue wrappingKey, JsValue wrapAlgorithm)
    {
        return Perform(thisObject, CryptoOperation.WrapKey, what =>
        {
            var keyFormat = ReadKeyFormat(format, what);
            var cryptoKey = RequireCryptoKey(key, what, "parameter 2");
            var wrapper = RequireCryptoKey(wrappingKey, what, "parameter 3");
            var identifier = AlgorithmNormalization.ConvertIdentifier(wrapAlgorithm);

            // Steps 2 and 3.
            var normalized = NormalizeForWrapping(identifier, CryptoOperation.WrapKey, CryptoOperation.Encrypt, what);

            // Steps 7 and 8: the wrapping key was made for this algorithm, and it permits wrapKey.
            RequireKeyFor(normalized, wrapper, KeyUsage.WrapKey, "wrapKey", what);

            // Steps 9 and 10, which are exportKey's own two checks — see the remarks on this method.
            RequireExportableKey(cryptoKey, what);

            // Steps 11 and 12.
            var exported = PerformExportKey(cryptoKey, keyFormat, what);
            var bytes = keyFormat == KeyFormat.Jwk
                ? SerializeJsonWebKey(exported, normalized.Name)
                : CopyExportedBytes(exported);

            // Step 13.
            if (IsRegisteredFor(CryptoOperation.WrapKey, normalized.Name))
            {
                return Context.CreateArrayBuffer(PerformWrap(normalized, wrapper, bytes, what));
            }

            if (IsRegisteredFor(CryptoOperation.Encrypt, normalized.Name))
            {
                return Context.CreateArrayBuffer(PerformEncrypt(normalized, wrapper, bytes, what));
            }

            // "Otherwise: throw a NotSupportedError." Unreachable: the normalization above succeeded against
            // one of these two registries, and it is written out because the step is.
            Context.ThrowNotSupportedError(
                what + ": " + normalized.Name + " supports neither the wrapKey operation nor the encrypt operation.");
            return JsValue.Undefined;
        });
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#SubtleCrypto-method-unwrapKey, whose IDL is
    /// <c>Promise&lt;CryptoKey&gt; unwrapKey(KeyFormat format, BufferSource wrappedKey,
    /// CryptoKey unwrappingKey, AlgorithmIdentifier unwrapAlgorithm,
    /// AlgorithmIdentifier unwrappedKeyAlgorithm, boolean extractable, sequence&lt;KeyUsage&gt; keyUsages)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mirror of <c>wrapKey</c>, with <b>three</b> normalizations rather than two: the
    /// <c>unwrapAlgorithm</c> for <c>unwrapKey</c> and then, on failure, for <c>decrypt</c>; and the
    /// <c>unwrappedKeyAlgorithm</c> for <c>importKey</c>, which is the algorithm the <i>new</i> key will
    /// belong to. The two are independent — an AES-KW key unwraps an RSA private key perfectly well — and it
    /// is the second that decides everything about the key that comes out.
    /// </para>
    /// <para>
    /// <c>extractable</c> and <c>keyUsages</c> belong to the <b>new</b> key alone, exactly as
    /// <c>deriveKey</c>'s do, and the unwrapping key's own extractability is never consulted. A <c>jwk</c>
    /// carrying <c>ext: false</c> is honoured against them, which is the asymmetry <c>wrapKey</c>'s note
    /// describes: a key wrapped elsewhere may say it is not to be extractable, and this is where that is read.
    /// </para>
    /// <para>
    /// <b>Everything that can go wrong after the unwrapping is an import failure, in the existing taxonomy.</b>
    /// The unwrapped bytes are handed to the very same import steps <c>importKey</c> runs, so a JWK whose
    /// <c>kty</c> is wrong for the requested algorithm is a <c>DataError</c>, a usage the algorithm does not
    /// support is a <c>SyntaxError</c>, and a secret key with no usages at all is the <c>SyntaxError</c> step
    /// 14 names. The one failure that is unwrapping's own is a <c>jwk</c> whose bytes are not a JSON document
    /// at all — "parse a JWK" is where that becomes a <c>DataError</c>, and it is the shape a wrong
    /// unwrapping key produces under a cipher with no integrity check of its own.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "unwrapKey", Length = 7)]
    private JsValue UnwrapKey(
        JsValue thisObject,
        JsValue format,
        JsValue wrappedKey,
        JsValue unwrappingKey,
        JsValue unwrapAlgorithm,
        JsValue unwrappedKeyAlgorithm,
        JsValue extractable,
        JsValue keyUsages)
    {
        return Perform(thisObject, CryptoOperation.UnwrapKey, what =>
        {
            var keyFormat = ReadKeyFormat(format, what);
            RequireBufferSource(wrappedKey, what, "parameter 2");
            var unwrapper = RequireCryptoKey(unwrappingKey, what, "parameter 3");
            var identifier = AlgorithmNormalization.ConvertIdentifier(unwrapAlgorithm);
            var keyIdentifier = AlgorithmNormalization.ConvertIdentifier(unwrappedKeyAlgorithm);
            var isExtractable = TypeConverter.ToBoolean(extractable);
            var usages = KeyUsages.ReadSequence(Context, keyUsages, what);

            // Steps 2 to 5, in the specification's order and all before any step of any algorithm.
            var normalized = NormalizeForWrapping(identifier, CryptoOperation.UnwrapKey, CryptoOperation.Decrypt, what);
            var normalizedKey = AlgorithmNormalization.Normalize(Context, keyIdentifier, CryptoOperation.ImportKey, what);

            // Step 6: "Let wrappedKey be the result of getting a copy of the bytes held by the wrappedKey
            // parameter passed to the unwrapKey() method." It comes after both normalizations, so a getter on
            // either algorithm's `name` — and this method reads two of them — is still in time to be seen.
            var wrapped = CopyBufferSourceBytes(wrappedKey);

            // Steps 9 and 10.
            RequireKeyFor(normalized, unwrapper, KeyUsage.UnwrapKey, "unwrapKey", what);

            // Step 11.
            byte[] bytes;
            if (IsRegisteredFor(CryptoOperation.UnwrapKey, normalized.Name))
            {
                bytes = PerformUnwrap(normalized, unwrapper, wrapped, what);
            }
            else if (IsRegisteredFor(CryptoOperation.Decrypt, normalized.Name))
            {
                bytes = PerformDecrypt(normalized, unwrapper, wrapped, what);
            }
            else
            {
                // Unreachable, for the reason wrapKey's own third arm is.
                Context.ThrowNotSupportedError(
                    what + ": " + normalized.Name + " supports neither the unwrapKey operation nor the decrypt operation.");
                return JsValue.Undefined;
            }

            // Step 12: "If format is equal to the string 'jwk': Let key be the result of executing the parse
            // a JWK algorithm, with bytes as the data to be parsed. Otherwise: Let key be bytes."
            var jwk = keyFormat == KeyFormat.Jwk ? ParseJsonWebKey(bytes, what) : null;
            var rawData = keyFormat == KeyFormat.Jwk ? null : bytes;

            // Steps 13 to 16. The empty-usages SyntaxError of step 14, and the setting of the two internal
            // slots, are what the import dispatch already does for importKey and deriveKey.
            return PerformImportKey(normalizedKey, keyFormat, rawData, jwk, isExtractable, usages, what);
        });
    }

    /// <summary>
    /// Step 2 of <c>wrapKey</c> and <c>unwrapKey</c>: normalize for the wrapping operation and, "if an error
    /// occurred", for the cipher operation instead.
    /// </summary>
    /// <remarks>
    /// The three exceptions caught are the ones <see cref="Perform"/> catches, for the same reason it catches
    /// exactly those: they are the whole of what a script-visible failure here can arrive as, and everything
    /// else a <c>JintException</c> covers is an execution constraint or a cancellation, which must not be
    /// turned into a second attempt any more than into a rejection. Whatever the <i>second</i> normalization
    /// raises is left to propagate, which is what makes it the failure the caller sees.
    /// </remarks>
    private NormalizedAlgorithm NormalizeForWrapping(
        JsValue identifier,
        CryptoOperation wrapping,
        CryptoOperation cipher,
        string what)
    {
        try
        {
            return AlgorithmNormalization.Normalize(Context, identifier, wrapping, what);
        }
        catch (Exception e) when (e is JavaScriptException or TypeErrorException or RangeErrorException)
        {
            return AlgorithmNormalization.Normalize(Context, identifier, cipher, what);
        }
    }

    /// <summary>"If normalizedAlgorithm supports the wrap key operation" — a lookup in that operation's registry.</summary>
    private static bool IsRegisteredFor(CryptoOperation operation, string name)
        => Array.IndexOf(AlgorithmNormalization.RegisteredFor(operation), name) >= 0;

    /// <summary>
    /// "The wrap key operation specified by normalizedAlgorithm using wrappingKey as key and bytes as
    /// plaintext" — which only AES-KW registers.
    /// </summary>
    private byte[] PerformWrap(NormalizedAlgorithm normalized, JsCryptoKey key, byte[] plaintext, string what)
    {
        switch (normalized.Name)
        {
            case AlgorithmNormalization.AesKw:
                return AesKwAlgorithm.WrapKey(Context, key, plaintext, what);
            default:
                UnhandledAlgorithm(normalized.Name, CryptoOperation.WrapKey);
                return null!;
        }
    }

    /// <summary>
    /// "The unwrap key operation specified by normalizedAlgorithm using unwrappingKey as key and wrappedKey
    /// as ciphertext".
    /// </summary>
    private byte[] PerformUnwrap(NormalizedAlgorithm normalized, JsCryptoKey key, byte[] ciphertext, string what)
    {
        switch (normalized.Name)
        {
            case AlgorithmNormalization.AesKw:
                return AesKwAlgorithm.UnwrapKey(Context, key, ciphertext, what);
            default:
                UnhandledAlgorithm(normalized.Name, CryptoOperation.UnwrapKey);
                return null!;
        }
    }

    /// <summary>
    /// Step 12 of <c>wrapKey</c> for the <c>jwk</c> format — see the remarks on that method for both the
    /// serializer this uses and the padding AES-KW gets.
    /// </summary>
    private byte[] SerializeJsonWebKey(JsValue exported, string wrapAlgorithmName)
    {
        if (new JsonSerializer(_engine).Serialize(exported) is not JsString json)
        {
            // Unreachable: the value is an object this engine built out of strings, booleans and an array of
            // strings, none of which JSON.stringify answers `undefined` for.
            Throw.InvalidOperationException("The exported JSON Web Key has no JSON representation.");
            return null!;
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(json.ToString());

        return string.Equals(wrapAlgorithmName, AlgorithmNormalization.AesKw, StringComparison.Ordinal)
            ? PadToWrappableLength(bytes)
            : bytes;
    }

    /// <summary>
    /// The JSON document with enough <c>U+0020</c> characters inserted before its closing brace to make it a
    /// whole number of 64-bit blocks — see the remarks on <see cref="WrapKey"/> for where the convention
    /// comes from and why it round-trips.
    /// </summary>
    private static byte[] PadToWrappableLength(byte[] json)
    {
        var remainder = json.Length % 8;
        if (remainder == 0)
        {
            return json;
        }

        var padded = new byte[json.Length + (8 - remainder)];

        // Everything but the closing brace, then the spaces, then the brace back on the end.
        json.AsSpan(0, json.Length - 1).CopyTo(padded);
        padded.AsSpan(json.Length - 1, 8 - remainder).Fill((byte) ' ');
        padded[padded.Length - 1] = json[json.Length - 1];

        return padded;
    }

    /// <summary>
    /// Step 12's "Otherwise: Let bytes be exportedKey" — the bytes of the <c>ArrayBuffer</c> the export
    /// produced, which for every non-<c>jwk</c> format is what it returns.
    /// </summary>
    private static byte[] CopyExportedBytes(JsValue exported)
    {
        if (!BufferSource.TryGetBytes(exported, out var bytes))
        {
            // Unreachable: every format but jwk exports an ArrayBuffer, which is what the export dispatch
            // above hands back.
            Throw.InvalidOperationException("The exported key is not a buffer source.");
        }

        return bytes.ToArray();
    }

    /// <summary>
    /// "Parse a JWK" — https://w3c.github.io/webcrypto/#dfn-parse-a-jwk: interpret the bytes as UTF-8, parse
    /// the text as JSON, convert the result to a <c>JsonWebKey</c> dictionary, "If the kty field of key is
    /// not defined, then throw a DataError".
    /// </summary>
    /// <remarks>
    /// The parser is the engine's own <c>JSON.parse</c>, which reports a malformed document — and a byte
    /// sequence that is not well-formed UTF-8 — as a <c>SyntaxError</c>. That is turned into the
    /// <c>DataError</c> this algorithm names: a script asking to unwrap a key never asked to parse JSON, and
    /// what it is being told is that the data does not meet the operation's requirements. Nothing else on
    /// this path raises a <c>SyntaxError</c>, so the conversion cannot capture anything it did not mean to.
    /// </remarks>
    private JsonWebKeyData ParseJsonWebKey(byte[] bytes, string what)
    {
        JsValue parsed;

        try
        {
            parsed = new JsonParser(_engine).Parse(bytes);
        }
        catch (JavaScriptException)
        {
            Context.ThrowDataError(what + ": the unwrapped data is not a JSON document.");
            return null!;
        }

        if (parsed is not ObjectInstance source)
        {
            Context.ThrowDataError(what + ": the unwrapped data is a JSON document but not a JSON Web Key object.");
            return null!;
        }

        var jwk = JsonWebKeyData.Read(Context, source, what);

        if (jwk.Kty is null)
        {
            Context.ThrowDataError(what + ": the unwrapped JSON Web Key has no kty field.");
        }

        return jwk;
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
            case AlgorithmNormalization.AesCtr:
            case AlgorithmNormalization.AesCbc:
            case AlgorithmNormalization.AesGcm:
            case AlgorithmNormalization.AesKw:
                return AesKeyManagement.GetKeyLength(Context, normalized, what);
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
    /// Only the algorithms that also register <c>get key length</c> can arrive here, and not the twelve that
    /// register <c>importKey</c>: step 6 normalizes the same <c>derivedKeyType</c> for that operation first,
    /// so <c>deriveKey(…, { name: 'RSA-OAEP', hash: 'SHA-256' }, …)</c> is already a
    /// <c>NotSupportedError</c> by the time anything is derived. An asymmetric key cannot be derived at all,
    /// which is the registry saying so rather than any step refusing it. Nothing is special-cased for the
    /// derived case otherwise, so a derived key is the same object an imported one is — which is exactly why
    /// this delegates to the very dispatch <c>importKey</c> uses rather than keeping a second, shorter list
    /// that would have to be remembered whenever an algorithm is added.
    /// </remarks>
    private JsCryptoKey ImportDerivedKey(NormalizedAlgorithm normalized, byte[] secret, bool extractable, KeyUsage usages, string what)
        => PerformImportKey(normalized, KeyFormat.Raw, secret, jwk: null, extractable, usages, what);

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
    /// The <c>(BufferSource or JsonWebKey)</c> union conversion, which is <c>importKey</c>'s alone.
    /// <see langword="null"/> is the answer for the <c>BufferSource</c> arm.
    /// </summary>
    /// <remarks>
    /// <c>null</c> and <c>undefined</c> become a <c>JsonWebKey</c> with every member absent, which is what
    /// WebIDL does for a union containing a dictionary type; a number, a string or a boolean is a
    /// <c>TypeError</c>, because converting a non-object to a dictionary is one.
    /// <para>
    /// Only the <i>arm</i> is decided here. The bytes of the <c>BufferSource</c> arm are not taken, because
    /// taking them is step 4 of <c>importKey</c> and runs after normalization — which is why this answers
    /// with the dictionary or with nothing, rather than with one of two payloads.
    /// </para>
    /// </remarks>
    private JsonWebKeyData? ConvertKeyData(JsValue keyData, string what)
    {
        if (BufferSource.IsBufferSource(keyData))
        {
            if (AlgorithmNormalization.IsSharedBufferSource(keyData))
            {
                Context.ThrowTypeError(what + ": parameter 2 is backed by a SharedArrayBuffer, which this operation does not accept.");
            }

            return null;
        }

        if (keyData.IsNull() || keyData.IsUndefined())
        {
            return new JsonWebKeyData();
        }

        if (keyData is ObjectInstance source)
        {
            return JsonWebKeyData.Read(Context, source, what);
        }

        Context.ThrowTypeError(what + ": parameter 2 is neither a BufferSource nor a JsonWebKey object.");
        return null;
    }

    /// <summary>
    /// WebIDL's conversion of a <c>BufferSource</c> argument, which is
    /// <c>(ArrayBufferView or ArrayBuffer)</c> — https://webidl.spec.whatwg.org/#BufferSource. This is the
    /// conversion and nothing else: it decides the type and refuses a shared buffer, and takes no bytes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It runs with the other argument conversions, before a single step of the method body, which is what
    /// makes <c>digest('nonsense', 42)</c> a <c>TypeError</c> rather than a <c>NotSupportedError</c>. The
    /// bytes are <see cref="CopyBufferSourceBytes"/>'s, at the numbered step that copies them.
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
    /// the ones the view spans at the moment the copy step runs, so the answer is right rather than merely
    /// tolerated, and no engine an embedder is likely to be replacing refuses it.
    /// </para>
    /// </remarks>
    private void RequireBufferSource(JsValue data, string what, string parameter)
    {
        if (!BufferSource.IsBufferSource(data))
        {
            Context.ThrowTypeError(what + ": " + parameter + " is not of type 'BufferSource'.");
        }

        if (AlgorithmNormalization.IsSharedBufferSource(data))
        {
            Context.ThrowTypeError(what + ": " + parameter + " is backed by a SharedArrayBuffer, which this operation does not accept.");
        }
    }

    /// <summary>
    /// "Getting a copy of the bytes held by" a buffer source —
    /// https://webidl.spec.whatwg.org/#dfn-get-buffer-source-copy — which every one of these methods performs
    /// as a numbered step of its own, after the algorithm has been normalized.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The copy is real, and where it is taken from is the whole point: normalization reads the algorithm's
    /// <c>name</c> and may run a script's getter with this very buffer in scope, so the bytes are read
    /// <i>now</i> rather than carried over from the conversion. A getter that rewrites them is honoured, and
    /// one that transfers the buffer leaves a detached view, whose copy is the empty byte sequence — which
    /// is what https://webidl.spec.whatwg.org/#dfn-get-buffer-source-copy step 7 says it should be.
    /// </para>
    /// <para>
    /// After the copy the array is the operation's own, so a mutation the script makes once the call has
    /// returned changes nothing — that half was never in question and is what the corpus's "… after call"
    /// rows assert.
    /// </para>
    /// </remarks>
    private static byte[] CopyBufferSourceBytes(JsValue data)
    {
        // The conversion proved this is a buffer source, and nothing a getter can do takes that away: a
        // detach or a resize changes the bytes on offer, never the type of the object holding them.
        BufferSource.TryGetBytes(data, out var bytes);
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
