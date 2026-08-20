#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;

namespace Jint.WebApi.Performance;

/// <summary>
/// A <c>PerformanceEntry</c> instance — one metric on the performance timeline.
/// <para>
/// https://w3c.github.io/performance-timeline/#dom-performanceentry
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Every IDL attribute of <c>PerformanceEntry</c> is read-only, so the whole state lives in CLR fields here
/// and <see cref="PerformanceEntryPrototype"/> reads it through a brand check, exactly as <c>Event</c> and
/// <c>DOMException</c> do. An instance therefore has no own property at all, which is what a browser reports
/// for <c>Object.getOwnPropertyNames(performance.mark('x'))</c>.
/// </para>
/// <para>
/// The class is abstract because the specification never creates a bare <c>PerformanceEntry</c>: the
/// interface has no constructor operation and every entry is of some registered entry type. The two Jint
/// implements are <see cref="JsPerformanceMark"/> and <see cref="JsPerformanceMeasure"/>, and each is a CLR
/// type of its own precisely so the <c>detail</c> getter on either prototype can brand-check for its own
/// interface rather than for "an entry that happens to carry a detail".
/// </para>
/// <para>
/// Two attributes of the current IDL are deliberately absent rather than faked: <c>id</c>, and
/// <c>navigationId</c>, which names the document navigation an entry belongs to and so has no meaning where
/// there is no document.
/// </para>
/// </remarks>
internal abstract class JsPerformanceEntry : ObjectInstance
{
    private protected JsPerformanceEntry(Engine engine, JsString name, double startTime, double duration, JsValue detail)
        : base(engine, ObjectClass.Object)
    {
        EntryName = name;
        StartTime = startTime;
        Duration = duration;
        Detail = detail;
    }

    /// <summary>https://w3c.github.io/performance-timeline/#dom-performanceentry-name.</summary>
    internal JsString EntryName { get; }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performanceentry-entrytype — <c>"mark"</c> or
    /// <c>"measure"</c>, the two entry types https://w3c.github.io/timing-entrytypes-registry/ defines for a
    /// runtime with no document to navigate.
    /// </summary>
    internal abstract JsString EntryType { get; }

    /// <summary>https://w3c.github.io/performance-timeline/#dom-performanceentry-starttime.</summary>
    internal double StartTime { get; }

    /// <summary>https://w3c.github.io/performance-timeline/#dom-performanceentry-duration.</summary>
    internal double Duration { get; }

    /// <summary>
    /// The already-cloned <c>detail</c>, or <c>null</c>. It is an attribute of <c>PerformanceMark</c> and
    /// <c>PerformanceMeasure</c> rather than of <c>PerformanceEntry</c>, but both carry one and the storage is
    /// identical, so it lives here and the brand check that guards it lives on each prototype.
    /// </summary>
    internal JsValue Detail { get; }
}

/// <summary>
/// A <c>PerformanceMark</c> instance.
/// <para>
/// https://w3c.github.io/user-timing/#dom-performancemark
/// </para>
/// </summary>
internal sealed class JsPerformanceMark : JsPerformanceEntry
{
    /// <summary>The one <c>entryType</c> string every mark shares.</summary>
    internal static readonly JsString MarkEntryType = new("mark");

    internal JsPerformanceMark(Engine engine, JsString name, double startTime, JsValue detail)
        : base(engine, name, startTime, duration: 0, detail)
    {
    }

    /// <summary>
    /// https://w3c.github.io/user-timing/#dom-performancemark — "Set entry's entryType attribute to
    /// DOMString 'mark'".
    /// </summary>
    internal override JsString EntryType => MarkEntryType;
}

/// <summary>
/// A <c>PerformanceMeasure</c> instance.
/// <para>
/// https://w3c.github.io/user-timing/#dom-performancemeasure
/// </para>
/// </summary>
internal sealed class JsPerformanceMeasure : JsPerformanceEntry
{
    /// <summary>The one <c>entryType</c> string every measure shares.</summary>
    internal static readonly JsString MeasureEntryType = new("measure");

    internal JsPerformanceMeasure(Engine engine, JsString name, double startTime, double duration, JsValue detail)
        : base(engine, name, startTime, duration, detail)
    {
    }

    /// <summary>
    /// https://w3c.github.io/user-timing/#dom-performancemeasure — "Set entry's entryType attribute to
    /// DOMString 'measure'".
    /// </summary>
    internal override JsString EntryType => MeasureEntryType;
}
#endif
