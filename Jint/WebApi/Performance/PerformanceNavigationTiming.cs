#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Fetch;

namespace Jint.WebApi.Performance;

/// <summary>https://w3c.github.io/navigation-timing/#sec-PerformanceNavigationTiming.</summary>
internal sealed class JsPerformanceNavigationTiming(Engine engine, in ResourceTimingInfo info, string type, int redirectCount)
    : JsPerformanceResourceTiming(engine, in info)
{
    private static readonly JsString _entryType = new("navigation");
    internal override JsString EntryType => _entryType;
    internal override double Duration => LoadEventEnd;
    internal string NavigationType { get; } = type;
    internal int RedirectCount { get; } = redirectCount;
    internal double DomInteractive { get; set; }
    internal double DomContentLoadedEventStart { get; set; }
    internal double DomContentLoadedEventEnd { get; set; }
    internal double DomComplete { get; set; }
    internal double LoadEventStart { get; set; }
    internal double LoadEventEnd { get; set; }
}

internal sealed class PerformanceNavigationTimingConstructor : Constructor
{
    internal PerformanceNavigationTimingConstructor(Engine engine, Realm realm, PerformanceResourceTimingConstructor parent)
        : base(engine, realm, new JsString("PerformanceNavigationTiming"))
    {
        _prototype = parent;
        PrototypeObject = new PerformanceNavigationTimingPrototype(engine, realm, this, parent.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal PerformanceNavigationTimingPrototype PrototypeObject { get; }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }
}

[JsObject(UseShape = true)]
internal sealed partial class PerformanceNavigationTimingPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly PerformanceNavigationTimingConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString _tag = new("PerformanceNavigationTiming");

    internal PerformanceNavigationTimingPrototype(Engine engine, Realm realm,
        PerformanceNavigationTimingConstructor constructor, ObjectInstance parent) : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = parent;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    [JsAccessor("unloadEventStart", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber UnloadEventStartGet(JsValue thisObject) { Brand(thisObject); return JsNumber.PositiveZero; }

    [JsAccessor("unloadEventEnd", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber UnloadEventEndGet(JsValue thisObject) { Brand(thisObject); return JsNumber.PositiveZero; }

    [JsAccessor("domInteractive", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber DomInteractiveGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).DomInteractive);

    [JsAccessor("domContentLoadedEventStart", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber DomContentLoadedEventStartGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).DomContentLoadedEventStart);

    [JsAccessor("domContentLoadedEventEnd", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber DomContentLoadedEventEndGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).DomContentLoadedEventEnd);

    [JsAccessor("domComplete", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber DomCompleteGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).DomComplete);

    [JsAccessor("loadEventStart", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber LoadEventStartGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).LoadEventStart);

    [JsAccessor("loadEventEnd", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber LoadEventEndGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).LoadEventEnd);

    [JsAccessor("type", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString TypeGet(JsValue thisObject) => JsString.Create(Brand(thisObject).NavigationType);

    [JsAccessor("redirectCount", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber RedirectCountGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).RedirectCount);

    [JsAccessor("criticalCHRestart", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber CriticalCHRestartGet(JsValue thisObject) { Brand(thisObject); return JsNumber.PositiveZero; }

    [JsAccessor("notRestoredReasons", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue NotRestoredReasonsGet(JsValue thisObject) { Brand(thisObject); return Null; }

    [JsFunction(Name = "toJSON", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsObject ToJson(JsValue thisObject)
    {
        var entry = Brand(thisObject);
        var result = _realm.Intrinsics.PerformanceResourceTiming.PrototypeObject.CreateJson(entry);
        result.CreateDataPropertyOrThrow("unloadEventStart", JsNumber.PositiveZero);
        result.CreateDataPropertyOrThrow("unloadEventEnd", JsNumber.PositiveZero);
        result.CreateDataPropertyOrThrow("domInteractive", JsNumber.Create(entry.DomInteractive));
        result.CreateDataPropertyOrThrow("domContentLoadedEventStart", JsNumber.Create(entry.DomContentLoadedEventStart));
        result.CreateDataPropertyOrThrow("domContentLoadedEventEnd", JsNumber.Create(entry.DomContentLoadedEventEnd));
        result.CreateDataPropertyOrThrow("domComplete", JsNumber.Create(entry.DomComplete));
        result.CreateDataPropertyOrThrow("loadEventStart", JsNumber.Create(entry.LoadEventStart));
        result.CreateDataPropertyOrThrow("loadEventEnd", JsNumber.Create(entry.LoadEventEnd));
        result.CreateDataPropertyOrThrow("type", JsString.Create(entry.NavigationType));
        result.CreateDataPropertyOrThrow("redirectCount", JsNumber.Create(entry.RedirectCount));
        result.CreateDataPropertyOrThrow("criticalCHRestart", JsNumber.PositiveZero);
        result.CreateDataPropertyOrThrow("notRestoredReasons", Null);
        return result;
    }

    private JsPerformanceNavigationTiming Brand(JsValue thisObject)
    {
        if (thisObject is JsPerformanceNavigationTiming entry) return entry;
        Throw.TypeError(_realm, "Illegal invocation: receiver is not a PerformanceNavigationTiming");
        return null!;
    }
}
#endif
