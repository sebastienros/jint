#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.StructuredClone;

namespace Jint.WebApi.Performance;

/// <summary>
/// The argument conversions the User Timing API shares between <c>performance.mark</c>,
/// <c>performance.measure</c> and the <c>PerformanceMark</c> constructor.
/// <para>
/// https://w3c.github.io/user-timing/
/// </para>
/// </summary>
/// <remarks>
/// The WebIDL dictionary conversion happens <b>before</b> the algorithm's own steps run, and it reads the
/// members in <i>lexicographical order of their identifiers</i>
/// (https://webidl.spec.whatwg.org/#es-dictionary) — not in the order the algorithm consults them. That is
/// observable whenever a member is an accessor, so the readers here follow it exactly: <c>detail</c> then
/// <c>startTime</c> for a mark, and <c>detail</c>, <c>duration</c>, <c>end</c>, <c>start</c> for a measure.
/// A member whose value is <see langword="undefined"/> and which has no default is <i>not present</i>, which
/// is what makes <c>{ start: undefined }</c> mean "no start" rather than "start of NaN".
/// </remarks>
internal static class UserTiming
{
    /// <summary>
    /// A <c>(DOMString or DOMHighResTimeStamp)</c> that has already been through the WebIDL union
    /// conversion: either the name of a mark to look up, or a timestamp to use as it stands.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct MarkOrTimestamp(string? Mark, double Timestamp)
    {
        /// <summary>Whether this is a mark name rather than a timestamp.</summary>
        internal bool IsMark => Mark is not null;
    }

    /// <summary>
    /// https://w3c.github.io/user-timing/#dictdef-performancemarkoptions — <c>detail</c> defaults to
    /// <c>null</c>, <c>startTime</c> has no default and so may be absent.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct MarkOptions(JsValue Detail, bool HasStartTime, double StartTime);

    /// <summary>
    /// https://w3c.github.io/user-timing/#dictdef-performancemeasureoptions — every member is optional and
    /// none has a default, so each carries its own "is present" flag. Which combinations are legal is
    /// <c>measure</c>'s business, not the conversion's.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct MeasureOptions(
        bool HasDetail,
        JsValue Detail,
        bool HasDuration,
        double Duration,
        bool HasEnd,
        MarkOrTimestamp End,
        bool HasStart,
        MarkOrTimestamp Start)
    {
        /// <summary>
        /// Whether the value was a dictionary carrying at least one member, which is the condition
        /// https://w3c.github.io/user-timing/#dom-performance-measure step 1 gates its three
        /// <c>TypeError</c>s on.
        /// </summary>
        internal bool HasAnyMember => HasDetail || HasDuration || HasEnd || HasStart;
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-double — "Let x be ? ToNumber(V). If x is NaN, +∞, or −∞, throw a
    /// TypeError." <c>DOMHighResTimeStamp</c> is a <c>double</c>, not an <c>unrestricted double</c>, so an
    /// infinite or NaN timestamp is rejected at the boundary rather than propagated into the arithmetic.
    /// </summary>
    internal static double ToHighResTimeStamp(Realm realm, JsValue value, string context, string member)
    {
        var number = TypeConverter.ToNumber(value);
        if (!double.IsFinite(number))
        {
            Throw.TypeError(realm, context + ": the '" + member + "' value is not a finite number.");
        }

        return number;
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-union for <c>(DOMString or DOMHighResTimeStamp)</c>: a Number
    /// becomes the timestamp and everything else — a string, an object, a boolean, <c>null</c>, a BigInt —
    /// becomes the mark name, because the union has no object or dictionary member for them to land in.
    /// </summary>
    /// <remarks>
    /// The test is on the JavaScript type, so a boxed <c>new Number(5)</c> is an Object and is stringified to
    /// the mark name <c>"5"</c>, exactly as the union conversion requires.
    /// </remarks>
    internal static MarkOrTimestamp ToMarkOrTimestamp(Realm realm, JsValue value, string context, string member)
    {
        if (value is JsNumber)
        {
            return new MarkOrTimestamp(Mark: null, ToHighResTimeStamp(realm, value, context, member));
        }

        return new MarkOrTimestamp(TypeConverter.ToString(value), Timestamp: 0);
    }

    /// <summary>
    /// The <c>PerformanceMarkOptions</c> dictionary conversion.
    /// </summary>
    /// <remarks>
    /// The IDL default of <c>detail</c> is <c>null</c>, which is why an absent dictionary, an absent member
    /// and an explicit <see langword="undefined"/> all give the same answer — and why
    /// <c>performance.mark('x').detail</c> is <c>null</c> rather than <c>undefined</c>.
    /// </remarks>
    internal static MarkOptions ReadMarkOptions(Realm realm, JsValue options, string context)
    {
        if (options is not ObjectInstance dictionary)
        {
            return new MarkOptions(JsValue.Null, HasStartTime: false, StartTime: 0);
        }

        var detail = dictionary.Get("detail");
        if (detail.IsUndefined())
        {
            detail = JsValue.Null;
        }

        var startTime = dictionary.Get("startTime");
        if (startTime.IsUndefined())
        {
            return new MarkOptions(detail, HasStartTime: false, StartTime: 0);
        }

        return new MarkOptions(detail, HasStartTime: true, ToHighResTimeStamp(realm, startTime, context, "startTime"));
    }

    /// <summary>
    /// The <c>PerformanceMeasureOptions</c> dictionary conversion.
    /// </summary>
    internal static MeasureOptions ReadMeasureOptions(Realm realm, ObjectInstance dictionary, string context)
    {
        var detail = dictionary.Get("detail");
        var hasDetail = !detail.IsUndefined();

        var durationValue = dictionary.Get("duration");
        var hasDuration = !durationValue.IsUndefined();
        var duration = hasDuration ? ToHighResTimeStamp(realm, durationValue, context, "duration") : 0;

        var endValue = dictionary.Get("end");
        var hasEnd = !endValue.IsUndefined();
        var end = hasEnd ? ToMarkOrTimestamp(realm, endValue, context, "end") : default;

        var startValue = dictionary.Get("start");
        var hasStart = !startValue.IsUndefined();
        var start = hasStart ? ToMarkOrTimestamp(realm, startValue, context, "start") : default;

        return new MeasureOptions(hasDetail, detail, hasDuration, duration, hasEnd, end, hasStart, start);
    }

    /// <summary>
    /// "Run the StructuredSerialize algorithm … then run the StructuredDeserialize algorithm", which is what
    /// both entry types do with their <c>detail</c> — https://w3c.github.io/user-timing/#dom-performancemark.
    /// </summary>
    /// <remarks>
    /// So a detail that cannot be cloned (a function, a symbol, a host-wrapped CLR object) raises a
    /// <c>DataCloneError</c> <c>DOMException</c> from the very same code <c>structuredClone</c> uses, and a
    /// detail that can is disconnected from whatever the caller goes on to do with the original object.
    /// </remarks>
    internal static JsValue CloneDetail(Engine engine, Realm realm, JsValue detail)
    {
        if (detail.IsNull())
        {
            return JsValue.Null;
        }

        return new StructuredCloner(engine, realm).Clone(detail, transferList: null);
    }

    /// <summary>
    /// The engine's web-API state, which every high resolution time reading comes from.
    /// </summary>
    /// <remarks>
    /// Unreachable in the failing direction: the globals that reach this are installed only where
    /// <c>WebApiRegistration</c> created the state, in the same block.
    /// </remarks>
    internal static WebApiEngineState RequireState(Engine engine, string what)
    {
        var state = engine._webApi;
        if (state is null)
        {
            Throw.InvalidOperationException(what + " was reached on an engine that has no web-API state.");
        }

        return state;
    }
}
#endif
