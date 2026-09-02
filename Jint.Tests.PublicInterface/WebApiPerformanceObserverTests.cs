#if NET8_0_OR_GREATER
using Jint;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>PerformanceObserver</c> seen from outside the assembly: which flag installs it, that its callback needs
/// the host's own pump, and what a full timeline reports to it.
/// </summary>
/// <remarks>
/// Delivery is a task on the engine's event loop, so a host that never pumps never sees a callback. That is
/// the same contract <c>setTimeout</c> has and is the one thing an embedder has to know before wiring an
/// observer up to its own metrics.
/// </remarks>
public class WebApiPerformanceObserverTests
{
    private static Engine ObserverEngine()
        => new(options => options.UseWebApis(WebApiFeatures.Performance));

    /// <summary>
    /// Runs the delivery task and any it queues in turn: a callback that marks queues another.
    /// </summary>
    private static void Pump(Engine engine)
    {
        for (var i = 0; i < 8; i++)
        {
            engine.Tasks.ProcessTasks();
        }
    }

    [Test]
    public void ADefaultEngineHasNoObserver()
    {
        var engine = new Engine();

        engine.Evaluate("typeof PerformanceObserver").AsString().Should().Be("undefined");
        engine.Evaluate("'PerformanceObserver' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void ThePerformanceFlagInstallsTheObserver()
    {
        var engine = ObserverEngine();

        engine.Evaluate("typeof PerformanceObserver").AsString().Should().Be("function");
        engine.Evaluate("typeof PerformanceObserverEntryList").AsString().Should().Be("function");
        engine.Evaluate("PerformanceObserver.supportedEntryTypes.join(',')").AsString().Should().Be("mark,measure");
    }

    /// <summary>
    /// The interface inherits from <c>EventTarget</c>, so asking for the performance timeline brings the
    /// events feature with it. That is the feature closure at work, and it is visible from the host side.
    /// </summary>
    [Test]
    public void AskingForThePerformanceTimelineBringsTheEvents()
    {
        var engine = ObserverEngine();

        engine.Evaluate("performance instanceof EventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("typeof EventTarget").AsString().Should().Be("function");
        engine.Evaluate("typeof Event").AsString().Should().Be("function");
    }

    /// <summary>
    /// The host's pump is what runs the callback, exactly as it is what runs a timer.
    /// </summary>
    [Test]
    public void TheHostsPumpIsWhatDeliversToTheCallback()
    {
        var engine = ObserverEngine();

        engine.Execute("""
            var seen = [];
            new PerformanceObserver(function (list) {
              for (const entry of list.getEntries()) { seen.push(entry.entryType + ':' + entry.name); }
            }).observe({ entryTypes: ['mark', 'measure'] });
            performance.mark('boot');
            performance.measure('startup');
            var deliveredDuringTheScript = seen.length;
            """);

        Pump(engine);

        engine.Evaluate("deliveredDuringTheScript").AsNumber().Should().Be(0);
        engine.Evaluate("seen.slice().sort().join('|')").AsString().Should().Be("mark:boot|measure:startup");
    }

    [Test]
    public void TheBufferedFlagReplaysTheTimelineIntoALateObserver()
    {
        var engine = ObserverEngine();

        engine.Execute("""
            performance.mark('one');
            performance.mark('two');
            var seen = [];
            new PerformanceObserver(function (list) { seen.push(list.getEntries().length); })
                .observe({ type: 'mark', buffered: true });
            """);

        Pump(engine);
        engine.Evaluate("seen.join(',')").AsString().Should().Be("2");
    }

    /// <summary>
    /// The performance entry buffer is bounded, unlike a browser's — an engine embedded in a long-lived host
    /// has no page unload to free it — and what the bound drops is counted and reported once, as the callback
    /// options' <c>droppedEntriesCount</c>.
    /// </summary>
    /// <remarks>
    /// It takes 10,001 marks to see one, which is the cap `JsPerformance` documents. The count is reported on
    /// the observer's first callback only: after that the dictionary carries no such member at all, which is
    /// what makes <c>options.droppedEntriesCount === undefined</c> the honest answer rather than a stale zero.
    /// </remarks>
    [Test]
    public void AFullTimelineReportsItsDroppedEntriesOnce()
    {
        var engine = ObserverEngine();

        engine.Execute("""
            var reported = [];
            new PerformanceObserver(function (list, observer, options) { reported.push(options.droppedEntriesCount); })
                .observe({ type: 'mark' });
            for (var i = 0; i < 10005; i++) { performance.mark('m' + i); }
            """);

        Pump(engine);
        engine.Evaluate("reported[0]").AsNumber().Should().Be(5, "the buffer holds 10,000 and 10,005 were taken");

        engine.Execute("reported.length = 0; performance.mark('after');");
        Pump(engine);

        engine.Evaluate("reported.length").AsNumber().Should().Be(1);
        engine.Evaluate("reported[0] === undefined").AsBoolean()
            .Should().BeTrue("the count is surfaced on the first callback and never again");
    }

    [Test]
    public void DisconnectStopsTheCallbackReachingTheHostsScript()
    {
        var engine = ObserverEngine();

        engine.Execute("""
            var calls = 0;
            var observer = new PerformanceObserver(function () { calls++; });
            observer.observe({ entryTypes: ['mark'] });
            observer.disconnect();
            performance.mark('ignored');
            """);

        Pump(engine);
        engine.Evaluate("calls").AsNumber().Should().Be(0);
    }
}
#endif
