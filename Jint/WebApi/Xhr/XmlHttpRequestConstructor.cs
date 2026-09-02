#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Xhr;

/// <summary>
/// The <c>XMLHttpRequest</c> interface object.
/// <para>
/// https://xhr.spec.whatwg.org/#interface-xmlhttprequest
/// </para>
/// </summary>
/// <remarks>
/// <c>XMLHttpRequest</c> inherits from <c>XMLHttpRequestEventTarget</c>, so its <c>[[Prototype]]</c> is that
/// interface object and <c>xhr instanceof EventTarget</c> holds two links further up. The five readyState
/// constants appear here as well as on the prototype, per https://webidl.spec.whatwg.org/#es-constants, with
/// the attributes constants are given there: <c>{ writable: false, enumerable: true, configurable: false }</c>.
/// That section defines them one after another in the order the IDL declares them, and that order is
/// observable, so <c>PreserveDeclarationOrder</c> keeps the generator from sorting them by name.
/// </remarks>
[JsObject(UseShape = true, PreserveDeclarationOrder = true)]
internal sealed partial class XmlHttpRequestConstructor : Constructor
{
    private static readonly JsString _functionName = new("XMLHttpRequest");

    [JsProperty(Name = "UNSENT", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Unsent = JsNumber.Create(JsXmlHttpRequest.Unsent);
    [JsProperty(Name = "OPENED", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Opened = JsNumber.Create(JsXmlHttpRequest.Opened);
    [JsProperty(Name = "HEADERS_RECEIVED", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber HeadersReceived = JsNumber.Create(JsXmlHttpRequest.HeadersReceived);
    [JsProperty(Name = "LOADING", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Loading = JsNumber.Create(JsXmlHttpRequest.Loading);
    [JsProperty(Name = "DONE", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Done = JsNumber.Create(JsXmlHttpRequest.Done);

    internal XmlHttpRequestConstructor(
        Engine engine,
        Realm realm,
        XmlHttpRequestEventTargetConstructor eventTargetConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventTargetConstructor;
        PrototypeObject = new XmlHttpRequestPrototype(engine, realm, this, eventTargetConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal XmlHttpRequestPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-constructor — "construct a new XMLHttpRequest object",
    /// whose whole content is the object's initial state.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var state = _engine._webApi;
        if (state?.FetchOptions is null)
        {
            // Unreachable: the global that reaches this is installed only where the state was created, in the
            // same block of WebApiRegistration.
            Throw.InvalidOperationException("The XMLHttpRequest global was reached on an engine that has no fetch configuration.");
        }

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.XmlHttpRequest.PrototypeObject,
            static (Engine engine, Realm realm, WebApiEngineState? created) => new JsXmlHttpRequest(engine, realm, created!),
            state);
    }
}
#endif
