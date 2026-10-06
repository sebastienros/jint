#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Performance;

/// <summary>https://w3c.github.io/resource-timing/#sec-performanceresourcetiming.</summary>
internal sealed class PerformanceResourceTimingConstructor : Constructor
{
    internal PerformanceResourceTimingConstructor(Engine engine, Realm realm, PerformanceEntryConstructor parent)
        : base(engine, realm, new JsString("PerformanceResourceTiming"))
    {
        _prototype = parent;
        PrototypeObject = new PerformanceResourceTimingPrototype(engine, realm, this, parent.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal PerformanceResourceTimingPrototype PrototypeObject { get; }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }
}
#endif
