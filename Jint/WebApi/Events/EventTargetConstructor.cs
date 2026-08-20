#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Events;

/// <summary>
/// The <c>EventTarget</c> interface object.
/// <para>
/// https://dom.spec.whatwg.org/#interface-eventtarget
/// </para>
/// </summary>
/// <remarks>
/// <c>new EventTarget()</c> is specified to "do nothing", which is exactly what a constructible interface
/// object with no arguments amounts to: the instance starts with an empty event listener list. It declares no
/// static member, so it needs nothing from the source generator.
/// </remarks>
internal sealed class EventTargetConstructor : Constructor
{
    private static readonly JsString _functionName = new("EventTarget");

    internal EventTargetConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new EventTargetPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal EventTargetPrototype PrototypeObject { get; }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-eventtarget-eventtarget
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.EventTarget.PrototypeObject,
            static (Engine engine, Realm realm, object? _) => new JsEventTarget(engine, realm));
    }
}
#endif
