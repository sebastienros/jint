#if NET8_0_OR_GREATER
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Performance;

/// <summary>
/// A <c>PerformanceObserverEntryList</c> instance — the entries one observer callback was handed.
/// <para>
/// https://w3c.github.io/performance-timeline/#performanceobserverentrylist
/// </para>
/// </summary>
/// <remarks>
/// The list is fixed at construction and is a copy of what the observer buffer held, so a callback that goes
/// on to create more entries sees none of them here — which is what makes the argument a record of one
/// delivery rather than a live view of the timeline.
/// </remarks>
internal sealed class JsPerformanceObserverEntryList : ObjectInstance
{
    internal JsPerformanceObserverEntryList(Engine engine, Realm realm, List<JsPerformanceEntry> entries)
        : base(engine, ObjectClass.Object)
    {
        Realm = realm;
        Entries = entries;
    }

    /// <summary>The realm the three getters build their arrays against.</summary>
    internal Realm Realm { get; }

    /// <summary>https://w3c.github.io/performance-timeline/#dfn-entry-list.</summary>
    internal List<JsPerformanceEntry> Entries { get; }
}
#endif
