#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>TextEncoder</c> and <c>TextDecoder</c> as the Encoding Standard specifies them —
/// https://encoding.spec.whatwg.org/#api.
/// </summary>
public class EncodingTests
{
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Encoding));

    #region TextEncoder

    [Fact]
    public void TextEncoderIsUtf8AndNothingElse()
    {
        var engine = WebEngine();

        // "The encoding is always utf-8" — https://encoding.spec.whatwg.org/#dom-textencoder-encoding.
        engine.Evaluate("new TextEncoder().encoding").AsString().Should().Be("utf-8");
        engine.Evaluate("TextEncoder.length").AsNumber().Should().Be(0);
        engine.Evaluate("TextEncoder.name").AsString().Should().Be("TextEncoder");
    }

    [Fact]
    public void EncodeDefaultsToTheEmptyString()
    {
        var engine = WebEngine();

        engine.Evaluate("new TextEncoder().encode().length").AsNumber().Should().Be(0);
        engine.Evaluate("new TextEncoder().encode(undefined).length").AsNumber().Should().Be(0);
        engine.Evaluate("new TextEncoder().encode('') instanceof Uint8Array").AsBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("hi", "104,105")]
    [InlineData("é", "195,169")]
    [InlineData("€", "226,130,172")]
    [InlineData("😀", "240,159,152,128")]
    public void EncodeProducesUtf8(string input, string expected)
    {
        var engine = WebEngine();

        engine.SetValue("input", input);
        engine.Evaluate("new TextEncoder().encode(input).join(',')").AsString().Should().Be(expected);
    }

    [Fact]
    public void EncodeReplacesAnUnpairedSurrogate()
    {
        var engine = WebEngine();

        // The argument is a USVString, so a lone surrogate becomes U+FFFD before it is ever encoded.
        engine.Evaluate("new TextEncoder().encode('\\uD800').join(',')").AsString().Should().Be("239,191,189");
        engine.Evaluate("new TextEncoder().encode('\\uDC00').join(',')").AsString().Should().Be("239,191,189");
        engine.Evaluate("new TextEncoder().encode('a\\uD800b').join(',')").AsString().Should().Be("97,239,191,189,98");
    }

    [Fact]
    public void EncodeIntoReportsCodeUnitsReadAndBytesWritten()
    {
        var engine = WebEngine();

        var result = engine.Evaluate("""
            const r = new TextEncoder().encodeInto('hi€', new Uint8Array(16));
            [r.read, r.written].join(',');
            """);

        // Three code units in, five bytes out.
        result.AsString().Should().Be("3,5");
    }

    [Fact]
    public void EncodeIntoCountsASurrogatePairAsTwoCodeUnits()
    {
        var engine = WebEngine();

        engine.Evaluate("""
            const pair = new TextEncoder().encodeInto('😀', new Uint8Array(8));
            [pair.read, pair.written].join(',');
            """).AsString().Should().Be("2,4");

        // ... and the U+FFFD an unpaired surrogate becomes as one, since it is one code unit.
        engine.Evaluate("""
            const lone = new TextEncoder().encodeInto('\uD800', new Uint8Array(8));
            [lone.read, lone.written].join(',');
            """).AsString().Should().Be("1,3");
    }

    [Fact]
    public void EncodeIntoNeverSplitsACodePoint()
    {
        var engine = WebEngine();

        // Three bytes are needed for '€' and only two are left, so nothing at all is written for it.
        engine.Evaluate("""
            const tooSmall = new Uint8Array(3);
            const partial = new TextEncoder().encodeInto('a€', tooSmall);
            [partial.read, partial.written, tooSmall.join(',')].join('|');
            """).AsString().Should().Be("1|1|97,0,0");

        // With exactly enough room it fits.
        engine.Evaluate("""
            const exact = new Uint8Array(4);
            const full = new TextEncoder().encodeInto('a€', exact);
            [full.read, full.written, exact.join(',')].join('|');
            """).AsString().Should().Be("2|4|97,226,130,172");
    }

    [Fact]
    public void EncodeIntoWritesThroughAViewsOwnWindow()
    {
        var engine = WebEngine();

        engine.Evaluate("""
            const buffer = new Uint8Array(6);
            const window = buffer.subarray(2, 5);
            const r = new TextEncoder().encodeInto('abcd', window);
            [r.read, r.written, buffer.join(',')].join('|');
            """).AsString().Should().Be("3|3|0,0,97,98,99,0");
    }

    [Fact]
    public void EncodeIntoRefusesADestinationThatIsNotAUint8Array()
    {
        var engine = WebEngine();

        foreach (var destination in new[] { "new Uint16Array(8)", "new Uint8ClampedArray(8)", "new ArrayBuffer(8)", "[]", "undefined" })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"new TextEncoder().encodeInto('a', {destination})"))
                .Error.Get("name").AsString().Should().Be("TypeError");
        }
    }

    [Fact]
    public void TextEncoderRequiresNewAndBrandChecksItsReceiver()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("TextEncoder()"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("TextEncoder.prototype.encode.call({})"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Object.getOwnPropertyDescriptor(TextEncoder.prototype, 'encoding').get.call({})"));

        // The prototype is not itself a TextEncoder, which is what a brand check means.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("TextEncoder.prototype.encoding"));
    }

    #endregion

    #region TextDecoder labels

    [Fact]
    public void TextDecoderDefaultsToUtf8()
    {
        var engine = WebEngine();

        engine.Evaluate("new TextDecoder().encoding").AsString().Should().Be("utf-8");
        engine.Evaluate("new TextDecoder().fatal").AsBoolean().Should().BeFalse();
        engine.Evaluate("new TextDecoder().ignoreBOM").AsBoolean().Should().BeFalse();
        engine.Evaluate("new TextDecoder(undefined, undefined).encoding").AsString().Should().Be("utf-8");
    }

    [Theory]
    // https://encoding.spec.whatwg.org/#names-and-labels, the UTF-8 row.
    [InlineData("utf-8", "utf-8")]
    [InlineData("utf8", "utf-8")]
    [InlineData("UTF-8", "utf-8")]
    [InlineData("unicode-1-1-utf-8", "utf-8")]
    [InlineData("unicode11utf8", "utf-8")]
    [InlineData("unicode20utf8", "utf-8")]
    [InlineData("x-unicode20utf8", "utf-8")]
    // The UTF-16LE row — note that a bare "utf-16" is little-endian.
    [InlineData("utf-16", "utf-16le")]
    [InlineData("utf-16le", "utf-16le")]
    [InlineData("UTF-16LE", "utf-16le")]
    [InlineData("unicode", "utf-16le")]
    [InlineData("unicodefeff", "utf-16le")]
    [InlineData("csunicode", "utf-16le")]
    [InlineData("iso-10646-ucs-2", "utf-16le")]
    [InlineData("ucs-2", "utf-16le")]
    // The UTF-16BE row.
    [InlineData("utf-16be", "utf-16be")]
    [InlineData("unicodefffe", "utf-16be")]
    // "Get an encoding" strips leading and trailing ASCII whitespace first.
    [InlineData("  utf-8\t\n", "utf-8")]
    [InlineData("\r\futf8 ", "utf-8")]
    public void ResolvesALabelToItsEncodingName(string label, string expected)
    {
        var engine = WebEngine();

        engine.SetValue("label", label);
        engine.Evaluate("new TextDecoder(label).encoding").AsString().Should().Be(expected);
    }

    [Theory]
    // The legacy encodings have a file of their own, LegacyEncodingTests. What is left here is the labels
    // that name nothing at all, plus the two kinds the constructor refuses even though the label is a label:
    // the replacement encoding, which the specification refuses, and the multi-byte encodings, which Jint
    // does not implement.
    [InlineData("")]
    [InlineData("utf-9")]
    [InlineData("utf-32")]
    [InlineData("shift_jis")]
    [InlineData("replacement")]
    // ASCII case-insensitive matching and nothing more: U+017F must not fold onto 's'.
    [InlineData("c\u017Funicode")]
    // U+000B VT is not ASCII whitespace, so it is not stripped.
    [InlineData("\u000Butf-8")]
    public void RefusesALabelItCannotDecode(string label)
    {
        var engine = WebEngine();

        engine.SetValue("label", label);
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new TextDecoder(label)"))
            .Error.Get("name").AsString().Should().Be("RangeError");
    }

    [Fact]
    public void ReadsTheOptionsDictionary()
    {
        var engine = WebEngine();

        engine.Evaluate("new TextDecoder('utf-8', { fatal: true }).fatal").AsBoolean().Should().BeTrue();
        engine.Evaluate("new TextDecoder('utf-8', { ignoreBOM: true }).ignoreBOM").AsBoolean().Should().BeTrue();

        // Dictionary members are coerced with ToBoolean, and an absent one takes its default.
        engine.Evaluate("new TextDecoder('utf-8', { fatal: 1 }).fatal").AsBoolean().Should().BeTrue();
        engine.Evaluate("new TextDecoder('utf-8', { fatal: undefined }).fatal").AsBoolean().Should().BeFalse();
        engine.Evaluate("new TextDecoder('utf-8', null).fatal").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void ConvertsBothArgumentsBeforeTheLabelIsLookedUp()
    {
        var engine = WebEngine();

        // WebIDL converts the arguments and only then runs the constructor steps, so the getter runs even
        // though the label is about to be refused.
        var result = engine.Evaluate("""
            let seen = false;
            try {
                new TextDecoder('nonsense', { get fatal() { seen = true; return false; } });
            } catch (e) {
                seen + ':' + e.name;
            }
            """);

        result.AsString().Should().Be("true:RangeError");
    }

    #endregion

    #region TextDecoder decoding

    [Fact]
    public void DecodesEveryBufferSourceShape()
    {
        var engine = WebEngine();

        engine.Evaluate("new TextDecoder().decode(new Uint8Array([104, 105]))").AsString().Should().Be("hi");
        engine.Evaluate("new TextDecoder().decode(new Uint8Array([104, 105]).buffer)").AsString().Should().Be("hi");
        engine.Evaluate("new TextDecoder().decode(new DataView(new Uint8Array([104, 105]).buffer))").AsString().Should().Be("hi");

        // A view's own window, not the whole buffer.
        engine.Evaluate("new TextDecoder().decode(new Uint8Array([104, 105, 106, 107]).subarray(1, 3))").AsString().Should().Be("ij");
        engine.Evaluate("new TextDecoder().decode(new DataView(new Uint8Array([104, 105, 106, 107]).buffer, 2))").AsString().Should().Be("jk");
    }

    [Fact]
    public void DecodesNothingWhenGivenNothing()
    {
        var engine = WebEngine();

        engine.Evaluate("new TextDecoder().decode()").AsString().Should().Be("");
        engine.Evaluate("new TextDecoder().decode(undefined)").AsString().Should().Be("");
        engine.Evaluate("new TextDecoder().decode(new Uint8Array(0))").AsString().Should().Be("");
    }

    [Fact]
    public void CopiesTheInputOnlyOnceTheOptionsDictionaryHasBeenConverted()
    {
        var engine = WebEngine();

        // WebIDL converts both arguments before the operation runs, and
        // https://encoding.spec.whatwg.org/#dom-textdecoder-decode step 3 then pushes "a copy of input" onto
        // the I/O queue — so a `stream` getter that detaches the buffer during the dictionary conversion
        // leaves nothing to decode: https://webidl.spec.whatwg.org/#dfn-get-buffer-source-copy step 5 makes
        // a detached buffer source the empty byte sequence. Mirrors "TextDecoder decode() with array buffer
        // detached during arg conversion" in web-platform-tests encoding/textdecoder-arguments.any.js.
        engine.Evaluate("""
            const decoder = new TextDecoder();
            const arr = new Uint8Array(10000).fill(42);
            const result = decoder.decode(arr, { get stream() { arr.buffer.transfer(0); return false; } });
            """);

        engine.Evaluate("result").AsString().Should().Be("");

        // The getter still runs, and its answer is still the `stream` option.
        engine.Evaluate("""
            const other = new TextDecoder();
            const bytes = new Uint8Array([0x61, 0xE2, 0x82]);
            const streamed = other.decode(bytes, { get stream() { bytes.buffer.transfer(0); return true; } });
            const flushed = other.decode();
            """);

        engine.Evaluate("streamed").AsString().Should().Be("");
        engine.Evaluate("flushed").AsString().Should().Be("");
    }

    [Fact]
    public void RefusesAnInputThatIsNotABufferSource()
    {
        var engine = WebEngine();

        // The IDL type is not nullable, so null is not the same as omitting the argument.
        foreach (var input in new[] { "null", "'abc'", "42", "[]", "{}" })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"new TextDecoder().decode({input})"))
                .Error.Get("name").AsString().Should().Be("TypeError");
        }
    }

    [Fact]
    public void ReplacesInvalidUtf8WithU00FFFD()
    {
        var engine = WebEngine();

        engine.Evaluate("new TextDecoder().decode(new Uint8Array([0xFF])).charCodeAt(0)").AsNumber().Should().Be(0xFFFD);
        engine.Evaluate("new TextDecoder().decode(new Uint8Array([0x61, 0xC0, 0x62]))").AsString().Should().Be("a\uFFFDb");
    }

    [Fact]
    public void FatalTurnsInvalidInputIntoATypeError()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new TextDecoder('utf-8', { fatal: true }).decode(new Uint8Array([0xFF]))"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        // An incomplete sequence at the end of a non-streaming decode is invalid too, because the decode
        // flushes.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new TextDecoder('utf-8', { fatal: true }).decode(new Uint8Array([0xE2, 0x82]))"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        // ... and a decoder that threw is usable again.
        engine.Evaluate("""
            const decoder = new TextDecoder('utf-8', { fatal: true });
            try { decoder.decode(new Uint8Array([0xFF])); } catch (e) { }
            decoder.decode(new Uint8Array([104, 105]));
            """).AsString().Should().Be("hi");
    }

    [Fact]
    public void DecodesUtf16InBothEndiannesses()
    {
        var engine = WebEngine();

        engine.Evaluate("new TextDecoder('utf-16le').decode(new Uint8Array([0x68, 0x00, 0x69, 0x00]))").AsString().Should().Be("hi");
        engine.Evaluate("new TextDecoder('utf-16be').decode(new Uint8Array([0x00, 0x68, 0x00, 0x69]))").AsString().Should().Be("hi");

        // A surrogate pair, so the code unit order matters.
        engine.Evaluate("new TextDecoder('utf-16le').decode(new Uint8Array([0x3D, 0xD8, 0x00, 0xDE]))").AsString().Should().Be("😀");
        engine.Evaluate("new TextDecoder('utf-16be').decode(new Uint8Array([0xD8, 0x3D, 0xDE, 0x00]))").AsString().Should().Be("😀");
    }

    [Fact]
    public void ReplacesAnUnpairedUtf16Surrogate()
    {
        var engine = WebEngine();

        engine.Evaluate("new TextDecoder('utf-16le').decode(new Uint8Array([0x00, 0xD8, 0x61, 0x00])).charCodeAt(0)")
            .AsNumber().Should().Be(0xFFFD);
    }

    [Fact]
    public void EndsAUtf16QueueWithOneReplacementHoweverMuchIsPending()
    {
        var engine = WebEngine();

        // https://encoding.spec.whatwg.org/#shared-utf-16-decoder, the end-of-queue step: "If UTF-16 lead
        // byte is non-null or UTF-16 lead surrogate is non-null, set UTF-16 lead byte and UTF-16 lead
        // surrogate to null, and return error" — one error however many of the two are pending. Mirrors the
        // "utf-16{le,be} does not produce more chars than truncated" rows of
        // web-platform-tests encoding/textdecoder-mistakes.any.js.
        engine.Evaluate("new TextDecoder('utf-16le').decode(new Uint8Array([0, 0, 0]))").AsString().Should().Be("\0�");
        engine.Evaluate("new TextDecoder('utf-16le').decode(new Uint8Array([42, 0, 0]))").AsString().Should().Be("*�");
        engine.Evaluate("new TextDecoder('utf-16le').decode(new Uint8Array([0, 0xd8, 0]))").AsString().Should().Be("�");
        engine.Evaluate("new TextDecoder('utf-16le').decode(new Uint8Array([0, 0xd8, 0xd8]))").AsString().Should().Be("�");

        engine.Evaluate("new TextDecoder('utf-16be').decode(new Uint8Array([0, 0, 0]))").AsString().Should().Be("\0�");
        engine.Evaluate("new TextDecoder('utf-16be').decode(new Uint8Array([0, 42, 0]))").AsString().Should().Be("*�");
        engine.Evaluate("new TextDecoder('utf-16be').decode(new Uint8Array([0xd8, 0, 0]))").AsString().Should().Be("�");
        engine.Evaluate("new TextDecoder('utf-16be').decode(new Uint8Array([0xd8, 0, 0xd8]))").AsString().Should().Be("�");
    }

    #endregion

    #region Streaming

    [Fact]
    public void StreamsACodePointSplitAcrossTwoChunks()
    {
        var engine = WebEngine();

        // '€' is E2 82 AC; the first chunk ends mid-sequence and must produce nothing.
        engine.Evaluate("""
            const decoder = new TextDecoder();
            const first = decoder.decode(new Uint8Array([0x61, 0xE2, 0x82]), { stream: true });
            const second = decoder.decode(new Uint8Array([0xAC, 0x62]));
            [first, second].join('|');
            """).AsString().Should().Be("a|€b");
    }

    [Fact]
    public void ANonStreamingDecodeEndsTheStream()
    {
        var engine = WebEngine();

        // The held-over bytes of the first chunk are flushed as U+FFFD by the next non-streaming call, and
        // the call after that starts a fresh stream rather than continuing.
        engine.Evaluate("""
            const decoder = new TextDecoder();
            decoder.decode(new Uint8Array([0xE2, 0x82]), { stream: true });
            const flushed = decoder.decode();
            const fresh = decoder.decode(new Uint8Array([0xAC]));
            [flushed.charCodeAt(0), fresh.charCodeAt(0)].join(',');
            """).AsString().Should().Be("65533,65533");
    }

    [Fact]
    public void StreamsAUtf16CodeUnitSplitAcrossTwoChunks()
    {
        var engine = WebEngine();

        engine.Evaluate("""
            const decoder = new TextDecoder('utf-16le');
            const first = decoder.decode(new Uint8Array([0x68]), { stream: true });
            const second = decoder.decode(new Uint8Array([0x00, 0x69, 0x00]));
            [first, second].join('|');
            """).AsString().Should().Be("|hi");
    }

    [Fact]
    public void FatalDoesNotFireOnAnIncompleteSequenceMidStream()
    {
        var engine = WebEngine();

        engine.Evaluate("""
            const decoder = new TextDecoder('utf-8', { fatal: true });
            decoder.decode(new Uint8Array([0xE2, 0x82]), { stream: true });
            decoder.decode(new Uint8Array([0xAC]));
            """).AsString().Should().Be("€");
    }

    #endregion

    #region Byte order mark

    [Fact]
    public void StripsOneLeadingBomPerStream()
    {
        var engine = WebEngine();

        engine.Evaluate("new TextDecoder().decode(new Uint8Array([0xEF, 0xBB, 0xBF, 0x61]))").AsString().Should().Be("a");

        // Only the first one: a second U+FEFF is content.
        engine.Evaluate("new TextDecoder().decode(new Uint8Array([0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF])).charCodeAt(0)")
            .AsNumber().Should().Be(0xFEFF);

        // And only when it leads.
        engine.Evaluate("new TextDecoder().decode(new Uint8Array([0x61, 0xEF, 0xBB, 0xBF])).length").AsNumber().Should().Be(2);
    }

    [Fact]
    public void KeepsTheBomWhenIgnoreBomIsSet()
    {
        var engine = WebEngine();

        engine.Evaluate("new TextDecoder('utf-8', { ignoreBOM: true }).decode(new Uint8Array([0xEF, 0xBB, 0xBF, 0x61])).length")
            .AsNumber().Should().Be(2);
        engine.Evaluate("new TextDecoder('utf-8', { ignoreBOM: true }).decode(new Uint8Array([0xEF, 0xBB, 0xBF, 0x61])).charCodeAt(0)")
            .AsNumber().Should().Be(0xFEFF);
    }

    [Fact]
    public void StripsABomSplitAcrossChunks()
    {
        var engine = WebEngine();

        // The BOM is dropped by the serialize step, which sees scalar values rather than bytes, so it does
        // not matter that its three bytes arrived in two pieces.
        engine.Evaluate("""
            const decoder = new TextDecoder();
            const first = decoder.decode(new Uint8Array([0xEF, 0xBB]), { stream: true });
            const second = decoder.decode(new Uint8Array([0xBF, 0x61]));
            [first, second].join('|');
            """).AsString().Should().Be("|a");
    }

    [Fact]
    public void StripsTheBomOnlyOnceAcrossAWholeStream()
    {
        var engine = WebEngine();

        engine.Evaluate("""
            const decoder = new TextDecoder();
            const first = decoder.decode(new Uint8Array([0xEF, 0xBB, 0xBF, 0x61]), { stream: true });
            const second = decoder.decode(new Uint8Array([0xEF, 0xBB, 0xBF, 0x62]));
            [first, second.charCodeAt(0), second.length].join('|');
            """).AsString().Should().Be("a|65279|2");
    }

    [Fact]
    public void StripsTheUtf16Bom()
    {
        var engine = WebEngine();

        engine.Evaluate("new TextDecoder('utf-16le').decode(new Uint8Array([0xFF, 0xFE, 0x61, 0x00]))").AsString().Should().Be("a");
        engine.Evaluate("new TextDecoder('utf-16be').decode(new Uint8Array([0xFE, 0xFF, 0x00, 0x61]))").AsString().Should().Be("a");
    }

    #endregion

    #region Shape

    [Fact]
    public void TextDecoderBrandChecksItsReceiver()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("TextDecoder()"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("TextDecoder.prototype.decode.call({})"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("TextDecoder.prototype.encoding"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("TextDecoder.prototype.fatal"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("TextDecoder.prototype.ignoreBOM"));
    }

    [Fact]
    public void HasTheWebIdlShape()
    {
        var engine = WebEngine();

        engine.Evaluate("new TextEncoder() instanceof TextEncoder").AsBoolean().Should().BeTrue();
        engine.Evaluate("new TextDecoder() instanceof TextDecoder").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(TextEncoder.prototype) === Object.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("TextEncoder.prototype.constructor === TextEncoder").AsBoolean().Should().BeTrue();
        engine.Evaluate("TextDecoder.prototype.constructor === TextDecoder").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(new TextEncoder())").AsString().Should().Be("[object TextEncoder]");
        engine.Evaluate("Object.prototype.toString.call(new TextDecoder())").AsString().Should().Be("[object TextDecoder]");

        // An instance has no own property: everything is an accessor or an operation on the prototype.
        engine.Evaluate("Object.getOwnPropertyNames(new TextEncoder()).length").AsNumber().Should().Be(0);
        engine.Evaluate("Object.getOwnPropertyNames(new TextDecoder()).length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void HasTheArityTheIdlDeclares()
    {
        var engine = WebEngine();

        // Optional arguments do not count, so only encodeInto's two required ones do.
        engine.Evaluate("TextEncoder.prototype.encode.length").AsNumber().Should().Be(0);
        engine.Evaluate("TextEncoder.prototype.encodeInto.length").AsNumber().Should().Be(2);
        engine.Evaluate("TextDecoder.prototype.decode.length").AsNumber().Should().Be(0);
        engine.Evaluate("TextDecoder.length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void ExposesAttributesAndOperationsWithTheirWebIdlPropertyAttributes()
    {
        var engine = WebEngine();

        // WebIDL attributes are enumerable accessor properties on the prototype.
        engine.Evaluate("""
            const d = Object.getOwnPropertyDescriptor(TextDecoder.prototype, 'encoding');
            [typeof d.get, typeof d.set, d.enumerable, d.configurable].join('|');
            """).AsString().Should().Be("function|undefined|true|true");

        // The operations are enumerable too — https://webidl.spec.whatwg.org/#es-operations, and the triple
        // Node 24 reports for TextDecoder.prototype.decode. This is where WebIDL parts company with
        // ECMA-262, whose built-in function properties are non-enumerable.
        engine.Evaluate("Object.getOwnPropertyDescriptor(TextDecoder.prototype, 'decode').enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getOwnPropertyDescriptor(TextEncoder.prototype, 'encode').writable").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.keys(TextDecoder.prototype).includes('decode')").AsBoolean().Should().BeTrue();

        // @@toStringTag is configurable only, as the specification gives it.
        engine.Evaluate("""
            const t = Object.getOwnPropertyDescriptor(TextEncoder.prototype, Symbol.toStringTag);
            [t.value, t.writable, t.enumerable, t.configurable].join('|');
            """).AsString().Should().Be("TextEncoder|false|false|true");
    }

    [Fact]
    public void SubclassingKeepsThePrototypeTheSubclassAsksFor()
    {
        var engine = WebEngine();

        engine.Evaluate("""
            class MyEncoder extends TextEncoder { }
            const e = new MyEncoder();
            [e instanceof MyEncoder, e instanceof TextEncoder, e.encoding].join('|');
            """).AsString().Should().Be("true|true|utf-8");
    }

    [Fact]
    public void RoundTripsThroughEncodeAndDecode()
    {
        var engine = WebEngine();

        engine.Evaluate("""
            const text = 'Grüße, 世界! 😀';
            new TextDecoder().decode(new TextEncoder().encode(text)) === text;
            """).AsBoolean().Should().BeTrue();
    }

    #endregion
}
#endif
