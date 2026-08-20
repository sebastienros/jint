#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>crypto.subtle.digest</c> against the Web Cryptography API — https://w3c.github.io/webcrypto/#subtlecrypto-interface.
/// The keyed operations, which have their own vectors and their own failure modes, are in
/// <see cref="SubtleCryptoKeyTests"/>; what is pinned here is the one operation that needs no key at all,
/// plus the absence of key derivation and key wrapping, because "absent rather than throwing" is a promise
/// feature detection is written against.
/// </summary>
/// <remarks>
/// The digests are asserted against the published example vectors of FIPS-180-4 (and, for SHA-1, RFC 3174),
/// as exact hexadecimal — a hash is the one thing in this file that can be checked to the last bit.
/// Everything else is about the shape of the operation: which failures are rejections rather than throws,
/// in which order they come, and which spelling of an algorithm name reaches which hash function.
/// </remarks>
public class SubtleCryptoTests
{
    /// <summary>The message every SHA implementation's second published example hashes: 56 ASCII bytes.</summary>
    private const string TwoBlockShortMessage = "abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq";

    /// <summary>The 112-byte message SHA-384 and SHA-512 use for the same purpose, their block being 128 bytes.</summary>
    private const string TwoBlockLongMessage =
        "abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu";

    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Crypto));

    /// <summary>
    /// Runs one digest to completion and renders it as lowercase hex. The bytes are built from character
    /// codes rather than through <c>TextEncoder</c>, so that the crypto feature is the only one the engine
    /// carries and nothing here can be passing because of a neighbour.
    /// </summary>
    private static string DigestHex(string algorithmExpression, string message)
    {
        return WebEngine().Evaluate($$"""
            (async () => {
                const data = Uint8Array.from('{{message}}', c => c.charCodeAt(0));
                const digest = await crypto.subtle.digest({{algorithmExpression}}, data);
                return Array.from(new Uint8Array(digest)).map(b => b.toString(16).padStart(2, '0')).join('');
            })()
            """).UnwrapIfPromise().AsString();
    }

    /// <summary>Settles the expression's promise and answers whatever it resolved or rejected to.</summary>
    private static JsValue Settle(Engine engine, string source) => engine.Evaluate(source).UnwrapIfPromise();

    [Theory]
    // https://www.rfc-editor.org/rfc/rfc3174 and https://csrc.nist.gov/projects/cryptographic-algorithm-validation-program
    [InlineData("SHA-1", "", "da39a3ee5e6b4b0d3255bfef95601890afd80709")]
    [InlineData("SHA-256", "", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("SHA-384", "", "38b060a751ac96384cd9327eb1b1e36a21fdb71114be07434c0cc7bf63f6e1da274edebfe76f65fbd51ad2f14898b95b")]
    [InlineData("SHA-512", "", "cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e")]
    [InlineData("SHA-1", "abc", "a9993e364706816aba3e25717850c26c9cd0d89d")]
    [InlineData("SHA-256", "abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData("SHA-384", "abc", "cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7")]
    [InlineData("SHA-512", "abc", "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f")]
    public void ProducesThePublishedVectorsForTheEmptyAndOneBlockMessages(string algorithm, string message, string expected)
    {
        DigestHex($"'{algorithm}'", message).Should().Be(expected);
    }

    [Theory]
    // The message each algorithm's own published two-block example uses: 56 bytes for the 64-byte-block
    // algorithms, 112 for the 128-byte-block ones. Both spill past one block once the padding is added, which
    // is what makes them worth hashing here at all — a one-block-only implementation passes everything above.
    [InlineData("SHA-1", false, "84983e441c3bd26ebaae4aa1f95129e5e54670f1")]
    [InlineData("SHA-256", false, "248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1")]
    [InlineData("SHA-384", true, "09330c33f71147e83d192fc782cd1b4753111b173b3b05d22fa08086e3b0f712fcc7c71a557e2db966c3e9fa91746039")]
    [InlineData("SHA-512", true, "8e959b75dae313da8cf4f72814fc143f8f7779c6eb9f7fa17299aeadb6889018501d289e4900f7e4331b99dec4b5433ac7d329eeb6dd26545e96e55b874be909")]
    public void ProducesThePublishedVectorsForAMultiBlockMessage(string algorithm, bool longMessage, string expected)
    {
        DigestHex($"'{algorithm}'", longMessage ? TwoBlockLongMessage : TwoBlockShortMessage).Should().Be(expected);
    }

    [Theory]
    [InlineData("'SHA-256'")]
    [InlineData("{ name: 'SHA-256' }")]
    [InlineData("{ get name() { return 'SHA-256'; } }")]
    [InlineData("{ name: 'SHA-256', extraneous: 'ignored' }")]
    public void AcceptsBothShapesOfAlgorithmIdentifier(string algorithmExpression)
    {
        // `typedef (object or DOMString) AlgorithmIdentifier` — a string is normalized into an Algorithm
        // dictionary whose name is that string, and an object has its `name` member read.
        DigestHex(algorithmExpression, "abc")
            .Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Theory]
    [InlineData("sha-256")]
    [InlineData("SHA-256")]
    [InlineData("Sha-256")]
    [InlineData("sHa-256")]
    public void MatchesTheRegisteredNameCaseInsensitively(string spelling)
    {
        // "If registeredAlgorithms contains a key that is a case-insensitive string match for algName" —
        // and the digest operation then matches the *registered* key case-sensitively, so normalization is
        // the only thing that lets a lowercase spelling reach SHA-256 at all.
        DigestHex($"'{spelling}'", "abc")
            .Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void CaseInsensitivityIsAsciiOnly()
    {
        var engine = WebEngine();

        // https://w3c.github.io/webcrypto/#case-insensitive defines the comparison as ASCII case-insensitive.
        // U+017F LATIN SMALL LETTER LONG S uppercases to 'S' under Unicode's simple mapping, and must still
        // not match the registered key. Built from its code point rather than typed, so that no editor or
        // encoding on the way in can quietly turn it back into a plain 's'.
        Settle(engine, """
            crypto.subtle.digest(String.fromCharCode(0x17F) + 'HA-256', new Uint8Array(0))
                .then(() => 'resolved', e => e.name)
            """).AsString().Should().Be("NotSupportedError");
    }

    [Theory]
    [InlineData("'MD5'")]
    [InlineData("'SHA-224'")]
    [InlineData("'SHA3-256'")]
    [InlineData("''")]
    [InlineData("'SHA-256 '")]
    [InlineData("' SHA-256'")]
    [InlineData("'SHA256'")]
    [InlineData("undefined")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("{ name: 'MD5' }")]
    public void RejectsAnUnregisteredAlgorithmWithANotSupportedError(string algorithmExpression)
    {
        var engine = WebEngine();

        // "Otherwise: Return a new NotSupportedError and terminate this algorithm", which step 3 turns into
        // a rejection. A non-object identifier is stringified first, so `undefined` and `42` arrive here as
        // the names "undefined" and "42" rather than failing differently.
        Settle(engine, $$"""
            crypto.subtle.digest({{algorithmExpression}}, new Uint8Array(0))
                .then(() => 'resolved', e => [e instanceof DOMException, e.name, e.code].join('|'))
            """).AsString().Should().Be("true|NotSupportedError|9");
    }

    [Fact]
    public void ANotSupportedErrorIsCatchableInScript()
    {
        var engine = WebEngine();

        // The whole point of a rejection: `try`/`catch` around an `await` sees it, and the engine does not
        // erupt into the host.
        Settle(engine, """
            (async () => {
                try {
                    await crypto.subtle.digest('MD5', new Uint8Array(0));
                    return 'no error';
                } catch (e) {
                    return e.name + ': ' + (e instanceof DOMException);
                }
            })()
            """).AsString().Should().Be("NotSupportedError: true");
    }

    [Theory]
    [InlineData("'abc'")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{}")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("undefined")]
    [InlineData("new Uint8Array(4).buffer.constructor")]
    public void RejectsSomethingThatIsNotABufferSourceWithATypeError(string dataExpression)
    {
        var engine = WebEngine();

        // `BufferSource data` is `(ArrayBufferView or ArrayBuffer)`; anything else fails WebIDL's own
        // conversion, which is a TypeError — and a promise-returning operation turns that into a rejection
        // rather than a throw.
        Settle(engine, $$"""
            crypto.subtle.digest('SHA-256', {{dataExpression}})
                .then(() => 'resolved', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");
    }

    [Fact]
    public void TheDataArgumentIsConvertedBeforeTheAlgorithmIsNormalized()
    {
        var engine = WebEngine();

        // WebIDL converts every argument before a single step of the method body runs, and normalization is
        // step 2 of the body. So a bad `data` outranks an unregistered algorithm: this is a TypeError, not
        // the NotSupportedError 'nonsense' would earn on its own.
        Settle(engine, """
            crypto.subtle.digest('nonsense', 42).then(() => 'resolved', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");

        // ... and with a valid `data` the very same algorithm name does earn it.
        Settle(engine, """
            crypto.subtle.digest('nonsense', new Uint8Array(0)).then(() => 'resolved', e => e.name)
            """).AsString().Should().Be("NotSupportedError");
    }

    [Fact]
    public void RejectsAnAlgorithmObjectWithNoNameWithATypeError()
    {
        var engine = WebEngine();

        // Converting the object to the IDL dictionary `Algorithm { required DOMString name; }`: a member
        // that reads as undefined is the TypeError WebIDL raises for a missing required member, not a
        // NotSupportedError for the name "undefined".
        Settle(engine, """
            crypto.subtle.digest({}, new Uint8Array(0)).then(() => 'resolved', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");

        Settle(engine, """
            crypto.subtle.digest({ name: undefined }, new Uint8Array(0)).then(() => 'resolved', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");
    }

    [Fact]
    public void RejectsASymbolWhereAStringIsExpectedWithATypeError()
    {
        var engine = WebEngine();

        // `(object or DOMString)` stringifies anything that is not an object, and a symbol refuses to be
        // stringified. It reaches the engine as its no-engine-at-hand TypeError shape rather than as a
        // JavaScript error value, which is a second door into this method and has to end in a rejection too.
        Settle(engine, """
            crypto.subtle.digest(Symbol('nope'), new Uint8Array(0)).then(() => 'resolved', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");

        // The same door, one level in: the `name` member of an algorithm object is a DOMString as well.
        Settle(engine, """
            crypto.subtle.digest({ name: Symbol('nope') }, new Uint8Array(0))
                .then(() => 'resolved', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");
    }

    [Fact]
    public void AnErrorFromTheNameGetterBecomesTheRejection()
    {
        var engine = WebEngine();

        // The dictionary conversion performs one Get, which may run script. Whatever it throws is what the
        // promise rejects with — the operation still never throws to its caller.
        Settle(engine, """
            crypto.subtle.digest({ get name() { throw new RangeError('from the getter'); } }, new Uint8Array(0))
                .then(() => 'resolved', e => e.constructor.name + ': ' + e.message)
            """).AsString().Should().Be("RangeError: from the getter");
    }

    [Fact]
    public void ReadsTheNameMemberExactlyOnce()
    {
        var engine = WebEngine();

        // One Get is the whole of the dictionary conversion; a second read would be a second chance for a
        // script's getter to change the answer between the check and the hash.
        Settle(engine, """
            (async () => {
                let reads = 0;
                const algorithm = { get name() { reads++; return 'SHA-256'; } };
                await crypto.subtle.digest(algorithm, new Uint8Array(0));
                return reads;
            })()
            """).AsNumber().Should().Be(1);
    }

    [Fact]
    public void NeverThrowsSynchronously()
    {
        var engine = WebEngine();

        // Every failure is a rejection, so the call itself always answers a promise — even when both
        // arguments are nonsense and even when the receiver is wrong.
        engine.Evaluate("""
            (() => {
                const results = [
                    crypto.subtle.digest('nope', 'nope'),
                    crypto.subtle.digest(Symbol.iterator, new Uint8Array(0)),
                    crypto.subtle.digest.call({}, 'SHA-256', new Uint8Array(0)),
                ];
                results.forEach(p => p.catch(() => {}));
                return results.every(p => p instanceof Promise);
            })()
            """).AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void BrandChecksItsReceiverAsARejection()
    {
        var engine = WebEngine();

        // https://webidl.spec.whatwg.org/#dfn-create-operation-function puts the brand check inside the same
        // try whose exception a promise-returning operation converts into a rejection, so an extracted
        // `digest` called on the wrong object rejects rather than throws.
        Settle(engine, """
            crypto.subtle.digest.call({}, 'SHA-256', new Uint8Array(0))
                .then(() => 'resolved', e => e.constructor.name)
            """).AsString().Should().Be("TypeError");

        // ... and still works when the receiver is the real one, however it was reached.
        Settle(engine, """
            (() => {
                const digest = crypto.subtle.digest;
                return digest.call(crypto.subtle, 'SHA-256', new Uint8Array(0)).then(d => d.byteLength);
            })()
            """).AsNumber().Should().Be(32);
    }

    [Fact]
    public void TheSubtleGetterBrandChecksItsReceiverAsAThrow()
    {
        var engine = WebEngine();

        // An attribute is not a promise-returning operation, so its brand check is an ordinary throw.
        engine.Evaluate("""
            (() => {
                const getter = Object.getOwnPropertyDescriptor(crypto, 'subtle').get;
                try { getter.call({}); } catch (e) { return e.constructor.name; }
                return 'no error';
            })()
            """).AsString().Should().Be("TypeError");
    }

    [Fact]
    public void ADetachedBufferHashesAsTheEmptyMessage()
    {
        var engine = WebEngine();

        // https://webidl.spec.whatwg.org/#dfn-get-buffer-source-copy: "If IsDetachedBuffer(jsArrayBuffer) is
        // true, then return the empty byte sequence" — so this is the digest of nothing, not a failure.
        Settle(engine, """
            (() => {
                const buffer = new ArrayBuffer(8);
                const view = new Uint8Array(buffer);
                buffer.transfer();
                return crypto.subtle.digest('SHA-256', view)
                    .then(d => Array.from(new Uint8Array(d)).map(b => b.toString(16).padStart(2, '0')).join(''));
            })()
            """).AsString().Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }

    [Fact]
    public void AnOutOfBoundsLengthTrackingViewHashesAsTheEmptyMessage()
    {
        var engine = WebEngine();

        // Same answer through the other door: a length-tracking view whose resizable buffer has shrunk past
        // its offset spans no bytes at all.
        Settle(engine, """
            (() => {
                const buffer = new ArrayBuffer(8, { maxByteLength: 16 });
                const view = new Uint8Array(buffer, 4);
                buffer.resize(2);
                return crypto.subtle.digest('SHA-256', view).then(d => d.byteLength + ':' + view.length);
            })()
            """).AsString().Should().Be("32:0");
    }

    [Fact]
    public void HashesOnlyTheBytesTheViewSpans()
    {
        var engine = WebEngine();

        // A view with a byte offset is a window, not the whole buffer: only 'abc' is hashed here, and the
        // bytes on either side of it must not reach the hash function.
        Settle(engine, """
            (() => {
                const buffer = new Uint8Array([0xFF, 0xFF, 0x61, 0x62, 0x63, 0xFF]).buffer;
                return crypto.subtle.digest('SHA-256', new Uint8Array(buffer, 2, 3))
                    .then(d => Array.from(new Uint8Array(d)).map(b => b.toString(16).padStart(2, '0')).join(''));
            })()
            """).AsString().Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Theory]
    [InlineData("new Uint8Array([0x61, 0x62, 0x63])")]
    [InlineData("new Uint8Array([0x61, 0x62, 0x63]).buffer")]
    [InlineData("new DataView(new Uint8Array([0x61, 0x62, 0x63]).buffer)")]
    [InlineData("new Int8Array([0x61, 0x62, 0x63])")]
    [InlineData("new Uint16Array([0x6261, 0x0063])")]
    public void AcceptsEveryShapeOfBufferSource(string dataExpression)
    {
        var engine = WebEngine();

        // `BufferSource` is "an ArrayBuffer, or any view over one" — the element type of a view is not
        // consulted, only the bytes underneath it. The Uint16Array spells the same three little-endian bytes
        // plus a trailing zero, so it is the one entry here that hashes four bytes rather than three.
        var expected = dataExpression.Contains("Uint16Array", StringComparison.Ordinal)
            ? "dc1114cd074914bd872cc1f9a23ec910ea2203bc79779ab2e17da25782a624fc"
            : "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

        Settle(engine, $$"""
            crypto.subtle.digest('SHA-256', {{dataExpression}})
                .then(d => Array.from(new Uint8Array(d)).map(b => b.toString(16).padStart(2, '0')).join(''))
            """).AsString().Should().Be(expected);
    }

    [Fact]
    public void RejectsABufferSourceBackedByASharedArrayBufferWithATypeError()
    {
        var engine = WebEngine();

        // The IDL is `BufferSource`, not `AllowSharedBufferSource`, and WebIDL refuses a shared buffer for
        // any type not carrying [AllowShared] — the rule crypto.getRandomValues refuses one under.
        foreach (var expression in new[] { "new Uint8Array(new SharedArrayBuffer(8))", "new SharedArrayBuffer(8)" })
        {
            Settle(engine, $$"""
                crypto.subtle.digest('SHA-256', {{expression}}).then(() => 'resolved', e => e.constructor.name)
                """).AsString().Should().Be("TypeError");
        }
    }

    [Fact]
    public void AnswersARealPromiseThatSettlesOnAMicrotaskTurn()
    {
        var engine = WebEngine();

        // The work is synchronous, but the promise is a promise: its reaction runs on the microtask turn,
        // after the rest of the current script.
        engine.Evaluate("""
            (() => {
                const order = [];
                crypto.subtle.digest('SHA-256', new Uint8Array(0)).then(() => order.push('digest'));
                order.push('sync');
                return Promise.resolve().then(() => order.join(','));
            })()
            """).UnwrapIfPromise().AsString().Should().Be("sync,digest");
    }

    [Fact]
    public void ResolvesWithAnArrayBufferOfTheRealmsOwnIntrinsic()
    {
        var engine = WebEngine();

        Settle(engine, """
            crypto.subtle.digest('SHA-384', new Uint8Array(0)).then(d =>
                [d instanceof ArrayBuffer, Object.getPrototypeOf(d) === ArrayBuffer.prototype, d.byteLength].join('|'))
            """).AsString().Should().Be("true|true|48");
    }

    [Fact]
    public void EachCallResolvesWithAFreshBufferTheScriptMayWriteTo()
    {
        var engine = WebEngine();

        // The digest crosses into script, where it is mutable, so two calls must never share bytes.
        Settle(engine, """
            (async () => {
                const first = await crypto.subtle.digest('SHA-256', new Uint8Array(0));
                new Uint8Array(first)[0] = 0;
                const second = await crypto.subtle.digest('SHA-256', new Uint8Array(0));
                return (first !== second) + '|' + new Uint8Array(second)[0];
            })()
            """).AsString().Should().Be("true|227");
    }

    [Fact]
    public void IsOneStableObjectWithTheInterfacesToStringTag()
    {
        var engine = WebEngine();

        engine.Evaluate("crypto.subtle === crypto.subtle").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(crypto.subtle)").AsString().Should().Be("[object SubtleCrypto]");
        engine.Evaluate("crypto.subtle[Symbol.toStringTag]").AsString().Should().Be("SubtleCrypto");
        engine.Evaluate("typeof crypto.subtle").AsString().Should().Be("object");
    }

    [Fact]
    public void IsAReadOnlyNonEnumerableAttributeOfTheCryptoObject()
    {
        var engine = WebEngine();

        engine.Evaluate("'subtle' in crypto").AsBoolean().Should().BeTrue();

        // There is no Crypto.prototype here, so the attribute is an own accessor of the object with the
        // attributes an ECMAScript built-in member carries. Object.keys(crypto) stays empty, as in a browser.
        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(crypto, 'subtle')").AsObject();
        descriptor.Get("get").IsCallable.Should().BeTrue();
        descriptor.Get("set").IsUndefined().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeFalse();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();

        engine.Evaluate("JSON.stringify(Object.keys(crypto))").AsString().Should().Be("[]");
    }

    [Fact]
    public void ExposesDigestAndTheKeyedOperationsButNotKeyDerivation()
    {
        var engine = WebEngine();

        // Absent rather than present-and-throwing: a library checking `typeof crypto.subtle.deriveBits`
        // before reaching for it has to get the truthful answer, which is the same reason `crypto.subtle`
        // itself is absent from an engine without the crypto feature.
        engine.Evaluate("typeof crypto.subtle.digest").AsString().Should().Be("function");

        engine.Evaluate("""
            ['deriveKey', 'deriveBits', 'wrapKey', 'unwrapKey'].filter(name => name in crypto.subtle).join(',')
            """).AsString().Should().Be("");

        // As with crypto itself, the operation is an own property with a built-in method's attributes, so
        // enumeration sees nothing.
        engine.Evaluate("JSON.stringify(Object.keys(crypto.subtle))").AsString().Should().Be("[]");

        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(crypto.subtle, 'digest')").AsObject();
        descriptor.Get("writable").AsBoolean().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void HasTheIdlArity()
    {
        var engine = WebEngine();

        // WebIDL length counts the required arguments only, and digest declares two.
        engine.Evaluate("crypto.subtle.digest.length").AsNumber().Should().Be(2);
        engine.Evaluate("crypto.subtle.digest.name").AsString().Should().Be("digest");
    }

    [Fact]
    public void IsNotInstalledWithoutTheCryptoFlag()
    {
        // subtle has no flag of its own: it is part of the Crypto interface, so it rides the crypto flag.
        new Engine(options => options.UseWebApis(WebApiFeatures.Console))
            .Evaluate("typeof crypto").AsString().Should().Be("undefined");

        // ... and does not reach into a shadow realm when it is enabled.
        WebEngine().Evaluate("new ShadowRealm().evaluate('typeof crypto')").AsString().Should().Be("undefined");
    }

    [Fact]
    public void TwoEnginesShareNoState()
    {
        var options = new Options().UseWebApis(WebApiFeatures.Crypto);

        var first = new Engine(options);
        var second = new Engine(options);

        // Each realm builds its own SubtleCrypto object, so a value from one engine is never the other's.
        first.Evaluate("crypto.subtle === crypto.subtle").AsBoolean().Should().BeTrue();
        second.Evaluate("crypto.subtle === crypto.subtle").AsBoolean().Should().BeTrue();
        Settle(first, "crypto.subtle.digest('SHA-1', new Uint8Array(0)).then(d => d.byteLength)")
            .AsNumber().Should().Be(20);
        Settle(second, "crypto.subtle.digest('SHA-512', new Uint8Array(0)).then(d => d.byteLength)")
            .AsNumber().Should().Be(64);
    }
}
#endif
