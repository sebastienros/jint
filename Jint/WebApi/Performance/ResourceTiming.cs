#if NET8_0_OR_GREATER
using Jint.Runtime;
using Jint.WebApi.Fetch;

namespace Jint.WebApi.Performance;

/// <summary>https://w3c.github.io/resource-timing/#dfn-mark-resource-timing.</summary>
internal static class ResourceTiming
{
    internal static FetchResourceTiming? Start(Engine engine, Realm realm, string url, string initiatorType,
        string? origin, string credentials, bool renderBlocking = false, bool queue = true)
    {
        if ((engine._webApiFeatures & WebApiFeatures.Performance) == WebApiFeatures.None || engine._webApi is not { } state)
        {
            return null;
        }

        var registration = engine.CaptureEventLoopRegistration();
        return new FetchResourceTiming(state.TimeProvider, state.CurrentHighResolutionTime, url, initiatorType,
            origin, credentials, renderBlocking, queue ? Report : null);

        // Only posting crosses the transport thread; intrinsics and entries are touched inside the job.
        void Report(ResourceTimingInfo info) => engine.AddToEventLoop(
            () => Add(engine, realm, in info), registration, EventLoopJobKind.Task);
    }

    internal static void Add(Engine engine, Realm realm, in ResourceTimingInfo info)
    {
        var entry = new JsPerformanceResourceTiming(engine, in info)
        {
            _prototype = realm.Intrinsics.PerformanceResourceTiming.PrototypeObject,
        };
        realm.Intrinsics.PerformanceObject.QueuePerformanceEntry(entry);
    }
}
#endif
