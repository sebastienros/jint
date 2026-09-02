#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Performance;

/// <summary>
/// <c>PerformanceObserverEntryList.prototype</c> — the interface prototype object.
/// <para>
/// https://w3c.github.io/performance-timeline/#performanceobserverentrylist
/// </para>
/// </summary>
/// <remarks>
/// The three operations are the same three <c>performance</c> declares and run the same filter, over the
/// entries of one delivery instead of over the timeline — see <see cref="PerformanceEntryFilter"/>.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class PerformanceObserverEntryListPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly PerformanceObserverEntryListConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString PerformanceObserverEntryListToStringTag = new("PerformanceObserverEntryList");

    internal PerformanceObserverEntryListPrototype(
        Engine engine,
        Realm realm,
        PerformanceObserverEntryListConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
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
    /// https://w3c.github.io/performance-timeline/#dom-performanceobserverentrylist-getentries
    /// </summary>
    [JsFunction(Name = "getEntries", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsArray GetEntries(JsValue thisObject)
    {
        var list = Brand(thisObject, "getEntries");
        return PerformanceEntryFilter.Filter(list.Realm, list.Entries, name: null, type: null);
    }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performanceobserverentrylist-getentriesbytype
    /// </summary>
    [JsFunction(Name = "getEntriesByType", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsArray GetEntriesByType(JsValue thisObject, JsValue type)
    {
        var list = Brand(thisObject, "getEntriesByType");
        return PerformanceEntryFilter.Filter(list.Realm, list.Entries, name: null, TypeConverter.ToString(type));
    }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performanceobserverentrylist-getentriesbyname — the
    /// entry type is an optional argument with no default, so an omitted or explicitly undefined one filters
    /// on the name alone.
    /// </summary>
    [JsFunction(Name = "getEntriesByName", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsArray GetEntriesByName(JsValue thisObject, JsValue name, JsValue type)
    {
        var list = Brand(thisObject, "getEntriesByName");
        return PerformanceEntryFilter.Filter(
            list.Realm,
            list.Entries,
            TypeConverter.ToString(name),
            type.IsUndefined() ? null : TypeConverter.ToString(type));
    }

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a platform object implementing
    /// the interface raises a <c>TypeError</c>.
    /// </summary>
    private JsPerformanceObserverEntryList Brand(JsValue thisObject, string operation)
    {
        if (thisObject is JsPerformanceObserverEntryList list)
        {
            return list;
        }

        Throw.TypeError(
            _realm,
            $"Failed to execute '{operation}' on 'PerformanceObserverEntryList': illegal invocation, receiver is not a PerformanceObserverEntryList.");
        return null!;
    }
}
#endif
