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

    [Fact]
    public void ConstructsAnEmptyBlobFromNoArguments()
    {
        // https://w3c.github.io/FileAPI/#constructorBlob step 1.
        Eval("new Blob().size").AsNumber().Should().Be(0);
        Eval("new Blob().type").AsString().Should().Be("");

        // A missing sequence is still a missing sequence when the options are given.
        Eval("new Blob(undefined, { type: 'x/y' }).size").AsNumber().Should().Be(0);
        Eval("new Blob(undefined, { type: 'x/y' }).type").AsString().Should().Be("x/y");
    }

    [Fact]
    public void ConcatenatesThePartsInOrder()
    {
        // https://w3c.github.io/FileAPI/#process-blob-parts
        Eval("new Blob(['a', 'bc', 'd']).size").AsNumber().Should().Be(4);
        Eval("new Blob(['a', 'bc']).text()").UnwrapIfPromise().AsString().Should().Be("abc");
    }

    [Fact]
    public void EncodesStringPartsAsUtf8()
    {
        // "Append the result of UTF-8 encoding s to bytes" — so size counts bytes, not code units.
        Eval("new Blob(['é']).size").AsNumber().Should().Be(2);
        Eval("new Blob(['𝌆']).size").AsNumber().Should().Be(4);

        // A USVString substitutes U+FFFD for an unpaired surrogate, which is three bytes.
        Eval("new Blob(['\\uD800']).size").AsNumber().Should().Be(3);
        Eval("new Blob(['\\uD800']).text()").UnwrapIfPromise().AsString().Should().Be("\uFFFD");
    }

    [Fact]
    public void StringifiesAPartThatIsNeitherABufferSourceNorABlob()
    {
        Eval("new Blob([123]).text()").UnwrapIfPromise().AsString().Should().Be("123");
        Eval("new Blob([null]).text()").UnwrapIfPromise().AsString().Should().Be("null");
        Eval("new Blob([{ toString() { return 'hi'; } }]).text()").UnwrapIfPromise().AsString().Should().Be("hi");
    }

    [Fact]
    public void CopiesTheBytesOfEveryBufferSourceShape()
    {
        // "If element is a BufferSource, get a copy of the bytes held by the buffer source".
        Eval("new Blob([new Uint8Array([1, 2, 3])]).size").AsNumber().Should().Be(3);
        Eval("new Blob([new Uint8Array([1, 2, 3, 4]).subarray(1, 3)]).size").AsNumber().Should().Be(2);
        Eval("new Blob([new Uint32Array([1, 2])]).size").AsNumber().Should().Be(8);
        Eval("new Blob([new Uint8Array([1, 2, 3, 4]).buffer]).size").AsNumber().Should().Be(4);
        Eval("new Blob([new DataView(new Uint8Array([1, 2, 3, 4]).buffer, 2)]).size").AsNumber().Should().Be(2);
    }

    [Fact]
    public void CopiesRatherThanAliasesABufferSource()
    {
        var engine = WebEngine();
        engine.Execute("var a = new Uint8Array([65]); var b = new Blob([a]); a[0] = 66;");

        // The blob is immutable, so a later write through the view cannot reach it.
        engine.Evaluate("b.text()").UnwrapIfPromise().AsString().Should().Be("A");
    }

    [Fact]
    public void AppendsTheBytesOfANestedBlob()
    {
        Eval("new Blob([new Blob(['ab']), 'c']).text()").UnwrapIfPromise().AsString().Should().Be("abc");
    }

    [Fact]
    public void TreatsADetachedBufferAsNoBytes()
    {
        // A detached buffer holds nothing to copy. It is not an error: WebIDL admits the value, and the
        // byte sequence it contributes is empty.
        Eval("(function () { var b = new ArrayBuffer(8); b.transfer(); return new Blob([b]).size; })()")
            .AsNumber().Should().Be(0);
    }

    [Fact]
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

    [Fact]
    public void ReadsBackAnEmptyBlob()
    {
        Eval("new Blob().text()").UnwrapIfPromise().AsString().Should().Be("");
        Eval("new Blob().arrayBuffer()").UnwrapIfPromise().AsObject().Get("byteLength").AsNumber().Should().Be(0);
        Eval("new Blob().bytes()").UnwrapIfPromise().AsObject().Get("length").AsNumber().Should().Be(0);
        Eval("new Blob().slice(0, 0).size").AsNumber().Should().Be(0);
    }

    [Fact]
    public void RejectsAnythingThatIsNotASequence()
    {
        // WebIDL sequence conversion: a bare string is not an object and is not iterated character by
        // character, which is the mistake `new Blob('abc')` usually is.
        Assert.Throws<JavaScriptException>(() => Eval("new Blob('abc')"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        Assert.Throws<JavaScriptException>(() => Eval("new Blob(null)"));
        Assert.Throws<JavaScriptException>(() => Eval("new Blob({})"));
        Assert.Throws<JavaScriptException>(() => Eval("new Blob(5)"));
    }

    [Fact]
    public void AcceptsAnyIterable()
    {
        Eval("new Blob(new Set(['a', 'b'])).size").AsNumber().Should().Be(2);
        Eval("new Blob(function* () { yield 'a'; yield 'bc'; }()).size").AsNumber().Should().Be(3);
    }

    [Theory]
    [InlineData("text/plain", "text/plain")]
    [InlineData("TEXT/PLAIN", "text/plain")]
    [InlineData("Text/Plain; Charset=UTF-8", "text/plain; charset=utf-8")]
    // A code point outside U+0020..U+007E replaces the whole type with the empty string.
    [InlineData("a\u00FFb", "")]
    [InlineData("a\tb", "")]
    [InlineData("a\nb", "")]
    public void NormalizesTheMediaType(string given, string expected)
    {
        // https://w3c.github.io/FileAPI/#constructorBlob step 3.
        WebEngine().SetValue("given", given).Evaluate("new Blob([], { type: given }).type").AsString().Should().Be(expected);
    }

    [Fact]
    public void TakesTheOptionsBagTheWayWebIdlDoes()
    {
        // null and undefined mean "every member defaulted"; anything else that is not an object is a
        // TypeError — https://webidl.spec.whatwg.org/#es-dictionary.
        Eval("new Blob([], null).type").AsString().Should().Be("");
        Eval("new Blob([], undefined).type").AsString().Should().Be("");
        Eval("new Blob([], { type: undefined }).type").AsString().Should().Be("");
        Assert.Throws<JavaScriptException>(() => Eval("new Blob([], 5)"));
    }

    [Fact]
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

    [Fact]
    public void ValidatesTheEndingsEnumerationButTreatsNativeAsTransparent()
    {
        Eval("new Blob(['a'], { endings: 'transparent' }).size").AsNumber().Should().Be(1);

        // "native" is accepted, and deliberately does nothing: rewriting line endings would make a blob's
        // bytes depend on the host operating system.
        Eval("new Blob(['a\\nb'], { endings: 'native' }).size").AsNumber().Should().Be(3);

        // An unknown enumeration value is still a TypeError, as a WebIDL enum conversion is.
        Assert.Throws<JavaScriptException>(() => Eval("new Blob([], { endings: 'bogus' })"));
    }

    [Fact]
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

    [Fact]
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

    [Fact]
    public void HasTheToStringTagAndConstructorWebIdlAsksFor()
    {
        var engine = WebEngine();

        engine.Evaluate("Object.prototype.toString.call(new Blob())").AsString().Should().Be("[object Blob]");
        engine.Evaluate("new Blob().constructor === Blob").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(Blob.prototype) === Object.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Blob.length").AsNumber().Should().Be(0);
        engine.Evaluate("Blob.name").AsString().Should().Be("Blob");
    }

    [Fact]
    public void RequiresNew()
    {
        Assert.Throws<JavaScriptException>(() => Eval("Blob()"));
    }

    [Fact]
    public void SupportsSubclassing()
    {
        var engine = WebEngine();

        engine.Execute("class MyBlob extends Blob { constructor(p) { super(p); this.tag = 'mine'; } }");
        engine.Evaluate("new MyBlob(['abc']).size").AsNumber().Should().Be(3);
        engine.Evaluate("new MyBlob(['abc']) instanceof MyBlob").AsBoolean().Should().BeTrue();
        engine.Evaluate("new MyBlob(['abc']).tag").AsString().Should().Be("mine");
    }

    [Theory]
    // https://w3c.github.io/FileAPI/#slice-blob — relativeStart/relativeEnd normalization.
    [InlineData("blob.slice()", "abcdef")]
    [InlineData("blob.slice(2)", "cdef")]
    [InlineData("blob.slice(2, 4)", "cd")]
    [InlineData("blob.slice(-2)", "ef")]
    [InlineData("blob.slice(-100)", "abcdef")]
    [InlineData("blob.slice(0, -2)", "abcd")]
    [InlineData("blob.slice(0, -100)", "")]
    [InlineData("blob.slice(4, 2)", "")]
    [InlineData("blob.slice(100)", "")]
    [InlineData("blob.slice(0, 100)", "abcdef")]
    // An optional argument with no default value is missing when undefined is passed explicitly.
    [InlineData("blob.slice(undefined, undefined)", "abcdef")]
    [InlineData("blob.slice(2, undefined)", "cdef")]
    // [Clamp] rounds to nearest, ties to even, and NaN becomes zero.
    [InlineData("blob.slice(1.5, 4.5)", "cd")]
    [InlineData("blob.slice(2.4)", "cdef")]
    [InlineData("blob.slice(NaN, 2)", "ab")]
    [InlineData("blob.slice(-Infinity, Infinity)", "abcdef")]
    public void SlicesByByteOrderPosition(string expression, string expected)
    {
        var engine = WebEngine();
        engine.Execute("var blob = new Blob(['abcdef']);");

        engine.Evaluate(expression + ".text()").UnwrapIfPromise().AsString().Should().Be(expected);
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void TextIsALenientBomStrippingUtf8Decode()
    {
        // https://encoding.spec.whatwg.org/#utf-8-decode strips one leading BOM ...
        Eval("new Blob([new Uint8Array([0xEF, 0xBB, 0xBF, 0x61])]).text()").UnwrapIfPromise().AsString().Should().Be("a");

        // ... and substitutes U+FFFD for an ill-formed sequence rather than rejecting.
        Eval("new Blob([new Uint8Array([0xFF])]).text()").UnwrapIfPromise().AsString().Should().Be("\uFFFD");

        // The BOM is only stripped once, and only at the start.
        Eval("new Blob([new Uint8Array([0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF])]).text()").UnwrapIfPromise().AsString().Should().Be("\uFEFF");
    }

    [Fact]
    public void HasNoStreamMethod()
    {
        // Absent rather than throwing: Jint has no streams yet, and feature detection is written against
        // absence.
        Eval("typeof Blob.prototype.stream").AsString().Should().Be("undefined");
        Eval("'stream' in Blob.prototype").AsBoolean().Should().BeFalse();
        Eval("typeof Blob.prototype.textStream").AsString().Should().Be("undefined");
    }

    [Fact]
    public void DeclaresTheArityTheIdlDoes()
    {
        var engine = WebEngine();

        engine.Evaluate("Blob.prototype.slice.length").AsNumber().Should().Be(0);
        engine.Evaluate("Blob.prototype.text.length").AsNumber().Should().Be(0);
        engine.Evaluate("Blob.prototype.arrayBuffer.length").AsNumber().Should().Be(0);
        engine.Evaluate("Blob.prototype.bytes.length").AsNumber().Should().Be(0);
    }
}
#endif
