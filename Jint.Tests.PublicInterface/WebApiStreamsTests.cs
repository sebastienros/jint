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
        "ReadableStreamDefaultReader", "ReadableStreamBYOBReader", "WritableStreamDefaultWriter",
        "ReadableStreamDefaultController", "ReadableByteStreamController", "ReadableStreamBYOBRequest",
        "WritableStreamDefaultController", "TransformStreamDefaultController",
    ];

    [Test]
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

    [Test]
    public void TheStreamsFlagInstallsEveryInterfaceTheStandardDeclares()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));

        foreach (var name in _globals)
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("function", name);
        }

        // DOMException comes with any feature, because it is how the web APIs report a failure.
        engine.Evaluate("typeof DOMException").AsString().Should().Be("function");
    }

    [Test]
    public void EachHelperInterfaceGlobalIsWhatItsInstancesInheritFrom()
    {
        // The eight a script never constructs by name are globals too, as they are in a browser, and each is
        // the object its instances actually inherit from — so an `instanceof` written against the global name
        // and an `Object.getPrototypeOf` comparison answer the same thing.
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));

        engine.Evaluate("new ReadableStream().getReader() instanceof ReadableStreamDefaultReader")
            .AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(new ReadableStream().getReader()) === ReadableStreamDefaultReader.prototype")
            .AsBoolean().Should().BeTrue();
        engine.Evaluate("new WritableStream().getWriter() instanceof WritableStreamDefaultWriter")
            .AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(new WritableStream().getWriter()) === WritableStreamDefaultWriter.prototype")
            .AsBoolean().Should().BeTrue();

        // The three the standard gives a constructor operation are constructible; the five it does not refuse
        // `new` — https://webidl.spec.whatwg.org/#es-interface-call.
        foreach (var name in new[]
                 {
                     "ReadableStreamDefaultController", "ReadableByteStreamController", "ReadableStreamBYOBRequest",
                     "WritableStreamDefaultController", "TransformStreamDefaultController",
                 })
        {
            engine.Evaluate($"(function () {{ try {{ new {name}(); return 'no throw'; }} catch (e) {{ return e.constructor.name; }} }})()")
                .AsString().Should().Be("TypeError", name);
        }
    }

    [Test]
    public void ByteStreamsWorkFromOutsideTheAssembly()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));

        // A host reads a byte stream the way a browser script does: supply the buffer, get it back filled,
        // and keep using the view the read handed over — the one it passed in has been transferred away.
        engine.Execute("""
            globalThis.log = [];
            const stream = new ReadableStream({
                type: 'bytes',
                autoAllocateChunkSize: 8,
                pull(c) { c.byobRequest.view.set([1, 2, 3]); c.byobRequest.respond(3); c.close(); }
            });

            const reader = stream.getReader({ mode: 'byob' });
            reader.read(new Uint8Array(4)).then(r => log.push(Array.from(r.value).join('-') + ':' + r.value.buffer.byteLength));
            """);

        engine.Evaluate("log.join(',')").AsString().Should().Be("1-2-3:4");

        // Asking for a BYOB reader from a stream that is not a byte stream is still the TypeError it was.
        engine.Evaluate("(() => { try { new ReadableStream().getReader({ mode: 'byob' }); return 'no throw'; } catch (e) { return e.name; } })()")
            .AsString().Should().Be("TypeError");
    }

    [Test]
    public void TheDefaultSetIncludesStreams()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof ReadableStream").AsString().Should().Be("function");
        WebApiFeatures.Default.Should().HaveFlag(WebApiFeatures.Streams);

        // The bit is fixed, so a value a host persisted keeps its meaning.
        ((int) WebApiFeatures.Streams).Should().Be(1 << 12);
    }

    [Test]
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

    [Test]
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

    [Test]
    public void AShadowRealmDoesNotGetThem()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));

        // Only the principal realm's global object is touched — deliberately more conservative than a
        // browser, where these are [Exposed=*].
        engine.Evaluate("new ShadowRealm().evaluate('typeof ReadableStream')").AsString().Should().Be("undefined");
        engine.Evaluate("new ShadowRealm().evaluate('typeof CountQueuingStrategy')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof ReadableStream").AsString().Should().Be("function");
    }

    [Test]
    public void SurvivesAGlobalSnapshotRestore()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("var s = new ReadableStream({ start(c) { c.enqueue('x'); c.close(); } });");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("typeof s").AsString().Should().Be("undefined");
        engine.Evaluate("new ReadableStream({ start(c) { c.enqueue('y'); } }).locked").AsBoolean().Should().BeFalse();
    }

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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
