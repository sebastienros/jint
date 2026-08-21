#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>CompressionStream</c> and <c>DecompressionStream</c> — https://compression.spec.whatwg.org/.
/// </summary>
/// <remarks>
/// The pin that matters most is <see cref="DeflateIsTheZlibWrapperNotRawDeflate"/>: the standard's
/// <c>"deflate"</c> is RFC 1950's ZLIB container, and only <c>"deflate-raw"</c> is RFC 1951's bare bit
/// stream. The vectors the tests decompress were produced by CPython's <c>zlib</c> module, so they are an
/// independent implementation's idea of each format rather than a round trip against ourselves — with the
/// exception of <see cref="BclBrotliVector"/>, whose provenance is stated on it.
/// </remarks>
public class CompressionStreamTests
{
    /// <summary>"Hello, Jint!" as zlib-wrapped DEFLATE — <c>zlib.compress(b"Hello, Jint!", 6)</c>.</summary>
    private const string ZlibVector = "120,156,243,72,205,201,201,215,81,240,202,204,43,81,4,0,26,156,3,247";

    /// <summary>
    /// The same payload as raw DEFLATE — <c>zlib.compressobj(6, zlib.DEFLATED, -15)</c>. It is exactly the
    /// zlib vector without its two-byte header and four-byte ADLER32, which is the whole difference between
    /// the two formats.
    /// </summary>
    private const string RawDeflateVector = "243,72,205,201,201,215,81,240,202,204,43,81,4,0";

    /// <summary>The same payload as gzip — <c>gzip.GzipFile(mtime=0, compresslevel=6)</c>.</summary>
    private const string GzipVector =
        "31,139,8,0,0,0,0,0,0,255,243,72,205,201,201,215,81,240,202,204,43,81,4,0,123,253,209,10,12,0,0,0";

    /// <summary>
    /// The same payload as brotli, and <b>this one was produced by the BCL</b> —
    /// <c>new BrotliStream(output, CompressionMode.Compress)</c> at its default level, which emitted these
    /// same bytes on .NET 8 and on .NET 10. It is therefore not an interop vector; it is a fixed blob to
    /// decode, so the tests that read it do not depend on this engine's encoder at all. Nothing asserts that
    /// the encoder still produces it — that would pin a platform's framing choice, which no part of the
    /// standard fixes. <see cref="ForeignBrotliVector"/> is the independent one.
    /// </summary>
    private const string BclBrotliVector = "139,5,128,72,101,108,108,111,44,32,74,105,110,116,33,3";

    /// <summary>
    /// <c>"expected output"</c> as brotli, taken from web-platform-tests'
    /// <c>compression/resources/decompression-input.js</c> and therefore produced by a brotli encoder that is
    /// not .NET's. It is a genuinely independent vector, and it exercises a shape .NET's own encoder never
    /// emits for this input: the payload is stored literally (readable as ASCII in the bytes below) in an
    /// uncompressed meta-block, where <see cref="BclBrotliVector"/> is Huffman-coded.
    /// </summary>
    private const string ForeignBrotliVector = "33,56,0,4,101,120,112,101,99,116,101,100,32,111,117,116,112,117,116,3";

    private const string Payload = "Hello, Jint!";

    private static Engine StreamEngine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Compression | WebApiFeatures.Streams | WebApiFeatures.Encoding));
        engine.Execute("""
            var log = [];

            // Everything below is byte arrays, so a chunk is reported as its bytes and a stream as one
            // concatenated array.
            async function drain(stream, label) {
              const reader = stream.getReader();
              const bytes = [];
              for (;;) {
                let result;
                try { result = await reader.read(); }
                catch (e) { log.push(label + ':error:' + e.name + ':' + e.message); return null; }
                if (result.done) { return bytes; }
                if (!(result.value instanceof Uint8Array)) { log.push(label + ':not-a-uint8array'); return null; }
                for (const b of result.value) { bytes.push(b); }
              }
            }

            function feed(writable, bytes, chunkSize) {
              const writer = writable.getWriter();
              for (let i = 0; i < bytes.length; i += chunkSize) {
                writer.write(new Uint8Array(bytes.slice(i, i + chunkSize))).catch(() => {});
              }
              writer.close().catch(e => log.push('close:' + e.name));
            }

            async function decompress(format, bytes, chunkSize) {
              const ds = new DecompressionStream(format);
              const output = drain(ds.readable, format);
              feed(ds.writable, bytes, chunkSize || bytes.length);
              const result = await output;
              return result === null ? null : new TextDecoder().decode(new Uint8Array(result));
            }

            async function compress(format, text) {
              const cs = new CompressionStream(format);
              const output = drain(cs.readable, format);
              feed(cs.writable, Array.from(new TextEncoder().encode(text)), 1024);
              return await output;
            }

            // The vectors arrive as the comma-separated text the C# side holds them in, so nothing about
            // these tests depends on how a CLR array is projected into script.
            function vector(text) { return text.split(',').map(Number); }
            """);
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    [Theory]
    [InlineData("gzip", GzipVector)]
    [InlineData("deflate", ZlibVector)]
    [InlineData("deflate-raw", RawDeflateVector)]
    public void DecompressesAnotherImplementationsBytes(string format, string bytes)
    {
        var engine = StreamEngine();

        var text = engine.Evaluate($"decompress('{format}', vector('{bytes}'))").UnwrapIfPromise();

        text.AsString().Should().Be(Payload);
        Log(engine).Should().BeEmpty();
    }

    [Theory]
    [InlineData("gzip")]
    [InlineData("deflate")]
    [InlineData("deflate-raw")]
    [InlineData("brotli")]
    public void RoundTripsThroughItsOwnCompressor(string format)
    {
        var engine = StreamEngine();

        var text = engine.Evaluate($$"""
            (async () => {
              const original = 'the quick brown fox '.repeat(200) + 'é€😀';
              const compressed = await compress('{{format}}', original);
              const back = await decompress('{{format}}', compressed);
              return (back === original) + ':' + (compressed.length < original.length);
            })()
            """).UnwrapIfPromise();

        // Round trips, and actually compressed: the payload is repetitive enough that it must shrink.
        text.AsString().Should().Be("true:true");
        Log(engine).Should().BeEmpty();
    }

    [Fact]
    public void ProducesTheFormatsOwnFraming()
    {
        var engine = StreamEngine();

        var checks = engine.Evaluate("""
            (async () => {
              const gzip = await compress('gzip', 'Hello, Jint!');
              const zlib = await compress('deflate', 'Hello, Jint!');
              const raw = await compress('deflate-raw', 'Hello, Jint!');
              return [
                // RFC 1952's magic number and "the only valid value of the CM field is 8".
                'gzip:' + (gzip[0] === 0x1F && gzip[1] === 0x8B && gzip[2] === 8),
                // RFC 1950's CMF/FLG: the low nibble of CMF is the compression method (8), and the two
                // bytes read as a big-endian number are a multiple of 31.
                'cmf:' + ((zlib[0] & 0x0F) === 8),
                'fcheck:' + (((zlib[0] * 256 + zlib[1]) % 31) === 0),
                // The whole of the difference between the two deflate formats: zlib is the same bit stream
                // with a two-byte header and a four-byte ADLER32 around it.
                'wrapper:' + (zlib.length - raw.length === 6),
                'body:' + (zlib.slice(2, -4).join(',') === raw.join(','))
              ].join(' ');
            })()
            """).UnwrapIfPromise();

        checks.AsString().Should().Be("gzip:true cmf:true fcheck:true wrapper:true body:true");
    }

    [Fact]
    public void DeflateIsTheZlibWrapperNotRawDeflate()
    {
        // The classic implementation mistake: mapping "deflate" onto a bare DeflateStream. Each format
        // refuses the other's bytes, and this is the test that fails the moment the mapping slips.
        var engine = StreamEngine();
        engine.Execute($"var zlib = vector('{ZlibVector}'); var raw = vector('{RawDeflateVector}');");

        engine.Evaluate("decompress('deflate-raw', zlib)").UnwrapIfPromise().IsNull().Should().BeTrue();
        engine.Evaluate("decompress('deflate', raw)").UnwrapIfPromise().IsNull().Should().BeTrue();

        Log(engine).Should().Contain("deflate-raw:error:TypeError").And.Contain("deflate:error:TypeError");

        // ... and each one accepts its own.
        engine.Execute("log.length = 0;");
        engine.Evaluate("decompress('deflate', zlib)").UnwrapIfPromise().AsString().Should().Be(Payload);
        engine.Evaluate("decompress('deflate-raw', raw)").UnwrapIfPromise().AsString().Should().Be(Payload);
        Log(engine).Should().BeEmpty();
    }

    [Theory]
    [InlineData("gzip", GzipVector)]
    [InlineData("deflate", ZlibVector)]
    [InlineData("deflate-raw", RawDeflateVector)]
    [InlineData("brotli", BclBrotliVector)]
    public void DecompressesInputSplitOneByteAtATime(string format, string bytes)
    {
        var engine = StreamEngine();

        // A member split across as many chunks as it has bytes decodes to exactly what the whole sequence
        // does: the context is per stream, not per chunk.
        engine.Evaluate($"decompress('{format}', vector('{bytes}'), 1)").UnwrapIfPromise().AsString().Should().Be(Payload);
        Log(engine).Should().BeEmpty();
    }

    [Fact]
    public void CompressedOutputIsSplitIntoUint8ArrayChunks()
    {
        var engine = StreamEngine();

        var shapes = engine.Evaluate("""
            (async () => {
              const cs = new CompressionStream('gzip');
              const reader = cs.readable.getReader();
              const shapes = [];
              const collect = (async () => {
                for (;;) {
                  const r = await reader.read();
                  if (r.done) { return; }
                  shapes.push(r.value.constructor.name + '(' + (r.value.byteLength > 0) + ')');
                }
              })();
              const writer = cs.writable.getWriter();
              writer.write(new Uint8Array(200000));
              writer.close();
              await collect;
              return shapes.length > 1 ? 'many:' + shapes[0] : 'one:' + shapes[0];
            })()
            """).UnwrapIfPromise();

        // "Splitting buffer into one or more non-empty pieces": every piece is a non-empty Uint8Array,
        // whether the compressor hands the output over in one or in several.
        shapes.AsString().Should().EndWith("Uint8Array(true)");
    }

    [Fact]
    public void CorruptInputErrorsBothSides()
    {
        var engine = StreamEngine();
        engine.Execute($"var gzip = vector('{GzipVector}');");

        engine.Execute("""
            var corrupt = gzip.slice();
            corrupt[15] = corrupt[15] ^ 0xFF;
            var ds = new DecompressionStream('gzip');
            drain(ds.readable, 'out');
            var writer = ds.writable.getWriter();
            writer.write(new Uint8Array(corrupt)).catch(e => log.push('write:' + e.name));
            writer.closed.catch(e => log.push('writable:' + e.name));
            """);

        Log(engine).Should().StartWith("out:error:TypeError:");
        Log(engine).Should().Contain("write:TypeError").And.Contain("writable:TypeError");
    }

    [Fact]
    public void ClosingADecompressionStreamThatReceivedNothingIsAnError()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var ds = new DecompressionStream('gzip');
            drain(ds.readable, 'out');
            ds.writable.getWriter().close().catch(e => log.push('close:' + e.name));
            """);

        // "If the end of the compressed input has not been reached, then throw a TypeError": no bytes at
        // all cannot be a complete member in any of the three formats.
        Log(engine).Should().StartWith("out:error:TypeError:");
        Log(engine).Should().Contain("close:TypeError");
    }

    [Fact]
    public void ClosingACompressionStreamThatReceivedNothingProducesAnEmptyMember()
    {
        var engine = StreamEngine();

        // Compressing nothing is not an error — it is a valid, tiny member that decompresses to nothing.
        // The lengths are each format's empty member: a gzip header and trailer, a zlib header and
        // ADLER32, and one final empty DEFLATE block. They are exact because CompressionCodec emits them
        // from constants of its own, the BCL's deflate-family encoders writing nothing at all for no input.
        var text = engine.Evaluate("""
            (async () => {
              const lengths = [];
              for (const format of ['gzip', 'deflate', 'deflate-raw']) {
                const compressed = await compress(format, '');
                const back = await decompress(format, compressed);
                lengths.push(format + ':' + compressed.length + ':' + (back === '' ? 'empty' : back));
              }
              return lengths.join(' ');
            })()
            """).UnwrapIfPromise();

        text.AsString().Should().Be("gzip:20:empty deflate:8:empty deflate-raw:2:empty");
        Log(engine).Should().BeEmpty();
    }

    [Fact]
    public void ClosingABrotliCompressionStreamThatReceivedNothingProducesAnEmptyStream()
    {
        var engine = StreamEngine();

        // Brotli is the one format with no constant behind it: BrotliStream writes the empty stream itself
        // when it is closed, so these bytes are the encoder's. The assertion is therefore the property that
        // matters — some bytes, and they decode to nothing — rather than a length, which would pin an
        // encoder's framing choice the standard leaves free.
        var text = engine.Evaluate("""
            (async () => {
              const compressed = await compress('brotli', '');
              const back = await decompress('brotli', compressed);
              return (compressed.length > 0) + ':' + (back === '' ? 'empty' : back);
            })()
            """).UnwrapIfPromise();

        text.AsString().Should().Be("true:empty");
        Log(engine).Should().BeEmpty();
    }

    [Fact]
    public void DecompressesBrotliBytesItDidNotProduce()
    {
        var engine = StreamEngine();

        // The independent vector first — a stored meta-block this engine's encoder would never emit for that
        // input — and then the fixed BCL blob, whole and split one byte at a time.
        engine.Evaluate($"decompress('brotli', vector('{ForeignBrotliVector}'))")
            .UnwrapIfPromise().AsString().Should().Be("expected output");
        engine.Evaluate($"decompress('brotli', vector('{BclBrotliVector}'))")
            .UnwrapIfPromise().AsString().Should().Be(Payload);

        // https://compression.spec.whatwg.org/#decompressionstream: an empty payload is still a stream, and
        // this is the two-byte one web-platform-tests uses. It decodes to no chunks at all, not an error.
        engine.Evaluate("decompress('brotli', [0xa1, 0x01])").UnwrapIfPromise().AsString().Should().BeEmpty();

        Log(engine).Should().BeEmpty();
    }

    [Fact]
    public void RoundTripsBrotliOneByteAtATimeOnBothSides()
    {
        var engine = StreamEngine();

        // A payload longer than one chunk of anything, written one byte at a time and read back one byte at
        // a time: the compression context is per stream, not per chunk, on both sides.
        var text = engine.Evaluate("""
            (async () => {
              const original = 'brotli '.repeat(500) + 'é€😀';
              const compressed = await compress('brotli', original);
              const back = await decompress('brotli', compressed, 1);
              return (back === original) + ':' + (compressed.length < original.length);
            })()
            """).UnwrapIfPromise();

        text.AsString().Should().Be("true:true");
        Log(engine).Should().BeEmpty();
    }

    [Fact]
    public void RefusesBytesTheBrotliDecoderCannotParse()
    {
        var engine = StreamEngine();

        // BrotliStream reports data it cannot parse as InvalidOperationException where the deflate family
        // raises InvalidDataException — a BCL naming inconsistency DecompressionCodec translates. What is
        // pinned here is only what script sees: the standard's TypeError on both sides, exactly as for the
        // other three formats. If the translation is dropped, the CLR exception escapes Evaluate instead.
        engine.Execute($"var gzip = vector('{GzipVector}');");

        engine.Evaluate("decompress('brotli', gzip)").UnwrapIfPromise().IsNull().Should().BeTrue();
        Log(engine).Should().Contain("brotli:error:TypeError");

        engine.Execute("log.length = 0;");
        engine.Execute("""
            var ds = new DecompressionStream('brotli');
            drain(ds.readable, 'out');
            var writer = ds.writable.getWriter();
            writer.write(new Uint8Array([255, 255, 255, 255, 255, 255, 255, 255])).catch(e => log.push('write:' + e.name));
            writer.closed.catch(e => log.push('writable:' + e.name));
            """);

        Log(engine).Should().StartWith("out:error:TypeError:");
        Log(engine).Should().Contain("write:TypeError").And.Contain("writable:TypeError");

        // And the format names itself in the message, so a script's console says which one refused.
        Log(engine).Should().Contain("not valid brotli data");
    }

    [Fact]
    public void BrotliSharesTheLenientTruncationDivergence()
    {
        var engine = StreamEngine();

        // The divergence DecompressionCodec documents on itself, asserted rather than assumed: .NET exposes
        // no decoder that reports how many bytes it consumed, so a stream that ends mid-member closes
        // cleanly and bytes after a complete stream are ignored. BrotliStream is no different from the
        // deflate family here, which is what puts brotli's two rows of
        // compression/decompression-corrupt-input.any.js under WptDivergence.NeedsIncrementalInflater
        // alongside them. https://compression.spec.whatwg.org/#decompressionstream calls both an error.
        engine.Execute($"var full = vector('{BclBrotliVector}');");

        var text = engine.Evaluate("""
            (async () => {
              const truncated = await decompress('brotli', full.slice(0, -1));
              const extended = await decompress('brotli', full.concat([0]));
              return 'truncated:' + JSON.stringify(truncated) + ' trailing:' + JSON.stringify(extended);
            })()
            """).UnwrapIfPromise();

        // Both are what the standard would have errored, and neither loses the payload that did decode.
        text.AsString().Should().Be($"truncated:\"{Payload}\" trailing:\"{Payload}\"");
        Log(engine).Should().BeEmpty();

        // The one truncation that is caught stays caught for brotli too: no compressed bytes at all.
        engine.Execute("""
            var ds = new DecompressionStream('brotli');
            drain(ds.readable, 'nothing');
            ds.writable.getWriter().close().catch(e => log.push('close:' + e.name));
            """);
        Log(engine).Should().StartWith("nothing:error:TypeError:").And.Contain("close:TypeError");
    }

    [Fact]
    public void RefusesAChunkThatIsNotABufferSource()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var cs = new CompressionStream('gzip');
            drain(cs.readable, 'out');
            cs.writable.getWriter().write('not bytes').catch(e => log.push('write:' + e.name));
            """);

        Log(engine).Should().Be("out:error:TypeError:CompressionStream: the chunk is not an ArrayBuffer or a view over one,write:TypeError");
    }

    [Fact]
    public void ComposesWithPipeThrough()
    {
        var engine = StreamEngine();

        var text = engine.Evaluate("""
            (async () => {
              const source = new ReadableStream({
                start(c) { c.enqueue(new TextEncoder().encode('round ')); c.enqueue(new TextEncoder().encode('trip')); c.close(); }
              });
              let text = '';
              const piped = source
                .pipeThrough(new CompressionStream('deflate'))
                .pipeThrough(new DecompressionStream('deflate'))
                .pipeThrough(new TextDecoderStream());
              for await (const chunk of piped) { text += chunk; }
              return text;
            })()
            """).UnwrapIfPromise();

        text.AsString().Should().Be("round trip");
    }

    [Theory]
    [InlineData("GZIP")]
    [InlineData("Deflate")]
    [InlineData("Brotli")]
    [InlineData("deflate_raw")]
    [InlineData("br")]
    [InlineData("")]
    public void RefusesAFormatItDoesNotSupport(string format)
    {
        var engine = StreamEngine();
        engine.SetValue("format", format);

        // https://webidl.spec.whatwg.org/#es-enumeration matches the identifier exactly and
        // case-sensitively, so none of these is the enumeration value it resembles. "br" in particular is
        // the HTTP Content-Encoding token for brotli and is not what CompressionFormat calls it.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new CompressionStream(format)"))
            .Error.Get("name").AsString().Should().Be("TypeError");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new DecompressionStream(format)"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void AcceptsEveryFormatTheEnumerationNames()
    {
        var engine = StreamEngine();

        // https://compression.spec.whatwg.org/#supported-formats declares four values, and the constructors'
        // step 1 — "if format is unsupported in CompressionStream, then throw a TypeError" — now has nothing
        // left to refuse. This is the test that fails when a format arm is removed from the validation.
        engine.Evaluate("""
            ['deflate', 'deflate-raw', 'gzip', 'brotli']
              .map(f => f + ':' + (new CompressionStream(f) && new DecompressionStream(f) ? 'ok' : 'no'))
              .join(' ')
            """).AsString().Should().Be("deflate:ok deflate-raw:ok gzip:ok brotli:ok");
    }

    [Fact]
    public void RequiresTheFormatArgumentAndNew()
    {
        var engine = StreamEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new CompressionStream()"))
            .Error.Get("message").AsString().Should().Contain("1 argument required");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("CompressionStream('gzip')"))
            .Error.Get("name").AsString().Should().Be("TypeError");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("DecompressionStream('gzip')"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        engine.Evaluate("CompressionStream.length").AsNumber().Should().Be(1);
        engine.Evaluate("DecompressionStream.length").AsNumber().Should().Be(1);
    }

    [Fact]
    public void ExposesNothingButTheGenericTransformStreamMixin()
    {
        var engine = StreamEngine();
        engine.Execute("var cs = new CompressionStream('gzip'); var ds = new DecompressionStream('gzip');");

        engine.Evaluate("cs.readable instanceof ReadableStream").AsBoolean().Should().BeTrue();
        engine.Evaluate("cs.writable instanceof WritableStream").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(cs)").AsString().Should().Be("[object CompressionStream]");
        engine.Evaluate("Object.prototype.toString.call(ds)").AsString().Should().Be("[object DecompressionStream]");

        // No format attribute: the standard keeps it internal, and a browser exposes none either.
        engine.Evaluate("Object.getOwnPropertyNames(CompressionStream.prototype).sort().join(',')")
            .AsString().Should().Be("constructor,readable,writable");

        foreach (var attribute in new[] { "readable", "writable" })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"CompressionStream.prototype.{attribute}"))
                .Error.Get("name").AsString().Should().Be("TypeError", attribute);

            // The two interfaces share the mixin but not their brands.
            Assert.Throws<JavaScriptException>(() => engine.Evaluate(
                    $"Object.getOwnPropertyDescriptor(CompressionStream.prototype, '{attribute}').get.call(ds)"))
                .Error.Get("name").AsString().Should().Be("TypeError", attribute);
        }
    }

    [Fact]
    public void NeedsBothItsFlagAndTheStreamsFlag()
    {
        foreach (var name in new[] { "CompressionStream", "DecompressionStream" })
        {
            new Engine(options => options.UseWebApis(WebApiFeatures.Compression))
                .Evaluate($"typeof {name}").AsString().Should().Be("undefined", name);
            new Engine(options => options.UseWebApis(WebApiFeatures.Streams))
                .Evaluate($"typeof {name}").AsString().Should().Be("undefined", name);
            new Engine(options => options.UseWebApis(WebApiFeatures.Streams | WebApiFeatures.Compression))
                .Evaluate($"typeof {name}").AsString().Should().Be("function", name);
        }
    }
}
#endif
