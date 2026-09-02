#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>PerformanceObserver</c>, the delivery task behind it, and the two ways an observer can be registered.
/// <para>
/// https://w3c.github.io/performance-timeline/#dom-performanceobserver
/// </para>
/// </summary>
/// <remarks>
/// Delivery is a task on the engine's event loop, so every test here pumps: a mark taken by a script does not
/// reach an observer until something drains the loop, which is exactly the contract a timer has and is what
/// makes the callback ordering testable rather than incidental.
/// </remarks>
public class PerformanceObserverTests
{
    private static Engine ObserverEngine()
        => new(options => options.UseWebApis(WebApiFeatures.Performance));

    /// <summary>
    /// Runs the delivery task, and the ones it queues, to a fixed point. A callback that marks queues another
    /// task, so a single pump is not enough for the buffered-replay tests.
    /// </summary>
    private static void Pump(Engine engine)
    {
        for (var i = 0; i < 8; i++)
        {
            engine.Tasks.ProcessTasks();
        }
    }

    /// <summary>
    /// The delivery is a task on the event loop, so <c>mark()</c> itself never calls the callback — which is
    /// what makes a mark taken in a tight loop cost one queued job rather than one callback per entry.
    /// </summary>
    /// <remarks>
    /// The synchronous half is asserted from inside the script rather than from the host, because every host
    /// entry point drains the event loop on its way out: by the time <c>Execute</c> has returned, the task it
    /// queued has already run.
    /// </remarks>
    [Test]
    public void MarkNeverCallsTheObserverSynchronously()
    {
        var engine = ObserverEngine();

        engine.Execute("""
            var seen = [];
            new PerformanceObserver(list => { for (const e of list.getEntries()) seen.push(e.name); })
                .observe({ entryTypes: ['mark'] });
            performance.mark('a');
            performance.mark('b');
            var deliveredDuringTheScript = seen.length;
            """);

        Pump(engine);

        engine.Evaluate("deliveredDuringTheScript").AsNumber().Should().Be(0);

        // And one callback carrying both, not one callback per entry.
        engine.Evaluate("seen.join(',')").AsString().Should().Be("a,b");
    }

    [Test]
    public void TheCallbackIsGivenTheEntryListTheObserverAndTheOptions()
    {
        var engine = ObserverEngine();

        engine.Execute("""
            var shape = null;
            var observer = new PerformanceObserver(function (entries, self, options) {
              shape = {
                isList: entries instanceof PerformanceObserverEntryList,
                isObserver: self instanceof PerformanceObserver,
                sameObserver: self === observer && this === observer,
                dropped: options.droppedEntriesCount
              };
            });
            observer.observe({ entryTypes: ['mark'] });
            performance.mark('a');
            """);

        Pump(engine);

        engine.Evaluate("shape.isList").AsBoolean().Should().BeTrue();
        engine.Evaluate("shape.isObserver").AsBoolean().Should().BeTrue();
        engine.Evaluate("shape.sameObserver").AsBoolean().Should().BeTrue();
        engine.Evaluate("shape.dropped").AsNumber().Should().Be(0, "nothing has been dropped from a fresh timeline");
    }

    /// <summary>
    /// HTML runs a task only once the microtask queue has drained, and Jint's single job queue is that
    /// microtask queue — so a promise reaction queued in the same turn as the mark runs first.
    /// </summary>
    [Test]
    public void DeliveryIsATaskAndRunsBehindTheMicrotasksOfItsTurn()
    {
        var engine = ObserverEngine();

        engine.Execute("""
            var order = [];
            new PerformanceObserver(() => order.push('observer')).observe({ type: 'mark' });
            performance.mark('a');
            Promise.resolve().then(() => order.push('microtask'));
            """);

        Pump(engine);
        engine.Evaluate("order.join(',')").AsString().Should().Be("microtask,observer");
    }

    [Test]
    public void TheBufferedFlagReplaysWhatTheTimelineAlreadyHolds()
    {
        var engine = ObserverEngine();

        engine.Execute("""
            performance.mark('before-1');
            performance.mark('before-2');
            performance.measure('a-measure');
            var seen = [];
            new PerformanceObserver(list => { for (const e of list.getEntries()) seen.push(e.name); })
                .observe({ type: 'mark', buffered: true });
            """);

        Pump(engine);

        // The measure is not replayed: `buffered` replays the tuple of the observed type and no other.
        engine.Evaluate("seen.join(',')").AsString().Should().Be("before-1,before-2");
    }

    [Test]
    public void WithoutTheBufferedFlagAPastEntryIsNotReplayed()
    {
        var engine = ObserverEngine();

        engine.Execute("""
            performance.mark('before');
            var calls = 0;
            new PerformanceObserver(() => calls++).observe({ type: 'mark' });
            """);

        Pump(engine);
        engine.Evaluate("calls").AsNumber().Should().Be(0);
    }

    [Test]
    public void TakeRecordsEmptiesTheObserverBufferWithoutDelivering()
    {
        var engine = ObserverEngine();

        engine.Execute("""
            var calls = 0;
            var observer = new PerformanceObserver(() => calls++);
            observer.observe({ entryTypes: ['mark'] });
            performance.mark('a');
            performance.mark('b');
            var taken = observer.takeRecords().map(e => e.name).join(',');
            var second = observer.takeRecords().length;
            """);

        Pump(engine);

        engine.Evaluate("taken").AsString().Should().Be("a,b");
        engine.Evaluate("second").AsNumber().Should().Be(0);
        engine.Evaluate("calls").AsNumber().Should().Be(0, "takeRecords took the entries the delivery would have carried");
    }

    [Test]
    public void DisconnectStopsDeliveryAndForgetsTheTypesObserved()
    {
        var engine = ObserverEngine();

        engine.Execute("""
            var seen = [];
            var observer = new PerformanceObserver(list => { for (const e of list.getEntries()) seen.push(e.name); });
            observer.observe({ type: 'mark' });
            observer.disconnect();
            observer.observe({ type: 'measure' });
            performance.mark('a');
            performance.measure('b');
            """);

        Pump(engine);
        engine.Evaluate("seen.join(',')").AsString().Should().Be("b");
    }

    /// <summary>
    /// The two <c>observe()</c> shapes stack differently: <c>entryTypes</c> replaces the whole options list,
    /// <c>type</c> appends to it.
    /// </summary>
    [Test]
    public void ObservingByTypeStacksAndObservingByEntryTypesReplaces()
    {
        var engine = ObserverEngine();

        engine.Execute("""
            var stacked = [];
            var byType = new PerformanceObserver(list => { for (const e of list.getEntries()) stacked.push(e.entryType); });
            byType.observe({ type: 'mark' });
            byType.observe({ type: 'measure' });

            var replaced = [];
            var byEntryTypes = new PerformanceObserver(list => { for (const e of list.getEntries()) replaced.push(e.entryType); });
            byEntryTypes.observe({ entryTypes: ['mark'] });
            byEntryTypes.observe({ entryTypes: ['measure'] });

            performance.mark('a');
            performance.measure('b');
            """);

        Pump(engine);

        engine.Evaluate("stacked.slice().sort().join(',')").AsString().Should().Be("mark,measure");
        engine.Evaluate("replaced.join(',')").AsString().Should().Be("measure");
    }

    [TestCase("observer.observe({ type: 'mark' })", "InvalidModificationError")]
    [TestCase("observer.observe({})", "TypeError")]
    [TestCase("observer.observe({ entryTypes: 'mark' })", "TypeError")]
    [TestCase("observer.observe({ entryTypes: ['mark'], buffered: true })", "TypeError")]
    [TestCase("observer.observe({ type: 'mark', entryTypes: ['measure'] })", "TypeError")]
    public void TheArgumentGrammarIsRefusedByName(string script, string expected)
    {
        var engine = ObserverEngine();
        engine.Execute("var observer = new PerformanceObserver(() => {}); observer.observe({ entryTypes: ['mark'] });");

        var exception = Assert.Throws<JavaScriptException>(() => engine.Execute(script))!;
        exception.Error.Get("name").AsString().Should().Be(expected);
    }

    /// <summary>
    /// "Remove all types from entry types that are not contained in the frozen array of supported entry
    /// types. If the resulting sequence is empty, abort these steps" — so an unknown type is a silent no-op
    /// rather than an error, which is what lets a script written for a browser degrade instead of throwing.
    /// </summary>
    [Test]
    public void AnUnsupportedEntryTypeIsIgnoredRatherThanRefused()
    {
        var engine = ObserverEngine();

        engine.Execute("""
            var calls = 0;
            var observer = new PerformanceObserver(() => calls++);
            observer.observe({ entryTypes: [] });
            observer.observe({ entryTypes: ['resource', 'navigation'] });
            observer.observe({ entryTypes: ['mark', 'resource'] });
            performance.mark('a');
            performance.measure('b');
            """);

        Pump(engine);
        engine.Evaluate("calls").AsNumber().Should().Be(1, "only the mark matched, and the unknown names were dropped");
    }

    [Test]
    public void SupportedEntryTypesIsAFrozenAlphabeticalArrayAnsweredByIdentity()
    {
        var engine = ObserverEngine();

        engine.Evaluate("PerformanceObserver.supportedEntryTypes.join(',')").AsString().Should().Be("mark,measure");
        engine.Evaluate("Object.isFrozen(PerformanceObserver.supportedEntryTypes)").AsBoolean().Should().BeTrue();
        engine.Evaluate("PerformanceObserver.supportedEntryTypes === PerformanceObserver.supportedEntryTypes")
            .AsBoolean().Should().BeTrue("the IDL declares it [SameObject]");
    }

    [Test]
    public void TheObserverIsNotInstalledWithoutThePerformanceFlag()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Console));

        engine.Evaluate("typeof PerformanceObserver").AsString().Should().Be("undefined");
        engine.Evaluate("typeof PerformanceObserverEntryList").AsString().Should().Be("undefined");
    }

    /// <summary>
    /// A registration is a callback closing over the evaluation cycle it was made in, so a restore ends it —
    /// unlike the performance entry buffer beside it, which is data behind a restored binding and survives.
    /// </summary>
    [Test]
    public void ARestoreEndsTheRegistrationsAndKeepsTheTimeline()
    {
        var engine = ObserverEngine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("""
            globalThis.seen = [];
            new PerformanceObserver(list => { for (const e of list.getEntries()) globalThis.seen.push(e.name); })
                .observe({ entryTypes: ['mark'] });
            performance.mark('before');
            """);

        Pump(engine);
        engine.Evaluate("seen.join(',')").AsString().Should().Be("before");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Execute("globalThis.seen = []; performance.mark('after');");
        Pump(engine);

        engine.Evaluate("seen.length").AsNumber().Should().Be(0, "the registration belonged to the cycle the restore ended");
        engine.Evaluate("performance.getEntriesByType('mark').map(e => e.name).join(',')").AsString()
            .Should().Be("before,after", "the timeline is data behind a restored binding and survives");
    }
}
#endif
