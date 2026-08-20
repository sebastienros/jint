#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>TextDecoderStream</c> — https://encoding.spec.whatwg.org/#interface-textdecoderstream.
/// </summary>
/// <remarks>
/// It is the same decoding a <c>TextDecoder</c> does, driven the way a script would drive one: every chunk
/// is a streaming decode and closing the writable side is the final flush. The tests that matter are
/// therefore the ones about what happens at a chunk boundary — a split sequence, a split BOM, a sequence
/// that never completes.
/// </remarks>
public class TextDecoderStreamTests
{
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
                log.push(label + ':' + result.value);
              }
            }
            function bytes() { return new Uint8Array(Array.prototype.slice.call(arguments)); }
            """);
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    [Fact]
    public void ExposesTheMixinsAndTheDecoderAttributes()
    {
        var engine = StreamEngine();
        engine.Execute("var s = new TextDecoderStream();");

        engine.Evaluate("s.readable instanceof ReadableStream").AsBoolean().Should().BeTrue();
        engine.Evaluate("s.writable instanceof WritableStream").AsBoolean().Should().BeTrue();
        engine.Evaluate("[s.encoding, s.fatal, s.ignoreBOM].join(',')").AsString().Should().Be("utf-8,false,false");
        engine.Evaluate("Object.prototype.toString.call(s)").AsString().Should().Be("[object TextDecoderStream]");
        engine.Evaluate("TextDecoderStream.length").AsNumber().Should().Be(0);
        engine.Evaluate("TextDecoderStream.name").AsString().Should().Be("TextDecoderStream");

        engine.Evaluate("new TextDecoderStream('UTF-16LE', { fatal: true, ignoreBOM: true }).encoding")
            .AsString().Should().Be("utf-16le");
        engine.Evaluate("new TextDecoderStream(undefined, { fatal: true }).fatal").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void TakesTheSameConstructorArgumentsAsTextDecoder()
    {
        var engine = StreamEngine();

        // windows-1252 became a supported single-byte encoding when the legacy tables landed, so the
        // still-refused boundary is a legacy multi-byte label — RangeError, exactly as TextDecoder reports it.
        engine.Evaluate("new TextDecoderStream('windows-1252').encoding").AsString().Should().Be("windows-1252");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new TextDecoderStream('big5')"))
            .Error.Get("name").AsString().Should().Be("RangeError");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new TextDecoderStream('utf-8', 42)"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        // The options are read before the label is validated, which is what the WebIDL conversion order
        // requires: a getter on the dictionary runs even when the label is nonsense.
        engine.Execute("var read = []; var options = { get fatal() { read.push('fatal'); return false; } };");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new TextDecoderStream('nonsense', options)"));
        engine.Evaluate("read.join(',')").AsString().Should().Be("fatal");
    }

    [Fact]
    public void DecodesEachChunk()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextDecoderStream();
            collect(s.readable, 'out');
            var writer = s.writable.getWriter();
            writer.write(bytes(104, 105));
            writer.write(bytes(226, 130, 172));
            writer.close();
            """);

        Log(engine).Should().Be("out:hi,out:€,out:done");
    }

    [Fact]
    public void JoinsASequenceSplitAcrossChunks()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextDecoderStream();
            collect(s.readable, 'out');
            var writer = s.writable.getWriter();
            writer.write(bytes(226, 130));
            writer.write(bytes(172));
            writer.close();
            """);

        // The first chunk holds an incomplete sequence, so it enqueues nothing at all — "if outputChunk is
        // not the empty string" — and the euro sign appears once the third byte arrives.
        Log(engine).Should().Be("out:€,out:done");
    }

    [Fact]
    public void ASequenceLeftIncompleteAtTheEndBecomesTheReplacementCharacter()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextDecoderStream();
            collect(s.readable, 'out');
            var writer = s.writable.getWriter();
            writer.write(bytes(97, 226, 130));
            writer.close();
            """);

        // The flush is what ends the stream, and an incomplete sequence at that point is U+FFFD.
        Log(engine).Should().Be("out:a,out:�,out:done");
    }

    [Fact]
    public void AFatalDecoderErrorsBothSides()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextDecoderStream('utf-8', { fatal: true });
            collect(s.readable, 'out');
            var writer = s.writable.getWriter();
            writer.write(bytes(0xC0, 0x80)).catch(e => log.push('write:' + e.name));
            writer.closed.catch(e => log.push('writable:' + e.name));
            """);

        // "If the error mode is fatal and the decoder returns error, both readable and writable will be
        // errored with a TypeError."
        Log(engine).Should().Be("out:error:TypeError,write:TypeError,writable:TypeError");
    }

    [Fact]
    public void AFatalDecoderAlsoRefusesAnIncompleteSequenceAtTheEnd()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextDecoderStream('utf-8', { fatal: true });
            collect(s.readable, 'out');
            var writer = s.writable.getWriter();
            writer.write(bytes(226, 130));
            writer.close().catch(e => log.push('close:' + e.name));
            """);

        Log(engine).Should().Be("out:error:TypeError,close:TypeError");
    }

    [Fact]
    public void DropsALeadingByteOrderMarkUnlessAskedNotTo()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextDecoderStream();
            collect(s.readable, 'first');
            var writer = s.writable.getWriter();
            writer.write(bytes(0xEF, 0xBB, 0xBF, 104, 105));
            writer.close();
            """);

        Log(engine).Should().Be("first:hi,first:done");

        // With ignoreBOM the mark is part of the output; the code points say so without an invisible
        // character in the expectation.
        engine.Evaluate("""
            (async () => {
              const s = new TextDecoderStream('utf-8', { ignoreBOM: true });
              const writer = s.writable.getWriter();
              writer.write(new Uint8Array([0xEF, 0xBB, 0xBF, 104, 105]));
              writer.close();
              let text = '';
              for await (const chunk of s.readable) { text += chunk; }
              return Array.from(text).map(c => c.charCodeAt(0)).join(',');
            })()
            """).UnwrapIfPromise().AsString().Should().Be("65279,104,105");
    }

    [Fact]
    public void DropsAByteOrderMarkSplitAcrossChunks()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextDecoderStream();
            collect(s.readable, 'out');
            var writer = s.writable.getWriter();
            writer.write(bytes(0xEF));
            writer.write(bytes(0xBB));
            writer.write(bytes(0xBF, 104, 105));
            writer.close();
            """);

        // The BOM is one stream's worth of state, not one chunk's: the first two chunks decode to nothing
        // and the third produces "hi" with the mark already removed.
        Log(engine).Should().Be("out:hi,out:done");
    }

    [Fact]
    public void RefusesAChunkThatIsNotABufferSource()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextDecoderStream();
            collect(s.readable, 'out');
            var writer = s.writable.getWriter();
            writer.write('not bytes').catch(e => log.push('write:' + e.name));
            """);

        Log(engine).Should().Be("out:error:TypeError,write:TypeError");
    }

    [Fact]
    public void AcceptsEveryBufferSourceShape()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextDecoderStream();
            collect(s.readable, 'out');
            var writer = s.writable.getWriter();
            var buffer = new Uint8Array([104, 105]).buffer;
            writer.write(buffer);
            writer.write(new DataView(new Uint8Array([33]).buffer));
            writer.close();
            """);

        Log(engine).Should().Be("out:hi,out:!,out:done");
    }

    [Fact]
    public void DecodesUtf16LikeTextDecoderDoes()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var s = new TextDecoderStream('utf-16le');
            collect(s.readable, 'out');
            var writer = s.writable.getWriter();
            writer.write(bytes(104, 0, 105));
            writer.write(bytes(0));
            writer.close();
            """);

        // The second code unit's bytes arrive in two chunks, and the pair is still one character.
        Log(engine).Should().Be("out:h,out:i,out:done");
    }

    [Fact]
    public void ComposesWithPipeThrough()
    {
        var engine = StreamEngine();
        var result = engine.Evaluate("""
            (async () => {
              const source = new ReadableStream({
                start(c) {
                  c.enqueue(new Uint8Array([0xF0, 0x9F]));
                  c.enqueue(new Uint8Array([0x98, 0x80, 33]));
                  c.close();
                }
              });
              let text = '';
              for await (const chunk of source.pipeThrough(new TextDecoderStream())) { text += chunk; }
              return text;
            })()
            """).UnwrapIfPromise();

        result.AsString().Should().Be("😀!");
    }

    [Fact]
    public void TheAttributesBrandCheckTheirReceiver()
    {
        var engine = StreamEngine();

        foreach (var attribute in new[] { "encoding", "fatal", "ignoreBOM", "readable", "writable" })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"TextDecoderStream.prototype.{attribute}"))
                .Error.Get("name").AsString().Should().Be("TypeError", attribute);
        }

        // A TextDecoder includes the same TextDecoderCommon mixin but is a different interface.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate(
                "Object.getOwnPropertyDescriptor(TextDecoderStream.prototype, 'encoding').get.call(new TextDecoder())"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("TextDecoderStream()"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }
}
#endif
