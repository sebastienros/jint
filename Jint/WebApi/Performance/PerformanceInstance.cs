#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.DomException;

namespace Jint.WebApi.Performance;

/// <summary>
/// The <c>performance</c> object — an instance of the <c>Performance</c> interface.
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
/// <b>The time origin is per engine, not per evaluation cycle.</b> A pooled engine that a host recycles with
/// <c>Engine.Advanced.RestoreGlobalSnapshot</c> keeps the origin it was built with, so <c>now()</c> goes on
/// growing across cycles and a script cannot use it to tell how long <i>its own</i> cycle has been running.
/// That is deliberate: the origin is what makes the readings monotonic, and rewinding it at a restore would
/// hand the next cycle a clock that had gone backwards — which is the one thing
/// https://w3c.github.io/hr-time/#dom-performance-now forbids outright. The entry buffer survives a restore
/// for the plainer reason that a restore reverts the global <i>binding table</i> and explicitly not the object
/// graphs behind the restored bindings; a pooled host that wants a clean timeline calls <c>clearMarks()</c>
/// and <c>clearMeasures()</c>, which is what a browser gives it too.
/// </para>
/// <para>
/// Not implemented, and absent rather than throwing so that feature detection sees the truth:
/// <c>PerformanceObserver</c> and everything that reports to one, <c>toJSON</c>,
/// <c>setResourceTimingBufferSize</c> and the resource-timing surface, and the <c>EventTarget</c> this
/// interface inherits from.
/// </para>
/// <para>
/// Two documented simplifications against WebIDL, the same pair <c>console</c> and <c>crypto</c> carry. There
/// is no <c>Performance</c> interface object and no <c>Performance.prototype</c>, so the members are own
/// properties of this object with the attributes an ECMAScript built-in has, rather than those of a WebIDL
/// interface prototype's members; they all still brand-check their receiver, and
/// <c>Object.keys(performance)</c> answers the empty array here exactly as it does in a browser. And the
/// object is installed as an ordinary enumerable data property of the global rather than through the
/// <c>[Replaceable]</c> accessor pair WebIDL gives it. The <i>entries</i> are not simplified in that way:
/// <c>PerformanceEntry</c>, <c>PerformanceMark</c> and <c>PerformanceMeasure</c> are real interface objects
/// with real prototypes, because a script holds those objects and hands them around.
/// </para>
/// <para>
/// One deliberate divergence: the readings are <b>not coarsened</b>. https://w3c.github.io/hr-time/#dfn-coarsen-time
/// asks a browser to round to at best 100 microseconds because a page shares a process with cross-origin
/// data that a fine clock helps steal. An embedded engine has no cross-origin anything, and a host that wants
/// a coarse clock supplies a coarse <see cref="TimeProvider"/>; the resolution here is simply whatever that
/// provider gives, which for <see cref="TimeProvider.System"/> is the <c>Stopwatch</c> tick.
/// </para>
/// </remarks>
[JsObject]
internal sealed partial class PerformanceInstance : BuiltinShapeObject
{
    /// <summary>
    /// How many entries the timeline holds before it starts dropping them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// https://w3c.github.io/timing-entrytypes-registry/ gives both <c>mark</c> and <c>measure</c> a
    /// <c>maxBufferSize</c> of <i>Infinite</i>, which a browser can afford because a page's timeline dies with
    /// the page. An engine embedded in a long-lived host has no such event, and
    /// <c>while (true) performance.mark('x')</c> would otherwise be an unbounded memory leak that no execution
    /// constraint describes — a statement budget counts statements and a memory limit watches the JavaScript
    /// heap, neither of which is where this list lives. So the buffer is finite, and the overflow behaviour is
    /// the specification's own: https://w3c.github.io/performance-timeline/#dfn-determine-if-a-performance-entry-buffer-is-full
    /// says of a full buffer "Increase tuple's dropped entries count by 1. Return true", and the entry is
    /// simply not added. Nothing throws — <c>mark()</c> and <c>measure()</c> still return the entry they
    /// built, exactly as they do for a buffer with room, so the only observable effect is that
    /// <c>getEntries()</c> stops growing. The count is shared by marks and measures because the memory is;
    /// <c>clearMarks()</c> and <c>clearMeasures()</c> free it again.
    /// </para>
    /// <para>
    /// The dropped-entries count itself is not exposed, because the only thing that reads it in the
    /// specification is a <c>PerformanceObserver</c> callback's <c>droppedEntriesCount</c>, and there is no
    /// <c>PerformanceObserver</c> here.
    /// </para>
    /// </remarks>
    private const int MaxBufferedEntries = 10_000;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString PerformanceToStringTag = new("Performance");

    private readonly Realm _realm;
    private readonly WebApiEngineState _state;

    /// <summary>
    /// The performance entry buffer, https://w3c.github.io/performance-timeline/#performance-entry-buffer,
    /// in the order the entries were added. Null until the first entry, so a script that only reads
    /// <c>now()</c> allocates nothing.
    /// </summary>
    private List<JsPerformanceEntry>? _entries;

    private PerformanceInstance(Engine engine, Realm realm, ObjectPrototype objectPrototype, WebApiEngineState state)
        : base(engine)
    {
        _realm = realm;
        _state = state;
        _prototype = objectPrototype;
    }

    internal static PerformanceInstance Create(Engine engine, Realm realm, ObjectPrototype objectPrototype)
    {
        var state = UserTiming.RequireState(engine, "The performance object");
        return new PerformanceInstance(engine, realm, objectPrototype, state);
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
    [JsFunction(Name = "now", Length = 0)]
    private JsNumber Now(JsValue thisObject)
    {
        Brand(thisObject, "Failed to execute 'now' on 'Performance'");
        return JsNumber.Create(_state.CurrentHighResolutionTime);
    }

    /// <summary>
    /// https://w3c.github.io/hr-time/#dom-performance-timeorigin — the duration from the Unix epoch to this
    /// engine's time origin, in milliseconds.
    /// </summary>
    [JsAccessor("timeOrigin")]
    private JsNumber TimeOriginGet(JsValue thisObject)
    {
        Brand(thisObject, "Failed to read the 'timeOrigin' property from 'Performance'");
        return JsNumber.Create(_state.TimeOrigin);
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
    [JsFunction(Name = "mark", Length = 1)]
    private JsPerformanceMark Mark(JsValue thisObject, JsValue markName, JsValue markOptions)
    {
        Brand(thisObject, "Failed to execute 'mark' on 'Performance'");

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

        AddToBuffer(entry);
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
    [JsFunction(Name = "measure", Length = 1)]
    private JsPerformanceMeasure Measure(JsValue thisObject, JsValue measureName, JsValue startOrMeasureOptions, JsValue endMark)
    {
        const string Context = "Failed to execute 'measure' on 'Performance'";

        Brand(thisObject, Context);

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
            endTime = TimestampOfMark(TypeConverter.ToString(endMark), Context);
        }
        else if (isOptions && options.HasEnd)
        {
            endTime = ConvertMarkToTimestamp(options.End, Context);
        }
        else if (isOptions && options.HasStart && options.HasDuration)
        {
            endTime = ConvertMarkToTimestamp(options.Start, Context) + options.Duration;
        }
        else
        {
            endTime = _state.CurrentHighResolutionTime;
        }

        // Step 3: the start time. The `start` mark is deliberately converted again rather than reused from
        // step 2 — the algorithm says so, and nothing can have changed the buffer in between.
        double startTime;
        if (isOptions && options.HasStart)
        {
            startTime = ConvertMarkToTimestamp(options.Start, Context);
        }
        else if (isOptions && options.HasDuration && options.HasEnd)
        {
            startTime = ConvertMarkToTimestamp(options.End, Context) - options.Duration;
        }
        else if (!isOptions)
        {
            startTime = TimestampOfMark(TypeConverter.ToString(startOrMeasureOptions), Context);
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

        AddToBuffer(entry);
        return entry;
    }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performance-getentries — "filter buffer map by name and
    /// type" with both filters set to null.
    /// </summary>
    [JsFunction(Name = "getEntries", Length = 0)]
    private JsArray GetEntries(JsValue thisObject)
    {
        Brand(thisObject, "Failed to execute 'getEntries' on 'Performance'");
        return FilterBuffer(name: null, type: null);
    }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performance-getentriesbytype
    /// </summary>
    [JsFunction(Name = "getEntriesByType", Length = 1)]
    private JsArray GetEntriesByType(JsValue thisObject, JsValue type)
    {
        Brand(thisObject, "Failed to execute 'getEntriesByType' on 'Performance'");
        return FilterBuffer(name: null, TypeConverter.ToString(type));
    }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performance-getentriesbyname — the entry type is an
    /// optional argument with no default, so an omitted or explicitly undefined one filters on the name alone.
    /// </summary>
    [JsFunction(Name = "getEntriesByName", Length = 1)]
    private JsArray GetEntriesByName(JsValue thisObject, JsValue name, JsValue type)
    {
        Brand(thisObject, "Failed to execute 'getEntriesByName' on 'Performance'");
        return FilterBuffer(TypeConverter.ToString(name), type.IsUndefined() ? null : TypeConverter.ToString(type));
    }

    /// <summary>
    /// https://w3c.github.io/user-timing/#dom-performance-clearmarks
    /// </summary>
    [JsFunction(Name = "clearMarks", Length = 0)]
    private JsValue ClearMarks(JsValue thisObject, JsValue markName)
    {
        Brand(thisObject, "Failed to execute 'clearMarks' on 'Performance'");
        RemoveEntries(JsPerformanceMark.MarkEntryType, markName);
        return Undefined;
    }

    /// <summary>
    /// https://w3c.github.io/user-timing/#dom-performance-clearmeasures
    /// </summary>
    [JsFunction(Name = "clearMeasures", Length = 0)]
    private JsValue ClearMeasures(JsValue thisObject, JsValue measureName)
    {
        Brand(thisObject, "Failed to execute 'clearMeasures' on 'Performance'");
        RemoveEntries(JsPerformanceMeasure.MeasureEntryType, measureName);
        return Undefined;
    }

    /// <summary>
    /// The surviving half of "queue a PerformanceEntry": append unless the buffer is full, in which case the
    /// entry is silently dropped. See <see cref="MaxBufferedEntries"/>.
    /// </summary>
    private void AddToBuffer(JsPerformanceEntry entry)
    {
        var entries = _entries ??= new List<JsPerformanceEntry>();
        if (entries.Count >= MaxBufferedEntries)
        {
            return;
        }

        entries.Add(entry);
    }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dfn-filter-buffer-map-by-name-and-type, whose last step is
    /// "Sort results's entries in chronological order with respect to startTime".
    /// </summary>
    private JsArray FilterBuffer(string? name, string? type)
    {
        var entries = _entries;
        var matched = new List<JsPerformanceEntry>();

        if (entries is not null)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (name is not null && !string.Equals(entry.EntryName.ToString(), name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (type is not null && !string.Equals(entry.EntryType.ToString(), type, StringComparison.Ordinal))
                {
                    continue;
                }

                matched.Add(entry);
            }

            SortChronologically(matched);
        }

        var values = new List<JsValue>(matched.Count);
        for (var i = 0; i < matched.Count; i++)
        {
            values.Add(matched[i]);
        }

        return _realm.Intrinsics.Array.ConstructFast(values);
    }

    /// <summary>
    /// Orders entries by <c>startTime</c>, keeping buffer order among equal ones.
    /// </summary>
    /// <remarks>
    /// Insertion order is already chronological unless a mark was given an explicit <c>startTime</c> — the one
    /// way an entry can be added out of order — so the common case is the scan that finds nothing to do. The
    /// sort itself is stabilized by the buffer index, because <see cref="Array.Sort{T}(T[], Comparison{T})"/>
    /// is introsort and is not stable, and two marks taken in the same clock tick must not swap.
    /// </remarks>
    private static void SortChronologically(List<JsPerformanceEntry> entries)
    {
        for (var i = 1; i < entries.Count; i++)
        {
            if (entries[i].StartTime < entries[i - 1].StartTime)
            {
                StableSort(entries);
                return;
            }
        }
    }

    private static void StableSort(List<JsPerformanceEntry> entries)
    {
        var items = new (double StartTime, int Index, JsPerformanceEntry Entry)[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            items[i] = (entries[i].StartTime, i, entries[i]);
        }

        Array.Sort(items, static (a, b) =>
        {
            var comparison = a.StartTime.CompareTo(b.StartTime);
            return comparison != 0 ? comparison : a.Index.CompareTo(b.Index);
        });

        for (var i = 0; i < items.Length; i++)
        {
            entries[i] = items[i].Entry;
        }
    }

    /// <summary>
    /// The body both <c>clearMarks</c> and <c>clearMeasures</c> share: remove every entry of the given type,
    /// or only those of that type with the given name.
    /// </summary>
    private void RemoveEntries(JsString entryType, JsValue name)
    {
        var entries = _entries;
        if (entries is null || entries.Count == 0)
        {
            return;
        }

        // Optional with no default: an omitted or explicitly undefined name clears them all.
        var filter = name.IsUndefined() ? null : TypeConverter.ToString(name);

        var write = 0;
        for (var read = 0; read < entries.Count; read++)
        {
            var entry = entries[read];

            // Reference equality is exact here: every entry answers with the one shared JsString its type
            // declares.
            var remove = ReferenceEquals(entry.EntryType, entryType)
                && (filter is null || string.Equals(entry.EntryName.ToString(), filter, StringComparison.Ordinal));

            if (!remove)
            {
                entries[write] = entry;
                write++;
            }
        }

        entries.RemoveRange(write, entries.Count - write);
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
    private double ConvertMarkToTimestamp(in UserTiming.MarkOrTimestamp mark, string context)
    {
        if (mark.Mark is not null)
        {
            return TimestampOfMark(mark.Mark, context);
        }

        // Step 3.1: "If mark is negative, throw a TypeError."
        if (mark.Timestamp < 0)
        {
            Throw.TypeError(_realm, context + ": a timestamp cannot be negative.");
        }

        return mark.Timestamp;
    }

    /// <summary>
    /// "let end time be the value of the startTime attribute from the most recent occurrence of a
    /// PerformanceMark object in the performance entry buffer whose name is mark. If no matching entry is
    /// found, throw a SyntaxError."
    /// </summary>
    /// <remarks>
    /// That <c>SyntaxError</c> is the WebIDL error <i>name</i>
    /// (https://webidl.spec.whatwg.org/#syntaxerror), so it is a <c>DOMException</c> named
    /// <c>SyntaxError</c> and not the ECMAScript <c>SyntaxError</c> constructor — which is what a browser
    /// throws here, and what <c>e.name === 'SyntaxError' &amp;&amp; e instanceof DOMException</c> tests for.
    /// </remarks>
    private double TimestampOfMark(string name, string context)
    {
        var entries = _entries;
        if (entries is not null)
        {
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i] is JsPerformanceMark mark
                    && string.Equals(mark.EntryName.ToString(), name, StringComparison.Ordinal))
                {
                    return mark.StartTime;
                }
            }
        }

        var exception = _realm.Intrinsics.DomException.CreateException(
            DomExceptionNames.Syntax,
            context + ": the mark '" + name + "' does not exist.");

        var location = _engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(_engine, exception, in location);
        return 0;
    }

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a platform object implementing
    /// the interface raises a <c>TypeError</c>.
    /// </summary>
    private void Brand(JsValue thisObject, string what)
    {
        if (thisObject is not PerformanceInstance)
        {
            Throw.TypeError(_realm, what + ": illegal invocation, receiver is not a Performance object.");
        }
    }
}
#endif
