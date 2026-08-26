#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>Blob</c> as the File API specifies it — https://w3c.github.io/FileAPI/.
/// </summary>
public class BlobTests
{
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Files));

    private static JsValue Eval(string source) => WebEngine().Evaluate(source);

    [Test]
    public void ConstructsAnEmptyBlobFromNoArguments()
    {
        // https://w3c.github.io/FileAPI/#constructorBlob step 1.
        Eval("new Blob().size").AsNumber().Should().Be(0);
        Eval("new Blob().type").AsString().Should().Be("");

        // A missing sequence is still a missing sequence when the options are given.
        Eval("new Blob(undefined, { type: 'x/y' }).size").AsNumber().Should().Be(0);
        Eval("new Blob(undefined, { type: 'x/y' }).type").AsString().Should().Be("x/y");
    }

    [Test]
    public void ConcatenatesThePartsInOrder()
    {
        // https://w3c.github.io/FileAPI/#process-blob-parts
        Eval("new Blob(['a', 'bc', 'd']).size").AsNumber().Should().Be(4);
        Eval("new Blob(['a', 'bc']).text()").UnwrapIfPromise().AsString().Should().Be("abc");
    }

    [Test]
    public void EncodesStringPartsAsUtf8()
    {
        // "Append the result of UTF-8 encoding s to bytes" — so size counts bytes, not code units.
        Eval("new Blob(['é']).size").AsNumber().Should().Be(2);
        Eval("new Blob(['𝌆']).size").AsNumber().Should().Be(4);

        // A USVString substitutes U+FFFD for an unpaired surrogate, which is three bytes.
        Eval("new Blob(['\\uD800']).size").AsNumber().Should().Be(3);
        Eval("new Blob(['\\uD800']).text()").UnwrapIfPromise().AsString().Should().Be("\uFFFD");
    }

    [Test]
    public void StringifiesAPartThatIsNeitherABufferSourceNorABlob()
    {
        Eval("new Blob([123]).text()").UnwrapIfPromise().AsString().Should().Be("123");
        Eval("new Blob([null]).text()").UnwrapIfPromise().AsString().Should().Be("null");
        Eval("new Blob([{ toString() { return 'hi'; } }]).text()").UnwrapIfPromise().AsString().Should().Be("hi");
    }

    [Test]
    public void CopiesTheBytesOfEveryBufferSourceShape()
    {
        // "If element is a BufferSource, get a copy of the bytes held by the buffer source".
        Eval("new Blob([new Uint8Array([1, 2, 3])]).size").AsNumber().Should().Be(3);
        Eval("new Blob([new Uint8Array([1, 2, 3, 4]).subarray(1, 3)]).size").AsNumber().Should().Be(2);
        Eval("new Blob([new Uint32Array([1, 2])]).size").AsNumber().Should().Be(8);
        Eval("new Blob([new Uint8Array([1, 2, 3, 4]).buffer]).size").AsNumber().Should().Be(4);
        Eval("new Blob([new DataView(new Uint8Array([1, 2, 3, 4]).buffer, 2)]).size").AsNumber().Should().Be(2);
    }

    [Test]
    public void CopiesRatherThanAliasesABufferSource()
    {
        var engine = WebEngine();
        engine.Execute("var a = new Uint8Array([65]); var b = new Blob([a]); a[0] = 66;");

        // The blob is immutable, so a later write through the view cannot reach it.
        engine.Evaluate("b.text()").UnwrapIfPromise().AsString().Should().Be("A");
    }

    [Test]
    public void AppendsTheBytesOfANestedBlob()
    {
        Eval("new Blob([new Blob(['ab']), 'c']).text()").UnwrapIfPromise().AsString().Should().Be("abc");
    }

    [Test]
    public void TreatsADetachedBufferAsNoBytes()
    {
        // A detached buffer holds nothing to copy. It is not an error: WebIDL admits the value, and the
        // byte sequence it contributes is empty.
        Eval("(function () { var b = new ArrayBuffer(8); b.transfer(); return new Blob([b]).size; })()")
            .AsNumber().Should().Be(0);
    }

    [Test]
    public void TreatsAViewThatHasGoneOutOfBoundsAsNoBytes()
    {
        // A resizable buffer can shrink out from under a view, leaving both its offset and its length past
        // the end of the block. There are no bytes left to copy, and reaching for them must not fault.
        Eval("""
            (function () {
                const buffer = new ArrayBuffer(8, { maxByteLength: 8 });
                const view = new Uint8Array(buffer, 4);
                buffer.resize(2);
                return new Blob([view]).size;
            })()
            """).AsNumber().Should().Be(0);
    }

    [Test]
    public void ReadsBackAnEmptyBlob()
    {
        Eval("new Blob().text()").UnwrapIfPromise().AsString().Should().Be("");
        Eval("new Blob().arrayBuffer()").UnwrapIfPromise().AsObject().Get("byteLength").AsNumber().Should().Be(0);
        Eval("new Blob().bytes()").UnwrapIfPromise().AsObject().Get("length").AsNumber().Should().Be(0);
        Eval("new Blob().slice(0, 0).size").AsNumber().Should().Be(0);
    }

    [Test]
    public void RejectsAnythingThatIsNotASequence()
    {
        // WebIDL sequence conversion: a bare string is not an object and is not iterated character by
        // character, which is the mistake `new Blob('abc')` usually is.
        Assert.Throws<JavaScriptException>(() => Eval("new Blob('abc')"))!
            .Error.Get("name").AsString().Should().Be("TypeError");

        Assert.Throws<JavaScriptException>(() => Eval("new Blob(null)"));
        Assert.Throws<JavaScriptException>(() => Eval("new Blob({})"));
        Assert.Throws<JavaScriptException>(() => Eval("new Blob(5)"));
    }

    [Test]
    public void AcceptsAnyIterable()
    {
        Eval("new Blob(new Set(['a', 'b'])).size").AsNumber().Should().Be(2);
        Eval("new Blob(function* () { yield 'a'; yield 'bc'; }()).size").AsNumber().Should().Be(3);
    }

    [TestCase("text/plain", "text/plain")]
    [TestCase("TEXT/PLAIN", "text/plain")]
    [TestCase("Text/Plain; Charset=UTF-8", "text/plain; charset=utf-8")]
    // A code point outside U+0020..U+007E replaces the whole type with the empty string.
    [TestCase("a\u00FFb", "")]
    [TestCase("a\tb", "")]
    [TestCase("a\nb", "")]
    public void NormalizesTheMediaType(string given, string expected)
    {
        // https://w3c.github.io/FileAPI/#constructorBlob step 3.
        WebEngine().SetValue("given", given).Evaluate("new Blob([], { type: given }).type").AsString().Should().Be(expected);
    }

    [Test]
    public void TakesTheOptionsBagTheWayWebIdlDoes()
    {
        // null and undefined mean "every member defaulted"; anything else that is not an object is a
        // TypeError — https://webidl.spec.whatwg.org/#es-dictionary.
        Eval("new Blob([], null).type").AsString().Should().Be("");
        Eval("new Blob([], undefined).type").AsString().Should().Be("");
        Eval("new Blob([], { type: undefined }).type").AsString().Should().Be("");
        Assert.Throws<JavaScriptException>(() => Eval("new Blob([], 5)"));
    }

    [Test]
    public void ReadsDictionaryMembersInLexicographicalOrder()
    {
        var engine = WebEngine();

        // Members are converted in lexicographical order of their identifiers, so `endings` is observed
        // before `type`. The parts are drained before either, being an earlier argument.
        engine.Execute("""
            var seen = [];
            new Blob(
                (function* () { seen.push('parts'); yield 'x'; })(),
                { get endings() { seen.push('endings'); return 'transparent'; },
                  get type() { seen.push('type'); return 'a/b'; } });
            """);

        engine.Evaluate("seen.join(',')").AsString().Should().Be("parts,endings,type");
    }

    [Test]
    public void ValidatesTheEndingsEnumerationButTreatsNativeAsTransparent()
    {
        Eval("new Blob(['a'], { endings: 'transparent' }).size").AsNumber().Should().Be(1);

        // "native" is accepted, and deliberately does nothing: rewriting line endings would make a blob's
        // bytes depend on the host operating system.
        Eval("new Blob(['a\\nb'], { endings: 'native' }).size").AsNumber().Should().Be(3);

        // An unknown enumeration value is still a TypeError, as a WebIDL enum conversion is.
        Assert.Throws<JavaScriptException>(() => Eval("new Blob([], { endings: 'bogus' })"));
    }

    [Test]
    public void ExposesSizeAndTypeAsPrototypeAccessors()
    {
        var engine = WebEngine();

        // WebIDL attributes live on the interface prototype object, so the instance has no own property.
        engine.Evaluate("Object.getOwnPropertyNames(new Blob(['a'])).length").AsNumber().Should().Be(0);

        engine.Evaluate("var d = Object.getOwnPropertyDescriptor(Blob.prototype, 'size'); typeof d.get").AsString().Should().Be("function");
        engine.Evaluate("d.set").IsUndefined().Should().BeTrue();
        engine.Evaluate("d.enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate("d.configurable").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void BrandChecksEveryMember()
    {
        var engine = WebEngine();

        // Blob.prototype is not itself a Blob.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Blob.prototype.size"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Blob.prototype.type"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Blob.prototype.slice.call({})"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Blob.prototype.text.call({})"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Blob.prototype.arrayBuffer.call([])"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Blob.prototype.bytes.call('')"));
    }

    [Test]
    public void HasTheToStringTagAndConstructorWebIdlAsksFor()
    {
        var engine = WebEngine();

        engine.Evaluate("Object.prototype.toString.call(new Blob())").AsString().Should().Be("[object Blob]");
        engine.Evaluate("new Blob().constructor === Blob").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(Blob.prototype) === Object.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Blob.length").AsNumber().Should().Be(0);
        engine.Evaluate("Blob.name").AsString().Should().Be("Blob");
    }

    [Test]
    public void RequiresNew()
    {
        Assert.Throws<JavaScriptException>(() => Eval("Blob()"));
    }

    [Test]
    public void SupportsSubclassing()
    {
        var engine = WebEngine();

        engine.Execute("class MyBlob extends Blob { constructor(p) { super(p); this.tag = 'mine'; } }");
        engine.Evaluate("new MyBlob(['abc']).size").AsNumber().Should().Be(3);
        engine.Evaluate("new MyBlob(['abc']) instanceof MyBlob").AsBoolean().Should().BeTrue();
        engine.Evaluate("new MyBlob(['abc']).tag").AsString().Should().Be("mine");
    }

    // https://w3c.github.io/FileAPI/#slice-blob — relativeStart/relativeEnd normalization.
    [TestCase("blob.slice()", "abcdef")]
    [TestCase("blob.slice(2)", "cdef")]
    [TestCase("blob.slice(2, 4)", "cd")]
    [TestCase("blob.slice(-2)", "ef")]
    [TestCase("blob.slice(-100)", "abcdef")]
    [TestCase("blob.slice(0, -2)", "abcd")]
    [TestCase("blob.slice(0, -100)", "")]
    [TestCase("blob.slice(4, 2)", "")]
    [TestCase("blob.slice(100)", "")]
    [TestCase("blob.slice(0, 100)", "abcdef")]
    // An optional argument with no default value is missing when undefined is passed explicitly.
    [TestCase("blob.slice(undefined, undefined)", "abcdef")]
    [TestCase("blob.slice(2, undefined)", "cdef")]
    // [Clamp] rounds to nearest, ties to even, and NaN becomes zero.
    [TestCase("blob.slice(1.5, 4.5)", "cd")]
    [TestCase("blob.slice(2.4)", "cdef")]
    [TestCase("blob.slice(NaN, 2)", "ab")]
    [TestCase("blob.slice(-Infinity, Infinity)", "abcdef")]
    public void SlicesByByteOrderPosition(string expression, string expected)
    {
        var engine = WebEngine();
        engine.Execute("var blob = new Blob(['abcdef']);");

        engine.Evaluate(expression + ".text()").UnwrapIfPromise().AsString().Should().Be(expected);
    }

    [Test]
    public void GivesTheSliceOnlyTheContentTypeItWasAskedFor()
    {
        var engine = WebEngine();
        engine.Execute("var blob = new Blob(['abcdef'], { type: 'text/plain' });");

        // The type is never inherited: a slice with no contentType has the empty type.
        engine.Evaluate("blob.slice(0, 2).type").AsString().Should().Be("");
        engine.Evaluate("blob.slice(0, 2, undefined).type").AsString().Should().Be("");
        engine.Evaluate("blob.slice(0, 2, 'TEXT/HTML').type").AsString().Should().Be("text/html");
        engine.Evaluate("blob.slice(0, 2, 'a\u00FFb').type").AsString().Should().Be("");

        // And the result is a Blob, never a File.
        engine.Evaluate("blob.slice(0, 2) instanceof Blob").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ReadsBackAsTextArrayBufferAndBytes()
    {
        var engine = WebEngine();
        engine.Execute("var blob = new Blob(['hi']);");

        engine.Evaluate("blob.text()").UnwrapIfPromise().AsString().Should().Be("hi");
        engine.Evaluate("blob.arrayBuffer()").UnwrapIfPromise().AsObject().Get("byteLength").AsNumber().Should().Be(2);
        engine.Evaluate("blob.bytes()").UnwrapIfPromise().AsObject().Get("length").AsNumber().Should().Be(2);

        engine.Evaluate("blob.arrayBuffer().then(a => a instanceof ArrayBuffer)").UnwrapIfPromise().AsBoolean().Should().BeTrue();
        engine.Evaluate("blob.bytes().then(b => b instanceof Uint8Array && b[0] === 104 && b[1] === 105)")
            .UnwrapIfPromise().AsBoolean().Should().BeTrue();
    }

    [Test]
    public void TheReadMethodsAnswerRealPromises()
    {
        var engine = WebEngine();

        // Already resolved, but promises all the same — awaiting one is a microtask turn, not a stall.
        engine.Evaluate("new Blob(['x']).text() instanceof Promise").AsBoolean().Should().BeTrue();
        engine.Evaluate("(async () => (await new Blob(['x']).text()) + '!')()").UnwrapIfPromise().AsString().Should().Be("x!");

        // A read never hands out a view over the blob's own storage: writing to what came back must not
        // change what the next read produces.
        engine.Execute("var blob = new Blob(['AB']); var first; blob.bytes().then(b => { first = b; b[0] = 67; });");
        engine.Evaluate("blob.text()").UnwrapIfPromise().AsString().Should().Be("AB");
    }

    [Test]
    public void TextIsALenientBomStrippingUtf8Decode()
    {
        // https://encoding.spec.whatwg.org/#utf-8-decode strips one leading BOM ...
        Eval("new Blob([new Uint8Array([0xEF, 0xBB, 0xBF, 0x61])]).text()").UnwrapIfPromise().AsString().Should().Be("a");

        // ... and substitutes U+FFFD for an ill-formed sequence rather than rejecting.
        Eval("new Blob([new Uint8Array([0xFF])]).text()").UnwrapIfPromise().AsString().Should().Be("\uFFFD");

        // The BOM is only stripped once, and only at the start.
        Eval("new Blob([new Uint8Array([0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF])]).text()").UnwrapIfPromise().AsString().Should().Be("\uFEFF");
    }

    [Test]
    public void StreamProducesTheBytesAsOneChunkAndThenCloses()
    {
        // https://w3c.github.io/FileAPI/#blob-get-stream. The bytes are already in memory, so they are one
        // chunk rather than the implementation-defined slices a browser's loop reads.
        var engine = WebEngine();
        engine.Execute("var read = []; var s = new Blob(['hello']).stream(); var r = s.getReader();");

        engine.Evaluate("r.read().then(x => x.done + ':' + (x.value instanceof Uint8Array) + ':' + x.value.length)")
            .UnwrapIfPromise().AsString().Should().Be("false:true:5");

        engine.Evaluate("r.read().then(x => x.done + ':' + (x.value === undefined))")
            .UnwrapIfPromise().AsString().Should().Be("true:true");
    }

    [Test]
    public void StreamOfAnEmptyBlobIsDoneAtOnce()
    {
        Eval("new Blob([]).stream().getReader().read().then(x => x.done + ':' + (x.value === undefined))")
            .UnwrapIfPromise().AsString().Should().Be("true:true");
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#blob-get-stream: "Let stream be a new ReadableStream created in
    /// realm … set up with byte reading support." So a BYOB reader works on it, and a BYOB read is bounded
    /// by the caller's buffer rather than by the one chunk a default reader sees.
    /// </summary>
    [Test]
    public void StreamIsAByteStreamAndCanBeReadByob()
    {
        var engine = WebEngine();
        engine.Execute("var r = new Blob(['hello']).stream().getReader({ mode: 'byob' });");

        engine.Evaluate("Object.getPrototypeOf(r).constructor.name").AsString().Should().Be("ReadableStreamBYOBReader");

        // A buffer smaller than the blob takes what fits; the rest stays queued for the next read.
        engine.Evaluate("r.read(new Uint8Array(3)).then(x => x.done + ':' + String.fromCharCode.apply(null, Array.from(x.value)))")
            .UnwrapIfPromise().AsString().Should().Be("false:hel");

        engine.Evaluate("r.read(new Uint8Array(3)).then(x => x.done + ':' + String.fromCharCode.apply(null, Array.from(x.value)))")
            .UnwrapIfPromise().AsString().Should().Be("false:lo");

        engine.Evaluate("r.read(new Uint8Array(3)).then(x => x.done + ':' + x.value.byteLength)")
            .UnwrapIfPromise().AsString().Should().Be("true:0");
    }

    /// <summary>
    /// A byte source can also serve a BYOB read through <c>respondWithNewView</c>-shaped machinery — but a
    /// blob's stream has its bytes already, so what this pins is the other half of the ownership rule: the
    /// caller's buffer is transferred away, and the view handed back owns the memory.
    /// </summary>
    [Test]
    public void StreamTransfersTheBufferOfAByobRead()
    {
        var engine = WebEngine();
        engine.Execute("var view = new Uint8Array(8); var buffer = view.buffer;");

        engine.Evaluate("new Blob(['ab']).stream().getReader({ mode: 'byob' }).read(view).then(x => x.value.buffer.byteLength)")
            .UnwrapIfPromise().AsNumber().Should().Be(8);

        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(0);
        engine.Evaluate("view.byteLength").AsNumber().Should().Be(0);
    }

    [Test]
    public void StreamIsAReadableStreamEvenWithoutTheStreamsFeature()
    {
        // This engine asked for Files and nothing else, so the ReadableStream *global* is absent — but the
        // object stream() answers is the real interface, reached through its prototype. Naming it takes the
        // Streams feature (or WebApiFeatures.Default, which has both).
        var engine = WebEngine();

        engine.Evaluate("typeof ReadableStream").AsString().Should().Be("undefined");
        engine.Evaluate("Object.prototype.toString.call(new Blob(['x']).stream())").AsString().Should().Be("[object ReadableStream]");
        engine.Evaluate("new Blob(['x']).stream().locked").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void StreamHandsEachCallItsOwnStreamOverACopyOfTheBytes()
    {
        var engine = WebEngine();
        engine.Execute("var b = new Blob(['ab']); var s1 = b.stream(); var s2 = b.stream();");

        engine.Evaluate("s1 === s2").AsBoolean().Should().BeFalse();

        // Writing through one chunk cannot reach the blob or the other stream: a blob is immutable and what
        // crosses into script is a copy.
        engine.Execute("s1.getReader().read().then(x => { x.value[0] = 0x7A; });");
        engine.Tasks.ProcessTasks();

        engine.Evaluate("b.text()").UnwrapIfPromise().AsString().Should().Be("ab");
        engine.Evaluate("s2.getReader().read().then(x => String.fromCharCode.apply(null, Array.from(x.value)))")
            .UnwrapIfPromise().AsString().Should().Be("ab");
    }

    /// <summary>
    /// An engine that can read a stream to the end and record what came out of it, so a test can pin both
    /// the decoded text and the number of chunks it arrived in.
    /// </summary>
    private static Engine TextStreamEngine()
    {
        var engine = WebEngine();
        engine.Execute("""
            var log = [];
            async function collect(stream, label) {
              const reader = stream.getReader();
              for (;;) {
                let result;
                try { result = await reader.read(); }
                catch (e) { log.push(label + ':error:' + e.name); return; }
                if (result.done) { log.push(label + ':done'); return; }
                log.push(label + ':' + typeof result.value + ':' + result.value);
              }
            }
            function bytes() { return new Uint8Array(Array.prototype.slice.call(arguments)); }
            """);
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    /// <summary>
    /// https://w3c.github.io/FileAPI/#dom-blob-textstream: the blob's stream piped through a UTF-8
    /// <c>TextDecoderStream</c>, so the chunks are strings rather than bytes.
    /// </summary>
    [Test]
    public void TextStreamDecodesTheBytesAsUtf8Strings()
    {
        var engine = TextStreamEngine();
        engine.Execute("collect(new Blob(['hello world']).textStream(), 'out');");

        // One chunk, because the source is Blob.stream() and that hands over its bytes in one — the decoder
        // adds no boundaries of its own. WPT only asserts that there is at least one; this is the reduction
        // Blob.stream() documents, pinned where it is observable as text.
        Log(engine).Should().Be("out:string:hello world,out:done");
    }

    [Test]
    public void TextStreamOfAnEmptyBlobClosesWithNoChunks()
    {
        // The flush produces the empty string, and "if outputChunk is not the empty string" means nothing is
        // enqueued for it — https://encoding.spec.whatwg.org/#flush-and-enqueue.
        var engine = TextStreamEngine();
        engine.Execute("collect(new Blob().textStream(), 'out');");

        Log(engine).Should().Be("out:done");
    }

    /// <summary>
    /// A code point split across two blob parts still decodes to that code point: the parts are concatenated
    /// into one byte sequence before anything reads it.
    /// </summary>
    /// <remarks>
    /// It is worth being honest about what this does and does not exercise. Jint's blob stream hands its
    /// bytes over as a single chunk, so the split here is a split between blob <i>parts</i> and not a chunk
    /// boundary the decoder ever sees — the streaming-decode path that carries an incomplete sequence from
    /// one chunk to the next is pinned by <c>TextDecoderStreamTests.JoinsASequenceSplitAcrossChunks</c>
    /// instead. What this pins is that the composition cannot decode part-wise, which is exactly what would
    /// break if <c>textStream()</c> ever decoded each part on its own or if the source were later chunked.
    /// </remarks>
    [Test]
    public void TextStreamJoinsAMultiByteSequenceSplitAcrossParts()
    {
        var engine = TextStreamEngine();

        // U+20AC EURO SIGN is E2 82 AC, split after the second byte; U+1D306 is F0 9D 8C 86, split after the
        // first, so the second part opens mid sequence *and* mid surrogate pair.
        engine.Execute("""
            var b = new Blob([bytes(0xE2, 0x82), bytes(0xAC), bytes(0xF0), bytes(0x9D, 0x8C, 0x86)]);
            collect(b.textStream(), 'out');
            """);

        Log(engine).Should().Be("out:string:€𝌆,out:done");
    }

    /// <summary>
    /// The decoder is an ordinary UTF-8 <c>TextDecoderStream</c>, so "serialize I/O queue" step 2.3 drops a
    /// single leading U+FEFF — https://encoding.spec.whatwg.org/#concept-td-serialize.
    /// </summary>
    /// <remarks>
    /// The vendored corpus asserts nothing about a BOM, so this is Jint's own pin rather than a WPT row. It
    /// deliberately asserts the same three answers <c>text()</c> gives, since both are "UTF-8 decode" and the
    /// two must not drift.
    /// </remarks>
    [Test]
    public void TextStreamStripsOneLeadingByteOrderMark()
    {
        // One drain per Execute, so the log is this stream's chunks rather than three pipes interleaved.
        var engine = TextStreamEngine();
        engine.Execute("collect(new Blob([bytes(0xEF, 0xBB, 0xBF, 0x41)]).textStream(), 'one');");
        engine.Execute("collect(new Blob([bytes(0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF)]).textStream(), 'two');");
        engine.Execute("collect(new Blob([bytes(0x41, 0xEF, 0xBB, 0xBF)]).textStream(), 'mid');");

        // Exactly one BOM goes, and only when it leads: the second of two survives as U+FEFF, and one after
        // a character was never the start of the stream at all.
        Log(engine).Should().Be("one:string:A,one:done,two:string:﻿,two:done,mid:string:A﻿,mid:done");

        // The same bytes through text(), which is the other spelling of UTF-8 decode.
        Eval("new Blob([new Uint8Array([0xEF, 0xBB, 0xBF, 0x41])]).text()").UnwrapIfPromise().AsString().Should().Be("A");
    }

    [Test]
    public void TextStreamSubstitutesU00FFFDRatherThanFailing()
    {
        // The error mode is "replacement", not "fatal" — "set up a text decoder stream" defaults it that
        // way and textStream() overrides nothing. So an ill-formed sequence is a chunk, never an error.
        var engine = TextStreamEngine();
        engine.Execute("collect(new Blob([bytes(0x41, 0xFF, 0x42)]).textStream(), 'out');");

        Log(engine).Should().Be("out:string:A�B,out:done");
    }

    /// <summary>
    /// "This differs from <c>readAsText()</c> in that it always uses the UTF-8 encoding" — the blob's
    /// <c>type</c> is never consulted, whatever charset it names.
    /// </summary>
    [Test]
    public void TextStreamIgnoresTheCharsetOfTheBlobType()
    {
        var engine = TextStreamEngine();
        engine.Execute("""
            var utf16 = new Blob([bytes(0x68, 0x00, 0x69, 0x00)], { type: 'text/plain; charset=utf-16le' });
            var latin1 = new Blob([bytes(0xC3, 0xA9)], { type: 'text/plain; charset=iso-8859-1' });
            var nonsense = new Blob(['hi'], { type: 'text/plain; charset=invalid-charset' });
            """);

        // One drain per Execute, so the log is not three pipes interleaved.
        engine.Execute("collect(utf16.textStream(), 'utf16');");
        engine.Execute("collect(latin1.textStream(), 'latin1');");
        engine.Execute("collect(nonsense.textStream(), 'nonsense');");

        // An invalid charset is not a RangeError either: no label is ever resolved, so there is nothing to
        // reject. All three decode as UTF-8.
        Log(engine).Should().Be(
            "utf16:string:h\0i\0,utf16:done,latin1:string:é,latin1:done,nonsense:string:hi,nonsense:done");
    }

    /// <summary>
    /// <c>[NewObject]</c>: every call builds a fresh source, a fresh decoder and a fresh result, so reading
    /// one cannot disturb another.
    /// </summary>
    [Test]
    public void TextStreamHandsEachCallItsOwnStream()
    {
        var engine = TextStreamEngine();
        engine.Execute("var b = new Blob(['hello']); var s1 = b.textStream(); var s2 = b.textStream();");

        engine.Evaluate("s1 === s2").AsBoolean().Should().BeFalse();
        engine.Evaluate("s1.locked || s2.locked").AsBoolean().Should().BeFalse();

        // Draining one leaves the other untouched — including its lock, which is what a shared source or a
        // shared decoder would have taken.
        engine.Execute("collect(s1, 'first');");
        engine.Evaluate("s2.locked").AsBoolean().Should().BeFalse();
        engine.Execute("collect(s2, 'second');");

        Log(engine).Should().Be("first:string:hello,first:done,second:string:hello,second:done");
    }

    /// <summary>
    /// A <c>stream()</c> and a <c>textStream()</c> taken from one blob are two streams over two sources,
    /// not one source read twice — so either can be drained without the other noticing, in either order.
    /// </summary>
    [Test]
    public void TextStreamAndStreamOfOneBlobAreIndependent()
    {
        var engine = TextStreamEngine();
        engine.Execute("var b = new Blob(['hello']); var raw = b.stream(); var txt = b.textStream();");

        // Neither is locked by the other's existence, although textStream() has locked the *source* it made
        // for itself — which is a different stream from the one stream() handed over.
        engine.Evaluate("raw.locked || txt.locked").AsBoolean().Should().BeFalse();

        engine.Execute("collect(txt, 'txt');");
        engine.Execute("""
            (async function () {
              const reader = raw.getReader();
              const chunk = await reader.read();
              log.push('raw:' + chunk.value.constructor.name + ':' + chunk.value.length);
              log.push('raw:' + (await reader.read()).done);
            })();
            """);

        // The byte stream still yields bytes and the text stream still yields the string they decode to.
        Log(engine).Should().Be("txt:string:hello,txt:done,raw:Uint8Array:5,raw:true");
    }

    /// <summary>
    /// Cancelling the returned stream. The pipe is built with every option defaulted — nothing prevented —
    /// so cancelling the readable side errors the transform's writable side and the pipe cancels the source
    /// behind it, which is what <c>preventCancel</c> being false means.
    /// </summary>
    /// <remarks>
    /// What that backward cancellation does to the source is not observable from script here, because the
    /// source is a blob's bytes in memory and its cancel steps have nothing to release. What is observable,
    /// and is what this pins, is that the cancellation settles cleanly rather than erroring the stream: the
    /// promise fulfils with <c>undefined</c>, the next read is done, and <c>closed</c> fulfils.
    /// </remarks>
    [Test]
    public void TextStreamCanBeCancelled()
    {
        var engine = TextStreamEngine();
        engine.Execute("""
            var s = new Blob(['hello world']).textStream();
            (async function () {
              const reader = s.getReader();
              log.push('cancel:' + String(await reader.cancel('no thanks')));
              const after = await reader.read();
              log.push('after:' + after.done + ':' + String(after.value));
              try { await reader.closed; log.push('closed:fulfilled'); }
              catch (e) { log.push('closed:rejected:' + e.name); }
            })();
            """);

        Log(engine).Should().Be("cancel:undefined,after:true:undefined,closed:fulfilled");

        // And through the stream itself, with no reader ever acquired — cancel() acquires and releases one.
        var direct = TextStreamEngine();
        direct.Execute("""
            var s = new Blob(['hello world']).textStream();
            s.cancel('no thanks').then(v => log.push('cancel:' + String(v)), e => log.push('cancel:error:' + e.name));
            """);

        Log(direct).Should().Be("cancel:undefined");
    }

    /// <summary>
    /// The result is an ordinary <c>ReadableStream</c> of strings: the byte-stream machinery is the
    /// <i>source</i>, and what a transform's readable side hands out is not a byte stream.
    /// </summary>
    [Test]
    public void TextStreamIsNotAByteStream()
    {
        var engine = WebEngine();

        // Files and nothing else, so ReadableStream is not a global — but the object is the real interface,
        // reached through its prototype, exactly as stream()'s is.
        engine.Evaluate("typeof ReadableStream").AsString().Should().Be("undefined");
        engine.Evaluate("Object.prototype.toString.call(new Blob(['x']).textStream())").AsString()
            .Should().Be("[object ReadableStream]");
        engine.Evaluate("Object.getPrototypeOf(new Blob(['x']).stream()) === Object.getPrototypeOf(new Blob(['x']).textStream())")
            .AsBoolean().Should().BeTrue();

        // stream() serves a BYOB reader; textStream() cannot, because its chunks are strings.
        engine.Execute("var byob = new Blob(['x']).stream().getReader({ mode: 'byob' });");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new Blob(['x']).textStream().getReader({ mode: 'byob' })"))!
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Test]
    public void TextStreamBrandChecksItsReceiver()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Blob.prototype.textStream()"))!
            .Error.Get("name").AsString().Should().Be("TypeError");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Blob.prototype.textStream.call({})"))!
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Test]
    public void DeclaresTheArityTheIdlDoes()
    {
        var engine = WebEngine();

        engine.Evaluate("Blob.prototype.slice.length").AsNumber().Should().Be(0);
        engine.Evaluate("Blob.prototype.text.length").AsNumber().Should().Be(0);
        engine.Evaluate("Blob.prototype.arrayBuffer.length").AsNumber().Should().Be(0);
        engine.Evaluate("Blob.prototype.bytes.length").AsNumber().Should().Be(0);
        engine.Evaluate("Blob.prototype.stream.length").AsNumber().Should().Be(0);
        engine.Evaluate("Blob.prototype.textStream.length").AsNumber().Should().Be(0);
        engine.Evaluate("Blob.prototype.textStream.name").AsString().Should().Be("textStream");
    }
}
#endif
