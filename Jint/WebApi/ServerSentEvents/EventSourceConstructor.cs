#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;
using Jint.WebApi.Url;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.ServerSentEvents;

/// <summary>
/// The <c>EventSource</c> interface object.
/// <para>
/// https://html.spec.whatwg.org/multipage/server-sent-events.html#the-eventsource-interface
/// </para>
/// </summary>
/// <remarks>
/// <c>EventSource</c> inherits from <c>EventTarget</c>, so its <c>[[Prototype]]</c> is the <c>EventTarget</c>
/// interface object and <c>source instanceof EventTarget</c> holds. The three readyState constants appear
/// here as well as on the prototype, per https://webidl.spec.whatwg.org/#es-constants, with the attributes
/// constants are given there: <c>{ writable: false, enumerable: true, configurable: false }</c>.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class EventSourceConstructor : Constructor
{
    private static readonly JsString _functionName = new("EventSource");
    private static readonly JsString _withCredentials = new("withCredentials");

    [JsProperty(Name = "CONNECTING", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Connecting = JsNumber.Create(JsEventSource.Connecting);
    [JsProperty(Name = "OPEN", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Open = JsNumber.Create(JsEventSource.Open);
    [JsProperty(Name = "CLOSED", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Closed = JsNumber.Create(JsEventSource.Closed);

    internal EventSourceConstructor(Engine engine, Realm realm, EventTargetConstructor eventTargetConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventTargetConstructor;
        PrototypeObject = new EventSourcePrototype(engine, realm, this, eventTargetConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal EventSourcePrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#dom-eventsource — the whole of the
    /// constructor, ending in step 15, "fetch request", and step 16, "return ev".
    /// </summary>
    /// <remarks>
    /// The steps a browser needs and this engine has no counterpart for are the ones about the environment
    /// the object lives in: there is no settings object to parse the URL relative to (so the URL must be
    /// absolute), no CORS attribute state to select (see <c>withCredentials</c>) and no client to attribute
    /// the request to.
    /// </remarks>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var state = _engine._webApi;
        if (state?.FetchOptions is null)
        {
            // Unreachable: the global that reaches this is installed only where the state was created, in the
            // same block of WebApiRegistration.
            Throw.InvalidOperationException("The EventSource global was reached on an engine that has no fetch configuration.");
        }

        if (arguments.Length == 0)
        {
            Throw.TypeError(_realm, "Failed to construct 'EventSource': 1 argument required, but only 0 present.");
        }

        // The arguments are converted in order — the USVString first, then the dictionary — and only then do
        // the constructor steps run, which is why an unparsable URL is diagnosed after the dictionary is read.
        var href = UrlValues.ToUsvString(arguments[0]);
        var withCredentials = ReadWithCredentials(arguments.At(1));

        // Step 3: "let urlRecord be the result of encoding-parsing a URL given url, relative to settings", and
        // step 4: "if urlRecord is failure, then throw a SyntaxError DOMException" — a DOMException, not the
        // TypeError the fetch interfaces raise for the same mistake.
        var url = UrlParser.Parse(href);
        if (url is null)
        {
            ThrowDomException(DomExceptionNames.Syntax, $"Failed to construct 'EventSource': Cannot open an EventSource to '{href}'. The URL is invalid.");
        }

        var source = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.EventSource.PrototypeObject,
            static (Engine engine, Realm realm, (WebApiEngineState State, UrlRecord Url, bool WithCredentials) created)
                => new JsEventSource(engine, realm, created.State, created.Url, created.WithCredentials),
            (State: state, Url: url!, WithCredentials: withCredentials));

        source.Connect();
        return source;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#dictdef-eventsourceinit converted per
    /// https://webidl.spec.whatwg.org/#es-dictionary: <see langword="undefined"/> and <see langword="null"/>
    /// give the member its default, and anything that is not an object is a <c>TypeError</c>.
    /// </summary>
    private bool ReadWithCredentials(JsValue init)
    {
        if (init.IsUndefined() || init.IsNull())
        {
            return false;
        }

        if (init is not ObjectInstance dictionary)
        {
            Throw.TypeError(_realm, "Failed to construct 'EventSource': the provided value is not of type 'EventSourceInit'.");
            return false;
        }

        return TypeConverter.ToBoolean(dictionary.Get(_withCredentials));
    }

    private void ThrowDomException(string name, string message)
    {
        var exception = _realm.Intrinsics.DomException.CreateException(name, message);
        var location = _engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(_engine, exception, in location);
    }
}
#endif
