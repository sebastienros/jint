#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Performance;

/// <summary>
/// <c>PerformanceMark.prototype</c> — the interface prototype object.
/// <para>
/// https://w3c.github.io/user-timing/#dom-performancemark
/// </para>
/// </summary>
/// <remarks>
/// The interface adds exactly one attribute to <c>PerformanceEntry</c>, and its brand check is for a
/// <c>PerformanceMark</c> specifically: reading it off a <c>PerformanceMeasure</c>, which carries a
/// <c>detail</c> of its own, is still a <c>TypeError</c>.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class PerformanceMarkPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly PerformanceMarkConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString PerformanceMarkToStringTag = new("PerformanceMark");

    internal PerformanceMarkPrototype(
        Engine engine,
        Realm realm,
        PerformanceMarkConstructor constructor,
        ObjectInstance entryPrototype) : base(engine, realm)
    {
        _prototype = entryPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://w3c.github.io/user-timing/#dom-performancemark-detail — "The detail attribute must return the
    /// value it is set to (it's copied from the PerformanceMarkOptions dictionary)."
    /// </summary>
    [JsAccessor("detail", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue DetailGet(JsValue thisObject)
    {
        if (thisObject is not JsPerformanceMark mark)
        {
            Throw.TypeError(_realm, "Illegal invocation: receiver is not a PerformanceMark");
            return null!;
        }

        return mark.Detail;
    }
}
#endif
