#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Performance;

/// <summary>
/// <c>PerformanceEntry.prototype</c> — the interface prototype object.
/// <para>
/// https://w3c.github.io/performance-timeline/#dom-performanceentry
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The four attributes are accessors here rather than own properties of the instance, as WebIDL specifies
/// attributes; each brand-checks its receiver and raises a <c>TypeError</c> for anything that is not a
/// performance entry — including <c>PerformanceEntry.prototype</c> itself, which is not one.
/// </para>
/// <para>
/// <c>toJSON</c> is declared <c>[Default]</c> in the IDL, so its behaviour is
/// https://webidl.spec.whatwg.org/#default-tojson-steps run for <c>PerformanceEntry</c>: the inheritance
/// stack it collects from is this interface alone, and only attributes with a <i>JSON type</i> are collected.
/// Both rules point the same way for <c>detail</c>, which is declared on the two derived interfaces and is of
/// type <c>any</c> — so it is <b>not</b> part of the result, and a script that wants it reads
/// <c>entry.detail</c>.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class PerformanceEntryPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly PerformanceEntryConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString PerformanceEntryToStringTag = new("PerformanceEntry");

    /// <summary>
    /// The shape of what <c>toJSON</c> answers, declared once so every result object in an engine shares one
    /// hidden class and a loop reading <c>.duration</c> off them keeps a monomorphic inline cache. The order
    /// is the declaration order of the IDL attributes, which is the order the default <c>toJSON</c> steps
    /// collect them in.
    /// </summary>
    private static readonly JsObjectLayout _jsonLayout = JsObjectLayout.CreateBuilder()
        .Add("name")
        .Add("entryType")
        .Add("startTime")
        .Add("duration")
        .Build();

    internal PerformanceEntryPrototype(
        Engine engine,
        Realm realm,
        PerformanceEntryConstructor constructor,
        ObjectInstance objectPrototype) : base(engine, realm)
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
    /// https://w3c.github.io/performance-timeline/#dom-performanceentry-name
    /// </summary>
    [JsAccessor("name", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString NameGet(JsValue thisObject) => Brand(thisObject).EntryName;

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performanceentry-entrytype
    /// </summary>
    [JsAccessor("entryType", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString EntryTypeGet(JsValue thisObject) => Brand(thisObject).EntryType;

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performanceentry-starttime
    /// </summary>
    [JsAccessor("startTime", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber StartTimeGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).StartTime);

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performanceentry-duration
    /// </summary>
    [JsAccessor("duration", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber DurationGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).Duration);

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performanceentry-tojson — the default <c>toJSON</c>
    /// steps, https://webidl.spec.whatwg.org/#default-tojson-steps.
    /// </summary>
    [JsFunction(Name = "toJSON", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsObject ToJson(JsValue thisObject)
    {
        var entry = Brand(thisObject);
        return JsObject.Create(
            _engine,
            _jsonLayout,
            [entry.EntryName, entry.EntryType, JsNumber.Create(entry.StartTime), JsNumber.Create(entry.Duration)]);
    }

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a platform object implementing
    /// the interface raises a <c>TypeError</c>.
    /// </summary>
    private JsPerformanceEntry Brand(JsValue thisObject)
    {
        if (thisObject is JsPerformanceEntry entry)
        {
            return entry;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a PerformanceEntry");
        return null!;
    }
}
#endif
