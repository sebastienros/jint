#if NET8_0_OR_GREATER
using Jint;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The four transform streams other standards define — <c>TextEncoderStream</c>,
/// <c>TextDecoderStream</c>, <c>CompressionStream</c> and <c>DecompressionStream</c> — seen from outside
/// the assembly.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party. What
/// these pins are about is the <b>two-flag</b> rule: each of the four is one standard's algorithm running
/// inside the Streams Standard's machinery, so an engine that named only one half of a pair gets neither
/// global — and a host reading the table in the README has to be able to rely on that.
/// </remarks>
public class WebApiTransformStreamTests
{
    private static readonly string[] _textGlobals = ["TextEncoderStream", "TextDecoderStream"];
    private static readonly string[] _compressionGlobals = ["CompressionStream", "DecompressionStream"];

    [Fact]
    public void ADefaultEngineHasNoTransformStreamGlobals()
    {
        var engine = new Engine();

        foreach (var name in _textGlobals.Concat(_compressionGlobals))
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("undefined", name);
            engine.Evaluate($"'{name}' in globalThis").AsBoolean().Should().BeFalse(name);
        }
    }

    [Fact]
    public void TheTextStreamsNeedBothEncodingAndStreams()
    {
        foreach (var name in _textGlobals)
        {
            new Engine(options => options.UseWebApis(WebApiFeatures.Encoding))
                .Evaluate($"typeof {name}").AsString().Should().Be("undefined", name);
            new Engine(options => options.UseWebApis(WebApiFeatures.Streams))
                .Evaluate($"typeof {name}").AsString().Should().Be("undefined", name);
            new Engine(options => options.UseWebApis(WebApiFeatures.Encoding | WebApiFeatures.Streams))
                .Evaluate($"typeof {name}").AsString().Should().Be("function", name);
        }

        // The non-streaming pair still comes with the Encoding flag alone, which is what makes the rule a
        // narrowing rather than a change to what that flag means.
        new Engine(options => options.UseWebApis(WebApiFeatures.Encoding))
            .Evaluate("typeof TextEncoder + ',' + typeof TextDecoder").AsString().Should().Be("function,function");
    }

    [Fact]
    public void TheCompressionStreamsNeedBothCompressionAndStreams()
    {
        foreach (var name in _compressionGlobals)
        {
            new Engine(options => options.UseWebApis(WebApiFeatures.Compression))
                .Evaluate($"typeof {name}").AsString().Should().Be("undefined", name);
            new Engine(options => options.UseWebApis(WebApiFeatures.Streams))
                .Evaluate($"typeof {name}").AsString().Should().Be("undefined", name);
            new Engine(options => options.UseWebApis(WebApiFeatures.Compression | WebApiFeatures.Streams))
                .Evaluate($"typeof {name}").AsString().Should().Be("function", name);
        }
    }

    [Fact]
    public void TheDefaultSetIncludesCompression()
    {
        var engine = new Engine(options => options.UseWebApis());

        foreach (var name in _textGlobals.Concat(_compressionGlobals))
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("function", name);
        }

        WebApiFeatures.Default.Should().HaveFlag(WebApiFeatures.Compression);

        // The bit is fixed, so a value a host persisted keeps its meaning.
        ((int) WebApiFeatures.Compression).Should().Be(1 << 20);
    }

    [Fact]
    public void TheGlobalsCarryTheAttributesWebIdlAsksFor()
    {
        var engine = new Engine(options => options.UseWebApis());

        foreach (var name in _textGlobals.Concat(_compressionGlobals))
        {
            var descriptor = engine.Evaluate($"Object.getOwnPropertyDescriptor(globalThis, '{name}')").AsObject();

            descriptor.Get("writable").AsBoolean().Should().BeTrue(name);
            descriptor.Get("configurable").AsBoolean().Should().BeTrue(name);
            descriptor.Get("enumerable").AsBoolean().Should().BeFalse(name);
        }
    }

    [Fact]
    public void AHostRegisteredGlobalWins()
    {
        var marker = new JsString("host's own CompressionStream");

        var engine = new Engine(options => options
            .AddLazyGlobal("CompressionStream", _ => marker)
            .UseWebApis());

        engine.Evaluate("CompressionStream").Should().BeSameAs(marker);
        engine.Evaluate("typeof DecompressionStream").AsString().Should().Be("function");
    }

    [Fact]
    public void AShadowRealmDoesNotGetThem()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("new ShadowRealm().evaluate('typeof CompressionStream')").AsString().Should().Be("undefined");
        engine.Evaluate("new ShadowRealm().evaluate('typeof TextDecoderStream')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof CompressionStream").AsString().Should().Be("function");
    }

    [Fact]
    public void AHostCanRoundTripDataThroughTheOrdinaryPromiseSurface()
    {
        var engine = new Engine(options => options.UseWebApis());

        // Everything a compression stream hands out is an ordinary engine promise, so the blocking unwrap a
        // host already uses drives the whole pipeline.
        var result = engine.Evaluate("""
            (async () => {
              const original = 'jint '.repeat(500);
              const source = new ReadableStream({
                start(c) { c.enqueue(new TextEncoder().encode(original)); c.close(); }
              });
              let text = '';
              const piped = source
                .pipeThrough(new CompressionStream('gzip'))
                .pipeThrough(new DecompressionStream('gzip'))
                .pipeThrough(new TextDecoderStream());
              for await (const chunk of piped) { text += chunk; }
              return text === original;
            })()
            """).UnwrapIfPromise();

        result.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ACorruptStreamIsATypeErrorTheScriptCanCatch()
    {
        var engine = new Engine(options => options.UseWebApis());

        var outcome = engine.Evaluate("""
            (async () => {
              const ds = new DecompressionStream('gzip');
              const reading = (async () => {
                try {
                  for await (const chunk of ds.readable) { /* nothing arrives */ }
                  return 'no throw';
                } catch (e) { return e.name; }
              })();
              const writer = ds.writable.getWriter();
              writer.write(new Uint8Array([1, 2, 3, 4, 5, 6, 7, 8])).catch(() => {});
              writer.close().catch(() => {});
              return await reading;
            })()
            """).UnwrapIfPromise();

        outcome.AsString().Should().Be("TypeError");
    }

    [Fact]
    public void SurvivesAGlobalSnapshotRestore()
    {
        var engine = new Engine(options => options.UseWebApis());
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("var s = new CompressionStream('deflate');");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("typeof s").AsString().Should().Be("undefined");
        engine.Evaluate("new CompressionStream('deflate').readable.locked").AsBoolean().Should().BeFalse();
        engine.Evaluate("new TextEncoderStream().encoding").AsString().Should().Be("utf-8");
    }

    [Fact]
    public void OneOptionsInstanceServesSeveralEnginesIndependently()
    {
        var options = new Options().UseWebApis();

        var first = new Engine(options);
        var second = new Engine(options);

        // Each realm builds its own interface objects, and neither engine's compression context is visible
        // to the other.
        first.Execute("var s = new CompressionStream('gzip');");
        second.Execute("var s = new CompressionStream('gzip');");

        first.Evaluate("s instanceof CompressionStream").AsBoolean().Should().BeTrue();
        second.Evaluate("s instanceof CompressionStream").AsBoolean().Should().BeTrue();

        var firstBytes = first.Evaluate("""
            (async () => {
              const writer = s.writable.getWriter();
              writer.write(new TextEncoder().encode('first'));
              writer.close();
              const chunks = [];
              for await (const chunk of s.readable) { chunks.push(...chunk); }
              return chunks.length;
            })()
            """).UnwrapIfPromise();

        firstBytes.AsNumber().Should().BeGreaterThan(0);
    }
}
#endif
