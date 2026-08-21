#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The elliptic-curve half of <c>crypto.subtle</c> — ECDSA (https://w3c.github.io/webcrypto/#ecdsa) and
/// ECDH (https://w3c.github.io/webcrypto/#ecdh) over P-256, P-384 and P-521, together with the <c>raw</c>
/// point format and the <c>kty: "EC"</c> JSON Web Key that arrived with them.
/// </summary>
/// <remarks>
/// <para>
/// The cryptography is checked against published vectors, which is the only way to check it. All three come
/// from documents whose examples were produced by implementations that are not this one: RFC 7515 Appendix
/// A.3 for ECDSA P-256 with SHA-256 (key, signing input and signature), RFC 7515 Appendix A.4 for P-521 with
/// SHA-512, and RFC 6979 Appendix A.2.6 for P-384 with SHA-384 over the message "sample". ECDSA draws a
/// fresh nonce for every signature, so only the <i>verify</i> direction can be a known answer; the signing
/// direction is checked by round trip against the same key, together with an assertion that two signatures
/// of one message differ, which is what makes the round trip evidence of anything.
/// </para>
/// <para>
/// The <c>spki</c>, <c>pkcs8</c> and <c>raw</c> bytes were DER-encoded from each RFC's own coordinates, by
/// hand, outside this engine — so a test that imports them and re-exports them byte for byte is checking
/// this engine's parsing and re-encoding against an encoding it did not produce.
/// </para>
/// <para>
/// ECDH appears throughout for its <i>keys</i>: what it does with them — <c>deriveBits</c>, and the
/// <c>deriveKey</c> composition over it — lives in <see cref="SubtleCryptoDeriveTests"/>, together with the
/// RFC 5903 agreement vector. What is pinned here is the split that keeps the two elliptic-curve algorithms
/// apart: an ECDH key carries only the derivation usages and an ECDSA key only the signature ones.
/// </para>
/// </remarks>
public class SubtleCryptoEcTests
{
    /// <summary>
    /// The same helpers <see cref="SubtleCryptoRsaTests"/> uses, for the same reason: bytes are built from
    /// hex or from character codes rather than through <c>TextEncoder</c> or <c>atob</c>, so that crypto is
    /// the only feature the engine carries and nothing can be passing because of a neighbour.
    /// </summary>
    private const string Prelude = """
        const hex = b => Array.from(new Uint8Array(b)).map(x => x.toString(16).padStart(2, '0')).join('');
        const bytes = h => Uint8Array.from(h.match(/../g) || [], x => parseInt(x, 16));
        const ascii = s => Uint8Array.from(s, c => c.charCodeAt(0));
        """;

    private static Engine WebEngine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Crypto));
        engine.Evaluate(Prelude);
        return engine;
    }

    /// <summary>Runs an async body to completion and answers what it returned.</summary>
    private static JsValue Run(string body, Engine? engine = null)
    {
        engine ??= WebEngine();
        return engine.Evaluate("(async () => {\n" + body + "\n})()").UnwrapIfPromise();
    }

    /// <summary>Settles one expression's promise and answers whatever it resolved or rejected to.</summary>
    private static JsValue Settle(Engine engine, string source) => engine.Evaluate(source).UnwrapIfPromise();

    // ---------------------------------------------------------------------------------------------------
    // The published vectors
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void VerifiesTheRfc7515Es256VectorFromEveryPublicFormat()
    {
        // https://www.rfc-editor.org/rfc/rfc7515#appendix-A.3 — the ECDSA P-256 SHA-256 signature over that
        // appendix's JWS Signing Input. The same public key described three ways must reach the same answer,
        // and a message one character different must not.
        Run($$"""
            const params = { name: 'ECDSA', namedCurve: 'P-256' };

            const spki = await crypto.subtle.importKey('spki', bytes('{{Es256SpkiHex}}'), params, false, ['verify']);
            const jwk = await crypto.subtle.importKey('jwk', { {{Es256PublicJwk}} }, params, false, ['verify']);
            const raw = await crypto.subtle.importKey('raw', bytes('{{Es256RawHex}}'), params, false, ['verify']);

            const signature = bytes('{{Es256SignatureHex}}');
            const message = ascii('{{Es256SigningInput}}');
            const verify = { name: 'ECDSA', hash: 'SHA-256' };

            return [
                await crypto.subtle.verify(verify, spki, signature, message),
                await crypto.subtle.verify(verify, jwk, signature, message),
                await crypto.subtle.verify(verify, raw, signature, message),
                await crypto.subtle.verify(verify, spki, signature, ascii('{{Es256SigningInput}}x')),
                // "If signature does not have a length of n * 2 bytes, then return false" — a step of its own.
                await crypto.subtle.verify(verify, spki, new Uint8Array(64), message),
                await crypto.subtle.verify(verify, spki, new Uint8Array(0), message),
                await crypto.subtle.verify(verify, spki, new Uint8Array(63), message),
            ].join(',');
            """).AsString().Should().Be("true,true,true,false,false,false,false");
    }

    [Fact]
    public void VerifiesTheRfc7515Es512VectorOnP521()
    {
        // https://www.rfc-editor.org/rfc/rfc7515#appendix-A.4 — ES512, which is ECDSA over **P-521** with
        // SHA-512: the number in a JOSE alg is the hash's output length, not the curve's field size.
        Run($$"""
            const key = await crypto.subtle.importKey(
                'jwk', { {{Es512PublicJwk}} }, { name: 'ECDSA', namedCurve: 'P-521' }, false, ['verify']);

            const verify = { name: 'ECDSA', hash: 'SHA-512' };
            const signature = bytes('{{Es512SignatureHex}}');

            return [
                signature.length,
                await crypto.subtle.verify(verify, key, signature, ascii('{{Es512SigningInput}}')),
                await crypto.subtle.verify(verify, key, signature, ascii('{{Es512SigningInput}}x')),
                key.algorithm.namedCurve,
            ].join('|');
            """).AsString().Should().Be("132|true|false|P-521");
    }

    [Fact]
    public void VerifiesTheRfc6979P384Vector()
    {
        // https://www.rfc-editor.org/rfc/rfc6979#appendix-A.2.6 — the (r, s) pair that document's own
        // implementation produced for the message "sample" under P-384 with SHA-384. Deterministic ECDSA
        // reaches a different signature from this engine's randomized one, but the *verification* of it is
        // the same question either way.
        Run($$"""
            const key = await crypto.subtle.importKey(
                'spki', bytes('{{P384SpkiHex}}'), { name: 'ECDSA', namedCurve: 'P-384' }, false, ['verify']);

            const signature = bytes('{{P384SignatureHex}}');

            return [
                signature.length,
                await crypto.subtle.verify({ name: 'ECDSA', hash: 'SHA-384' }, key, signature, ascii('sample')),
                await crypto.subtle.verify({ name: 'ECDSA', hash: 'SHA-384' }, key, signature, ascii('samplf')),
                // The vector is a SHA-384 one; the same bytes under another hash are simply not it.
                await crypto.subtle.verify({ name: 'ECDSA', hash: 'SHA-256' }, key, signature, ascii('sample')),
            ].join('|');
            """).AsString().Should().Be("96|true|false|false");
    }

    [Theory]
    [InlineData("P-256", 64)]
    [InlineData("P-384", 96)]
    [InlineData("P-521", 132)]
    public void RoundTripsASignatureAtTheCurvesOwnFixedWidth(string namedCurve, int signatureLength)
    {
        // The signature is r || s at the field width — 32, 48 and 66 bytes per integer — which is
        // IeeeP1363FixedFieldConcatenation and not the DER SEQUENCE .NET can also produce.
        Run($$"""
            const params = { name: 'ECDSA', namedCurve: '{{namedCurve}}' };
            const priv = await crypto.subtle.importKey('pkcs8', bytes('{{Pkcs8For(namedCurve)}}'), params, false, ['sign']);
            const pub = await crypto.subtle.importKey('spki', bytes('{{SpkiFor(namedCurve)}}'), params, false, ['verify']);

            const sign = { name: 'ECDSA', hash: 'SHA-256' };
            const message = ascii('the message');

            const first = await crypto.subtle.sign(sign, priv, message);
            const second = await crypto.subtle.sign(sign, priv, message);

            return [
                first.byteLength,
                await crypto.subtle.verify(sign, pub, first, message),
                await crypto.subtle.verify(sign, pub, second, message),
                // ECDSA draws a fresh nonce each time, so two signatures over one message differ. Without
                // this the round trip above would pass for an implementation that signed nothing at all.
                hex(first) === hex(second),
            ].join('|');
            """).AsString().Should().Be(signatureLength + "|true|true|false");
    }

    [Theory]
    [InlineData("P-256", "SHA-1", 64)]
    [InlineData("P-256", "SHA-256", 64)]
    [InlineData("P-256", "SHA-384", 64)]
    [InlineData("P-256", "SHA-512", 64)]
    [InlineData("P-521", "SHA-1", 132)]
    [InlineData("P-521", "SHA-256", 132)]
    public void EveryHashIsLegalOnEveryCurveAndTheKeyRemembersNeither(string namedCurve, string hash, int signatureLength)
    {
        // The hash belongs to the EcdsaParams of each call and the curve to the key, so P-256 with SHA-512
        // is an ordinary thing to ask for and the signature's length is decided by the curve alone. The key's
        // own algorithm dictionary is an EcKeyAlgorithm, which has no `hash` member at all.
        Run($$"""
            const params = { name: 'ECDSA', namedCurve: '{{namedCurve}}' };
            const priv = await crypto.subtle.importKey('pkcs8', bytes('{{Pkcs8For(namedCurve)}}'), params, false, ['sign']);
            const pub = await crypto.subtle.importKey('spki', bytes('{{SpkiFor(namedCurve)}}'), params, false, ['verify']);

            const message = ascii('m');
            const signature = await crypto.subtle.sign({ name: 'ECDSA', hash: '{{hash}}' }, priv, message);

            return [
                signature.byteLength,
                await crypto.subtle.verify({ name: 'ECDSA', hash: '{{hash}}' }, pub, signature, message),
                Object.keys(priv.algorithm).join(','),
                priv.algorithm.hash === undefined,
            ].join('|');
            """).AsString().Should().Be(signatureLength + "|true|name,namedCurve|true");
    }

    [Fact]
    public void ASignatureMadeUnderOneHashDoesNotVerifyUnderAnother()
    {
        // The corollary of the hash living on the call rather than on the key: the same key and the same
        // message under a different hash is a different signing input, and verification says so.
        Run($$"""
            const params = { name: 'ECDSA', namedCurve: 'P-256' };
            const priv = await crypto.subtle.importKey('pkcs8', bytes('{{Es256Pkcs8Hex}}'), params, false, ['sign']);
            const pub = await crypto.subtle.importKey('spki', bytes('{{Es256SpkiHex}}'), params, false, ['verify']);

            const message = ascii('m');
            const signature = await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, priv, message);

            return [
                await crypto.subtle.verify({ name: 'ECDSA', hash: 'SHA-256' }, pub, signature, message),
                await crypto.subtle.verify({ name: 'ECDSA', hash: 'SHA-384' }, pub, signature, message),
            ].join(',');
            """).AsString().Should().Be("true,false");
    }

    // ---------------------------------------------------------------------------------------------------
    // Key formats
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("ECDSA")]
    [InlineData("ECDH")]
    public void RoundTripsAnEcKeyThroughAllFourFormats(string algorithm)
    {
        // Both algorithms export to every format an EC key has — and the DER a key exports is the DER it was
        // imported from, byte for byte, because the handle is the platform's canonical re-encoding of the
        // structure that arrived. The two algorithms share one encoding, which is exactly why a
        // SubjectPublicKeyInfo alone cannot say which of them a key is for.
        var privateUsages = string.Equals(algorithm, "ECDH", StringComparison.Ordinal) ? "['deriveBits']" : "['sign']";
        var publicUsages = string.Equals(algorithm, "ECDH", StringComparison.Ordinal) ? "[]" : "['verify']";

        Run($$"""
            const params = { name: '{{algorithm}}', namedCurve: 'P-256' };

            const priv = await crypto.subtle.importKey('pkcs8', bytes('{{Es256Pkcs8Hex}}'), params, true, {{privateUsages}});
            const pub = await crypto.subtle.importKey('spki', bytes('{{Es256SpkiHex}}'), params, true, {{publicUsages}});

            const pkcs8 = await crypto.subtle.exportKey('pkcs8', priv);
            const spki = await crypto.subtle.exportKey('spki', pub);
            const raw = await crypto.subtle.exportKey('raw', pub);

            const privJwk = await crypto.subtle.exportKey('jwk', priv);
            const pubJwk = await crypto.subtle.exportKey('jwk', pub);

            // ... and the private key's own spki, reached the long way round through jwk.
            const fromJwk = await crypto.subtle.importKey('jwk', privJwk, params, true, {{privateUsages}});

            return [
                hex(pkcs8) === '{{Es256Pkcs8Hex}}',
                hex(spki) === '{{Es256SpkiHex}}',
                hex(raw) === '{{Es256RawHex}}',
                hex(await crypto.subtle.exportKey('pkcs8', fromJwk)) === '{{Es256Pkcs8Hex}}',
                privJwk.x === pubJwk.x && privJwk.y === pubJwk.y,
                privJwk.crv + '/' + privJwk.kty + '/' + priv.algorithm.name,
            ].join('|');
            """).AsString().Should().Be("true|true|true|true|true|P-256/EC/" + algorithm);
    }

    [Fact]
    public void ARawPointAndAJwkDescribingItReachTheSameKey()
    {
        // The uncompressed point 04||X||Y and a JSON Web Key carrying the same X and Y are two spellings of
        // one public key, so they must export the same DER and verify the same signature.
        Run($$"""
            const params = { name: 'ECDSA', namedCurve: 'P-256' };

            const fromRaw = await crypto.subtle.importKey('raw', bytes('{{Es256RawHex}}'), params, true, ['verify']);
            const fromJwk = await crypto.subtle.importKey('jwk', { {{Es256PublicJwk}} }, params, true, ['verify']);

            const signature = bytes('{{Es256SignatureHex}}');
            const message = ascii('{{Es256SigningInput}}');

            return [
                hex(await crypto.subtle.exportKey('spki', fromRaw)) === hex(await crypto.subtle.exportKey('spki', fromJwk)),
                hex(await crypto.subtle.exportKey('raw', fromRaw)) === hex(await crypto.subtle.exportKey('raw', fromJwk)),
                JSON.stringify(await crypto.subtle.exportKey('jwk', fromRaw)) === JSON.stringify(await crypto.subtle.exportKey('jwk', fromJwk)),
                await crypto.subtle.verify({ name: 'ECDSA', hash: 'SHA-256' }, fromRaw, signature, message),
            ].join('|');
            """).AsString().Should().Be("true|true|true|true");
    }

    [Fact]
    public void EachFormatBelongsToOneKeyType()
    {
        // "If the [[type]] internal slot of key is not 'public', then throw an InvalidAccessError" — spki
        // and raw describe a public key and pkcs8 a private one, and none can stand in for another.
        Run($$"""
            const params = { name: 'ECDSA', namedCurve: 'P-256' };
            const priv = await crypto.subtle.importKey('pkcs8', bytes('{{Es256Pkcs8Hex}}'), params, true, ['sign']);
            const pub = await crypto.subtle.importKey('spki', bytes('{{Es256SpkiHex}}'), params, true, ['verify']);

            const outcomes = [];
            for (const [key, format] of [[priv, 'spki'], [priv, 'raw'], [pub, 'pkcs8']]) {
                try { await crypto.subtle.exportKey(format, key); outcomes.push('exported'); }
                catch (e) { outcomes.push(e.name + '/' + (e instanceof DOMException)); }
            }

            return outcomes.join(',');
            """).AsString().Should().Be("InvalidAccessError/true,InvalidAccessError/true,InvalidAccessError/true");
    }

    [Fact]
    public void ANonExtractableEcKeyCannotBeExportedInAnyFormat()
    {
        Run($$"""
            const key = await crypto.subtle.importKey(
                'pkcs8', bytes('{{Es256Pkcs8Hex}}'), { name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign']);

            const outcomes = [];
            for (const format of ['pkcs8', 'jwk']) {
                try { await crypto.subtle.exportKey(format, key); outcomes.push('exported'); }
                catch (e) { outcomes.push(e.name); }
            }

            // ... and it still signs: extractable is about the material, not about the key's use.
            outcomes.push((await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, key, ascii('x'))).byteLength);
            return outcomes.join('|');
            """).AsString().Should().Be("InvalidAccessError|InvalidAccessError|64");
    }

    [Theory]
    // A compressed point, which this engine does not support and the step anticipates by name — at the
    // length a compressed point actually has, and at the length an uncompressed one has.
    [InlineData("'02' + '{{X}}'", "compressed")]
    [InlineData("'03' + '{{X}}'", "compressed")]
    [InlineData("'02' + '{{BODY}}'", "compressed")]
    // The point at infinity, which is "an identity point".
    [InlineData("'00'", "0x04")]
    // A full-length point whose leading byte names no format at all: it is the marker rather than the
    // length that refuses this one.
    [InlineData("'05' + '{{BODY}}'", "0x04")]
    [InlineData("'ff' + '{{BODY}}'", "0x04")]
    // Wrong lengths.
    [InlineData("'04' + '{{X}}'", "0x04")]
    [InlineData("'{{RAW}}' + '00'", "0x04")]
    [InlineData("''", "0x04")]
    public void RefusesARawPointThatIsNotAnUncompressedOne(string expression, string expectedFragment)
    {
        var engine = WebEngine();

        var source = expression
            .Replace("{{BODY}}", Es256RawHex.Substring(2), StringComparison.Ordinal)
            .Replace("{{X}}", Es256XHex, StringComparison.Ordinal)
            .Replace("{{RAW}}", Es256RawHex, StringComparison.Ordinal);

        Settle(engine, $$"""
            crypto.subtle.importKey('raw', bytes({{source}}), { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify'])
                .then(() => 'imported', e => e.name + '/' + (e instanceof DOMException) + '/' + (e.message.indexOf('{{expectedFragment}}') >= 0))
            """).AsString().Should().Be("DataError/true/true");
    }

    [Fact]
    public void RefusesDerThatIsNotTheStructureTheFormatNames()
    {
        var engine = WebEngine();

        // "If an error occurred while parsing, then throw a DataError" — including an spki that is really a
        // pkcs8 and the other way round, which parse cleanly as themselves and not at all as each other.
        Settle(engine, $$"""
            crypto.subtle.importKey('spki', bytes('{{Es256Pkcs8Hex}}'), { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify'])
                .then(() => 'imported', e => e.name + '/' + (e instanceof DOMException))
            """).AsString().Should().Be("DataError/true");

        Settle(engine, $$"""
            crypto.subtle.importKey('pkcs8', bytes('{{Es256SpkiHex}}'), { name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("DataError");

        foreach (var data in new[] { "new Uint8Array(0)", "new Uint8Array(16)", "new Uint8Array([0x30, 0x03, 0x02, 0x01, 0x00])" })
        {
            Settle(engine, $$"""
                crypto.subtle.importKey('spki', {{data}}, { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify'])
                    .then(() => 'imported', e => e.name)
                """).AsString().Should().Be("DataError");
        }

        // An RSA SubjectPublicKeyInfo is a well-formed structure naming the wrong algorithm, so it reaches
        // the id-ecPublicKey check rather than the parser.
        Settle(engine, $$"""
            crypto.subtle.importKey('spki', bytes('{{RsaSpkiHex}}'), { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify'])
                .then(() => 'imported', e => e.name + '/' + (e.message.indexOf('id-ecPublicKey') >= 0))
            """).AsString().Should().Be("DataError/true");
    }

    [Fact]
    public void AcceptsAPkcs8ThatRepeatsItsCurveInsideAndReExportsTheCanonicalOne()
    {
        // RFC 5915's ECPrivateKey may carry the curve a second time, in its own optional parameters field,
        // and .NET's own encoder omits it. So this is a valid structure another implementation produces that
        // this engine does not — importing it and exporting a *different*, canonical encoding of the same key
        // is what the handle being the platform's own re-encoding means.
        Run($$"""
            const key = await crypto.subtle.importKey(
                'pkcs8', bytes('{{Es256Pkcs8WithInnerParametersHex}}'), { name: 'ECDSA', namedCurve: 'P-256' }, true, ['sign']);

            const exported = hex(await crypto.subtle.exportKey('pkcs8', key));

            return [
                exported === '{{Es256Pkcs8WithInnerParametersHex}}',
                exported === '{{Es256Pkcs8Hex}}',
                (await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, key, ascii('m'))).byteLength,
            ].join('|');
            """).AsString().Should().Be("false|true|64");
    }

    [Fact]
    public void RefusesAPkcs8WhoseInnerStructureContradictsItsOuterOne()
    {
        var engine = WebEngine();

        // "If the parameters field of ecPrivateKey is present, and … does not contain the same object
        // identifier as the parameters field of the privateKeyAlgorithm … then throw a DataError", and the
        // publicKey field must be the one the private key value produces. Both are the platform's parser's
        // to enforce, and both arrive here as the CryptographicException that becomes this DataError.
        foreach (var data in new[] { Es256Pkcs8WithMismatchedInnerCurveHex, Es256Pkcs8WithMismatchedPublicKeyHex })
        {
            Settle(engine, $$"""
                crypto.subtle.importKey('pkcs8', bytes('{{data}}'), { name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign'])
                    .then(() => 'imported', e => e.name + '/' + (e instanceof DOMException))
                """).AsString().Should().Be("DataError/true");
        }
    }

    [Fact]
    public void RefusesTrailingBytesAfterAWellFormedStructure()
    {
        var engine = WebEngine();

        // "parse an ASN.1 structure … with exactData set to true": a structure that is followed by anything
        // is not the structure that was asked for, however well-formed its prefix is.
        Settle(engine, $$"""
            (() => {
                const der = bytes('{{Es256SpkiHex}}');
                const padded = new Uint8Array(der.length + 1);
                padded.set(der);
                return crypto.subtle.importKey('spki', padded, { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify'])
                    .then(() => 'imported', e => e.name + '/' + (e.message.indexOf('trailing') >= 0));
            })()
            """).AsString().Should().Be("DataError/true");
    }

    [Theory]
    [InlineData("spki", "P-384", "['verify']")]
    [InlineData("spki", "P-521", "['verify']")]
    [InlineData("pkcs8", "P-384", "['sign']")]
    [InlineData("pkcs8", "P-521", "['sign']")]
    public void RefusesADerStructureWhoseCurveIsNotTheOneRequested(string format, string namedCurve, string usages)
    {
        var engine = WebEngine();

        // "If namedCurve is defined, and not equal to the namedCurve member of normalizedAlgorithm, throw a
        // DataError." The platform's own importer accepts a key of any curve and reports the fact afterwards
        // differently on each operating system, so the object identifier is read out of the DER itself —
        // without which a P-256 import would quietly hand back a P-384 key.
        var data = string.Equals(format, "spki", StringComparison.Ordinal) ? SpkiFor(namedCurve) : Pkcs8For(namedCurve);

        Settle(engine, $$"""
            crypto.subtle.importKey('{{format}}', bytes('{{data}}'), { name: 'ECDSA', namedCurve: 'P-256' }, false, {{usages}})
                .then(key => 'imported as ' + key.algorithm.namedCurve, e => e.name + '/' + (e.message.indexOf('{{namedCurve}}') >= 0))
            """).AsString().Should().Be("DataError/true");
    }

    // ---------------------------------------------------------------------------------------------------
    // JSON Web Key
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void ExportsAnEcJwkWithTheFieldsAndTheOrderWebIdlGivesIt()
    {
        // A dictionary is converted to an object member by member in lexicographical order —
        // https://webidl.spec.whatwg.org/#es-dictionary. An EC public key carries crv, x and y (Section
        // 6.2.1 of JSON Web Algorithms) and a private key those plus d (6.2.2). There is deliberately **no**
        // alg: the export steps set one for RSA and none here.
        Run($$"""
            const params = { name: 'ECDSA', namedCurve: 'P-256' };
            const priv = await crypto.subtle.importKey('pkcs8', bytes('{{Es256Pkcs8Hex}}'), params, true, ['sign']);
            const pub = await crypto.subtle.importKey('spki', bytes('{{Es256SpkiHex}}'), params, true, ['verify']);

            const privJwk = await crypto.subtle.exportKey('jwk', priv);
            const pubJwk = await crypto.subtle.exportKey('jwk', pub);

            return [
                Object.keys(privJwk).join(','),
                Object.keys(pubJwk).join(','),
                'alg' in privJwk,
                privJwk.kty + '/' + privJwk.crv + '/' + privJwk.ext + '/' + privJwk.key_ops.join('+'),
                privJwk.x === '{{Es256XB64}}' && privJwk.y === '{{Es256YB64}}' && privJwk.d === '{{Es256DB64}}',
            ].join('|');
            """).AsString().Should().Be(
                "crv,d,ext,key_ops,kty,x,y|crv,ext,key_ops,kty,x,y|false|EC/P-256/true/sign|true");
    }

    [Fact]
    public void AJwkCoordinateKeepsALeadingZeroByteOnBothDirections()
    {
        // Section 6.2.1.2 of JSON Web Algorithms: "The length of this octet string MUST be the full size of
        // a coordinate for the curve" — the opposite of the minimal-length encoding every RSA field uses.
        // The RFC 7515 A.4 key's y begins with a zero byte, so an implementation that trimmed it, or that
        // left-padded on import rather than refusing a short value, would fail here and nowhere else.
        Run($$"""
            const params = { name: 'ECDSA', namedCurve: 'P-521' };
            const key = await crypto.subtle.importKey('jwk', { {{Es512PrivateJwk}} }, params, true, ['sign']);
            const jwk = await crypto.subtle.exportKey('jwk', key);

            const decoded = new Uint8Array(await crypto.subtle.exportKey('raw',
                await crypto.subtle.importKey('jwk', { {{Es512PublicJwk}} }, params, true, ['verify'])));

            return [
                jwk.y === '{{Es512YB64}}',
                jwk.x === '{{Es512XB64}}',
                jwk.d === '{{Es512DB64}}',
                // 0x04 then two 66-byte coordinates, the second of which starts with the zero byte.
                decoded.length + '/' + decoded[0] + '/' + decoded[67],
            ].join('|');
            """).AsString().Should().Be("true|true|true|133/4/0");
    }

    [Theory]
    // A minimally encoded coordinate, which is exactly what an RSA field would be — the two encodings are
    // deliberately not interchangeable, so this is a DataError rather than something to left-pad.
    [InlineData("P-256", "x", Es256ShortX)]
    [InlineData("P-256", "x", Es256LongX)]
    [InlineData("P-521", "y", MinimalEs512Y)]
    public void RefusesAJwkCoordinateThatIsNotTheCurvesFullWidth(string namedCurve, string field, string value)
    {
        var engine = WebEngine();

        Settle(engine, $$"""
            (() => {
                const jwk = { {{PublicJwkFor(namedCurve)}} };
                jwk.{{field}} = '{{value}}';
                return crypto.subtle.importKey('jwk', jwk, { name: 'ECDSA', namedCurve: '{{namedCurve}}' }, false, ['verify'])
                    .then(() => 'imported', e => e.name + '/' + (e.message.indexOf('full') >= 0));
            })()
            """).AsString().Should().Be("DataError/true");
    }

    [Theory]
    // "If the kty field of jwk is not 'EC', then throw a DataError."
    [InlineData("delete jwk.kty")]
    [InlineData("jwk.kty = 'ec'")]
    [InlineData("jwk.kty = 'RSA'")]
    // "Let namedCurve be … the crv field of jwk. If namedCurve is not equal to the namedCurve member of
    // normalizedAlgorithm, throw a DataError."
    [InlineData("delete jwk.crv")]
    [InlineData("jwk.crv = 'P-384'")]
    [InlineData("jwk.crv = 'p-256'")]
    // Section 6.2.1 of JSON Web Algorithms: x and y are what an EC public key is.
    [InlineData("delete jwk.x")]
    [InlineData("delete jwk.y")]
    [InlineData("jwk.x = ''")]
    [InlineData("jwk.y = 'not+base64url'")]
    // The alg table, and an alg naming a curve the key does not carry.
    [InlineData("jwk.alg = 'ES384'")]
    [InlineData("jwk.alg = 'ES512'")]
    [InlineData("jwk.alg = 'RS256'")]
    [InlineData("jwk.alg = 'es256'")]
    // The three fields every JWK import checks.
    [InlineData("jwk.use = 'enc'")]
    [InlineData("jwk.key_ops = ['sign']")]
    [InlineData("jwk.key_ops = ['verify', 'verify']")]
    [InlineData("jwk.ext = false")]
    public void RefusesAMalformedEcdsaJwkWithADataError(string mutation)
    {
        var engine = WebEngine();

        // Every row is imported as an extractable verifying P-256 key, so the `ext` and `key_ops` rows are
        // about the JWK rather than about the request.
        Settle(engine, $$"""
            (() => {
                const jwk = { {{Es256PublicJwk}} };
                {{mutation}};
                return crypto.subtle.importKey('jwk', jwk, { name: 'ECDSA', namedCurve: 'P-256' }, true, ['verify'])
                    .then(() => 'imported', e => e.name + '/' + (e instanceof DOMException));
            })()
            """).AsString().Should().Be("DataError/true");
    }

    [Theory]
    [InlineData("P-256", "ES256")]
    [InlineData("P-384", "ES384")]
    // The row that cannot be derived from the curve's name: ES512 is the SHA-512 pairing, and its curve is
    // P-521 — https://www.rfc-editor.org/rfc/rfc7518#section-3.4.
    [InlineData("P-521", "ES512")]
    public void TheJwkAlgorithmNamesTheCurveByItsHashPairing(string namedCurve, string alg)
    {
        // The import table is honoured and the export deliberately writes no alg back, which is the one
        // asymmetry an EC JWK has that an RSA one does not.
        Run($$"""
            const jwk = { {{PublicJwkFor(namedCurve)}} };
            jwk.alg = '{{alg}}';

            const key = await crypto.subtle.importKey('jwk', jwk, { name: 'ECDSA', namedCurve: '{{namedCurve}}' }, true, ['verify']);
            const exported = await crypto.subtle.exportKey('jwk', key);

            return [
                key.algorithm.namedCurve,
                'alg' in exported,
                // ... and what comes back still imports, which is what makes the omission safe.
                (await crypto.subtle.importKey('jwk', exported, { name: 'ECDSA', namedCurve: '{{namedCurve}}' }, true, ['verify'])).algorithm.namedCurve,
            ].join('|');
            """).AsString().Should().Be(namedCurve + "|false|" + namedCurve);
    }

    [Fact]
    public void AnEcdhJwkNeitherRequiresNorChecksTheAlgField()
    {
        var engine = WebEngine();

        // The ECDH import steps read no alg at all — there is no table for it in them — so a JWK carrying
        // one that ECDSA would refuse imports here without complaint, and `use` is 'enc' rather than 'sig'.
        Settle(engine, $$"""
            (() => {
                const jwk = { {{Es256PrivateJwk}} };
                jwk.alg = 'ES512';
                jwk.use = 'enc';
                return crypto.subtle.importKey('jwk', jwk, { name: 'ECDH', namedCurve: 'P-256' }, true, ['deriveBits'])
                    .then(key => key.algorithm.name + '/' + key.algorithm.namedCurve + '/' + key.usages.join('+'), e => e.name);
            })()
            """).AsString().Should().Be("ECDH/P-256/deriveBits");

        // ... while ECDSA refuses both of those, which is what makes the difference a decision rather than
        // an oversight.
        Settle(engine, $$"""
            (() => {
                const jwk = { {{Es256PrivateJwk}} };
                jwk.alg = 'ES512';
                return crypto.subtle.importKey('jwk', jwk, { name: 'ECDSA', namedCurve: 'P-256' }, true, ['sign'])
                    .then(() => 'imported', e => e.name);
            })()
            """).AsString().Should().Be("DataError");

        Settle(engine, $$"""
            (() => {
                const jwk = { {{Es256PrivateJwk}} };
                jwk.use = 'enc';
                return crypto.subtle.importKey('jwk', jwk, { name: 'ECDSA', namedCurve: 'P-256' }, true, ['sign'])
                    .then(() => 'imported', e => e.name);
            })()
            """).AsString().Should().Be("DataError");

        // ... and ECDH refuses 'sig' for the same reason ECDSA refuses 'enc'.
        Settle(engine, $$"""
            (() => {
                const jwk = { {{Es256PrivateJwk}} };
                jwk.use = 'sig';
                return crypto.subtle.importKey('jwk', jwk, { name: 'ECDH', namedCurve: 'P-256' }, true, ['deriveBits'])
                    .then(() => 'imported', e => e.name);
            })()
            """).AsString().Should().Be("DataError");
    }

    [Fact]
    public void ReadsEveryEcJwkFieldInWebIdlsOwnOrderAndOnlyOnce()
    {
        // A dictionary's members are converted in lexicographical order, each read exactly once — so crv
        // comes straight after alg and x and y come last, after use. A second read would be a second chance
        // for a getter to change the answer between the check and the import.
        Run($$"""
            const reads = [];
            const source = { {{Es256PrivateJwk}} };
            const jwk = {};
            for (const name of ['y', 'x', 'kty', 'd', 'crv']) {
                Object.defineProperty(jwk, name, { enumerable: true, get() { reads.push(name); return source[name]; } });
            }
            Object.defineProperty(jwk, 'alg', { enumerable: true, get() { reads.push('alg'); return 'ES256'; } });
            Object.defineProperty(jwk, 'ext', { enumerable: true, get() { reads.push('ext'); return true; } });
            Object.defineProperty(jwk, 'key_ops', { enumerable: true, get() { reads.push('key_ops'); return ['sign']; } });
            Object.defineProperty(jwk, 'use', { enumerable: true, get() { reads.push('use'); return 'sig'; } });

            await crypto.subtle.importKey('jwk', jwk, { name: 'ECDSA', namedCurve: 'P-256' }, true, ['sign']);
            return reads.join(',');
            """).AsString().Should().Be("alg,crv,d,ext,key_ops,kty,use,x,y");
    }

    // ---------------------------------------------------------------------------------------------------
    // Curves
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("ECDSA", "'P-192'")]
    [InlineData("ECDSA", "'p-256'")]
    [InlineData("ECDSA", "'secp256k1'")]
    [InlineData("ECDSA", "'P-256 '")]
    [InlineData("ECDSA", "'Ed25519'")]
    [InlineData("ECDH", "'X25519'")]
    [InlineData("ECDH", "'P-224'")]
    [InlineData("ECDH", "42")]
    [InlineData("ECDH", "null")]
    public void RefusesACurveThisEngineDoesNotImplementWithANotSupportedError(string algorithm, string namedCurve)
    {
        var engine = WebEngine();

        // NamedCurve is `typedef DOMString`, not a WebIDL enumeration, so nothing about the argument
        // conversion refuses these — the operation's own first step does, and it matches case-sensitively
        // where the algorithm *name* one member to its left matches case-insensitively.
        Settle(engine, $$"""
            crypto.subtle.generateKey({ name: '{{algorithm}}', namedCurve: {{namedCurve}} }, false, ['{{PrivateUsageOf(algorithm)}}'])
                .then(() => 'generated', e => e.name + '/' + (e instanceof DOMException))
            """).AsString().Should().Be("NotSupportedError/true");

        Settle(engine, $$"""
            crypto.subtle.importKey('raw', bytes('{{Es256RawHex}}'), { name: '{{algorithm}}', namedCurve: {{namedCurve}} }, false, [])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("NotSupportedError");
    }

    [Fact]
    public void TheCurveIsCheckedBeforeAnythingElseAboutTheRequest()
    {
        var engine = WebEngine();

        // Step 1 of importKey is the curve, before the format branch and therefore before that branch's own
        // usage check — so a request that is wrong in both ways reports the curve.
        Settle(engine, """
            crypto.subtle.importKey('spki', new Uint8Array([1, 2, 3]), { name: 'ECDSA', namedCurve: 'P-192' }, false, ['sign'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("NotSupportedError");

        // With a curve that is recognized, the usages are next and the bytes are last.
        Settle(engine, """
            crypto.subtle.importKey('spki', new Uint8Array([1, 2, 3]), { name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("SyntaxError");

        Settle(engine, """
            crypto.subtle.importKey('spki', new Uint8Array([1, 2, 3]), { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("DataError");
    }

    [Fact]
    public void TheNamedCurveIsARequiredMemberOfBothEcDictionaries()
    {
        var engine = WebEngine();

        // `required NamedCurve namedCurve` — an absent required member is the TypeError WebIDL raises,
        // which is a different failure from a curve name the algorithm does not implement.
        foreach (var algorithm in new[] { "'ECDSA'", "{ name: 'ECDSA' }", "{ name: 'ECDSA', namedCurve: undefined }" })
        {
            Settle(engine, $$"""
                crypto.subtle.generateKey({{algorithm}}, false, ['sign', 'verify'])
                    .then(() => 'generated', e => e.constructor.name)
                """).AsString().Should().Be("TypeError");

            Settle(engine, $$"""
                crypto.subtle.importKey('raw', bytes('{{Es256RawHex}}'), {{algorithm}}, false, ['verify'])
                    .then(() => 'imported', e => e.constructor.name)
                """).AsString().Should().Be("TypeError");
        }
    }

    [Fact]
    public void TheHashIsARequiredMemberOfEcdsaParams()
    {
        var engine = WebEngine();

        // `required HashAlgorithmIdentifier hash`, read per call — and a name that is not a registered
        // digest is the NotSupportedError normalizing a hash gives it, not a TypeError.
        foreach (var algorithm in new[] { "'ECDSA'", "{ name: 'ECDSA' }", "{ name: 'ECDSA', hash: undefined }" })
        {
            Settle(engine, $$"""
                crypto.subtle.importKey('pkcs8', bytes('{{Es256Pkcs8Hex}}'), { name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign'])
                    .then(key => crypto.subtle.sign({{algorithm}}, key, new Uint8Array(4)))
                    .then(() => 'signed', e => e.constructor.name)
                """).AsString().Should().Be("TypeError");
        }

        Settle(engine, $$"""
            crypto.subtle.importKey('pkcs8', bytes('{{Es256Pkcs8Hex}}'), { name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign'])
                .then(key => crypto.subtle.sign({ name: 'ECDSA', hash: 'MD5' }, key, new Uint8Array(4)))
                .then(() => 'signed', e => e.name)
            """).AsString().Should().Be("NotSupportedError");
    }

    // ---------------------------------------------------------------------------------------------------
    // Points that are not on the curve
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void RefusesAPointThatIsNotOnTheCurveWithADataError()
    {
        var engine = WebEngine();

        // The platform reports this differently per operating system — on Windows it is a
        // PlatformNotSupportedException naming the curve, and elsewhere a CryptographicException — and
        // neither may reach a script as anything but the DataError the step names.
        Settle(engine, $$"""
            (() => {
                const jwk = { {{Es256PublicJwk}} };
                jwk.y = '{{Es256FlippedY}}';
                return crypto.subtle.importKey('jwk', jwk, { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify'])
                    .then(() => 'imported', e => e.name + '/' + (e instanceof DOMException) + '/' + (e.message.indexOf('valid point') >= 0));
            })()
            """).AsString().Should().Be("DataError/true/true");

        // The same point through raw, where the last byte of Y is the last byte of the whole encoding.
        Settle(engine, $$"""
            (() => {
                const point = bytes('{{Es256RawHex}}');
                point[point.length - 1] ^= 1;
                return crypto.subtle.importKey('raw', point, { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify'])
                    .then(() => 'imported', e => e.name + '/' + (e.message.indexOf('valid point') >= 0));
            })()
            """).AsString().Should().Be("DataError/true");

        // ... and through spki, where the platform's own parser is what refuses it.
        Settle(engine, $$"""
            (() => {
                const der = bytes('{{Es256SpkiHex}}');
                der[der.length - 1] ^= 1;
                return crypto.subtle.importKey('spki', der, { name: 'ECDH', namedCurve: 'P-256' }, false, [])
                    .then(() => 'imported', e => e.name + '/' + (e instanceof DOMException));
            })()
            """).AsString().Should().Be("DataError/true");
    }

    // ---------------------------------------------------------------------------------------------------
    // The key-type and usage matrices
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void AnOperationRefusesTheWrongHalfOfTheKeyPair()
    {
        // Step 1 of both ECDSA operations: sign needs a private key and verify a public one. It is an
        // InvalidAccessError, and it comes from the algorithm rather than from the usages — which is why
        // each key here carries the usage the operation asks for.
        Run($$"""
            const params = { name: 'ECDSA', namedCurve: 'P-256' };
            const pub = await crypto.subtle.importKey('spki', bytes('{{Es256SpkiHex}}'), params, false, ['verify']);
            const priv = await crypto.subtle.importKey('pkcs8', bytes('{{Es256Pkcs8Hex}}'), params, false, ['sign']);

            const outcomes = [];
            const attempt = async fn => { try { await fn(); outcomes.push('ok'); } catch (e) { outcomes.push(e.name); } };

            await attempt(() => crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, pub, ascii('m')));
            await attempt(() => crypto.subtle.verify({ name: 'ECDSA', hash: 'SHA-256' }, priv, new Uint8Array(64), ascii('m')));

            return outcomes.join(',');
            """).AsString().Should().Be("InvalidAccessError,InvalidAccessError");
    }

    [Fact]
    public void AnEcdsaKeyTypeCarriesExactlyTheOneUsageItCanPerform()
    {
        var engine = WebEngine();

        // A private ECDSA key may carry only 'sign' and a public one only 'verify', so a key carrying the
        // wrong usage never exists — the mismatch is caught at import as the SyntaxError the format's first
        // step raises, and the InvalidAccessError a usable key used wrongly would get is unreachable.
        foreach (var (format, data, usages) in new[]
        {
            ("spki", Es256SpkiHex, "['sign']"),
            ("spki", Es256SpkiHex, "['verify', 'sign']"),
            ("spki", Es256SpkiHex, "['deriveBits']"),
            ("raw", Es256RawHex, "['sign']"),
            ("pkcs8", Es256Pkcs8Hex, "['verify']"),
            ("pkcs8", Es256Pkcs8Hex, "['sign', 'deriveKey']"),
        })
        {
            Settle(engine, $$"""
                crypto.subtle.importKey('{{format}}', bytes('{{data}}'), { name: 'ECDSA', namedCurve: 'P-256' }, false, {{usages}})
                    .then(() => 'imported', e => e.name + '/' + (e instanceof DOMException))
                """).AsString().Should().Be("SyntaxError/true");
        }
    }

    [Fact]
    public void AnEcdhPublicKeyCarriesNoUsagesAtAll()
    {
        var engine = WebEngine();

        // "If usages is not empty then throw a SyntaxError" — an ECDH public key's permitted usage set is
        // empty rather than a list, which is a rule no other algorithm here has. A public key is one half of
        // an agreement; the deriving is the private half's.
        foreach (var (format, data) in new[] { ("spki", Es256SpkiHex), ("raw", Es256RawHex) })
        {
            foreach (var usages in new[] { "['deriveBits']", "['deriveKey']", "['verify']" })
            {
                Settle(engine, $$"""
                    crypto.subtle.importKey('{{format}}', bytes('{{data}}'), { name: 'ECDH', namedCurve: 'P-256' }, false, {{usages}})
                        .then(() => 'imported', e => e.name + '/' + (e.message.indexOf('no usages at all') >= 0))
                    """).AsString().Should().Be("SyntaxError/true");
            }

            // With the empty list it is an ordinary key — which is how a script imports a peer's public key.
            Settle(engine, $$"""
                crypto.subtle.importKey('{{format}}', bytes('{{data}}'), { name: 'ECDH', namedCurve: 'P-256' }, false, [])
                    .then(key => key.type + '/' + key.usages.length + '/' + key.algorithm.namedCurve, e => e.name)
                """).AsString().Should().Be("public/0/P-256");
        }

        // A JWK without d is the same public key and gets the same rule.
        Settle(engine, $$"""
            crypto.subtle.importKey('jwk', { {{Es256PublicJwk}} }, { name: 'ECDH', namedCurve: 'P-256' }, false, ['deriveBits'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("SyntaxError");
    }

    [Fact]
    public void AnEcdhPrivateKeyCarriesTheDerivationUsagesAndNoOthers()
    {
        var engine = WebEngine();

        // The usage set an ECDH key may be imported with is exactly ['deriveKey', 'deriveBits'], and it is
        // checked before a byte of the key data is parsed — so a request naming a signature usage is the
        // SyntaxError the usages earn rather than anything the DER might have said. It is the mirror of the
        // ECDSA key one test above, and between them they are what keeps `sign` and `deriveBits` from ever
        // meeting the same key.
        Settle(engine, $$"""
            crypto.subtle.importKey('pkcs8', bytes('{{Es256Pkcs8Hex}}'), { name: 'ECDH', namedCurve: 'P-256' }, false, ['deriveKey', 'deriveBits'])
                .then(key => key.type + '/' + key.usages.join('+'), e => e.name)
            """).AsString().Should().Be("private/deriveKey+deriveBits");

        foreach (var usages in new[] { "['sign']", "['deriveBits', 'encrypt']", "['verify']" })
        {
            Settle(engine, $$"""
                crypto.subtle.importKey('pkcs8', bytes('{{Es256Pkcs8Hex}}'), { name: 'ECDH', namedCurve: 'P-256' }, false, {{usages}})
                    .then(() => 'imported', e => e.name)
                """).AsString().Should().Be("SyntaxError");
        }
    }

    [Fact]
    public void APrivateKeyMustBeImportedWithAtLeastOneUsageAndAPublicKeyNeedNot()
    {
        var engine = WebEngine();

        // "If the [[type]] internal slot of result is 'secret' or 'private' and usages is empty, then throw
        // a SyntaxError" — a public key is deliberately outside that step, which is what makes the ECDH
        // public key of the test above a key at all.
        Settle(engine, $$"""
            crypto.subtle.importKey('pkcs8', bytes('{{Es256Pkcs8Hex}}'), { name: 'ECDSA', namedCurve: 'P-256' }, false, [])
                .then(() => 'imported', e => e.name + '/' + (e instanceof DOMException))
            """).AsString().Should().Be("SyntaxError/true");

        Settle(engine, $$"""
            crypto.subtle.importKey('pkcs8', bytes('{{Es256Pkcs8Hex}}'), { name: 'ECDH', namedCurve: 'P-256' }, false, [])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("SyntaxError");

        Settle(engine, $$"""
            crypto.subtle.importKey('spki', bytes('{{Es256SpkiHex}}'), { name: 'ECDSA', namedCurve: 'P-256' }, false, [])
                .then(key => key.type + '/' + key.usages.length, e => e.name)
            """).AsString().Should().Be("public/0");
    }

    [Fact]
    public void AKeyRemembersTheAlgorithmItWasMadeForAndRefusesAnother()
    {
        // The name check comes before everything the algorithm itself does, and it is an InvalidAccessError
        // — the same key material described two ways is two keys. An algorithm that is not registered for
        // the operation at all is a different failure, decided before any key is looked at.
        Run($$"""
            const ecdsa = await crypto.subtle.importKey(
                'pkcs8', bytes('{{Es256Pkcs8Hex}}'), { name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign']);
            const ecdh = await crypto.subtle.importKey(
                'pkcs8', bytes('{{Es256Pkcs8Hex}}'), { name: 'ECDH', namedCurve: 'P-256' }, false, ['deriveBits']);

            const outcomes = [];
            const attempt = async fn => { try { await fn(); outcomes.push('ok'); } catch (e) { outcomes.push(e.name); } };

            await attempt(() => crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, ecdh, ascii('m')));
            await attempt(() => crypto.subtle.sign({ name: 'ECDH', hash: 'SHA-256' }, ecdsa, ascii('m')));
            await attempt(() => crypto.subtle.encrypt({ name: 'ECDSA' }, ecdsa, ascii('m')));
            await attempt(() => crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, ecdsa, ascii('m')));

            return outcomes.join(',');
            """).AsString().Should().Be("InvalidAccessError,NotSupportedError,NotSupportedError,ok");
    }

    // ---------------------------------------------------------------------------------------------------
    // generateKey and CryptoKeyPair
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("P-256", 64)]
    [InlineData("P-384", 96)]
    [InlineData("P-521", 132)]
    public void GeneratesAnEcdsaPairWithTheShapeTheSpecificationDescribes(string namedCurve, int signatureLength)
    {
        Run($$"""
            const pair = await crypto.subtle.generateKey(
                { name: 'ECDSA', namedCurve: '{{namedCurve}}' }, false, ['sign', 'verify']);

            const message = ascii('generated');
            const signature = await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, pair.privateKey, message);

            return [
                // CryptoKeyPair is a dictionary, not an interface: an ordinary object with two own data
                // properties, no interface object on the global, and Object.prototype behind it.
                Object.keys(pair).join(','),
                Object.getPrototypeOf(pair) === Object.prototype,
                typeof CryptoKeyPair,
                pair.publicKey instanceof CryptoKey && pair.privateKey instanceof CryptoKey,

                pair.privateKey.type + '/' + pair.publicKey.type,
                // "Set the [[extractable]] internal slot of publicKey to true" — whatever was asked for.
                pair.privateKey.extractable + '/' + pair.publicKey.extractable,
                pair.privateKey.usages.join('+') + '/' + pair.publicKey.usages.join('+'),

                // One EcKeyAlgorithm, shared by both halves, with no hash member.
                Object.keys(pair.privateKey.algorithm).join(','),
                pair.privateKey.algorithm.name + '/' + pair.privateKey.algorithm.namedCurve,

                signature.byteLength,
                await crypto.subtle.verify({ name: 'ECDSA', hash: 'SHA-256' }, pair.publicKey, signature, message),
            ].join('|');
            """).AsString().Should().Be(
                "privateKey,publicKey|true|undefined|true|"
                + "private/public|false/true|sign/verify|"
                + "name,namedCurve|ECDSA/" + namedCurve + "|"
                + signatureLength + "|true");
    }

    [Fact]
    public void GeneratesAnEcdhPairWhosePublicHalfCarriesNoUsages()
    {
        // "Set the [[usages]] internal slot of publicKey to be the empty list" — flatly, not as an
        // intersection, so a pair generated with both derivation usages still has an empty public half. The
        // pair is exportable and round-trips, which is all a script can do with it today.
        Run("""
            const pair = await crypto.subtle.generateKey(
                { name: 'ECDH', namedCurve: 'P-384' }, true, ['deriveKey', 'deriveBits']);

            const spki = await crypto.subtle.exportKey('spki', pair.publicKey);
            const raw = await crypto.subtle.exportKey('raw', pair.publicKey);
            const reimported = await crypto.subtle.importKey('spki', spki, { name: 'ECDH', namedCurve: 'P-384' }, true, []);

            return [
                pair.privateKey.usages.join('+'),
                pair.publicKey.usages.length,
                pair.privateKey.algorithm.name + '/' + pair.privateKey.algorithm.namedCurve,
                // 0x04 and two 48-byte coordinates.
                raw.byteLength,
                hex(await crypto.subtle.exportKey('raw', reimported)) === hex(raw),
            ].join('|');
            """).AsString().Should().Be("deriveKey+deriveBits|0|ECDH/P-384|97|true");
    }

    [Fact]
    public void RefusesAPairWhosePrivateHalfWouldHaveNoUsages()
    {
        var engine = WebEngine();

        // "If the [[usages]] internal slot of the privateKey attribute of result is the empty sequence, then
        // throw a SyntaxError." For ECDH that is the whole of what generating with an empty list means; for
        // ECDSA a pair generated with 'verify' alone is the same mistake.
        Settle(engine, """
            crypto.subtle.generateKey({ name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify'])
                .then(() => 'generated', e => e.name + '/' + (e instanceof DOMException))
            """).AsString().Should().Be("SyntaxError/true");

        Settle(engine, """
            crypto.subtle.generateKey({ name: 'ECDH', namedCurve: 'P-256' }, false, [])
                .then(() => 'generated', e => e.name)
            """).AsString().Should().Be("SyntaxError");

        // A pair whose *public* half carries nothing is perfectly ordinary — for ECDH it is the only kind.
        Settle(engine, """
            crypto.subtle.generateKey({ name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign'])
                .then(pair => pair.privateKey.usages.join('+') + '/' + pair.publicKey.usages.length, e => e.name)
            """).AsString().Should().Be("sign/0");
    }

    [Theory]
    [InlineData("ECDSA", "['encrypt']")]
    [InlineData("ECDSA", "['sign', 'deriveKey']")]
    [InlineData("ECDSA", "['sign', 'wrapKey']")]
    [InlineData("ECDH", "['sign']")]
    [InlineData("ECDH", "['deriveBits', 'verify']")]
    [InlineData("ECDH", "['encrypt', 'decrypt']")]
    public void RefusesAUsageAnEcAlgorithmDoesNotSupportWithASyntaxError(string algorithm, string usages)
    {
        var engine = WebEngine();

        // Step 1 of generateKey, which runs before the curve is even looked at, so this generates nothing.
        Settle(engine, $$"""
            crypto.subtle.generateKey({ name: '{{algorithm}}', namedCurve: 'P-256' }, false, {{usages}})
                .then(() => 'generated', e => e.name + '/' + (e instanceof DOMException))
            """).AsString().Should().Be("SyntaxError/true");
    }

    [Fact]
    public void TheUsageCheckPrecedesTheCurveCheckInGenerateKey()
    {
        var engine = WebEngine();

        // The two operations order their first steps differently and both orders are pinned: generateKey
        // checks the usages first (step 1) and the curve second, while importKey checks the curve first.
        Settle(engine, """
            crypto.subtle.generateKey({ name: 'ECDSA', namedCurve: 'P-192' }, false, ['encrypt'])
                .then(() => 'generated', e => e.name)
            """).AsString().Should().Be("SyntaxError");

        Settle(engine, """
            crypto.subtle.generateKey({ name: 'ECDSA', namedCurve: 'P-192' }, false, ['sign'])
                .then(() => 'generated', e => e.name)
            """).AsString().Should().Be("NotSupportedError");
    }

    // ---------------------------------------------------------------------------------------------------
    // Normalization and the registry
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("ecdsa")]
    [InlineData("EcDsA")]
    [InlineData("ECDSA")]
    public void MatchesTheRegisteredEcNameCaseInsensitivelyButNotTheCurve(string spelling)
    {
        // "Case-insensitive" is ASCII case-insensitive, and the key remembers the registered spelling rather
        // than the caller's — which is what makes the name check on the next operation work at all. The
        // curve, one member along, is matched case-sensitively, so the two rules sit side by side in the very
        // same dictionary.
        Run($$"""
            const key = await crypto.subtle.importKey(
                'spki', bytes('{{Es256SpkiHex}}'), { name: 'ecdsa', namedCurve: 'P-256' }, false, ['verify']);

            return [
                await crypto.subtle.verify(
                    { name: '{{spelling}}', hash: 'sha-256' }, key, bytes('{{Es256SignatureHex}}'), ascii('{{Es256SigningInput}}')),
                key.algorithm.name,
                key.algorithm.namedCurve,
            ].join('|');
            """).AsString().Should().Be("true|ECDSA|P-256");

        Settle(WebEngine(), $$"""
            crypto.subtle.importKey('spki', bytes('{{Es256SpkiHex}}'), { name: '{{spelling}}', namedCurve: 'p-256' }, false, ['verify'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("NotSupportedError");
    }

    [Fact]
    public void TheEcAlgorithmsAreRegisteredForExactlyTheOperationsTheSpecificationGivesThem()
    {
        var engine = WebEngine();

        // ECDSA registers sign, verify, generateKey, importKey and exportKey; ECDH registers generateKey,
        // importKey, exportKey and deriveBits. An algorithm that is not registered for an operation is a
        // NotSupportedError, decided before any key is looked at.
        Settle(engine, $$"""
            (async () => {
                const outcomes = [];
                const attempt = async fn => { try { await fn(); outcomes.push('ok'); } catch (e) { outcomes.push(e.name); } };

                const ecdh = await crypto.subtle.importKey(
                    'spki', bytes('{{Es256SpkiHex}}'), { name: 'ECDH', namedCurve: 'P-256' }, true, []);
                const ecdsa = await crypto.subtle.importKey(
                    'raw', bytes('{{Es256RawHex}}'), { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify']);

                await attempt(() => crypto.subtle.digest('ECDSA', new Uint8Array(0)));
                await attempt(() => crypto.subtle.digest('ECDH', new Uint8Array(0)));
                await attempt(() => crypto.subtle.encrypt({ name: 'ECDH' }, ecdh, new Uint8Array(0)));
                await attempt(() => crypto.subtle.sign({ name: 'ECDH', hash: 'SHA-256' }, ecdh, new Uint8Array(0)));
                await attempt(() => crypto.subtle.exportKey('raw', ecdh));
                await attempt(() => crypto.subtle.verify(
                    { name: 'ECDSA', hash: 'SHA-256' }, ecdsa, bytes('{{Es256SignatureHex}}'), ascii('{{Es256SigningInput}}')));

                return outcomes.join(',');
            })()
            """).AsString().Should().Be(
                "NotSupportedError,NotSupportedError,NotSupportedError,NotSupportedError,ok,ok");

        // ECDSA is not registered for deriveBits and ECDH is, which is the one asymmetry between the two
        // that is not about usages. SubtleCryptoDeriveTests carries the derivations themselves.
        Settle(engine, $$"""
            (async () => {
                const pair = await crypto.subtle.generateKey({ name: 'ECDH', namedCurve: 'P-256' }, false, ['deriveBits']);
                const outcomes = [];
                const attempt = async fn => { try { await fn(); outcomes.push('ok'); } catch (e) { outcomes.push(e.name); } };

                await attempt(() => crypto.subtle.deriveBits({ name: 'ECDSA', public: pair.publicKey }, pair.privateKey, 128));
                await attempt(() => crypto.subtle.deriveBits({ name: 'ECDH', public: pair.publicKey }, pair.privateKey, 128));

                return outcomes.join(',');
            })()
            """).AsString().Should().Be("NotSupportedError,ok");
    }

    [Fact]
    public void NoEcOperationEverThrowsSynchronouslyOrLeaksACryptographicException()
    {
        var engine = WebEngine();

        // A promise-returning WebIDL operation reports every failure as a rejection, and the failures the
        // platform's own cryptography raises are no exception. The third row is the one that matters most
        // here: an off-curve point arrives on Windows as a PlatformNotSupportedException, which is not a
        // CryptographicException and would escape a catch clause written for one alone.
        engine.Evaluate($$"""
            (() => {
                const calls = [
                    () => crypto.subtle.importKey('spki', new Uint8Array([9, 9, 9]), { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify']),
                    () => crypto.subtle.importKey('pkcs8', new Uint8Array([9, 9, 9]), { name: 'ECDH', namedCurve: 'P-384' }, false, ['deriveBits']),
                    () => crypto.subtle.importKey('raw', bytes('{{Es256RawHexWithFlippedY}}'), { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify']),
                    () => crypto.subtle.importKey('jwk', { kty: 'EC', crv: 'P-256', x: 'AA', y: 'AA' }, { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify']),
                    () => crypto.subtle.generateKey({ name: 'ECDSA', namedCurve: 'P-192' }, false, ['sign']),
                ];

                return calls.map(call => {
                    const promise = call();
                    return (promise instanceof Promise) + ':' + typeof promise.then;
                }).join(',');
            })()
            """).AsString().Should().Be("true:function,true:function,true:function,true:function,true:function");

        // ... and each of them settles as a rejection carrying a DOMException.
        Settle(engine, $$"""
            Promise.all([
                crypto.subtle.importKey('spki', new Uint8Array([9, 9, 9]), { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify']).catch(e => e.name),
                crypto.subtle.importKey('pkcs8', new Uint8Array([9, 9, 9]), { name: 'ECDH', namedCurve: 'P-384' }, false, ['deriveBits']).catch(e => e.name),
                crypto.subtle.importKey('raw', bytes('{{Es256RawHexWithFlippedY}}'), { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify']).catch(e => e.name),
                crypto.subtle.importKey('jwk', { kty: 'EC', crv: 'P-256', x: 'AA', y: 'AA' }, { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify']).catch(e => e.name),
                crypto.subtle.generateKey({ name: 'ECDSA', namedCurve: 'P-192' }, false, ['sign']).catch(e => e.name),
            ]).then(names => names.join(','))
            """).AsString().Should().Be("DataError,DataError,DataError,DataError,NotSupportedError");
    }

    // ---------------------------------------------------------------------------------------------------
    // Test vectors
    //
    // The coordinates and the expected outputs are transcribed from the RFCs named in this class's remarks.
    // The spki, pkcs8 and raw bytes were encoded from those coordinates outside this engine.
    // ---------------------------------------------------------------------------------------------------

    private static string SpkiFor(string namedCurve) => namedCurve switch
    {
        "P-256" => Es256SpkiHex,
        "P-384" => P384SpkiHex,
        _ => Es512SpkiHex,
    };

    private static string Pkcs8For(string namedCurve) => namedCurve switch
    {
        "P-256" => Es256Pkcs8Hex,
        "P-384" => P384Pkcs8Hex,
        _ => Es512Pkcs8Hex,
    };

    private static string PublicJwkFor(string namedCurve) => namedCurve switch
    {
        "P-256" => Es256PublicJwk,
        "P-384" => P384PublicJwk,
        _ => Es512PublicJwk,
    };

    private static string PrivateUsageOf(string algorithm)
        => string.Equals(algorithm, "ECDH", StringComparison.Ordinal) ? "deriveBits" : "sign";

    // RFC 7515 Appendix A.3 — ECDSA P-256 SHA-256.
    private const string Es256XB64 = "f83OJ3D2xF1Bg8vub9tLe1gHMzV76e8Tus9uPHvRVEU";
    private const string Es256YB64 = "x_FEzRu9m36HLN_tue659LNpXW6pCyStikYjKIWI5a0";
    private const string Es256DB64 = "jpsQnnGQmL-YBIffH1136cspYG6-0iY7X1fCE9-E9LI";

    private const string Es256PublicJwk = "kty: 'EC', crv: 'P-256', x: '" + Es256XB64 + "', y: '" + Es256YB64 + "'";

    private const string Es256PrivateJwk =
        "kty: 'EC', crv: 'P-256', x: '" + Es256XB64 + "', y: '" + Es256YB64 + "', d: '" + Es256DB64 + "'";

    private const string Es256XHex = "7fcdce2770f6c45d4183cbee6fdb4b7b580733357be9ef13bacf6e3c7bd15445";

    private const string Es256RawHex =
        "047fcdce2770f6c45d4183cbee6fdb4b7b580733357be9ef13bacf6e3c7bd15445c7f144cd1bbd9b7e872cdfedb9eeb9f4b3695d6ea90b24ad8a4623288588e5ad";

    /// <summary>The same point with the last byte of Y flipped, which is not on the curve.</summary>
    private const string Es256RawHexWithFlippedY =
        "047fcdce2770f6c45d4183cbee6fdb4b7b580733357be9ef13bacf6e3c7bd15445c7f144cd1bbd9b7e872cdfedb9eeb9f4b3695d6ea90b24ad8a4623288588e5ac";

    private const string Es256FlippedY = "x_FEzRu9m36HLN_tue659LNpXW6pCyStikYjKIWI5aw";

    /// <summary>The x coordinate one byte short, which is what a minimal-length encoding of it would be.</summary>
    private const string Es256ShortX = "zc4ncPbEXUGDy-5v20t7WAczNXvp7xO6z248e9FURQ";

    /// <summary>The x coordinate with a leading zero byte added, which is one byte too long.</summary>
    private const string Es256LongX = "AH_Nzidw9sRdQYPL7m_bS3tYBzM1e-nvE7rPbjx70VRF";

    private const string Es256SpkiHex =
        "3059301306072a8648ce3d020106082a8648ce3d030107034200047fcdce2770f6c45d4183cbee6fdb4b7b580733357be9ef13bacf6e3c7bd15445c7f144cd1bbd9b7e872cdfedb9eeb9f4b3695d6ea90b24ad8a4623288588e5ad";

    private const string Es256Pkcs8Hex =
        "308187020100301306072a8648ce3d020106082a8648ce3d030107046d306b02010104208e9b109e719098bf980487df1f5d77e9cb29606ebed2263b5f57c213df84f4b2a144034200047fcdce2770f6c45d4183cbee6fdb4b7b580733357be9ef13bacf6e3c7bd15445c7f144cd1bbd9b7e872cdfedb9eeb9f4b3695d6ea90b24ad8a4623288588e5ad";

    /// <summary>
    /// The same key with RFC 5915's optional <c>parameters</c> field present inside the <c>ECPrivateKey</c>,
    /// which is a valid encoding .NET's own encoder does not produce.
    /// </summary>
    private const string Es256Pkcs8WithInnerParametersHex =
        "308193020100301306072a8648ce3d020106082a8648ce3d0301070479307702010104208e9b109e719098bf980487df1f5d77e9cb29606ebed2263b5f57c213df84f4b2a00a06082a8648ce3d030107a144034200047fcdce2770f6c45d4183cbee6fdb4b7b580733357be9ef13bacf6e3c7bd15445c7f144cd1bbd9b7e872cdfedb9eeb9f4b3695d6ea90b24ad8a4623288588e5ad";

    /// <summary>The same, with that inner field naming P-384 where the outer one names P-256.</summary>
    private const string Es256Pkcs8WithMismatchedInnerCurveHex =
        "308193020100301306072a8648ce3d020106082a8648ce3d0301070479307702010104208e9b109e719098bf980487df1f5d77e9cb29606ebed2263b5f57c213df84f4b2a00706052b81040022a144034200047fcdce2770f6c45d4183cbee6fdb4b7b580733357be9ef13bacf6e3c7bd15445c7f144cd1bbd9b7e872cdfedb9eeb9f4b3695d6ea90b24ad8a4623288588e5ad";

    /// <summary>The canonical encoding with the last byte of its embedded public key flipped.</summary>
    private const string Es256Pkcs8WithMismatchedPublicKeyHex =
        "308187020100301306072a8648ce3d020106082a8648ce3d030107046d306b02010104208e9b109e719098bf980487df1f5d77e9cb29606ebed2263b5f57c213df84f4b2a144034200047fcdce2770f6c45d4183cbee6fdb4b7b580733357be9ef13bacf6e3c7bd15445c7f144cd1bbd9b7e872cdfedb9eeb9f4b3695d6ea90b24ad8a4623288588e5ac";

    private const string Es256SigningInput =
        "eyJhbGciOiJFUzI1NiJ9.eyJpc3MiOiJqb2UiLA0KICJleHAiOjEzMDA4MTkzODAsDQogImh0dHA6Ly9leGFtcGxlLmNvbS9pc19yb290Ijp0cnVlfQ";

    private const string Es256SignatureHex =
        "0ed1215379636c483c2f7f155807d402a3b228033af97c7e17819ac3169ea665c50a07d38c3c70e5d8f12daf084a5480a66590c5f293509a8f3f7f8a83a354d5";

    // RFC 7515 Appendix A.4 — ECDSA P-521 SHA-512. The y coordinate begins with a zero byte, which is the
    // fixed-width rule's own test case.
    private const string Es512XB64 =
        "AekpBQ8ST8a8VcfVOTNl353vSrDCLLJXmPk06wTjxrrjcBpXp5EOnYG_NjFZ6OvLFV1jSfS9tsz4qUxcWceqwQGk";

    private const string Es512YB64 =
        "ADSmRA43Z1DSNx_RvcLI87cdL07l6jQyyBXMoxVg_l2Th-x3S1WDhjDly79ajL4Kkd0AZMaZmh9ubmf63e3kyMj2";

    private const string Es512DB64 =
        "AY5pb7A0UFiB3RELSD64fTLOSV_jazdF7fLYyuTw8lOfRhWg6Y6rUrPAxerEzgdRhajnu0ferB0d53vM9mE15j2C";

    /// <summary>The same y with its leading zero byte dropped — 65 bytes where P-521 requires 66.</summary>
    private const string MinimalEs512Y =
        "NKZEDjdnUNI3H9G9wsjztx0vTuXqNDLIFcyjFWD-XZOH7HdLVYOGMOXLv1qMvgqR3QBkxpmaH25uZ_rd7eTIyPY";

    private const string Es512PublicJwk = "kty: 'EC', crv: 'P-521', x: '" + Es512XB64 + "', y: '" + Es512YB64 + "'";

    private const string Es512PrivateJwk =
        "kty: 'EC', crv: 'P-521', x: '" + Es512XB64 + "', y: '" + Es512YB64 + "', d: '" + Es512DB64 + "'";

    private const string Es512SpkiHex =
        "30819b301006072a8648ce3d020106052b81040023038186000401e929050f124fc6bc55c7d5393365df9def4ab0c22cb25798f934eb04e3c6bae3701a57a7910e9d81bf363159e8ebcb155d6349f4bdb6ccf8a94c5c59c7aac101a40034a6440e376750d2371fd1bdc2c8f3b71d2f4ee5ea3432c815cca31560fe5d9387ec774b55838630e5cbbf5a8cbe0a91dd0064c6999a1f6e6e67faddede4c8c8f6";

    private const string Es512Pkcs8Hex =
        "3081ee020100301006072a8648ce3d020106052b810400230481d63081d30201010442018e696fb034505881dd110b483eb87d32ce495fe36b3745edf2d8cae4f0f2539f4615a0e98eab52b3c0c5eac4ce075185a8e7bb47deac1d1de77bccf66135e63d82a18189038186000401e929050f124fc6bc55c7d5393365df9def4ab0c22cb25798f934eb04e3c6bae3701a57a7910e9d81bf363159e8ebcb155d6349f4bdb6ccf8a94c5c59c7aac101a40034a6440e376750d2371fd1bdc2c8f3b71d2f4ee5ea3432c815cca31560fe5d9387ec774b55838630e5cbbf5a8cbe0a91dd0064c6999a1f6e6e67faddede4c8c8f6";

    private const string Es512SigningInput = "eyJhbGciOiJFUzUxMiJ9.UGF5bG9hZA";

    private const string Es512SignatureHex =
        "01dc0c81e7abc2d1e887e975f7697ad21a7dc001d915525b2df0ff531322ef47309d93986912356ca3d644e73e99966ac2a4f6488f8a183281df85ced1ac3fed776d006f06692c0529d0803d98285c3d980496423c45f7c4aa51c1c74e3bc2a9107c098f2a8e8330ceee22af53cbdc9f036b9b161b496f444415ee90e5e894bcde3bf267";

    // RFC 6979 Appendix A.2.6 — ECDSA P-384, the signature that document gives for SHA-384 over "sample".
    private const string P384PublicJwk =
        "kty: 'EC', crv: 'P-384', x: '7DpOQVtOGaRWhhgCn0J_pdqai8SukuAuBqrlKGswDGTe-PDqkFWGYGSiVFFUgLwT', y: 'gBXZty19VyROqO-awMYhiWcIpZNn-d-59UyoSz8cnbEoiyMcOuDU_nNE_SUzJkcg'";

    private const string P384SpkiHex =
        "3076301006072a8648ce3d020106052b8104002203620004ec3a4e415b4e19a4568618029f427fa5da9a8bc4ae92e02e06aae5286b300c64def8f0ea9055866064a254515480bc138015d9b72d7d57244ea8ef9ac0c621896708a59367f9dfb9f54ca84b3f1c9db1288b231c3ae0d4fe7344fd2533264720";

    private const string P384Pkcs8Hex =
        "3081b6020100301006072a8648ce3d020106052b8104002204819e30819b02010104306b9d3dad2e1b8c1c05b19875b6659f4de23c3b667bf297ba9aa47740787137d896d5724e4c70a825f872c9ea60d2edf5a16403620004ec3a4e415b4e19a4568618029f427fa5da9a8bc4ae92e02e06aae5286b300c64def8f0ea9055866064a254515480bc138015d9b72d7d57244ea8ef9ac0c621896708a59367f9dfb9f54ca84b3f1c9db1288b231c3ae0d4fe7344fd2533264720";

    private const string P384SignatureHex =
        "94edbb92a5ecb8aad4736e56c691916b3f88140666ce9fa73d64c4ea95ad133c81a648152e44acf96e36dd1e80fabe4699ef4aeb15f178cea1fe40db2603138f130e740a19624526203b6351d0a3a94fa329c145786e679e7b82c71a38628ac8";

    /// <summary>
    /// The RSA SubjectPublicKeyInfo of <see cref="SubtleCryptoRsaTests"/>, used here for one thing only: it
    /// is a well-formed structure naming an algorithm that is not <c>id-ecPublicKey</c>.
    /// </summary>
    private const string RsaSpkiHex =
        "30820122300d06092a864886f70d01010105000382010f003082010a0282010100a1f8160ae2e3c9b465ce8d2d656263362b927dbe29e1f02477fc1625cc90a136e38bd93497c5b6ea63dd7711e67c7429f956b0fb8a8f089adc4b69893cc1333f53edd019b87784252fec914fe4857769594bea4280d32c0f55bf62944f130396bc6e9bdf6ebdd2bda3678eeca0c668f701b38dbffb38c8342ce2fe6d27fade4a5a4874979dd4b9cf9adec4c75b05852c2c0f5ef8a5c1750392f944e8ed64c110c6b647609aa4783aeb9c6c9ad755313050638b83665c6f6f7a82a396702a1f641b82d3ebf2392219491fb686872c5716f50af8358d9a8b9d17c340728f7f87d89a18d8fcab67ad84590c2ecf759339363c07034d6f606f9e21e05456cae5e9a10203010001";
}
#endif
