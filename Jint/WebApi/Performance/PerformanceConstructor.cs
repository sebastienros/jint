#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

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
/// <b>It does not inherit from <c>EventTarget</c>.</b> https://w3c.github.io/hr-time/#sec-performance declares
/// <c>interface Performance : EventTarget</c>, and this interface object's <c>[[Prototype]]</c> is
/// <c>%Function.prototype%</c> rather than the <c>EventTarget</c> interface object, with
/// <c>Performance.prototype</c> inheriting straight from <c>%Object.prototype%</c>. Claiming the inheritance
/// would be claiming the members — <c>addEventListener</c>, <c>dispatchEvent</c> — of an interface nothing here
/// fires an event at, which is the half-truth this whole exposure decision exists to remove. The absence is
/// the same one <c>PerformancePrototype</c> records for <c>PerformanceObserver</c>, and for the same reason: a
/// script's feature detection should see what is actually there.
/// </para>
/// </remarks>
internal sealed class PerformanceConstructor : Constructor
{
    private static readonly JsString _functionName = new("Performance");

    internal PerformanceConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new PerformancePrototype(engine, realm, this, objectPrototype);
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
