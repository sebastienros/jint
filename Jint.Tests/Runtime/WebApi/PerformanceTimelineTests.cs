#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>performance.mark</c>, <c>performance.measure</c> and the performance timeline they feed — User Timing
/// (https://w3c.github.io/user-timing/) on top of Performance Timeline
/// (https://w3c.github.io/performance-timeline/).
/// </summary>
/// <remarks>
/// Everything that involves a timestamp runs on a <see cref="ManualClock"/>, because the arithmetic is the
/// point: with the clock in the test's hand "a measure from mark a to now lasts 250ms" is an equality rather
/// than a tolerance.
/// </remarks>
public class PerformanceTimelineTests
{
    /// <summary>
    /// A <see cref="TimeProvider"/> whose clock only moves when a test moves it.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }

    private static (Engine Engine, ManualClock Clock) TimelineEngine()
    {
        var clock = new ManualClock();
        var provider = clock;
        var engine = new Engine(options => options.UseWebApis(webApi =>
        {
            webApi.Features = WebApiFeatures.Performance;
            webApi.Timers.TimeProvider = provider;
        }));

        return (engine, clock);
    }

    private static Engine Timeline() => TimelineEngine().Engine;

    [Fact]
    public void MarkRecordsAnEntryStampedWithTheCurrentTime()
    {
        var (engine, clock) = TimelineEngine();

        clock.Advance(120);
        engine.Execute("var entry = performance.mark('boot');");

        engine.Evaluate("entry.name").AsString().Should().Be("boot");
        engine.Evaluate("entry.entryType").AsString().Should().Be("mark");
        engine.Evaluate("entry.startTime").AsNumber().Should().Be(120);

        // https://w3c.github.io/user-timing/#dom-performancemark — "Set entry's duration attribute to 0."
        engine.Evaluate("entry.duration").AsNumber().Should().Be(0);
        engine.Evaluate("entry.detail").IsNull().Should().BeTrue();

        engine.Evaluate("performance.getEntries().length").AsNumber().Should().Be(1);
        engine.Evaluate("performance.getEntries()[0] === entry").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void MarkHonoursAnExplicitStartTimeAndRefusesANegativeOne()
    {
        var engine = Timeline();

        engine.Evaluate("performance.mark('m', { startTime: 42.5 }).startTime").AsNumber().Should().Be(42.5);

        // Step 5.2: "If markOptions's startTime is negative, throw a TypeError."
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("performance.mark('m', { startTime: -1 })"))
            .Message.Should().Contain("negative");

        // DOMHighResTimeStamp is a WebIDL `double`, not an `unrestricted double`, so a non-finite value is a
        // TypeError at the conversion — https://webidl.spec.whatwg.org/#es-double.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("performance.mark('m', { startTime: NaN })"))
            .Message.Should().Contain("finite");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("performance.mark('m', { startTime: Infinity })"))
            .Message.Should().Contain("finite");
    }

    [Fact]
    public void MarkStructuredClonesItsDetail()
    {
        var engine = Timeline();

        engine.Execute("""
            var source = { nested: { n: 1 } };
            var entry = performance.mark('m', { detail: source });
            source.nested.n = 2;
            """);

        // "Run the StructuredSerialize algorithm … then run the StructuredDeserialize algorithm", so the
        // entry holds a graph of its own that a later mutation cannot reach.
        engine.Evaluate("entry.detail.nested.n").AsNumber().Should().Be(1);
        engine.Evaluate("entry.detail === source").AsBoolean().Should().BeFalse();
        engine.Evaluate("entry.detail.nested === source.nested").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void ADetailThatCannotBeClonedRaisesDataCloneError()
    {
        var engine = Timeline();

        engine.Evaluate("""
            (() => {
                try { performance.mark('m', { detail: () => 1 }); return 'no throw'; }
                catch (e) { return e.name + '/' + (e instanceof DOMException); }
            })()
            """).AsString().Should().Be("DataCloneError/true");
    }

    [Fact]
    public void MeasureWithNoOptionsSpansFromTheTimeOriginToNow()
    {
        var (engine, clock) = TimelineEngine();

        clock.Advance(300);
        engine.Execute("var m = performance.measure('whole');");

        // Step 2 falls through to now() and step 3 falls through to 0.
        engine.Evaluate("m.entryType").AsString().Should().Be("measure");
        engine.Evaluate("m.startTime").AsNumber().Should().Be(0);
        engine.Evaluate("m.duration").AsNumber().Should().Be(300);
        engine.Evaluate("m.detail").IsNull().Should().BeTrue();
    }

    [Fact]
    public void MeasureFromAMarkNameToNowAndBetweenTwoMarks()
    {
        var (engine, clock) = TimelineEngine();

        clock.Advance(100);
        engine.Execute("performance.mark('a');");
        clock.Advance(150);
        engine.Execute("performance.mark('b');");
        clock.Advance(50);

        engine.Evaluate("performance.measure('to-now', 'a').startTime").AsNumber().Should().Be(100);
        engine.Evaluate("performance.measure('to-now', 'a').duration").AsNumber().Should().Be(200);

        var between = engine.Evaluate("performance.measure('a-to-b', 'a', 'b')").AsObject();
        between.Get("startTime").AsNumber().Should().Be(100);
        between.Get("duration").AsNumber().Should().Be(150);
    }

    [Fact]
    public void MeasureReadsTheMostRecentMarkOfAName()
    {
        var (engine, clock) = TimelineEngine();

        engine.Execute("performance.mark('a');");
        clock.Advance(400);
        engine.Execute("performance.mark('a');");

        // "the most recent occurrence of a PerformanceMark object in the performance entry buffer whose name
        // is mark".
        engine.Evaluate("performance.measure('m', 'a').startTime").AsNumber().Should().Be(400);
    }

    [Fact]
    public void MeasureCoversTheWholeOptionsMatrix()
    {
        var (engine, clock) = TimelineEngine();

        clock.Advance(500);
        engine.Execute("performance.mark('a');");

        // start + end
        var startEnd = engine.Evaluate("performance.measure('m', { start: 10, end: 60 })").AsObject();
        startEnd.Get("startTime").AsNumber().Should().Be(10);
        startEnd.Get("duration").AsNumber().Should().Be(50);

        // start + duration
        var startDuration = engine.Evaluate("performance.measure('m', { start: 10, duration: 25 })").AsObject();
        startDuration.Get("startTime").AsNumber().Should().Be(10);
        startDuration.Get("duration").AsNumber().Should().Be(25);

        // duration + end
        var durationEnd = engine.Evaluate("performance.measure('m', { duration: 25, end: 60 })").AsObject();
        durationEnd.Get("startTime").AsNumber().Should().Be(35);
        durationEnd.Get("duration").AsNumber().Should().Be(25);

        // start alone: the end falls through to now()
        var startOnly = engine.Evaluate("performance.measure('m', { start: 100 })").AsObject();
        startOnly.Get("startTime").AsNumber().Should().Be(100);
        startOnly.Get("duration").AsNumber().Should().Be(400);

        // end alone: the start falls through to 0
        var endOnly = engine.Evaluate("performance.measure('m', { end: 120 })").AsObject();
        endOnly.Get("startTime").AsNumber().Should().Be(0);
        endOnly.Get("duration").AsNumber().Should().Be(120);

        // a mark name is accepted wherever a timestamp is
        var byMark = engine.Evaluate("performance.measure('m', { start: 'a', end: 700 })").AsObject();
        byMark.Get("startTime").AsNumber().Should().Be(500);
        byMark.Get("duration").AsNumber().Should().Be(200);
    }

    [Fact]
    public void MeasureClonesTheDetailFromTheOptions()
    {
        var engine = Timeline();

        engine.Execute("""
            var source = { k: 1 };
            var m = performance.measure('m', { start: 0, detail: source });
            source.k = 2;
            """);

        engine.Evaluate("m.detail.k").AsNumber().Should().Be(1);

        // PerformanceMeasureOptions gives `detail` no IDL default, so an absent one is still null on the
        // entry — that part is the algorithm's step 9, not the dictionary's.
        engine.Evaluate("performance.measure('n', { start: 0 }).detail").IsNull().Should().BeTrue();
    }

    [Fact]
    public void MeasureRejectsTheThreeContradictoryArgumentShapes()
    {
        var engine = Timeline();
        engine.Execute("performance.mark('a');");

        // "If endMark is given, throw a TypeError."
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("performance.measure('m', { start: 0 }, 'a')"))
            .Message.Should().Contain("end mark");

        // "If both start and end are omitted, throw a TypeError."
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("performance.measure('m', { duration: 5 })"))
            .Message.Should().Contain("'start' and 'end'");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("performance.measure('m', { detail: 1 })"))
            .Message.Should().Contain("'start' and 'end'");

        // "If start, duration and end all exist, throw a TypeError."
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("performance.measure('m', { start: 0, duration: 1, end: 2 })"))
            .Message.Should().Contain("all three");
    }

    [Fact]
    public void AnEmptyOptionsObjectIsNotAContradiction()
    {
        var (engine, clock) = TimelineEngine();

        clock.Advance(90);

        // Step 1 is gated on "at least one of start, end, duration and detail exists", so an options object
        // with none of them behaves exactly like an omitted argument. So do null and undefined, which the
        // union conversion also routes to the dictionary.
        engine.Evaluate("performance.measure('m', {}).duration").AsNumber().Should().Be(90);
        engine.Evaluate("performance.measure('m', undefined).duration").AsNumber().Should().Be(90);
        engine.Evaluate("performance.measure('m', null).duration").AsNumber().Should().Be(90);
    }

    [Fact]
    public void ANumberIsAMarkNameInThePositionalArgumentButATimestampInTheDictionary()
    {
        var (engine, clock) = TimelineEngine();

        clock.Advance(700);
        engine.Execute("performance.mark('5');");

        // The positional argument is (DOMString or PerformanceMeasureOptions) — no numeric member — so 5 is
        // stringified and looked up as a mark.
        engine.Evaluate("performance.measure('m', 5).startTime").AsNumber().Should().Be(700);

        // The dictionary's start is (DOMString or DOMHighResTimeStamp), so there 5 really is a timestamp.
        engine.Evaluate("performance.measure('m', { start: 5 }).startTime").AsNumber().Should().Be(5);
    }

    [Fact]
    public void AnUnknownMarkIsASyntaxErrorDomException()
    {
        var engine = Timeline();

        // https://webidl.spec.whatwg.org/#syntaxerror — the WebIDL error name, so a DOMException and not the
        // ECMAScript SyntaxError constructor.
        engine.Evaluate("""
            (() => {
                try { performance.measure('m', 'nope'); return 'no throw'; }
                catch (e) { return [e.name, e instanceof DOMException, e instanceof SyntaxError].join('/'); }
            })()
            """).AsString().Should().Be("SyntaxError/true/false");

        engine.Evaluate("""
            (() => {
                try { performance.measure('m', { start: 0, end: 'nope' }); return 'no throw'; }
                catch (e) { return e.name; }
            })()
            """).AsString().Should().Be("SyntaxError");
    }

    [Fact]
    public void TheEndTimeIsResolvedBeforeTheStartTime()
    {
        var engine = Timeline();

        // Both marks are missing; the algorithm computes the end time first, so it is the end that is named.
        engine.Evaluate("""
            (() => {
                try { performance.measure('m', { start: 'missing-a', end: 'missing-b' }); return 'no throw'; }
                catch (e) { return e.message; }
            })()
            """).AsString().Should().Contain("missing-b");
    }

    [Fact]
    public void ANegativeTimestampIsATypeError()
    {
        var engine = Timeline();

        // "If mark is negative, throw a TypeError" in convert a mark to a timestamp.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("performance.measure('m', { start: -1 })"))
            .Message.Should().Contain("negative");
    }

    [Fact]
    public void GetEntriesFiltersByNameAndByType()
    {
        var (engine, clock) = TimelineEngine();

        engine.Execute("performance.mark('a');");
        clock.Advance(10);
        engine.Execute("performance.mark('b'); performance.measure('a', 'a');");

        engine.Evaluate("performance.getEntries().length").AsNumber().Should().Be(3);
        engine.Evaluate("performance.getEntriesByType('mark').map(e => e.name).join()").AsString().Should().Be("a,b");
        engine.Evaluate("performance.getEntriesByType('measure').length").AsNumber().Should().Be(1);
        engine.Evaluate("performance.getEntriesByType('resource').length").AsNumber().Should().Be(0);

        // Name alone matches across types; adding the type narrows it.
        engine.Evaluate("performance.getEntriesByName('a').map(e => e.entryType).join()").AsString().Should().Be("mark,measure");
        engine.Evaluate("performance.getEntriesByName('a', 'measure').length").AsNumber().Should().Be(1);
        engine.Evaluate("performance.getEntriesByName('nothing').length").AsNumber().Should().Be(0);

        // A real, ordinary array of this realm.
        engine.Evaluate("Array.isArray(performance.getEntries())").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void EntriesComeBackInChronologicalOrder()
    {
        var engine = Timeline();

        // Added late but stamped early: "Sort results's entries in chronological order with respect to
        // startTime" is what makes the answer come back in the other order.
        engine.Execute("performance.mark('late', { startTime: 300 }); performance.mark('early', { startTime: 100 });");

        engine.Evaluate("performance.getEntries().map(e => e.name).join()").AsString().Should().Be("early,late");
    }

    [Fact]
    public void EntriesWithTheSameStartTimeKeepTheirBufferOrder()
    {
        var engine = Timeline();

        engine.Execute("""
            for (const n of ['a', 'b', 'c', 'd']) {
                performance.mark(n, { startTime: 5 });
            }
            performance.mark('first', { startTime: 1 });
            """);

        engine.Evaluate("performance.getEntries().map(e => e.name).join()").AsString().Should().Be("first,a,b,c,d");
    }

    [Fact]
    public void ClearMarksAndClearMeasuresRemoveOnlyTheirOwnKind()
    {
        var engine = Timeline();

        engine.Execute("performance.mark('a'); performance.mark('b'); performance.measure('a', 'a');");

        engine.Evaluate("performance.clearMarks('a')").IsUndefined().Should().BeTrue();
        engine.Evaluate("performance.getEntries().map(e => e.entryType + ':' + e.name).join()")
            .AsString().Should().Be("mark:b,measure:a");

        engine.Execute("performance.clearMeasures();");
        engine.Evaluate("performance.getEntries().map(e => e.name).join()").AsString().Should().Be("b");

        engine.Execute("performance.clearMarks();");
        engine.Evaluate("performance.getEntries().length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void AClearedMarkIsNoLongerAMeasureAnchor()
    {
        var engine = Timeline();

        engine.Execute("performance.mark('a'); performance.clearMarks('a');");

        engine.Evaluate("""
            (() => { try { performance.measure('m', 'a'); return 'no throw'; } catch (e) { return e.name; } })()
            """).AsString().Should().Be("SyntaxError");
    }

    [Fact]
    public void TheBufferIsBoundedAndOverflowIsSilent()
    {
        var engine = Timeline();

        // 10 000 is the documented cap; the 10 001st entry is built and returned but not buffered, which is
        // exactly what a full buffer does in
        // https://w3c.github.io/performance-timeline/#dfn-determine-if-a-performance-entry-buffer-is-full.
        engine.Execute("for (let i = 0; i < 10001; i++) { performance.mark('m'); }");

        engine.Evaluate("performance.getEntries().length").AsNumber().Should().Be(10000);
        engine.Evaluate("performance.mark('overflow').name").AsString().Should().Be("overflow");
        engine.Evaluate("performance.getEntriesByName('overflow').length").AsNumber().Should().Be(0);

        // ... and clearing frees the room again.
        engine.Execute("performance.clearMarks();");
        engine.Evaluate("performance.mark('after').name").AsString().Should().Be("after");
        engine.Evaluate("performance.getEntriesByName('after').length").AsNumber().Should().Be(1);
    }

    [Fact]
    public void ConstructingAMarkDoesNotAddItToTheBuffer()
    {
        var (engine, clock) = TimelineEngine();

        clock.Advance(80);

        // https://w3c.github.io/user-timing/#dom-performance-mark is "run the constructor, THEN queue and add
        // the entry", so `new` alone gives an object the timeline never hears about.
        engine.Execute("var m = new PerformanceMark('solo', { detail: { a: 1 } });");

        engine.Evaluate("m.name").AsString().Should().Be("solo");
        engine.Evaluate("m.entryType").AsString().Should().Be("mark");
        engine.Evaluate("m.startTime").AsNumber().Should().Be(80);
        engine.Evaluate("m.detail.a").AsNumber().Should().Be(1);
        engine.Evaluate("performance.getEntries().length").AsNumber().Should().Be(0);

        // ... and it is invisible as a measure anchor, for the same reason.
        engine.Evaluate("""
            (() => { try { performance.measure('m', 'solo'); return 'no throw'; } catch (e) { return e.name; } })()
            """).AsString().Should().Be("SyntaxError");
    }

    [Fact]
    public void OnlyPerformanceMarkIsConstructible()
    {
        var engine = Timeline();

        // Neither of the other two interfaces declares a constructor operation, so their interface objects
        // exist and are functions but refuse to construct — https://webidl.spec.whatwg.org/#es-interface-call.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new PerformanceEntry()"))
            .Message.Should().Be("Illegal constructor");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new PerformanceMeasure()"))
            .Message.Should().Be("Illegal constructor");

        engine.Evaluate("new PerformanceMark('x') instanceof PerformanceMark").AsBoolean().Should().BeTrue();
        engine.Evaluate("class Sub extends PerformanceMark {}; new Sub('x') instanceof Sub").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void TheInterfaceHierarchyIsTheOneWebIdlDescribes()
    {
        var engine = Timeline();

        engine.Evaluate("Object.getPrototypeOf(PerformanceMark) === PerformanceEntry").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(PerformanceMeasure) === PerformanceEntry").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(PerformanceMark.prototype) === PerformanceEntry.prototype").AsBoolean().Should().BeTrue();

        engine.Execute("var mark = performance.mark('a'); var measure = performance.measure('a', 'a');");
        engine.Evaluate("mark instanceof PerformanceMark && mark instanceof PerformanceEntry").AsBoolean().Should().BeTrue();
        engine.Evaluate("measure instanceof PerformanceMeasure && measure instanceof PerformanceEntry").AsBoolean().Should().BeTrue();
        engine.Evaluate("mark instanceof PerformanceMeasure").AsBoolean().Should().BeFalse();

        engine.Evaluate("Object.prototype.toString.call(mark)").AsString().Should().Be("[object PerformanceMark]");
        engine.Evaluate("Object.prototype.toString.call(measure)").AsString().Should().Be("[object PerformanceMeasure]");
        engine.Evaluate("Object.prototype.toString.call(PerformanceEntry.prototype)").AsString().Should().Be("[object PerformanceEntry]");
    }

    [Fact]
    public void TheAttributesLiveOnThePrototypeAndBrandCheckTheirReceiver()
    {
        var engine = Timeline();
        engine.Execute("var mark = performance.mark('a'); var measure = performance.measure('a', 'a');");

        // WebIDL attributes: accessors on the interface prototype, so the instance owns nothing.
        engine.Evaluate("JSON.stringify(Object.getOwnPropertyNames(mark))").AsString().Should().Be("[]");

        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(PerformanceEntry.prototype, 'startTime')").AsObject();
        descriptor.Get("get").IsCallable.Should().BeTrue();
        descriptor.Get("set").IsUndefined().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();

        foreach (var member in new[] { "name", "entryType", "startTime", "duration" })
        {
            Assert.Throws<JavaScriptException>(
                () => engine.Evaluate($"Object.getOwnPropertyDescriptor(PerformanceEntry.prototype, '{member}').get.call({{}})"))
                .Message.Should().Contain("PerformanceEntry");
        }

        // detail is declared on each derived interface, so each brand-checks for its own.
        Assert.Throws<JavaScriptException>(
            () => engine.Evaluate("Object.getOwnPropertyDescriptor(PerformanceMark.prototype, 'detail').get.call(measure)"))
            .Message.Should().Contain("PerformanceMark");
        Assert.Throws<JavaScriptException>(
            () => engine.Evaluate("Object.getOwnPropertyDescriptor(PerformanceMeasure.prototype, 'detail').get.call(mark)"))
            .Message.Should().Contain("PerformanceMeasure");
    }

    [Fact]
    public void ToJsonCarriesTheFourInheritedAttributesAndNotTheDetail()
    {
        var engine = Timeline();

        engine.Execute("var m = performance.mark('a', { startTime: 7, detail: { hidden: true } });");

        // The [Default] toJSON is declared on PerformanceEntry, and the default steps collect only the
        // declaring interface's own attributes — and only those of a JSON type, which `any` is not.
        engine.Evaluate("JSON.stringify(m.toJSON())").AsString()
            .Should().Be("""{"name":"a","entryType":"mark","startTime":7,"duration":0}""");

        engine.Evaluate("JSON.stringify(m)").AsString()
            .Should().Be("""{"name":"a","entryType":"mark","startTime":7,"duration":0}""");
    }

    [Fact]
    public void HasTheIdlArity()
    {
        var engine = Timeline();

        engine.Evaluate("performance.mark.length").AsNumber().Should().Be(1);
        engine.Evaluate("performance.measure.length").AsNumber().Should().Be(1);
        engine.Evaluate("performance.getEntries.length").AsNumber().Should().Be(0);
        engine.Evaluate("performance.getEntriesByType.length").AsNumber().Should().Be(1);
        engine.Evaluate("performance.getEntriesByName.length").AsNumber().Should().Be(1);
        engine.Evaluate("performance.clearMarks.length").AsNumber().Should().Be(0);
        engine.Evaluate("performance.clearMeasures.length").AsNumber().Should().Be(0);
        engine.Evaluate("PerformanceMark.length").AsNumber().Should().Be(1);
    }

    [Fact]
    public void EveryMemberBrandChecksItsReceiver()
    {
        var engine = Timeline();

        foreach (var member in new[] { "mark", "measure", "getEntries", "getEntriesByType", "getEntriesByName", "clearMarks", "clearMeasures" })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"performance.{member}.call({{}}, 'x')"))
                .Message.Should().Contain("Performance");
        }
    }

    [Fact]
    public void TheEntryInterfacesAreInstalledOnlyBehindThePerformanceFlag()
    {
        var engine = Timeline();

        foreach (var name in new[] { "PerformanceEntry", "PerformanceMark", "PerformanceMeasure" })
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("function");

            // Interface objects: writable and configurable, but not enumerable.
            var descriptor = engine.Realm.GlobalObject.GetOwnProperty(name);
            descriptor.Enumerable.Should().BeFalse();
            descriptor.Writable.Should().BeTrue();
            descriptor.Configurable.Should().BeTrue();

            new Engine(options => options.UseWebApis(WebApiFeatures.Console))
                .Evaluate($"typeof {name}").AsString().Should().Be("undefined");

            engine.Evaluate($"new ShadowRealm().evaluate('typeof {name}')").AsString().Should().Be("undefined");
        }
    }

    [Fact]
    public void TheBufferBelongsToTheEngineAndSurvivesAGlobalSnapshotRestore()
    {
        var engine = Timeline();

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine.Execute("performance.mark('before');");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // A restore reverts the global binding table and explicitly not the object graphs behind the restored
        // bindings, and the entry buffer is one of those. A pooled host that wants a clean timeline clears it.
        engine.Evaluate("performance.getEntries().length").AsNumber().Should().Be(1);
        engine.Execute("performance.clearMarks();");
        engine.Evaluate("performance.getEntries().length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void TwoEnginesFromOneOptionsInstanceHaveSeparateTimelines()
    {
        var options = new Options().UseWebApis(WebApiFeatures.Performance);

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("performance.mark('a'); performance.mark('b');");
        second.Execute("performance.mark('c');");

        first.Evaluate("performance.getEntries().map(e => e.name).join()").AsString().Should().Be("a,b");
        second.Evaluate("performance.getEntries().map(e => e.name).join()").AsString().Should().Be("c");
    }
}
#endif
