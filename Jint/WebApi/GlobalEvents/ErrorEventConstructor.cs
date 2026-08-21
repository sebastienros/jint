#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;
using Jint.WebApi.Url;

namespace Jint.WebApi.GlobalEvents;

/// <summary>
/// The <c>ErrorEvent</c> interface object.
/// <para>
/// https://html.spec.whatwg.org/multipage/webappapis.html#the-errorevent-interface
/// </para>
/// </summary>
/// <remarks>
/// <c>ErrorEvent</c> inherits from <c>Event</c>, so its <c>[[Prototype]]</c> is the <c>Event</c> interface
/// object — https://webidl.spec.whatwg.org/#interface-object.
/// </remarks>
internal sealed class ErrorEventConstructor : Constructor
{
    private static readonly JsString _functionName = new("ErrorEvent");
    private static readonly JsString _colno = new("colno");
    private static readonly JsString _error = new("error");
    private static readonly JsString _filename = new("filename");
    private static readonly JsString _lineno = new("lineno");
    private static readonly JsString _message = new("message");

    internal ErrorEventConstructor(Engine engine, Realm realm, EventConstructor eventConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventConstructor;
        PrototypeObject = new ErrorEventPrototype(engine, realm, this, eventConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal ErrorEventPrototype PrototypeObject { get; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#the-errorevent-interface — the ordinary event
    /// constructor over <c>ErrorEventInit</c>, whose members default to <c>""</c>, <c>""</c>, <c>0</c>,
    /// <c>0</c> and — <c>error</c> being an <c>any</c> with no default — <c>undefined</c>.
    /// </summary>
    /// <remarks>
    /// The inherited <c>EventInit</c> members are converted first and this dictionary's own in
    /// lexicographical order — <c>colno</c>, <c>error</c>, <c>filename</c>, <c>lineno</c>, <c>message</c> —
    /// which is what https://webidl.spec.whatwg.org/#es-dictionary asks for and is observable through getters
    /// on the dictionary.
    /// </remarks>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var type = EventConstructor.RequireType(_realm, arguments, "ErrorEvent");
        var initArgument = arguments.At(1);

        var init = EventConstructor.ReadEventInit(_realm, initArgument, "ErrorEvent");
        var dictionary = initArgument as ObjectInstance;

        var colno = Member(dictionary, _colno) is { } colnoValue ? TypeConverter.ToUint32(colnoValue) : 0u;
        var error = Member(dictionary, _error) ?? Undefined;
        var filename = Member(dictionary, _filename) is { } filenameValue ? UrlValues.ToUsvString(filenameValue) : string.Empty;
        var lineno = Member(dictionary, _lineno) is { } linenoValue ? TypeConverter.ToUint32(linenoValue) : 0u;
        var message = Member(dictionary, _message) is { } messageValue ? TypeConverter.ToString(messageValue) : string.Empty;

        var details = new ErrorEventDetails(message, filename, lineno, colno, error);

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.ErrorEvent.PrototypeObject,
            static (Engine engine, Realm _, (JsString Type, EventInit Init, double TimeStamp, ErrorEventDetails Details) state)
                => new JsErrorEvent(engine, state.Type, state.Init, state.TimeStamp, in state.Details),
            (Type: type, Init: init, TimeStamp: EventConstructor.TimeStampNow(_engine), Details: details));
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-fire for the <c>error</c> event HTML's <i>report an
    /// exception</i> fires at the global scope: created by the engine, so <c>isTrusted</c> is true, and
    /// cancelable because step 5 initializes it that way — a listener calling <c>preventDefault()</c> is what
    /// makes <i>notHandled</i> false.
    /// </summary>
    internal JsErrorEvent CreateTrustedError(JsString type, in ErrorEventDetails details)
    {
        return new JsErrorEvent(_engine, type, new EventInit(Bubbles: false, Cancelable: true, Composed: false), EventConstructor.TimeStampNow(_engine), in details)
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
