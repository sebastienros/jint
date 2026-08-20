#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>CountQueuingStrategy</c> and <c>ByteLengthQueuingStrategy</c> —
/// https://streams.spec.whatwg.org/#qs.
/// </summary>
public class QueuingStrategyTests
{
    private static Engine StreamEngine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams));
        engine.Execute("var log = [];");
        return engine;
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join(',')").AsString();

    [Theory]
    [InlineData("CountQueuingStrategy")]
    [InlineData("ByteLengthQueuingStrategy")]
    public void KeepsTheHighWaterMarkItWasGiven(string strategy)
    {
        var engine = StreamEngine();
        engine.Evaluate($"new {strategy}({{ highWaterMark: 4 }}).highWaterMark").AsNumber().Should().Be(4);
    }

    [Theory]
    [InlineData("CountQueuingStrategy")]
    [InlineData("ByteLengthQueuingStrategy")]
    public void RequiresAnInitDictionaryWithAHighWaterMark(string strategy)
    {
        var engine = StreamEngine();

        // `constructor(QueuingStrategyInit init)` takes a required dictionary with a required member.
        foreach (var argument in new[] { "", "null", "undefined", "true", "5", "{}" })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"new {strategy}({argument})"))
                .Error.Get("name").AsString().Should().Be("TypeError", $"{strategy}({argument})");
        }

        engine.Evaluate($"{strategy}.length").AsNumber().Should().Be(1);
    }

    [Theory]
    [InlineData("CountQueuingStrategy")]
    [InlineData("ByteLengthQueuingStrategy")]
    public void ConvertsTheHighWaterMarkWithTheUnrestrictedDoubleRulesAndValidatesNothing(string strategy)
    {
        // "Note that the provided high water mark will not be validated ahead of time. Instead, if it is
        // negative, NaN, or not a number, the resulting strategy will cause the corresponding stream
        // constructor to throw."
        var engine = StreamEngine();

        engine.Evaluate($"new {strategy}({{ highWaterMark: -5 }}).highWaterMark").AsNumber().Should().Be(-5);
        engine.Evaluate($"new {strategy}({{ highWaterMark: true }}).highWaterMark").AsNumber().Should().Be(1);
        engine.Evaluate($"new {strategy}({{ highWaterMark: '0' }}).highWaterMark").AsNumber().Should().Be(0);
        engine.Evaluate($"Number.isNaN(new {strategy}({{ highWaterMark: 'foo' }}).highWaterMark)").AsBoolean().Should().BeTrue();
        engine.Evaluate($"new {strategy}({{ highWaterMark: -Infinity }}).highWaterMark").AsNumber().Should().Be(double.NegativeInfinity);

        Assert.Throws<JavaScriptException>(() => engine.Evaluate($"new ReadableStream({{}}, new {strategy}({{ highWaterMark: -1 }}))"))
            .Error.Get("name").AsString().Should().Be("RangeError");
    }

    [Theory]
    [InlineData("CountQueuingStrategy")]
    [InlineData("ByteLengthQueuingStrategy")]
    public void ReportsAThrowingHighWaterMarkGetter(string strategy)
    {
        var engine = StreamEngine();
        engine.Execute("var e = new Error('wow');");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate($"new {strategy}({{ get highWaterMark() {{ throw e; }} }})"))
            .Error.Get("message").AsString().Should().Be("wow");
    }

    [Theory]
    [InlineData("CountQueuingStrategy")]
    [InlineData("ByteLengthQueuingStrategy")]
    public void SizeIsOneSharedFunctionPerRealmRatherThanAMethod(string strategy)
    {
        var engine = StreamEngine();
        engine.Execute($"var a = new {strategy}({{ highWaterMark: 5 }}); var b = new {strategy}({{ highWaterMark: 10 }});");

        // "This design is somewhat historical. It is motivated by the desire to ensure that size is a
        // function, not a method, i.e. it does not check its this value."
        engine.Evaluate("a.size === b.size").AsBoolean().Should().BeTrue();
        engine.Evaluate("a.size.name").AsString().Should().Be("size");
        engine.Evaluate("'prototype' in a.size").AsBoolean().Should().BeFalse();

        // Not a constructor either.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new (a.size)()"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Theory]
    [InlineData("CountQueuingStrategy")]
    [InlineData("ByteLengthQueuingStrategy")]
    public void SubclassingWorks(string strategy)
    {
        var engine = StreamEngine();
        engine.Execute($$"""
            class Sub extends {{strategy}} {
              size() { return 2; }
              extra() { return true; }
            }
            var sc = new Sub({ highWaterMark: 77 });
            """);

        engine.Evaluate("sc.constructor.name").AsString().Should().Be("Sub");
        engine.Evaluate("sc.highWaterMark").AsNumber().Should().Be(77);
        engine.Evaluate("sc.size()").AsNumber().Should().Be(2);
        engine.Evaluate("sc.extra()").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CountSizeAlwaysAnswersOne()
    {
        var engine = StreamEngine();
        engine.Execute("var size = new CountQueuingStrategy({ highWaterMark: 5 }).size;");

        engine.Evaluate("size.length").AsNumber().Should().Be(0);
        engine.Evaluate("size()").AsNumber().Should().Be(1);
        engine.Evaluate("size(null)").AsNumber().Should().Be(1);
        engine.Evaluate("size('potato')").AsNumber().Should().Be(1);
        engine.Evaluate("size({ get byteLength() { throw new Error('never read'); } })").AsNumber().Should().Be(1);
    }

    [Fact]
    public void ByteLengthSizeReadsTheByteLengthProperty()
    {
        var engine = StreamEngine();
        engine.Execute("var size = new ByteLengthQueuingStrategy({ highWaterMark: 5 }).size; var e = new Error('wow');");

        engine.Evaluate("size.length").AsNumber().Should().Be(1);
        engine.Evaluate("size({ byteLength: 1024 })").AsNumber().Should().Be(1024);
        engine.Evaluate("size({ get byteLength() { return 7; } })").AsNumber().Should().Be(7);

        // GetV(chunk, "byteLength"): undefined for an object without the property, a TypeError for a value
        // that cannot be converted to an object, and the getter's own exception when it throws.
        engine.Evaluate("size({}) === undefined").AsBoolean().Should().BeTrue();
        engine.Evaluate("size('potato') === undefined").AsBoolean().Should().BeTrue();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("size()"))
            .Error.Get("name").AsString().Should().Be("TypeError");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("size(null)"))
            .Error.Get("name").AsString().Should().Be("TypeError");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("size({ get byteLength() { throw e; } })"))
            .Error.Get("message").AsString().Should().Be("wow");
    }

    [Fact]
    public void CountQueuingStrategyMeasuresAStreamInChunks()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            new ReadableStream({ start(c) { controller = c; } }, new CountQueuingStrategy({ highWaterMark: 3 }));
            log.push(controller.desiredSize);
            controller.enqueue('a longer chunk');
            log.push(controller.desiredSize);
            """);

        Log(engine).Should().Be("3,2");
    }

    [Fact]
    public void ByteLengthQueuingStrategyMeasuresAStreamInBytes()
    {
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            new ReadableStream({ start(c) { controller = c; } }, new ByteLengthQueuingStrategy({ highWaterMark: 1024 }));
            controller.enqueue(new Uint8Array(100));
            log.push(controller.desiredSize);
            controller.enqueue(new Uint8Array(24));
            log.push(controller.desiredSize);
            """);

        Log(engine).Should().Be("924,900");
    }

    [Fact]
    public void AnyObjectWithTheRightShapeIsAQueuingStrategy()
    {
        // "Any object with these properties can be used when a queuing strategy object is expected."
        var engine = StreamEngine();
        engine.Execute("""
            var controller;
            new ReadableStream(
              { start(c) { controller = c; } },
              { highWaterMark: 10, size: chunk => chunk.weight });
            controller.enqueue({ weight: 4 });
            log.push(controller.desiredSize);
            """);

        Log(engine).Should().Be("6");
    }

    [Fact]
    public void ANonCallableSizeIsATypeError()
    {
        var engine = StreamEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new ReadableStream({}, { size: 42 })"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void RefusesASizeThatIsNotAFiniteNonNegativeNumber()
    {
        var engine = StreamEngine();

        foreach (var size in new[] { "-1", "NaN", "Infinity" })
        {
            engine.Execute($$"""
                log.length = 0;
                var controller;
                var stream = new ReadableStream(
                  { start(c) { controller = c; } },
                  { size: () => {{size}} });
                try { controller.enqueue('a'); } catch (e) { log.push('threw:' + e.name); }
                stream.getReader().closed.catch(e => log.push('closed:' + e.name));
                """);

            Log(engine).Should().Be("threw:RangeError,closed:RangeError", size);
        }
    }

    [Fact]
    public void MembersCarryTheWebIdlShape()
    {
        var engine = StreamEngine();

        foreach (var strategy in new[] { "CountQueuingStrategy", "ByteLengthQueuingStrategy" })
        {
            engine.Evaluate($"Object.prototype.toString.call(new {strategy}({{ highWaterMark: 1 }}))")
                .AsString().Should().Be($"[object {strategy}]");
            engine.Evaluate($"Object.getOwnPropertyNames(new {strategy}({{ highWaterMark: 1 }})).length")
                .AsNumber().Should().Be(0, strategy);

            var descriptor = engine.Realm.GlobalObject.GetOwnProperty(strategy);
            descriptor.Enumerable.Should().BeFalse(strategy);
            descriptor.Writable.Should().BeTrue(strategy);
            descriptor.Configurable.Should().BeTrue(strategy);

            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"Object.getOwnPropertyDescriptor({strategy}.prototype, 'highWaterMark').get.call({{}})"))
                .Error.Get("name").AsString().Should().Be("TypeError", strategy);
            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"Object.getOwnPropertyDescriptor({strategy}.prototype, 'size').get.call({{}})"))
                .Error.Get("name").AsString().Should().Be("TypeError", strategy);
        }

        // Each interface brand-checks against its own type, not against "a queuing strategy".
        Assert.Throws<JavaScriptException>(() => engine.Evaluate(
            "Object.getOwnPropertyDescriptor(CountQueuingStrategy.prototype, 'highWaterMark').get.call(new ByteLengthQueuingStrategy({ highWaterMark: 1 }))"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }
}
#endif
