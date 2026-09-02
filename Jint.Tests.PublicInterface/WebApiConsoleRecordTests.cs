#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The structured console record seen from outside the assembly: what a sink that overrides the record
/// overload is handed, and the promise that a sink which does not override it sees exactly the traffic it
/// always did.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so every type named here is reachable by a third party —
/// which is the point for a record whose whole purpose is to be read by one. The pair that matters most is
/// <see cref="GroupEndReachesTheRecordOverloadAndNeverTheStringOne"/> and
/// <see cref="AStringOnlySinkSeesTheSameTrafficItAlwaysDid"/>: the record path is called for every
/// invocation, and none of the new calls reach a sink that did not ask for them.
/// </remarks>
public class WebApiConsoleRecordTests
{
    /// <summary>A sink that takes the record and lets the base class decide what the string overload sees.</summary>
    private sealed class RecordingSink : ConsoleSink
    {
        internal List<(ConsoleMethod Method, ConsoleLogLevel Level, JsValue[] Arguments, string? Message, int GroupDepth, IReadOnlyList<ConsoleStackFrame>? StackTrace)> Records { get; } = new();

        internal List<string> Lines { get; } = new();

        public override void Write(in ConsoleRecord record)
        {
            // The arguments are only valid for the duration of the call, so they are copied here rather
            // than kept — which is also what the record's own documentation tells a host to do.
            var arguments = new JsValue[record.Arguments.Count];
            for (var i = 0; i < arguments.Length; i++)
            {
                arguments[i] = record.Arguments[i];
            }

            Records.Add((record.Method, record.Level, arguments, record.Message, record.GroupDepth, record.StackTrace));
            base.Write(in record);
        }

        public override void Write(ConsoleLogLevel level, string message) => Lines.Add(message);
    }

    /// <summary>A sink written before the record overload existed, which must not notice that it does.</summary>
    private sealed class StringOnlySink : ConsoleSink
    {
        internal List<(ConsoleLogLevel Level, string Message)> Records { get; } = new();

        public override void Write(ConsoleLogLevel level, string message) => Records.Add((level, message));
    }

    private static RecordingSink Run(string script)
    {
        var sink = new RecordingSink();
        var engine = new Engine(options => options.UseConsole(sink));
        engine.Execute(script);
        return sink;
    }

    [Test]
    public void EveryPrintedRecordCarriesItsMethodAndItsRawArguments()
    {
        var sink = Run("console.log('a %s', 'b', { x: 1 })");

        var record = sink.Records.Should().ContainSingle().Which;
        record.Method.Should().Be(ConsoleMethod.Log);
        record.Level.Should().Be(ConsoleLogLevel.Log);
        record.Message.Should().Be("a b { x: 1 }");
        record.GroupDepth.Should().Be(0);
        record.StackTrace.Should().BeNull();

        // The arguments are the values the script wrote, untouched by the formatter that consumed them.
        record.Arguments.Should().HaveCount(3);
        record.Arguments[0].AsString().Should().Be("a %s");
        record.Arguments[1].AsString().Should().Be("b");
        record.Arguments[2].AsObject().Get("x").AsNumber().Should().Be(1);
    }

    [Test]
    public void TheArgumentsAreTheVeryValuesTheScriptPassed()
    {
        var sink = new RecordingSink();
        var engine = new Engine(options => options.UseConsole(sink));
        engine.Execute("var subject = { x: 1 }; console.log(subject);");

        var subject = engine.Evaluate("subject");

        // Identity, not equality: a record hands the host the object the script logged, so a consumer can
        // key on it -- which is what a remote-object table has to do.
        sink.Records.Should().ContainSingle().Which.Arguments[0].Should().BeSameAs(subject);
    }

    [Test]
    public void TheRecordsMessageIsTheStringOverloadsMessage()
    {
        var sink = Run("console.group('outer'); console.log('inner'); console.groupEnd(); console.log('after')");

        // Every record that printed printed exactly the line the string overload was handed, in order.
        var printed = sink.Records.ConvertAll(r => r.Message);
        printed.Should().Equal("outer", "  inner", null, "after");
        sink.Lines.Should().Equal("outer", "  inner", "after");
    }

    [Test]
    public void GroupDepthIsTheIndentationTheRecordWasPrintedAt()
    {
        var sink = Run("console.group('a'); console.group('b'); console.log('c'); console.groupEnd(); console.groupEnd()");

        var depths = sink.Records.ConvertAll(r => (r.Method, r.GroupDepth));
        depths.Should().Equal(
            (ConsoleMethod.Group, 0),
            (ConsoleMethod.Group, 1),
            (ConsoleMethod.Log, 2),
            (ConsoleMethod.GroupEnd, 1),
            (ConsoleMethod.GroupEnd, 0));
    }

    [Test]
    public void GroupEndReachesTheRecordOverloadAndNeverTheStringOne()
    {
        var sink = Run("console.groupEnd()");

        var record = sink.Records.Should().ContainSingle().Which;
        record.Method.Should().Be(ConsoleMethod.GroupEnd);
        record.Message.Should().BeNull();
        record.Arguments.Should().BeEmpty();

        // A null message is what stops the default forwarding, so a string sink hears nothing at all.
        sink.Lines.Should().BeEmpty();
    }

    [Test]
    public void TheMethodsThatActWithoutPrintingStillProduceARecord()
    {
        var sink = Run("console.time('t'); console.count('c'); console.countReset('c'); console.timeEnd('t')");

        var methods = sink.Records.ConvertAll(r => (r.Method, Printed: r.Message is not null));
        methods.Should().Equal(
            (ConsoleMethod.Time, false),
            (ConsoleMethod.Count, true),
            (ConsoleMethod.CountReset, false),
            (ConsoleMethod.TimeEnd, true));
    }

    [Test]
    public void AnOmittedArgumentIsAbsentRatherThanUndefined()
    {
        Run("console.count()").Records.Should().ContainSingle().Which.Arguments.Should().BeEmpty();

        var labelled = Run("console.count('hits')").Records.Should().ContainSingle().Which;
        labelled.Arguments.Should().ContainSingle().Which.AsString().Should().Be("hits");

        // `dir`'s second argument is part of the IDL signature and is reported only when it was written.
        Run("console.dir({})").Records.Should().ContainSingle().Which.Arguments.Should().HaveCount(1);
        Run("console.dir({}, {})").Records.Should().ContainSingle().Which.Arguments.Should().HaveCount(2);
    }

    [Test]
    public void AssertReportsTheArgumentsAfterTheCondition()
    {
        var failed = Run("console.assert(false, 'nope', 1)").Records.Should().ContainSingle().Which;
        failed.Method.Should().Be(ConsoleMethod.Assert);
        failed.Message.Should().Be("Assertion failed: nope 1");
        failed.Arguments.Should().HaveCount(2);
        failed.Arguments[0].AsString().Should().Be("nope");

        // Step 1 of the algorithm returns before anything happens, so there is nothing to report.
        Run("console.assert(true, 'nope')").Records.Should().BeEmpty();
    }

    [Test]
    public void TraceCarriesTheFramesItRendered()
    {
        var sink = new RecordingSink();
        var engine = new Engine(options => options.UseConsole(sink));
        engine.Execute("function inner() { console.trace('here'); } function outer() { inner(); } outer();");

        var record = sink.Records.Should().ContainSingle().Which;
        record.Method.Should().Be(ConsoleMethod.Trace);
        record.Message.Should().StartWith("Trace: here");

        record.StackTrace.Should().NotBeNull();
        var frames = record.StackTrace!;
        frames.Should().HaveCountGreaterThan(2);
        frames[0].FunctionName.Should().Be("inner");
        frames[1].FunctionName.Should().Be("outer");
        frames[0].Line.Should().BeGreaterThan(0);
        frames[0].Column.Should().BeGreaterThan(0);

        // The frames describe the same stack the message rendered, so every one of them is in the text.
        foreach (var frame in frames)
        {
            record.Message.Should().Contain(frame.Line + ":" + frame.Column);
        }
    }

    [Test]
    public void OnlyTraceCarriesFrames()
    {
        Run("console.log('x')").Records.Should().ContainSingle().Which.StackTrace.Should().BeNull();
    }

    [Test]
    public void AStringOnlySinkSeesTheSameTrafficItAlwaysDid()
    {
        var sink = new StringOnlySink();
        var engine = new Engine(options => options.UseConsole(sink));
        engine.Execute("""
            console.log();
            console.log('a');
            console.group('g');
            console.log('b');
            console.groupEnd();
            console.assert(true, 'never');
            console.time('t');
            console.count('c');
            console.countReset('c');
            console.timeStamp('m');
            """);

        // Not one of the record-only calls -- groupEnd, the successful time and countReset, the empty log,
        // the truthy assert -- reaches a sink that only ever asked for lines.
        sink.Records.Should().Equal(
            (ConsoleLogLevel.Log, "a"),
            (ConsoleLogLevel.Log, "g"),
            (ConsoleLogLevel.Log, "  b"),
            (ConsoleLogLevel.Log, "c: 1"),
            (ConsoleLogLevel.Log, "m"));
    }

    [Test]
    public void TheNullSinkStillTakesTheRecordPath()
    {
        var engine = new Engine(options => options.UseConsole(ConsoleSink.Null));

        // Nothing to assert but the absence of a failure: the default overload has to cope with a record
        // that printed nothing, which is the one shape the string overload never saw.
        engine.Invoking(e => e.Execute("console.groupEnd(); console.log('x')")).Should().NotThrow();
    }
}
#endif
