#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The <c>crypto</c> object against the Web Cryptography API — https://w3c.github.io/webcrypto/#crypto-interface.
/// </summary>
/// <remarks>
/// Randomness makes an exact assertion impossible, so the shape of every "it filled the array" test is that
/// enough bytes were written for all of them being zero to be impossible in practice: a 32-byte array is
/// 2^-256 to come back untouched. What <i>is</i> asserted exactly is everything around the randomness —
/// which views are accepted, which failure each rejection is, the quota boundary, the bytes outside a view's
/// own slice, and the UUID's shape.
/// </remarks>
public class CryptoTests
{
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Crypto));

    [Theory]
    [InlineData("Int8Array")]
    [InlineData("Uint8Array")]
    [InlineData("Uint8ClampedArray")]
    [InlineData("Int16Array")]
    [InlineData("Uint16Array")]
    [InlineData("Int32Array")]
    [InlineData("Uint32Array")]
    [InlineData("BigInt64Array")]
    [InlineData("BigUint64Array")]
    public void FillsEveryIntegerViewTypeTheAlgorithmAccepts(string type)
    {
        var engine = WebEngine();

        // Step 1's list, verbatim: "an Int8Array, Uint8Array, Uint8ClampedArray, Int16Array, Uint16Array,
        // Int32Array, Uint32Array, BigInt64Array, or BigUint64Array".
        engine.Evaluate($$"""
            (() => {
                const array = new {{type}}(32);
                const returned = crypto.getRandomValues(array);
                const zero = typeof array[0] === 'bigint' ? 0n : 0;
                return returned === array && array.some(v => v !== zero);
            })()
            """).AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ReturnsTheVeryArrayItWasGiven()
    {
        var engine = WebEngine();

        // Step 7: "Return array" — the same object, never a copy.
        engine.Evaluate("(() => { const a = new Uint8Array(8); return crypto.getRandomValues(a) === a; })()")
            .AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void WritesOnlyIntoTheViewsOwnSlice()
    {
        var engine = WebEngine();

        // A view with a byte offset must have exactly its own window filled: the bytes on either side of it
        // in the same buffer belong to nobody here and must be left alone.
        engine.Evaluate("""
            (() => {
                const buffer = new ArrayBuffer(16);
                crypto.getRandomValues(new Uint8Array(buffer, 4, 8));
                const all = new Uint8Array(buffer);
                const outsideUntouched = all.slice(0, 4).every(v => v === 0) && all.slice(12).every(v => v === 0);
                const insideWritten = all.slice(4, 12).some(v => v !== 0);
                return outsideUntouched && insideWritten;
            })()
            """).AsBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("new Float16Array(4)")]
    [InlineData("new Float32Array(4)")]
    [InlineData("new Float64Array(4)")]
    [InlineData("new DataView(new ArrayBuffer(8))")]
    public void RejectsAViewThatIsNotAnIntegerArrayWithATypeMismatchError(string expression)
    {
        var engine = WebEngine();

        // Step 1: a float array and a DataView are both ArrayBufferViews, so they pass WebIDL's conversion
        // and fail the method's own first step — a DOMException, not a TypeError.
        engine.Evaluate($$"""
            (() => {
                try { crypto.getRandomValues({{expression}}); }
                catch (e) { return [e instanceof DOMException, e.name, e.code].join('|'); }
                return 'no error';
            })()
            """).AsString().Should().Be("true|TypeMismatchError|17");
    }

    [Theory]
    [InlineData("[1, 2, 3]")]
    [InlineData("{}")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("undefined")]
    [InlineData("'not a view'")]
    [InlineData("new ArrayBuffer(8)")]
    public void RejectsSomethingThatIsNotAViewAtAllWithATypeError(string expression)
    {
        var engine = WebEngine();

        // The IDL is getRandomValues(ArrayBufferView array): anything that is not a view fails WebIDL's own
        // conversion, which is a TypeError and never a DOMException. An ArrayBuffer is not a view.
        var thrown = Assert.Throws<JavaScriptException>(() => engine.Evaluate($"crypto.getRandomValues({expression})"));
        thrown.Error.Get("name").AsString().Should().Be("TypeError");
        thrown.Message.Should().Contain("ArrayBufferView");
    }

    [Fact]
    public void RejectsAViewOntoASharedArrayBufferWithATypeError()
    {
        var engine = WebEngine();

        // https://webidl.spec.whatwg.org/#es-arraybufferview — a shared view is accepted only where the
        // operation declares [AllowShared], and the Crypto IDL does not.
        var thrown = Assert.Throws<JavaScriptException>(
            () => engine.Evaluate("crypto.getRandomValues(new Uint8Array(new SharedArrayBuffer(8)))"));

        thrown.Error.Get("name").AsString().Should().Be("TypeError");
        thrown.Message.Should().Contain("SharedArrayBuffer");
    }

    [Fact]
    public void AcceptsExactlyTheQuotaAndRefusesOneByteMore()
    {
        var engine = WebEngine();

        // Step 3: "If byteLength is greater than 65536, throw a QuotaExceededError" — so 65536 itself is fine.
        engine.Evaluate("crypto.getRandomValues(new Uint8Array(65536)).byteLength").AsNumber().Should().Be(65536);

        engine.Evaluate("""
            (() => {
                try { crypto.getRandomValues(new Uint8Array(65537)); }
                catch (e) { return [e instanceof DOMException, e.name, e.code].join('|'); }
                return 'no error';
            })()
            """).AsString().Should().Be("true|QuotaExceededError|22");
    }

    [Fact]
    public void CountsTheQuotaInBytesRatherThanInElements()
    {
        var engine = WebEngine();

        // 32768 Uint16 elements are 65536 bytes and fit; one more element is 65538 bytes and does not.
        engine.Evaluate("crypto.getRandomValues(new Uint16Array(32768)).length").AsNumber().Should().Be(32768);

        engine.Evaluate("""
            (() => {
                try { crypto.getRandomValues(new Uint16Array(32769)); }
                catch (e) { return e.name; }
                return 'no error';
            })()
            """).AsString().Should().Be("QuotaExceededError");
    }

    [Fact]
    public void AnEmptyViewIsReturnedUntouched()
    {
        var engine = WebEngine();

        engine.Evaluate("(() => { const a = new Uint8Array(0); return crypto.getRandomValues(a) === a; })()")
            .AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ADetachedViewIsReturnedUntouched()
    {
        var engine = WebEngine();

        // A detached view's byte length is zero, so step 4's byte sequence is empty and step 6 writes it into
        // nothing at all. The quota is a maximum, not a minimum, so there is nothing here to raise about.
        engine.Evaluate("""
            (() => {
                const buffer = new ArrayBuffer(8);
                const array = new Uint8Array(buffer);
                buffer.transfer();
                return crypto.getRandomValues(array) === array && array.length === 0;
            })()
            """).AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void AnOutOfBoundsLengthTrackingViewIsReturnedUntouched()
    {
        var engine = WebEngine();

        // Same reasoning through the other door: a length-tracking view whose resizable buffer has shrunk
        // past its offset is out of bounds, and the buffer witness reports its byte length as zero.
        engine.Evaluate("""
            (() => {
                const buffer = new ArrayBuffer(8, { maxByteLength: 16 });
                const array = new Uint8Array(buffer, 4);
                buffer.resize(2);
                return crypto.getRandomValues(array) === array && array.length === 0;
            })()
            """).AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void RefusesToWriteIntoAnImmutableBuffer()
    {
        var engine = WebEngine();

        // Building the view over an immutable buffer is fine — it is reading it that the proposal allows and
        // writing it that it does not, so the refusal below is this operation's and not the constructor's.
        engine.Evaluate("new Uint8Array(new ArrayBuffer(8).transferToImmutable()).length").AsNumber().Should().Be(8);

        // https://tc39.es/proposal-immutable-arraybuffer/ — the same TypeError an ordinary element assignment
        // raises, because this is an ordinary write into the same bytes.
        var thrown = Assert.Throws<JavaScriptException>(() => engine.Evaluate("""
            crypto.getRandomValues(new Uint8Array(new ArrayBuffer(8).transferToImmutable()))
            """));

        thrown.Error.Get("name").AsString().Should().Be("TypeError");
        thrown.Message.Should().Contain("immutable");
    }

    [Fact]
    public void ProducesAVersion4LowercaseUuid()
    {
        var engine = WebEngine();

        // The last step's concatenation, with the version nibble step 3 sets and the variant bits step 4
        // sets: 8-4-4-4-12 lowercase hex, a '4' opening the third group and one of 8/9/a/b the fourth.
        engine.Evaluate("""
            (() => {
                const pattern = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
                for (let i = 0; i < 100; i++) {
                    if (!pattern.test(crypto.randomUUID())) return 'bad: ' + crypto.randomUUID();
                }
                return 'ok';
            })()
            """).AsString().Should().Be("ok");
    }

    [Fact]
    public void ProducesADifferentUuidEveryTime()
    {
        var engine = WebEngine();

        engine.Evaluate("new Set(Array.from({ length: 500 }, () => crypto.randomUUID())).size")
            .AsNumber().Should().Be(500);
    }

    [Fact]
    public void BrandChecksBothOperations()
    {
        var engine = WebEngine();

        // The WebIDL brand check: an extracted operation cannot be called on anything but a Crypto object.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("crypto.getRandomValues.call({}, new Uint8Array(4))"))
            .Message.Should().Contain("Crypto");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("crypto.randomUUID.call(undefined)"))
            .Message.Should().Contain("Crypto");

        // ... and still works when the receiver is the real one, however it was reached.
        engine.Evaluate("(() => { const f = crypto.getRandomValues; return f.call(crypto, new Uint8Array(4)).length; })()")
            .AsNumber().Should().Be(4);
    }

    [Fact]
    public void HasASubtleCryptoCarryingTheOperationsThisEngineImplements()
    {
        var engine = WebEngine();

        // `crypto.subtle` exists and answers a SubtleCrypto object. What it carries is the ten operations
        // over SHA, HMAC, AES-GCM, the RSA family, the elliptic curves, HKDF and PBKDF2; key wrapping is
        // absent rather than present-and-throwing, because `if (crypto.subtle.wrapKey)` is how a library that
        // does cryptography decides whether it can, and it has to get the truthful answer — see
        // SubtleCryptoTests and SubtleCryptoKeyTests for the operations themselves.
        engine.Evaluate("typeof crypto.subtle").AsString().Should().Be("object");
        engine.Evaluate("'subtle' in crypto").AsBoolean().Should().BeTrue();

        engine.Evaluate("""
            ['digest', 'encrypt', 'decrypt', 'sign', 'verify', 'generateKey', 'importKey', 'exportKey', 'deriveBits', 'deriveKey']
                .map(name => typeof crypto.subtle[name]).join(',')
            """).AsString().Should().Be(
                "function,function,function,function,function,function,function,function,function,function");

        engine.Evaluate("""
            ['wrapKey', 'unwrapKey'].map(name => typeof crypto.subtle[name]).join(',')
            """).AsString().Should().Be("undefined,undefined");
    }

    [Fact]
    public void IsOneStableObjectWithTheInterfacesToStringTag()
    {
        var engine = WebEngine();

        engine.Evaluate("crypto === crypto").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(crypto)").AsString().Should().Be("[object Crypto]");
        engine.Evaluate("crypto[Symbol.toStringTag]").AsString().Should().Be("Crypto");
    }

    [Fact]
    public void GivesItsMembersTheAttributesOfBuiltInMethods()
    {
        var engine = WebEngine();

        // There is no Crypto.prototype here, so the operations are own properties of the object with the
        // attributes an ECMAScript built-in method carries. What a script can observe of that difference is
        // nothing: a browser answers the empty array for Object.keys(crypto) too, because there the
        // operations live on the prototype.
        engine.Evaluate("JSON.stringify(Object.keys(crypto))").AsString().Should().Be("[]");

        foreach (var member in new[] { "getRandomValues", "randomUUID" })
        {
            var descriptor = engine.Evaluate($"Object.getOwnPropertyDescriptor(crypto, '{member}')").AsObject();
            descriptor.Get("writable").AsBoolean().Should().BeTrue();
            descriptor.Get("configurable").AsBoolean().Should().BeTrue();
            descriptor.Get("enumerable").AsBoolean().Should().BeFalse();
        }
    }

    [Fact]
    public void HasTheIdlArities()
    {
        var engine = WebEngine();

        // WebIDL length counts the required arguments only.
        engine.Evaluate("crypto.getRandomValues.length").AsNumber().Should().Be(1);
        engine.Evaluate("crypto.randomUUID.length").AsNumber().Should().Be(0);
        engine.Evaluate("crypto.getRandomValues.name").AsString().Should().Be("getRandomValues");
        engine.Evaluate("crypto.randomUUID.name").AsString().Should().Be("randomUUID");
    }

    [Fact]
    public void IsNotInstalledWithoutItsFlag()
    {
        new Engine(options => options.UseWebApis(WebApiFeatures.Console))
            .Evaluate("typeof crypto").AsString().Should().Be("undefined");

        // ... and does not reach into a shadow realm when it is.
        WebEngine().Evaluate("new ShadowRealm().evaluate('typeof crypto')").AsString().Should().Be("undefined");
    }
}
#endif
