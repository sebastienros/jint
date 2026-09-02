#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Performance;

/// <summary>
/// The <c>PerformanceObserverEntryList</c> interface object.
/// <para>
/// https://w3c.github.io/performance-timeline/#performanceobserverentrylist
/// </para>
/// </summary>
/// <remarks>
/// The interface declares no constructor operation, so the object is a function that refuses to construct —
/// https://webidl.spec.whatwg.org/#es-interface-call. It is exposed because
/// <c>entries instanceof PerformanceObserverEntryList</c> is what a callback's first argument is checked
/// with, and <c>performance-timeline/po-observe.any.js</c> checks it.
/// </remarks>
internal sealed class PerformanceObserverEntryListConstructor : Constructor
{
    private static readonly JsString _functionName = new("PerformanceObserverEntryList");

    internal PerformanceObserverEntryListConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new PerformanceObserverEntryListPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal PerformanceObserverEntryListPrototype PrototypeObject { get; }

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
