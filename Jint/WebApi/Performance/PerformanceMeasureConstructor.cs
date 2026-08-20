#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Performance;

/// <summary>
/// The <c>PerformanceMeasure</c> interface object.
/// <para>
/// https://w3c.github.io/user-timing/#dom-performancemeasure
/// </para>
/// </summary>
/// <remarks>
/// <c>PerformanceMeasure</c> inherits from <c>PerformanceEntry</c>, so its <c>[[Prototype]]</c> is the
/// <c>PerformanceEntry</c> interface object. It declares no constructor operation — a measure only ever comes
/// from <c>performance.measure()</c> — which in WebIDL means the interface object exists and is a function
/// but refuses to construct anything, https://webidl.spec.whatwg.org/#es-interface-call.
/// </remarks>
internal sealed class PerformanceMeasureConstructor : Constructor
{
    private static readonly JsString _functionName = new("PerformanceMeasure");

    internal PerformanceMeasureConstructor(Engine engine, Realm realm, PerformanceEntryConstructor entryConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = entryConstructor;
        PrototypeObject = new PerformanceMeasurePrototype(engine, realm, this, entryConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal PerformanceMeasurePrototype PrototypeObject { get; }

    /// <summary>
    /// An interface without a constructor operation is not constructible.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }
}
#endif
