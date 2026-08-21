#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;
using Jint.WebApi.Fetch;

namespace Jint.WebApi.FetchEvents;

/// <summary>
/// The <c>FetchEvent</c> interface object.
/// <para>
/// https://w3c.github.io/ServiceWorker/#fetchevent-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Its <c>[[Prototype]]</c> is the <c>Event</c> interface object —
/// https://webidl.spec.whatwg.org/#interface-object — where the IDL says <c>ExtendableEvent</c>. That
/// interface is deliberately not materialized; <see cref="JsFetchEvent"/> says why.
/// </para>
/// <para>
/// <c>length</c> is 2, not 1: <c>constructor(DOMString type, FetchEventInit eventInitDict)</c> declares the
/// dictionary <b>without</b> <c>optional</c>, because it has a <c>required</c> member. That is the same shape
/// <c>PromiseRejectionEvent</c> has, and for the same reason.
/// </para>
/// </remarks>
internal sealed class FetchEventConstructor : Constructor
{
    private static readonly JsString _functionName = new("FetchEvent");
    private static readonly JsString _request = new("request");

    internal FetchEventConstructor(Engine engine, Realm realm, EventConstructor eventConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventConstructor;
        PrototypeObject = new FetchEventPrototype(engine, realm, this, eventConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.Create(2), PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal FetchEventPrototype PrototypeObject { get; }

    /// <summary>
    /// The ordinary event constructor over <c>FetchEventInit</c>,
    /// https://w3c.github.io/ServiceWorker/#dictdef-fetcheventinit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The inherited <c>EventInit</c> members (through the empty <c>ExtendableEventInit</c>) are converted
    /// first and this dictionary's own after them, per https://webidl.spec.whatwg.org/#es-dictionary — an
    /// order that is observable through getters on the dictionary. Only <c>request</c> is read: the other five
    /// members describe navigation preload and service worker clients, neither of which exists here, and a
    /// member that is read and then ignored is worse than one that is not read at all.
    /// </para>
    /// <para>
    /// <c>required Request request</c>, so an absent member — and an explicit <c>undefined</c>, which WebIDL
    /// treats as absent — is a <c>TypeError</c>, and so is anything present that is not a <c>Request</c>: an
    /// interface type performs no coercion (https://webidl.spec.whatwg.org/#es-interface).
    /// </para>
    /// <para>
    /// A <c>FetchEvent</c> a script constructs is <b>not</b> trusted, and an untrusted one refuses both
    /// <c>respondWith()</c> and <c>waitUntil()</c> — see <see cref="JsFetchEvent"/>. It is still worth being
    /// constructible: it is what the interface's own IDL says, it is what makes the interface object more than
    /// a brand, and a script can dispatch one at an <c>EventTarget</c> of its own to exercise a listener.
    /// </para>
    /// </remarks>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var type = EventConstructor.RequireType(_realm, arguments, "FetchEvent");

        if (arguments.Length < 2)
        {
            Throw.TypeError(_realm, "Failed to construct 'FetchEvent': 2 arguments required, but only 1 present.");
        }

        var initArgument = arguments[1];

        // The inherited members first, which is also what refuses anything that is neither an object nor
        // undefined nor null.
        var init = EventConstructor.ReadEventInit(_realm, initArgument, "FetchEvent");

        // So what is left here is undefined or null, both of which convert to a dictionary with no members at
        // all — and a dictionary with no members is missing a required one.
        if (initArgument is not ObjectInstance dictionary)
        {
            Throw.TypeError(_realm, "Failed to construct 'FetchEvent': required member request is undefined.");
            return null!;
        }

        var requestValue = dictionary.Get(_request);
        if (requestValue.IsUndefined())
        {
            Throw.TypeError(_realm, "Failed to construct 'FetchEvent': required member request is undefined.");
        }

        if (requestValue is not JsRequest request)
        {
            Throw.TypeError(_realm, "Failed to construct 'FetchEvent': member request is not of type 'Request'.");
            return null!;
        }

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.FetchEvent.PrototypeObject,
            static (Engine engine, Realm realm, (JsString Type, EventInit Init, double TimeStamp, JsRequest Request) state)
                => new JsFetchEvent(engine, realm, state.Type, state.Init, state.TimeStamp, state.Request),
            (Type: type, Init: init, TimeStamp: EventConstructor.TimeStampNow(_engine), Request: request));
    }

    /// <summary>
    /// The trusted <c>fetch</c> event the engine dispatches for an inbound request, which is
    /// https://w3c.github.io/ServiceWorker/#on-fetch-request-algorithm reduced to the three initializations
    /// that mean anything here: the type, <c>cancelable</c> — "Initialize e's cancelable attribute to true" —
    /// and the request.
    /// </summary>
    /// <remarks>
    /// It is created by the engine rather than by a script, so <c>isTrusted</c> is true, and that is precisely
    /// what lets <c>respondWith()</c> and <c>waitUntil()</c> work on it at all.
    /// </remarks>
    internal JsFetchEvent CreateTrustedFetchEvent(JsRequest request)
    {
        var init = new EventInit(Bubbles: false, Cancelable: true, Composed: false);
        return new JsFetchEvent(_engine, _realm, FetchEventNames.Fetch, init, EventConstructor.TimeStampNow(_engine), request)
        {
            IsTrusted = true,
            _prototype = PrototypeObject,
        };
    }
}
#endif
