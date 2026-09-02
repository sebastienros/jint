#if NET8_0_OR_GREATER
#nullable enable

using Jint.WebApi;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <see cref="ConsoleSink.WantsStackTrace"/>: the call site a console message came from, which a debugger
/// protocol anchors the message to and no other sink pays for.
/// </summary>
/// <remarks>
/// The frames are only readable while the call is still on the stack, so the engine either captures them
/// before the sink is reached or not at all — which is why this is a question asked of the sink rather than
/// something a sink could compute for itself once it has the record.
/// </remarks>
public class ConsoleStackTraceTests
{
    private sealed class RecordingSink(bool wantsStackTrace) : ConsoleSink
    {
        internal List<(ConsoleMethod Method, IReadOnlyList<ConsoleStackFrame>? StackTrace)> Records { get; } = [];

        public override bool WantsStackTrace => wantsStackTrace;

        public override void Write(ConsoleLogLevel level, string message)
        {
        }

        public override void Write(in ConsoleRecord record)
        {
            Records.Add((record.Method, record.StackTrace));
        }
    }

    private static RecordingSink Run(string script, bool wantsStackTrace, string source = "app.js")
    {
        var sink = new RecordingSink(wantsStackTrace);
        var engine = new Engine(options => options
            .UseWebApis(WebApiFeatures.Console | WebApiFeatures.Timers | WebApiFeatures.Events)
            .UseConsole(sink));
        engine.Execute(script, source);
        engine.Tasks.ProcessTasks();
        return sink;
    }

    [Test]
    public void ASinkThatDoesNotAskGetsNoFramesAndPaysForNone()
    {
        var sink = Run("console.log('hello');", wantsStackTrace: false);

        sink.Records.Should().ContainSingle().Which.StackTrace.Should().BeNull();
    }

    [Test]
    public void ASinkThatAsksGetsTheCallSiteOfEveryMethod()
    {
        var sink = Run(
            """
            function inner() {
                console.log('hello');
            }

            function outer() {
                inner();
            }

            outer();
            """,
            wantsStackTrace: true);

        var frames = sink.Records.Should().ContainSingle().Which.StackTrace;
        frames.Should().NotBeNull();

        // Innermost first, and the console method's own frame is not one of them — the trace starts where
        // the script called it.
        frames!.Select(frame => frame.FunctionName).Should().Equal("inner", "outer", "");
        frames[0].Source.Should().Be("app.js");
        frames[0].Line.Should().Be(2);
        frames[0].Column.Should().BeGreaterThan(0);
    }

    [Test]
    public void ConsoleTraceStillCarriesItsOwnFramesWithoutBeingAsked()
    {
        var sink = Run("function f() { console.trace('why'); } f();", wantsStackTrace: false);

        sink.Records.Should().ContainSingle().Which.StackTrace.Should().NotBeNull();
        sink.Records[0].StackTrace![0].FunctionName.Should().Be("f");
    }

    [Test]
    public void AMessageLoggedFromATimerCallbackAnchorsInsideTheCallback()
    {
        var sink = Run(
            """
            setTimeout(function heartbeat() {
                console.log('tick');
            }, 0);
            """,
            wantsStackTrace: true);

        var frames = sink.Records.Should().ContainSingle().Which.StackTrace;
        frames.Should().NotBeNull();
        frames!.Select(frame => frame.FunctionName).Should().Equal("heartbeat", "");
        frames[0].Line.Should().Be(2);
    }

    [Test]
    public void AMessageLoggedFromAnEventListenerAnchorsInsideTheListener()
    {
        var sink = Run(
            """
            var target = new EventTarget();
            target.addEventListener('ping', function receive() {
                console.log('got it');
            });
            target.dispatchEvent(new Event('ping'));
            """,
            wantsStackTrace: true);

        var frames = sink.Records.Should().ContainSingle().Which.StackTrace;
        frames.Should().NotBeNull();
        frames![0].FunctionName.Should().Be("receive");
        frames[0].Line.Should().Be(3);
    }

    [Test]
    public void ConsoleTraceInsideAnEventListenerStartsAtTheListener()
    {
        var sink = Run(
            """
            var target = new EventTarget();
            target.addEventListener('ping', function receive() { console.trace('why'); });
            target.dispatchEvent(new Event('ping'));
            """,
            wantsStackTrace: false);

        sink.Records.Should().ContainSingle().Which.StackTrace![0].FunctionName.Should().Be("receive");
    }

    [Test]
    public void AMethodThatPrintsNothingStillCarriesItsCallSite()
    {
        // groupEnd prints nothing, so it exists only as a record — and a front end that draws a group from
        // the record still wants to know where it came from.
        var sink = Run("console.group('g'); console.groupEnd();", wantsStackTrace: true);

        sink.Records.Should().HaveCount(2);
        sink.Records[1].Method.Should().Be(ConsoleMethod.GroupEnd);
        sink.Records[1].StackTrace.Should().NotBeNull();
    }
}
#endif
