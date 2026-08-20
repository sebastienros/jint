#if NET8_0_OR_GREATER
#nullable enable

using System.Security.Cryptography;
using Jint;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The keyed <c>crypto.subtle</c> operations seen from outside the assembly: what a host has to write to get
/// them, what it can hand a script, what it can read back, and what it cannot read at all.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party and every
/// assertion goes through script or the public options surface, exactly as an embedder's would. The
/// interoperability assertions are the point of the file: a MAC a script produced has to equal the one the
/// host computes with <see cref="HMACSHA256"/>, and a ciphertext a script produced has to be one the host can
/// open with <see cref="AesGcm"/> — otherwise the API is only self-consistent.
/// </remarks>
public class WebApiCryptoKeyTests
{
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Crypto));

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    [Fact]
    public void ADefaultEngineHasNoKeyedCrypto()
    {
        var engine = new Engine();

        engine.Evaluate("typeof crypto").AsString().Should().Be("undefined");
        engine.Evaluate("typeof CryptoKey").AsString().Should().Be("undefined");
        engine.Evaluate("'CryptoKey' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void TheCryptoFlagIsWhatInstallsThem()
    {
        WebEngine().Evaluate("typeof crypto.subtle.generateKey").AsString().Should().Be("function");
        WebEngine().Evaluate("typeof CryptoKey").AsString().Should().Be("function");

        new Engine(options => options.UseWebApis(WebApiFeatures.Console))
            .Evaluate("typeof CryptoKey").AsString().Should().Be("undefined");

        new Options().UseWebApis().WebApi.Features.Should().HaveFlag(WebApiFeatures.Crypto);
    }

    [Fact]
    public void AScriptsMacIsTheOneTheHostComputes()
    {
        var engine = WebEngine();

        var keyMaterial = new byte[32];
        for (var i = 0; i < keyMaterial.Length; i++)
        {
            keyMaterial[i] = (byte) (i * 7);
        }

        var message = "the message the host and the script agree on"u8.ToArray();

        engine.SetValue("keyMaterial", keyMaterial);
        engine.SetValue("message", message);

        var fromScript = engine.Evaluate("""
            (async () => {
                const key = await crypto.subtle.importKey('raw', new Uint8Array(keyMaterial), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
                const mac = await crypto.subtle.sign('HMAC', key, new Uint8Array(message));
                return Array.from(new Uint8Array(mac)).map(b => b.toString(16).padStart(2, '0')).join('');
            })()
            """).UnwrapIfPromise().AsString();

        fromScript.Should().Be(Hex(HMACSHA256.HashData(keyMaterial, message)));
    }

    [Fact]
    public void AHostCanVerifyWithTheKeyMaterialItExported()
    {
        var engine = WebEngine();

        // The other direction, and the one an embedder actually needs: the script mints the key, hands the
        // host the exported bytes, and the host checks a signature with its own primitives.
        var exported = engine.Evaluate("""
            (async () => {
                const hex = b => Array.from(new Uint8Array(b)).map(x => x.toString(16).padStart(2, '0')).join('');
                const key = await crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-512', length: 256 }, true, ['sign']);
                const raw = await crypto.subtle.exportKey('raw', key);
                const mac = await crypto.subtle.sign('HMAC', key, Uint8Array.from('payload', c => c.charCodeAt(0)));
                return hex(raw) + ':' + hex(mac);
            })()
            """).UnwrapIfPromise().AsString().Split(':');

        var raw = Convert.FromHexString(exported[0]);
        var mac = Convert.FromHexString(exported[1]);

        raw.Should().HaveCount(32);
        mac.Should().HaveCount(64);
        HMACSHA512.HashData(raw, "payload"u8.ToArray()).Should().Equal(mac);
    }

    [Fact]
    public void AHostCanOpenWhatAScriptEncrypted()
    {
        var engine = WebEngine();

        // The specification's layout is ciphertext || tag, and AesGcm wants them apart — which is the one
        // thing an embedder has to know to read a script's output. Nothing else is needed: no headers, no
        // framing, no length prefix.
        var key = new byte[16];
        RandomNumberGenerator.Fill(key);
        var iv = new byte[12];
        RandomNumberGenerator.Fill(iv);

        engine.SetValue("keyMaterial", key);
        engine.SetValue("iv", iv);

        var sealedBytes = Convert.FromHexString(engine.Evaluate("""
            (async () => {
                const key = await crypto.subtle.importKey('raw', new Uint8Array(keyMaterial), 'AES-GCM', false, ['encrypt']);
                const ciphertext = await crypto.subtle.encrypt(
                    { name: 'AES-GCM', iv: new Uint8Array(iv), additionalData: Uint8Array.from('v1', c => c.charCodeAt(0)) },
                    key,
                    Uint8Array.from('a message for the host', c => c.charCodeAt(0)));
                return Array.from(new Uint8Array(ciphertext)).map(x => x.toString(16).padStart(2, '0')).join('');
            })()
            """).UnwrapIfPromise().AsString());

        var tag = sealedBytes.AsSpan(sealedBytes.Length - 16).ToArray();
        var ciphertext = sealedBytes.AsSpan(0, sealedBytes.Length - 16).ToArray();
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        aes.Decrypt(iv, ciphertext, tag, plaintext, "v1"u8);

        System.Text.Encoding.UTF8.GetString(plaintext).Should().Be("a message for the host");
    }

    [Fact]
    public void AScriptCanOpenWhatTheHostEncrypted()
    {
        var engine = WebEngine();

        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        var iv = new byte[12];
        RandomNumberGenerator.Fill(iv);

        var plaintext = "a message for the script"u8.ToArray();
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using (var aes = new AesGcm(key, tagSizeInBytes: 16))
        {
            aes.Encrypt(iv, plaintext, ciphertext, tag, "v1"u8);
        }

        engine.SetValue("keyMaterial", key);
        engine.SetValue("iv", iv);
        engine.SetValue("sealedBytes", ciphertext.Concat(tag).ToArray());

        engine.Evaluate("""
            (async () => {
                const key = await crypto.subtle.importKey('raw', new Uint8Array(keyMaterial), 'AES-GCM', false, ['decrypt']);
                const opened = await crypto.subtle.decrypt(
                    { name: 'AES-GCM', iv: new Uint8Array(iv), additionalData: Uint8Array.from('v1', c => c.charCodeAt(0)) },
                    key,
                    new Uint8Array(sealedBytes));
                return String.fromCharCode(...new Uint8Array(opened));
            })()
            """).UnwrapIfPromise().AsString().Should().Be("a message for the script");
    }

    [Fact]
    public void AHostHoldingAKeyStillCannotReadIt()
    {
        var engine = WebEngine();

        // A host may hold a CryptoKey as an ordinary JsValue and hand it back to script. What it cannot do —
        // through the public surface, which is all an embedder has — is read the key material off it: the
        // object has no own properties, the four attributes are all it exposes, and a non-extractable key
        // refuses exportKey.
        var key = engine.Evaluate("""
            crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, false, ['sign'])
            """).UnwrapIfPromise();

        key.IsObject().Should().BeTrue();
        var keyObject = key.AsObject();
        keyObject.GetOwnPropertyKeys().Should().BeEmpty();
        keyObject.Get("type").AsString().Should().Be("secret");
        keyObject.Get("extractable").AsBoolean().Should().BeFalse();

        engine.SetValue("hostHeldKey", key);
        engine.Evaluate("JSON.stringify(hostHeldKey)").AsString().Should().Be("{}");
        engine.Evaluate("Object.keys(hostHeldKey).length").AsNumber().Should().Be(0);

        engine.Evaluate("""
            crypto.subtle.exportKey('raw', hostHeldKey).then(() => 'exported', e => e.name)
            """).UnwrapIfPromise().AsString().Should().Be("InvalidAccessError");

        // ... and it is still a working key.
        engine.Evaluate("""
            crypto.subtle.sign('HMAC', hostHeldKey, new Uint8Array(3)).then(mac => mac.byteLength)
            """).UnwrapIfPromise().AsNumber().Should().Be(32);
    }

    [Fact]
    public void NeverThrowsIntoTheHost()
    {
        var engine = WebEngine();

        // A promise-returning WebIDL operation converts every exception into a rejection, so a host calling
        // Evaluate on a line of nonsense gets a promise back rather than a JavaScriptException.
        var value = engine.Evaluate("crypto.subtle.importKey('pem', 42, 'MD5', 'yes', 'sign')");

        value.IsObject().Should().BeTrue();
        engine.SetValue("hostHeldPromise", value);
        engine.Evaluate("hostHeldPromise.then(() => 'resolved', e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError");
    }

    [Fact]
    public void ReportsItsFailuresAsRejectionsAScriptCanCatch()
    {
        var engine = WebEngine();

        engine.Evaluate("""
            (async () => {
                const seen = [];
                try { await crypto.subtle.generateKey({ name: 'AES-GCM', length: 111 }, false, ['encrypt']); }
                catch (e) { seen.push(e.name); }
                try { await crypto.subtle.importKey('jwk', { kty: 'RSA' }, { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']); }
                catch (e) { seen.push(e.name); }
                try { await crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, false, []); }
                catch (e) { seen.push(e.name); }
                try { await crypto.subtle.importKey('spki', new Uint8Array(16), 'AES-GCM', false, ['encrypt']); }
                catch (e) { seen.push(e.name); }
                try {
                    const key = await crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, false, ['verify']);
                    await crypto.subtle.sign('HMAC', key, new Uint8Array(0));
                } catch (e) { seen.push(e.name); }
                return seen.join(',');
            })()
            """).UnwrapIfPromise().AsString()
            .Should().Be("OperationError,DataError,SyntaxError,NotSupportedError,InvalidAccessError");
    }

    [Fact]
    public void IsOneSetOfObjectsPerEngineAndNeverShared()
    {
        var options = new Options().UseWebApis(WebApiFeatures.Crypto);

        var first = new Engine(options);
        var second = new Engine(options);

        // Two engines built from one shared Options instance each get their own interface object, which is
        // the same promise every other web-API object makes.
        first.Evaluate("typeof CryptoKey").AsString().Should().Be("function");
        second.Evaluate("typeof CryptoKey").AsString().Should().Be("function");

        var key = first.Evaluate("crypto.subtle.generateKey({ name: 'AES-GCM', length: 128 }, false, ['encrypt'])")
            .UnwrapIfPromise();

        first.SetValue("key", key);
        first.Evaluate("key instanceof CryptoKey").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void AHostRegisteredCryptoKeyGlobalWins()
    {
        var marker = new JsString("the host's own CryptoKey");

        var engine = new Engine(options => options
            .AddLazyGlobal("CryptoKey", _ => marker)
            .UseWebApis());

        // The host's configuration runs first and the install is non-clobbering, so nothing of ours reaches
        // that name — and the operations still work, because they never look the global up.
        engine.Evaluate("CryptoKey").Should().BeSameAs(marker);
        engine.Evaluate("crypto.subtle.generateKey({ name: 'AES-GCM', length: 128 }, false, ['encrypt']).then(k => k.type)")
            .UnwrapIfPromise().AsString().Should().Be("secret");
    }

    [Fact]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("new ShadowRealm().evaluate('typeof CryptoKey')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof CryptoKey").AsString().Should().Be("function");
    }
}
#endif
