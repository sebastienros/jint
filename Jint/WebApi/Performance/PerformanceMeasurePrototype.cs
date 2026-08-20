#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Performance;

/// <summary>
/// <c>PerformanceMeasure.prototype</c> — the interface prototype object.
/// <para>
/// https://w3c.github.io/user-timing/#dom-performancemeasure
/// </para>
/// </summary>
/// <remarks>
/// The sibling of <see cref="PerformanceMarkPrototype"/>: one added attribute, brand-checked for this
/// interface alone.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class PerformanceMeasurePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly PerformanceMeasureConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString PerformanceMeasureToStringTag = new("PerformanceMeasure");

    internal PerformanceMeasurePrototype(
        Engine engine,
        Realm realm,
        PerformanceMeasureConstructor constructor,
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
    /// https://w3c.github.io/user-timing/#dom-performancemeasure-detail — "The detail attribute must return
    /// the value it is set to (it's copied from the PerformanceMeasureOptions dictionary)."
    /// </summary>
    [JsAccessor("detail", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue DetailGet(JsValue thisObject)
    {
        if (thisObject is not JsPerformanceMeasure measure)
        {
            Throw.TypeError(_realm, "Illegal invocation: receiver is not a PerformanceMeasure");
            return null!;
        }

        return measure.Detail;
    }
}
#endif
