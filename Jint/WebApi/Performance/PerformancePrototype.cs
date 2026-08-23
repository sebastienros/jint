#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Performance;

/// <summary>
/// <c>Performance.prototype</c> — the interface prototype object, and where every member of the
/// <c>performance</c> object lives.
/// <para>
/// https://w3c.github.io/hr-time/#sec-performance
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>now()</c> and <c>timeOrigin</c> are answered from <c>Options.WebApi.Timers.TimeProvider</c>, the very
/// clock the timers are scheduled against, so a host that installs a fake one drives <c>setTimeout</c> and
/// <c>performance.now()</c> coherently instead of watching one of them stand still while the other runs.
/// <see cref="TimeOriginGet"/> and <see cref="Now"/> are the two halves of one reading: the origin is the
/// wall-clock moment the engine's web-API state was created, and <c>now()</c> is the monotonic duration since
/// that same moment, so <c>performance.timeOrigin + performance.now()</c> is the current time in Unix
/// milliseconds. Every timestamp a mark or a measure carries comes from the same reading.
/// </para>
/// <para>
/// The performance entry buffer and everything that reads or writes it belong to <see cref="JsPerformance"/>,
/// the instance: the members here brand-check their receiver and then operate on it, which is the split
/// WebIDL draws and what makes an extracted <c>getEntries</c> behave as a browser's does.
/// </para>
/// <para>
/// Not implemented, and absent rather than throwing so that feature detection sees the truth:
/// <c>PerformanceObserver</c> and everything that reports to one, <c>toJSON</c>,
/// <c>setResourceTimingBufferSize</c> and the resource-timing surface, and the <c>EventTarget</c> this
/// interface inherits from — see <see cref="PerformanceConstructor"/> for why the inheritance is not claimed.
/// </para>
/// <para>
/// <c>Object.keys(performance)</c> answers the empty array here exactly as it does in a browser, because there
/// too the members live one level up — on this object, where they are enumerable as WebIDL asks. One
/// documented simplification remains: the <c>performance</c> object itself is installed as an ordinary
/// enumerable data property of the global rather than through the <c>[Replaceable]</c> accessor pair WebIDL
/// gives it.
/// </para>
/// <para>
/// One deliberate divergence: the readings are <b>not coarsened</b>. https://w3c.github.io/hr-time/#dfn-coarsen-time
/// asks a browser to round to at best 100 microseconds because a page shares a process with cross-origin
/// data that a fine clock helps steal. An embedded engine has no cross-origin anything, and a host that wants
/// a coarse clock supplies a coarse <see cref="TimeProvider"/>; the resolution here is simply whatever that
/// provider gives, which for <see cref="TimeProvider.System"/> is the <c>Stopwatch</c> tick.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class PerformancePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly PerformanceConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString PerformanceToStringTag = new("Performance");

    internal PerformancePrototype(
        Engine engine,
        Realm realm,
        PerformanceConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://w3c.github.io/hr-time/#dom-performance-now — "the number of milliseconds in the current high
    /// resolution time", which is the duration from this engine's time origin to now, read from the monotonic
    /// clock.
    /// </summary>
    [JsFunction(Name = "now", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsNumber Now(JsValue thisObject)
    {
        return JsNumber.Create(Brand(thisObject, "Failed to execute 'now' on 'Performance'").State.CurrentHighResolutionTime);
    }

    /// <summary>
    /// https://w3c.github.io/hr-time/#dom-performance-timeorigin — the duration from the Unix epoch to this
    /// engine's time origin, in milliseconds.
    /// </summary>
    [JsAccessor("timeOrigin", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber TimeOriginGet(JsValue thisObject)
    {
        return JsNumber.Create(Brand(thisObject, "Failed to read the 'timeOrigin' property from 'Performance'").State.TimeOrigin);
    }

    /// <summary>
    /// https://w3c.github.io/user-timing/#dom-performance-mark — run the <c>PerformanceMark</c> constructor,
    /// queue the entry, add it to the performance entry buffer, and return it.
    /// </summary>
    /// <remarks>
    /// "Queue a PerformanceEntry" (https://w3c.github.io/performance-timeline/#queue-a-performanceentry) is
    /// almost entirely about <c>PerformanceObserver</c>s, of which there are none here; what survives of it is
    /// the buffer add, which is why the two steps are one line.
    /// </remarks>
    [JsFunction(Name = "mark", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsPerformanceMark Mark(JsValue thisObject, JsValue markName, JsValue markOptions)
    {
        var performance = Brand(thisObject, "Failed to execute 'mark' on 'Performance'");

        var (name, startTime, detail) = PerformanceMarkConstructor.ReadArguments(
            _engine,
            _realm,
            markName,
            markOptions,
            "Failed to execute 'mark' on 'Performance'");

        var entry = new JsPerformanceMark(_engine, name, startTime, detail)
        {
            _prototype = _realm.Intrinsics.PerformanceMark.PrototypeObject,
        };

        performance.AddToBuffer(entry);
        return entry;
    }

    /// <summary>
    /// https://w3c.github.io/user-timing/#dom-performance-measure
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole of the overload matrix lives in the argument's WebIDL type,
    /// <c>(DOMString or PerformanceMeasureOptions)</c>: by
    /// https://webidl.spec.whatwg.org/#es-union a value is the dictionary when it is an Object, or
    /// <c>null</c>, or absent (the IDL default is <c>{}</c>), and the mark <i>name</i> for everything else —
    /// so <c>measure('m', 5)</c> looks for a mark called <c>"5"</c> rather than measuring from timestamp 5,
    /// while <c>measure('m', { start: 5 })</c> does the latter.
    /// </para>
    /// <para>
    /// The end time is computed before the start time, which is the specification's order and is observable:
    /// with <c>{ start: 'missing-a', end: 'missing-b' }</c> it is <c>missing-b</c> that is reported.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "measure", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsPerformanceMeasure Measure(JsValue thisObject, JsValue measureName, JsValue startOrMeasureOptions, JsValue endMark)
    {
        const string Context = "Failed to execute 'measure' on 'Performance'";

        var performance = Brand(thisObject, Context);

        var name = TypeConverter.ToJsString(measureName);

        // The union conversion. Note that `null` lands in the dictionary arm along with `undefined`, because
        // the union has a dictionary member — only a primitive that is neither becomes a mark name.
        var isOptions = startOrMeasureOptions.IsUndefined() || startOrMeasureOptions.IsNull() || startOrMeasureOptions is ObjectInstance;
        var options = startOrMeasureOptions is ObjectInstance dictionary
            ? UserTiming.ReadMeasureOptions(_realm, dictionary, Context)
            : default;

        // An optional argument with no default value is "not given" when it is absent or explicitly
        // undefined — https://webidl.spec.whatwg.org/#es-overloads.
        var hasEndMark = !endMark.IsUndefined();

        // Step 1: the three ways the arguments can contradict each other.
        if (isOptions && options.HasAnyMember)
        {
            if (hasEndMark)
            {
                Throw.TypeError(_realm, Context + ": an end mark cannot be combined with a PerformanceMeasureOptions argument.");
            }

            if (!options.HasStart && !options.HasEnd)
            {
                Throw.TypeError(_realm, Context + ": at least one of 'start' and 'end' must be specified.");
            }

            if (options.HasStart && options.HasDuration && options.HasEnd)
            {
                Throw.TypeError(_realm, Context + ": 'start', 'duration' and 'end' cannot all three be specified.");
            }
        }

        // Step 2: the end time.
        double endTime;
        if (hasEndMark)
        {
            endTime = performance.TimestampOfMark(TypeConverter.ToString(endMark), Context);
        }
        else if (isOptions && options.HasEnd)
        {
            endTime = ConvertMarkToTimestamp(performance, options.End, Context);
        }
        else if (isOptions && options.HasStart && options.HasDuration)
        {
            endTime = ConvertMarkToTimestamp(performance, options.Start, Context) + options.Duration;
        }
        else
        {
            endTime = performance.State.CurrentHighResolutionTime;
        }

        // Step 3: the start time. The `start` mark is deliberately converted again rather than reused from
        // step 2 — the algorithm says so, and nothing can have changed the buffer in between.
        double startTime;
        if (isOptions && options.HasStart)
        {
            startTime = ConvertMarkToTimestamp(performance, options.Start, Context);
        }
        else if (isOptions && options.HasDuration && options.HasEnd)
        {
            startTime = ConvertMarkToTimestamp(performance, options.End, Context) - options.Duration;
        }
        else if (!isOptions)
        {
            startTime = performance.TimestampOfMark(TypeConverter.ToString(startOrMeasureOptions), Context);
        }
        else
        {
            startTime = 0;
        }

        // Steps 4 to 9. The detail is cloned only now, so a measure that cannot be located never serializes
        // anything.
        var detail = options.HasDetail ? UserTiming.CloneDetail(_engine, _realm, options.Detail) : JsValue.Null;

        var entry = new JsPerformanceMeasure(_engine, name, startTime, endTime - startTime, detail)
        {
            _prototype = _realm.Intrinsics.PerformanceMeasure.PrototypeObject,
        };

        performance.AddToBuffer(entry);
        return entry;
    }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performance-getentries — "filter buffer map by name and
    /// type" with both filters set to null.
    /// </summary>
    [JsFunction(Name = "getEntries", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsArray GetEntries(JsValue thisObject)
    {
        return Brand(thisObject, "Failed to execute 'getEntries' on 'Performance'").FilterBuffer(name: null, type: null);
    }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performance-getentriesbytype
    /// </summary>
    [JsFunction(Name = "getEntriesByType", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsArray GetEntriesByType(JsValue thisObject, JsValue type)
    {
        return Brand(thisObject, "Failed to execute 'getEntriesByType' on 'Performance'")
            .FilterBuffer(name: null, TypeConverter.ToString(type));
    }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performance-getentriesbyname — the entry type is an
    /// optional argument with no default, so an omitted or explicitly undefined one filters on the name alone.
    /// </summary>
    [JsFunction(Name = "getEntriesByName", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsArray GetEntriesByName(JsValue thisObject, JsValue name, JsValue type)
    {
        return Brand(thisObject, "Failed to execute 'getEntriesByName' on 'Performance'")
            .FilterBuffer(TypeConverter.ToString(name), type.IsUndefined() ? null : TypeConverter.ToString(type));
    }

    /// <summary>
    /// https://w3c.github.io/user-timing/#dom-performance-clearmarks
    /// </summary>
    [JsFunction(Name = "clearMarks", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue ClearMarks(JsValue thisObject, JsValue markName)
    {
        Brand(thisObject, "Failed to execute 'clearMarks' on 'Performance'")
            .RemoveEntries(JsPerformanceMark.MarkEntryType, markName);
        return Undefined;
    }

    /// <summary>
    /// https://w3c.github.io/user-timing/#dom-performance-clearmeasures
    /// </summary>
    [JsFunction(Name = "clearMeasures", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue ClearMeasures(JsValue thisObject, JsValue measureName)
    {
        Brand(thisObject, "Failed to execute 'clearMeasures' on 'Performance'")
            .RemoveEntries(JsPerformanceMeasure.MeasureEntryType, measureName);
        return Undefined;
    }

    /// <summary>
    /// https://w3c.github.io/user-timing/#convert-a-mark-to-a-timestamp
    /// </summary>
    /// <remarks>
    /// Step 1, the <c>PerformanceTiming</c> branch, is guarded by "if mark … has the same name as a read only
    /// attribute in the PerformanceTiming interface", an interface that belongs to a document's navigation.
    /// There is no document here, so that branch is absent and a name that happens to be
    /// <c>"domComplete"</c> is looked up as an ordinary mark.
    /// </remarks>
    private double ConvertMarkToTimestamp(JsPerformance performance, in UserTiming.MarkOrTimestamp mark, string context)
    {
        if (mark.Mark is not null)
        {
            return performance.TimestampOfMark(mark.Mark, context);
        }

        // Step 3.1: "If mark is negative, throw a TypeError."
        if (mark.Timestamp < 0)
        {
            Throw.TypeError(_realm, context + ": a timestamp cannot be negative.");
        }

        return mark.Timestamp;
    }

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a platform object implementing
    /// the interface raises a <c>TypeError</c>.
    /// </summary>
    private JsPerformance Brand(JsValue thisObject, string what)
    {
        if (thisObject is not JsPerformance performance)
        {
            Throw.TypeError(_realm, what + ": illegal invocation, receiver is not a Performance object.");
            return null!;
        }

        return performance;
    }
}
#endif
