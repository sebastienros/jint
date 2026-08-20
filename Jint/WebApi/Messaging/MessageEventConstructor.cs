#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.Messaging;

/// <summary>
/// The <c>MessageEvent</c> interface object.
/// <para>
/// https://html.spec.whatwg.org/multipage/comms.html#messageevent
/// </para>
/// </summary>
/// <remarks>
/// <c>MessageEvent</c> inherits from <c>Event</c>, so its <c>[[Prototype]]</c> is the <c>Event</c> interface
/// object — https://webidl.spec.whatwg.org/#interface-object — which is what makes
/// <c>Object.getPrototypeOf(MessageEvent) === Event</c> hold. It declares no static member of its own, so it
/// needs nothing from the source generator.
/// </remarks>
internal sealed class MessageEventConstructor : Constructor
{
    private static readonly JsString _functionName = new("MessageEvent");
    private static readonly JsString _data = new("data");
    private static readonly JsString _origin = new("origin");
    private static readonly JsString _lastEventId = new("lastEventId");
    private static readonly JsString _source = new("source");
    private static readonly JsString _ports = new("ports");

    private JsArray? _emptyPorts;

    internal MessageEventConstructor(Engine engine, Realm realm, EventConstructor eventConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventConstructor;
        PrototypeObject = new MessageEventPrototype(engine, realm, this, eventConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal MessageEventPrototype PrototypeObject { get; }

    /// <summary>
    /// The frozen empty array every port-delivered event's <c>ports</c> is. One per realm rather than one per
    /// event: it is frozen and empty, so no script can tell two of them apart from one shared one.
    /// </summary>
    private JsArray EmptyPorts => _emptyPorts ??= FreezePorts([]);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/comms.html#dom-messageevent-messageevent
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var type = EventConstructor.RequireType(_realm, arguments, "MessageEvent");
        var initArgument = arguments.At(1);

        // The inherited members are converted before the interface's own, which is the order
        // https://webidl.spec.whatwg.org/#es-dictionary puts an inherited dictionary's members in.
        var init = EventConstructor.ReadEventInit(_realm, initArgument, "MessageEvent");
        var members = ReadMessageEventInit(initArgument);

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.MessageEvent.PrototypeObject,
            static (Engine engine, Realm _, (JsString Type, EventInit Init, double TimeStamp, MessageEventInit Members) state)
                => new JsMessageEvent(
                    engine,
                    state.Type,
                    state.Init,
                    state.TimeStamp,
                    state.Members.Data,
                    state.Members.Origin,
                    state.Members.LastEventId,
                    state.Members.Source,
                    state.Members.Ports),
            (Type: type, Init: init, TimeStamp: EventConstructor.TimeStampNow(_engine), Members: members));
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-fire for a <c>MessageEvent</c>: the event the engine creates
    /// for itself when a port delivers, whose <c>isTrusted</c> is therefore true. Everything but <c>data</c>
    /// takes its IDL default, because the message port post message steps set nothing else.
    /// </summary>
    internal JsMessageEvent CreateTrustedMessageEvent(JsString type, JsValue data)
        => CreateTrustedMessageEvent(type, data, JsString.Empty, JsString.Empty);

    /// <summary>
    /// <see cref="CreateTrustedMessageEvent(JsString, JsValue)"/> with the two members <c>EventSource</c>'s
    /// dispatch steps additionally set — the stream's origin and the last event ID,
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#dispatchMessage. <c>source</c> and
    /// <c>ports</c> keep their IDL defaults there too: both name a <c>MessagePort</c>, and an event source
    /// has none to hand over.
    /// </summary>
    internal JsMessageEvent CreateTrustedMessageEvent(JsString type, JsValue data, JsString origin, JsString lastEventId)
    {
        return new JsMessageEvent(
            _engine,
            type,
            default,
            EventConstructor.TimeStampNow(_engine),
            data,
            origin,
            lastEventId,
            Null,
            EmptyPorts)
        {
            IsTrusted = true,
            _prototype = PrototypeObject,
        };
    }

    /// <summary>
    /// The members https://html.spec.whatwg.org/multipage/comms.html#messageeventinit adds to
    /// <c>EventInit</c>, read in declaration order.
    /// </summary>
    private MessageEventInit ReadMessageEventInit(JsValue init)
    {
        if (init is not ObjectInstance dictionary)
        {
            return new MessageEventInit(Null, JsString.Empty, JsString.Empty, Null, EmptyPorts);
        }

        // `any data = null`: an absent member and an explicit undefined both give null, which is what
        // `new MessageEvent('x').data` answers in a browser.
        var data = dictionary.Get(_data);
        if (data.IsUndefined())
        {
            data = Null;
        }

        var origin = ReadString(dictionary, _origin);
        var lastEventId = ReadString(dictionary, _lastEventId);

        // `MessageEventSource? source = null`, and the only member of that union Jint has is MessagePort.
        var sourceValue = dictionary.Get(_source);
        var source = Null;
        if (!sourceValue.IsUndefined() && !sourceValue.IsNull())
        {
            if (sourceValue is not JsMessagePort)
            {
                Throw.TypeError(_realm, "Failed to construct 'MessageEvent': member source is not of type 'MessagePort'.");
            }

            source = sourceValue;
        }

        return new MessageEventInit(data, origin, lastEventId, source, ReadPorts(dictionary));
    }

    private static JsString ReadString(ObjectInstance dictionary, JsString member)
    {
        var value = dictionary.Get(member);
        return value.IsUndefined() ? JsString.Empty : TypeConverter.ToJsString(value);
    }

    /// <summary>
    /// The <c>sequence&lt;MessagePort&gt; ports = []</c> member. Nothing in this engine can transfer a port,
    /// so this is the only way a <c>MessageEvent</c> ever carries one — and it carries whatever the script
    /// put there, which is what a script constructing its own event for <c>dispatchEvent</c> expects.
    /// </summary>
    private JsArray ReadPorts(ObjectInstance dictionary)
    {
        var value = dictionary.Get(_ports);
        if (value.IsUndefined())
        {
            return EmptyPorts;
        }

        var iterator = value.GetIterator(_realm);
        var ports = new List<JsValue>();
        while (iterator.TryIteratorStepValue(out var item))
        {
            if (item is not JsMessagePort)
            {
                iterator.Close(CompletionType.Throw);
                Throw.TypeError(_realm, "Failed to construct 'MessageEvent': member ports is not of type 'MessagePort'.");
            }

            ports.Add(item);
        }

        return ports.Count == 0 ? EmptyPorts : FreezePorts(ports.ToArray());
    }

    /// <summary>
    /// A <c>FrozenArray&lt;MessagePort&gt;</c>: an ordinary array whose integrity level is frozen, which is
    /// what https://webidl.spec.whatwg.org/#es-frozen-array creates.
    /// </summary>
    private JsArray FreezePorts(JsValue[] ports)
    {
        var array = _realm.Intrinsics.Array.ConstructFast(ports);
        array.SetIntegrityLevel(ObjectInstance.IntegrityLevel.Frozen);
        return array;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/comms.html#messageeventinit, after conversion.
    /// </summary>
    private readonly record struct MessageEventInit(
        JsValue Data,
        JsString Origin,
        JsString LastEventId,
        JsValue Source,
        JsArray Ports);
}
#endif
