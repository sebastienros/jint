#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The key-derivation half of <c>crypto.subtle</c> — <c>deriveBits</c> and <c>deriveKey</c>
/// (https://w3c.github.io/webcrypto/#SubtleCrypto-method-deriveBits and
/// https://w3c.github.io/webcrypto/#SubtleCrypto-method-deriveKey) over the three algorithms registered for
/// them: HKDF (https://w3c.github.io/webcrypto/#hkdf), PBKDF2 (https://w3c.github.io/webcrypto/#pbkdf2) and
/// ECDH (https://w3c.github.io/webcrypto/#ecdh-operations-derive-bits).
/// </summary>
/// <remarks>
/// <para>
/// Every derivation is checked against a vector published by somebody else, which is the only way to check
/// cryptography: RFC 6070 for PBKDF2-HMAC-SHA-1, RFC 7914 §11 for PBKDF2-HMAC-SHA-256, RFC 5869 Appendix A
/// for HKDF over both SHA-256 and SHA-1, and RFC 5903 §8.1 for an ECDH agreement on P-256 — that last one
/// supplying both private keys, both public points <i>and</i> the shared secret, so both directions of the
/// agreement have a known answer rather than merely agreeing with each other.
/// </para>
/// <para>
/// The derived-key round trips (PBKDF2 to AES-GCM, ECDH to HMAC) go one step further and pin the exact key
/// bytes, computed outside this engine from the BCL's own one-shot primitives. A round trip on its own would
/// pass just as well against a derivation that is internally consistent and wrong.
/// </para>
/// <para>
/// The <c>length</c> argument's whole matrix — null, omitted, zero, not a multiple of eight, past the
/// maximum, and the <c>[EnforceRange]</c> refusals — is pinned per algorithm, because the three answer it
/// differently and the differences are normative rather than incidental.
/// </para>
/// </remarks>
public class SubtleCryptoDeriveTests
{
    /// <summary>
    /// The same helpers <see cref="SubtleCryptoEcTests"/> uses, plus <c>seq</c> for RFC 5869's long
    /// vectors, whose inputs the RFC itself describes as runs of consecutive byte values.
    /// </summary>
    private const string Prelude = """
        const hex = b => Array.from(new Uint8Array(b)).map(x => x.toString(16).padStart(2, '0')).join('');
        const bytes = h => Uint8Array.from(h.match(/../g) || [], x => parseInt(x, 16));
        const ascii = s => Uint8Array.from(s, c => c.charCodeAt(0));
        const seq = (start, count) => Uint8Array.from({ length: count }, (_, i) => start + i);
        const empty = new Uint8Array(0);
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
    // The RFC 5903 §8.1 ECDH vector, which every ECDH test below is built from
    // ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// "i" — the initiator's private key, base64url-encoded as JSON Web Algorithms §6.2.2.1 requires.
    /// </summary>
    private const string AlicePrivateJwk = """
        kty: 'EC', crv: 'P-256',
        d: 'yI8B9RDZrD9wopLaojFt5UTpqriv6EBJxiqcV4YtFDM',
        x: '2tC2U5QiHPmwUeH-yleH0Jjf5jf8kLnvlF0MN3JYEYA',
        y: 'UnGgRhzbglLWHxxFb6PlmrH0WzOsz19YOJ4Fd7iZC7M'
        """;

    /// <summary>"r" — the responder's private key.</summary>
    private const string BobPrivateJwk = """
        kty: 'EC', crv: 'P-256',
        d: 'xu-cXXiuASoBEWSss5fOIIhoXY8Gv5vgsoOrRkdr7lM',
        x: '0S37UonI1PgSCLcCcDmMNCKWlwoLzLdMc2_HVUSUv2M',
        y: 'VvvzyjZswj6BV4VME8WNaqwj8Eatow-DU-dPMwOYcqs'
        """;

    /// <summary>"gi" — the initiator's public point, as the uncompressed <c>04 || X || Y</c> of [SEC1] 2.3.3.</summary>
    private const string AlicePublicRawHex =
        "04dad0b65394221cf9b051e1feca5787d098dfe637fc90b9ef945d0c3772581180"
        + "5271a0461cdb8252d61f1c456fa3e59ab1f45b33accf5f58389e0577b8990bb3";

    /// <summary>"gr" — the responder's public point.</summary>
    private const string BobPublicRawHex =
        "04d12dfb5289c8d4f81208b70270398c342296970a0bccb74c736fc7554494bf63"
        + "56fbf3ca366cc23e8157854c13c58d6aac23f046ada30f8353e74f33039872ab";

    /// <summary>"girx" — the x-coordinate of the shared point, which is the whole of the secret.</summary>
    private const string SharedSecretHex = "d6840f6b42f6edafd13116e0e12565202fef8e9ece7dce03812464d04b9442de";

    /// <summary>
    /// The four keys of the RFC 5903 vector, as a prelude every ECDH test starts from. The public halves are
    /// imported from <c>raw</c> points and the private halves from JSON Web Keys, so the vector's own hex
    /// reaches the engine by two different doors.
    /// </summary>
    private const string EcdhKeys = $$"""
        const P256 = { name: 'ECDH', namedCurve: 'P-256' };
        const alicePriv = await crypto.subtle.importKey('jwk', { {{AlicePrivateJwk}} }, P256, false, ['deriveBits', 'deriveKey']);
        const bobPriv = await crypto.subtle.importKey('jwk', { {{BobPrivateJwk}} }, P256, false, ['deriveBits', 'deriveKey']);
        const alicePub = await crypto.subtle.importKey('raw', bytes('{{AlicePublicRawHex}}'), P256, false, []);
        const bobPub = await crypto.subtle.importKey('raw', bytes('{{BobPublicRawHex}}'), P256, false, []);
        """;

    // ---------------------------------------------------------------------------------------------------
    // PBKDF2 — RFC 6070 and RFC 7914 §11
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    // https://www.rfc-editor.org/rfc/rfc6070#section-2, the PBKDF2-HMAC-SHA-1 test vectors.
    [InlineData("password", "salt", 1, 160, "SHA-1", "0c60c80f961f0e71f3a9b524af6012062fe037a6")]
    [InlineData("password", "salt", 2, 160, "SHA-1", "ea6c014dc72d6f8ccd1ed92ace1d41f0d8de8957")]
    [InlineData("password", "salt", 4096, 160, "SHA-1", "4b007901b765489abead49d926f721d065a429c1")]
    [InlineData(
        "passwordPASSWORDpassword", "saltSALTsaltSALTsaltSALTsaltSALTsalt", 4096, 200, "SHA-1",
        "3d2eec4fe41c849b80c8d83662c0e44a8b291a964cf2f07038")]
    // https://www.rfc-editor.org/rfc/rfc7914#section-11, whose first two vectors are PBKDF2-HMAC-SHA-256.
    [InlineData("passwd", "salt", 1, 512, "SHA-256",
        "55ac046e56e3089fec1691c22544b605f94185216dde0465e68b9d57c20dacbc"
        + "49ca9cccf179b645991664b39d77ef317c71b845b1e30bd509112041d3a19783")]
    [InlineData("Password", "NaCl", 80000, 512, "SHA-256",
        "4ddcd8f60b98be21830cee5ef22701f9641a4418d04c0414aeff08876b34ab56"
        + "a1d425a1225833549adb841b51c9b3176a272bdebba1d078478f62b397f33c8d")]
    public void DerivesThePublishedPbkdf2Vectors(
        string password,
        string salt,
        int iterations,
        int length,
        string hash,
        string expected)
    {
        Run($$"""
            const key = await crypto.subtle.importKey('raw', ascii('{{password}}'), 'PBKDF2', false, ['deriveBits']);
            const params = { name: 'PBKDF2', salt: ascii('{{salt}}'), iterations: {{iterations}}, hash: '{{hash}}' };
            return hex(await crypto.subtle.deriveBits(params, key, {{length}}));
            """).AsString().Should().Be(expected);
    }

    [Fact]
    public void DerivesTheRfc6070VectorWhoseInputsContainANullByte()
    {
        // The sixth vector of https://www.rfc-editor.org/rfc/rfc6070#section-2 is P = "pass\0word" and
        // S = "sa\0lt". It is here because a NUL inside the password is exactly what a length-prefixed byte
        // sequence handles and a C string does not, and the key material reaches PBKDF2 as the former.
        Run("""
            const key = await crypto.subtle.importKey('raw', bytes('7061737300776f7264'), 'PBKDF2', false, ['deriveBits']);
            const params = { name: 'PBKDF2', salt: bytes('7361006c74'), iterations: 4096, hash: 'SHA-1' };
            return hex(await crypto.subtle.deriveBits(params, key, 128));
            """).AsString().Should().Be("56fa6aa75548099dcc37d7f03425e0c3");
    }

    [Fact]
    public void RefusesAnIterationCountOfZeroWithAnOperationError()
    {
        // "If the iterations member of normalizedAlgorithm is zero, then throw an OperationError" — step 2.
        //
        // The second row is what makes this a pin on the *step* rather than on the platform: step 2 runs
        // before step 3's "if length is zero, return an empty byte sequence", so a zero-length request with
        // a zero iteration count is still the error. Without the check that request short-circuits to the
        // empty array and never reaches the platform, whose own refusal — an ArgumentOutOfRangeException
        // this code turns into an OperationError — would otherwise cover for the missing step on the first
        // row alone.
        Run("""
            const key = await crypto.subtle.importKey('raw', ascii('password'), 'PBKDF2', false, ['deriveBits']);

            const attempt = async length => {
                try {
                    const bits = await crypto.subtle.deriveBits(
                        { name: 'PBKDF2', salt: ascii('salt'), iterations: 0, hash: 'SHA-1' }, key, length);
                    return 'derived:' + bits.byteLength;
                } catch (e) {
                    return e.name + '/' + (e instanceof DOMException);
                }
            };

            return (await attempt(160)) + ',' + (await attempt(0));
            """).AsString().Should().Be("OperationError/true,OperationError/true");
    }

    [Fact]
    public void RefusesAnIterationCountAboveThisEnginesCeiling()
    {
        // PBKDF2 is a deliberately slow loop whose trip count comes from script and which happens inside one
        // BCL call, so no execution constraint can interrupt it. The ceiling is this engine's, the algorithm
        // has none, and the message names the restriction rather than pretending the request was malformed.
        var message = Run("""
            const key = await crypto.subtle.importKey('raw', ascii('password'), 'PBKDF2', false, ['deriveBits']);
            try {
                await crypto.subtle.deriveBits({ name: 'PBKDF2', salt: ascii('salt'), iterations: 4194305, hash: 'SHA-1' }, key, 160);
                return 'derived';
            } catch (e) {
                return e.name + ': ' + e.message;
            }
            """).AsString();

        message.Should().StartWith("OperationError: ");
        message.Should().Contain("4194305");
        message.Should().Contain("4194304");

        // One below the ceiling still runs, so the bound is the number it says and not a smaller one it
        // happens to enforce.
        Run("""
            const key = await crypto.subtle.importKey('raw', ascii('password'), 'PBKDF2', false, ['deriveBits']);
            const out = await crypto.subtle.deriveBits({ name: 'PBKDF2', salt: ascii('salt'), iterations: 4194304, hash: 'SHA-256' }, key, 8);
            return out.byteLength;
            """).AsNumber().Should().Be(1);
    }

    [Fact]
    public void APbkdf2KeyIsImportOnlyAndNeverExtractable()
    {
        var engine = WebEngine();

        // "If extractable is not false, then throw a SyntaxError." The [[handle]] of a PBKDF2 key is the
        // password itself, so an extractable one would be a way to read a password back out of a key.
        Settle(engine, """
            crypto.subtle.importKey('raw', new Uint8Array(8), 'PBKDF2', true, ['deriveBits'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("SyntaxError");

        // "If format is not 'raw', throw a NotSupportedError" — all three of the others.
        Settle(engine, """
            Promise.all(['jwk', 'spki', 'pkcs8'].map(format =>
                crypto.subtle.importKey(format, format === 'jwk' ? { kty: 'oct', k: 'AAAA' } : new Uint8Array(8), 'PBKDF2', false, ['deriveBits'])
                    .then(() => 'imported', e => e.name))).then(names => names.join(','))
            """).AsString().Should().Be("NotSupportedError,NotSupportedError,NotSupportedError");

        // Neither generateKey nor exportKey is registered for PBKDF2 at all, which is the registry's answer
        // and not the algorithm's — so both are a NotSupportedError before any step runs.
        Settle(engine, """
            crypto.subtle.generateKey({ name: 'PBKDF2' }, false, ['deriveBits']).then(() => 'generated', e => e.name)
            """).AsString().Should().Be("NotSupportedError");

        Settle(engine, """
            crypto.subtle.importKey('raw', new Uint8Array(8), 'PBKDF2', false, ['deriveBits'])
                .then(key => crypto.subtle.exportKey('raw', key))
                .then(() => 'exported', e => e.name)
            """).AsString().Should().Be("NotSupportedError");
    }

    // ---------------------------------------------------------------------------------------------------
    // HKDF — RFC 5869 Appendix A
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void DerivesTheRfc5869Sha256Vectors()
    {
        // https://www.rfc-editor.org/rfc/rfc5869#appendix-A — A.1 (basic), A.2 (long inputs and a
        // multi-block expansion, which is the one that exercises the T(1)..T(N) counter) and A.3 (zero-length
        // salt and info, which the RFC writes as "not provided" and which this API can only spell as the
        // empty byte sequence, `salt` and `info` being required members).
        Run("""
            const derive = async (ikm, salt, info, bits) => {
                const key = await crypto.subtle.importKey('raw', ikm, 'HKDF', false, ['deriveBits']);
                return hex(await crypto.subtle.deriveBits({ name: 'HKDF', hash: 'SHA-256', salt, info }, key, bits));
            };

            return [
                await derive(bytes('0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b'), bytes('000102030405060708090a0b0c'), bytes('f0f1f2f3f4f5f6f7f8f9'), 42 * 8),
                await derive(seq(0x00, 80), seq(0x60, 80), seq(0xb0, 80), 82 * 8),
                await derive(bytes('0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b'), empty, empty, 42 * 8),
            ].join('|');
            """).AsString().Should().Be(
                "3cb25f25faacd57a90434f64d0362f2a2d2d0a90cf1a5a4c5db02d56ecc4c5bf34007208d5b887185865"
                + "|b11e398dc80327a1c8e7f78c596a49344f012eda2d4efad8a050cc4c19afa97c59045a99cac7827271cb41c65e590e09da3275600c2f09b8367793a9aca3db71cc30c58179ec3e87c14c01d5c1f3434f1d87"
                + "|8da4e775a563c18f715f802a063c5a31b8a11f5c5ee1879ec3454e5f3c738d2d9d201395faa4b61a96c8");
    }

    [Fact]
    public void DerivesTheRfc5869Sha1Vectors()
    {
        // A.4 and A.6 of the same appendix, which are the SHA-1 half — HKDF's security rests on HMAC, so
        // SHA-1 stays a registered hash here as it does in every browser.
        Run("""
            const derive = async (ikm, salt, info, bits) => {
                const key = await crypto.subtle.importKey('raw', ikm, 'HKDF', false, ['deriveBits']);
                return hex(await crypto.subtle.deriveBits({ name: 'HKDF', hash: 'SHA-1', salt, info }, key, bits));
            };

            return [
                await derive(bytes('0b0b0b0b0b0b0b0b0b0b0b'), bytes('000102030405060708090a0b0c'), bytes('f0f1f2f3f4f5f6f7f8f9'), 42 * 8),
                await derive(bytes('0b0b0b0b0b0b0b0b0b0b0b'), empty, empty, 42 * 8),
            ].join('|');
            """).AsString().Should().Be(
                "085a01ea1b10f36933068b56efa5ad81a4f14b822f5b091568a9cdd4f155fda2c22e422478d305f3f896"
                + "|14101530f62ccf2b30cc6d220554d8d96802825489c52c84c99342b96e018c221c71a88a4a258f71ffea");
    }

    [Theory]
    [InlineData("SHA-1", 160)]
    [InlineData("SHA-256", 256)]
    [InlineData("SHA-384", 384)]
    [InlineData("SHA-512", 512)]
    public void RefusesAnHkdfLengthPastTheExpansionCeiling(string hash, int hashLength)
    {
        // "If length is greater than 255 * hashLength, then throw an OperationError" — RFC 5869's own bound,
        // the expansion counter T(1)..T(N) being a single octet. The check is this engine's rather than the
        // platform's, because .NET reports an over-long request as an ArgumentOutOfRangeException, which is
        // a CLR exception and must never escape a promise-returning operation.
        var maximum = 255 * hashLength;

        var result = Run($$"""
            const key = await crypto.subtle.importKey('raw', new Uint8Array(22), 'HKDF', false, ['deriveBits']);
            const params = { name: 'HKDF', hash: '{{hash}}', salt: empty, info: empty };

            const atCeiling = (await crypto.subtle.deriveBits(params, key, {{maximum}})).byteLength;

            try {
                await crypto.subtle.deriveBits(params, key, {{maximum}} + 8);
                return atCeiling + '|derived';
            } catch (e) {
                return atCeiling + '|' + e.name + '/' + (e instanceof DOMException) + '/' + e.message;
            }
            """).AsString();

        // The message is part of the pin, not decoration: the platform refuses an over-long output too, with
        // an ArgumentOutOfRangeException that this code turns into an OperationError of its own — so a test
        // asserting only the error's *name* cannot tell the specification's step 3 from the platform's
        // accident, and would go green if the step were deleted. Naming the ceiling is what distinguishes
        // them, and the ceiling is what the step is.
        result.Should().StartWith((maximum / 8) + "|OperationError/true/");
        result.Should().Contain($"a length of {maximum + 8} bits exceeds the {maximum} bits HKDF can expand to with {hash} (255 * {hashLength})");
    }

    [Fact]
    public void AnHkdfKeyIsImportOnlyAndNeverExtractable()
    {
        var engine = WebEngine();

        Settle(engine, """
            crypto.subtle.importKey('raw', new Uint8Array(8), 'HKDF', true, ['deriveBits'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("SyntaxError");

        Settle(engine, """
            Promise.all(['jwk', 'spki', 'pkcs8'].map(format =>
                crypto.subtle.importKey(format, format === 'jwk' ? { kty: 'oct', k: 'AAAA' } : new Uint8Array(8), 'HKDF', false, ['deriveBits'])
                    .then(() => 'imported', e => e.name))).then(names => names.join(','))
            """).AsString().Should().Be("NotSupportedError,NotSupportedError,NotSupportedError");

        Settle(engine, """
            crypto.subtle.generateKey({ name: 'HKDF' }, false, ['deriveBits']).then(() => 'generated', e => e.name)
            """).AsString().Should().Be("NotSupportedError");

        Settle(engine, """
            crypto.subtle.importKey('raw', new Uint8Array(8), 'HKDF', false, ['deriveBits'])
                .then(key => crypto.subtle.exportKey('raw', key))
                .then(() => 'exported', e => e.name)
            """).AsString().Should().Be("NotSupportedError");
    }

    [Theory]
    [InlineData("HKDF")]
    [InlineData("PBKDF2")]
    public void ADerivationKeyCarriesTheBareKeyAlgorithmAndOnlyTheDerivationUsages(string algorithm)
    {
        // "Let algorithm be a new KeyAlgorithm object. Set the name attribute of algorithm to 'HKDF'" — and
        // nothing else. There is no hash and no length on the key, because for both of these the hash, the
        // salt and the iteration count all belong to each derivation instead.
        Run($$"""
            const key = await crypto.subtle.importKey('raw', new Uint8Array(8), '{{algorithm}}', false, ['deriveKey', 'deriveBits']);
            return JSON.stringify(key.algorithm) + '|' + key.type + '|' + key.extractable + '|' + JSON.stringify(key.usages);
            """).AsString().Should().Be($$"""{"name":"{{algorithm}}"}|secret|false|["deriveKey","deriveBits"]""");

        var engine = WebEngine();

        // "If usages contains a value that is not 'deriveKey' or 'deriveBits', then throw a SyntaxError."
        Settle(engine, $$"""
            crypto.subtle.importKey('raw', new Uint8Array(8), '{{algorithm}}', false, ['sign'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("SyntaxError");

        // The empty list is within the permitted set, so it survives the algorithm's own step — and is then
        // refused by importKey's shared tail, "if the [[type]] internal slot of result is 'secret' … and
        // usages is empty, then throw a SyntaxError". A key of these two algorithms is always secret, so
        // there is no usable key with no usages.
        Settle(engine, $$"""
            crypto.subtle.importKey('raw', new Uint8Array(8), '{{algorithm}}', false, [])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("SyntaxError");
    }

    // ---------------------------------------------------------------------------------------------------
    // ECDH
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void DerivesTheRfc5903P256SharedSecretInBothDirections()
    {
        // https://www.rfc-editor.org/rfc/rfc5903#section-8.1 supplies both private keys, both public points
        // and the x-coordinate of the shared point, so this is a known answer and not merely a pair of
        // results that agree.
        Run($$"""
            {{EcdhKeys}}

            return [
                hex(await crypto.subtle.deriveBits({ name: 'ECDH', public: bobPub }, alicePriv, null)),
                hex(await crypto.subtle.deriveBits({ name: 'ECDH', public: alicePub }, bobPriv, null)),
            ].join('|');
            """).AsString().Should().Be(SharedSecretHex + "|" + SharedSecretHex);
    }

    [Fact]
    public void ANullLengthIsTheWholeSharedSecretAndAnOmittedArgumentMeansNull()
    {
        // "If length is null: return secret." The argument is `optional … unsigned long? length = null`, so
        // omitting it, passing undefined and passing null are three spellings of the same request — which is
        // what makes deriveBits usable for ECDH without knowing a curve's field width.
        Run($$"""
            {{EcdhKeys}}
            const params = { name: 'ECDH', public: bobPub };

            return [
                hex(await crypto.subtle.deriveBits(params, alicePriv)),
                hex(await crypto.subtle.deriveBits(params, alicePriv, undefined)),
                hex(await crypto.subtle.deriveBits(params, alicePriv, null)),
            ].join('|');
            """).AsString().Should().Be(string.Join("|", SharedSecretHex, SharedSecretHex, SharedSecretHex));
    }

    [Fact]
    public void TruncatesToTheFirstLengthBitsIncludingAPartialByte()
    {
        // "Return a byte sequence containing the first length bits of secret" — a bit count, not a byte
        // count, and ECDH's steps impose none of the multiple-of-eight restriction HKDF's and PBKDF2's do.
        // 230 bits is 28 whole bytes plus 6 bits, so the answer is 29 bytes whose last one keeps its top six
        // bits and has the other two cleared: 0x4b becomes 0x48.
        Run($$"""
            {{EcdhKeys}}
            const params = { name: 'ECDH', public: bobPub };

            return [
                hex(await crypto.subtle.deriveBits(params, alicePriv, 128)),
                hex(await crypto.subtle.deriveBits(params, alicePriv, 230)),
                (await crypto.subtle.deriveBits(params, alicePriv, 0)).byteLength,
                hex(await crypto.subtle.deriveBits(params, alicePriv, 256)),
            ].join('|');
            """).AsString().Should().Be(
                SharedSecretHex.Substring(0, 32)
                + "|" + SharedSecretHex.Substring(0, 56) + "48"
                + "|0"
                + "|" + SharedSecretHex);
    }

    [Theory]
    [InlineData("P-256", 256)]
    [InlineData("P-384", 384)]
    [InlineData("P-521", 528)]
    public void TheMaximumLengthIsTheCurvesFieldWidthRoundedUpToWholeOctets(string curve, int maximum)
    {
        // "Let maximumLength be the length in bits of the output of the field element to octet string
        // conversion … If length is not null and is greater than maximumLength, then throw an
        // OperationError." The conversion pads to whole octets, so P-521's maximum is 528 and not 521 — the
        // one row of this table that cannot be read off the curve's name. Both keys are the same pair here,
        // so the ceiling is the only thing that can refuse the request; where the two curves differ it is not
        // reached at all, which is AMismatchedCurveIsAnInvalidAccessErrorWhateverTheLengthAsksFor's subject.
        Run($$"""
            const pair = await crypto.subtle.generateKey({ name: 'ECDH', namedCurve: '{{curve}}' }, false, ['deriveBits']);
            const params = { name: 'ECDH', public: pair.publicKey };

            const atMaximum = (await crypto.subtle.deriveBits(params, pair.privateKey, {{maximum}})).byteLength;
            const withoutLength = (await crypto.subtle.deriveBits(params, pair.privateKey)).byteLength;

            try {
                await crypto.subtle.deriveBits(params, pair.privateKey, {{maximum}} + 8);
                return atMaximum + '|' + withoutLength + '|derived';
            } catch (e) {
                return atMaximum + '|' + withoutLength + '|' + e.name;
            }
            """).AsString().Should().Be($"{maximum / 8}|{maximum / 8}|OperationError");
    }

    [Fact]
    public void RefusesEveryWrongKeyRoleWithAnInvalidAccessError()
    {
        // The five checks the ECDH derive-bits steps make, in the order they make them: the `public` member
        // must be a public key (step 2) of an ECDH key (step 3), the base key must be a private one
        // (step 6), and the two must share a curve (step 8).
        Run($$"""
            {{EcdhKeys}}
            const ecdsaPair = await crypto.subtle.generateKey({ name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign', 'verify']);
            const p384 = await crypto.subtle.generateKey({ name: 'ECDH', namedCurve: 'P-384' }, false, ['deriveBits']);

            const attempt = async (params, key) => {
                try { await crypto.subtle.deriveBits(params, key, 128); return 'derived'; }
                catch (e) { return e.name; }
            };

            return [
                // The public member is a private key.
                await attempt({ name: 'ECDH', public: bobPriv }, alicePriv),
                // ... is an ECDSA key, over the very same curve.
                await attempt({ name: 'ECDH', public: ecdsaPair.publicKey }, alicePriv),
                // The base key is a public key.
                await attempt({ name: 'ECDH', public: bobPub }, bobPub),
                // The two keys are on different curves.
                await attempt({ name: 'ECDH', public: p384.publicKey }, alicePriv),
                // The base key is not an ECDH key at all, which deriveBits itself catches before the
                // algorithm's steps run.
                await attempt({ name: 'ECDH', public: bobPub }, ecdsaPair.privateKey),
            ].join(',');
            """).AsString().Should().Be("InvalidAccessError,InvalidAccessError,InvalidAccessError,InvalidAccessError,InvalidAccessError");
    }

    [Fact]
    public void AMismatchedCurveIsAnInvalidAccessErrorWhateverTheLengthAsksFor()
    {
        // The one place this engine deliberately departs from the ECDH derive-bits prose, and the reason is
        // in EcAlgorithm.DeriveBits's remarks: steps 4 and 5 measure *maximumLength* off the **public** key's
        // domain parameters and refuse an over-long `length` with an OperationError before steps 7 and 8 have
        // established that the two keys are a pair at all. Read literally, a P-384 or P-521 base key handed a
        // P-256 public key and asked for its own field width is therefore refused for its length. Chrome,
        // Firefox, Safari and Node all answer the InvalidAccessError of the later step, and the corpus pins
        // that in `WebCryptoAPI/derive_bits_keys/ecdh_bits.https.any.js`'s `P-384 mismatched curves` and
        // `P-521 mismatched curves` rows — which ask for exactly 8 x the base key's field width. Jint follows
        // the browsers, so the first two rows below are the ones that move: put steps 4 and 5 back above the
        // key-agreement checks and they read OperationError again.
        Run("""
            const p256 = await crypto.subtle.generateKey({ name: 'ECDH', namedCurve: 'P-256' }, false, ['deriveBits']);
            const p384 = await crypto.subtle.generateKey({ name: 'ECDH', namedCurve: 'P-384' }, false, ['deriveBits']);
            const p521 = await crypto.subtle.generateKey({ name: 'ECDH', namedCurve: 'P-521' }, false, ['deriveBits']);

            const attempt = async (pub, priv, length) => {
                try { await crypto.subtle.deriveBits({ name: 'ECDH', public: pub }, priv, length); return 'derived'; }
                catch (e) { return e.name; }
            };

            return [
                // The corpus's own two rows: 8 x the *base* key's field width, against a narrower public key.
                await attempt(p256.publicKey, p384.privateKey, 384),
                await attempt(p256.publicKey, p521.privateKey, 528),
                // The same two mismatches at a length no ceiling could object to, and at no length at all.
                await attempt(p256.publicKey, p384.privateKey, 128),
                await attempt(p256.publicKey, p521.privateKey, null),
                // The corpus's P-256 row, which was green under either order because the curve it is
                // mismatched against is the wider one — so on its own it proves nothing about the ordering.
                await attempt(p384.publicKey, p256.privateKey, 256),
                // Matched curves and an over-long length is still step 5's OperationError: the move reorders
                // the ceiling, it does not remove it.
                await attempt(p256.publicKey, p256.privateKey, 264),
                await attempt(p521.publicKey, p521.privateKey, 536),
            ].join(',');
            """).AsString().Should().Be(
                "InvalidAccessError,InvalidAccessError,InvalidAccessError,InvalidAccessError,InvalidAccessError"
                + ",OperationError,OperationError");
    }

    [Fact]
    public void ThePublicMemberIsACryptoKeyInterfaceAndCoercesNothing()
    {
        // `required CryptoKey public` is an interface type, so WebIDL accepts a platform object of that
        // interface and raises a TypeError for everything else — including an object shaped like a key, and
        // including a real key's own `algorithm` dictionary, which is the mistake this is most likely to
        // catch.
        Run($$"""
            {{EcdhKeys}}

            const attempt = async member => {
                try { await crypto.subtle.deriveBits({ name: 'ECDH', public: member }, alicePriv, 128); return 'derived'; }
                catch (e) { return e.constructor.name; }
            };

            return [
                await attempt(undefined),
                await attempt(null),
                await attempt({ type: 'public', algorithm: { name: 'ECDH', namedCurve: 'P-256' } }),
                await attempt(bobPub.algorithm),
            ].join(',');
            """).AsString().Should().Be("TypeError,TypeError,TypeError,TypeError");
    }

    // ---------------------------------------------------------------------------------------------------
    // The length argument, across all three algorithms
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void HkdfAndPbkdf2RefuseANullLengthAndANonMultipleOfEight()
    {
        // "If length is null or is not a multiple of 8, then throw an OperationError" — the first step of
        // both, and the whole of the difference from ECDH, which has a natural output size to fall back on
        // and truncates to a bit.
        Run("""
            const hkdf = await crypto.subtle.importKey('raw', new Uint8Array(22), 'HKDF', false, ['deriveBits']);
            const pbkdf2 = await crypto.subtle.importKey('raw', ascii('password'), 'PBKDF2', false, ['deriveBits']);
            const hkdfParams = { name: 'HKDF', hash: 'SHA-256', salt: empty, info: empty };
            const pbkdf2Params = { name: 'PBKDF2', salt: ascii('salt'), iterations: 1, hash: 'SHA-256' };

            const attempt = async (params, key, ...length) => {
                try { await crypto.subtle.deriveBits(params, key, ...length); return 'derived'; }
                catch (e) { return e.name; }
            };

            return [
                await attempt(hkdfParams, hkdf),
                await attempt(hkdfParams, hkdf, undefined),
                await attempt(hkdfParams, hkdf, null),
                await attempt(hkdfParams, hkdf, 230),
                await attempt(pbkdf2Params, pbkdf2),
                await attempt(pbkdf2Params, pbkdf2, null),
                await attempt(pbkdf2Params, pbkdf2, 230),
            ].join(',');
            """).AsString().Should().Be(
                "OperationError,OperationError,OperationError,OperationError,OperationError,OperationError,OperationError");
    }

    [Fact]
    public void AZeroLengthIsTheEmptyByteSequenceForAllThree()
    {
        // PBKDF2's steps say so outright ("If length is zero, return an empty byte sequence"); HKDF's do not
        // mention it, but HKDF-Expand with L = 0 runs no iterations and yields the empty string, so it is
        // the same answer — and it has to be produced without asking the platform, which refuses an output
        // length of zero with an ArgumentOutOfRangeException.
        Run($$"""
            {{EcdhKeys}}
            const hkdf = await crypto.subtle.importKey('raw', new Uint8Array(22), 'HKDF', false, ['deriveBits']);
            const pbkdf2 = await crypto.subtle.importKey('raw', ascii('password'), 'PBKDF2', false, ['deriveBits']);

            return [
                (await crypto.subtle.deriveBits({ name: 'HKDF', hash: 'SHA-256', salt: empty, info: empty }, hkdf, 0)).byteLength,
                (await crypto.subtle.deriveBits({ name: 'PBKDF2', salt: ascii('salt'), iterations: 1, hash: 'SHA-256' }, pbkdf2, 0)).byteLength,
                (await crypto.subtle.deriveBits({ name: 'ECDH', public: bobPub }, alicePriv, 0)).byteLength,
            ].join(',');
            """).AsString().Should().Be("0,0,0");
    }

    [Fact]
    public void TheLengthArgumentIsEnforceRange()
    {
        // `[EnforceRange] unsigned long?`, so a value that is not a finite number or whose truncated value
        // falls outside [0, 2^32 - 1] is a TypeError raised by the argument conversion — before a single
        // step of the method body, and therefore before the algorithm is even normalized.
        Run($$"""
            {{EcdhKeys}}
            const params = { name: 'ECDH', public: bobPub };

            const attempt = async length => {
                try { await crypto.subtle.deriveBits(params, alicePriv, length); return 'derived'; }
                catch (e) { return e.constructor.name; }
            };

            return [
                await attempt(NaN),
                await attempt(Infinity),
                await attempt(-8),
                await attempt(2 ** 32 + 8),
                // A fractional value truncates rather than failing, which is what ConvertToInt does before
                // it checks the range.
                hex(await crypto.subtle.deriveBits(params, alicePriv, 128.9)),
            ].join(',');
            """).AsString().Should().Be(
                "TypeError,TypeError,TypeError,TypeError," + SharedSecretHex.Substring(0, 32));
    }

    // ---------------------------------------------------------------------------------------------------
    // deriveKey
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void DerivesAnAesGcmKeyFromAPassword()
    {
        // The shape an embedder actually reaches for. The expected key is PBKDF2-HMAC-SHA-256 over
        // ("password", "salt", 1000) truncated to 32 bytes, computed outside this engine — so the round trip
        // below is evidence rather than a tautology.
        Run("""
            const password = await crypto.subtle.importKey('raw', ascii('password'), 'PBKDF2', false, ['deriveKey']);

            const key = await crypto.subtle.deriveKey(
                { name: 'PBKDF2', salt: ascii('salt'), iterations: 1000, hash: 'SHA-256' },
                password,
                { name: 'AES-GCM', length: 256 },
                true,
                ['encrypt', 'decrypt']);

            const iv = new Uint8Array(12);
            const sealed = await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, key, ascii('attack at dawn'));
            const opened = await crypto.subtle.decrypt({ name: 'AES-GCM', iv }, key, sealed);

            return [
                key.type,
                JSON.stringify(key.algorithm),
                key.extractable,
                JSON.stringify(key.usages),
                hex(await crypto.subtle.exportKey('raw', key)),
                String.fromCharCode(...new Uint8Array(opened)),
            ].join('|');
            """).AsString().Should().Be(
                "secret"
                + """|{"name":"AES-GCM","length":256}"""
                + "|true"
                + """|["encrypt","decrypt"]"""
                + "|632c2812e46d4604102ba7618e9d6d7d2f8128f6266b4a03264d2a0460b7dcb3"
                + "|attack at dawn");
    }

    [Fact]
    public void DerivesAnHmacKeyFromAnEcdhAgreement()
    {
        // The derived key is the first 256 bits of the RFC 5903 shared secret, and the MAC is
        // HMAC-SHA-256 over "x" under exactly those bytes — both computed outside this engine.
        Run($$"""
            {{EcdhKeys}}

            const mac = await crypto.subtle.deriveKey(
                { name: 'ECDH', public: bobPub },
                alicePriv,
                { name: 'HMAC', hash: 'SHA-256', length: 256 },
                false,
                ['sign', 'verify']);

            const signature = await crypto.subtle.sign('HMAC', mac, ascii('x'));

            return [
                mac.type,
                JSON.stringify(mac.algorithm),
                mac.extractable,
                hex(signature),
                await crypto.subtle.verify('HMAC', mac, signature, ascii('x')),
            ].join('|');
            """).AsString().Should().Be(
                "secret"
                + """|{"name":"HMAC","hash":{"name":"SHA-256"},"length":256}"""
                + "|false"
                + "|fb4edf4b94a401d3004cebbdd5e4882f4520efbcee04468c178c5f7fd9e44dad"
                + "|true");
    }

    [Fact]
    public void DerivesAnHkdfKeyFromAnEcdhAgreementAndThenBitsFromIt()
    {
        // The specification's own worked example (§35.1, written there over X25519): agree, derive the whole
        // shared secret into an HKDF key — which works precisely because HKDF's `get key length` answers
        // null and ECDH's derive-bits reads a null length as "all of it" — and expand that.
        Run($$"""
            {{EcdhKeys}}

            const shared = await crypto.subtle.deriveKey({ name: 'ECDH', public: bobPub }, alicePriv, 'HKDF', false, ['deriveBits']);
            const bits = await crypto.subtle.deriveBits({ name: 'HKDF', hash: 'SHA-256', salt: empty, info: empty }, shared, 128);

            return JSON.stringify(shared.algorithm) + '|' + shared.type + '|' + shared.extractable + '|' + hex(bits);
            """).AsString().Should().Be("""{"name":"HKDF"}|secret|false|3bf511eebadf44c1f7b0282a1262fe4d""");
    }

    [Fact]
    public void ThePbkdf2ToHkdfCompositionFailsBecauseNeitherSideSuppliesALength()
    {
        // The mirror image of the test above, and the reason the null is not merely bookkeeping: HKDF's
        // `get key length` answers null, and PBKDF2's derive-bits refuses a null length with an
        // OperationError, so the composition has nowhere to get a number from.
        Run("""
            const password = await crypto.subtle.importKey('raw', ascii('password'), 'PBKDF2', false, ['deriveKey']);
            try {
                await crypto.subtle.deriveKey(
                    { name: 'PBKDF2', salt: ascii('salt'), iterations: 1, hash: 'SHA-256' }, password, 'HKDF', false, ['deriveBits']);
                return 'derived';
            } catch (e) {
                return e.name;
            }
            """).AsString().Should().Be("OperationError");
    }

    [Fact]
    public void TheGetKeyLengthOperationDecidesHowManyBitsAreDerived()
    {
        // HMAC's `get key length` reads HmacImportParams: an absent length is the hash's block size, a
        // present non-zero one is itself, and a present zero is a *TypeError* — the specification's own
        // choice, and the only place in this API where a wrong value is one. AES-GCM's reads
        // AesDerivedKeyParams and refuses anything that is not 128, 192 or 256 with an OperationError.
        Run("""
            const password = await crypto.subtle.importKey('raw', ascii('password'), 'PBKDF2', false, ['deriveKey']);
            const params = { name: 'PBKDF2', salt: ascii('salt'), iterations: 1, hash: 'SHA-256' };

            const derive = async derivedKeyType => {
                try {
                    const key = await crypto.subtle.deriveKey(params, password, derivedKeyType, false, ['sign']);
                    return key.algorithm.length;
                } catch (e) {
                    return e.constructor === DOMException ? e.name : e.constructor.name;
                }
            };

            return [
                // An absent length is the block size: 512 bits for SHA-256, 1024 for SHA-512.
                await derive({ name: 'HMAC', hash: 'SHA-256' }),
                await derive({ name: 'HMAC', hash: 'SHA-512' }),
                await derive({ name: 'HMAC', hash: 'SHA-256', length: 128 }),
                await derive({ name: 'HMAC', hash: 'SHA-256', length: 0 }),
            ].join(',');
            """).AsString().Should().Be("512,1024,128,TypeError");

        Run("""
            const password = await crypto.subtle.importKey('raw', ascii('password'), 'PBKDF2', false, ['deriveKey']);
            const params = { name: 'PBKDF2', salt: ascii('salt'), iterations: 1, hash: 'SHA-256' };

            const derive = async length => {
                try {
                    const key = await crypto.subtle.deriveKey(params, password, { name: 'AES-GCM', length }, false, ['encrypt']);
                    return key.algorithm.length;
                } catch (e) {
                    return e.name;
                }
            };

            return [await derive(128), await derive(192), await derive(256), await derive(100), await derive(512)].join(',');
            """).AsString().Should().Be("128,192,256,OperationError,OperationError");
    }

    [Fact]
    public void NormalizesTheDerivedKeyTypeTwiceInTheSpecifiedOrder()
    {
        // Steps 2 to 7: the algorithm is normalized for deriveBits, and the derivedKeyType is normalized
        // twice — once for importKey and once for `get key length` — with all three happening before a
        // single algorithm step runs. The second reading is observable, and pinning it is what keeps a
        // future "optimisation" that normalizes the derivedKeyType once from silently changing what a
        // script's getters see.
        Run("""
            const password = await crypto.subtle.importKey('raw', ascii('password'), 'PBKDF2', false, ['deriveKey']);
            const reads = [];

            // HMAC is the derivedKeyType that makes the double reading visible: HmacImportParams is what it
            // registers for *both* importKey and get key length, so all three members are read twice.
            const derivedKeyType = {
                get name() { reads.push('name'); return 'HMAC'; },
                get hash() { reads.push('hash'); return 'SHA-256'; },
                get length() { reads.push('length'); return 256; },
            };

            const params = {
                get name() { reads.push('algorithm.name'); return 'PBKDF2'; },
                get hash() { reads.push('algorithm.hash'); return 'SHA-256'; },
                get iterations() { reads.push('algorithm.iterations'); return 1; },
                get salt() { reads.push('algorithm.salt'); return ascii('salt'); },
            };

            const key = await crypto.subtle.deriveKey(params, password, derivedKeyType, false, ['sign']);
            return reads.join(',') + '|' + key.algorithm.length;
            """).AsString().Should().Be(
                // The algorithm's own members, in WebIDL's lexicographical order after `name`.
                "algorithm.name,algorithm.hash,algorithm.iterations,algorithm.salt,"
                // Then the derivedKeyType, twice: once as HmacImportParams for importKey, once as
                // HmacImportParams for get key length.
                + "name,hash,length,name,hash,length"
                + "|256");
    }

    [Fact]
    public void TheBaseKeyNeedsDeriveKeyForDeriveKeyAndDeriveBitsForDeriveBits()
    {
        // The two methods check *different* usages — deriveKey wants 'deriveKey' even though bits are what
        // the derivation produces — and neither consults the base key's own extractability, which is what
        // lets a non-extractable password produce an extractable key.
        Run("""
            const forBits = await crypto.subtle.importKey('raw', ascii('password'), 'PBKDF2', false, ['deriveBits']);
            const forKey = await crypto.subtle.importKey('raw', ascii('password'), 'PBKDF2', false, ['deriveKey']);
            const params = { name: 'PBKDF2', salt: ascii('salt'), iterations: 1, hash: 'SHA-256' };

            const attempt = async fn => {
                try { await fn(); return 'ok'; } catch (e) { return e.name; }
            };

            return [
                await attempt(() => crypto.subtle.deriveBits(params, forBits, 128)),
                await attempt(() => crypto.subtle.deriveBits(params, forKey, 128)),
                await attempt(() => crypto.subtle.deriveKey(params, forKey, { name: 'AES-GCM', length: 128 }, false, ['encrypt'])),
                await attempt(() => crypto.subtle.deriveKey(params, forBits, { name: 'AES-GCM', length: 128 }, false, ['encrypt'])),
            ].join(',');
            """).AsString().Should().Be("ok,InvalidAccessError,ok,InvalidAccessError");
    }

    [Fact]
    public void TheDerivedKeyIsBuiltByTheImportPathAndInheritsItsRefusals()
    {
        // deriveKey is a composition: the bits are imported through exactly the `raw` branch importKey would
        // have taken, so a derived secret key with no usages is the SyntaxError importKey's shared tail
        // gives. What it will *not* accept at all is an asymmetric derivedKeyType — the registry decides
        // that, not any step: RSA and the elliptic curves register importKey but not `get key length`, so
        // step 6's normalization refuses them before a single bit is derived.
        Run("""
            const password = await crypto.subtle.importKey('raw', ascii('password'), 'PBKDF2', false, ['deriveKey']);
            const params = { name: 'PBKDF2', salt: ascii('salt'), iterations: 1, hash: 'SHA-256' };

            const attempt = async (derivedKeyType, usages) => {
                try { await crypto.subtle.deriveKey(params, password, derivedKeyType, false, usages); return 'derived'; }
                catch (e) { return e.name; }
            };

            return [
                // Asymmetric derivedKeyTypes: registered for importKey, not for get key length.
                await attempt({ name: 'RSA-OAEP', hash: 'SHA-256' }, ['encrypt']),
                await attempt({ name: 'ECDSA', namedCurve: 'P-256' }, ['sign']),
                // A secret key nobody may use is a mistake, not a key.
                await attempt({ name: 'AES-GCM', length: 128 }, []),
                // A usage the derived algorithm does not support.
                await attempt({ name: 'AES-GCM', length: 128 }, ['sign']),
            ].join(',');
            """).AsString().Should().Be("NotSupportedError,NotSupportedError,SyntaxError,SyntaxError");
    }

    // ---------------------------------------------------------------------------------------------------
    // The registries
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    // ECDH derives and never signs or encrypts; ECDSA is the reverse.
    [InlineData("deriveBits", "{ name: 'ECDSA', namedCurve: 'P-256' }")]
    [InlineData("deriveBits", "'HMAC'")]
    [InlineData("deriveBits", "'AES-GCM'")]
    [InlineData("deriveBits", "'RSA-OAEP'")]
    // HKDF and PBKDF2 derive and nothing else, so every other operation is a NotSupportedError.
    [InlineData("sign", "'HKDF'")]
    [InlineData("encrypt", "'PBKDF2'")]
    [InlineData("generateKey", "'HKDF'")]
    [InlineData("generateKey", "'PBKDF2'")]
    public void RefusesAnAlgorithmThatIsNotRegisteredForTheOperation(string operation, string algorithm)
    {
        var engine = WebEngine();

        var call = operation switch
        {
            "deriveBits" => $"crypto.subtle.deriveBits({algorithm}, key, 128)",
            "sign" => $"crypto.subtle.sign({algorithm}, key, new Uint8Array(0))",
            "encrypt" => $"crypto.subtle.encrypt({algorithm}, key, new Uint8Array(0))",
            _ => $"crypto.subtle.generateKey({algorithm}, false, ['deriveBits'])",
        };

        Settle(engine, $$"""
            crypto.subtle.importKey('raw', new Uint8Array(16), 'PBKDF2', false, ['deriveBits'])
                .then(key => {{call}})
                .then(() => 'succeeded', e => e.name + '/' + e.code)
            """).AsString().Should().Be("NotSupportedError/9");
    }

    [Fact]
    public void ADerivationAgainstTheWrongAlgorithmIsAnInvalidAccessError()
    {
        // "If the name member of normalizedAlgorithm is not equal to the name attribute of the [[algorithm]]
        // internal slot of baseKey then throw an InvalidAccessError" — the check both methods make before
        // anything else. The two derivation algorithms are registered for the same operation, so this is the
        // only thing keeping an HKDF key out of a PBKDF2 derivation.
        Run("""
            const hkdf = await crypto.subtle.importKey('raw', new Uint8Array(22), 'HKDF', false, ['deriveBits']);

            try {
                await crypto.subtle.deriveBits({ name: 'PBKDF2', salt: ascii('salt'), iterations: 1, hash: 'SHA-256' }, hkdf, 128);
                return 'derived';
            } catch (e) {
                return e.name;
            }
            """).AsString().Should().Be("InvalidAccessError");
    }

    [Fact]
    public void TheHkdfAndPbkdf2ParameterMembersAreAllRequired()
    {
        // HkdfParams declares `required hash`, `required salt` and `required info`, and Pbkdf2Params
        // `required salt`, `required iterations` and `required hash` — so "no salt" is the empty byte
        // sequence and never an omission, and a missing member is the TypeError WebIDL raises for a required
        // one rather than a defaulted derivation.
        Run("""
            const hkdf = await crypto.subtle.importKey('raw', new Uint8Array(22), 'HKDF', false, ['deriveBits']);
            const pbkdf2 = await crypto.subtle.importKey('raw', ascii('password'), 'PBKDF2', false, ['deriveBits']);

            const attempt = async (params, key) => {
                try { await crypto.subtle.deriveBits(params, key, 128); return 'derived'; }
                catch (e) { return e.constructor.name; }
            };

            return [
                await attempt({ name: 'HKDF', salt: empty, info: empty }, hkdf),
                await attempt({ name: 'HKDF', hash: 'SHA-256', info: empty }, hkdf),
                await attempt({ name: 'HKDF', hash: 'SHA-256', salt: empty }, hkdf),
                await attempt({ name: 'PBKDF2', iterations: 1, hash: 'SHA-256' }, pbkdf2),
                await attempt({ name: 'PBKDF2', salt: empty, hash: 'SHA-256' }, pbkdf2),
                await attempt({ name: 'PBKDF2', salt: empty, iterations: 1 }, pbkdf2),
                // A salt that is not a buffer source is the same failure, and a SharedArrayBuffer view is
                // refused because the IDL says BufferSource and not AllowSharedBufferSource.
                await attempt({ name: 'HKDF', hash: 'SHA-256', salt: 'salt', info: empty }, hkdf),
            ].join(',');
            """).AsString().Should().Be("TypeError,TypeError,TypeError,TypeError,TypeError,TypeError,TypeError");
    }

    [Fact]
    public void BufferSourceMembersAreCopiedAtNormalizationTime()
    {
        // "Set the dictionary member to the result of getting a copy of the bytes held by idlValue." The copy
        // is real and it has to be: normalization goes on to read further members, which may run a script's
        // getter with an already-read array still in scope, so a window onto the engine's backing store would
        // be a window onto whatever that getter left behind.
        //
        // `info` is read before `salt` — WebIDL walks a dictionary's own members in lexicographical order —
        // so the mutation goes the other way round: a `salt` getter that rewrites the *info* array must not
        // change the derivation, because those bytes were copied one member earlier.
        Run("""
            const key = await crypto.subtle.importKey('raw', new Uint8Array(22), 'HKDF', false, ['deriveBits']);
            const info = new Uint8Array([1, 2, 3, 4]);

            const params = {
                name: 'HKDF',
                hash: 'SHA-256',
                info,
                get salt() { info.fill(0xff); return empty; },
            };

            const derived = hex(await crypto.subtle.deriveBits(params, key, 128));

            // The control derives with the bytes the caller actually passed, from an object no getter can
            // reach, and the mutated array is the third answer this must not equal.
            const control = hex(await crypto.subtle.deriveBits(
                { name: 'HKDF', hash: 'SHA-256', salt: empty, info: new Uint8Array([1, 2, 3, 4]) }, key, 128));
            const mutated = hex(await crypto.subtle.deriveBits(
                { name: 'HKDF', hash: 'SHA-256', salt: empty, info: new Uint8Array([0xff, 0xff, 0xff, 0xff]) }, key, 128));

            return (derived === control) + '|' + (derived === mutated) + '|' + hex(info);
            """).AsString().Should().Be("true|false|ffffffff");
    }
}
#endif
