#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The keyed half of <c>crypto.subtle</c> — https://w3c.github.io/webcrypto/#subtlecrypto-interface:
/// <c>CryptoKey</c>, <c>generateKey</c>, <c>importKey</c>, <c>exportKey</c>, <c>sign</c>, <c>verify</c>,
/// <c>encrypt</c> and <c>decrypt</c> over HMAC and AES-GCM.
/// </summary>
/// <remarks>
/// <para>
/// The cryptography itself is checked against published vectors, which is the only way to check it: HMAC
/// against RFC 2202 (SHA-1) and RFC 4231 (SHA-256, SHA-384, SHA-512), and AES-GCM against the NIST CAVP
/// vectors, whose <c>ciphertext || tag</c> concatenation with a 96-bit IV is exactly the byte layout the Web
/// Cryptography API specifies. A round trip through this engine's own code would prove only that it agrees
/// with itself.
/// </para>
/// <para>
/// Everything else is about the shape of the operations: which failure each rejection is, in which order the
/// failures come, what a key does and does not expose, and the usage matrix.
/// </para>
/// </remarks>
public class SubtleCryptoKeyTests
{
    /// <summary>
    /// Helpers every script here shares. Bytes are built from hex or from character codes rather than
    /// through <c>TextEncoder</c>, so that the crypto feature is the only one the engine carries and nothing
    /// can be passing because of a neighbour.
    /// </summary>
    private const string Prelude = """
        const hex = b => Array.from(new Uint8Array(b)).map(x => x.toString(16).padStart(2, '0')).join('');
        const bytes = h => Uint8Array.from(h.match(/../g) || [], x => parseInt(x, 16));
        const ascii = s => Uint8Array.from(s, c => c.charCodeAt(0));
        const repeat = (byte, count) => new Uint8Array(count).fill(byte);
        """;

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
    // HMAC: the published vectors
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    // https://www.rfc-editor.org/rfc/rfc2202 section 3, test cases 1 to 4.
    [InlineData("SHA-1", "0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b", "4869205468657265", "b617318655057264e28bc0b6fb378c8ef146be00")]
    [InlineData("SHA-1", "4a656665", "7768617420646f2079612077616e7420666f72206e6f7468696e673f", "effcdf6ae5eb2fa2d27416d5f184df9c259a7c79")]
    [InlineData("SHA-1", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd", "125d7342b9ac11cd91a39af48aa17b4f63f175d3")]
    [InlineData("SHA-1", "0102030405060708090a0b0c0d0e0f10111213141516171819", "cdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcd", "4c9007f4026250c6bc8414f9bf50c86c2d7235da")]
    // https://www.rfc-editor.org/rfc/rfc4231 section 4, test cases 1 to 4.
    [InlineData("SHA-256", "0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b", "4869205468657265", "b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7")]
    [InlineData("SHA-256", "4a656665", "7768617420646f2079612077616e7420666f72206e6f7468696e673f", "5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843")]
    [InlineData("SHA-256", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd", "773ea91e36800e46854db8ebd09181a72959098b3ef8c122d9635514ced565fe")]
    [InlineData("SHA-256", "0102030405060708090a0b0c0d0e0f10111213141516171819", "cdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcd", "82558a389a443c0ea4cc819899f2083a85f0faa3e578f8077a2e3ff46729665b")]
    [InlineData("SHA-384", "0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b", "4869205468657265", "afd03944d84895626b0825f4ab46907f15f9dadbe4101ec682aa034c7cebc59cfaea9ea9076ede7f4af152e8b2fa9cb6")]
    [InlineData("SHA-384", "4a656665", "7768617420646f2079612077616e7420666f72206e6f7468696e673f", "af45d2e376484031617f78d2b58a6b1b9c7ef464f5a01b47e42ec3736322445e8e2240ca5e69e2c78b3239ecfab21649")]
    [InlineData("SHA-384", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd", "88062608d3e6ad8a0aa2ace014c8a86f0aa635d947ac9febe83ef4e55966144b2a5ab39dc13814b94e3ab6e101a34f27")]
    [InlineData("SHA-384", "0102030405060708090a0b0c0d0e0f10111213141516171819", "cdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcd", "3e8a69b7783c25851933ab6290af6ca77a9981480850009cc5577c6e1f573b4e6801dd23c4a7d679ccf8a386c674cffb")]
    [InlineData("SHA-512", "0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b", "4869205468657265", "87aa7cdea5ef619d4ff0b4241a1d6cb02379f4e2ce4ec2787ad0b30545e17cdedaa833b7d6b8a702038b274eaea3f4e4be9d914eeb61f1702e696c203a126854")]
    [InlineData("SHA-512", "4a656665", "7768617420646f2079612077616e7420666f72206e6f7468696e673f", "164b7a7bfcf819e2e395fbe73b56e0a387bd64222e831fd610270cd7ea2505549758bf75c05a994a6d034f65f8f0e6fdcaeab1a34d4a6b4b636e070a38bce737")]
    [InlineData("SHA-512", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd", "fa73b0089d56a284efb0f0756c890be9b1b5dbdd8ee81a3655f83e33b2279d39bf3e848279a722c806b485a47e67c807b946a337bee8942674278859e13292fb")]
    [InlineData("SHA-512", "0102030405060708090a0b0c0d0e0f10111213141516171819", "cdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcd", "b0ba465637458c6990e5a8c5f61d4af7e576d97ff94b872de76f8050361ee3dba91ca5c11aa25eb4d679275cc5788063a5f19741120c4f2de2adebeb10a298dd")]
    public void SignsThePublishedHmacVectors(string hash, string keyHex, string dataHex, string expected)
    {
        Run($$"""
            const key = await crypto.subtle.importKey('raw', bytes('{{keyHex}}'), { name: 'HMAC', hash: '{{hash}}' }, false, ['sign']);
            return hex(await crypto.subtle.sign('HMAC', key, bytes('{{dataHex}}')));
            """).AsString().Should().Be(expected);
    }

    [Fact]
    public void VerifiesWhatItSignedAndRefusesAnythingElse()
    {
        Run("""
            const key = await crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, false, ['sign', 'verify']);
            const message = ascii('the message');
            const signature = await crypto.subtle.sign('HMAC', key, message);

            const results = [
                await crypto.subtle.verify('HMAC', key, signature, message),
                await crypto.subtle.verify('HMAC', key, signature, ascii('the messagf')),
                await crypto.subtle.verify('HMAC', key, new Uint8Array(32), message),
                await crypto.subtle.verify('HMAC', key, new Uint8Array(0), message),
                await crypto.subtle.verify('HMAC', key, new Uint8Array(33), message),
            ];
            return results.join(',');
            """).AsString().Should().Be("true,false,false,false,false");
    }

    [Fact]
    public void TwoEnginesEachMintTheirOwnKeys()
    {
        // A CryptoKey holds a hard reference to the realm that built it, and the brand check is by CLR type,
        // so a key can cross engines only if a host hands it across — which is unsupported for every JsValue.
        // What is pinned here is the ordinary case: two engines each mint their own and neither sees the
        // other's.
        var options = new Options().UseWebApis(WebApiFeatures.Crypto);
        var first = new Engine(options);
        var second = new Engine(options);

        Run("""
            const key = await crypto.subtle.importKey('raw', repeat(0x0b, 20), { name: 'HMAC', hash: 'SHA-1' }, false, ['sign']);
            return hex(await crypto.subtle.sign('HMAC', key, ascii('Hi There')));
            """, first).AsString().Should().Be("b617318655057264e28bc0b6fb378c8ef146be00");

        Run("""
            const key = await crypto.subtle.importKey('raw', repeat(0x0b, 20), { name: 'HMAC', hash: 'SHA-1' }, false, ['sign']);
            return hex(await crypto.subtle.sign('HMAC', key, ascii('Hi There')));
            """, second).AsString().Should().Be("b617318655057264e28bc0b6fb378c8ef146be00");
    }

    // ---------------------------------------------------------------------------------------------------
    // AES-GCM: the published vectors
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    // The NIST CAVP AES-GCM vectors (gcmEncryptExtIV*.rsp), as vendored by Go's crypto/cipher tests. Each
    // `expected` is the specification's own `ciphertext || tag` layout with a 128-bit tag, so it is what
    // encrypt must answer with byte for byte. At least two per key length, plus two carrying additionalData.
    [InlineData("11754cd72aec309bf52f7687212e8957", "3c819d9a9bed087615030b65", "", "", "250327c674aaf477aef2675748cf6971")]
    [InlineData("7fddb57453c241d03efbed3ac44e371c", "ee283a3fc75575e33efd4887", "d5de42b461646c255c87bd2962d3b9a2", "", "2ccda4a5415cb91e135c2a0f78c9b2fdb36d1df9b9d5e596f83e8b7f52971cb3")]
    [InlineData("fbe3467cc254f81be8e78d765a2e6333", "c6697351ff4aec29cdbaabf2", "", "67", "3659cdc25288bf499ac736c03bfc1159")]
    [InlineData("fe47fcce5fc32665d2ae399e4eec72ba", "5adb9609dbaeb58cbd6e7275", "7c0e88c88899a779228465074797cd4c2e1498d259b54390b85e3eef1c02df60e743f1b840382c4bccaf3bafb4ca8429bea063", "88319d6e1d3ffa5f987199166c8a9b56c2aeba5a", "98f4826f05a265e6dd2be82db241c0fbbbf9ffb1c173aa83964b7cf5393043736365253ddbc5db8778371495da76d269e5db3e291ef1982e4defedaa2249f898556b47")]
    [InlineData("e2e001a36c60d2bf40d69ff5b2b1161ea218db263be16a4e", "3c819d9a9bed087615030b65", "", "", "c7b8da1fe2e3dccc4071ba92a0a57ba8")]
    [InlineData("feffe9928665731c6d6a8f9467308308feffe9928665731c", "54cc7dc2c37ec006bcc6d1da", "007c5e5b3e59df24a7c355584fc1518d", "", "7bd53594c28b6c6596feb240199cad4c9badb907fd65bde541b8df3bd444d3a8")]
    [InlineData("5394e890d37ba55ec9d5f327f15680f6a63ef5279c79331643ad0af6d2623525", "3c819d9a9bed087615030b65", "", "", "d9b260d4bc4630733ffb642f5ce45726")]
    [InlineData("feffe9928665731c6d6a8f9467308308feffe9928665731c6d6a8f9467308308", "54cc7dc2c37ec006bcc6d1da", "007c5e5b3e59df24a7c355584fc1518d", "", "d50b9e252b70945d4240d351677eb10f937cdaef6f2822b6a3191654ba41b197")]
    public void EncryptsAndDecryptsThePublishedAesGcmVectors(string keyHex, string ivHex, string plaintextHex, string aadHex, string expected)
    {
        Run($$"""
            const key = await crypto.subtle.importKey('raw', bytes('{{keyHex}}'), 'AES-GCM', false, ['encrypt', 'decrypt']);
            const params = { name: 'AES-GCM', iv: bytes('{{ivHex}}'), additionalData: bytes('{{aadHex}}') };

            const ciphertext = await crypto.subtle.encrypt(params, key, bytes('{{plaintextHex}}'));
            const roundTripped = await crypto.subtle.decrypt(params, key, ciphertext);

            return hex(ciphertext) + '|' + hex(roundTripped);
            """).AsString().Should().Be(expected + "|" + plaintextHex);
    }

    /// <summary>
    /// Whether this platform's own AES-GCM produces a tag of the given bit length. OpenSSL and CNG take
    /// 96..128 bits; Apple's CryptoKit takes 128 and nothing else, which is why the two tests below ask
    /// instead of assuming.
    /// </summary>
    private static bool PlatformSupportsTagBits(int bits)
    {
        var sizes = System.Security.Cryptography.AesGcm.TagByteSizes;
        var tagBytes = bits / 8;
        if (tagBytes < sizes.MinSize || tagBytes > sizes.MaxSize)
        {
            return false;
        }

        return sizes.SkipSize == 0 || (tagBytes - sizes.MinSize) % sizes.SkipSize == 0;
    }

    [Fact]
    public void ATruncatedTagIsThePrefixOfTheFullOne()
    {
        Assert.SkipUnless(PlatformSupportsTagBits(96), "this platform's AES-GCM produces only 128-bit tags");

        // NIST SP 800-38D defines a t-bit tag as MSB_t of the 128-bit one, and the ciphertext does not depend
        // on t at all — so the whole output for tagLength 96 is the first twelve bytes shorter. Checking it
        // against the 128-bit vector proves both the truncation and the ciphertext || tag layout.
        Run("""
            const key = await crypto.subtle.importKey('raw', bytes('7fddb57453c241d03efbed3ac44e371c'), 'AES-GCM', false, ['encrypt', 'decrypt']);
            const iv = bytes('ee283a3fc75575e33efd4887');
            const plaintext = bytes('d5de42b461646c255c87bd2962d3b9a2');

            const full = hex(await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, key, plaintext));
            const short_ = await crypto.subtle.encrypt({ name: 'AES-GCM', iv, tagLength: 96 }, key, plaintext);

            const decrypted = await crypto.subtle.decrypt({ name: 'AES-GCM', iv, tagLength: 96 }, key, short_);
            return [full, hex(short_), hex(decrypted)].join('|');
            """).AsString().Should().Be(
                "2ccda4a5415cb91e135c2a0f78c9b2fdb36d1df9b9d5e596f83e8b7f52971cb3|"
                + "2ccda4a5415cb91e135c2a0f78c9b2fdb36d1df9b9d5e596f83e8b7f|"
                + "d5de42b461646c255c87bd2962d3b9a2");
    }

    [Theory]
    [InlineData(96)]
    [InlineData(104)]
    [InlineData(112)]
    [InlineData(120)]
    [InlineData(128)]
    public void RoundTripsEveryTagLengthThePlatformSupports(int tagLength)
    {
        if (!PlatformSupportsTagBits(tagLength))
        {
            // A spec-valid length the platform cannot produce is the OperationError the platform gate maps
            // it to — never a raw ArgumentException erupting out of a promise-returning operation.
            Run($$"""
                const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 128 }, false, ['encrypt', 'decrypt']);
                try
                {
                    await crypto.subtle.encrypt({ name: 'AES-GCM', iv: repeat(9, 12), tagLength: {{tagLength}} }, key, ascii('hello'));
                    return 'encrypted';
                }
                catch (e)
                {
                    return e.name;
                }
                """).AsString().Should().Be("OperationError");
            return;
        }

        Run($$"""
            const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 128 }, false, ['encrypt', 'decrypt']);
            const params = { name: 'AES-GCM', iv: repeat(9, 12), tagLength: {{tagLength}} };
            const ciphertext = await crypto.subtle.encrypt(params, key, ascii('hello'));
            const plaintext = await crypto.subtle.decrypt(params, key, ciphertext);
            return (ciphertext.byteLength - 5) * 8 + ':' + String.fromCharCode(...new Uint8Array(plaintext));
            """).AsString().Should().Be(tagLength + ":hello");
    }

    [Fact]
    public void EveryWayTheDecryptionCanFailIsTheSameOperationError()
    {
        // The one thing an authenticated cipher promises is that a ciphertext, a tag, an iv or an
        // additionalData that does not belong are one answer rather than four. Nothing here may distinguish
        // them — not the error name, and not the message.
        Run("""
            const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt']);
            const iv = repeat(1, 12);
            const params = { name: 'AES-GCM', iv, additionalData: ascii('context') };
            const ciphertext = new Uint8Array(await crypto.subtle.encrypt(params, key, ascii('hello')));

            const flipped = ciphertext.slice();
            flipped[0] ^= 1;
            const tagFlipped = ciphertext.slice();
            tagFlipped[tagFlipped.length - 1] ^= 1;

            const attempts = [
                { name: 'AES-GCM', iv, additionalData: ascii('context') },  // the honest one
                { name: 'AES-GCM', iv },                                     // no additionalData
                { name: 'AES-GCM', iv: repeat(2, 12), additionalData: ascii('context') },
            ];

            const outcomes = [];
            for (const attempt of attempts) {
                try {
                    await crypto.subtle.decrypt(attempt, key, ciphertext);
                    outcomes.push('decrypted');
                } catch (e) {
                    outcomes.push(e.name + '/' + e.message);
                }
            }
            for (const data of [flipped, tagFlipped, ciphertext.slice(0, 4)]) {
                try {
                    await crypto.subtle.decrypt(params, key, data);
                    outcomes.push('decrypted');
                } catch (e) {
                    outcomes.push(e.name + '/' + e.message);
                }
            }
            return outcomes.join('\n');
            """).AsString().Should().Be(string.Join('\n',
                "decrypted",
                "OperationError/Failed to execute 'decrypt' on 'SubtleCrypto': the data could not be decrypted.",
                "OperationError/Failed to execute 'decrypt' on 'SubtleCrypto': the data could not be decrypted.",
                "OperationError/Failed to execute 'decrypt' on 'SubtleCrypto': the data could not be decrypted.",
                "OperationError/Failed to execute 'decrypt' on 'SubtleCrypto': the data could not be decrypted.",
                "OperationError/Failed to execute 'decrypt' on 'SubtleCrypto': the ciphertext is 32 bits long, which is shorter than the 128-bit authentication tag it must end with."));
    }

    [Theory]
    [InlineData(128)]
    [InlineData(192)]
    [InlineData(256)]
    public void GeneratesAnAesKeyOfEachLength(int length)
    {
        Run($$"""
            const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: {{length}} }, true, ['encrypt', 'decrypt']);
            const raw = await crypto.subtle.exportKey('raw', key);
            return [key.algorithm.name, key.algorithm.length, raw.byteLength * 8, key.type].join('|');
            """).AsString().Should().Be("AES-GCM|" + length + "|" + length + "|secret");
    }

    // ---------------------------------------------------------------------------------------------------
    // CryptoKey
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void AKeyExposesTheFourAttributesAndNothingElse()
    {
        Run("""
            const key = await crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-384' }, true, ['verify', 'sign']);

            return [
                key.type,
                key.extractable,
                // The usages are the "normalized value" of what was asked for: the recognized values in the
                // specification's own order, whatever order they arrived in.
                key.usages.join(','),
                JSON.stringify(key.algorithm),
                Object.keys(key.algorithm).join(','),
                // A platform object's state lives in internal slots, so the key itself has no own properties.
                JSON.stringify(Object.getOwnPropertyNames(key)),
                Object.prototype.toString.call(key),
                key instanceof CryptoKey,
            ].join('|');
            """).AsString().Should().Be(
                "secret|true|sign,verify|{\"name\":\"HMAC\",\"hash\":{\"name\":\"SHA-384\"},\"length\":1024}|name,hash,length|[]|[object CryptoKey]|true");
    }

    [Fact]
    public void DuplicateUsagesCollapseAndOrderIsCanonical()
    {
        // "The normalized value of a usages list" is the usage intersection with the recognized values, and
        // an intersection is a set: `['verify','sign','verify']` normalizes to `['sign','verify']`.
        Run("""
            const key = await crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, false, ['verify', 'sign', 'verify']);
            return key.usages.join(',');
            """).AsString().Should().Be("sign,verify");
    }

    [Fact]
    public void TheAlgorithmAndUsagesObjectsAreCachedAndCannotChangeTheKey()
    {
        // https://w3c.github.io/webcrypto/#dfn-cached-ecmascript-object — one object per slot, built on the
        // first read. It is an ordinary object, so a script may write to it; nothing the engine decides is
        // read back out of it, which is the half that matters.
        Run("""
            const key = await crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);

            const stable = key.algorithm === key.algorithm && key.usages === key.usages;

            key.algorithm.name = 'AES-GCM';
            key.usages.push('verify');

            let afterwards;
            try {
                await crypto.subtle.verify('HMAC', key, new Uint8Array(32), ascii('x'));
                afterwards = 'verified';
            } catch (e) {
                afterwards = e.name;
            }

            return stable + '|' + afterwards;
            """).AsString().Should().Be("true|InvalidAccessError");
    }

    [Fact]
    public void TheInterfaceObjectRefusesToConstructAnything()
    {
        var engine = WebEngine();

        // CryptoKey declares no constructor operation, so the interface object exists, is a function, and
        // constructs nothing — https://webidl.spec.whatwg.org/#es-interface-call.
        engine.Evaluate("typeof CryptoKey").AsString().Should().Be("function");
        engine.Evaluate("(() => { try { new CryptoKey(); } catch (e) { return e.constructor.name + ': ' + e.message; } })()")
            .AsString().Should().Be("TypeError: Illegal constructor");
        engine.Evaluate("(() => { try { CryptoKey(); } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");

        engine.Evaluate("CryptoKey.name").AsString().Should().Be("CryptoKey");
        engine.Evaluate("CryptoKey.length").AsNumber().Should().Be(0);
        engine.Evaluate("CryptoKey.prototype.constructor === CryptoKey").AsBoolean().Should().BeTrue();
        engine.Evaluate("CryptoKey.prototype[Symbol.toStringTag]").AsString().Should().Be("CryptoKey");
    }

    [Fact]
    public void EveryAttributeBrandChecksItsReceiver()
    {
        var engine = WebEngine();

        // The attributes live on the prototype, so they can be extracted — and then they must refuse anything
        // that is not a key, including CryptoKey.prototype itself, which is not one.
        engine.Evaluate("""
            (() => {
                const names = ['type', 'extractable', 'algorithm', 'usages'];
                return names.map(name => {
                    const getter = Object.getOwnPropertyDescriptor(CryptoKey.prototype, name).get;
                    try { getter.call({}); } catch (e) { return e.constructor.name; }
                    return 'no error';
                }).join(',');
            })()
            """).AsString().Should().Be("TypeError,TypeError,TypeError,TypeError");

        engine.Evaluate("(() => { try { return CryptoKey.prototype.type; } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");
    }

    [Fact]
    public void AKeyIsBuiltInTheRealmsOwnPrototype()
    {
        Run("""
            const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 128 }, false, ['encrypt']);
            return [
                Object.getPrototypeOf(key) === CryptoKey.prototype,
                Array.isArray(key.usages),
                Object.getPrototypeOf(key.usages) === Array.prototype,
                Object.getPrototypeOf(key.algorithm) === Object.prototype,
            ].join('|');
            """).AsString().Should().Be("true|true|true|true");
    }

    // ---------------------------------------------------------------------------------------------------
    // Key material never escapes except through exportKey
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void ANonExtractableKeyCannotBeExportedInAnyFormat()
    {
        Run("""
            const key = await crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
            const outcomes = [];
            for (const format of ['raw', 'jwk']) {
                try {
                    await crypto.subtle.exportKey(format, key);
                    outcomes.push('exported');
                } catch (e) {
                    outcomes.push(e.name + '/' + (e instanceof DOMException));
                }
            }
            // ... and it still signs: extractable is about the material, not about the key's use.
            outcomes.push((await crypto.subtle.sign('HMAC', key, ascii('x'))).byteLength);
            return outcomes.join('|');
            """).AsString().Should().Be("InvalidAccessError/true|InvalidAccessError/true|32");
    }

    [Fact]
    public void WhatWasExportedIsACopyOfTheKeyAndNotTheKey()
    {
        // The exported buffer crosses into script, where it is mutable. If it aliased the key material, a
        // script could change what the key signs with — from the outside, without the key ever being told.
        Run("""
            const key = await crypto.subtle.importKey('raw', repeat(0x0b, 20), { name: 'HMAC', hash: 'SHA-1' }, true, ['sign']);

            const first = await crypto.subtle.exportKey('raw', key);
            new Uint8Array(first).fill(0xff);

            const second = await crypto.subtle.exportKey('raw', key);
            const signature = await crypto.subtle.sign('HMAC', key, ascii('Hi There'));

            return [hex(second), hex(signature), first !== second].join('|');
            """).AsString().Should().Be(
                "0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b|b617318655057264e28bc0b6fb378c8ef146be00|true");
    }

    [Fact]
    public void RoundTripsAGeneratedKeyThroughEveryFormat()
    {
        // The shape a script actually writes: generate, export, import somewhere else, and get the same MAC.
        Run("""
            const original = await crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-512', length: 256 }, true, ['sign', 'verify']);
            const message = ascii('round trip');
            const expected = hex(await crypto.subtle.sign('HMAC', original, message));

            const raw = await crypto.subtle.exportKey('raw', original);
            const fromRaw = await crypto.subtle.importKey('raw', raw, { name: 'HMAC', hash: 'SHA-512' }, false, ['sign']);

            const jwk = await crypto.subtle.exportKey('jwk', original);
            const fromJwk = await crypto.subtle.importKey('jwk', jwk, { name: 'HMAC', hash: 'SHA-512' }, false, ['verify']);

            return [
                hex(await crypto.subtle.sign('HMAC', fromRaw, message)) === expected,
                await crypto.subtle.verify('HMAC', fromJwk, bytes(expected), message),
                raw.byteLength * 8,
                fromRaw.algorithm.length,
            ].join('|');
            """).AsString().Should().Be("true|true|256|256");
    }

    [Fact]
    public void RoundTripsAnAesKeyThroughJwkAndDecryptsWithIt()
    {
        Run("""
            const original = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 192 }, true, ['encrypt', 'decrypt']);
            const iv = repeat(3, 12);
            const ciphertext = await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, original, ascii('secret'));

            const jwk = await crypto.subtle.exportKey('jwk', original);
            const imported = await crypto.subtle.importKey('jwk', jwk, 'AES-GCM', false, ['decrypt']);

            const plaintext = await crypto.subtle.decrypt({ name: 'AES-GCM', iv }, imported, ciphertext);
            return String.fromCharCode(...new Uint8Array(plaintext)) + '|' + imported.algorithm.length + '|' + imported.usages.join(',');
            """).AsString().Should().Be("secret|192|decrypt");
    }

    // ---------------------------------------------------------------------------------------------------
    // JSON Web Key
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void ExportsAJwkWithTheFieldsAndTheOrderWebIdlGivesIt()
    {
        // A dictionary is converted to an object member by member in lexicographical order —
        // https://webidl.spec.whatwg.org/#es-dictionary — which is what a browser's JSON.stringify of a JWK
        // shows too. `k` is base64url with no padding, per Section 6.4 of JSON Web Algorithms.
        Run("""
            const key = await crypto.subtle.importKey('raw', repeat(0x0b, 20), { name: 'HMAC', hash: 'SHA-256' }, true, ['sign', 'verify']);
            const jwk = await crypto.subtle.exportKey('jwk', key);
            return Object.keys(jwk).join(',') + '|' + JSON.stringify(jwk);
            """).AsString().Should().Be(
                "alg,ext,k,key_ops,kty|{\"alg\":\"HS256\",\"ext\":true,\"k\":\"CwsLCwsLCwsLCwsLCwsLCwsLCws\",\"key_ops\":[\"sign\",\"verify\"],\"kty\":\"oct\"}");
    }

    [Theory]
    [InlineData("SHA-1", "HS1")]
    [InlineData("SHA-256", "HS256")]
    [InlineData("SHA-384", "HS384")]
    [InlineData("SHA-512", "HS512")]
    public void TheJwkAlgorithmNamesTheInnerHash(string hash, string alg)
    {
        Run($$"""
            const key = await crypto.subtle.generateKey({ name: 'HMAC', hash: '{{hash}}' }, true, ['sign']);
            const jwk = await crypto.subtle.exportKey('jwk', key);
            const imported = await crypto.subtle.importKey('jwk', jwk, { name: 'HMAC', hash: '{{hash}}' }, true, ['sign']);
            return jwk.alg + '|' + imported.algorithm.hash.name;
            """).AsString().Should().Be(alg + "|" + hash);
    }

    [Theory]
    [InlineData(128, "A128GCM")]
    [InlineData(192, "A192GCM")]
    [InlineData(256, "A256GCM")]
    public void TheJwkAlgorithmNamesTheAesKeyLength(int length, string alg)
    {
        Run($$"""
            const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: {{length}} }, true, ['encrypt']);
            const jwk = await crypto.subtle.exportKey('jwk', key);
            return jwk.alg + '|' + jwk.key_ops.join(',') + '|' + jwk.ext;
            """).AsString().Should().Be(alg + "|encrypt|true");
    }

    [Fact]
    public void ImportsAJwkWrittenByHand()
    {
        // The shape a script reads out of a configuration file: a plain object literal, base64url without
        // padding, and no fields beyond the ones the key needs.
        Run("""
            const key = await crypto.subtle.importKey(
                'jwk',
                { kty: 'oct', k: 'CwsLCwsLCwsLCwsLCwsLCwsLCws' },
                { name: 'HMAC', hash: 'SHA-1' },
                false,
                ['sign']);

            return hex(await crypto.subtle.sign('HMAC', key, ascii('Hi There')));
            """).AsString().Should().Be("b617318655057264e28bc0b6fb378c8ef146be00");
    }

    [Theory]
    // The kty must be "oct" for a symmetric key, and it must be there at all.
    [InlineData("{ k: 'AAAAAAAAAAAAAAAAAAAAAA' }", "DataError")]
    [InlineData("{ kty: 'RSA', k: 'AAAAAAAAAAAAAAAAAAAAAA' }", "DataError")]
    [InlineData("{ kty: 'OCT', k: 'AAAAAAAAAAAAAAAAAAAAAA' }", "DataError")]
    // Section 6.4.1 of JSON Web Algorithms: "This member MUST be present."
    [InlineData("{ kty: 'oct' }", "DataError")]
    // k must be base64url: no padding, no + or /, no whitespace, and no length that decodes to a fraction of
    // a byte.
    [InlineData("{ kty: 'oct', k: 'CwsLCwsLCwsLCwsLCwsLCwsLCws=' }", "DataError")]
    [InlineData("{ kty: 'oct', k: 'Cws+CwsLCwsLCwsLCwsLCwsLCws' }", "DataError")]
    [InlineData("{ kty: 'oct', k: 'Cws/CwsLCwsLCwsLCwsLCwsLCws' }", "DataError")]
    [InlineData("{ kty: 'oct', k: 'Cws CwsLCwsLCwsLCwsLCwsLCws' }", "DataError")]
    [InlineData("{ kty: 'oct', k: 'CwsLC' }", "DataError")]
    // An empty key is an empty key, whichever way it is spelled.
    [InlineData("{ kty: 'oct', k: '' }", "DataError")]
    // The alg field must agree with the hash the import asks for.
    [InlineData("{ kty: 'oct', k: 'CwsLCwsLCwsLCwsLCwsLCwsLCws', alg: 'HS512' }", "DataError")]
    [InlineData("{ kty: 'oct', k: 'CwsLCwsLCwsLCwsLCwsLCwsLCws', alg: 'A128GCM' }", "DataError")]
    // "If usages is non-empty and the use field of jwk is present and is not 'sig', throw a DataError."
    [InlineData("{ kty: 'oct', k: 'CwsLCwsLCwsLCwsLCwsLCwsLCws', use: 'enc' }", "DataError")]
    // key_ops must be valid JSON Web Key and must cover the requested usages.
    [InlineData("{ kty: 'oct', k: 'CwsLCwsLCwsLCwsLCwsLCwsLCws', key_ops: ['verify'] }", "DataError")]
    [InlineData("{ kty: 'oct', k: 'CwsLCwsLCwsLCwsLCwsLCwsLCws', key_ops: ['sign', 'sign'] }", "DataError")]
    // "If the ext field of jwk is present and has the value false and extractable is true, throw a DataError."
    [InlineData("{ kty: 'oct', k: 'CwsLCwsLCwsLCwsLCwsLCwsLCws', ext: false }", "DataError")]
    public void RefusesAMalformedJwkWithADataError(string jwk, string expected)
    {
        var engine = WebEngine();

        // Every one of these is imported as extractable with the single usage 'sign', so the `ext: false` row
        // and the `key_ops` rows are about the JWK rather than about the request.
        Settle(engine, $$"""
            crypto.subtle.importKey('jwk', {{jwk}}, { name: 'HMAC', hash: 'SHA-256' }, true, ['sign'])
                .then(() => 'imported', e => e.name + '/' + (e instanceof DOMException))
            """).AsString().Should().Be(expected + "/true");
    }

    [Fact]
    public void AcceptsTheJwkFieldsThatDoAgree()
    {
        // The mirror image of the rows above: each field, present and correct, imports.
        Run("""
            const key = await crypto.subtle.importKey(
                'jwk',
                { kty: 'oct', k: 'CwsLCwsLCwsLCwsLCwsLCwsLCws', alg: 'HS256', use: 'sig', key_ops: ['sign', 'verify'], ext: true },
                { name: 'HMAC', hash: 'SHA-256' },
                true,
                ['sign']);

            // key_ops may name more than was asked for; the key carries what was asked for.
            return key.usages.join(',') + '|' + key.extractable + '|' + key.algorithm.length;
            """).AsString().Should().Be("sign|true|160");
    }

    [Fact]
    public void AnExtFalseJwkImportsWhenTheKeyIsNotAskedToBeExtractable()
    {
        // The check is about the pair: a key marked non-extractable may be imported, as long as it stays
        // non-extractable.
        Run("""
            const key = await crypto.subtle.importKey(
                'jwk',
                { kty: 'oct', k: 'CwsLCwsLCwsLCwsLCwsLCwsLCws', ext: false },
                { name: 'HMAC', hash: 'SHA-256' },
                false,
                ['sign']);
            return key.extractable + '|' + key.usages.join(',');
            """).AsString().Should().Be("false|sign");
    }

    [Fact]
    public void AJwkUseFieldIsIgnoredWhenNothingIsAskedOfTheKey()
    {
        // "If usages is non-empty and the use field …" — but an empty usages list cannot reach the check,
        // because the operation ends in the SyntaxError for a secret key with no usages instead.
        var engine = WebEngine();

        Settle(engine, """
            crypto.subtle.importKey('jwk', { kty: 'oct', k: 'CwsLCwsLCwsLCwsLCwsLCwsLCws', use: 'enc' },
                { name: 'HMAC', hash: 'SHA-256' }, false, [])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("SyntaxError");
    }

    [Fact]
    public void ReadsTheJwkFieldsInWebIdlsOwnOrderAndOnlyOnce()
    {
        // A dictionary's members are converted in lexicographical order, each read exactly once. A second
        // read would be a second chance for a getter to change the answer between the check and the import.
        Run("""
            const reads = [];
            const jwk = {
                get kty() { reads.push('kty'); return 'oct'; },
                get k() { reads.push('k'); return 'CwsLCwsLCwsLCwsLCwsLCwsLCws'; },
                get alg() { reads.push('alg'); return 'HS256'; },
                get use() { reads.push('use'); return 'sig'; },
                get ext() { reads.push('ext'); return true; },
                get key_ops() { reads.push('key_ops'); return ['sign']; },
            };

            await crypto.subtle.importKey('jwk', jwk, { name: 'HMAC', hash: 'SHA-256' }, true, ['sign']);
            return reads.join(',');
            """).AsString().Should().Be("alg,ext,k,key_ops,kty,use");
    }

    [Fact]
    public void TheJwkIsReadBeforeTheAlgorithmIsNormalized()
    {
        // WebIDL converts every argument before a single step of the method body runs, and the JsonWebKey
        // conversion is one of those argument conversions — so the getters run even though the algorithm is
        // about to be rejected.
        Run("""
            let read = false;
            const jwk = { kty: 'oct', get k() { read = true; return 'CwsLCwsLCwsLCwsLCwsLCwsLCws'; } };

            let failure;
            try {
                await crypto.subtle.importKey('jwk', jwk, 'RSA-OAEP', true, ['sign']);
            } catch (e) {
                failure = e.name;
            }
            return read + '|' + failure;
            """).AsString().Should().Be("true|NotSupportedError");
    }

    // ---------------------------------------------------------------------------------------------------
    // The usage matrix
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    // A key must permit the operation being asked of it, or the answer is an InvalidAccessError — never a
    // silent success and never a TypeError.
    [InlineData("sign", "['verify']", "InvalidAccessError")]
    [InlineData("verify", "['sign']", "InvalidAccessError")]
    [InlineData("sign", "['sign']", "ok")]
    [InlineData("verify", "['verify']", "ok")]
    [InlineData("sign", "['sign', 'verify']", "ok")]
    public void EnforcesTheHmacUsageMatrix(string operation, string usages, string expected)
    {
        var call = string.Equals(operation, "sign", StringComparison.Ordinal)
            ? "crypto.subtle.sign('HMAC', key, ascii('m'))"
            : "crypto.subtle.verify('HMAC', key, new Uint8Array(32), ascii('m'))";

        Run($$"""
            const key = await crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, false, {{usages}});
            try {
                await {{call}};
                return 'ok';
            } catch (e) {
                return e.name;
            }
            """).AsString().Should().Be(expected);
    }

    [Theory]
    [InlineData("encrypt", "['decrypt']", "InvalidAccessError")]
    [InlineData("decrypt", "['encrypt']", "InvalidAccessError")]
    [InlineData("encrypt", "['encrypt']", "ok")]
    [InlineData("decrypt", "['decrypt']", "ok")]
    [InlineData("encrypt", "['wrapKey']", "InvalidAccessError")]
    [InlineData("decrypt", "['encrypt', 'decrypt']", "ok")]
    public void EnforcesTheAesGcmUsageMatrix(string operation, string usages, string expected)
    {
        // The same key material is imported twice - once with the usages under test and once as an encryptor
        // - so that the decrypt rows have something to decrypt that was made with their own key.
        var data = string.Equals(operation, "encrypt", StringComparison.Ordinal) ? "ascii('m')" : "sample";

        Run($$"""
            const material = repeat(5, 16);
            const iv = repeat(4, 12);
            const encryptor = await crypto.subtle.importKey('raw', material, 'AES-GCM', false, ['encrypt']);
            const sample = await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, encryptor, ascii('m'));

            const key = await crypto.subtle.importKey('raw', material, 'AES-GCM', false, {{usages}});
            try {
                await crypto.subtle.{{operation}}({ name: 'AES-GCM', iv }, key, {{data}});
                return 'ok';
            } catch (e) {
                return e.name;
            }
            """).AsString().Should().Be(expected);
    }

    [Fact]
    public void AKeyMadeForOneAlgorithmRefusesAnother()
    {
        Run("""
            const hmac = await crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, false, ['sign', 'verify']);
            const aes = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 128 }, false, ['encrypt', 'decrypt']);

            const outcomes = [];
            // The name check comes before the usage check, and both are InvalidAccessError.
            try { await crypto.subtle.encrypt({ name: 'AES-GCM', iv: repeat(0, 12) }, hmac, ascii('m')); outcomes.push('ok'); }
            catch (e) { outcomes.push(e.name); }
            try { await crypto.subtle.sign('HMAC', aes, ascii('m')); outcomes.push('ok'); }
            catch (e) { outcomes.push(e.name); }

            // An algorithm that is not registered for the operation at all is a different failure: the
            // registry is consulted before any key is looked at.
            try { await crypto.subtle.sign('AES-GCM', aes, ascii('m')); outcomes.push('ok'); }
            catch (e) { outcomes.push(e.name); }
            try { await crypto.subtle.encrypt({ name: 'HMAC', iv: repeat(0, 12) }, hmac, ascii('m')); outcomes.push('ok'); }
            catch (e) { outcomes.push(e.name); }

            return outcomes.join(',');
            """).AsString().Should().Be("InvalidAccessError,InvalidAccessError,NotSupportedError,NotSupportedError");
    }

    [Theory]
    [InlineData("{ name: 'HMAC', hash: 'SHA-256' }", "['encrypt']")]
    [InlineData("{ name: 'HMAC', hash: 'SHA-256' }", "['sign', 'deriveKey']")]
    [InlineData("{ name: 'AES-GCM', length: 128 }", "['sign']")]
    [InlineData("{ name: 'AES-GCM', length: 128 }", "['encrypt', 'verify']")]
    public void RefusesAUsageTheAlgorithmDoesNotSupportWithASyntaxError(string algorithm, string usages)
    {
        var engine = WebEngine();

        // A recognized usage that is wrong for the algorithm is the SyntaxError the algorithm's own first
        // step raises — "a required parameter was missing or out-of-range".
        Settle(engine, $$"""
            crypto.subtle.generateKey({{algorithm}}, false, {{usages}}).then(() => 'generated', e => e.name)
            """).AsString().Should().Be("SyntaxError");

        Settle(engine, $$"""
            crypto.subtle.importKey('raw', new Uint8Array(16), {{algorithm}}, false, {{usages}}).then(() => 'imported', e => e.name)
            """).AsString().Should().Be("SyntaxError");
    }

    [Fact]
    public void RefusesASecretKeyWithNoUsagesAtAll()
    {
        var engine = WebEngine();

        foreach (var source in new[]
        {
            "crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, false, [])",
            "crypto.subtle.generateKey({ name: 'AES-GCM', length: 128 }, false, [])",
            "crypto.subtle.importKey('raw', new Uint8Array(16), 'AES-GCM', false, [])",
        })
        {
            Settle(engine, source + ".then(() => 'made', e => e.name + '/' + (e instanceof DOMException))")
                .AsString().Should().Be("SyntaxError/true");
        }
    }

    [Fact]
    public void AnUnrecognizedUsageIsATypeErrorRatherThanASyntaxError()
    {
        var engine = WebEngine();

        // The IDL type is `sequence<KeyUsage>`, and a value outside an enumeration fails the WebIDL
        // conversion. That is a different failure from a usage that is recognized but wrong for the
        // algorithm, and it happens earlier — before the algorithm is normalized at all.
        Settle(engine, """
            crypto.subtle.generateKey('nonsense', false, ['encipher']).then(() => 'generated', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");

        Settle(engine, """
            crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, false, 'sign').then(() => 'generated', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");
    }

    // ---------------------------------------------------------------------------------------------------
    // Formats and algorithms that are not there
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("spki")]
    [InlineData("pkcs8")]
    public void RefusesAnAsymmetricKeyFormatWithANotSupportedError(string format)
    {
        var engine = WebEngine();

        // A recognized KeyFormat that the algorithm's own steps do not handle — "Otherwise: throw a
        // NotSupportedError" — which for a symmetric key is every format but raw and jwk, in a browser too.
        Settle(engine, $$"""
            crypto.subtle.importKey('{{format}}', new Uint8Array(16), 'AES-GCM', false, ['encrypt'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("NotSupportedError");

        Settle(engine, $$"""
            crypto.subtle.generateKey({ name: 'AES-GCM', length: 128 }, true, ['encrypt'])
                .then(key => crypto.subtle.exportKey('{{format}}', key))
                .then(() => 'exported', e => e.name)
            """).AsString().Should().Be("NotSupportedError");
    }

    [Theory]
    [InlineData("'RAW'")]
    [InlineData("'pem'")]
    [InlineData("''")]
    [InlineData("undefined")]
    [InlineData("42")]
    public void RefusesAFormatOutsideTheEnumerationWithATypeError(string format)
    {
        var engine = WebEngine();

        // KeyFormat is a WebIDL enumeration, matched case-sensitively; anything else is a TypeError rather
        // than a NotSupportedError.
        Settle(engine, $$"""
            crypto.subtle.importKey({{format}}, new Uint8Array(16), 'AES-GCM', false, ['encrypt'])
                .then(() => 'imported', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");
    }

    [Theory]
    [InlineData("sign", "'RSASSA-PKCS1-v1_5'")]
    [InlineData("verify", "'ECDSA'")]
    [InlineData("encrypt", "'AES-CBC'")]
    [InlineData("decrypt", "{ name: 'AES-CTR' }")]
    [InlineData("generateKey", "'RSA-OAEP'")]
    [InlineData("importKey", "'PBKDF2'")]
    public void RefusesAnUnregisteredAlgorithmWithANotSupportedError(string operation, string algorithm)
    {
        var engine = WebEngine();

        var call = operation switch
        {
            "sign" => $"crypto.subtle.sign({algorithm}, key, new Uint8Array(0))",
            "verify" => $"crypto.subtle.verify({algorithm}, key, new Uint8Array(0), new Uint8Array(0))",
            "encrypt" => $"crypto.subtle.encrypt({algorithm}, key, new Uint8Array(0))",
            "decrypt" => $"crypto.subtle.decrypt({algorithm}, key, new Uint8Array(0))",
            "generateKey" => $"crypto.subtle.generateKey({algorithm}, false, ['sign'])",
            _ => $"crypto.subtle.importKey('raw', new Uint8Array(16), {algorithm}, false, ['sign'])",
        };

        Settle(engine, $$"""
            crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, false, ['sign', 'verify'])
                .then(key => {{call}})
                .then(() => 'succeeded', e => e.name + '/' + e.code)
            """).AsString().Should().Be("NotSupportedError/9");
    }

    [Fact]
    public void OnlyTheOperationsThisEngineImplementsExist()
    {
        var engine = WebEngine();

        engine.Evaluate("""
            ['digest', 'sign', 'verify', 'encrypt', 'decrypt', 'generateKey', 'importKey', 'exportKey']
                .map(name => typeof crypto.subtle[name]).join(',')
            """).AsString().Should().Be("function,function,function,function,function,function,function,function");

        // Absent rather than present-and-throwing: `typeof crypto.subtle.deriveBits` is how a library that
        // does cryptography decides whether it can, and it has to get the truthful answer.
        engine.Evaluate("""
            ['deriveKey', 'deriveBits', 'wrapKey', 'unwrapKey'].filter(name => name in crypto.subtle).join(',')
            """).AsString().Should().Be("");
    }

    [Fact]
    public void EveryOperationHasTheIdlArityAndTheAttributesOfABuiltInMethod()
    {
        var engine = WebEngine();

        // WebIDL's length counts the required arguments only.
        engine.Evaluate("""
            ['digest', 'sign', 'verify', 'encrypt', 'decrypt', 'generateKey', 'importKey', 'exportKey']
                .map(name => name + ':' + crypto.subtle[name].length + ':' + crypto.subtle[name].name).join(',')
            """).AsString().Should().Be(
                "digest:2:digest,sign:3:sign,verify:4:verify,encrypt:3:encrypt,decrypt:3:decrypt,"
                + "generateKey:3:generateKey,importKey:5:importKey,exportKey:2:exportKey");

        engine.Evaluate("JSON.stringify(Object.keys(crypto.subtle))").AsString().Should().Be("[]");
    }

    // ---------------------------------------------------------------------------------------------------
    // Normalization, argument conversion, and the order the failures come in
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("'hmac'")]
    [InlineData("'Hmac'")]
    [InlineData("{ name: 'hMaC' }")]
    public void MatchesTheRegisteredAlgorithmNameCaseInsensitively(string algorithm)
    {
        // "If registeredAlgorithms contains a key that is a case-insensitive string match for algName: set
        // algName to the value of the matching key" - and every operation then matches the registered key
        // case-sensitively, so normalization is the only thing that lets a lowercase spelling reach HMAC at
        // all. The key remembers the registered spelling, not the caller's.
        Run($$"""
            const key = await crypto.subtle.importKey('raw', repeat(0x0b, 20), { name: 'hmac', hash: 'sha-1' }, false, ['sign']);
            return hex(await crypto.subtle.sign({{algorithm}}, key, ascii('Hi There')))
                + '|' + key.algorithm.name + '|' + key.algorithm.hash.name;
            """).AsString().Should().Be("b617318655057264e28bc0b6fb378c8ef146be00|HMAC|SHA-1");
    }

    [Fact]
    public void TheHashMemberIsNormalizedAsADigestAlgorithm()
    {
        var engine = WebEngine();

        // "If member is of the type HashAlgorithmIdentifier: set it to the result of normalizing an
        // algorithm, with op set to 'digest'" — so the hash may be a string or an object, and a name that is
        // not a registered digest is a NotSupportedError.
        Settle(engine, """
            crypto.subtle.generateKey({ name: 'HMAC', hash: { name: 'SHA-384' } }, false, ['sign'])
                .then(key => key.algorithm.hash.name + '|' + key.algorithm.length, e => e.name)
            """).AsString().Should().Be("SHA-384|1024");

        Settle(engine, """
            crypto.subtle.generateKey({ name: 'HMAC', hash: 'MD5' }, false, ['sign']).then(() => 'generated', e => e.name)
            """).AsString().Should().Be("NotSupportedError");

        // A required dictionary member that is missing is the TypeError WebIDL raises for it, not a
        // NotSupportedError for the name "undefined" — and a bare 'HMAC' string is a dictionary with no hash.
        foreach (var algorithm in new[] { "'HMAC'", "{ name: 'HMAC' }", "{ name: 'HMAC', hash: undefined }" })
        {
            Settle(engine, $$"""
                crypto.subtle.generateKey({{algorithm}}, false, ['sign']).then(() => 'generated', e => e.constructor.name)
                """).AsString().Should().Be("TypeError");
        }
    }

    [Fact]
    public void TheAesKeyLengthIsARequiredMember()
    {
        var engine = WebEngine();

        foreach (var algorithm in new[] { "'AES-GCM'", "{ name: 'AES-GCM' }" })
        {
            Settle(engine, $$"""
                crypto.subtle.generateKey({{algorithm}}, false, ['encrypt']).then(() => 'generated', e => e.constructor.name)
                """).AsString().Should().Be("TypeError");
        }

        // A length outside the three AES has is the algorithm's own OperationError, not a TypeError: it
        // passed the IDL conversion and failed step 2.
        foreach (var length in new[] { "0", "64", "129", "512" })
        {
            Settle(engine, $$"""
                crypto.subtle.generateKey({ name: 'AES-GCM', length: {{length}} }, false, ['encrypt'])
                    .then(() => 'generated', e => e.name)
                """).AsString().Should().Be("OperationError");
        }

        // ... and one outside `unsigned short` fails the [EnforceRange] conversion instead.
        Settle(engine, """
            crypto.subtle.generateKey({ name: 'AES-GCM', length: 65536 }, false, ['encrypt'])
                .then(() => 'generated', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");
    }

    [Fact]
    public void TheHmacKeyLengthFollowsTheHashBlockSizeUnlessItIsGiven()
    {
        Run("""
            const lengths = [];
            for (const hash of ['SHA-1', 'SHA-256', 'SHA-384', 'SHA-512']) {
                const key = await crypto.subtle.generateKey({ name: 'HMAC', hash }, true, ['sign']);
                lengths.push(key.algorithm.length + ':' + (await crypto.subtle.exportKey('raw', key)).byteLength);
            }

            const asked = await crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256', length: 128 }, true, ['sign']);
            lengths.push(asked.algorithm.length + ':' + (await crypto.subtle.exportKey('raw', asked)).byteLength);
            return lengths.join(',');
            """).AsString().Should().Be("512:64,512:64,1024:128,1024:128,128:16");
    }

    [Fact]
    public void RefusesToGenerateAnHmacKeyItCannotRepresent()
    {
        var engine = WebEngine();

        // A zero length is the specification's own OperationError. A length that is not a whole number of
        // bytes is one of the ways "the key generation step fails" here, which the class documents: an
        // eight-byte key whose last seven bits were a lie would be worse than a refusal.
        Settle(engine, """
            crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256', length: 0 }, false, ['sign'])
                .then(() => 'generated', e => e.name)
            """).AsString().Should().Be("OperationError");

        Settle(engine, """
            crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256', length: 57 }, false, ['sign'])
                .then(() => 'generated', e => e.name)
            """).AsString().Should().Be("OperationError");
    }

    [Fact]
    public void TheImportedHmacLengthMustNameTheBytesThatArrived()
    {
        var engine = WebEngine();

        // Step 9: a requested length may trim bits off the last byte but never a whole byte, in either
        // direction — greater than the material is a DataError, and so is a byte less.
        Settle(engine, """
            crypto.subtle.importKey('raw', new Uint8Array(20), { name: 'HMAC', hash: 'SHA-256', length: 161 }, false, ['sign'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("DataError");

        Settle(engine, """
            crypto.subtle.importKey('raw', new Uint8Array(20), { name: 'HMAC', hash: 'SHA-256', length: 152 }, false, ['sign'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("DataError");

        Settle(engine, """
            crypto.subtle.importKey('raw', new Uint8Array(20), { name: 'HMAC', hash: 'SHA-256', length: 153 }, false, ['sign'])
                .then(key => key.algorithm.length, e => e.name)
            """).AsNumber().Should().Be(153);

        // A present zero is a DataError here where generateKey calls the same value an OperationError.
        Settle(engine, """
            crypto.subtle.importKey('raw', new Uint8Array(20), { name: 'HMAC', hash: 'SHA-256', length: 0 }, false, ['sign'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("DataError");

        // An empty key has no bits to name at all.
        Settle(engine, """
            crypto.subtle.importKey('raw', new Uint8Array(0), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("DataError");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(17)]
    [InlineData(64)]
    public void RefusesAnAesKeyOfTheWrongSizeWithADataError(int bytes)
    {
        var engine = WebEngine();

        Settle(engine, $$"""
            crypto.subtle.importKey('raw', new Uint8Array({{bytes}}), 'AES-GCM', false, ['encrypt'])
                .then(() => 'imported', e => e.name)
            """).AsString().Should().Be("DataError");
    }

    [Theory]
    // The tag lengths the specification lists but this platform's AES-GCM cannot produce.
    [InlineData("32", "OperationError")]
    [InlineData("64", "OperationError")]
    // A value that is not in the list at all.
    [InlineData("8", "OperationError")]
    [InlineData("100", "OperationError")]
    [InlineData("255", "OperationError")]
    // ... and one outside `octet`, which fails the [EnforceRange] conversion first.
    [InlineData("256", "TypeError")]
    [InlineData("-8", "TypeError")]
    public void RefusesATagLengthItCannotProduce(string tagLength, string expected)
    {
        var engine = WebEngine();

        Settle(engine, $$"""
            crypto.subtle.generateKey({ name: 'AES-GCM', length: 128 }, false, ['encrypt'])
                .then(key => crypto.subtle.encrypt({ name: 'AES-GCM', iv: new Uint8Array(12), tagLength: {{tagLength}} }, key, new Uint8Array(0)))
                .then(() => 'encrypted', e => e.name === 'OperationError' ? 'OperationError' : e.constructor.name)
            """).AsString().Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(16)]
    public void RefusesAnIvThePlatformCannotUse(int ivLength)
    {
        var engine = WebEngine();

        // A documented limit of the platform's AES-GCM, reported as the OperationError the algorithm's own
        // steps end in. 96 bits is what NIST SP 800-38D recommends and what nearly every protocol uses.
        Settle(engine, $$"""
            crypto.subtle.generateKey({ name: 'AES-GCM', length: 128 }, false, ['encrypt'])
                .then(key => crypto.subtle.encrypt({ name: 'AES-GCM', iv: new Uint8Array({{ivLength}}) }, key, new Uint8Array(0)))
                .then(() => 'encrypted', e => e.name)
            """).AsString().Should().Be("OperationError");
    }

    [Fact]
    public void TheIvIsARequiredMemberOfAesGcmParams()
    {
        var engine = WebEngine();

        foreach (var algorithm in new[] { "'AES-GCM'", "{ name: 'AES-GCM' }", "{ name: 'AES-GCM', iv: undefined }", "{ name: 'AES-GCM', iv: 'twelve bytes' }" })
        {
            Settle(engine, $$"""
                crypto.subtle.generateKey({ name: 'AES-GCM', length: 128 }, false, ['encrypt'])
                    .then(key => crypto.subtle.encrypt({{algorithm}}, key, new Uint8Array(0)))
                    .then(() => 'encrypted', e => e.constructor.name)
                """).AsString().Should().Be("TypeError");
        }
    }

    [Fact]
    public void ABufferSourceMemberIsCopiedWhenItIsRead()
    {
        // "If member is of the type BufferSource and is present: set the dictionary member … to the result of
        // getting a copy of the bytes held by idlValue." The iv is read before `tagLength` is, so a getter on
        // tagLength that rewrites the iv cannot change the one the operation uses — the ciphertext must be
        // the one the original iv produces.
        Run("""
            const key = await crypto.subtle.importKey('raw', bytes('7fddb57453c241d03efbed3ac44e371c'), 'AES-GCM', false, ['encrypt']);
            const iv = bytes('ee283a3fc75575e33efd4887');

            const ciphertext = await crypto.subtle.encrypt(
                { name: 'AES-GCM', iv, get tagLength() { iv.fill(0xff); return 128; } },
                key,
                bytes('d5de42b461646c255c87bd2962d3b9a2'));

            return hex(ciphertext) + '|' + hex(iv);
            """).AsString().Should().Be(
                "2ccda4a5415cb91e135c2a0f78c9b2fdb36d1df9b9d5e596f83e8b7f52971cb3|ffffffffffffffffffffffff");
    }

    [Fact]
    public void TheDataArgumentIsCopiedBeforeAnyGetterCanRun()
    {
        // The argument conversions run before the method body, and the body's first step normalizes an
        // algorithm — which reads `name` and may run a script's getter, with the caller's buffer in scope. A
        // window onto the engine's own array would hash what the getter left behind.
        Run("""
            const data = ascii('abc');
            const digest = await crypto.subtle.digest({ get name() { data.fill(0x21); return 'SHA-256'; } }, data);
            return hex(digest) + '|' + hex(data);
            """).AsString().Should().Be(
                "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad|212121");
    }

    [Fact]
    public void ArgumentsAreConvertedLeftToRightBeforeTheBodyRuns()
    {
        var engine = WebEngine();

        // sign(algorithm, key, data): a bad key outranks an unregistered algorithm, and a bad data outranks
        // a bad key, because argument conversion runs before normalization and runs left to right.
        Settle(engine, """
            crypto.subtle.sign('nonsense', {}, new Uint8Array(0)).then(() => 'signed', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");

        Settle(engine, """
            crypto.subtle.sign('nonsense', 'not a key', 'not a buffer').then(() => 'signed', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");

        // ... and with both arguments valid the algorithm name does earn its own failure.
        Settle(engine, """
            crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, false, ['sign'])
                .then(key => crypto.subtle.sign('nonsense', key, new Uint8Array(0)))
                .then(() => 'signed', e => e.name)
            """).AsString().Should().Be("NotSupportedError");
    }

    [Fact]
    public void RefusesAKeyDataThatIsNeitherABufferSourceNorAJwk()
    {
        var engine = WebEngine();

        // The union's two arms, each with the format that does not match it.
        Settle(engine, """
            crypto.subtle.importKey('jwk', new Uint8Array(16), 'AES-GCM', false, ['encrypt'])
                .then(() => 'imported', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");

        Settle(engine, """
            crypto.subtle.importKey('raw', { kty: 'oct', k: 'CwsLCwsLCwsLCwsLCwsLCwsLCws' }, 'AES-GCM', false, ['encrypt'])
                .then(() => 'imported', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");

        // Converting a non-object to a dictionary is a TypeError, so a primitive fails whichever format is
        // asked for.
        foreach (var keyData in new[] { "42", "'a string'", "true", "Symbol('nope')" })
        {
            Settle(engine, $$"""
                crypto.subtle.importKey('jwk', {{keyData}}, { name: 'HMAC', hash: 'SHA-256' }, false, ['sign'])
                    .then(() => 'imported', e => e.constructor.name)
                """).AsString().Should().Be("TypeError");
        }
    }

    [Fact]
    public void RefusesABufferSourceBackedByASharedArrayBuffer()
    {
        var engine = WebEngine();

        // The IDL is BufferSource, not AllowSharedBufferSource, and WebIDL refuses a shared buffer for any
        // type not carrying [AllowShared] — the rule crypto.getRandomValues refuses one under.
        Settle(engine, """
            crypto.subtle.importKey('raw', new Uint8Array(new SharedArrayBuffer(16)), 'AES-GCM', false, ['encrypt'])
                .then(() => 'imported', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");

        Settle(engine, """
            crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, false, ['sign'])
                .then(key => crypto.subtle.sign('HMAC', key, new SharedArrayBuffer(8)))
                .then(() => 'signed', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");

        Settle(engine, """
            crypto.subtle.generateKey({ name: 'AES-GCM', length: 128 }, false, ['encrypt'])
                .then(key => crypto.subtle.encrypt({ name: 'AES-GCM', iv: new Uint8Array(new SharedArrayBuffer(12)) }, key, new Uint8Array(0)))
                .then(() => 'encrypted', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");
    }

    [Fact]
    public void AcceptsEveryShapeOfBufferSourceForTheKeyAndTheMessage()
    {
        // `BufferSource` is "an ArrayBuffer, or any view over one" — the element type of a view is not
        // consulted, only the bytes underneath it.
        Run("""
            const material = repeat(0x0b, 20);
            const shapes = [
                material,
                material.buffer,
                new DataView(material.buffer),
                new Int8Array(material.buffer),
            ];

            const results = [];
            for (const shape of shapes) {
                const key = await crypto.subtle.importKey('raw', shape, { name: 'HMAC', hash: 'SHA-1' }, false, ['sign']);
                results.push(hex(await crypto.subtle.sign('HMAC', key, new DataView(ascii('Hi There').buffer))));
            }
            return new Set(results).size + '|' + results[0];
            """).AsString().Should().Be("1|b617318655057264e28bc0b6fb378c8ef146be00");
    }

    [Fact]
    public void ADetachedBufferIsTheEmptyByteSequence()
    {
        var engine = WebEngine();

        // https://webidl.spec.whatwg.org/#dfn-get-buffer-source-copy step 7: a detached buffer contributes
        // the empty byte sequence. For an AES key that is a DataError about the length, not a TypeError.
        Settle(engine, """
            (() => {
                const buffer = new ArrayBuffer(16);
                const view = new Uint8Array(buffer);
                buffer.transfer();
                return crypto.subtle.importKey('raw', view, 'AES-GCM', false, ['encrypt'])
                    .then(() => 'imported', e => e.name);
            })()
            """).AsString().Should().Be("DataError");
    }

    // ---------------------------------------------------------------------------------------------------
    // Promises, rejections and the receiver
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void NoOperationEverThrowsSynchronously()
    {
        var engine = WebEngine();

        // Every failure is a rejection, so the call itself always answers a promise — even when every
        // argument is nonsense and even when the receiver is wrong.
        engine.Evaluate("""
            (() => {
                const results = [
                    crypto.subtle.sign('nope', 'nope', 'nope'),
                    crypto.subtle.verify(Symbol.iterator, {}, 1, 2),
                    crypto.subtle.encrypt(null, null, null),
                    crypto.subtle.decrypt(undefined, undefined, undefined),
                    crypto.subtle.generateKey(Symbol('x'), 0, 0),
                    crypto.subtle.importKey('nope', 'nope', 'nope', 'nope', 'nope'),
                    crypto.subtle.exportKey('raw', {}),
                    crypto.subtle.sign.call({}, 'HMAC', {}, new Uint8Array(0)),
                    crypto.subtle.exportKey.call(null, 'raw', {}),
                ];
                results.forEach(p => p.catch(() => {}));
                return results.every(p => p instanceof Promise);
            })()
            """).AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void EveryOperationBrandChecksItsReceiverAsARejection()
    {
        var engine = WebEngine();

        // The brand check sits inside the same try whose exception a promise-returning operation converts
        // into a rejection — https://webidl.spec.whatwg.org/#dfn-create-operation-function.
        Settle(engine, """
            Promise.all(['sign', 'verify', 'encrypt', 'decrypt', 'generateKey', 'importKey', 'exportKey']
                .map(name => crypto.subtle[name].call({}).then(() => 'resolved', e => e.constructor.name)))
                .then(names => names.join(','))
            """).AsString().Should().Be("TypeError,TypeError,TypeError,TypeError,TypeError,TypeError,TypeError");

        // ... and an extracted operation still works when the receiver is the real one.
        Settle(engine, """
            (() => {
                const generateKey = crypto.subtle.generateKey;
                return generateKey.call(crypto.subtle, { name: 'HMAC', hash: 'SHA-256' }, false, ['sign'])
                    .then(key => key.type);
            })()
            """).AsString().Should().Be("secret");
    }

    [Fact]
    public void AnErrorFromAGetterBecomesTheRejection()
    {
        var engine = WebEngine();

        // Whatever a script's own getter throws is what the promise rejects with — the operation still never
        // throws to its caller, and the error is not replaced by one of ours.
        Settle(engine, """
            crypto.subtle.generateKey({ get name() { throw new RangeError('from the getter'); } }, false, ['sign'])
                .then(() => 'generated', e => e.constructor.name + ': ' + e.message)
            """).AsString().Should().Be("RangeError: from the getter");

        Settle(engine, """
            crypto.subtle.importKey('jwk', { get kty() { throw new EvalError('from the jwk'); } },
                { name: 'HMAC', hash: 'SHA-256' }, false, ['sign'])
                .then(() => 'imported', e => e.constructor.name + ': ' + e.message)
            """).AsString().Should().Be("EvalError: from the jwk");
    }

    [Fact]
    public void EveryOperationSettlesOnAMicrotaskTurn()
    {
        var engine = WebEngine();

        // The work is synchronous, but the promises are promises: their reactions run on the microtask turn,
        // after the rest of the current script.
        engine.Evaluate("""
            (() => {
                const order = [];
                crypto.subtle.generateKey({ name: 'AES-GCM', length: 128 }, false, ['encrypt']).then(() => order.push('generateKey'));
                crypto.subtle.digest('SHA-256', new Uint8Array(0)).then(() => order.push('digest'));
                order.push('sync');
                return Promise.resolve().then(() => order.join(','));
            })()
            """).UnwrapIfPromise().AsString().Should().Be("sync,generateKey,digest");
    }

    [Fact]
    public void AFailureIsCatchableInScript()
    {
        Run("""
            const outcomes = [];
            try {
                await crypto.subtle.generateKey({ name: 'AES-GCM', length: 100 }, false, ['encrypt']);
            } catch (e) {
                outcomes.push(e.name + '/' + (e instanceof DOMException) + '/' + (e instanceof Error));
            }
            return outcomes.join(',');
            """).AsString().Should().Be("OperationError/true/true");
    }

    // ---------------------------------------------------------------------------------------------------
    // Installation
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void CryptoKeyRidesTheCryptoFlagAndNeverReachesAShadowRealm()
    {
        var engine = WebEngine();

        engine.Evaluate("typeof CryptoKey").AsString().Should().Be("function");
        engine.Evaluate("new ShadowRealm().evaluate('typeof CryptoKey')").AsString().Should().Be("undefined");

        // A WebIDL interface object is writable and configurable but not enumerable —
        // https://webidl.spec.whatwg.org/#es-interfaces.
        engine.Evaluate("""
            (() => {
                const d = Object.getOwnPropertyDescriptor(globalThis, 'CryptoKey');
                return [d.enumerable, d.writable, d.configurable].join('|');
            })()
            """).AsString().Should().Be("false|true|true");

        new Engine(options => options.UseWebApis(WebApiFeatures.Console))
            .Evaluate("typeof CryptoKey").AsString().Should().Be("undefined");

        new Engine().Evaluate("typeof CryptoKey").AsString().Should().Be("undefined");
    }

    [Fact]
    public void AKeyIsNotStructuredCloneable()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Crypto | WebApiFeatures.StructuredClone));

        // A CryptoKey is a serializable object per the specification, and this engine's structuredClone
        // refuses everything it does not recognize rather than producing a clone that has silently lost the
        // state its source carried — which for a key would be the key.
        Settle(engine, """
            crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, false, ['sign'])
                .then(key => { try { structuredClone(key); return 'cloned'; } catch (e) { return e.name; } })
            """).AsString().Should().Be("DataCloneError");
    }
}
#endif
