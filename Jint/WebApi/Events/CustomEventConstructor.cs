#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Events;

/// <summary>
/// The <c>CustomEvent</c> interface object.
/// <para>
/// https://dom.spec.whatwg.org/#interface-customevent
/// </para>
/// </summary>
/// <remarks>
/// <c>CustomEvent</c> inherits from <c>Event</c>, so its <c>[[Prototype]]</c> is the <c>Event</c> interface
/// object rather than <c>%Function.prototype%</c> — https://webidl.spec.whatwg.org/#interface-object — which
/// is what makes <c>Object.getPrototypeOf(CustomEvent) === Event</c> hold. It declares no static member of its
/// own, so unlike its prototype it needs nothing from the source generator.
/// </remarks>
internal sealed class CustomEventConstructor : Constructor
{
    private static readonly JsString _functionName = new("CustomEvent");

    internal CustomEventConstructor(Engine engine, Realm realm, EventConstructor eventConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventConstructor;
        PrototypeObject = new CustomEventPrototype(engine, realm, this, eventConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal CustomEventPrototype PrototypeObject { get; }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-customevent-customevent
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var type = EventConstructor.RequireType(_realm, arguments, "CustomEvent");
        var initArgument = arguments.At(1);

        // The inherited members are converted before the interface's own, which is the order
        // https://webidl.spec.whatwg.org/#es-dictionary puts an inherited dictionary's members in.
        var init = EventConstructor.ReadEventInit(_realm, initArgument, "CustomEvent");
        var detail = ReadDetail(initArgument);

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.CustomEvent.PrototypeObject,
            static (Engine engine, Realm _, (JsString Type, EventInit Init, double TimeStamp, JsValue Detail) state)
                => new JsCustomEvent(engine, state.Type, state.Init, state.TimeStamp, state.Detail),
            (Type: type, Init: init, TimeStamp: EventConstructor.TimeStampNow(_engine), Detail: detail));
    }

    /// <summary>
    /// The <c>detail</c> member of https://dom.spec.whatwg.org/#dictdef-customeventinit, whose IDL default is
    /// <c>null</c> — so an absent dictionary, an absent member and an explicit <c>undefined</c> all give
    /// <c>null</c>, which is what <c>new CustomEvent('x').detail</c> answers in a browser.
    /// </summary>
    private static JsValue ReadDetail(JsValue init)
    {
        if (init is not ObjectInstance dictionary)
        {
            return Null;
        }

        var detail = dictionary.Get(CommonEventProperties.Detail);
        return detail.IsUndefined() ? Null : detail;
    }
}
#endif
