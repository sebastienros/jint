#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;
using Jint.WebApi.Url;

namespace Jint.WebApi.WebSockets;

/// <summary>
/// The <c>CloseEvent</c> interface object.
/// <para>
/// https://websockets.spec.whatwg.org/#the-closeevent-interface
/// </para>
/// </summary>
/// <remarks>
/// <c>CloseEvent</c> inherits from <c>Event</c>, so its <c>[[Prototype]]</c> is the <c>Event</c> interface
/// object — https://webidl.spec.whatwg.org/#interface-object.
/// </remarks>
internal sealed class CloseEventConstructor : Constructor
{
    private static readonly JsString _functionName = new("CloseEvent");
    private static readonly JsString _code = new("code");
    private static readonly JsString _reason = new("reason");
    private static readonly JsString _wasClean = new("wasClean");

    internal CloseEventConstructor(Engine engine, Realm realm, EventConstructor eventConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventConstructor;
        PrototypeObject = new CloseEventPrototype(engine, realm, this, eventConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal CloseEventPrototype PrototypeObject { get; }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#the-closeevent-interface — the ordinary event constructor over
    /// <c>CloseEventInit</c>, whose members default to <c>false</c>, <c>0</c> and <c>""</c>.
    /// </summary>
    /// <remarks>
    /// The inherited <c>EventInit</c> members are converted first and this dictionary's own in
    /// lexicographical order — <c>code</c>, <c>reason</c>, <c>wasClean</c> — which is what
    /// https://webidl.spec.whatwg.org/#es-dictionary asks for and is observable through getters on the
    /// dictionary.
    /// </remarks>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var type = EventConstructor.RequireType(_realm, arguments, "CloseEvent");
        var initArgument = arguments.At(1);

        var init = EventConstructor.ReadEventInit(_realm, initArgument, "CloseEvent");
        var dictionary = initArgument as ObjectInstance;

        var code = 0;
        if (Member(dictionary, _code) is { } codeValue)
        {
            code = WebSocketValues.ToUnsignedShort(codeValue);
        }

        var reason = Member(dictionary, _reason) is { } reasonValue ? UrlValues.ToUsvString(reasonValue) : string.Empty;
        var wasClean = Member(dictionary, _wasClean) is { } cleanValue && TypeConverter.ToBoolean(cleanValue);

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.CloseEvent.PrototypeObject,
            static (Engine engine, Realm _, (JsString Type, EventInit Init, double TimeStamp, bool WasClean, int Code, string Reason) state)
                => new JsCloseEvent(engine, state.Type, state.Init, state.TimeStamp, state.WasClean, state.Code, state.Reason),
            (Type: type, Init: init, TimeStamp: EventConstructor.TimeStampNow(_engine), WasClean: wasClean, Code: code, Reason: reason));
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-fire for the <c>close</c> event a connection's end produces:
    /// created by the engine, so <c>isTrusted</c> is true.
    /// </summary>
    internal JsCloseEvent CreateTrustedClose(JsString type, int code, string reason, bool wasClean)
    {
        return new JsCloseEvent(_engine, type, default, EventConstructor.TimeStampNow(_engine), wasClean, code, reason)
        {
            IsTrusted = true,
            _prototype = PrototypeObject,
        };
    }

    private static JsValue? Member(ObjectInstance? dictionary, JsString name)
    {
        var value = dictionary?.Get(name);
        return value is null || value.IsUndefined() ? null : value;
    }
}
#endif
