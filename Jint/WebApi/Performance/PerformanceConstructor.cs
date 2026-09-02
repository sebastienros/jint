#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.Performance;

/// <summary>
/// The <c>Performance</c> interface object.
/// <para>
/// https://w3c.github.io/hr-time/#sec-performance
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The interface declares no constructor operation, so the interface object exists and is a function but
/// refuses to construct anything — https://webidl.spec.whatwg.org/#es-interface-call. The one instance is the
/// <c>performance</c> global, which is what <c>performance instanceof Performance</c> is written against, and
/// WinterTC's Minimum Common API §5.1 lists this interface as one a non-browser runtime should carry.
/// </para>
/// <para>
/// <b>It inherits from <c>EventTarget</c></b>, as https://w3c.github.io/hr-time/#sec-performance declares, so
/// this interface object's <c>[[Prototype]]</c> is the <c>EventTarget</c> interface object and
/// <c>Performance.prototype</c> inherits from <c>EventTarget.prototype</c>. Jint declined that while nothing
/// could fire an event at the object; what changed is not that something now does — the interface's whole
/// event surface is <c>resourcetimingbufferfull</c>, and there is no resource timing buffer here — but that
/// <c>PerformanceObserver</c> made the timeline something a script listens to, and half an <c>EventTarget</c>
/// is the half-truth this exposure decision exists to remove. The feature closure brings
/// <see cref="WebApiFeatures.Events"/> with <see cref="WebApiFeatures.Performance"/> for the same reason.
/// </para>
/// </remarks>
internal sealed class PerformanceConstructor : Constructor
{
    private static readonly JsString _functionName = new("Performance");

    internal PerformanceConstructor(Engine engine, Realm realm, EventTargetConstructor eventTargetConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventTargetConstructor;
        PrototypeObject = new PerformancePrototype(engine, realm, this, eventTargetConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal PerformancePrototype PrototypeObject { get; }

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
