#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Performance;

/// <summary>
/// The <c>PerformanceEntry</c> interface object.
/// <para>
/// https://w3c.github.io/performance-timeline/#dom-performanceentry
/// </para>
/// </summary>
/// <remarks>
/// The interface declares no constructor operation, which in WebIDL means the interface object exists and is
/// a function but refuses to construct anything — https://webidl.spec.whatwg.org/#es-interface-call. It is
/// exposed all the same, because it is the object <c>entry instanceof PerformanceEntry</c> resolves against
/// and the holder of every attribute a mark and a measure share.
/// </remarks>
internal sealed class PerformanceEntryConstructor : Constructor
{
    private static readonly JsString _functionName = new("PerformanceEntry");

    internal PerformanceEntryConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new PerformanceEntryPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal PerformanceEntryPrototype PrototypeObject { get; }

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
