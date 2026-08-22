#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// Asynchronous iteration of a <c>ReadableStream</c> — https://streams.spec.whatwg.org/#rs-asynciterator,
/// on top of WebIDL's default asynchronous iterator object,
/// https://webidl.spec.whatwg.org/#js-default-asynchronous-iterator-object.
/// </summary>
public class ReadableStreamAsyncIterationTests
{
    private static Engine StreamEngine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));
        engine.Execute("var log = [];");
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    [Fact]
    public void IteratesEveryChunkAndReleasesTheLockAtTheEnd()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ start(c) { c.enqueue('a'); c.enqueue('b'); c.close(); } });
            (async () => {
              for await (const chunk of stream) { log.push(chunk); }
              log.push('locked:' + stream.locked);
            })();
            """);

        Log(engine).Should().Be("a,b,locked:false");
    }

    [Fact]
    public void LocksTheStreamForTheWholeIteration()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ start(c) { c.enqueue('a'); } });
            var iterator = stream[Symbol.asyncIterator]();
            """);

        engine.Evaluate("stream.locked").AsBoolean().Should().BeTrue();
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("stream.getReader()"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void SymbolAsyncIteratorIsTheValuesFunctionItself()
    {
        var engine = StreamEngine();

        engine.Evaluate("ReadableStream.prototype[Symbol.asyncIterator] === ReadableStream.prototype.values")
            .AsBoolean().Should().BeTrue();
        engine.Evaluate("ReadableStream.prototype.values.length").AsNumber().Should().Be(0);
        engine.Evaluate("ReadableStream.prototype.values.name").AsString().Should().Be("values");
    }

    [Fact]
    public void TheIteratorPrototypeInheritsFromTheAsyncIteratorPrototype()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var iterator = new ReadableStream().values();
            var proto = Object.getPrototypeOf(iterator);
            // %AsyncIteratorPrototype% is two levels above an async generator instance: instance →
            // its own generator prototype → %AsyncGeneratorPrototype% → %AsyncIteratorPrototype%.
            var gen = (async function* () {})();
            var asyncIteratorPrototype = Object.getPrototypeOf(Object.getPrototypeOf(Object.getPrototypeOf(gen)));
            """);

        engine.Evaluate("Object.getPrototypeOf(proto) === asyncIteratorPrototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("proto[Symbol.toStringTag]").AsString().Should().Be("ReadableStream AsyncIterator");
        engine.Evaluate("typeof proto.next").AsString().Should().Be("function");
        engine.Evaluate("typeof proto.return").AsString().Should().Be("function");

        // Inheriting from %AsyncIteratorPrototype% means the iterator is itself async-iterable.
        engine.Evaluate("iterator[Symbol.asyncIterator]() === iterator").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void TheIteratorPrototypesMethodsCarryWebIdlsAttributes()
    {
        // "An asynchronous iterator prototype object must have a next data property with attributes
        // { [[Writable]]: true, [[Enumerable]]: true, [[Configurable]]: true }", and the same for return —
        // https://webidl.spec.whatwg.org/#es-asynchronous-iterator-prototype-object. Enumerable is the
        // surprise: a built-in function property is non-enumerable everywhere in ECMA-262, and WebIDL is
        // the one binding that says otherwise.
        var engine = StreamEngine();
        engine.Execute("var proto = Object.getPrototypeOf(new ReadableStream().values());");

        engine.Evaluate("Object.getOwnPropertyNames(proto).sort().join(',')").AsString().Should().Be("next,return");

        foreach (var method in new[] { "next", "return" })
        {
            engine.Execute($"var d = Object.getOwnPropertyDescriptor(proto, '{method}');");
            engine.Evaluate("d.writable").AsBoolean().Should().BeTrue(method);
            engine.Evaluate("d.enumerable").AsBoolean().Should().BeTrue(method);
            engine.Evaluate("d.configurable").AsBoolean().Should().BeTrue(method);
        }

        // The lengths and the absence of throw are the rest of the same paragraph.
        engine.Evaluate("proto.next.length").AsNumber().Should().Be(0);
        engine.Evaluate("proto.return.length").AsNumber().Should().Be(1);
        engine.Evaluate("typeof proto.throw").AsString().Should().Be("undefined");

        // The class string is a symbol, so it stays out of the name list above — and it keeps the attributes
        // a class string carries, not WebIDL's operation attributes.
        engine.Evaluate("Object.getOwnPropertySymbols(proto).length").AsNumber().Should().Be(1);
        engine.Execute("var t = Object.getOwnPropertyDescriptor(proto, Symbol.toStringTag);");
        engine.Evaluate("t.value").AsString().Should().Be("ReadableStream AsyncIterator");
        engine.Evaluate("t.writable").AsBoolean().Should().BeFalse();
        engine.Evaluate("t.enumerable").AsBoolean().Should().BeFalse();
        engine.Evaluate("t.configurable").AsBoolean().Should().BeTrue();

        // The @@asyncIterator that reaches the iterator is inherited from %AsyncIteratorPrototype%; the one
        // on ReadableStream.prototype is an interface member, and those stay non-enumerable.
        engine.Execute("var a = Object.getOwnPropertyDescriptor(ReadableStream.prototype, Symbol.asyncIterator);");
        engine.Evaluate("a.writable").AsBoolean().Should().BeTrue();
        engine.Evaluate("a.enumerable").AsBoolean().Should().BeFalse();
        engine.Evaluate("a.configurable").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void BreakingOutOfTheLoopCancelsTheStream()
    {
        // "By default, calling the async iterator's return() method will also cancel the stream."
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({
              start(c) { c.enqueue('a'); c.enqueue('b'); },
              cancel(reason) { log.push('cancel:' + reason); }
            });
            (async () => {
              for await (const chunk of stream) { log.push(chunk); break; }
              log.push('locked:' + stream.locked);
            })();
            """);

        Log(engine).Should().Be("a,cancel:undefined,locked:false");
    }

    [Fact]
    public void PreventCancelReleasesTheLockWithoutCancelling()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({
              start(c) { c.enqueue('a'); c.enqueue('b'); },
              cancel() { log.push('cancel'); }
            });
            (async () => {
              for await (const chunk of stream.values({ preventCancel: true })) { log.push(chunk); break; }
              log.push('locked:' + stream.locked);
              const { value } = await stream.getReader().read();
              log.push('left:' + value);
            })();
            """);

        Log(engine).Should().Be("a,locked:false,left:b");
    }

    [Fact]
    public void AnErrorInTheStreamThrowsOutOfTheLoopAndReleasesTheLock()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var stream = new ReadableStream({ start(c) { controller = c; c.enqueue('a'); } });
            (async () => {
              try {
                for await (const chunk of stream) { log.push(chunk); controller.error(new Error('bad')); }
              } catch (e) {
                log.push('caught:' + e.message);
              }
              log.push('locked:' + stream.locked);
            })();
            """);

        Log(engine).Should().Be("a,caught:bad,locked:false");
    }

    [Fact]
    public void ReportsAFailingCancelThroughTheReturnPromise()
    {
        // The iterator's return() is what `break` calls, and a cancel() that fails rejects it. Asserted on
        // return() directly rather than through `for await…of`: the engine's AsyncIteratorClose currently
        // swallows a rejected return() when the loop is left by `break`, which is a pre-existing gap that
        // shows for a plain async iterable too and has nothing to do with streams.
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({
              start(c) { c.enqueue('a'); },
              cancel() { throw new Error('cancel failed'); }
            });
            var iterator = stream.values();
            iterator.next().then(r => log.push(r.value));
            iterator.return('bye').then(
              () => log.push('fulfilled'),
              e => log.push('rejected:' + e.message));
            """);

        Log(engine).Should().Be("a,rejected:cancel failed");
    }

    [Fact]
    public void NextSerializesConcurrentCalls()
    {
        // WebIDL queues a second next() behind the first, so the read requests can never interleave.
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            var stream = new ReadableStream({ start(c) { controller = c; } });
            var iterator = stream.values();
            iterator.next().then(r => log.push('1:' + r.value));
            iterator.next().then(r => log.push('2:' + r.value));
            iterator.next().then(r => log.push('3:' + r.value + ':' + r.done));
            controller.enqueue('a');
            controller.enqueue('b');
            controller.close();
            """);

        Log(engine).Should().Be("1:a,2:b,3:undefined:true");
    }

    [Fact]
    public void NextAfterTheEndKeepsAnsweringDone()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var iterator = new ReadableStream({ start(c) { c.close(); } }).values();
            (async () => {
              log.push(JSON.stringify(await iterator.next()));
              log.push(JSON.stringify(await iterator.next()));
            })();
            """);

        Log(engine).Should().Be("{\"done\":true},{\"done\":true}");
    }

    [Fact]
    public void ReturnAnswersWithTheValueItWasGiven()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var iterator = new ReadableStream({ start(c) { c.enqueue('a'); }, cancel(r) { log.push('cancel:' + r); } }).values();
            iterator.return('bye').then(r => log.push('return:' + r.value + ':' + r.done));
            iterator.next().then(r => log.push('next:' + r.value + ':' + r.done));
            """);

        Log(engine).Should().Be("cancel:bye,return:bye:true,next:undefined:true");
    }

    [Fact]
    public void NextAndReturnBrandCheckTheirReceiverAsARejection()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var proto = Object.getPrototypeOf(new ReadableStream().values());
            proto.next.call({}).catch(e => log.push('next:' + e.name));
            proto.return.call({}).catch(e => log.push('return:' + e.name));
            """);

        Log(engine).Should().Be("next:TypeError,return:TypeError");
    }

    [Fact]
    public void ValuesConvertsItsOptionsBeforeLockingTheStream()
    {
        var engine = StreamEngine();
        engine.Execute("var stream = new ReadableStream(); var e = new Error('options');");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("stream.values({ get preventCancel() { throw e; } })"))
            .Error.Get("message").AsString().Should().Be("options");

        // The stream is untouched, so it can still be iterated afterwards.
        engine.Evaluate("stream.locked").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void IteratingALockedStreamThrows()
    {
        var engine = StreamEngine();
        engine.Execute("var stream = new ReadableStream(); stream.getReader();");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("stream.values()"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void WorksWithTheAsyncIteratorHelpers()
    {
        // Inheriting %AsyncIteratorPrototype% is what makes this work at all.
        var engine = StreamEngine();
        engine.Execute("""
            var stream = new ReadableStream({ start(c) { c.enqueue(1); c.enqueue(2); c.enqueue(3); c.close(); } });
            stream.values().map(x => x * 2).toArray().then(a => log.push(a.join('|')));
            """);

        Log(engine).Should().Be("2|4|6");
    }
}
#endif
