#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The RSA family of <c>crypto.subtle</c> — RSASSA-PKCS1-v1_5
/// (https://w3c.github.io/webcrypto/#rsassa-pkcs1), RSA-PSS (https://w3c.github.io/webcrypto/#rsa-pss) and
/// RSA-OAEP (https://w3c.github.io/webcrypto/#rsa-oaep), together with the <c>spki</c> and <c>pkcs8</c> key
/// formats and the asymmetric half of JSON Web Key that arrived with them.
/// </summary>
/// <remarks>
/// <para>
/// The cryptography is checked against published vectors, which is the only way to check it. The keys and
/// the expected outputs come from the JOSE RFCs, whose examples were produced by implementations that are
/// not this one: RFC 7515 Appendix A.2 for RSASSA-PKCS1-v1_5 with SHA-256 (key, signing input and
/// signature), RFC 7520 Section 4.2 for RSA-PSS with SHA-384 (key from Section 3.4, signing input and
/// signature), and RFC 7520 Section 5.2 for RSA-OAEP with SHA-1 (4096-bit key, and a fixed ciphertext whose
/// plaintext the RFC also gives). RSA-PSS and RSA-OAEP are randomized, so only their <i>verify</i> and
/// <i>decrypt</i> directions can be known-answer tested; their signing and encrypting directions are checked
/// by round trip against the same key.
/// </para>
/// <para>
/// The <c>pkcs8</c> and <c>spki</c> bytes for the RFC 7515 key were DER-encoded from that JWK's own
/// integers, by hand, outside this engine — so a test that imports them and reproduces the RFC's signature
/// is checking this engine's parsing against an encoding it did not produce. The other keys are imported as
/// JWK and re-exported, which checks the two directions against each other.
/// </para>
/// <para>
/// Key <b>generation</b> appears three times and always at 2048 bits: an RSA key pair is a prime search, and
/// the cost of one is several orders of magnitude above every other operation here. Everything that does not
/// need a fresh key uses one of the fixed ones.
/// </para>
/// </remarks>
public class SubtleCryptoRsaTests
{
    /// <summary>
    /// The same helpers <see cref="SubtleCryptoKeyTests"/> uses, for the same reason: bytes are built from
    /// hex or from character codes rather than through <c>TextEncoder</c> or <c>atob</c>, so that crypto is
    /// the only feature the engine carries and nothing can be passing because of a neighbour.
    /// </summary>
    private const string Prelude = """
        const hex = b => Array.from(new Uint8Array(b)).map(x => x.toString(16).padStart(2, '0')).join('');
        const bytes = h => Uint8Array.from(h.match(/../g) || [], x => parseInt(x, 16));
        const ascii = s => Uint8Array.from(s, c => c.charCodeAt(0));
        """;

    /// <summary>
    /// An engine with the crypto feature and the helpers above already declared — unlike
    /// <see cref="SubtleCryptoKeyTests"/>, whose prelude rides along with each script, because several tests
    /// here settle more than one expression on one engine and a second <c>const</c> declaration of the same
    /// name is a redeclaration.
    /// </summary>
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
    // RSASSA-PKCS1-v1_5: the published vector
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void SignsTheRfc7515Rs256Vector()
    {
        // https://www.rfc-editor.org/rfc/rfc7515#appendix-A.2 — the RSASSA-PKCS1-v1_5 SHA-256 signature over
        // that appendix's JWS Signing Input, with the private key of its Figure. PKCS#1 v1.5 is
        // deterministic, so the signature is a byte-for-byte known answer.
        Run($$"""
            const key = await crypto.subtle.importKey(
                'pkcs8', bytes('{{Rs256Pkcs8Hex}}'), { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['sign']);

            return hex(await crypto.subtle.sign('RSASSA-PKCS1-v1_5', key, ascii('{{Rs256SigningInput}}')));
            """).AsString().Should().Be(Rs256SignatureHex);
    }

    [Fact]
    public void VerifiesTheRfc7515Rs256VectorFromEveryPublicFormat()
    {
        // The same public key, described three ways, must reach the same answer — and a message that is one
        // character different must not.
        Run($$"""
            const spki = await crypto.subtle.importKey(
                'spki', bytes('{{Rs256SpkiHex}}'), { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['verify']);
            const jwk = await crypto.subtle.importKey(
                'jwk', { {{Rs256PublicJwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['verify']);

            const signature = bytes('{{Rs256SignatureHex}}');
            const message = ascii('{{Rs256SigningInput}}');

            return [
                await crypto.subtle.verify('RSASSA-PKCS1-v1_5', spki, signature, message),
                await crypto.subtle.verify('RSASSA-PKCS1-v1_5', jwk, signature, message),
                await crypto.subtle.verify('RSASSA-PKCS1-v1_5', spki, signature, ascii('{{Rs256SigningInput}}x')),
                await crypto.subtle.verify('RSASSA-PKCS1-v1_5', spki, new Uint8Array(256), message),
                await crypto.subtle.verify('RSASSA-PKCS1-v1_5', spki, new Uint8Array(0), message),
            ].join(',');
            """).AsString().Should().Be("true,true,false,false,false");
    }

    [Theory]
    [InlineData("SHA-1")]
    [InlineData("SHA-256")]
    [InlineData("SHA-384")]
    [InlineData("SHA-512")]
    public void SignsAndVerifiesWithEveryRegisteredHash(string hash)
    {
        Run($$"""
            const priv = await crypto.subtle.importKey(
                'jwk', { {{Rs256PrivateJwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: '{{hash}}' }, false, ['sign']);
            const pub = await crypto.subtle.importKey(
                'jwk', { {{Rs256PublicJwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: '{{hash}}' }, false, ['verify']);

            const message = ascii('the message');
            const signature = await crypto.subtle.sign('RSASSA-PKCS1-v1_5', priv, message);

            return [
                signature.byteLength,
                await crypto.subtle.verify('RSASSA-PKCS1-v1_5', pub, signature, message),
                priv.algorithm.hash.name,
            ].join('|');
            """).AsString().Should().Be("256|true|" + hash);
    }

    // ---------------------------------------------------------------------------------------------------
    // RSA-PSS: the published vector, and the salt-length restriction
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void VerifiesTheRfc7520Ps384Vector()
    {
        // https://www.rfc-editor.org/rfc/rfc7520#section-4.2 — a PS384 signature produced by another
        // implementation over that section's JWS Signing Input, with the 2048-bit key of Section 3.4. PSS is
        // randomized, so this direction is the one that can be a known answer at all.
        Run($$"""
            const key = await crypto.subtle.importKey(
                'jwk', { {{PssPublicJwk}} }, { name: 'RSA-PSS', hash: 'SHA-384' }, false, ['verify']);

            const params = { name: 'RSA-PSS', saltLength: 48 };
            const signature = bytes('{{Ps384SignatureHex}}');

            return [
                await crypto.subtle.verify(params, key, signature, ascii('{{Ps384SigningInput}}')),
                await crypto.subtle.verify(params, key, signature, ascii('{{Ps384SigningInput}}x')),
            ].join(',');
            """).AsString().Should().Be("true,false");
    }

    [Theory]
    [InlineData("SHA-1", 20)]
    [InlineData("SHA-256", 32)]
    [InlineData("SHA-384", 48)]
    [InlineData("SHA-512", 64)]
    public void RoundTripsAPssSignatureAtTheSaltLengthThePlatformProduces(string hash, int saltLength)
    {
        Run($$"""
            const priv = await crypto.subtle.importKey(
                'jwk', { {{PssPrivateJwk}} }, { name: 'RSA-PSS', hash: '{{hash}}' }, false, ['sign']);
            const pub = await crypto.subtle.importKey(
                'jwk', { {{PssPublicJwk}} }, { name: 'RSA-PSS', hash: '{{hash}}' }, false, ['verify']);

            const params = { name: 'RSA-PSS', saltLength: {{saltLength}} };
            const message = ascii('the message');

            const first = await crypto.subtle.sign(params, priv, message);
            const second = await crypto.subtle.sign(params, priv, message);

            return [
                await crypto.subtle.verify(params, pub, first, message),
                // PSS draws a fresh salt each time, so two signatures over the same message differ.
                hex(first) === hex(second),
            ].join(',');
            """).AsString().Should().Be("true,false");
    }

    [Theory]
    [InlineData("SHA-256", 0)]
    [InlineData("SHA-256", 20)]
    [InlineData("SHA-256", 31)]
    [InlineData("SHA-256", 33)]
    [InlineData("SHA-512", 32)]
    public void RefusesAPssSaltLengthThePlatformCannotProduce(string hash, int saltLength)
    {
        var engine = WebEngine();

        // A documented divergence: .NET's RSASignaturePadding.Pss carries no salt-length parameter and always
        // uses the hash's own output length, so every other length the caller may ask for is an
        // OperationError naming the restriction rather than a signature made with a salt nobody asked for.
        Settle(engine, $$"""
            crypto.subtle.importKey('jwk', { {{PssPrivateJwk}} }, { name: 'RSA-PSS', hash: '{{hash}}' }, false, ['sign'])
                .then(key => crypto.subtle.sign({ name: 'RSA-PSS', saltLength: {{saltLength}} }, key, new Uint8Array(4)))
                .then(() => 'signed', e => e.name + '/' + (e instanceof DOMException))
            """).AsString().Should().Be("OperationError/true");

        Settle(engine, $$"""
            crypto.subtle.importKey('jwk', { {{PssPublicJwk}} }, { name: 'RSA-PSS', hash: '{{hash}}' }, false, ['verify'])
                .then(key => crypto.subtle.verify({ name: 'RSA-PSS', saltLength: {{saltLength}} }, key, new Uint8Array(256), new Uint8Array(4)))
                .then(() => 'verified', e => e.name)
            """).AsString().Should().Be("OperationError");
    }

    [Fact]
    public void TheSaltLengthIsARequiredMemberOfRsaPssParams()
    {
        var engine = WebEngine();

        // `required [EnforceRange] unsigned long saltLength` — an absent required member is the TypeError
        // WebIDL raises for it, which is a different failure from a length the platform cannot produce.
        foreach (var algorithm in new[] { "'RSA-PSS'", "{ name: 'RSA-PSS' }", "{ name: 'RSA-PSS', saltLength: undefined }" })
        {
            Settle(engine, $$"""
                crypto.subtle.importKey('jwk', { {{PssPrivateJwk}} }, { name: 'RSA-PSS', hash: 'SHA-256' }, false, ['sign'])
                    .then(key => crypto.subtle.sign({{algorithm}}, key, new Uint8Array(4)))
                    .then(() => 'signed', e => e.constructor.name)
                """).AsString().Should().Be("TypeError");
        }
    }

    // ---------------------------------------------------------------------------------------------------
    // RSA-OAEP: the published vector, and the label restriction
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void DecryptsTheRfc7520RsaOaepVector()
    {
        // https://www.rfc-editor.org/rfc/rfc7520#section-5.2 — the Encrypted Key of Figure 87, produced by
        // another implementation with the 4096-bit key of Figure 84, whose plaintext is the Content
        // Encryption Key of Figure 85. The JOSE algorithm named "RSA-OAEP" is OAEP with SHA-1.
        Run($$"""
            const key = await crypto.subtle.importKey(
                'jwk', { {{OaepPrivateJwk}} }, { name: 'RSA-OAEP', hash: 'SHA-1' }, false, ['decrypt']);

            return hex(await crypto.subtle.decrypt({ name: 'RSA-OAEP' }, key, bytes('{{OaepCiphertextHex}}')));
            """).AsString().Should().Be(OaepPlaintextHex);
    }

    [Theory]
    [InlineData("SHA-1")]
    [InlineData("SHA-256")]
    [InlineData("SHA-384")]
    [InlineData("SHA-512")]
    public void RoundTripsAnOaepMessageWithEveryRegisteredHash(string hash)
    {
        Run($$"""
            const pub = await crypto.subtle.importKey(
                'jwk', { {{OaepPublicJwk}} }, { name: 'RSA-OAEP', hash: '{{hash}}' }, false, ['encrypt']);
            const priv = await crypto.subtle.importKey(
                'jwk', { {{OaepPrivateJwk}} }, { name: 'RSA-OAEP', hash: '{{hash}}' }, false, ['decrypt']);

            const plaintext = ascii('a short secret');
            const first = await crypto.subtle.encrypt({ name: 'RSA-OAEP' }, pub, plaintext);
            const second = await crypto.subtle.encrypt({ name: 'RSA-OAEP' }, pub, plaintext);

            return [
                first.byteLength,
                hex(await crypto.subtle.decrypt({ name: 'RSA-OAEP' }, priv, first)) === hex(plaintext),
                // OAEP draws fresh padding each time, so two ciphertexts of one message differ.
                hex(first) === hex(second),
            ].join('|');
            """).AsString().Should().Be("512|true|false");
    }

    [Fact]
    public void AnAbsentOrEmptyOaepLabelWorksAndANonEmptyOneDoesNot()
    {
        // A documented divergence: RSAEncryptionPadding carries a hash and no label, so the empty label is
        // the only one this engine can honour. Encrypting with the empty label instead of the caller's would
        // produce a ciphertext the intended recipient cannot decrypt, so it is refused.
        Run($$"""
            const pub = await crypto.subtle.importKey(
                'jwk', { {{OaepPublicJwk}} }, { name: 'RSA-OAEP', hash: 'SHA-256' }, false, ['encrypt']);
            const priv = await crypto.subtle.importKey(
                'jwk', { {{OaepPrivateJwk}} }, { name: 'RSA-OAEP', hash: 'SHA-256' }, false, ['decrypt']);

            const plaintext = ascii('labelled');
            const outcomes = [];

            for (const params of [{ name: 'RSA-OAEP' }, { name: 'RSA-OAEP', label: new Uint8Array(0) }, { name: 'RSA-OAEP', label: undefined }]) {
                const ciphertext = await crypto.subtle.encrypt(params, pub, plaintext);
                outcomes.push(hex(await crypto.subtle.decrypt(params, priv, ciphertext)) === hex(plaintext));
            }

            for (const params of [{ name: 'RSA-OAEP', label: ascii('x') }, { name: 'RSA-OAEP', label: new Uint8Array(32) }]) {
                try { await crypto.subtle.encrypt(params, pub, plaintext); outcomes.push('encrypted'); }
                catch (e) { outcomes.push(e.name); }
                try { await crypto.subtle.decrypt(params, priv, new Uint8Array(512)); outcomes.push('decrypted'); }
                catch (e) { outcomes.push(e.name); }
            }

            return outcomes.join(',');
            """).AsString().Should().Be(
                "true,true,true,OperationError,OperationError,OperationError,OperationError");
    }

    [Fact]
    public void EveryWayAnOaepDecryptionCanFailIsTheSameOperationError()
    {
        // OAEP's padding check is exactly what a chosen-ciphertext attack probes, so a ciphertext that is
        // corrupt, that was made for another key, or that is simply the wrong length must all be one answer.
        Run($$"""
            const pub = await crypto.subtle.importKey(
                'jwk', { {{OaepPublicJwk}} }, { name: 'RSA-OAEP', hash: 'SHA-256' }, false, ['encrypt']);
            const priv = await crypto.subtle.importKey(
                'jwk', { {{OaepPrivateJwk}} }, { name: 'RSA-OAEP', hash: 'SHA-256' }, false, ['decrypt']);

            const good = new Uint8Array(await crypto.subtle.encrypt({ name: 'RSA-OAEP' }, pub, ascii('secret')));

            const corrupt = good.slice();
            corrupt[100] ^= 1;

            const truncated = good.slice(0, 511);
            const wrongHash = await crypto.subtle.importKey(
                'jwk', { {{OaepPrivateJwk}} }, { name: 'RSA-OAEP', hash: 'SHA-384' }, false, ['decrypt']);

            const outcomes = [];
            for (const [key, data] of [[priv, corrupt], [priv, truncated], [priv, new Uint8Array(512)], [wrongHash, good]]) {
                try { await crypto.subtle.decrypt({ name: 'RSA-OAEP' }, key, data); outcomes.push('decrypted'); }
                catch (e) { outcomes.push(e.name + '/' + (e instanceof DOMException) + '/' + (e.message.indexOf('could not be decrypted') >= 0)); }
            }

            return outcomes.join(',');
            """).AsString().Should().Be(
                "OperationError/true/true,OperationError/true/true,OperationError/true/true,OperationError/true/true");
    }

    [Fact]
    public void RefusesAPlaintextTooLongForTheModulusWithAnOperationError()
    {
        var engine = WebEngine();

        // "If performing the operation results in an error, then throw an OperationError." A 4096-bit
        // modulus with SHA-512 OAEP padding carries at most 512 - 2*64 - 2 = 382 bytes.
        Settle(engine, $$"""
            crypto.subtle.importKey('jwk', { {{OaepPublicJwk}} }, { name: 'RSA-OAEP', hash: 'SHA-512' }, false, ['encrypt'])
                .then(key => crypto.subtle.encrypt({ name: 'RSA-OAEP' }, key, new Uint8Array(383)))
                .then(() => 'encrypted', e => e.name + '/' + (e instanceof DOMException))
            """).AsString().Should().Be("OperationError/true");
    }

    // ---------------------------------------------------------------------------------------------------
    // Key formats
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void RoundTripsAnRsaKeyThroughEveryFormatItHas()
    {
        // The shape a script actually writes: import once, export, import somewhere else, and get the same
        // deterministic signature out of each.
        Run($$"""
            const original = await crypto.subtle.importKey(
                'jwk', { {{Rs256PrivateJwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, true, ['sign']);

            const message = ascii('{{Rs256SigningInput}}');
            const expected = '{{Rs256SignatureHex}}';

            const pkcs8 = await crypto.subtle.exportKey('pkcs8', original);
            const fromPkcs8 = await crypto.subtle.importKey(
                'pkcs8', pkcs8, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['sign']);

            const jwk = await crypto.subtle.exportKey('jwk', original);
            const fromJwk = await crypto.subtle.importKey(
                'jwk', jwk, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['sign']);

            const publicKey = await crypto.subtle.importKey(
                'spki', bytes('{{Rs256SpkiHex}}'), { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, true, ['verify']);
            const spki = await crypto.subtle.exportKey('spki', publicKey);
            const fromSpki = await crypto.subtle.importKey(
                'spki', spki, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['verify']);

            return [
                hex(await crypto.subtle.sign('RSASSA-PKCS1-v1_5', fromPkcs8, message)) === expected,
                hex(await crypto.subtle.sign('RSASSA-PKCS1-v1_5', fromJwk, message)) === expected,
                await crypto.subtle.verify('RSASSA-PKCS1-v1_5', fromSpki, bytes(expected), message),
                // The DER a key exports is the DER it was imported from, byte for byte: both are the
                // platform's canonical encoding of the same key.
                hex(spki) === '{{Rs256SpkiHex}}',
                hex(pkcs8) === '{{Rs256Pkcs8Hex}}',
            ].join('|');
            """).AsString().Should().Be("true|true|true|true|true");
    }

    [Fact]
    public void TheSameKeyImportedFromJwkAndFromPkcs8IsTheSameKey()
    {
        // Cross-format consistency: the two import paths reach the same key material, so they export the
        // same DER, describe themselves the same way, and produce the same deterministic signature.
        Run($$"""
            const fromJwk = await crypto.subtle.importKey(
                'jwk', { {{Rs256PrivateJwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, true, ['sign']);
            const fromPkcs8 = await crypto.subtle.importKey(
                'pkcs8', bytes('{{Rs256Pkcs8Hex}}'), { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, true, ['sign']);

            const message = ascii('{{Rs256SigningInput}}');

            return [
                hex(await crypto.subtle.exportKey('pkcs8', fromJwk)) === hex(await crypto.subtle.exportKey('pkcs8', fromPkcs8)),
                JSON.stringify(await crypto.subtle.exportKey('jwk', fromJwk)) === JSON.stringify(await crypto.subtle.exportKey('jwk', fromPkcs8)),
                hex(await crypto.subtle.sign('RSASSA-PKCS1-v1_5', fromJwk, message)) === hex(await crypto.subtle.sign('RSASSA-PKCS1-v1_5', fromPkcs8, message)),
                fromJwk.algorithm.modulusLength === fromPkcs8.algorithm.modulusLength,
            ].join('|');
            """).AsString().Should().Be("true|true|true|true");
    }

    [Fact]
    public void AJwkWhoseIntegersCarryLeadingZeroBytesStillImports()
    {
        // JSON Web Algorithms asks for "the minimum number of octets needed to represent the value", but a
        // producer that pads is describing the same integer — a big-endian magnitude with leading zeros. The
        // proof that nothing was lost is the RFC's own signature.
        Run($$"""
            const jwk = { {{Rs256PrivateJwk}} };
            jwk.p = '{{Rs256PaddedP}}';
            jwk.qi = '{{Rs256PaddedQi}}';

            const key = await crypto.subtle.importKey(
                'jwk', jwk, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['sign']);

            return hex(await crypto.subtle.sign('RSASSA-PKCS1-v1_5', key, ascii('{{Rs256SigningInput}}')));
            """).AsString().Should().Be(Rs256SignatureHex);
    }

    [Theory]
    [InlineData("RSASSA-PKCS1-v1_5", "verify")]
    [InlineData("RSA-PSS", "verify")]
    [InlineData("RSA-OAEP", "encrypt")]
    public void RefusesTheRawFormatWithANotSupportedError(string algorithm, string usage)
    {
        var engine = WebEngine();

        // "Otherwise: throw a NotSupportedError" — raw describes a symmetric key, which no RSA algorithm has.
        Settle(engine, $$"""
            crypto.subtle.importKey('raw', new Uint8Array(256), { name: '{{algorithm}}', hash: 'SHA-256' }, false, ['{{usage}}'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("NotSupportedError");

        Settle(engine, $$"""
            crypto.subtle.importKey('jwk', { {{Rs256PublicJwk}} }, { name: '{{algorithm}}', hash: 'SHA-256' }, true, ['{{usage}}'])
                .then(key => crypto.subtle.exportKey('raw', key))
                .then(() => 'exported', e => e.name)
            """).AsString().Should().Be("NotSupportedError");
    }

    [Fact]
    public void EachDerFormatBelongsToOneKeyType()
    {
        // "If the [[type]] internal slot of key is not 'public', then throw an InvalidAccessError" — spki
        // describes a public key and pkcs8 a private one, and neither can stand in for the other.
        Run($$"""
            const priv = await crypto.subtle.importKey(
                'jwk', { {{Rs256PrivateJwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, true, ['sign']);
            const pub = await crypto.subtle.importKey(
                'jwk', { {{Rs256PublicJwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, true, ['verify']);

            const outcomes = [];
            for (const [key, format] of [[priv, 'spki'], [pub, 'pkcs8']]) {
                try { await crypto.subtle.exportKey(format, key); outcomes.push('exported'); }
                catch (e) { outcomes.push(e.name + '/' + (e instanceof DOMException)); }
            }

            return outcomes.join(',');
            """).AsString().Should().Be("InvalidAccessError/true,InvalidAccessError/true");
    }

    [Fact]
    public void ANonExtractableRsaKeyCannotBeExportedInAnyFormat()
    {
        Run($$"""
            const key = await crypto.subtle.importKey(
                'jwk', { {{Rs256PrivateJwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['sign']);

            const outcomes = [];
            for (const format of ['pkcs8', 'jwk']) {
                try { await crypto.subtle.exportKey(format, key); outcomes.push('exported'); }
                catch (e) { outcomes.push(e.name); }
            }

            // ... and it still signs: extractable is about the material, not about the key's use.
            outcomes.push((await crypto.subtle.sign('RSASSA-PKCS1-v1_5', key, ascii('x'))).byteLength);
            return outcomes.join('|');
            """).AsString().Should().Be("InvalidAccessError|InvalidAccessError|256");
    }

    // ---------------------------------------------------------------------------------------------------
    // JSON Web Key
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void ExportsAnRsaJwkWithTheFieldsAndTheOrderWebIdlGivesIt()
    {
        // A dictionary is converted to an object member by member in lexicographical order —
        // https://webidl.spec.whatwg.org/#es-dictionary. A public key carries n and e (Section 6.3.1 of JSON
        // Web Algorithms) and a private key those plus d, p, q, dp, dq and qi (Section 6.3.2).
        Run($$"""
            const priv = await crypto.subtle.importKey(
                'jwk', { {{Rs256PrivateJwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, true, ['sign']);
            const pub = await crypto.subtle.importKey(
                'jwk', { {{Rs256PublicJwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, true, ['verify']);

            const privJwk = await crypto.subtle.exportKey('jwk', priv);
            const pubJwk = await crypto.subtle.exportKey('jwk', pub);

            return [
                Object.keys(privJwk).join(','),
                Object.keys(pubJwk).join(','),
                privJwk.kty + '/' + privJwk.alg + '/' + privJwk.ext + '/' + privJwk.key_ops.join('+'),
                pubJwk.e,
                privJwk.n === pubJwk.n,
            ].join('|');
            """).AsString().Should().Be(
                "alg,d,dp,dq,e,ext,key_ops,kty,n,p,q,qi|alg,e,ext,key_ops,kty,n|RSA/RS256/true/sign|AQAB|true");
    }

    [Theory]
    [InlineData("RSASSA-PKCS1-v1_5", "SHA-1", "RS1")]
    [InlineData("RSASSA-PKCS1-v1_5", "SHA-256", "RS256")]
    [InlineData("RSASSA-PKCS1-v1_5", "SHA-384", "RS384")]
    [InlineData("RSASSA-PKCS1-v1_5", "SHA-512", "RS512")]
    [InlineData("RSA-PSS", "SHA-1", "PS1")]
    [InlineData("RSA-PSS", "SHA-256", "PS256")]
    [InlineData("RSA-PSS", "SHA-384", "PS384")]
    [InlineData("RSA-PSS", "SHA-512", "PS512")]
    [InlineData("RSA-OAEP", "SHA-1", "RSA-OAEP")]
    [InlineData("RSA-OAEP", "SHA-256", "RSA-OAEP-256")]
    [InlineData("RSA-OAEP", "SHA-384", "RSA-OAEP-384")]
    [InlineData("RSA-OAEP", "SHA-512", "RSA-OAEP-512")]
    public void TheJwkAlgorithmNamesTheAlgorithmAndItsHash(string algorithm, string hash, string alg)
    {
        // https://www.rfc-editor.org/rfc/rfc7518#section-3.1 and #section-4.3, plus RS1 and PS1, which the
        // Web Cryptography API names for SHA-1. The round trip proves the table is read the same way it is
        // written: an exported alg must import again.
        var usage = string.Equals(algorithm, "RSA-OAEP", StringComparison.Ordinal) ? "encrypt" : "verify";

        Run($$"""
            const key = await crypto.subtle.importKey(
                'jwk', { {{Rs256PublicJwk}} }, { name: '{{algorithm}}', hash: '{{hash}}' }, true, ['{{usage}}']);
            const jwk = await crypto.subtle.exportKey('jwk', key);

            const reimported = await crypto.subtle.importKey(
                'jwk', jwk, { name: '{{algorithm}}', hash: '{{hash}}' }, true, ['{{usage}}']);

            return jwk.alg + '|' + reimported.algorithm.hash.name + '|' + reimported.algorithm.name;
            """).AsString().Should().Be(alg + "|" + hash + "|" + algorithm);
    }

    [Theory]
    // "If the kty field of jwk is not a case-sensitive string match to 'RSA', then throw a DataError."
    [InlineData("delete jwk.kty", "DataError")]
    [InlineData("jwk.kty = 'rsa'", "DataError")]
    [InlineData("jwk.kty = 'oct'", "DataError")]
    // Section 6.3.1 of JSON Web Algorithms: n and e are what an RSA public key is.
    [InlineData("delete jwk.n", "DataError")]
    [InlineData("delete jwk.e", "DataError")]
    [InlineData("jwk.n = ''", "DataError")]
    [InlineData("jwk.n = 'not+base64url'", "DataError")]
    [InlineData("jwk.n = 'AAAA'", "DataError")]
    // "If the alg field is equal to the string 'RS256' … otherwise throw a DataError", and an alg that names
    // a hash the import did not ask for is a DataError too.
    [InlineData("jwk.alg = 'RS384'", "DataError")]
    [InlineData("jwk.alg = 'PS256'", "DataError")]
    [InlineData("jwk.alg = 'HS256'", "DataError")]
    // The three fields every JWK import checks.
    [InlineData("jwk.use = 'enc'", "DataError")]
    [InlineData("jwk.key_ops = ['sign']", "DataError")]
    [InlineData("jwk.key_ops = ['verify', 'verify']", "DataError")]
    [InlineData("jwk.ext = false", "DataError")]
    public void RefusesAMalformedRsaJwkWithADataError(string mutation, string expected)
    {
        var engine = WebEngine();

        // Every row is imported as an extractable verifying key, so the `ext` and `key_ops` rows are about
        // the JWK rather than about the request.
        Settle(engine, $$"""
            (() => {
                const jwk = { {{Rs256PublicJwk}} };
                {{mutation}};
                return crypto.subtle.importKey('jwk', jwk, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, true, ['verify'])
                    .then(() => 'imported', e => e.name + '/' + (e instanceof DOMException));
            })()
            """).AsString().Should().Be(expected + "/true");
    }

    [Fact]
    public void RefusesAPrivateJwkDescribedByDAloneWithADataError()
    {
        var engine = WebEngine();

        // A documented divergence: Section 6.3.2 of JSON Web Algorithms permits a private key described by
        // n, e and d without the CRT parameters, and .NET's RSAParameters describes a private key by its CRT
        // form. Recovering the primes from (n, e, d) is a factoring routine, which is not something to write
        // here, so such a key is refused rather than half-imported.
        Settle(engine, $$"""
            (() => {
                const jwk = { {{Rs256PrivateJwk}} };
                for (const field of ['p', 'q', 'dp', 'dq', 'qi']) { delete jwk[field]; }
                return crypto.subtle.importKey('jwk', jwk, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['sign'])
                    .then(() => 'imported', e => e.name + '/' + (e.message.indexOf('CRT parameters') >= 0));
            })()
            """).AsString().Should().Be("DataError/true");

        // A partial CRT set is refused too — Section 6.3.2 requires all of them or none.
        Settle(engine, $$"""
            (() => {
                const jwk = { {{Rs256PrivateJwk}} };
                delete jwk.qi;
                return crypto.subtle.importKey('jwk', jwk, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['sign'])
                    .then(() => 'imported', e => e.name);
            })()
            """).AsString().Should().Be("DataError");
    }

    [Fact]
    public void ReadsEveryRsaJwkFieldInWebIdlsOwnOrderAndOnlyOnce()
    {
        // A dictionary's members are converted in lexicographical order, each read exactly once. A second
        // read would be a second chance for a getter to change the answer between the check and the import.
        Run($$"""
            const reads = [];
            const source = { {{Rs256PrivateJwk}} };
            const jwk = {};
            for (const name of ['qi', 'q', 'p', 'n', 'kty', 'e', 'd', 'dq', 'dp']) {
                Object.defineProperty(jwk, name, { enumerable: true, get() { reads.push(name); return source[name]; } });
            }
            Object.defineProperty(jwk, 'alg', { enumerable: true, get() { reads.push('alg'); return 'RS256'; } });
            Object.defineProperty(jwk, 'ext', { enumerable: true, get() { reads.push('ext'); return true; } });
            Object.defineProperty(jwk, 'key_ops', { enumerable: true, get() { reads.push('key_ops'); return ['sign']; } });
            Object.defineProperty(jwk, 'use', { enumerable: true, get() { reads.push('use'); return 'sig'; } });

            await crypto.subtle.importKey('jwk', jwk, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, true, ['sign']);
            return reads.join(',');
            """).AsString().Should().Be("alg,d,dp,dq,e,ext,key_ops,kty,n,p,q,qi,use");
    }

    // ---------------------------------------------------------------------------------------------------
    // The key-type and usage matrices
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void AnOperationRefusesTheWrongHalfOfTheKeyPair()
    {
        // Step 1 of every RSA operation: sign and decrypt need a private key, verify and encrypt a public
        // one. It is an InvalidAccessError, and it comes from the algorithm rather than from the usages —
        // which is why each key here carries the usage the operation asks for.
        Run($$"""
            const signPub = await crypto.subtle.importKey(
                'jwk', { {{Rs256PublicJwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['verify']);
            const signPriv = await crypto.subtle.importKey(
                'jwk', { {{Rs256PrivateJwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['sign']);
            const oaepPub = await crypto.subtle.importKey(
                'jwk', { {{OaepPublicJwk}} }, { name: 'RSA-OAEP', hash: 'SHA-256' }, false, ['encrypt']);
            const oaepPriv = await crypto.subtle.importKey(
                'jwk', { {{OaepPrivateJwk}} }, { name: 'RSA-OAEP', hash: 'SHA-256' }, false, ['decrypt']);

            const outcomes = [];
            const attempt = async fn => { try { await fn(); outcomes.push('ok'); } catch (e) { outcomes.push(e.name); } };

            // A public key cannot sign, and a private key cannot verify.
            await attempt(() => crypto.subtle.sign('RSASSA-PKCS1-v1_5', signPub, ascii('m')));
            await attempt(() => crypto.subtle.verify('RSASSA-PKCS1-v1_5', signPriv, new Uint8Array(256), ascii('m')));
            // A private key cannot encrypt, and a public key cannot decrypt.
            await attempt(() => crypto.subtle.encrypt({ name: 'RSA-OAEP' }, oaepPriv, ascii('m')));
            await attempt(() => crypto.subtle.decrypt({ name: 'RSA-OAEP' }, oaepPub, new Uint8Array(512)));

            return outcomes.join(',');
            """).AsString().Should().Be(
                "InvalidAccessError,InvalidAccessError,InvalidAccessError,InvalidAccessError");
    }

    [Theory]
    // Each format's first step names the usages that format's key may carry, and anything else is a
    // SyntaxError — which is a different failure from the InvalidAccessError a usable key used wrongly gets.
    [InlineData("spki", "RSASSA-PKCS1-v1_5", "['sign']")]
    [InlineData("spki", "RSASSA-PKCS1-v1_5", "['verify', 'sign']")]
    [InlineData("spki", "RSA-OAEP", "['decrypt']")]
    [InlineData("spki", "RSA-OAEP", "['encrypt', 'unwrapKey']")]
    [InlineData("pkcs8", "RSASSA-PKCS1-v1_5", "['verify']")]
    [InlineData("pkcs8", "RSA-OAEP", "['encrypt']")]
    [InlineData("pkcs8", "RSA-PSS", "['sign', 'verify']")]
    public void RefusesAUsageTheFormatDoesNotAllowWithASyntaxError(string format, string algorithm, string usages)
    {
        var engine = WebEngine();

        var data = string.Equals(format, "spki", StringComparison.Ordinal) ? Rs256SpkiHex : Rs256Pkcs8Hex;

        Settle(engine, $$"""
            crypto.subtle.importKey('{{format}}', bytes('{{data}}'), { name: '{{algorithm}}', hash: 'SHA-256' }, false, {{usages}})
                .then(() => 'imported', e => e.name + '/' + (e instanceof DOMException))
            """).AsString().Should().Be("SyntaxError/true");
    }

    [Fact]
    public void TheUsagesAreCheckedBeforeTheKeyDataIsParsed()
    {
        var engine = WebEngine();

        // "If usages contains an entry which is not 'verify', then throw a SyntaxError" is step 1 of the
        // spki branch and the parse is step 2, so bytes that are not a SubjectPublicKeyInfo at all still
        // produce the SyntaxError the usages earn.
        Settle(engine, """
            crypto.subtle.importKey('spki', new Uint8Array([1, 2, 3]), { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['sign'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("SyntaxError");

        // With usages the format does allow, the same bytes reach the parse and fail there instead.
        Settle(engine, """
            crypto.subtle.importKey('spki', new Uint8Array([1, 2, 3]), { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['verify'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("DataError");
    }

    [Theory]
    // A key must permit the operation being asked of it, or the answer is an InvalidAccessError. For a
    // signature algorithm the check can never fire — each key type's permitted usage set is exactly the one
    // operation it can perform, so a key carrying the wrong usage cannot be built in the first place. It is
    // RSA-OAEP, whose key types carry two usages each, where the check has something to do.
    [InlineData("encrypt", "public", "['wrapKey']", "InvalidAccessError")]
    [InlineData("encrypt", "public", "['encrypt']", "ok")]
    [InlineData("encrypt", "public", "['encrypt', 'wrapKey']", "ok")]
    [InlineData("decrypt", "private", "['unwrapKey']", "InvalidAccessError")]
    [InlineData("decrypt", "private", "['decrypt']", "ok")]
    [InlineData("decrypt", "private", "['decrypt', 'unwrapKey']", "ok")]
    public void EnforcesTheRsaOaepUsageMatrix(string operation, string keyType, string usages, string expected)
    {
        var jwk = string.Equals(keyType, "public", StringComparison.Ordinal) ? OaepPublicJwk : OaepPrivateJwk;

        // The decrypt rows need something their own key made, so an encryptor is imported alongside.
        Run($$"""
            const encryptor = await crypto.subtle.importKey(
                'jwk', { {{OaepPublicJwk}} }, { name: 'RSA-OAEP', hash: 'SHA-256' }, false, ['encrypt']);
            const sample = await crypto.subtle.encrypt({ name: 'RSA-OAEP' }, encryptor, ascii('m'));

            const key = await crypto.subtle.importKey(
                'jwk', { {{jwk}} }, { name: 'RSA-OAEP', hash: 'SHA-256' }, false, {{usages}});
            try {
                await crypto.subtle.{{operation}}({ name: 'RSA-OAEP' }, key, {{(string.Equals(operation, "encrypt", StringComparison.Ordinal) ? "ascii('m')" : "sample")}});
                return 'ok';
            } catch (e) {
                return e.name;
            }
            """).AsString().Should().Be(expected);
    }

    [Fact]
    public void ASignatureKeyTypeCarriesExactlyTheOneUsageItCanPerform()
    {
        var engine = WebEngine();

        // The reason the matrix above is about RSA-OAEP: for RSASSA-PKCS1-v1_5 and RSA-PSS, a private key
        // may carry only 'sign' and a public key only 'verify', so the mismatch is caught at import as the
        // SyntaxError the format's first step raises — a key carrying the wrong usage never exists.
        foreach (var (jwk, usages) in new[] { (Rs256PrivateJwk, "['verify']"), (Rs256PublicJwk, "['sign']") })
        {
            Settle(engine, $$"""
                crypto.subtle.importKey('jwk', { {{jwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, {{usages}})
                    .then(() => 'imported', e => e.name + '/' + (e instanceof DOMException))
                """).AsString().Should().Be("SyntaxError/true");
        }
    }

    [Fact]
    public void APublicKeyMayBeImportedWithNoUsagesAtAllAndAPrivateKeyMayNot()
    {
        var engine = WebEngine();

        // "If the [[type]] internal slot of result is 'secret' or 'private' and usages is empty, then throw
        // a SyntaxError" — a public key is deliberately outside that step, so importing a certificate's key
        // to read its algorithm without granting it a use is an ordinary thing to do.
        Settle(engine, $$"""
            crypto.subtle.importKey('spki', bytes('{{Rs256SpkiHex}}'), { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, [])
                .then(key => key.type + '/' + key.usages.length + '/' + key.algorithm.modulusLength, e => e.name)
            """).AsString().Should().Be("public/0/2048");

        Settle(engine, $$"""
            crypto.subtle.importKey('pkcs8', bytes('{{Rs256Pkcs8Hex}}'), { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, [])
                .then(() => 'imported', e => e.name + '/' + (e instanceof DOMException))
            """).AsString().Should().Be("SyntaxError/true");

        // The same asymmetry through jwk, where the type is decided by whether `d` is present.
        Settle(engine, $$"""
            crypto.subtle.importKey('jwk', { {{Rs256PublicJwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, [])
                .then(key => key.type, e => e.name)
            """).AsString().Should().Be("public");

        Settle(engine, $$"""
            crypto.subtle.importKey('jwk', { {{Rs256PrivateJwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, [])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("SyntaxError");
    }

    [Fact]
    public void AKeyRemembersTheAlgorithmItWasMadeForAndRefusesAnother()
    {
        Run($$"""
            const rsassa = await crypto.subtle.importKey(
                'jwk', { {{Rs256PrivateJwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['sign']);
            const pss = await crypto.subtle.importKey(
                'jwk', { {{Rs256PrivateJwk}} }, { name: 'RSA-PSS', hash: 'SHA-256' }, false, ['sign']);

            const outcomes = [];
            const attempt = async fn => { try { await fn(); outcomes.push('ok'); } catch (e) { outcomes.push(e.name); } };

            // The name check comes before everything the algorithm itself does, and it is an
            // InvalidAccessError — the same key material described two ways is two keys.
            await attempt(() => crypto.subtle.sign({ name: 'RSA-PSS', saltLength: 32 }, rsassa, ascii('m')));
            await attempt(() => crypto.subtle.sign('RSASSA-PKCS1-v1_5', pss, ascii('m')));
            // An algorithm not registered for the operation at all is a different failure, decided before
            // any key is looked at.
            await attempt(() => crypto.subtle.encrypt({ name: 'RSASSA-PKCS1-v1_5' }, rsassa, ascii('m')));
            await attempt(() => crypto.subtle.sign('RSA-OAEP', rsassa, ascii('m')));

            return outcomes.join(',');
            """).AsString().Should().Be(
                "InvalidAccessError,InvalidAccessError,NotSupportedError,NotSupportedError");
    }

    // ---------------------------------------------------------------------------------------------------
    // generateKey and CryptoKeyPair
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void GeneratesASigningPairWithTheShapeTheSpecificationDescribes()
    {
        // The one place an RSA key pair is generated in this file at all, and the only place the
        // CryptoKeyPair dictionary is examined in full. 2048 bits: a prime search is the most expensive
        // thing in this API by orders of magnitude.
        Run("""
            const pair = await crypto.subtle.generateKey(
                { name: 'RSASSA-PKCS1-v1_5', modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: 'SHA-256' },
                false,
                ['sign', 'verify']);

            const message = ascii('generated');
            const signature = await crypto.subtle.sign('RSASSA-PKCS1-v1_5', pair.privateKey, message);

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
                // The usages are split by the usage intersection each half's steps name.
                pair.privateKey.usages.join('+') + '/' + pair.publicKey.usages.join('+'),

                // One RsaHashedKeyAlgorithm, shared by both halves.
                Object.keys(pair.privateKey.algorithm).join(','),
                pair.privateKey.algorithm.name + '/' + pair.privateKey.algorithm.modulusLength + '/' + pair.privateKey.algorithm.hash.name,
                // publicExponent is a BigInteger, which is a Uint8Array holding a big-endian magnitude.
                Object.prototype.toString.call(pair.publicKey.algorithm.publicExponent) + '/' + hex(pair.publicKey.algorithm.publicExponent),

                signature.byteLength,
                await crypto.subtle.verify('RSASSA-PKCS1-v1_5', pair.publicKey, signature, message),
            ].join('|');
            """).AsString().Should().Be(
                "privateKey,publicKey|true|undefined|true|"
                + "private/public|false/true|sign/verify|"
                + "name,modulusLength,publicExponent,hash|RSASSA-PKCS1-v1_5/2048/SHA-256|[object Uint8Array]/010001|"
                + "256|true");
    }

    [Fact]
    public void GeneratesAnOaepPairThatSplitsTheWrappingUsagesToo()
    {
        Run("""
            const pair = await crypto.subtle.generateKey(
                { name: 'RSA-OAEP', modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: 'SHA-256' },
                true,
                ['encrypt', 'decrypt', 'wrapKey', 'unwrapKey']);

            const plaintext = ascii('round trip');
            const ciphertext = await crypto.subtle.encrypt({ name: 'RSA-OAEP' }, pair.publicKey, plaintext);
            const back = await crypto.subtle.decrypt({ name: 'RSA-OAEP' }, pair.privateKey, ciphertext);

            return [
                pair.publicKey.usages.join('+'),
                pair.privateKey.usages.join('+'),
                pair.privateKey.algorithm.name,
                hex(back) === hex(plaintext),
            ].join('|');
            """).AsString().Should().Be("encrypt+wrapKey|decrypt+unwrapKey|RSA-OAEP|true");
    }

    [Fact]
    public void RefusesAPairWhosePrivateHalfWouldHaveNoUsages()
    {
        var engine = WebEngine();

        // "If result is a CryptoKeyPair object: If the [[usages]] internal slot of the privateKey attribute
        // of result is the empty sequence, then throw a SyntaxError." A pair nobody can sign with is the
        // mistake this catches; a pair whose *public* half carries nothing is perfectly ordinary.
        Settle(engine, """
            crypto.subtle.generateKey(
                { name: 'RSASSA-PKCS1-v1_5', modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: 'SHA-256' },
                false, ['verify'])
                .then(() => 'generated', e => e.name + '/' + (e instanceof DOMException))
            """).AsString().Should().Be("SyntaxError/true");

        Settle(engine, """
            crypto.subtle.generateKey(
                { name: 'RSASSA-PKCS1-v1_5', modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: 'SHA-256' },
                false, ['sign'])
                .then(pair => pair.privateKey.usages.join('+') + '/' + pair.publicKey.usages.length, e => e.name)
            """).AsString().Should().Be("sign/0");
    }

    [Theory]
    [InlineData("['encrypt']")]
    [InlineData("['sign', 'deriveKey']")]
    [InlineData("['sign', 'wrapKey']")]
    public void RefusesAUsageASignatureAlgorithmDoesNotSupportWithASyntaxError(string usages)
    {
        var engine = WebEngine();

        // Step 1 of generateKey, which runs before the prime search — so this test generates nothing.
        Settle(engine, $$"""
            crypto.subtle.generateKey(
                { name: 'RSASSA-PKCS1-v1_5', modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: 'SHA-256' },
                false, {{usages}})
                .then(() => 'generated', e => e.name)
            """).AsString().Should().Be("SyntaxError");
    }

    [Theory]
    [InlineData("new Uint8Array([3])")]
    [InlineData("new Uint8Array([1, 0, 0, 1])")]
    [InlineData("new Uint8Array([0, 1, 0, 1, 0, 1])")]
    public void RefusesAPublicExponentOtherThan65537(string exponent)
    {
        var engine = WebEngine();

        // A documented divergence: RSA.Create takes a key size and nothing else, so 65537 is the only
        // exponent .NET's key generation can be asked for. Each of these is a *valid* exponent by the
        // specification's own validation, so the failure is the platform's rather than the caller's.
        Settle(engine, $$"""
            crypto.subtle.generateKey(
                { name: 'RSASSA-PKCS1-v1_5', modulusLength: 2048, publicExponent: {{exponent}}, hash: 'SHA-256' },
                false, ['sign', 'verify'])
                .then(() => 'generated', e => e.name + '/' + (e.message.indexOf('65537') >= 0))
            """).AsString().Should().Be("OperationError/true");
    }

    [Theory]
    // "If publicExponent is less than 3, is even, or is greater than or equal to 2^modulusLength - 1, then
    // throw an OperationError" — the specification's own validation, which runs before the platform's.
    [InlineData("2048", "new Uint8Array([])")]
    [InlineData("2048", "new Uint8Array([0])")]
    [InlineData("2048", "new Uint8Array([1])")]
    [InlineData("2048", "new Uint8Array([2])")]
    [InlineData("2048", "new Uint8Array([4])")]
    [InlineData("8", "new Uint8Array([1, 0, 1])")]
    // "If modulusLength is less than 4".
    [InlineData("3", "new Uint8Array([1, 0, 1])")]
    [InlineData("0", "new Uint8Array([1, 0, 1])")]
    public void RefusesKeyGenerationParametersTheSpecificationRejects(string modulusLength, string exponent)
    {
        var engine = WebEngine();

        Settle(engine, $$"""
            crypto.subtle.generateKey(
                { name: 'RSA-PSS', modulusLength: {{modulusLength}}, publicExponent: {{exponent}}, hash: 'SHA-256' },
                false, ['sign', 'verify'])
                .then(() => 'generated', e => e.name + '/' + (e instanceof DOMException))
            """).AsString().Should().Be("OperationError/true");
    }

    [Theory]
    // Below what any RSA implementation .NET can reach will generate, not a multiple of the platform's
    // stride, and above this engine's own ceiling.
    [InlineData("256")]
    [InlineData("511")]
    [InlineData("2049")]
    [InlineData("16384")]
    [InlineData("65536")]
    public void RefusesAModulusLengthThisEngineWillNotGenerate(string modulusLength)
    {
        var engine = WebEngine();

        // None of these reaches a prime search: every one is refused before RSA.Create is called at all.
        Settle(engine, $$"""
            crypto.subtle.generateKey(
                { name: 'RSA-OAEP', modulusLength: {{modulusLength}}, publicExponent: new Uint8Array([1, 0, 1]), hash: 'SHA-256' },
                false, ['encrypt', 'decrypt'])
                .then(() => 'generated', e => e.name + '/' + (e instanceof DOMException))
            """).AsString().Should().Be("OperationError/true");
    }

    [Fact]
    public void TheKeyGenerationMembersAreRequiredAndRangeChecked()
    {
        var engine = WebEngine();

        // `required [EnforceRange] unsigned long modulusLength`, `required BigInteger publicExponent` and
        // `required HashAlgorithmIdentifier hash` — an absent one is the TypeError WebIDL raises.
        foreach (var algorithm in new[]
        {
            "'RSASSA-PKCS1-v1_5'",
            "{ name: 'RSASSA-PKCS1-v1_5' }",
            "{ name: 'RSASSA-PKCS1-v1_5', modulusLength: 2048 }",
            "{ name: 'RSASSA-PKCS1-v1_5', modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]) }",
            "{ name: 'RSASSA-PKCS1-v1_5', modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: undefined }",
            // BigInteger is `typedef Uint8Array`, and WebIDL's conversion for a named typed array type
            // accepts that type alone — an ArrayBuffer, a DataView and an Int8Array are all TypeErrors.
            "{ name: 'RSASSA-PKCS1-v1_5', modulusLength: 2048, publicExponent: new ArrayBuffer(3), hash: 'SHA-256' }",
            "{ name: 'RSASSA-PKCS1-v1_5', modulusLength: 2048, publicExponent: new DataView(new ArrayBuffer(3)), hash: 'SHA-256' }",
            "{ name: 'RSASSA-PKCS1-v1_5', modulusLength: 2048, publicExponent: new Int8Array(3), hash: 'SHA-256' }",
            "{ name: 'RSASSA-PKCS1-v1_5', modulusLength: 2048, publicExponent: [1, 0, 1], hash: 'SHA-256' }",
            // [EnforceRange]: a value outside the type is a TypeError rather than a wrap or a clamp.
            "{ name: 'RSASSA-PKCS1-v1_5', modulusLength: -1, publicExponent: new Uint8Array([1, 0, 1]), hash: 'SHA-256' }",
            "{ name: 'RSASSA-PKCS1-v1_5', modulusLength: Infinity, publicExponent: new Uint8Array([1, 0, 1]), hash: 'SHA-256' }",
        })
        {
            Settle(engine, $$"""
                crypto.subtle.generateKey({{algorithm}}, false, ['sign', 'verify'])
                    .then(() => 'generated', e => e.constructor.name)
                """).AsString().Should().Be("TypeError");
        }

        // The hash is normalized as a digest algorithm, so a name that is not a registered one is a
        // NotSupportedError rather than a TypeError.
        Settle(engine, """
            crypto.subtle.generateKey(
                { name: 'RSASSA-PKCS1-v1_5', modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: 'MD5' },
                false, ['sign', 'verify'])
                .then(() => 'generated', e => e.name)
            """).AsString().Should().Be("NotSupportedError");
    }

    // ---------------------------------------------------------------------------------------------------
    // Normalization and argument conversion
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("'rsassa-pkcs1-v1_5'")]
    [InlineData("'RSASSA-PKCS1-V1_5'")]
    [InlineData("{ name: 'rSaSsA-pKcS1-V1_5' }")]
    public void MatchesTheRegisteredRsaNameCaseInsensitively(string algorithm)
    {
        // "Case-insensitive" is ASCII case-insensitive, and the key remembers the registered spelling rather
        // than the caller's — which is what makes the name check on the next operation work at all.
        Run($$"""
            const key = await crypto.subtle.importKey(
                'jwk', { {{Rs256PrivateJwk}} }, { name: 'rsassa-pkcs1-v1_5', hash: 'sha-256' }, false, ['sign']);

            return hex(await crypto.subtle.sign({{algorithm}}, key, ascii('{{Rs256SigningInput}}')))
                + '|' + key.algorithm.name + '|' + key.algorithm.hash.name;
            """).AsString().Should().Be(Rs256SignatureHex + "|RSASSA-PKCS1-v1_5|SHA-256");
    }

    [Fact]
    public void TheHashIsARequiredMemberOfRsaHashedImportParams()
    {
        var engine = WebEngine();

        foreach (var algorithm in new[] { "'RSA-PSS'", "{ name: 'RSA-PSS' }", "{ name: 'RSA-PSS', hash: undefined }" })
        {
            Settle(engine, $$"""
                crypto.subtle.importKey('spki', bytes('{{Rs256SpkiHex}}'), {{algorithm}}, false, ['verify'])
                    .then(() => 'imported', e => e.constructor.name)
                """).AsString().Should().Be("TypeError");
        }
    }

    [Fact]
    public void RefusesDerThatIsNotTheStructureTheFormatNames()
    {
        var engine = WebEngine();

        // "If an error occurred while parsing, then throw a DataError" — including an spki that is really a
        // pkcs8 and the other way round, which parse cleanly as themselves and not at all as each other.
        Settle(engine, $$"""
            crypto.subtle.importKey('spki', bytes('{{Rs256Pkcs8Hex}}'), { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['verify'])
                .then(() => 'imported', e => e.name + '/' + (e instanceof DOMException))
            """).AsString().Should().Be("DataError/true");

        Settle(engine, $$"""
            crypto.subtle.importKey('pkcs8', bytes('{{Rs256SpkiHex}}'), { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['sign'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("DataError");

        foreach (var data in new[] { "new Uint8Array(0)", "new Uint8Array(16)", "new Uint8Array([0x30, 0x03, 0x02, 0x01, 0x00])" })
        {
            Settle(engine, $$"""
                crypto.subtle.importKey('spki', {{data}}, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['verify'])
                    .then(() => 'imported', e => e.name)
                """).AsString().Should().Be("DataError");
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
                const der = bytes('{{Rs256SpkiHex}}');
                const padded = new Uint8Array(der.length + 1);
                padded.set(der);
                return crypto.subtle.importKey('spki', padded, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['verify'])
                    .then(() => 'imported', e => e.name + '/' + (e.message.indexOf('trailing') >= 0));
            })()
            """).AsString().Should().Be("DataError/true");
    }

    [Fact]
    public void NoRsaOperationEverThrowsSynchronouslyOrLeaksACryptographicException()
    {
        var engine = WebEngine();

        // A promise-returning WebIDL operation reports every failure as a rejection, and the failures the
        // platform's own cryptography raises are no exception: a CryptographicException reaching the host
        // would be a CLR exception erupting out of a promise-returning API.
        engine.Evaluate($$"""
            (() => {
                const calls = [
                    () => crypto.subtle.importKey('spki', new Uint8Array([9, 9, 9]), { name: 'RSA-PSS', hash: 'SHA-256' }, false, ['verify']),
                    () => crypto.subtle.importKey('pkcs8', new Uint8Array([9, 9, 9]), { name: 'RSA-OAEP', hash: 'SHA-256' }, false, ['decrypt']),
                    () => crypto.subtle.importKey('jwk', { kty: 'RSA', n: 'AQAB', e: 'AQAB' }, { name: 'RSA-OAEP', hash: 'SHA-256' }, false, ['encrypt']),
                    () => crypto.subtle.generateKey({ name: 'RSA-PSS', modulusLength: 7, publicExponent: new Uint8Array([1, 0, 1]), hash: 'SHA-256' }, false, ['sign']),
                ];

                return calls.map(call => {
                    const promise = call();
                    return (promise instanceof Promise) + ':' + typeof promise.then;
                }).join(',');
            })()
            """).AsString().Should().Be("true:function,true:function,true:function,true:function");

        // ... and each of them settles as a rejection carrying a DOMException.
        Settle(engine, """
            Promise.all([
                crypto.subtle.importKey('spki', new Uint8Array([9, 9, 9]), { name: 'RSA-PSS', hash: 'SHA-256' }, false, ['verify']).catch(e => e.name),
                crypto.subtle.importKey('pkcs8', new Uint8Array([9, 9, 9]), { name: 'RSA-OAEP', hash: 'SHA-256' }, false, ['decrypt']).catch(e => e.name),
                crypto.subtle.importKey('jwk', { kty: 'RSA', n: 'AQAB', e: 'AQAB' }, { name: 'RSA-OAEP', hash: 'SHA-256' }, false, ['encrypt']).catch(e => e.name),
                crypto.subtle.generateKey({ name: 'RSA-PSS', modulusLength: 7, publicExponent: new Uint8Array([1, 0, 1]), hash: 'SHA-256' }, false, ['sign']).catch(e => e.name),
            ]).then(names => names.join(','))
            """).AsString().Should().Be("DataError,DataError,DataError,OperationError");
    }

    [Fact]
    public void TheRsaAlgorithmsAreRegisteredForExactlyTheOperationsTheSpecificationGivesThem()
    {
        var engine = WebEngine();

        // The registry decides which algorithm reaches which operation, and an algorithm that is not
        // registered for one is a NotSupportedError — a signature algorithm cannot encrypt, and a cipher
        // cannot sign.
        Settle(engine, $$"""
            (async () => {
                const outcomes = [];
                const attempt = async fn => { try { await fn(); outcomes.push('ok'); } catch (e) { outcomes.push(e.name); } };

                await attempt(() => crypto.subtle.digest('RSA-OAEP', new Uint8Array(0)));
                await attempt(() => crypto.subtle.importKey('jwk', { {{Rs256PublicJwk}} }, { name: 'RSA-OAEP', hash: 'SHA-256' }, false, ['encrypt']));
                await attempt(() => crypto.subtle.importKey('jwk', { {{Rs256PublicJwk}} }, { name: 'RSA-PSS', hash: 'SHA-256' }, false, ['verify']));
                await attempt(() => crypto.subtle.importKey('jwk', { {{Rs256PublicJwk}} }, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['verify']));

                return outcomes.join(',');
            })()
            """).AsString().Should().Be("NotSupportedError,ok,ok,ok");
    }

    // ---------------------------------------------------------------------------------------------------
    // Test vectors
    //
    // The JWKs and the expected outputs are transcribed from the RFCs named in this class's remarks. The
    // pkcs8 and spki bytes were DER-encoded from the RFC 7515 JWK's own integers outside this engine.
    // ---------------------------------------------------------------------------------------------------

    private const string Rs256PrivateJwk =
        "kty: 'RSA', n: 'ofgWCuLjybRlzo0tZWJjNiuSfb4p4fAkd_wWJcyQoTbji9k0l8W26mPddxHmfHQp-Vaw-4qPCJrcS2mJPMEzP1Pt0Bm4d4QlL-yRT-SFd2lZS-pCgNMsD1W_YpRPEwOWvG6b32690r2jZ47soMZo9wGzjb_7OMg0LOL-bSf63kpaSHSXndS5z5rexMdbBYUsLA9e-KXBdQOS-UTo7WTBEMa2R2CapHg665xsmtdVMTBQY4uDZlxvb3qCo5ZwKh9kG4LT6_I5IhlJH7aGhyxXFvUK-DWNmoudF8NAco9_h9iaGNj8q2ethFkMLs91kzk2PAcDTW9gb54h4FRWyuXpoQ', e: 'AQAB', d: 'Eq5xpGnNCivDflJsRQBXHx1hdR1k6Ulwe2JZD50LpXyWPEAeP88vLNO97IjlA7_GQ5sLKMgvfTeXZx9SE-7YwVol2NXOoAJe46sui395IW_GO-pWJ1O0BkTGoVEn2bKVRUCgu-GjBVaYLU6f3l9kJfFNS3E0QbVdxzubSu3Mkqzjkn439X0M_V51gfpRLI9JYanrC4D4qAdGcopV_0ZHHzQlBjudU2QvXt4ehNYTCBr6XCLQUShb1juUO1ZdiYoFaFQT5Tw8bGUl_x_jTj3ccPDVZFD9pIuhLhBOneufuBiB4cS98l2SR_RQyGWSeWjnczT0QU91p1DhOVRuOopznQ', p: '4BzEEOtIpmVdVEZNCqS7baC4crd0pqnRH_5IB3jw3bcxGn6QLvnEtfdUdiYrqBdss1l58BQ3KhooKeQTa9AB0Hw_Py5PJdTJNPY8cQn7ouZ2KKDcmnPGBY5t7yLc1QlQ5xHdwW1VhvKn-nXqhJTBgIPgtldC-KDV5z-y2XDwGUc', q: 'uQPEfgmVtjL0Uyyx88GZFF1fOunH3-7cepKmtH4pxhtCoHqpWmT8YAmZxaewHgHAjLYsp1ZSe7zFYHj7C6ul7TjeLQeZD_YwD66t62wDmpe_HlB-TnBA-njbglfIsRLtXlnDzQkv5dTltRJ11BKBBypeeF6689rjcJIDEz9RWdc', dp: 'BwKfV3Akq5_MFZDFZCnW-wzl-CCo83WoZvnLQwCTeDv8uzluRSnm71I3QCLdhrqE2e9YkxvuxdBfpT_PI7Yz-FOKnu1R6HsJeDCjn12Sk3vmAktV2zb34MCdy7cpdTh_YVr7tss2u6vneTwrA86rZtu5Mbr1C1XsmvkxHQAdYo0', dq: 'h_96-mK1R_7glhsum81dZxjTnYynPbZpHziZjeeHcXYsXaaMwkOlODsWa7I9xXDoRwbKgB719rrmI2oKr6N3Do9U0ajaHF-NKJnwgjMd2w9cjz3_-kyNlxAr2v4IKhGNpmM5iIgOS1VZnOZ68m6_pbLBSp3nssTdlqvd0tIiTHU', qi: 'IYd7DHOhrWvxkwPQsRM2tOgrjbcrfvtQJipd-DlcxyVuuM9sQLdgjVk2oy26F0EmpScGLq2MowX7fhd_QJQ3ydy5cY7YIBi87w93IKLEdfnbJtoOPLUW0ITrJReOgo1cq9SbsxYawBgfp_gh6A5603k2-ZQwVK0JKSHuLFkuQ3U'";

    private const string Rs256PublicJwk =
        "kty: 'RSA', n: 'ofgWCuLjybRlzo0tZWJjNiuSfb4p4fAkd_wWJcyQoTbji9k0l8W26mPddxHmfHQp-Vaw-4qPCJrcS2mJPMEzP1Pt0Bm4d4QlL-yRT-SFd2lZS-pCgNMsD1W_YpRPEwOWvG6b32690r2jZ47soMZo9wGzjb_7OMg0LOL-bSf63kpaSHSXndS5z5rexMdbBYUsLA9e-KXBdQOS-UTo7WTBEMa2R2CapHg665xsmtdVMTBQY4uDZlxvb3qCo5ZwKh9kG4LT6_I5IhlJH7aGhyxXFvUK-DWNmoudF8NAco9_h9iaGNj8q2ethFkMLs91kzk2PAcDTW9gb54h4FRWyuXpoQ', e: 'AQAB'";

    private const string Rs256Pkcs8Hex =
        "308204bd020100300d06092a864886f70d0101010500048204a7308204a30201000282010100a1f8160ae2e3c9b465ce8d2d656263362b927dbe29e1f02477fc1625cc90a136e38bd93497c5b6ea63dd7711e67c7429f956b0fb8a8f089adc4b69893cc1333f53edd019b87784252fec914fe4857769594bea4280d32c0f55bf62944f130396bc6e9bdf6ebdd2bda3678eeca0c668f701b38dbffb38c8342ce2fe6d27fade4a5a4874979dd4b9cf9adec4c75b05852c2c0f5ef8a5c1750392f944e8ed64c110c6b647609aa4783aeb9c6c9ad755313050638b83665c6f6f7a82a396702a1f641b82d3ebf2392219491fb686872c5716f50af8358d9a8b9d17c340728f7f87d89a18d8fcab67ad84590c2ecf759339363c07034d6f606f9e21e05456cae5e9a102030100010282010012ae71a469cd0a2bc37e526c4500571f1d61751d64e949707b62590f9d0ba57c963c401e3fcf2f2cd3bdec88e503bfc6439b0b28c82f7d3797671f5213eed8c15a25d8d5cea0025ee3ab2e8b7f79216fc63bea562753b40644c6a15127d9b2954540a0bbe1a30556982d4e9fde5f6425f14d4b713441b55dc73b9b4aedcc92ace3927e37f57d0cfd5e7581fa512c8f4961a9eb0b80f8a80746728a55ff46471f3425063b9d53642f5ede1e84d613081afa5c22d051285bd63b943b565d898a05685413e53c3c6c6525ff1fe34e3ddc70f0d56450fda48ba12e104e9deb9fb81881e1c4bdf25d9247f450c865927968e77334f4414f75a750e139546e3a8a739d02818100e01cc410eb48a6655d54464d0aa4bb6da0b872b774a6a9d11ffe480778f0ddb7311a7e902ef9c4b5f75476262ba8176cb35979f014372a1a2829e4136bd001d07c3f3f2e4f25d4c934f63c7109fba2e67628a0dc9a73c6058e6def22dcd50950e711ddc16d5586f2a7fa75ea8494c18083e0b65742f8a0d5e73fb2d970f0194702818100b903c47e0995b632f4532cb1f3c199145d5f3ae9c7dfeedc7a92a6b47e29c61b42a07aa95a64fc600999c5a7b01e01c08cb62ca756527bbcc56078fb0baba5ed38de2d07990ff6300faeadeb6c039a97bf1e507e4e7040fa78db8257c8b112ed5e59c3cd092fe5d4e5b51275d41281072a5e785ebaf3dae3709203133f5159d702818007029f577024ab9fcc1590c56429d6fb0ce5f820a8f375a866f9cb430093783bfcbb396e4529e6ef52374022dd86ba84d9ef58931beec5d05fa53fcf23b633f8538a9eed51e87b097830a39f5d92937be6024b55db36f7e0c09dcbb72975387f615afbb6cb36bbabe7793c2b03ceab66dbb931baf50b55ec9af9311d001d628d0281810087ff7afa62b547fee0961b2e9bcd5d6718d39d8ca73db6691f38998de78771762c5da68cc243a5383b166bb23dc570e84706ca801ef5f6bae6236a0aafa3770e8f54d1a8da1c5f8d2899f082331ddb0f5c8f3dfffa4c8d97102bdafe082a118da6633988880e4b55599ce67af26ebfa5b2c14a9de7b2c4dd96abddd2d2224c7502818021877b0c73a1ad6bf19303d0b11336b4e82b8db72b7efb50262a5df8395cc7256eb8cf6c40b7608d5936a32dba174126a527062ead8ca305fb7e177f409437c9dcb9718ed82018bcef0f7720a2c475f9db26da0e3cb516d084eb25178e828d5cabd49bb3161ac0181fa7f821e80e7ad37936f9943054ad092921ee2c592e4375";

    private const string Rs256SpkiHex =
        "30820122300d06092a864886f70d01010105000382010f003082010a0282010100a1f8160ae2e3c9b465ce8d2d656263362b927dbe29e1f02477fc1625cc90a136e38bd93497c5b6ea63dd7711e67c7429f956b0fb8a8f089adc4b69893cc1333f53edd019b87784252fec914fe4857769594bea4280d32c0f55bf62944f130396bc6e9bdf6ebdd2bda3678eeca0c668f701b38dbffb38c8342ce2fe6d27fade4a5a4874979dd4b9cf9adec4c75b05852c2c0f5ef8a5c1750392f944e8ed64c110c6b647609aa4783aeb9c6c9ad755313050638b83665c6f6f7a82a396702a1f641b82d3ebf2392219491fb686872c5716f50af8358d9a8b9d17c340728f7f87d89a18d8fcab67ad84590c2ecf759339363c07034d6f606f9e21e05456cae5e9a10203010001";

    private const string Rs256SigningInput =
        "eyJhbGciOiJSUzI1NiJ9.eyJpc3MiOiJqb2UiLA0KICJleHAiOjEzMDA4MTkzODAsDQogImh0dHA6Ly9leGFtcGxlLmNvbS9pc19yb290Ijp0cnVlfQ";

    private const string Rs256SignatureHex =
        "702e218943e88fd11eb5d82dbf7845f34106ae1b81fff7731116add1717d83656d420afd3c96eedd73a2663e5166687b000b87226e0187ed1073f945e582adfcef16d85a798ee8c66ddb3db8975b17d09402beedd5d9d97007108db28160d5f8040ca7445762b81fbe7ff9d92e0ae76f24f25b33bbe6f44ae61eb1040acb20044d3ef9128ed40130795bd4bd3b41eecad066ab651981fde48df77f372dc38b9fafdd3befb18b5da3cc3c2eb02f9e3a41d612caad15911273a05f23b9e838faaf849d698429ef5a1e88798236c3d40e604522a544c8f27a7a2db80663d16cf7caea56de405cb2215a45b2c25566b55ac1a748a070dfc8a32a469543d019eefb47";

    private const string PssPrivateJwk =
        "kty: 'RSA', n: 'n4EPtAOCc9AlkeQHPzHStgAbgs7bTZLwUBZdR8_KuKPEHLd4rHVTeT-O-XV2jRojdNhxJWTDvNd7nqQ0VEiZQHz_AJmSCpMaJMRBSFKrKb2wqVwGU_NsYOYL-QtiWN2lbzcEe6XC0dApr5ydQLrHqkHHig3RBordaZ6Aj-oBHqFEHYpPe7Tpe-OfVfHd1E6cS6M1FZcD1NNLYD5lFHpPI9bTwJlsde3uhGqC0ZCuEHg8lhzwOHrtIQbS0FVbb9k3-tVTU4fg_3L_vniUFAKwuCLqKnS2BYwdq_mzSnbLY7h_qixoR7jig3__kRhuaxwUkRz5iaiQkqgc5gHdrNP5zw', e: 'AQAB', d: 'bWUC9B-EFRIo8kpGfh0ZuyGPvMNKvYWNtB_ikiH9k20eT-O1q_I78eiZkpXxXQ0UTEs2LsNRS-8uJbvQ-A1irkwMSMkK1J3XTGgdrhCku9gRldY7sNA_AKZGh-Q661_42rINLRCe8W-nZ34ui_qOfkLnK9QWDDqpaIsA-bMwWWSDFu2MUBYwkHTMEzLYGqOe04noqeq1hExBTHBOBdkMXiuFhUq1BU6l-DqEiWxqg82sXt2h-LMnT3046AOYJoRioz75tSUQfGCshWTBnP5uDjd18kKhyv07lhfSJdrPdM5Plyl21hsFf4L_mHCuoFau7gdsPfHPxxjVOcOpBrQzwQ', p: '3Slxg_DwTXJcb6095RoXygQCAZ5RnAvZlno1yhHtnUex_fp7AZ_9nRaO7HX_-SFfGQeutao2TDjDAWU4Vupk8rw9JR0AzZ0N2fvuIAmr_WCsmGpeNqQnev1T7IyEsnh8UMt-n5CafhkikzhEsrmndH6LxOrvRJlsPp6Zv8bUq0k', q: 'uKE2dh-cTf6ERF4k4e_jy78GfPYUIaUyoSSJuBzp3Cubk3OCqs6grT8bR_cu0Dm1MZwWmtdqDyI95HrUeq3MP15vMMON8lHTeZu2lmKvwqW7anV5UzhM1iZ7z4yMkuUwFWoBvyY898EXvRD-hdqRxHlSqAZ192zB3pVFJ0s7pFc', dp: 'B8PVvXkvJrj2L-GYQ7v3y9r6Kw5g9SahXBwsWUzp19TVlgI-YV85q1NIb1rxQtD-IsXXR3-TanevuRPRt5OBOdiMGQp8pbt26gljYfKU_E9xn-RULHz0-ed9E9gXLKD4VGngpz-PfQ_q29pk5xWHoJp009Qf1HvChixRX59ehik', dq: 'CLDmDGduhylc9o7r84rEUVn7pzQ6PF83Y-iBZx5NT-TpnOZKF1pErAMVeKzFEl41DlHHqqBLSM0W1sOFbwTxYWZDm6sI6og5iTbwQGIC3gnJKbi_7k_vJgGHwHxgPaX2PnvP-zyEkDERuf-ry4c_Z11Cq9AqC2yeL6kdKT1cYF8', qi: '3PiqvXQN0zwMeE-sBvZgi289XP9XCQF3VWqPzMKnIgQp7_Tugo6-NZBKCQsMf3HaEGBjTVJs_jcK8-TRXvaKe-7ZMaQj8VfBdYkssbu0NKDDhjJ-GtiseaDVWt7dcH0cfwxgFUHpQh7FoCrjFJ6h6ZEpMF6xmujs4qMpPz8aaI4'";

    private const string PssPublicJwk =
        "kty: 'RSA', n: 'n4EPtAOCc9AlkeQHPzHStgAbgs7bTZLwUBZdR8_KuKPEHLd4rHVTeT-O-XV2jRojdNhxJWTDvNd7nqQ0VEiZQHz_AJmSCpMaJMRBSFKrKb2wqVwGU_NsYOYL-QtiWN2lbzcEe6XC0dApr5ydQLrHqkHHig3RBordaZ6Aj-oBHqFEHYpPe7Tpe-OfVfHd1E6cS6M1FZcD1NNLYD5lFHpPI9bTwJlsde3uhGqC0ZCuEHg8lhzwOHrtIQbS0FVbb9k3-tVTU4fg_3L_vniUFAKwuCLqKnS2BYwdq_mzSnbLY7h_qixoR7jig3__kRhuaxwUkRz5iaiQkqgc5gHdrNP5zw', e: 'AQAB'";

    private const string Ps384SigningInput =
        "eyJhbGciOiJQUzM4NCIsImtpZCI6ImJpbGJvLmJhZ2dpbnNAaG9iYml0b24uZXhhbXBsZSJ9.SXTigJlzIGEgZGFuZ2Vyb3VzIGJ1c2luZXNzLCBGcm9kbywgZ29pbmcgb3V0IHlvdXIgZG9vci4gWW91IHN0ZXAgb250byB0aGUgcm9hZCwgYW5kIGlmIHlvdSBkb24ndCBrZWVwIHlvdXIgZmVldCwgdGhlcmXigJlzIG5vIGtub3dpbmcgd2hlcmUgeW91IG1pZ2h0IGJlIHN3ZXB0IG9mZiB0by4";

    private const string Ps384SignatureHex =
        "72edb6781aa46032a02254e9cc35c6bda15fcfa586a33edf50371f4f49243b2e369a2021daac81ce4d7112c9e4d88a4debeb4f89de95ae494792ab06a83a8709d3fa3bc4d30797430c8b65955a3aff538b3e971b52b8625123316db5d4bddbd65f383e503f1b8a245e40595fcf6f3319656c76234051ff199f23c4815167e38d9974daa254946606aad84577bc3ff8a343ba7c55dcf44d3af151e4f7966f1df6c867af695f253f089948ef000d977b3d8c969f1c044e5ac03cd3d2a2349faba23f568237fdbf8cb01e41396a447b5f6bae1041252614002354a3db0728bbc61a34b9339c6c7e75d1ae8662625401f9968f067aa03e227caa3c0d833e5fbd846b";

    private const string OaepPrivateJwk =
        "kty: 'RSA', n: 'wbdxI55VaanZXPY29Lg5hdmv2XhvqAhoxUkanfzf2-5zVUxa6prHRrI4pP1AhoqJRlZfYtWWd5mmHRG2pAHIlh0ySJ9wi0BioZBl1XP2e-C-FyXJGcTy0HdKQWlrfhTm42EW7Vv04r4gfao6uxjLGwfpGrZLarohiWCPnkNrg71S2CuNZSQBIPGjXfkmIy2tl_VWgGnL22GplyXj5YlBLdxXp3XeStsqo571utNfoUTU8E4qdzJ3U1DItoVkPGsMwlmmnJiwA7sXRItBCivR4M5qnZtdw-7v4WuR4779ubDuJ5nalMv2S66-RPcnFAzWSKxtBDnFJJDGIUe7Tzizjg1nms0Xq_yPub_UOlWn0ec85FCft1hACpWG8schrOBeNqHBODFskYpUc2LC5JA2TaPF2dA67dg1TTsC_FupfQ2kNGcE1LgprxKHcVWYQb86B-HozjHZcqtauBzFNV5tbTuB-TpkcvJfNcFLlH3b8mb-H_ox35FjqBSAjLKyoeqfKTpVjvXhd09knwgJf6VKq6UC418_TOljMVfFTWXUxlnfhOOnzW6HSSzD1c9WrCuVzsUMv54szidQ9wf1cYWf3g5qFDxDQKis99gcDaiCAwM3yEBIzuNeeCa5dartHDb1xEB_HcHSeYbghbMjGfasvKn0aZRsnTyC0xhWBlsolZE', e: 'AQAB', d: 'n7fzJc3_WG59VEOBTkayzuSMM780OJQuZjN_KbH8lOZG25ZoA7T4Bxcc0xQn5oZE5uSCIwg91oCt0JvxPcpmqzaJZg1nirjcWZ-oBtVk7gCAWq-B3qhfF3izlbkosrzjHajIcY33HBhsy4_WerrXg4MDNE4HYojy68TcxT2LYQRxUOCf5TtJXvM8olexlSGtVnQnDRutxEUCwiewfmmrfveEogLx9EA-KMgAjTiISXxqIXQhWUQX1G7v_mV_Hr2YuImYcNcHkRvp9E7ook0876DhkO8v4UOZLwA1OlUX98mkoqwc58A_Y2lBYbVx1_s5lpPsEqbbH-nqIjh1fL0gdNfihLxnclWtW7pCztLnImZAyeCWAG7ZIfv-Rn9fLIv9jZ6r7r-MSH9sqbuziHN2grGjD_jfRluMHa0l84fFKl6bcqN1JWxPVhzNZo01yDF-1LiQnqUYSepPf6X3a2SOdkqBRiquE6EvLuSYIDpJq3jDIsgoL8Mo1LoomgiJxUwL_GWEOGu28gplyzm-9Q0U0nyhEf1uhSR8aJAQWAiFImWH5W_IQT9I7-yrindr_2fWQ_i1UgMsGzA7aOGzZfPljRy6z-tY_KuBG00-28S_aWvjyUc-Alp8AUyKjBZ-7CWH32fGWK48j1t-zomrwjL_mnhsPbGs0c9WsWgRzI-K8gE', p: '7_2v3OQZzlPFcHyYfLABQ3XP85Es4hCdwCkbDeltaUXgVy9l9etKghvM4hRkOvbb01kYVuLFmxIkCDtpi-zLCYAdXKrAK3PtSbtzld_XZ9nlsYa_QZWpXB_IrtFjVfdKUdMz94pHUhFGFj7nr6NNxfpiHSHWFE1zD_AC3mY46J961Y2LRnreVwAGNw53p07Db8yD_92pDa97vqcZOdgtybH9q6uma-RFNhO1AoiJhYZj69hjmMRXx-x56HO9cnXNbmzNSCFCKnQmn4GQLmRj9sfbZRqL94bbtE4_e0Zrpo8RNo8vxRLqQNwIy85fc6BRgBJomt8QdQvIgPgWCv5HoQ', q: 'zqOHk1P6WN_rHuM7ZF1cXH0x6RuOHq67WuHiSknqQeefGBA9PWs6ZyKQCO-O6mKXtcgE8_Q_hA2kMRcKOcvHil1hqMCNSXlflM7WPRPZu2qCDcqssd_uMbP-DqYthH_EzwL9KnYoH7JQFxxmcv5An8oXUtTwk4knKjkIYGRuUwfQTus0w1NfjFAyxOOiAQ37ussIcE6C6ZSsM3n41UlbJ7TCqewzVJaPJN5cxjySPZPD3Vp01a9YgAD6a3IIaKJdIxJS1ImnfPevSJQBE79-EXe2kSwVgOzvt-gsmM29QQ8veHy4uAqca5dZzMs7hkkHtw1z0jHV90epQJJlXXnH8Q', dp: '19oDkBh1AXelMIxQFm2zZTqUhAzCIr4xNIGEPNoDt1jK83_FJA-xnx5kA7-1erdHdms_Ef67HsONNv5A60JaR7w8LHnDiBGnjdaUmmuO8XAxQJ_ia5mxjxNjS6E2yD44USo2JmHvzeeNczq25elqbTPLhUpGo1IZuG72FZQ5gTjXoTXC2-xtCDEUZfaUNh4IeAipfLugbpe0JAFlFfrTDAMUFpC3iXjxqzbEanflwPvj6V9iDSgjj8SozSM0dLtxvu0LIeIQAeEgT_yXcrKGmpKdSO08kLBx8VUjkbv_3Pn20Gyu2YEuwpFlM_H1NikuxJNKFGmnAq9LcnwwT0jvoQ', dq: 'S6p59KrlmzGzaQYQM3o0XfHCGvfqHLYjCO557HYQf72O9kLMCfd_1VBEqeD-1jjwELKDjck8kOBl5UvohK1oDfSP1DleAy-cnmL29DqWmhgwM1ip0CCNmkmsmDSlqkUXDi6sAaZuntyukyflI-qSQ3C_BafPyFaKrt1fgdyEwYa08pESKwwWisy7KnmoUvaJ3SaHmohFS78TJ25cfc10wZ9hQNOrIChZlkiOdFCtxDqdmCqNacnhgE3bZQjGp3n83ODSz9zwJcSUvODlXBPc2AycH6Ci5yjbxt4Ppox_5pjm6xnQkiPgj01GpsUssMmBN7iHVsrE7N2iznBNCeOUIQ', qi: 'FZhClBMywVVjnuUud-05qd5CYU0dK79akAgy9oX6RX6I3IIIPckCciRrokxglZn-omAY5CnCe4KdrnjFOT5YUZE7G_Pg44XgCXaarLQf4hl80oPEf6-jJ5Iy6wPRx7G2e8qLxnh9cOdf-kRqgOS3F48Ucvw3ma5V6KGMwQqWFeV31XtZ8l5cVI-I3NzBS7qltpUVgz2Ju021eyc7IlqgzR98qKONl27DuEES0aK0WE97jnsyO27Yp88Wa2RiBrEocM89QZI1seJiGDizHRUP4UZxw9zsXww46wy0P6f9grnYp7t8LkyDDk8eoI4KX6SNMNVcyVS9IWjlq8EzqZEKIA'";

    private const string OaepPublicJwk =
        "kty: 'RSA', n: 'wbdxI55VaanZXPY29Lg5hdmv2XhvqAhoxUkanfzf2-5zVUxa6prHRrI4pP1AhoqJRlZfYtWWd5mmHRG2pAHIlh0ySJ9wi0BioZBl1XP2e-C-FyXJGcTy0HdKQWlrfhTm42EW7Vv04r4gfao6uxjLGwfpGrZLarohiWCPnkNrg71S2CuNZSQBIPGjXfkmIy2tl_VWgGnL22GplyXj5YlBLdxXp3XeStsqo571utNfoUTU8E4qdzJ3U1DItoVkPGsMwlmmnJiwA7sXRItBCivR4M5qnZtdw-7v4WuR4779ubDuJ5nalMv2S66-RPcnFAzWSKxtBDnFJJDGIUe7Tzizjg1nms0Xq_yPub_UOlWn0ec85FCft1hACpWG8schrOBeNqHBODFskYpUc2LC5JA2TaPF2dA67dg1TTsC_FupfQ2kNGcE1LgprxKHcVWYQb86B-HozjHZcqtauBzFNV5tbTuB-TpkcvJfNcFLlH3b8mb-H_ox35FjqBSAjLKyoeqfKTpVjvXhd09knwgJf6VKq6UC418_TOljMVfFTWXUxlnfhOOnzW6HSSzD1c9WrCuVzsUMv54szidQ9wf1cYWf3g5qFDxDQKis99gcDaiCAwM3yEBIzuNeeCa5dartHDb1xEB_HcHSeYbghbMjGfasvKn0aZRsnTyC0xhWBlsolZE', e: 'AQAB'";

    private const string OaepCiphertextHex =
        "ad3f7daf0ac14db4c8ec824cf1f5371258bbdb6e87101ec87210b136e87b9428ae778f0bc5ea2545db451789f34226de60e9794bf3c9b005d9c125ed0de3f3f6193e05bb6c4c1a82d94b0f39dc230bd36136ea4d36ef6e1c855fb43cae72f23a86f00b799e8e1a784d578bf5cf1da4eced43f3d980cdfc3efe44f7b64ba8ae90d4f82a4110d7c81d7d50e7cf8069ddc5cccba8dc59fd6ee10fdb25da90b5d86b5e4151742cba9338035183e3f1f805ded5deef61f06920f01a7129d515f5271f2504dc626e9e244073dda4b021a2b84f7f9dc16a6e04e8513905c1a2c2734b56ac9ee1ed54cb76a4a2087eec79042ea1b88be663b93357b5bb68852ba589bfdc7e3362fc8c7aae691aa1665b693cce93bcefc4d1959c7ba597cf6034976724b409f28a90ca334ffaa20b06534b3e1db989edf214b867db06b615e7f6e0ea77ac3270c6a09d07ce4ecfe5c9c4d6998f9aa965432a18d83517f4dcd8b046d6f9044fad39d1700804908f7bae0203c6aced805817b7f148d06f496f5e7d52a72bd867758573b58e2586936bb03c0d19fa302e93fb10e632ec42d06ed2dabb1cc39950414d3a32f6539453add14bdfb8a05ad3773103f9c804884b32363690e4f140c9a90cf4ebe826e8f1a29a3a7c7a012139271f69681a6736638e287df98ff425ad084359437a0361a0f2215ac89ebdc4311d1c0542191ccddf0a04236a330d1b";

    private const string OaepPlaintextHex =
        "99831fb208244c09b44dbbed945876872a179db1332508ccc6680b37777cc570";

    private const string Rs256PaddedP =
        "AADgHMQQ60imZV1URk0KpLttoLhyt3SmqdEf_kgHePDdtzEafpAu-cS191R2JiuoF2yzWXnwFDcqGigp5BNr0AHQfD8_Lk8l1Mk09jxxCfui5nYooNyac8YFjm3vItzVCVDnEd3BbVWG8qf6deqElMGAg-C2V0L4oNXnP7LZcPAZRw";

    private const string Rs256PaddedQi =
        "ACGHewxzoa1r8ZMD0LETNrToK423K377UCYqXfg5XMclbrjPbEC3YI1ZNqMtuhdBJqUnBi6tjKMF-34Xf0CUN8ncuXGO2CAYvO8PdyCixHX52ybaDjy1FtCE6yUXjoKNXKvUm7MWGsAYH6f4IegOetN5NvmUMFStCSkh7ixZLkN1";
}
#endif
