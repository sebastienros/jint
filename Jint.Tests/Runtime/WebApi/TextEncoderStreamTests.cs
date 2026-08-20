#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>TextEncoderStream</c> — https://encoding.spec.whatwg.org/#interface-textencoderstream.
/// </summary>
/// <remarks>
/// The interesting half is the leading-surrogate carry: chunks are <c>DOMString</c>s rather than
/// <c>USVString</c>s exactly so that a surrogate pair split across two of them still encodes as one scalar
/// value, and every other lone surrogate is U+FFFD.
/// </remarks>
public class TextEncoderStreamTests
{
    /// <summary>
    /// Both flags: the interface is one standard's algorithm running inside another's machinery, so the
    /// engine needs to have been asked for both.
    /// </summary>
    private static Engine StreamEngine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Encoding | WebApiFeatures.Streams));
        engine.Execute("""
            var log = [];
            async function collect(stream, label) {
              const reader = stream.getReader();
              for (;;) {
                let result;
                try { result = await reader.read(); }
                catch (e) { log.push(label + ':error:' + e.name); return; }
                if (result.done) { log.push(label + ':done'); return; }
                log.push(label + ':' + Array.from(result.value).join(' '));
              }
            }
            """);
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    [Fact]
    public void ExposesTheGenericTransformStreamMixin()
    {
        var engine = StreamEngine();
        engine.Execute("var s = new TextEncoderStream();");

        engine.Evaluate("s.readable instanceof ReadableStream").AsBoolean().Should().BeTrue();
        engine.Evaluate("s.writable instanceof WritableStream").AsBoolean().Should().BeTrue();
        engine.Evaluate("s.readable === s.readable && s.writable === s.writable").AsBoolean().Should().BeTrue();
        engine.Evaluate("s.encoding").AsString().Should().Be("utf-8");
        engine.Evaluate("Object.prototype.toString.call(s)").AsString().Should().Be("[object TextEncoderStream]");
        engine.Evaluate("TextEncoderStream.length").AsNumber().Should().Be(0);
        engine.Evaluate("TextEncoderStream.name").AsString().Should().Be("TextEncoderStream");

        // The transform itself is never exposed: the instance owns one, it is not one.
        engine.Evaluate("s instanceof TransformStream").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.getOwnPropertyNames(s).length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void EncodesEachChunkAsUtf8()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextEncoderStream();
            collect(s.readable, 'out');
            var writer = s.writable.getWriter();
            writer.write('hi');
            writer.write('€');
            writer.close();
            """);

        Log(engine).Should().Be("out:104 105,out:226 130 172,out:done");
    }

    [Fact]
    public void EnqueuesNothingForAChunkThatProducesNoBytes()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextEncoderStream();
            collect(s.readable, 'out');
            var writer = s.writable.getWriter();
            writer.write('');
            writer.write('x');
            writer.close();
            """);

        // "If output is not empty": the empty chunk enqueues nothing at all rather than an empty array.
        Log(engine).Should().Be("out:120,out:done");
    }

    [Fact]
    public void ReassemblesASurrogatePairSplitAcrossTwoChunks()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextEncoderStream();
            collect(s.readable, 'out');
            var writer = s.writable.getWriter();
            writer.write('a\uD83D');
            writer.write('\uDE00b');
            writer.close();
            """);

        // The high surrogate is held back — the first chunk enqueues only 'a', not 'a' plus U+FFFD — and
        // the pair then encodes as the one scalar value U+1F600. Encoding the two chunks independently
        // would have produced 97 239 191 189 and 239 191 189 98 instead.
        Log(engine).Should().Be("out:97,out:240 159 152 128 98,out:done");
    }

    [Fact]
    public void AHeldSurrogateThatIsNeverCompletedBecomesTheReplacementCharacter()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextEncoderStream();
            collect(s.readable, 'out');
            var writer = s.writable.getWriter();
            writer.write('\uD83D');
            writer.write('x');
            writer.close();
            """);

        // "Restore item to input and return U+FFFD": the code unit that failed to complete the pair is not
        // consumed, so it is encoded after the replacement character rather than dropped.
        Log(engine).Should().Be("out:239 191 189 120,out:done");
    }

    [Fact]
    public void ASurrogateLeftPendingAtTheEndIsFlushedAsTheReplacementCharacter()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextEncoderStream();
            collect(s.readable, 'out');
            var writer = s.writable.getWriter();
            writer.write('a\uD83D');
            writer.close();
            """);

        // "Encode and flush": « 0xEF, 0xBF, 0xBD ».
        Log(engine).Should().Be("out:97,out:239 191 189,out:done");
    }

    [Fact]
    public void ALoneTrailingSurrogateIsTheReplacementCharacter()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextEncoderStream();
            collect(s.readable, 'out');
            var writer = s.writable.getWriter();
            writer.write('\uDE00a');
            writer.close();
            """);

        Log(engine).Should().Be("out:239 191 189 97,out:done");
    }

    [Fact]
    public void ChunksAreConvertedToStringsRatherThanRefused()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextEncoderStream();
            collect(s.readable, 'out');
            var writer = s.writable.getWriter();
            writer.write(42);
            writer.write({ toString() { return 'obj'; } });
            writer.close();
            """);

        // The chunk is a DOMString, so anything at all is converted rather than rejected.
        Log(engine).Should().Be("out:52 50,out:111 98 106,out:done");
    }

    [Fact]
    public void ProducesUint8ArraysInTheEnginesOwnRealm()
    {
        var engine = StreamEngine();
        var result = engine.Evaluate("""
            (async () => {
              const s = new TextEncoderStream();
              const writer = s.writable.getWriter();
              writer.write('hi');
              writer.close();
              const chunk = (await s.readable.getReader().read()).value;
              return (chunk instanceof Uint8Array) + ':' + chunk.constructor.name + ':' + chunk.byteLength;
            })()
            """).UnwrapIfPromise();

        result.AsString().Should().Be("true:Uint8Array:2");
    }

    [Fact]
    public void ComposesWithPipeThrough()
    {
        var engine = StreamEngine();
        var result = engine.Evaluate("""
            (async () => {
              const source = new ReadableStream({
                start(c) { c.enqueue('héllo '); c.enqueue('wörld'); c.close(); }
              });
              const bytes = [];
              for await (const chunk of source.pipeThrough(new TextEncoderStream())) {
                bytes.push(...chunk);
              }
              return bytes.join(',');
            })()
            """).UnwrapIfPromise();

        // 104 233 -> "h" plus the two-byte é, and likewise for ö.
        result.AsString().Should().Be("104,195,169,108,108,111,32,119,195,182,114,108,100");
    }

    [Fact]
    public void RoundTripsThroughTextDecoderStream()
    {
        var engine = StreamEngine();
        var result = engine.Evaluate("""
            (async () => {
              const source = new ReadableStream({
                start(c) { c.enqueue('a\uD83D'); c.enqueue('\uDE00b'); c.close(); }
              });
              let text = '';
              for await (const chunk of source.pipeThrough(new TextEncoderStream()).pipeThrough(new TextDecoderStream())) {
                text += chunk;
              }
              return text === 'a😀b';
            })()
            """).UnwrapIfPromise();

        result.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void TheAttributesBrandCheckTheirReceiver()
    {
        var engine = StreamEngine();

        foreach (var attribute in new[] { "encoding", "readable", "writable" })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate(
                    $"Object.getOwnPropertyDescriptor(TextEncoderStream.prototype, '{attribute}').get.call({{}})"))
                .Error.Get("name").AsString().Should().Be("TypeError", attribute);

            // The prototype itself is not an instance either, and neither is the other transform stream.
            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"TextEncoderStream.prototype.{attribute}"))
                .Error.Get("name").AsString().Should().Be("TypeError", attribute);
        }

        Assert.Throws<JavaScriptException>(() => engine.Evaluate(
                "Object.getOwnPropertyDescriptor(TextEncoderStream.prototype, 'readable').get.call(new TextDecoderStream())"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void RequiresNewAndBothFeatureFlags()
    {
        var engine = StreamEngine();
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("TextEncoderStream()"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        // Either flag on its own installs neither of the two streams: they are useless without the other
        // half, and an absent global is the honest answer for feature detection.
        new Engine(options => options.UseWebApis(WebApiFeatures.Encoding))
            .Evaluate("typeof TextEncoderStream").AsString().Should().Be("undefined");
        new Engine(options => options.UseWebApis(WebApiFeatures.Streams))
            .Evaluate("typeof TextEncoderStream").AsString().Should().Be("undefined");
    }
}
#endif
