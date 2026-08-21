#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.GlobalEvents;

/// <summary>
/// The <c>PromiseRejectionEvent</c> interface object.
/// <para>
/// https://html.spec.whatwg.org/multipage/webappapis.html#the-promiserejectionevent-interface
/// </para>
/// </summary>
/// <remarks>
/// <c>PromiseRejectionEvent</c> inherits from <c>Event</c>, so its <c>[[Prototype]]</c> is the <c>Event</c>
/// interface object — https://webidl.spec.whatwg.org/#interface-object. Unlike every other event interface
/// here its <c>eventInitDict</c> argument is <b>not</b> optional, because the dictionary has a
/// <c>required</c> member; that is why <c>length</c> is 2 rather than 1.
/// </remarks>
internal sealed class PromiseRejectionEventConstructor : Constructor
{
    private static readonly JsString _functionName = new("PromiseRejectionEvent");
    private static readonly JsString _promise = new("promise");
    private static readonly JsString _reason = new("reason");

    internal PromiseRejectionEventConstructor(Engine engine, Realm realm, EventConstructor eventConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventConstructor;
        PrototypeObject = new PromiseRejectionEventPrototype(engine, realm, this, eventConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.Create(2), PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal PromiseRejectionEventPrototype PrototypeObject { get; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#the-promiserejectionevent-interface — the
    /// ordinary event constructor over <c>PromiseRejectionEventInit</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The inherited <c>EventInit</c> members are converted first and this dictionary's own in
    /// lexicographical order — <c>promise</c>, then <c>reason</c> —
    /// https://webidl.spec.whatwg.org/#es-dictionary.
    /// </para>
    /// <para>
    /// <c>promise</c> is a <c>required</c> member, so an absent one (and an explicit <c>undefined</c>, which
    /// WebIDL treats as absent) is a <c>TypeError</c>. Its declared type is <c>Promise&lt;any&gt;</c>, whose
    /// conversion is https://webidl.spec.whatwg.org/#es-promise: <i>any</i> value becomes a promise, so
    /// <c>new PromiseRejectionEvent('x', { promise: 42 })</c> has an already-resolved promise of 42 rather
    /// than the number.
    /// </para>
    /// </remarks>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var type = EventConstructor.RequireType(_realm, arguments, "PromiseRejectionEvent");

        if (arguments.Length < 2)
        {
            Throw.TypeError(_realm, "Failed to construct 'PromiseRejectionEvent': 2 arguments required, but only 1 present.");
        }

        var initArgument = arguments[1];

        // The inherited members first, which is also what refuses anything that is neither an object nor
        // undefined nor null.
        var init = EventConstructor.ReadEventInit(_realm, initArgument, "PromiseRejectionEvent");

        // So what is left here is undefined or null, both of which convert to a dictionary with no members at
        // all — and a dictionary with no members is missing a required one.
        if (initArgument is not ObjectInstance dictionary)
        {
            Throw.TypeError(_realm, "Failed to construct 'PromiseRejectionEvent': required member promise is undefined.");
            return null!;
        }

        // `promise` before `reason`, and its required-member check and its Promise<any> conversion both before
        // `reason` is even read — the order is observable through getters on the dictionary.
        var promiseValue = dictionary.Get(_promise);
        if (promiseValue.IsUndefined())
        {
            Throw.TypeError(_realm, "Failed to construct 'PromiseRejectionEvent': required member promise is undefined.");
        }

        var promise = _realm.Intrinsics.Promise.PromiseResolve(promiseValue);
        var reasonValue = dictionary.Get(_reason);

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.PromiseRejectionEvent.PrototypeObject,
            static (Engine engine, Realm _, (JsString Type, EventInit Init, double TimeStamp, JsValue Promise, JsValue Reason) state)
                => new JsPromiseRejectionEvent(engine, state.Type, state.Init, state.TimeStamp, state.Promise, state.Reason),
            (Type: type, Init: init, TimeStamp: EventConstructor.TimeStampNow(_engine), Promise: promise, Reason: reasonValue));
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-fire for the two events HTML's <i>unhandled promise
    /// rejections</i> section fires: created by the engine, so <c>isTrusted</c> is true, and carrying the
    /// promise object itself rather than a conversion of it.
    /// </summary>
    /// <param name="type"><c>unhandledrejection</c> or <c>rejectionhandled</c>.</param>
    /// <param name="promise">The promise the tracker reported.</param>
    /// <param name="reason">Its rejection reason.</param>
    /// <param name="cancelable">
    /// True for <c>unhandledrejection</c>, whose <i>notHandled</i> a listener may cancel; false for
    /// <c>rejectionhandled</c>, which HTML fires with no cancelable initializer at all.
    /// </param>
    internal JsPromiseRejectionEvent CreateTrustedRejection(JsString type, JsValue promise, JsValue reason, bool cancelable)
    {
        var init = new EventInit(Bubbles: false, Cancelable: cancelable, Composed: false);
        return new JsPromiseRejectionEvent(_engine, type, init, EventConstructor.TimeStampNow(_engine), promise, reason)
        {
            IsTrusted = true,
            _prototype = PrototypeObject,
        };
    }
}
#endif
