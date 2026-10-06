#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Performance;

/// <summary>https://w3c.github.io/resource-timing/#sec-performanceresourcetiming.</summary>
[JsObject(UseShape = true)]
internal sealed partial class PerformanceResourceTimingPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly PerformanceResourceTimingConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString _tag = new("PerformanceResourceTiming");

    internal PerformanceResourceTimingPrototype(Engine engine, Realm realm,
        PerformanceResourceTimingConstructor constructor, ObjectInstance parent) : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = parent;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    [JsAccessor("initiatorType", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString InitiatorTypeGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Info.InitiatorType);

    [JsAccessor("deliveryType", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString DeliveryTypeGet(JsValue thisObject) { Brand(thisObject); return JsString.Empty; }

    [JsAccessor("nextHopProtocol", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString NextHopProtocolGet(JsValue thisObject)
    {
        var info = Brand(thisObject).Info;
        return JsString.Create(info.TimingAllowed ? info.NextHopProtocol : "");
    }

    [JsAccessor("workerStart", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber WorkerStartGet(JsValue thisObject) { Brand(thisObject); return JsNumber.PositiveZero; }

    [JsAccessor("redirectStart", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber RedirectStartGet(JsValue thisObject) { Brand(thisObject); return JsNumber.PositiveZero; }

    [JsAccessor("redirectEnd", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber RedirectEndGet(JsValue thisObject) { Brand(thisObject); return JsNumber.PositiveZero; }

    [JsAccessor("fetchStart", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber FetchStartGet(JsValue thisObject)
    {
        var info = Brand(thisObject).Info;
        return JsNumber.Create(info.TimingAllowed ? info.FetchStart : info.StartTime);
    }

    [JsAccessor("domainLookupStart", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber DomainLookupStartGet(JsValue thisObject) => UnavailablePhase(thisObject);

    [JsAccessor("domainLookupEnd", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber DomainLookupEndGet(JsValue thisObject) => UnavailablePhase(thisObject);

    [JsAccessor("connectStart", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber ConnectStartGet(JsValue thisObject) => UnavailablePhase(thisObject);

    [JsAccessor("connectEnd", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber ConnectEndGet(JsValue thisObject) => UnavailablePhase(thisObject);

    [JsAccessor("secureConnectionStart", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber SecureConnectionStartGet(JsValue thisObject) { Brand(thisObject); return JsNumber.PositiveZero; }

    [JsAccessor("requestStart", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber RequestStartGet(JsValue thisObject) => UnavailablePhase(thisObject);

    [JsAccessor("responseStart", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber ResponseStartGet(JsValue thisObject)
    {
        var info = Brand(thisObject).Info;
        return JsNumber.Create(info.TimingAllowed ? info.ResponseStart : 0);
    }

    [JsAccessor("finalResponseHeadersStart", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber FinalResponseHeadersStartGet(JsValue thisObject) => ResponseStartGet(thisObject);

    [JsAccessor("firstInterimResponseStart", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber FirstInterimResponseStartGet(JsValue thisObject) { Brand(thisObject); return JsNumber.PositiveZero; }

    [JsAccessor("responseEnd", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber ResponseEndGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).Info.ResponseEnd);

    [JsAccessor("transferSize", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber TransferSizeGet(JsValue thisObject)
    {
        var info = Brand(thisObject).Info;
        return JsNumber.Create(info.TimingAllowed ? info.EncodedBodySize + 300 : 0);
    }

    [JsAccessor("encodedBodySize", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber EncodedBodySizeGet(JsValue thisObject)
    {
        var info = Brand(thisObject).Info;
        return JsNumber.Create(info.TimingAllowed ? info.EncodedBodySize : 0);
    }

    [JsAccessor("decodedBodySize", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber DecodedBodySizeGet(JsValue thisObject)
    {
        var info = Brand(thisObject).Info;
        return JsNumber.Create(info.TimingAllowed ? info.DecodedBodySize : 0);
    }

    [JsAccessor("responseStatus", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber ResponseStatusGet(JsValue thisObject)
    {
        var info = Brand(thisObject).Info;
        return JsNumber.Create(info.TimingAllowed ? info.ResponseStatus : 0);
    }

    [JsAccessor("renderBlockingStatus", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString RenderBlockingStatusGet(JsValue thisObject)
        => JsString.Create(Brand(thisObject).Info.RenderBlocking ? "blocking" : "non-blocking");

    [JsAccessor("contentType", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString ContentTypeGet(JsValue thisObject)
    {
        var info = Brand(thisObject).Info;
        return JsString.Create(info.TimingAllowed ? info.ContentType : "");
    }

    [JsAccessor("serverTiming", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsArray ServerTimingGet(JsValue thisObject)
    {
        Brand(thisObject);
        var array = _realm.Intrinsics.Array.ConstructFast(System.Array.Empty<JsValue>());
        array.SetIntegrityLevel(IntegrityLevel.Frozen);
        return array;
    }

    [JsFunction(Name = "toJSON", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsObject ToJson(JsValue thisObject) => CreateJson(Brand(thisObject));

    internal JsObject CreateJson(JsPerformanceResourceTiming entry)
    {
        var result = new JsObject(_engine);
        result.CreateDataPropertyOrThrow("name", entry.EntryName);
        result.CreateDataPropertyOrThrow("entryType", entry.EntryType);
        result.CreateDataPropertyOrThrow("startTime", JsNumber.Create(entry.StartTime));
        result.CreateDataPropertyOrThrow("duration", JsNumber.Create(entry.Duration));
        result.CreateDataPropertyOrThrow("initiatorType", InitiatorTypeGet(entry));
        result.CreateDataPropertyOrThrow("deliveryType", DeliveryTypeGet(entry));
        result.CreateDataPropertyOrThrow("nextHopProtocol", NextHopProtocolGet(entry));
        result.CreateDataPropertyOrThrow("workerStart", WorkerStartGet(entry));
        result.CreateDataPropertyOrThrow("redirectStart", RedirectStartGet(entry));
        result.CreateDataPropertyOrThrow("redirectEnd", RedirectEndGet(entry));
        result.CreateDataPropertyOrThrow("fetchStart", FetchStartGet(entry));
        result.CreateDataPropertyOrThrow("domainLookupStart", DomainLookupStartGet(entry));
        result.CreateDataPropertyOrThrow("domainLookupEnd", DomainLookupEndGet(entry));
        result.CreateDataPropertyOrThrow("connectStart", ConnectStartGet(entry));
        result.CreateDataPropertyOrThrow("connectEnd", ConnectEndGet(entry));
        result.CreateDataPropertyOrThrow("secureConnectionStart", SecureConnectionStartGet(entry));
        result.CreateDataPropertyOrThrow("requestStart", RequestStartGet(entry));
        result.CreateDataPropertyOrThrow("responseStart", ResponseStartGet(entry));
        result.CreateDataPropertyOrThrow("finalResponseHeadersStart", FinalResponseHeadersStartGet(entry));
        result.CreateDataPropertyOrThrow("firstInterimResponseStart", FirstInterimResponseStartGet(entry));
        result.CreateDataPropertyOrThrow("responseEnd", ResponseEndGet(entry));
        result.CreateDataPropertyOrThrow("transferSize", TransferSizeGet(entry));
        result.CreateDataPropertyOrThrow("encodedBodySize", EncodedBodySizeGet(entry));
        result.CreateDataPropertyOrThrow("decodedBodySize", DecodedBodySizeGet(entry));
        result.CreateDataPropertyOrThrow("responseStatus", ResponseStatusGet(entry));
        result.CreateDataPropertyOrThrow("renderBlockingStatus", RenderBlockingStatusGet(entry));
        result.CreateDataPropertyOrThrow("contentType", ContentTypeGet(entry));
        result.CreateDataPropertyOrThrow("serverTiming", ServerTimingGet(entry));
        return result;
    }

    private JsNumber UnavailablePhase(JsValue thisObject)
    {
        var info = Brand(thisObject).Info;
        return JsNumber.Create(info.TimingAllowed ? info.FetchStart : 0);
    }

    private JsPerformanceResourceTiming Brand(JsValue thisObject)
    {
        if (thisObject is JsPerformanceResourceTiming entry) return entry;
        Throw.TypeError(_realm, "Illegal invocation: receiver is not a PerformanceResourceTiming");
        return null!;
    }
}
#endif
