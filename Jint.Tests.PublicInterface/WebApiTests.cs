#if NET8_0_OR_GREATER
using Jint;
using Jint.Native;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The opt-in web platform APIs seen from outside the assembly: what a host has to write to get them, what it
/// gets when it writes nothing, and the promises it can build on.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party. The pin
/// that matters most is <see cref="ADefaultEngineHasNoWebGlobals"/>: the whole surface is opt-in, and an
/// engine that did not ask for it must be the engine it was before any of this existed.
/// </remarks>
public class WebApiTests
{
    private sealed class RecordingSink : ConsoleSink
    {
        internal List<(ConsoleLogLevel Level, string Message)> Records { get; } = new();

        public override void Write(ConsoleLogLevel level, string message)
        {
            Records.Add((level, message));
        }
    }

    [Fact]
    public void ADefaultEngineHasNoWebGlobals()
    {
        var engine = new Engine();

        engine.Evaluate("typeof console").AsString().Should().Be("undefined");
        engine.Evaluate("typeof DOMException").AsString().Should().Be("undefined");
        engine.Evaluate("'console' in globalThis").AsBoolean().Should().BeFalse();
        engine.Evaluate("'DOMException' in globalThis").AsBoolean().Should().BeFalse();

        // Not even an engine that named the group but no feature.
        var untouched = new Engine(options => options.WebApi.Features = WebApiFeatures.None);
        untouched.Evaluate("typeof console").AsString().Should().Be("undefined");
    }

    [Fact]
    public void UseWebApisInstallsTheDefaultSet()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof console").AsString().Should().Be("object");
        engine.Evaluate("typeof DOMException").AsString().Should().Be("function");

        // The default set never carries a network API, whatever it grows to include.
        engine.Evaluate("typeof fetch").AsString().Should().Be("undefined");
    }

    [Fact]
    public void UseConsoleWritesToATextWriter()
    {
        var writer = new StringWriter();
        var engine = new Engine(options => options.UseConsole(writer));

        engine.Execute("console.log('hello %s', 'world'); console.group('g'); console.warn('inner'); console.groupEnd();");

        writer.ToString().Should().Be("hello world" + Environment.NewLine + "g" + Environment.NewLine + "  inner" + Environment.NewLine);
    }

    [Fact]
    public void AHostSinkReceivesEveryRecordWithItsLevel()
    {
        var sink = new RecordingSink();
        var engine = new Engine(options => options.UseConsole(sink));

        engine.Execute("console.log('a'); console.error('b'); console.debug('c');");

        sink.Records.Should().HaveCount(3);
        sink.Records[0].Should().Be((ConsoleLogLevel.Log, "a"));
        sink.Records[1].Should().Be((ConsoleLogLevel.Error, "b"));
        sink.Records[2].Should().Be((ConsoleLogLevel.Debug, "c"));
    }

    [Fact]
    public void UseConsoleImpliesTheConsoleFeature()
    {
        var options = new Options().UseConsole(ConsoleSink.Null);

        options.WebApi.Features.Should().Be(WebApiFeatures.Console);
        new Engine(options).Evaluate("typeof console").AsString().Should().Be("object");
    }

    [Fact]
    public void FeatureCallsAccumulateRatherThanReplace()
    {
        var options = new Options()
            .UseConsole(ConsoleSink.Null)
            .UseWebApis(WebApiFeatures.None);

        options.WebApi.Features.Should().HaveFlag(WebApiFeatures.Console);
    }

    [Fact]
    public void TheDefaultSinkDiscardsEverything()
    {
        var options = new Options().UseWebApis();

        // Enabling the feature must never start writing to the process's standard output by itself.
        options.WebApi.Console.Sink.Should().BeSameAs(ConsoleSink.Null);

        var engine = new Engine(options);
        engine.Execute("console.log('nothing to see'); console.error('nor this');");
    }

    [Fact]
    public void AHostRegisteredGlobalWins()
    {
        var marker = new JsString("host's own console");

        var engine = new Engine(options => options
            .AddLazyGlobal("console", _ => marker)
            .UseConsole(ConsoleSink.Null));

        // The host's configuration runs first and the install is non-clobbering, so what the host registered
        // is what the script sees.
        engine.Evaluate("console").Should().BeSameAs(marker);
    }

    [Fact]
    public void OneOptionsInstanceServesSeveralEnginesIndependently()
    {
        var sink = new RecordingSink();
        var options = new Options().UseConsole(sink);

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("console.count('x'); console.group('g'); console.log('deep');");
        second.Execute("console.count('x'); console.log('flat');");

        // Counters and group depth belong to the engine, never to the shared options.
        sink.Records.ConvertAll(r => r.Message).Should().Equal("x: 1", "g", "  deep", "x: 1", "flat");
    }

    [Fact]
    public void ASwappedSinkTakesEffectOnTheNextRecord()
    {
        var first = new RecordingSink();
        var second = new RecordingSink();
        var options = new Options().UseConsole(first);
        var engine = new Engine(options);

        engine.Execute("console.log('one')");
        options.WebApi.Console.Sink = second;
        engine.Execute("console.log('two')");

        first.Records.Should().HaveCount(1);
        second.Records.Should().HaveCount(1);
        second.Records[0].Message.Should().Be("two");
    }

    [Fact]
    public void DomExceptionIsUsableFromScriptAndFromTheHost()
    {
        var engine = new Engine(options => options.UseWebApis());

        var exception = engine.Evaluate("new DOMException('gone', 'AbortError')").AsObject();

        exception.Get("name").AsString().Should().Be("AbortError");
        exception.Get("message").AsString().Should().Be("gone");
        exception.Get("code").AsNumber().Should().Be(20);
        engine.Evaluate("new DOMException('gone', 'AbortError') instanceof Error").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ErrorIsErrorAnswersTrueForADomException()
    {
        var engine = new Engine(options => options.UseWebApis());

        // WebIDL gives a DOMException the [[ErrorData]] internal slot that Error.isError brands on, so a
        // library that routes errors through it treats a platform exception as the error it is.
        engine.Evaluate("Error.isError(new DOMException())").AsBoolean().Should().BeTrue();

        // ... and nothing else about the predicate moved: a prototype is not an instance and a look-alike is
        // not an error.
        engine.Evaluate("Error.isError(Error.prototype)").AsBoolean().Should().BeFalse();
        engine.Evaluate("Error.isError(DOMException.prototype)").AsBoolean().Should().BeFalse();
        engine.Evaluate("Error.isError({ name: 'AbortError' })").AsBoolean().Should().BeFalse();
        engine.Evaluate("Error.isError(new Error())").AsBoolean().Should().BeTrue();

        // A default engine has no DOMException at all, and the predicate is unchanged there.
        new Engine().Evaluate("Error.isError(new Error()) && !Error.isError(Error.prototype) && !Error.isError({})")
            .AsBoolean().Should().BeTrue();
    }
}
#endif
