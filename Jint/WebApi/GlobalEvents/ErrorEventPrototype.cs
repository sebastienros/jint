#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.GlobalEvents;

/// <summary>
/// <c>ErrorEvent.prototype</c> — the interface prototype object.
/// <para>
/// https://html.spec.whatwg.org/multipage/webappapis.html#the-errorevent-interface
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>Event.prototype</c>, so an <c>error</c> event has every <c>Event</c> member
/// and <c>event instanceof Event</c> holds inside the listener.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class ErrorEventPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly ErrorEventConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString ErrorEventToStringTag = new("ErrorEvent");

    internal ErrorEventPrototype(
        Engine engine,
        Realm realm,
        ErrorEventConstructor constructor,
        ObjectInstance eventPrototype) : base(engine, realm)
    {
        _prototype = eventPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#dom-errorevent-message
    /// </summary>
    [JsAccessor("message", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString MessageGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Message);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#dom-errorevent-filename
    /// </summary>
    [JsAccessor("filename", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString FilenameGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Filename);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#dom-errorevent-lineno
    /// </summary>
    [JsAccessor("lineno", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber LinenoGet(JsValue thisObject) => JsNumber.Create((long) Brand(thisObject).Lineno);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#dom-errorevent-colno
    /// </summary>
    [JsAccessor("colno", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber ColnoGet(JsValue thisObject) => JsNumber.Create((long) Brand(thisObject).Colno);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#dom-errorevent-error
    /// </summary>
    [JsAccessor("error", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue ErrorGet(JsValue thisObject) => Brand(thisObject).Error;

    private JsErrorEvent Brand(JsValue thisObject)
    {
        if (thisObject is JsErrorEvent errorEvent)
        {
            return errorEvent;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not an ErrorEvent");
        return null!;
    }
}
#endif
