#if NET8_0_OR_GREATER
using Jint;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The WHATWG streams seen from outside the assembly: what a host has to write to get them, what it gets
/// when it writes nothing, and how the surface behaves at the host boundary.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party. The pin
/// that matters most is <see cref="ADefaultEngineHasNoStreamGlobals"/>: the surface is opt-in, and an engine
/// that did not ask for it must be the engine it was before any of this existed.
/// </remarks>
public class WebApiStreamsTests
{
    private static readonly string[] _globals =
    [
        "ReadableStream", "WritableStream", "TransformStream", "ByteLengthQueuingStrategy", "CountQueuingStrategy",
    ];

    [Fact]
    public void ADefaultEngineHasNoStreamGlobals()
    {
        var engine = new Engine();

        foreach (var name in _globals)
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("undefined", name);
            engine.Evaluate($"'{name}' in globalThis").AsBoolean().Should().BeFalse(name);
        }

        // Not even an engine that named the group but no feature.
        new Engine(options => options.WebApi.Features = WebApiFeatures.None)
            .Evaluate("typeof ReadableStream").AsString().Should().Be("undefined");

        // ... nor one that asked for a different feature.
        new Engine(options => options.UseWebApis(WebApiFeatures.Console))
            .Evaluate("typeof ReadableStream").AsString().Should().Be("undefined");
    }

    [Fact]
    public void TheStreamsFlagInstallsTheFiveInterfacesAScriptConstructs()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));

        foreach (var name in _globals)
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("function", name);
        }

        // DOMException comes with any feature, because it is how the web APIs report a failure.
        engine.Evaluate("typeof DOMException").AsString().Should().Be("function");
    }

    [Fact]
    public void TheHelperInterfacesAreReachableButAreNotGlobals()
    {
        // A documented narrowing of the browser surface: a browser exposes every stream interface as a
        // global, and Jint exposes only the five a script constructs by name. The rest are ordinary
        // interface objects reached through their instances.
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));

        foreach (var name in new[]
                 {
                     "ReadableStreamDefaultReader", "ReadableStreamDefaultController", "WritableStreamDefaultWriter",
                     "WritableStreamDefaultController", "TransformStreamDefaultController",
                 })
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("undefined", name);
        }

        engine.Evaluate("Object.getPrototypeOf(new ReadableStream().getReader()).constructor.name")
            .AsString().Should().Be("ReadableStreamDefaultReader");
        engine.Evaluate("Object.getPrototypeOf(new WritableStream().getWriter()).constructor.name")
            .AsString().Should().Be("WritableStreamDefaultWriter");
    }

    [Fact]
    public void ByteStreamsAreAbsentRatherThanBroken()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));

        engine.Evaluate("typeof ReadableByteStreamController").AsString().Should().Be("undefined");
        engine.Evaluate("typeof ReadableStreamBYOBReader").AsString().Should().Be("undefined");
        engine.Evaluate("typeof ReadableStreamBYOBRequest").AsString().Should().Be("undefined");

        // Asking for one is refused rather than quietly downgraded.
        engine.Evaluate("(() => { try { new ReadableStream({ type: 'bytes' }); return 'no throw'; } catch (e) { return e.name; } })()")
            .AsString().Should().Be("TypeError");
        engine.Evaluate("(() => { try { new ReadableStream().getReader({ mode: 'byob' }); return 'no throw'; } catch (e) { return e.name; } })()")
            .AsString().Should().Be("TypeError");
    }

    [Fact]
    public void TheDefaultSetIncludesStreams()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof ReadableStream").AsString().Should().Be("function");
        WebApiFeatures.Default.Should().HaveFlag(WebApiFeatures.Streams);

        // The bit is fixed, so a value a host persisted keeps its meaning.
        ((int) WebApiFeatures.Streams).Should().Be(1 << 12);
    }

    [Fact]
    public void TheGlobalsCarryTheAttributesWebIdlAsksFor()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));

        // An interface object is writable and configurable but NOT enumerable —
        // https://webidl.spec.whatwg.org/#es-interfaces.
        foreach (var name in _globals)
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
        var marker = new JsString("host's own ReadableStream");

        var engine = new Engine(options => options
            .AddLazyGlobal("ReadableStream", _ => marker)
            .UseWebApis(WebApiFeatures.Streams));

        // The host's configuration runs first and the install is non-clobbering.
        engine.Evaluate("ReadableStream").Should().BeSameAs(marker);

        // The names it did not claim are still installed.
        engine.Evaluate("typeof TransformStream").AsString().Should().Be("function");
    }

    [Fact]
    public void AShadowRealmDoesNotGetThem()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));

        // Only the principal realm's global object is touched — deliberately more conservative than a
        // browser, where these are [Exposed=*].
        engine.Evaluate("new ShadowRealm().evaluate('typeof ReadableStream')").AsString().Should().Be("undefined");
        engine.Evaluate("new ShadowRealm().evaluate('typeof CountQueuingStrategy')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof ReadableStream").AsString().Should().Be("function");
    }

    [Fact]
    public void SurvivesAGlobalSnapshotRestore()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("var s = new ReadableStream({ start(c) { c.enqueue('x'); c.close(); } });");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("typeof s").AsString().Should().Be("undefined");
        engine.Evaluate("new ReadableStream({ start(c) { c.enqueue('y'); } }).locked").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void OneOptionsInstanceServesSeveralEnginesIndependently()
    {
        var options = new Options().UseWebApis(WebApiFeatures.Streams);

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("var s = new ReadableStream({ start(c) { c.enqueue('first'); } });");
        second.Execute("var s = new ReadableStream({ start(c) { c.enqueue('second'); } });");

        // Nothing is shared between the two: each realm builds its own interface objects.
        first.Evaluate("s instanceof ReadableStream").AsBoolean().Should().BeTrue();
        second.Evaluate("s instanceof ReadableStream").AsBoolean().Should().BeTrue();

        first.Evaluate("s.getReader().read()").UnwrapIfPromise().AsObject().Get("value").AsString().Should().Be("first");
        second.Evaluate("s.getReader().read()").UnwrapIfPromise().AsObject().Get("value").AsString().Should().Be("second");
    }

    [Fact]
    public void AHostCanDriveAStreamToCompletionThroughTheOrdinaryPromiseSurface()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));

        // Every promise a stream hands out is an ordinary engine promise, so the blocking unwrap a host
        // already uses works on it.
        var result = engine.Evaluate("""
            (async () => {
              const chunks = [];
              const ts = new TransformStream({ transform(chunk, c) { c.enqueue(chunk.toUpperCase()); } });
              const done = new ReadableStream({
                start(c) { c.enqueue('a'); c.enqueue('b'); c.close(); }
              }).pipeTo(ts.writable);
              for await (const chunk of ts.readable) { chunks.push(chunk); }
              await done;
              return chunks.join('');
            })()
            """).UnwrapIfPromise();

        result.AsString().Should().Be("AB");
    }

    [Fact]
    public void StreamsAreUsableWithoutTheEventsFeature()
    {
        // The writable controller's `signal` is an AbortSignal, and pipeTo() accepts one — but neither
        // requires the events globals to have been installed. The objects exist regardless; only the global
        // names are gated.
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));

        engine.Evaluate("typeof AbortController").AsString().Should().Be("undefined");

        var name = engine.Evaluate("""
            (() => {
              let signal;
              const ws = new WritableStream({ start(c) { signal = c.signal; } });
              ws.abort('stop');
              return signal.constructor.name + ':' + signal.aborted + ':' + signal.reason;
            })()
            """);

        name.AsString().Should().Be("AbortSignal:true:stop");
    }

    [Fact]
    public void TheEventsFeatureIsWhatMakesAbortSignalReachableForPipeTo()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams | WebApiFeatures.Events));

        var outcome = engine.Evaluate("""
            (async () => {
              const controller = new AbortController();
              const written = [];
              const promise = new ReadableStream({ start(c) { c.enqueue('a'); } })
                .pipeTo(new WritableStream({ write(chunk) { written.push(chunk); } }), { signal: controller.signal });
              controller.abort();
              try { await promise; return 'no throw'; } catch (e) { return e.name + ':' + written.join(''); }
            })()
            """).UnwrapIfPromise();

        outcome.AsString().Should().Be("AbortError:a");
    }
}
#endif
