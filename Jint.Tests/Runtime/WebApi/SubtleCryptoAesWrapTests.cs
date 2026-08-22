#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The three AES modes this engine added last — AES-CBC (https://w3c.github.io/webcrypto/#aes-cbc), AES-CTR
/// (https://w3c.github.io/webcrypto/#aes-ctr) and AES-KW (https://w3c.github.io/webcrypto/#aes-kw) — and the
/// two operations that complete <c>crypto.subtle</c>, <c>wrapKey</c>
/// (https://w3c.github.io/webcrypto/#SubtleCrypto-method-wrapKey) and <c>unwrapKey</c>
/// (https://w3c.github.io/webcrypto/#SubtleCrypto-method-unwrapKey).
/// </summary>
/// <remarks>
/// <para>
/// Every cipher is checked against a vector somebody else published, which is the only way to check
/// cryptography: Appendix F.2 and F.5 of [NIST-SP800-38A] for CBC and CTR at all three key sizes, and Section
/// 4 of [RFC3394] for all six AES-KW combinations of key-encryption-key and payload size. A round trip proves
/// only that the engine agrees with itself.
/// </para>
/// <para>
/// Three expectations here are <b>not</b> published and were computed outside this engine, with a from-scratch
/// AES written for the purpose and validated against the NIST single-block ECB vectors of Appendix F.1: the
/// fifth block of each CBC ciphertext, which is the PKCS#7 padding block the Web Cryptography API adds and
/// NIST's unpadded example does not have; and the two CTR counter-wrap cases, where the counter field runs
/// past its maximum and has to continue at zero with the nonce to its left untouched. Those are the cases an
/// implementation gets wrong by treating the whole 128-bit block as one integer, and they are exactly where a
/// self-consistent round trip would notice nothing.
/// </para>
/// </remarks>
public class SubtleCryptoAesWrapTests
{
    /// <summary>The helpers the other <c>crypto.subtle</c> suites use, so a script here reads like theirs.</summary>
    private const string Prelude = """
        const hex = b => Array.from(new Uint8Array(b)).map(x => x.toString(16).padStart(2, '0')).join('');
        const bytes = h => Uint8Array.from(h.match(/../g) || [], x => parseInt(x, 16));
        const ascii = s => Uint8Array.from(s, c => c.charCodeAt(0));
        const repeat = (byte, count) => new Uint8Array(count).fill(byte);
        """;

    // ---------------------------------------------------------------------------------------------------
    // The NIST SP 800-38A Appendix F inputs, shared by the CBC and CTR vectors
    // ---------------------------------------------------------------------------------------------------

    /// <summary>The three example keys of Appendix F, at 128, 192 and 256 bits.</summary>
    private const string Key128 = "2b7e151628aed2a6abf7158809cf4f3c";
    private const string Key192 = "8e73b0f7da0e6452c810f32b809079e562f8ead2522c6b7b";
    private const string Key256 = "603deb1015ca71be2b73aef0857d77811f352c073b6108d72d9810a30914dff4";

    /// <summary>The four example plaintext blocks every one of those vectors is built from.</summary>
    private const string PlaintextBlocks =
        "6bc1bee22e409f96e93d7e117393172a"
        + "ae2d8a571e03ac9c9eb76fac45af8e51"
        + "30c81c46a35ce411e5fbc1191a0a52ef"
        + "f69f2445df4f9b17ad2b417be66c3710";

    /// <summary>The IV of the CBC examples and the initial counter block of the CTR ones.</summary>
    private const string CbcIv = "000102030405060708090a0b0c0d0e0f";
    private const string CtrCounter = "f0f1f2f3f4f5f6f7f8f9fafbfcfdfeff";

    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Crypto));

    /// <summary>Runs an async body to completion and answers what it returned.</summary>
    private static JsValue Run(string body, Engine? engine = null)
    {
        engine ??= WebEngine();
        return engine.Evaluate(Prelude + "\n(async () => {\n" + body + "\n})()").UnwrapIfPromise();
    }

    /// <summary>Settles one expression's promise and answers whatever it resolved or rejected to.</summary>
    private static JsValue Settle(Engine engine, string source) => engine.Evaluate(source).UnwrapIfPromise();

    // ---------------------------------------------------------------------------------------------------
    // AES-CBC — NIST SP 800-38A Appendix F.2
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    // The ciphertext is the four blocks of F.2.1 / F.2.3 / F.2.5 followed by one more: the Web Cryptography
    // API always adds PKCS#7 padding, and a plaintext that is already a whole number of blocks gains a whole
    // block of 0x10 octets. That fifth block is the one figure here NIST does not publish — see the remarks
    // on this class for how it was computed.
    [InlineData(
        Key128,
        "7649abac8119b246cee98e9b12e9197d5086cb9b507219ee95db113a917678b2"
        + "73bed6b8e3c1743b7116e69e222295163ff1caa1681fac09120eca307586e1a7"
        + "8cb82807230e1321d3fae00d18cc2012")]
    [InlineData(
        Key192,
        "4f021db243bc633d7178183a9fa071e8b4d9ada9ad7dedf4e5e738763f69145a"
        + "571b242012fb7ae07fa9baac3df102e008b0e27988598881d920a9e64f5615cd"
        + "612ccd79224b350935d45dd6a98f8176")]
    [InlineData(
        Key256,
        "f58c4c04d6e5f1ba779eabfb5f7bfbd69cfc4e967edb808d679f777bc6702c7d"
        + "39f23369a9d9bacfa530e26304231461b2eb05e2c39be9fcda6c19078c6a9d1b"
        + "3f461796d6b0d6b2e0c2a72b4d80e644")]
    public void EncryptsThePublishedAesCbcVectors(string keyHex, string expected)
    {
        Run($$"""
            const key = await crypto.subtle.importKey('raw', bytes('{{keyHex}}'), 'AES-CBC', false, ['encrypt', 'decrypt']);
            const params = { name: 'AES-CBC', iv: bytes('{{CbcIv}}') };

            const ciphertext = await crypto.subtle.encrypt(params, key, bytes('{{PlaintextBlocks}}'));
            const plaintext = await crypto.subtle.decrypt(params, key, ciphertext);

            // The first four blocks are NIST's own ciphertext, and decrypting the whole thing gives the
            // plaintext back — so both directions are pinned against the published bytes.
            return hex(ciphertext) + '|' + (hex(ciphertext).startsWith('{{expected}}'.slice(0, 128)))
                + '|' + hex(plaintext);
            """).AsString().Should().Be(expected + "|true|" + PlaintextBlocks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    public void PadsEveryPlaintextLengthToTheNextWholeBlockAndRecoversIt(int length)
    {
        // "Let paddedPlaintext be the result of adding padding octets to plaintext according to the procedure
        // defined in Section 10.3 of [RFC2315], step 2, with a value of k of 16" — which always adds between
        // one and sixteen octets, so the ciphertext is strictly longer than the plaintext and a plaintext on
        // a block boundary costs a whole extra block.
        var expectedLength = length + 16 - (length % 16);

        Run($$"""
            const key = await crypto.subtle.generateKey({ name: 'AES-CBC', length: 256 }, false, ['encrypt', 'decrypt']);
            const params = { name: 'AES-CBC', iv: repeat(7, 16) };
            const message = Uint8Array.from({ length: {{length}} }, (_, i) => i & 0xff);

            const ciphertext = await crypto.subtle.encrypt(params, key, message);
            const plaintext = await crypto.subtle.decrypt(params, key, ciphertext);

            return ciphertext.byteLength + '|' + hex(plaintext) + '|' + hex(message);
            """).AsString().Should().Match($"{expectedLength}|*");

        Run($$"""
            const key = await crypto.subtle.generateKey({ name: 'AES-CBC', length: 256 }, false, ['encrypt', 'decrypt']);
            const params = { name: 'AES-CBC', iv: repeat(7, 16) };
            const message = Uint8Array.from({ length: {{length}} }, (_, i) => i & 0xff);

            const ciphertext = await crypto.subtle.encrypt(params, key, message);
            const plaintext = await crypto.subtle.decrypt(params, key, ciphertext);

            return hex(plaintext) === hex(message);
            """).AsBoolean().Should().BeTrue();
    }

    [Theory]
    // Steps 4 and 5: "Let p be the value of the last octet of paddedPlaintext. If p is zero or greater than
    // 16, or if any of the last p octets of paddedPlaintext have a value which is not p, then throw an
    // OperationError." Each of the three ways that can fail is constructed rather than stumbled on, which is
    // what makes this deterministic and what makes each clause of the check load-bearing.
    //
    // The plaintext of block N is D(C[N]) xor C[N-1], so a byte of the *first* ciphertext block chooses a
    // byte of the padding block exactly. The three corruptions below are the whole of the check:
    //
    //   index 0, xor 0x01  — the padding's first octet becomes 0x11 while the last stays 0x10, so p is a
    //                        perfectly good 16 and one of the octets it claims is not 16. Only a check that
    //                        reads all p octets catches this one.
    [InlineData(0, "0x01")]
    //   index 15, xor 0x10 — the last octet becomes 0x00, which is "p is zero".
    [InlineData(15, "0x10")]
    //   index 15, xor 0x80 — the last octet becomes 0x90, which is "p is greater than 16".
    [InlineData(15, "0x80")]
    //   ... and a byte of the padding block itself, which randomizes the whole block and its padding with it.
    [InlineData(16, "0x01")]
    [InlineData(31, "0x01")]
    public void RefusesACiphertextWhosePaddingIsWrong(int index, string mask)
    {
        // The failure is the one message every other CBC failure gives: a decrypt that can tell "the padding
        // was malformed" from "the padding was fine" is a padding oracle, and CBC without an authentication
        // tag is the shape that attack was invented for.
        Run($$"""
            const key = await crypto.subtle.importKey('raw', bytes('{{Key128}}'), 'AES-CBC', false, ['encrypt', 'decrypt']);
            const params = { name: 'AES-CBC', iv: bytes('{{CbcIv}}') };

            const ciphertext = new Uint8Array(
                await crypto.subtle.encrypt(params, key, bytes('00112233445566778899aabbccddeeff')));

            // One 16-byte message becomes two blocks: the message and a whole block of 0x10 padding.
            const corrupted = ciphertext.slice();
            corrupted[{{index}}] ^= {{mask}};

            try {
                await crypto.subtle.decrypt(params, key, corrupted);
                return 'decrypted';
            } catch (e) {
                return ciphertext.byteLength + '|' + e.name + ':' + e.message.slice(e.message.indexOf(': ') + 2);
            }
            """).AsString().Should().Be("32|OperationError:the data could not be decrypted.");
    }

    [Theory]
    [InlineData("repeat(0, 15)")]
    [InlineData("repeat(0, 17)")]
    [InlineData("new Uint8Array(0)")]
    public void RefusesAnAesCbcIvThatIsNotOneBlock(string iv)
    {
        var engine = WebEngine();

        // "If the iv member of normalizedAlgorithm does not have a length of 16 bytes, then throw an
        // OperationError" — the algorithm's own restriction, not the platform's, unlike AES-GCM's nonce.
        Settle(engine, $$"""
            (async () => {
                const key = await crypto.subtle.generateKey({ name: 'AES-CBC', length: 128 }, false, ['encrypt', 'decrypt']);
                const repeat = (byte, count) => new Uint8Array(count).fill(byte);
                return crypto.subtle.encrypt({ name: 'AES-CBC', iv: {{iv}} }, key, new Uint8Array(0))
                    .then(() => 'encrypted', e => e.name);
            })()
            """).AsString().Should().Be("OperationError");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(17)]
    public void RefusesAnAesCbcCiphertextThatIsNotAWholeNumberOfBlocks(int length)
    {
        // "If the length of ciphertext is zero or is not a multiple of 16 bytes, then throw an
        // OperationError." The zero case is separate from the multiple: an AES-CBC ciphertext is never empty,
        // because the padding alone is a block.
        Run($$"""
            const key = await crypto.subtle.generateKey({ name: 'AES-CBC', length: 128 }, false, ['encrypt', 'decrypt']);
            try {
                await crypto.subtle.decrypt({ name: 'AES-CBC', iv: repeat(0, 16) }, key, new Uint8Array({{length}}));
                return 'decrypted';
            } catch (e) {
                return e.name;
            }
            """).AsString().Should().Be("OperationError");
    }

    // ---------------------------------------------------------------------------------------------------
    // AES-CTR — NIST SP 800-38A Appendix F.5
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    // F.5.1, F.5.3 and F.5.5, whose counter is the whole 128-bit block (m = 128) and whose plaintext is the
    // same four blocks. There is no padding: CTR is a stream cipher, so the ciphertext is the length of the
    // plaintext exactly.
    [InlineData(
        Key128,
        "874d6191b620e3261bef6864990db6ce9806f66b7970fdff8617187bb9fffdff"
        + "5ae4df3edbd5d35e5b4f09020db03eab1e031dda2fbe03d1792170a0f3009cee")]
    [InlineData(
        Key192,
        "1abc932417521ca24f2b0459fe7e6e0b090339ec0aa6faefd5ccc2c6f4ce8e94"
        + "1e36b26bd1ebc670d1bd1d665620abf74f78a7f6d29809585a97daec58c6b050")]
    [InlineData(
        Key256,
        "601ec313775789a5b7a7f504bbf3d228f443e3ca4d62b59aca84e990cacaf5c5"
        + "2b0930daa23de94ce87017ba2d84988ddfc9c58db67aada613c2dd08457941a6")]
    public void EncryptsThePublishedAesCtrVectors(string keyHex, string expected)
    {
        Run($$"""
            const key = await crypto.subtle.importKey('raw', bytes('{{keyHex}}'), 'AES-CTR', false, ['encrypt', 'decrypt']);
            const params = { name: 'AES-CTR', counter: bytes('{{CtrCounter}}'), length: 128 };

            const ciphertext = await crypto.subtle.encrypt(params, key, bytes('{{PlaintextBlocks}}'));

            // Encryption and decryption are one operation, so decrypting the published ciphertext must give
            // the published plaintext with the same call.
            const plaintext = await crypto.subtle.decrypt(params, key, bytes('{{expected}}'));

            return hex(ciphertext) + '|' + hex(plaintext);
            """).AsString().Should().Be(expected + "|" + PlaintextBlocks);
    }

    [Fact]
    public void IncrementsOnlyTheRightmostLengthBitsAndWrapsThemModuloTwoToTheLength()
    {
        // The case the specification points at Appendix B.1 of [NIST-SP800-38A] for: with length 32 the
        // counter is the last four bytes, and 0xffffffff is followed by 0x00000000 — not by a carry into the
        // twelve bytes of nonce to its left. An implementation that treated the block as one 128-bit integer
        // would produce a third keystream block from …0c00000000 and a different ciphertext from block two on.
        //
        // The expectation was computed outside this engine — see the remarks on this class.
        Run($$"""
            const key = await crypto.subtle.importKey('raw', bytes('{{Key128}}'), 'AES-CTR', false, ['encrypt']);
            const ciphertext = await crypto.subtle.encrypt(
                { name: 'AES-CTR', counter: bytes('000102030405060708090a0bffffffff'), length: 32 },
                key,
                new Uint8Array(48));
            return hex(ciphertext);
            """).AsString().Should().Be(
                "bdb7c0ef49717942fc68eeb17692fcf4"
                + "94193f8116eb745cfe7465d70c756236"
                + "a715b99567eaea4806b3a91c785f11cc");
    }

    [Theory]
    // length 4 puts the whole counter in the low nibble of the last byte: 0x1f becomes 0x10 and then 0x11, so
    // the nibble wraps and the *nonce* nibble above it keeps its value. The deliberate choice here is a nonce
    // nibble that is not 0xf — with 0xff a carry out of the nibble happens to land on bits that were already
    // set, and an implementation that forgot to mask the increment would give the same answer by luck.
    [InlineData("000102030405060708090a0b0c0d0e1f", 4,
        "d382e6e0f67d3973b51ef9c0321691e3af218e4e87e393aada314be47bf7a35f96ea8f8905839042b9ce80980c8d2630")]
    // length 12 straddles a byte boundary: the counter is all of the last byte and the low nibble of the one
    // before it, so 0x1fff becomes 0x1000 — a carry out of a whole byte into a partial one, with four bits of
    // nonce immediately above the carry.
    [InlineData("000102030405060708090a0b0c0d1fff", 12,
        "8250c610f6a654214a55c545382342f66cd367d6b58575858b44a7cbac0431fa51367ca1f0054dc9088d431c6c41ee1f")]
    public void WrapsACounterFieldThatDoesNotEndOnAByteBoundary(string counter, int length, string expected)
    {
        // Computed outside this engine, like the case above.
        Run($$"""
            const key = await crypto.subtle.importKey('raw', bytes('{{Key128}}'), 'AES-CTR', false, ['encrypt']);
            const ciphertext = await crypto.subtle.encrypt(
                { name: 'AES-CTR', counter: bytes('{{counter}}'), length: {{length}} },
                key,
                new Uint8Array(48));
            return hex(ciphertext);
            """).AsString().Should().Be(expected);
    }

    [Fact]
    public void WrapsTheWholeBlockWhenTheCounterFieldIsAllOfIt()
    {
        // length 128 and a counter block of all ones: the whole block wraps to zero, and the carry out of the
        // most significant bit is dropped rather than being anything at all.
        Run($$"""
            const key = await crypto.subtle.importKey('raw', bytes('{{Key128}}'), 'AES-CTR', false, ['encrypt']);
            const ciphertext = await crypto.subtle.encrypt(
                { name: 'AES-CTR', counter: bytes('ffffffffffffffffffffffffffffffff'), length: 128 },
                key,
                new Uint8Array(32));
            return hex(ciphertext);
            """).AsString().Should().Be("8af2860142f786f409307c1a3f7eaaac7df76b0c1ab899b33e42f047b91b546f");
    }

    [Theory]
    [InlineData("repeat(0, 15)", "128", "OperationError")]
    [InlineData("repeat(0, 17)", "128", "OperationError")]
    [InlineData("repeat(0, 16)", "0", "OperationError")]
    [InlineData("repeat(0, 16)", "129", "OperationError")]
    [InlineData("repeat(0, 16)", "255", "OperationError")]
    // The member's IDL type is `octet`, so a value outside 0..255 fails the [EnforceRange] conversion during
    // normalization, before a single step of the operation runs — a TypeError, not an OperationError.
    [InlineData("repeat(0, 16)", "256", "TypeError")]
    [InlineData("repeat(0, 16)", "-1", "TypeError")]
    [InlineData("repeat(0, 16)", "1", "ok")]
    [InlineData("repeat(0, 16)", "128", "ok")]
    public void EnforcesTheAesCtrCounterAndLengthMatrix(string counter, string length, string expected)
    {
        Run($$"""
            const key = await crypto.subtle.generateKey({ name: 'AES-CTR', length: 128 }, false, ['encrypt']);
            try {
                await crypto.subtle.encrypt({ name: 'AES-CTR', counter: {{counter}}, length: {{length}} }, key, ascii('m'));
                return 'ok';
            } catch (e) {
                return e.name === undefined ? e.constructor.name : e.name;
            }
            """).AsString().Should().Be(expected);
    }

    [Fact]
    public void EncryptsTheEmptyMessageToTheEmptyCiphertext()
    {
        // CTR needs no keystream at all for a zero-length message, and a stream cipher's ciphertext is the
        // length of its plaintext — unlike CBC, whose padding makes even the empty message a whole block.
        Run($$"""
            const ctr = await crypto.subtle.generateKey({ name: 'AES-CTR', length: 128 }, false, ['encrypt']);
            const cbc = await crypto.subtle.generateKey({ name: 'AES-CBC', length: 128 }, false, ['encrypt']);

            const a = await crypto.subtle.encrypt({ name: 'AES-CTR', counter: repeat(0, 16), length: 64 }, ctr, new Uint8Array(0));
            const b = await crypto.subtle.encrypt({ name: 'AES-CBC', iv: repeat(0, 16) }, cbc, new Uint8Array(0));

            return a.byteLength + ',' + b.byteLength;
            """).AsString().Should().Be("0,16");
    }

    // ---------------------------------------------------------------------------------------------------
    // AES-KW — RFC 3394 section 4
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    // https://www.rfc-editor.org/rfc/rfc3394#section-4, all six combinations. The key data of each vector is
    // itself a valid AES key length, so it is imported as an AES-CBC key and wrapped in the `raw` format —
    // which makes the bytes handed to the wrap operation exactly the vector's "Key Data".
    [InlineData("000102030405060708090A0B0C0D0E0F", "00112233445566778899AABBCCDDEEFF",
        "1fa68b0a8112b447aef34bd8fb5a7b829d3e862371d2cfe5")]
    [InlineData("000102030405060708090A0B0C0D0E0F1011121314151617", "00112233445566778899AABBCCDDEEFF",
        "96778b25ae6ca435f92b5b97c050aed2468ab8a17ad84e5d")]
    [InlineData("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F", "00112233445566778899AABBCCDDEEFF",
        "64e8c3f9ce0f5ba263e9777905818a2a93c8191e7d6e8ae7")]
    [InlineData("000102030405060708090A0B0C0D0E0F1011121314151617", "00112233445566778899AABBCCDDEEFF0001020304050607",
        "031d33264e15d33268f24ec260743edce1c6c7ddee725a936ba814915c6762d2")]
    [InlineData("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F", "00112233445566778899AABBCCDDEEFF0001020304050607",
        "a8f9bc1612c68b3ff6e6f4fbe30e71e4769c8b80a32cb8958cd5d17d6b254da1")]
    [InlineData("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F", "00112233445566778899AABBCCDDEEFF000102030405060708090A0B0C0D0E0F",
        "28c9f404c4b810f4cbccb35cfb87f8263f5786e2d80ed326cbc7f0e71a99f43bfb988b9b7a02dd21")]
    public void WrapsAndUnwrapsThePublishedRfc3394Vectors(string kekHex, string keyDataHex, string expected)
    {
        Run($$"""
            const kek = await crypto.subtle.importKey('raw', bytes('{{kekHex}}'), 'AES-KW', false, ['wrapKey', 'unwrapKey']);
            const keyData = await crypto.subtle.importKey('raw', bytes('{{keyDataHex}}'), 'AES-CBC', true, ['encrypt']);

            const wrapped = await crypto.subtle.wrapKey('raw', keyData, kek, 'AES-KW');

            const unwrapped = await crypto.subtle.unwrapKey(
                'raw', bytes('{{expected}}'), kek, 'AES-KW', { name: 'AES-CBC', length: {{keyDataHex.Length * 4}} }, true, ['encrypt']);

            // Both directions against the published bytes: the wrap produces the vector's ciphertext, and
            // the vector's ciphertext unwraps to the vector's key data.
            return hex(wrapped) + '|' + hex(await crypto.subtle.exportKey('raw', unwrapped));
            """).AsString().Should().Be(expected + "|" + keyDataHex.ToLowerInvariant());
    }

    [Fact]
    public void RefusesAWrappedPayloadThatIsNotAWholeNumberOfBlocks()
    {
        // "If plaintext is not a multiple of 64 bits in length, then throw an OperationError." An HMAC key of
        // 100 bits exports as a 13-byte raw key, which is exactly such a payload — and the 8-byte one below
        // is a whole number of blocks but only one of them, which the wrap algorithm has no reading of.
        Run("""
            const kek = await crypto.subtle.generateKey({ name: 'AES-KW', length: 256 }, false, ['wrapKey', 'unwrapKey']);

            const outcomes = [];
            for (const bits of [104, 64]) {
                const key = await crypto.subtle.importKey(
                    'raw', new Uint8Array(bits / 8), { name: 'HMAC', hash: 'SHA-256' }, true, ['sign']);
                try {
                    await crypto.subtle.wrapKey('raw', key, kek, 'AES-KW');
                    outcomes.push('wrapped');
                } catch (e) {
                    outcomes.push(e.name);
                }
            }

            return outcomes.join(',');
            """).AsString().Should().Be("OperationError,OperationError");
    }

    [Fact]
    public void RefusesAWrappedKeyWhoseIntegrityCheckFailsWithAnIndistinguishableMessage()
    {
        // "If the Key Unwrap operation returns an error, then throw an OperationError." Every way the unwrap
        // can fail — a flipped bit anywhere in the ciphertext, the wrong key-encryption key, a ciphertext of
        // the wrong shape — gives the one message, which is the same CCA discipline RSA-OAEP's decrypt keeps.
        Run("""
            const kek = await crypto.subtle.generateKey({ name: 'AES-KW', length: 128 }, true, ['wrapKey', 'unwrapKey']);
            const other = await crypto.subtle.generateKey({ name: 'AES-KW', length: 128 }, false, ['wrapKey', 'unwrapKey']);
            const key = await crypto.subtle.generateKey({ name: 'AES-CBC', length: 128 }, true, ['encrypt']);
            const wrapped = new Uint8Array(await crypto.subtle.wrapKey('raw', key, kek, 'AES-KW'));

            const unwrap = (data, using_) => crypto.subtle.unwrapKey(
                'raw', data, using_, 'AES-KW', { name: 'AES-CBC', length: 128 }, true, ['encrypt']);

            const outcomes = [];
            for (const index of [0, 7, 8, wrapped.length - 1]) {
                const corrupted = wrapped.slice();
                corrupted[index] ^= 0x80;
                try { await unwrap(corrupted, kek); outcomes.push('unwrapped'); }
                catch (e) { outcomes.push(e.name + ':' + e.message.slice(e.message.indexOf(': ') + 2)); }
            }

            try { await unwrap(wrapped, other); outcomes.push('unwrapped'); }
            catch (e) { outcomes.push(e.name + ':' + e.message.slice(e.message.indexOf(': ') + 2)); }

            // Every entry the same string, and the wrapped key still unwraps under the key that made it.
            const distinct = [...new Set(outcomes)];
            const roundTrip = hex(await crypto.subtle.exportKey('raw', await unwrap(wrapped, kek)))
                === hex(await crypto.subtle.exportKey('raw', key));

            return distinct.length + '|' + distinct[0] + '|' + roundTrip;
            """).AsString().Should().Be("1|OperationError:the key could not be unwrapped.|true");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(25)]
    public void RefusesAnAesKwCiphertextOfTheWrongShape(int length)
    {
        // A wrapped payload is (n + 1) blocks for n of at least two, so anything under three blocks — or not
        // a whole number of them — is an input the unwrap algorithm has no reading of. It is the same
        // OperationError an integrity failure earns.
        Run($$"""
            const kek = await crypto.subtle.generateKey({ name: 'AES-KW', length: 128 }, false, ['wrapKey', 'unwrapKey']);
            try {
                await crypto.subtle.unwrapKey(
                    'raw', new Uint8Array({{length}}), kek, 'AES-KW', { name: 'AES-CBC', length: 128 }, true, ['encrypt']);
                return 'unwrapped';
            } catch (e) {
                return e.name;
            }
            """).AsString().Should().Be("OperationError");
    }

    // ---------------------------------------------------------------------------------------------------
    // The shared AES key management, at the three names that are new
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("AES-CTR", 128, "A128CTR")]
    [InlineData("AES-CTR", 192, "A192CTR")]
    [InlineData("AES-CTR", 256, "A256CTR")]
    [InlineData("AES-CBC", 128, "A128CBC")]
    [InlineData("AES-CBC", 192, "A192CBC")]
    [InlineData("AES-CBC", 256, "A256CBC")]
    [InlineData("AES-GCM", 128, "A128GCM")]
    [InlineData("AES-GCM", 256, "A256GCM")]
    [InlineData("AES-KW", 128, "A128KW")]
    [InlineData("AES-KW", 192, "A192KW")]
    [InlineData("AES-KW", 256, "A256KW")]
    public void ExportsAndReimportsAJsonWebKeyWithTheAlgorithmsOwnAlgField(string name, int length, string alg)
    {
        // https://www.rfc-editor.org/rfc/rfc7518#section-4.7 for the ciphers and #section-4.4 for AES-KW —
        // the twelve names A128CBC … A256KW. The four algorithms' import and export steps are the same to the
        // letter apart from this string and the algorithm's own name, which is why they share an
        // implementation, and this is what proves the two strings are still the algorithm's own.
        var usages = string.Equals(name, "AES-KW", StringComparison.Ordinal) ? "['wrapKey']" : "['encrypt']";

        Run($$"""
            const key = await crypto.subtle.generateKey({ name: '{{name}}', length: {{length}} }, true, {{usages}});
            const jwk = await crypto.subtle.exportKey('jwk', key);

            const reimported = await crypto.subtle.importKey('jwk', jwk, '{{name}}', true, {{usages}});
            const raw = hex(await crypto.subtle.exportKey('raw', key));

            return jwk.alg + '|' + jwk.kty + '|' + jwk.ext + '|' + jwk.key_ops.join(',')
                + '|' + key.algorithm.name + '|' + key.algorithm.length
                + '|' + (hex(await crypto.subtle.exportKey('raw', reimported)) === raw);
            """).AsString().Should().Be(
                alg + "|oct|true|" + usages.Trim('[', ']').Replace("'", "") + "|" + name + "|" + length + "|true");
    }

    [Fact]
    public void RefusesAJsonWebKeyWhoseAlgNamesAnotherAesMode()
    {
        var engine = WebEngine();

        // "If the length in bits of data is 128: If the alg field of jwk is present, and is not 'A128CBC',
        // then throw a DataError." A128GCM is a perfectly good alg for a 128-bit AES key and is the wrong one
        // for AES-CBC, which is exactly the mistake this catches — the two modes are not interchangeable.
        Settle(engine, """
            crypto.subtle.importKey(
                'jwk',
                { kty: 'oct', alg: 'A128GCM', k: 'AAAAAAAAAAAAAAAAAAAAAA' },
                'AES-CBC',
                false,
                ['encrypt'])
                .then(() => 'imported', e => e.name + '/' + (e.message.indexOf('A128CBC') >= 0))
            """).AsString().Should().Be("DataError/true");
    }

    [Theory]
    // The three ciphers take all four usages; AES-KW's registration names only the two wrapping ones, so
    // encrypt and decrypt are the SyntaxError step 1 raises.
    [InlineData("AES-CTR", "['encrypt', 'decrypt', 'wrapKey', 'unwrapKey']", "ok")]
    [InlineData("AES-CBC", "['encrypt', 'decrypt', 'wrapKey', 'unwrapKey']", "ok")]
    [InlineData("AES-GCM", "['encrypt', 'decrypt', 'wrapKey', 'unwrapKey']", "ok")]
    [InlineData("AES-KW", "['wrapKey', 'unwrapKey']", "ok")]
    [InlineData("AES-KW", "['encrypt']", "SyntaxError")]
    [InlineData("AES-KW", "['wrapKey', 'decrypt']", "SyntaxError")]
    [InlineData("AES-CBC", "['sign']", "SyntaxError")]
    [InlineData("AES-CTR", "['deriveBits']", "SyntaxError")]
    public void EnforcesEachAesAlgorithmsOwnUsageSet(string name, string usages, string expected)
    {
        var engine = WebEngine();

        Settle(engine, $$"""
            crypto.subtle.generateKey({ name: '{{name}}', length: 128 }, false, {{usages}})
                .then(() => 'ok', e => e.name)
            """).AsString().Should().Be(expected);

        Settle(engine, $$"""
            crypto.subtle.importKey('raw', new Uint8Array(16), '{{name}}', false, {{usages}})
                .then(() => 'ok', e => e.name)
            """).AsString().Should().Be(expected);
    }

    [Theory]
    [InlineData("AES-CTR")]
    [InlineData("AES-CBC")]
    [InlineData("AES-KW")]
    public void DerivesEachAesModeThroughDeriveKey(string name)
    {
        // All four AES algorithms register `get key length`, so any of them may be a deriveKey
        // derivedKeyType — and the key that comes out is exactly the key importKey('raw', …) would have made
        // from the derived bytes, which is what the second half of this asserts.
        var usages = string.Equals(name, "AES-KW", StringComparison.Ordinal) ? "['wrapKey']" : "['encrypt']";

        Run($$"""
            const password = await crypto.subtle.importKey('raw', ascii('password'), 'PBKDF2', false, ['deriveKey', 'deriveBits']);
            const params = { name: 'PBKDF2', salt: ascii('salt'), iterations: 1000, hash: 'SHA-256' };

            const derived = await crypto.subtle.deriveKey(params, password, { name: '{{name}}', length: 192 }, true, {{usages}});
            const bits = await crypto.subtle.deriveBits(params, password, 192);
            const imported = await crypto.subtle.importKey('raw', bits, '{{name}}', true, {{usages}});

            return derived.algorithm.name + '|' + derived.algorithm.length
                + '|' + (hex(await crypto.subtle.exportKey('raw', derived)) === hex(await crypto.subtle.exportKey('raw', imported)));
            """).AsString().Should().Be(name + "|192|true");
    }

    // ---------------------------------------------------------------------------------------------------
    // wrapKey and unwrapKey
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void WrapsARawAesKeyUnderAesKwAndUnwrapsItIntact()
    {
        // The shape AES-KW exists for, end to end: a content key wrapped for transport under a
        // key-encryption key, and recovered by the other side with the usages and extractability that side
        // chose. 16 bytes of key data wrap to 24, the extra block being RFC 3394's integrity check.
        Run("""
            const kek = await crypto.subtle.generateKey({ name: 'AES-KW', length: 256 }, false, ['wrapKey', 'unwrapKey']);
            const content = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 128 }, true, ['encrypt', 'decrypt']);

            const wrapped = await crypto.subtle.wrapKey('raw', content, kek, 'AES-KW');
            const recovered = await crypto.subtle.unwrapKey(
                'raw', wrapped, kek, 'AES-KW', { name: 'AES-GCM', length: 128 }, false, ['decrypt']);

            const iv = repeat(9, 12);
            const ciphertext = await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, content, ascii('the message'));
            const plaintext = await crypto.subtle.decrypt({ name: 'AES-GCM', iv }, recovered, ciphertext);

            // The recovered key is the same key — it decrypts what the original encrypted — and carries the
            // usages and extractability unwrapKey was told, not the ones the original had.
            return wrapped.byteLength + '|' + String.fromCharCode(...new Uint8Array(plaintext))
                + '|' + recovered.extractable + '|' + recovered.usages.join(',');
            """).AsString().Should().Be("24|the message|false|decrypt");
    }

    [Fact]
    public void WrapsAJsonWebKeyUnderAesGcmThroughTheEncryptFallback()
    {
        // AES-GCM registers `encrypt` and never `wrapKey`, so this is the second normalization of step 2
        // doing its work — and the bytes that get encrypted are the UTF-8 of JSON.stringify of exactly the
        // object exportKey('jwk', …) hands a script.
        Run("""
            // The wrapping key carries `encrypt` as well, only so that the last assertion below can compute
            // the same bytes the wrap produced through the encrypt operation the fallback routes to.
            const kek = await crypto.subtle.generateKey(
                { name: 'AES-GCM', length: 256 }, false, ['wrapKey', 'unwrapKey', 'encrypt']);
            const key = await crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, true, ['sign', 'verify']);
            const params = { name: 'AES-GCM', iv: repeat(3, 12) };

            const wrapped = await crypto.subtle.wrapKey('jwk', key, kek, params);
            const recovered = await crypto.subtle.unwrapKey(
                'jwk', wrapped, kek, params, { name: 'HMAC', hash: 'SHA-256' }, false, ['verify']);

            const signature = await crypto.subtle.sign('HMAC', key, ascii('m'));
            const verified = await crypto.subtle.verify('HMAC', recovered, signature, ascii('m'));

            const jwk = await crypto.subtle.exportKey('jwk', key);
            const asEncrypted = await crypto.subtle.encrypt(
                params, kek, Uint8Array.from(JSON.stringify(jwk), c => c.charCodeAt(0)));

            return verified + '|' + recovered.usages.join(',') + '|' + recovered.extractable
                + '|' + (hex(wrapped) === hex(asEncrypted));
            """).AsString().Should().Be("true|verify|false|true");
    }

    [Fact]
    public void PadsAJsonWebKeyToAWholeNumberOfBlocksWhenTheWrappingAlgorithmIsAesKw()
    {
        // The convention the web-platform tests compute their expectation with, in
        // WebCryptoAPI/wrapKey_unwrapKey/wrapKey_unwrapKey.https.any.js:
        //     jwk.slice(0, -1) + " ".repeat(jwk.length % 8 ? 8 - jwk.length % 8 : 0) + "}"
        // The spaces go immediately before the closing brace, where JSON's grammar allows insignificant
        // whitespace, so the padded document parses back to the very same object. The specification's own
        // note is what permits it: "implementations may choose to adapt the serialization to the constraints
        // of the wrapping algorithm".
        Run("""
            const kek = await crypto.subtle.generateKey({ name: 'AES-KW', length: 256 }, false, ['wrapKey', 'unwrapKey']);
            const key = await crypto.subtle.generateKey({ name: 'AES-CBC', length: 128 }, true, ['encrypt']);

            const wrapped = await crypto.subtle.wrapKey('jwk', key, kek, 'AES-KW');

            // What the padded document is, computed here the way the web-platform tests compute it.
            const jwk = JSON.stringify(await crypto.subtle.exportKey('jwk', key));
            const padded = jwk.slice(0, -1) + ' '.repeat(jwk.length % 8 ? 8 - jwk.length % 8 : 0) + '}';

            // The exact bytes that were wrapped, recovered by unwrapping them as a `raw` HMAC key — which
            // imports any length of key material verbatim — rather than as the JWK they are. That is what
            // pins where the spaces went: appending them after the closing brace would produce a document
            // JSON.parse still accepts, so a round trip alone would notice nothing.
            const asRaw = await crypto.subtle.unwrapKey(
                'raw', wrapped, kek, 'AES-KW', { name: 'HMAC', hash: 'SHA-256' }, true, ['sign']);
            const wrappedText = String.fromCharCode(...new Uint8Array(await crypto.subtle.exportKey('raw', asRaw)));

            // Unwrapping as the JWK it is reads the padding as the whitespace it is and gives the same key.
            const recovered = await crypto.subtle.unwrapKey(
                'jwk', wrapped, kek, 'AES-KW', { name: 'AES-CBC', length: 128 }, true, ['encrypt']);

            return (jwk.length % 8 !== 0) + '|' + (padded.length % 8) + '|' + (wrapped.byteLength === padded.length + 8)
                + '|' + (wrappedText === padded)
                + '|' + (hex(await crypto.subtle.exportKey('raw', recovered)) === hex(await crypto.subtle.exportKey('raw', key)))
                + '|' + (JSON.stringify(JSON.parse(padded)) === JSON.stringify(JSON.parse(jwk)));
            """).AsString().Should().Be("true|0|true|true|true|true");
    }

    [Fact]
    public void SerializesAJsonWebKeyInTheExportLayoutsOwnOrder()
    {
        // The member order is the export layout's, which is what makes the wrapped bytes deterministic for
        // this engine: JsonWebKeyData builds every JWK from a fixed JsObjectLayout, and a layout's order is
        // its enumeration order. So JSON.stringify of an exported JWK is stable and a wrap of one key under
        // one wrapping key is the same bytes every time.
        Run("""
            const key = await crypto.subtle.generateKey({ name: 'AES-KW', length: 128 }, true, ['wrapKey', 'unwrapKey']);
            const jwk = await crypto.subtle.exportKey('jwk', key);

            const kek = await crypto.subtle.generateKey({ name: 'AES-KW', length: 256 }, false, ['wrapKey']);
            const first = hex(await crypto.subtle.wrapKey('jwk', key, kek, 'AES-KW'));
            const second = hex(await crypto.subtle.wrapKey('jwk', key, kek, 'AES-KW'));

            return Object.keys(jwk).join(',') + '|' + (first === second);
            """).AsString().Should().Be("alg,ext,k,key_ops,kty|true");
    }

    [Fact]
    public void WrapsAnAesKeyUnderRsaOaepThroughTheEncryptFallback()
    {
        // The other half of the fallback: an asymmetric wrapping key, so the wrap and the unwrap are done
        // with different keys. RSA-OAEP registers `encrypt` and `decrypt` and never wrapKey or unwrapKey, and
        // the public half carries wrapKey where the private half carries unwrapKey — "the usage intersection
        // of usages and [ 'encrypt', 'wrapKey' ]" against "[ 'decrypt', 'unwrapKey' ]".
        Run("""
            const pair = await crypto.subtle.generateKey(
                { name: 'RSA-OAEP', modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: 'SHA-256' },
                false,
                ['encrypt', 'decrypt', 'wrapKey', 'unwrapKey']);

            const content = await crypto.subtle.generateKey({ name: 'AES-CTR', length: 256 }, true, ['encrypt', 'decrypt']);

            const wrapped = await crypto.subtle.wrapKey('raw', content, pair.publicKey, { name: 'RSA-OAEP' });
            const recovered = await crypto.subtle.unwrapKey(
                'raw', wrapped, pair.privateKey, { name: 'RSA-OAEP' }, { name: 'AES-CTR', length: 256 }, true, ['encrypt']);

            return pair.publicKey.usages.join('+') + '|' + pair.privateKey.usages.join('+')
                + '|' + wrapped.byteLength
                + '|' + (hex(await crypto.subtle.exportKey('raw', recovered)) === hex(await crypto.subtle.exportKey('raw', content)));
            """).AsString().Should().Be("encrypt+wrapKey|decrypt+unwrapKey|256|true");
    }

    [Fact]
    public void RefusesToWrapAKeyThatIsNotExtractable()
    {
        // "Because the wrapKey method effectively exports the key, only keys marked as extractable may be
        // wrapped" — step 10, and it is the same InvalidAccessError exportKey gives, made before any
        // wrapping happens.
        Run("""
            const kek = await crypto.subtle.generateKey({ name: 'AES-KW', length: 128 }, false, ['wrapKey', 'unwrapKey']);
            const sealed_ = await crypto.subtle.generateKey({ name: 'AES-CBC', length: 128 }, false, ['encrypt']);

            try {
                await crypto.subtle.wrapKey('raw', sealed_, kek, 'AES-KW');
                return 'wrapped';
            } catch (e) {
                return e.name + '/' + (e.message.indexOf('not extractable') >= 0);
            }
            """).AsString().Should().Be("InvalidAccessError/true");
    }

    [Fact]
    public void RefusesAWrappingKeyWithoutTheWrapKeyUsage()
    {
        // Steps 7 and 8, and their mirrors in unwrapKey: the wrapping key was made for this algorithm, and it
        // permits this use. Both are InvalidAccessError, and the algorithm check comes first.
        Run("""
            const wrapOnly = await crypto.subtle.generateKey({ name: 'AES-KW', length: 128 }, false, ['wrapKey']);
            const unwrapOnly = await crypto.subtle.generateKey({ name: 'AES-KW', length: 128 }, false, ['unwrapKey']);
            const cbc = await crypto.subtle.generateKey({ name: 'AES-CBC', length: 128 }, false, ['encrypt', 'decrypt']);
            const key = await crypto.subtle.generateKey({ name: 'AES-CBC', length: 128 }, true, ['encrypt']);

            const outcomes = [];
            const attempt = async fn => {
                try { await fn(); outcomes.push('ok'); } catch (e) { outcomes.push(e.name); }
            };

            // A key that may unwrap but not wrap, and the reverse.
            await attempt(() => crypto.subtle.wrapKey('raw', key, unwrapOnly, 'AES-KW'));
            const wrapped = await crypto.subtle.wrapKey('raw', key, wrapOnly, 'AES-KW');
            await attempt(() => crypto.subtle.unwrapKey(
                'raw', wrapped, wrapOnly, 'AES-KW', { name: 'AES-CBC', length: 128 }, true, ['encrypt']));

            // And a wrapping key made for another algorithm entirely.
            await attempt(() => crypto.subtle.wrapKey('raw', key, cbc, 'AES-KW'));

            return outcomes.join(',');
            """).AsString().Should().Be("InvalidAccessError,InvalidAccessError,InvalidAccessError");
    }

    [Fact]
    public void ReportsAnUnwrappedJwkThatIsNotOneThroughTheImportTaxonomy()
    {
        // Everything after the unwrapping is an import failure in the taxonomy that already exists — except
        // the one failure "parse a JWK" owns, which is bytes that are not a JSON document at all. That is the
        // shape a wrong unwrapping key produces under a cipher with no integrity check, so it is a real path
        // and not a curiosity.
        Run("""
            const kek = await crypto.subtle.generateKey({ name: 'AES-KW', length: 256 }, false, ['wrapKey', 'unwrapKey']);
            const raw = await crypto.subtle.importKey('raw', repeat(0, 16), 'AES-CBC', true, ['encrypt']);
            const hmac = await crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, true, ['sign']);

            const outcomes = [];
            const attempt = async fn => {
                try { await fn(); outcomes.push('unwrapped'); } catch (e) { outcomes.push(e.name); }
            };

            // Sixteen zero bytes are a wrappable payload and are not JSON: a DataError from "parse a JWK".
            const notJson = await crypto.subtle.wrapKey('raw', raw, kek, 'AES-KW');
            await attempt(() => crypto.subtle.unwrapKey(
                'jwk', notJson, kek, 'AES-KW', { name: 'AES-CBC', length: 128 }, true, ['encrypt']));

            // A real JWK, but of the wrong key type for the algorithm being asked for: the DataError the
            // import steps raise for a kty that is not what they name.
            const octJwk = await crypto.subtle.wrapKey('jwk', hmac, kek, 'AES-KW');
            await attempt(() => crypto.subtle.unwrapKey(
                'jwk', octJwk, kek, 'AES-KW', { name: 'ECDSA', namedCurve: 'P-256' }, true, ['verify']));

            // A usage the imported algorithm does not support is the SyntaxError its own first step raises,
            // and an empty usages list for a secret key is the SyntaxError step 14 names.
            await attempt(() => crypto.subtle.unwrapKey(
                'jwk', octJwk, kek, 'AES-KW', { name: 'HMAC', hash: 'SHA-256' }, true, ['encrypt']));
            await attempt(() => crypto.subtle.unwrapKey(
                'jwk', octJwk, kek, 'AES-KW', { name: 'HMAC', hash: 'SHA-256' }, true, []));

            // ... and the same bytes with the algorithm they actually describe.
            await attempt(() => crypto.subtle.unwrapKey(
                'jwk', octJwk, kek, 'AES-KW', { name: 'HMAC', hash: 'SHA-256' }, true, ['sign']));

            return outcomes.join(',');
            """).AsString().Should().Be("DataError,DataError,SyntaxError,SyntaxError,unwrapped");
    }

    [Fact]
    public void ParsesTheJwkBeforeItRunsTheImportStepsAtAll()
    {
        // "Parse a JWK" is step 12 and the import operation is step 13, and the last thing the parse does is
        // "If the kty field of key is not defined, then throw a DataError". That ordering is observable: HKDF
        // registers importKey but refuses the jwk format with a NotSupportedError, so if the import ran first
        // that is what a kty-less document would earn. It is a DataError instead, because the document never
        // reached the import steps.
        Run("""
            const kek = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['unwrapKey', 'encrypt']);
            const params = { name: 'AES-GCM', iv: repeat(2, 12) };
            const wrapped = await crypto.subtle.encrypt(params, kek, ascii('{"foo":1}'));

            const outcomes = [];
            const attempt = async fn => {
                try { await fn(); outcomes.push('unwrapped'); }
                catch (e) { outcomes.push(e.name + ':' + (e.message.indexOf('no kty field') >= 0)); }
            };

            await attempt(() => crypto.subtle.unwrapKey(
                'jwk', wrapped, kek, params, 'HKDF', false, ['deriveBits']));

            // ... and the same document against an algorithm that does handle jwk, which reaches the very
            // same failure at the very same step rather than the one its own kty check would have given.
            await attempt(() => crypto.subtle.unwrapKey(
                'jwk', wrapped, kek, params, { name: 'AES-CBC', length: 128 }, true, ['encrypt']));

            return outcomes.join(',');
            """).AsString().Should().Be("DataError:true,DataError:true");
    }

    [Fact]
    public void HonoursAnExtFalseJsonWebKeyOnTheWayBackIn()
    {
        // The asymmetry the specification's own note describes: wrapKey cannot produce a JWK marked
        // non-extractable, because it can only wrap an extractable key — but unwrapKey reads the member, "so
        // that wrapped non-extractable keys created elsewhere, for example by a server, can be unwrapped
        // using this API".
        Run("""
            // `encrypt` is here only so the test can play the part of the server that produced the bytes.
            const kek = await crypto.subtle.generateKey(
                { name: 'AES-GCM', length: 256 }, false, ['unwrapKey', 'encrypt']);
            const params = { name: 'AES-GCM', iv: repeat(1, 12) };

            // A JWK a server might have produced, with ext: false, encrypted with the wrapping key.
            const jwk = JSON.stringify({ kty: 'oct', alg: 'A128CBC', k: 'AAAAAAAAAAAAAAAAAAAAAA', ext: false });
            const wrapped = await crypto.subtle.encrypt(params, kek, Uint8Array.from(jwk, c => c.charCodeAt(0)));

            const outcomes = [];
            try {
                const key = await crypto.subtle.unwrapKey(
                    'jwk', wrapped, kek, params, { name: 'AES-CBC', length: 128 }, false, ['encrypt']);
                outcomes.push('extractable=' + key.extractable);
            } catch (e) { outcomes.push(e.name); }

            // Asking for it as an extractable key is the DataError the import steps raise.
            try {
                await crypto.subtle.unwrapKey(
                    'jwk', wrapped, kek, params, { name: 'AES-CBC', length: 128 }, true, ['encrypt']);
                outcomes.push('unwrapped');
            } catch (e) { outcomes.push(e.name); }

            return outcomes.join(',');
            """).AsString().Should().Be("extractable=false,DataError");
    }

    [Fact]
    public void NormalizesForWrapKeyFirstAndForTheCipherOperationSecond()
    {
        // Step 2 of both methods: normalize for wrapKey and, "if an error occurred", for encrypt instead. The
        // two registries are disjoint, so which route a name takes is decided entirely by its own
        // registration — and a name in neither is reported against the *encrypt* registry, because that is
        // the normalization that ran last.
        Run("""
            const kw = await crypto.subtle.generateKey({ name: 'AES-KW', length: 128 }, false, ['wrapKey', 'unwrapKey']);
            const gcm = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 128 }, false, ['wrapKey', 'unwrapKey']);
            const key = await crypto.subtle.generateKey({ name: 'AES-CBC', length: 128 }, true, ['encrypt']);

            const outcomes = [];
            const attempt = async fn => {
                try { await fn(); outcomes.push('ok'); }
                catch (e) { outcomes.push(e.name + (e.message.indexOf('encrypt operation') >= 0 ? '/encrypt' : '')); }
            };

            // AES-KW takes the wrap route; AES-GCM the encrypt route, which needs its own parameters.
            await attempt(() => crypto.subtle.wrapKey('raw', key, kw, 'AES-KW'));
            await attempt(() => crypto.subtle.wrapKey('raw', key, gcm, { name: 'AES-GCM', iv: repeat(0, 12) }));

            // An unregistered name fails against the encrypt registry, which is the second normalization.
            await attempt(() => crypto.subtle.wrapKey('raw', key, kw, 'AES-XTS'));

            // And AES-KW itself is not in the encrypt registry at all, which is what makes the two routes
            // disjoint rather than merely ordered. That one names the encrypt registry for the ordinary
            // reason — it *is* an encrypt call — where the row above it names it because of the fallback.
            await attempt(() => crypto.subtle.encrypt({ name: 'AES-KW' }, kw, new Uint8Array(16)));

            return outcomes.join(',');
            """).AsString().Should().Be("ok,ok,NotSupportedError/encrypt,NotSupportedError/encrypt");
    }

    // ---------------------------------------------------------------------------------------------------
    // Feature detection: twelve of twelve
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void ExposesAllTwelveOperationsWithTheirIdlNameAndArity()
    {
        var engine = WebEngine();

        // WebIDL's length counts the required arguments only: wrapKey declares four and unwrapKey seven,
        // which are all of theirs. deriveBits stays 2 because its `length` is optional and nullable.
        engine.Evaluate("""
            ['digest', 'sign', 'verify', 'encrypt', 'decrypt', 'generateKey', 'importKey', 'exportKey',
             'deriveBits', 'deriveKey', 'wrapKey', 'unwrapKey']
                .map(name => name + ':' + typeof crypto.subtle[name] + ':' + crypto.subtle[name].length
                    + ':' + crypto.subtle[name].name).join(',')
            """).AsString().Should().Be(
                "digest:function:2:digest,sign:function:3:sign,verify:function:4:verify,"
                + "encrypt:function:3:encrypt,decrypt:function:3:decrypt,generateKey:function:3:generateKey,"
                + "importKey:function:5:importKey,exportKey:function:2:exportKey,"
                + "deriveBits:function:2:deriveBits,deriveKey:function:5:deriveKey,"
                + "wrapKey:function:4:wrapKey,unwrapKey:function:7:unwrapKey");

        // Properties of SubtleCrypto.prototype with an ECMAScript built-in method's attributes, as every other
        // operation is.
        engine.Evaluate("JSON.stringify(Object.keys(crypto.subtle))").AsString().Should().Be("[]");

        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(SubtleCrypto.prototype, 'wrapKey')").AsObject();
        descriptor.Get("writable").AsBoolean().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeFalse();
    }

    [Theory]
    // AES-KW registers wrapKey, unwrapKey, generateKey, importKey, exportKey and get key length — and nothing
    // else, so every cipher and signature operation is a NotSupportedError.
    [InlineData("sign", "'AES-KW'")]
    [InlineData("verify", "'AES-KW'")]
    [InlineData("encrypt", "'AES-KW'")]
    [InlineData("decrypt", "'AES-KW'")]
    [InlineData("deriveBits", "'AES-KW'")]
    // ... and the three ciphers are the reverse: they encrypt and decrypt and never wrap.
    [InlineData("sign", "'AES-CBC'")]
    [InlineData("verify", "'AES-CTR'")]
    [InlineData("deriveBits", "'AES-CBC'")]
    public void RefusesAnOperationTheAesAlgorithmIsNotRegisteredFor(string operation, string algorithm)
    {
        var engine = WebEngine();

        var call = operation switch
        {
            "sign" => $"crypto.subtle.sign({algorithm}, key, new Uint8Array(0))",
            "verify" => $"crypto.subtle.verify({algorithm}, key, new Uint8Array(0), new Uint8Array(0))",
            "encrypt" => $"crypto.subtle.encrypt({algorithm}, key, new Uint8Array(16))",
            "decrypt" => $"crypto.subtle.decrypt({algorithm}, key, new Uint8Array(16))",
            _ => $"crypto.subtle.deriveBits({algorithm}, key, 128)",
        };

        Settle(engine, $$"""
            crypto.subtle.generateKey({ name: 'AES-KW', length: 128 }, false, ['wrapKey', 'unwrapKey'])
                .then(key => {{call}})
                .then(() => 'succeeded', e => e.name + '/' + e.code)
            """).AsString().Should().Be("NotSupportedError/9");
    }

    [Theory]
    // Only AES-KW is registered for wrapKey and unwrapKey, so a name in neither that registry nor the
    // encrypt/decrypt one is a NotSupportedError — HMAC and the signature algorithms cannot wrap.
    [InlineData("'HMAC'")]
    [InlineData("'RSASSA-PKCS1-v1_5'")]
    [InlineData("'ECDSA'")]
    [InlineData("'PBKDF2'")]
    [InlineData("'AES-XTS'")]
    public void RefusesAWrappingAlgorithmThatIsInNeitherRegistry(string algorithm)
    {
        var engine = WebEngine();

        Settle(engine, $$"""
            (async () => {
                const kek = await crypto.subtle.generateKey({ name: 'AES-KW', length: 128 }, false, ['wrapKey', 'unwrapKey']);
                const key = await crypto.subtle.generateKey({ name: 'AES-CBC', length: 128 }, true, ['encrypt']);
                return crypto.subtle.wrapKey('raw', key, kek, {{algorithm}}).then(() => 'wrapped', e => e.name);
            })()
            """).AsString().Should().Be("NotSupportedError");
    }

    // ---------------------------------------------------------------------------------------------------
    // Nothing erupts
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void NoAesOperationEverThrowsSynchronouslyOrLeaksACryptographicException()
    {
        var engine = WebEngine();

        // A promise-returning WebIDL operation reports every failure as a rejection, and the failures the
        // platform's own cryptography raises are no exception: a CryptographicException reaching the host
        // would be a CLR exception erupting out of a promise-returning API.
        engine.Evaluate("""
            (() => {
                const calls = [
                    () => crypto.subtle.importKey('raw', new Uint8Array(7), 'AES-CBC', false, ['encrypt']),
                    () => crypto.subtle.importKey('jwk', { kty: 'oct', k: '' }, 'AES-KW', false, ['wrapKey']),
                    () => crypto.subtle.generateKey({ name: 'AES-CTR', length: 64 }, false, ['encrypt']),
                    () => crypto.subtle.wrapKey('raw', 42, 42, 'AES-KW'),
                    () => crypto.subtle.unwrapKey('raw', new Uint8Array(24), 42, 'AES-KW', 'AES-CBC', true, ['encrypt']),
                ];

                return calls.map(call => {
                    const promise = call();
                    return (promise instanceof Promise) + ':' + typeof promise.then;
                }).join(',');
            })()
            """).AsString().Should().Be(
                "true:function,true:function,true:function,true:function,true:function");

        // ... and each of them settles as a rejection of the kind the specification names, with nothing of
        // the CLR in it. The last two are WebIDL conversion failures, which are TypeErrors and not
        // DOMExceptions — the brand check of a CryptoKey argument runs before any step of the method.
        Settle(engine, """
            Promise.all([
                crypto.subtle.importKey('raw', new Uint8Array(7), 'AES-CBC', false, ['encrypt']).catch(e => e.name),
                crypto.subtle.importKey('jwk', { kty: 'oct', k: '' }, 'AES-KW', false, ['wrapKey']).catch(e => e.name),
                crypto.subtle.generateKey({ name: 'AES-CTR', length: 64 }, false, ['encrypt']).catch(e => e.name),
                crypto.subtle.wrapKey('raw', 42, 42, 'AES-KW').catch(e => e.constructor.name),
                crypto.subtle.unwrapKey('raw', new Uint8Array(24), 42, 'AES-KW', 'AES-CBC', true, ['encrypt']).catch(e => e.constructor.name),
            ]).then(names => names.join(','))
            """).AsString().Should().Be("DataError,DataError,OperationError,TypeError,TypeError");
    }

    [Fact]
    public void RunsEveryArgumentConversionBeforeAnyStepOfTheMethod()
    {
        var engine = WebEngine();

        // WebIDL converts the arguments in order before the body runs, so the KeyFormat enumeration of
        // parameter 1 outranks the CryptoKey brand check of parameter 2, which outranks the algorithm
        // normalization of step 2 — the same order every other operation here has.
        Settle(engine, """
            (async () => {
                const kek = await crypto.subtle.generateKey({ name: 'AES-KW', length: 128 }, false, ['wrapKey']);
                const key = await crypto.subtle.generateKey({ name: 'AES-CBC', length: 128 }, true, ['encrypt']);

                const outcomes = [];
                const attempt = async fn => {
                    try { await fn(); outcomes.push('ok'); }
                    catch (e) { outcomes.push(e.name === undefined ? e.constructor.name : e.name); }
                };

                await attempt(() => crypto.subtle.wrapKey('pem', key, kek, 'nonsense'));
                await attempt(() => crypto.subtle.wrapKey('raw', {}, kek, 'nonsense'));
                await attempt(() => crypto.subtle.wrapKey('raw', key, kek, 'nonsense'));

                await attempt(() => crypto.subtle.unwrapKey('pem', new Uint8Array(24), kek, 'AES-KW', 'AES-CBC', true, ['encrypt']));
                await attempt(() => crypto.subtle.unwrapKey('raw', 'not a buffer', kek, 'AES-KW', 'AES-CBC', true, ['encrypt']));
                await attempt(() => crypto.subtle.unwrapKey('raw', new Uint8Array(24), kek, 'AES-KW', 'nonsense', true, ['encrypt']));

                return outcomes.join(',');
            })()
            """).AsString().Should().Be(
                "TypeError,TypeError,NotSupportedError,TypeError,TypeError,NotSupportedError");
    }
}
#endif
