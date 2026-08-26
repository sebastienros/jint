#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>File</c> as the File API specifies it — https://w3c.github.io/FileAPI/#file-section.
/// </summary>
public class FileTests
{
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Files));

    private static JsValue Eval(string source) => WebEngine().Evaluate(source);

    [Test]
    public void CarriesTheBitsTheNameAndTheOptions()
    {
        var engine = WebEngine();
        engine.Execute("var f = new File(['ab', 'c'], 'note.txt', { type: 'TEXT/Plain', lastModified: 42 });");

        engine.Evaluate("f.size").AsNumber().Should().Be(3);
        engine.Evaluate("f.name").AsString().Should().Be("note.txt");
        engine.Evaluate("f.type").AsString().Should().Be("text/plain");
        engine.Evaluate("f.lastModified").AsNumber().Should().Be(42);
        engine.Evaluate("f.text()").UnwrapIfPromise().AsString().Should().Be("abc");
    }

    [Test]
    public void InheritsBlob()
    {
        var engine = WebEngine();

        // The interface prototype object's [[Prototype]] is the inherited interface prototype object, and
        // the interface object's is the inherited interface object —
        // https://webidl.spec.whatwg.org/#interface-object.
        engine.Evaluate("Object.getPrototypeOf(File.prototype) === Blob.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(File) === Blob").AsBoolean().Should().BeTrue();

        engine.Evaluate("new File([], 'a') instanceof Blob").AsBoolean().Should().BeTrue();
        engine.Evaluate("new File(['abc'], 'a').slice(1).size").AsNumber().Should().Be(2);
        engine.Evaluate("Object.prototype.toString.call(new File([], 'a'))").AsString().Should().Be("[object File]");
    }

    /// <summary>
    /// <c>File</c> declares no read methods of its own, so every one of <c>Blob</c>'s is reached through the
    /// prototype chain above — including <c>textStream()</c>, https://w3c.github.io/FileAPI/#dom-blob-textstream,
    /// which brand-checks for a <c>Blob</c> and a <c>File</c> is one.
    /// </summary>
    [Test]
    public void InheritsEveryBlobReadMethodIncludingTextStream()
    {
        var engine = WebEngine();
        engine.Execute("""
            var log = [];
            var f = new File(['héllo'], 'note.txt');
            (async function () {
              const reader = f.textStream().getReader();
              for (;;) {
                const r = await reader.read();
                if (r.done) { log.push('done'); return; }
                log.push(r.value);
              }
            })();
            """);

        engine.Evaluate("log.join(',')").AsString().Should().Be("héllo,done");

        // The method itself is Blob's, not a copy — nothing here re-declares it.
        engine.Evaluate("File.prototype.hasOwnProperty('textStream')").AsBoolean().Should().BeFalse();
        engine.Evaluate("f.textStream === Blob.prototype.textStream").AsBoolean().Should().BeTrue();

        engine.Evaluate("f.text()").UnwrapIfPromise().AsString().Should().Be("héllo");
        engine.Evaluate("f.bytes()").UnwrapIfPromise().AsObject().Get("length").AsNumber().Should().Be(6);
        engine.Evaluate("Object.prototype.toString.call(f.stream())").AsString().Should().Be("[object ReadableStream]");
    }

    [Test]
    public void SlicingAFileProducesAPlainBlob()
    {
        // "Return a new Blob object S" — a slice has no name to carry.
        var engine = WebEngine();
        engine.Execute("var s = new File(['abc'], 'a.txt').slice(1);");

        engine.Evaluate("s instanceof Blob").AsBoolean().Should().BeTrue();
        engine.Evaluate("s instanceof File").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void RequiresBothOfItsRequiredArguments()
    {
        Assert.Throws<JavaScriptException>(() => Eval("new File()"));
        Assert.Throws<JavaScriptException>(() => Eval("new File(['a'])"));
        Assert.Throws<JavaScriptException>(() => Eval("File(['a'], 'b')"));

        // fileBits is a required sequence, so undefined is not a stand-in for an empty one.
        Assert.Throws<JavaScriptException>(() => Eval("new File(undefined, 'a')"));
    }

    [Test]
    public void ConvertsTheNameToAScalarValueString()
    {
        // USVString: an unpaired surrogate becomes U+FFFD.
        Eval("new File([], 'a\\uD800b').name").AsString().Should().Be("a�b");

        // A well-formed pair survives.
        Eval("new File([], 'a\\uD83D\\uDE00b').name").AsString().Should().Be("a😀b");

        // And the name is a plain string conversion otherwise.
        Eval("new File([], 5).name").AsString().Should().Be("5");
    }

    [Test]
    public void DefaultsLastModifiedToTheEngineClock()
    {
        var engine = WebEngine();

        // The default is "the current date and time ... the equivalent of Date.now()", captured once at
        // construction and never moving afterwards.
        engine.Execute("var before = Date.now(); var f = new File([], 'a'); var after = Date.now();");

        engine.Evaluate("f.lastModified >= before && f.lastModified <= after").AsBoolean().Should().BeTrue();
        engine.Evaluate("f.lastModified === f.lastModified").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ReadsLastModifiedAsAWebIdlLongLong()
    {
        // long long truncates towards zero, and every non-finite value is zero.
        Eval("new File([], 'a', { lastModified: 1.9 }).lastModified").AsNumber().Should().Be(1);
        Eval("new File([], 'a', { lastModified: -1.9 }).lastModified").AsNumber().Should().Be(-1);
        Eval("new File([], 'a', { lastModified: NaN }).lastModified").AsNumber().Should().Be(0);
        Eval("new File([], 'a', { lastModified: Infinity }).lastModified").AsNumber().Should().Be(0);
        Eval("new File([], 'a', { lastModified: '7' }).lastModified").AsNumber().Should().Be(7);
        Eval("new File([], 'a', { lastModified: 0 }).lastModified").AsNumber().Should().Be(0);
    }

    [Test]
    public void ReadsTheInheritedDictionaryMembersFirst()
    {
        var engine = WebEngine();

        // Inherited members come first, then the derived dictionary's own — so endings, type, lastModified.
        engine.Execute("""
            var seen = [];
            new File([], 'a', {
                get endings() { seen.push('endings'); return 'transparent'; },
                get lastModified() { seen.push('lastModified'); return 0; },
                get type() { seen.push('type'); return ''; } });
            """);

        engine.Evaluate("seen.join(',')").AsString().Should().Be("endings,type,lastModified");
    }

    [Test]
    public void BrandChecksNameAndLastModified()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("File.prototype.name"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("File.prototype.lastModified"));

        // A plain blob is not a file, even though it passes Blob's own brand check.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Object.getOwnPropertyDescriptor(File.prototype, 'name').get.call(new Blob())"));
    }

    [Test]
    public void ExposesNameAndLastModifiedAsAccessorsOnItsOwnPrototype()
    {
        var engine = WebEngine();

        engine.Evaluate("Object.getOwnPropertyNames(new File([], 'a')).length").AsNumber().Should().Be(0);
        engine.Evaluate("Object.getOwnPropertyNames(File.prototype).sort().join(',')").AsString()
            .Should().Be("constructor,lastModified,name");

        engine.Evaluate("var d = Object.getOwnPropertyDescriptor(File.prototype, 'name'); d.enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate("d.configurable").AsBoolean().Should().BeTrue();
        engine.Evaluate("d.set").IsUndefined().Should().BeTrue();
    }

    [Test]
    public void DeclaresTheArityTheIdlDoes()
    {
        var engine = WebEngine();

        engine.Evaluate("File.length").AsNumber().Should().Be(2);
        engine.Evaluate("File.name").AsString().Should().Be("File");
        engine.Evaluate("new File([], 'a').constructor === File").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void SupportsSubclassing()
    {
        var engine = WebEngine();

        engine.Execute("class MyFile extends File {}");
        engine.Evaluate("new MyFile(['ab'], 'n').name").AsString().Should().Be("n");
        engine.Evaluate("new MyFile(['ab'], 'n').size").AsNumber().Should().Be(2);
        engine.Evaluate("new MyFile(['ab'], 'n') instanceof Blob").AsBoolean().Should().BeTrue();
    }
}
#endif
