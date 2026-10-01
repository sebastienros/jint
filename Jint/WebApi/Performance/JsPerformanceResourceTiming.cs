#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.WebApi.Fetch;

namespace Jint.WebApi.Performance;

/// <summary>https://w3c.github.io/resource-timing/#sec-performanceresourcetiming.</summary>
internal class JsPerformanceResourceTiming : JsPerformanceEntry
{
    internal static readonly JsString ResourceEntryType = new("resource");

    internal JsPerformanceResourceTiming(Engine engine, in ResourceTimingInfo info)
        : base(engine, JsString.Create(info.Name), info.StartTime, info.ResponseEnd - info.StartTime, Null)
    {
        Info = info;
    }

    internal ResourceTimingInfo Info { get; }
    internal override JsString EntryType => ResourceEntryType;
}
#endif
