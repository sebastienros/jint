#if NET8_0_OR_GREATER
using Jint;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The <c>crypto</c> object seen from outside the assembly: what a host has to write to get it, what it gets
/// when it writes nothing, and the promises it can build on.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party and every
/// assertion goes through script or the public options surface, exactly as an embedder's would.
/// </remarks>
public class WebApiCryptoTests
{
    [Fact]
    public void ADefaultEngineHasNoCrypto()
    {
        var engine = new Engine();

        engine.Evaluate("typeof crypto").AsString().Should().Be("undefined");
        engine.Evaluate("'crypto' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void UseWebApisInstallsCrypto()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof crypto").AsString().Should().Be("object");
        engine.Evaluate("typeof crypto.getRandomValues").AsString().Should().Be("function");
        engine.Evaluate("typeof crypto.randomUUID").AsString().Should().Be("function");

        // The default set is what UseWebApis() means, and it now names crypto.
        new Options().UseWebApis().WebApi.Features.Should().HaveFlag(WebApiFeatures.Crypto);
    }

    [Fact]
    public void AskingForConsoleAloneDoesNotBringCrypto()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Console));

        engine.Evaluate("typeof console").AsString().Should().Be("object");
        engine.Evaluate("typeof crypto").AsString().Should().Be("undefined");
    }

    [Fact]
    public void FillsAHostSuppliedTypedArrayWithRandomBytes()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Crypto));

        // Sixteen bytes coming back all zero would be a 2^-128 event, so this is a fair test that the host's
        // array really was filled and that the same object came back.
        engine.Evaluate("""
            (() => {
                const array = new Uint8Array(16);
                const returned = crypto.getRandomValues(array);
                return returned === array && array.some(b => b !== 0);
            })()
            """).AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ProducesRandomUuidsAHostCanParse()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Crypto));

        var first = engine.Evaluate("crypto.randomUUID()").AsString();
        var second = engine.Evaluate("crypto.randomUUID()").AsString();

        first.Should().NotBe(second);
        Guid.TryParseExact(first, "D", out var parsed).Should().BeTrue();
        parsed.ToString("D").Should().Be(first, "the string is already the lowercase hyphenated form");

        // Version 4 and the RFC 4122 variant, which is what the algorithm's bit-setting steps produce.
        first[14].Should().Be('4');
        "89ab".Contains(first[19]).Should().BeTrue();
    }

    [Fact]
    public void ReportsItsFailuresAsTheStandardExceptions()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Crypto));

        // A DOMException a script can catch and branch on, not a CLR exception erupting through the host.
        engine.Evaluate("""
            (() => {
                const seen = [];
                try { crypto.getRandomValues(new Float64Array(4)); } catch (e) { seen.push(e.name); }
                try { crypto.getRandomValues(new Uint8Array(65537)); } catch (e) { seen.push(e.name); }
                try { crypto.getRandomValues([1, 2, 3]); } catch (e) { seen.push(e.constructor.name); }
                return seen.join(',');
            })()
            """).AsString().Should().Be("TypeMismatchError,QuotaExceededError,TypeError");
    }

    [Fact]
    public void HasASubtleCryptoWhoseUnimplementedOperationsFeatureDetectionCanSee()
    {
        var engine = new Engine(options => options.UseWebApis());

        // `subtle` rides the Crypto flag rather than carrying one of its own — it is an attribute of the very
        // same interface — so a host that asked for crypto has it, and so is `CryptoKey`, which is the type of
        // the keys it hands out.
        engine.Evaluate("typeof crypto.subtle").AsString().Should().Be("object");
        engine.Evaluate("typeof crypto.subtle.digest").AsString().Should().Be("function");
        engine.Evaluate("typeof crypto.subtle.sign").AsString().Should().Be("function");
        engine.Evaluate("typeof crypto.subtle.importKey").AsString().Should().Be("function");
        engine.Evaluate("typeof CryptoKey").AsString().Should().Be("function");

        // Key derivation is here, with the arities WebIDL gives it — deriveBits declares two required
        // arguments because its `length` is optional and nullable.
        engine.Evaluate("""
            ['deriveBits', 'deriveKey']
                .map(name => typeof crypto.subtle[name] + ':' + crypto.subtle[name].length).join(',')
            """).AsString().Should().Be("function:2,function:5");

        // Key wrapping is absent, not present-and-throwing, so a library that checks before reaching for one
        // takes its fallback path.
        engine.Evaluate("""
            ['wrapKey', 'unwrapKey'].map(name => typeof crypto.subtle[name]).join(',')
            """).AsString().Should().Be("undefined,undefined");
    }

    [Fact]
    public void VerifiesAnRs256SignatureAHostCanHandTheScript()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Crypto));

        // The shape an embedder actually reaches for: an RS256 JWT verified inside script from a public key
        // the host supplies as a JSON Web Key. The key, the signing input and the signature are the example
        // of https://www.rfc-editor.org/rfc/rfc7515#appendix-A.2, so the answer is a known one.
        engine.SetValue("jwk", Rs256PublicJwk);
        engine.SetValue("signingInput", Rs256SigningInput);
        engine.SetValue("signatureHex", Convert.ToHexString(Convert.FromBase64String(Rs256SignatureBase64)));

        var result = engine.Evaluate("""
            (async () => {
                const key = await crypto.subtle.importKey(
                    'jwk', JSON.parse(jwk), { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['verify']);

                const signature = Uint8Array.from(signatureHex.match(/../g), x => parseInt(x, 16));
                const data = Uint8Array.from(signingInput, c => c.charCodeAt(0));
                const good = await crypto.subtle.verify('RSASSA-PKCS1-v1_5', key, signature, data);

                const tampered = Uint8Array.from(signingInput + 'x', c => c.charCodeAt(0));
                const bad = await crypto.subtle.verify('RSASSA-PKCS1-v1_5', key, signature, tampered);

                return [good, bad, key.type, key.algorithm.modulusLength, key.algorithm.hash.name].join('|');
            })()
            """).UnwrapIfPromise();

        result.AsString().Should().Be("true|false|public|2048|SHA-256");
    }

    [Fact]
    public void GeneratesAKeyPairAsThePlainDictionaryTheSpecificationDefines()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Crypto));

        // CryptoKeyPair is a dictionary rather than an interface, so there is no CryptoKeyPair on the global
        // to brand-check against — what generateKey resolves with is an ordinary object carrying two keys.
        var result = engine.Evaluate("""
            (async () => {
                const pair = await crypto.subtle.generateKey(
                    { name: 'RSA-PSS', modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: 'SHA-256' },
                    true,
                    ['sign', 'verify']);

                const message = Uint8Array.from('signed by the script', c => c.charCodeAt(0));
                const params = { name: 'RSA-PSS', saltLength: 32 };
                const signature = await crypto.subtle.sign(params, pair.privateKey, message);

                const spki = await crypto.subtle.exportKey('spki', pair.publicKey);

                return [
                    typeof CryptoKeyPair,
                    Object.keys(pair).join(','),
                    await crypto.subtle.verify(params, pair.publicKey, signature, message),
                    spki.byteLength > 250,
                ].join('|');
            })()
            """).UnwrapIfPromise();

        result.AsString().Should().Be("undefined|privateKey,publicKey|true|true");
    }

    [Fact]
    public void ReportsARestrictionOfThePlatformAsACatchableDomException()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Crypto));

        // The three restrictions .NET's own RSA imposes are reported as OperationError DOMExceptions a
        // script can catch and branch on, never as CLR exceptions erupting through the host.
        engine.SetValue("jwk", Rs256PublicJwk);

        var result = engine.Evaluate("""
            (async () => {
                const seen = [];

                const key = await crypto.subtle.importKey(
                    'jwk', JSON.parse(jwk), { name: 'RSA-OAEP', hash: 'SHA-256' }, false, ['encrypt']);

                try { await crypto.subtle.encrypt({ name: 'RSA-OAEP', label: new Uint8Array([1]) }, key, new Uint8Array(4)); }
                catch (e) { seen.push(e.name + ':' + (e instanceof DOMException)); }

                try {
                    await crypto.subtle.generateKey(
                        { name: 'RSA-PSS', modulusLength: 2048, publicExponent: new Uint8Array([3]), hash: 'SHA-256' },
                        false, ['sign', 'verify']);
                }
                catch (e) { seen.push(e.name + ':' + (e instanceof DOMException)); }

                const pss = await crypto.subtle.importKey(
                    'jwk', JSON.parse(jwk), { name: 'RSA-PSS', hash: 'SHA-256' }, false, ['verify']);

                try { await crypto.subtle.verify({ name: 'RSA-PSS', saltLength: 0 }, pss, new Uint8Array(256), new Uint8Array(4)); }
                catch (e) { seen.push(e.name + ':' + (e instanceof DOMException)); }

                return seen.join(',');
            })()
            """).UnwrapIfPromise();

        result.AsString().Should().Be("OperationError:true,OperationError:true,OperationError:true");
    }

    [Fact]
    public void ReportsEveryDerivationRefusalAsACatchableDomException()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Crypto));

        // Each of these is a shape the platform reports with a CLR exception of its own — an
        // ArgumentOutOfRangeException for an HKDF output past 255 hash blocks and for a zero-length one, an
        // ArgumentException for two ECDH keys of different sizes — and every one of them has to reach the
        // script as a DOMException instead. A CLR exception erupting out of a promise-returning operation is
        // the one thing this API must never do, and it is exactly what a host embedding the engine cannot
        // catch.
        var result = engine.Evaluate("""
            (async () => {
                const seen = [];
                const attempt = async fn => {
                    try { seen.push('ok:' + await fn()); }
                    catch (e) { seen.push(e.name + ':' + (e instanceof DOMException)); }
                };

                const ikm = await crypto.subtle.importKey('raw', new Uint8Array(22), 'HKDF', false, ['deriveBits']);
                const hkdf = { name: 'HKDF', hash: 'SHA-256', salt: new Uint8Array(0), info: new Uint8Array(0) };

                // Past 255 * hashLength.
                await attempt(() => crypto.subtle.deriveBits(hkdf, ikm, (255 * 32 + 1) * 8));
                // Zero bits, which is the empty byte sequence and not a failure.
                await attempt(async () => (await crypto.subtle.deriveBits(hkdf, ikm, 0)).byteLength);

                const password = await crypto.subtle.importKey('raw', new Uint8Array(8), 'PBKDF2', false, ['deriveBits']);
                // An iteration count no execution constraint could interrupt.
                await attempt(() => crypto.subtle.deriveBits(
                    { name: 'PBKDF2', salt: new Uint8Array(8), iterations: 2 ** 31, hash: 'SHA-256' }, password, 256));

                const p256 = await crypto.subtle.generateKey({ name: 'ECDH', namedCurve: 'P-256' }, false, ['deriveBits']);
                const p384 = await crypto.subtle.generateKey({ name: 'ECDH', namedCurve: 'P-384' }, false, ['deriveBits']);

                // Two keys of different sizes, which the platform reports as an ArgumentException.
                await attempt(() => crypto.subtle.deriveBits({ name: 'ECDH', public: p384.publicKey }, p256.privateKey, 256));
                // More bits than the curve has.
                await attempt(() => crypto.subtle.deriveBits({ name: 'ECDH', public: p256.publicKey }, p256.privateKey, 384));

                return seen.join(',');
            })()
            """).UnwrapIfPromise();

        result.AsString().Should().Be(
            "OperationError:true,ok:0,OperationError:true,InvalidAccessError:true,OperationError:true");
    }

    [Fact]
    public void AHostRegisteredCryptoGlobalWins()
    {
        var marker = new JsString("the host's own crypto");

        var engine = new Engine(options => options
            .AddLazyGlobal("crypto", _ => marker)
            .UseWebApis());

        // The host's configuration runs first and the install is non-clobbering.
        engine.Evaluate("crypto").Should().BeSameAs(marker);
    }

    [Fact]
    public void IsAnEnumerableDataPropertyOfTheGlobal()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Crypto));

        // A documented simplification of the WebIDL [Replaceable] accessor pair, and the same shape console
        // is installed with.
        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'crypto')").AsObject();
        descriptor.Get("writable").AsBoolean().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("new ShadowRealm().evaluate('typeof crypto')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof crypto").AsString().Should().Be("object");
    }

    [Fact]
    public void OneOptionsInstanceServesSeveralEngines()
    {
        var options = new Options().UseWebApis(WebApiFeatures.Crypto);

        var first = new Engine(options);
        var second = new Engine(options);

        first.Evaluate("crypto === crypto").AsBoolean().Should().BeTrue();
        first.Evaluate("crypto.randomUUID()").AsString().Should().NotBe(second.Evaluate("crypto.randomUUID()").AsString());
    }

    /// <summary>
    /// The RSA key, the JWS Signing Input and the RSASSA-PKCS1-v1_5 SHA-256 signature of
    /// https://www.rfc-editor.org/rfc/rfc7515#appendix-A.2 — an example produced by an implementation
    /// that is not this one, which is what makes verifying it a real check rather than a round trip.
    /// </summary>
    private const string Rs256PublicJwk =
        "{\"kty\":\"RSA\",\"n\":\"ofgWCuLjybRlzo0tZWJjNiuSfb4p4fAkd_wWJcyQoTbji9k0l8W26mPddxHmfHQp-Vaw-4qPCJrcS2mJPMEzP1Pt0Bm4d4QlL-yRT-SFd2lZS-pCgNMsD1W_YpRPEwOWvG6b32690r2jZ47soMZo9wGzjb_7OMg0LOL-bSf63kpaSHSXndS5z5rexMdbBYUsLA9e-KXBdQOS-UTo7WTBEMa2R2CapHg665xsmtdVMTBQY4uDZlxvb3qCo5ZwKh9kG4LT6_I5IhlJH7aGhyxXFvUK-DWNmoudF8NAco9_h9iaGNj8q2ethFkMLs91kzk2PAcDTW9gb54h4FRWyuXpoQ\",\"e\":\"AQAB\"}";

    private const string Rs256SigningInput =
        "eyJhbGciOiJSUzI1NiJ9.eyJpc3MiOiJqb2UiLA0KICJleHAiOjEzMDA4MTkzODAsDQogImh0dHA6Ly9leGFtcGxlLmNvbS9pc19yb290Ijp0cnVlfQ";

    private const string Rs256SignatureBase64 =
        "cC4hiUPoj9Eetdgtv3hF80EGrhuB//dzERat0XF9g2VtQgr9PJbu3XOiZj5RZmh7AAuHIm4Bh+0Qc/lF5YKt/O8W2Fp5jujGbds9uJdbF9CUAr7t1dnZcAcQjbKBYNX4BAynRFdiuB++f/nZLgrnbyTyWzO75vRK5h6xBArLIARNPvkSjtQBMHlb1L07Qe7K0GarZRmB/eSN9383LcOLn6/dO++xi12jzDwusC+eOkHWEsqtFZESc6BfI7noOPqvhJ1phCnvWh6IeYI2w9QOYEUipUTI8np6LbgGY9Fs98rqVt5AXLIhWkWywlVmtVrBp0igcN/IoypGlUPQGe77Rw==";

}
#endif
