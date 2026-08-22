#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.WebApi.Performance;

/// <summary>
/// The <c>performance</c> object — the realm's one instance of the <c>Performance</c> interface, and the owner
/// of its performance entry buffer.
/// <para>
/// https://w3c.github.io/hr-time/#sec-performance
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The split against <see cref="PerformancePrototype"/> is the one WebIDL draws: the <i>members</i> are the
/// interface's and live on the prototype, while the <i>timeline</i> — the entry buffer and everything that
/// reads or writes it — is state of this object. The prototype's members brand-check their receiver and then
/// operate on it, so an extracted <c>getEntries</c> is exactly as usable as a browser's.
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
/// </remarks>
internal sealed class JsPerformance : ObjectInstance
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

    private readonly Realm _realm;

    /// <summary>
    /// The performance entry buffer, https://w3c.github.io/performance-timeline/#performance-entry-buffer,
    /// in the order the entries were added. Null until the first entry, so a script that only reads
    /// <c>now()</c> allocates nothing.
    /// </summary>
    private List<JsPerformanceEntry>? _entries;

    private JsPerformance(Engine engine, Realm realm, WebApiEngineState state) : base(engine, ObjectClass.Object)
    {
        _realm = realm;
        State = state;
    }

    /// <summary>
    /// The engine's web-API state, which is where the clock and the time origin live —
    /// <c>performance.now()</c> and <c>performance.timeOrigin</c> are the two halves of one reading, and the
    /// timers are scheduled against the very same provider.
    /// </summary>
    internal WebApiEngineState State { get; }

    internal static JsPerformance Create(Engine engine, Realm realm)
    {
        var state = UserTiming.RequireState(engine, "The performance object");

        return new JsPerformance(engine, realm, state)
        {
            _prototype = realm.Intrinsics.Performance.PrototypeObject,
        };
    }

    /// <summary>
    /// The surviving half of "queue a PerformanceEntry": append unless the buffer is full, in which case the
    /// entry is silently dropped. See <see cref="MaxBufferedEntries"/>.
    /// </summary>
    internal void AddToBuffer(JsPerformanceEntry entry)
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
    internal JsArray FilterBuffer(string? name, string? type)
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
    /// The body both <c>clearMarks</c> and <c>clearMeasures</c> share: remove every entry of the given type,
    /// or only those of that type with the given name.
    /// </summary>
    internal void RemoveEntries(JsString entryType, JsValue name)
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
    internal double TimestampOfMark(string name, string context)
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
}
#endif
