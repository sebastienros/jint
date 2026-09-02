#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.Xhr;

/// <summary>
/// <c>XMLHttpRequestEventTarget.prototype</c> — the interface prototype object.
/// <para>
/// https://xhr.spec.whatwg.org/#xmlhttprequesteventtarget
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>EventTarget.prototype</c>, so an <c>XMLHttpRequest</c> and its upload object
/// both carry <c>addEventListener</c>. The seven members are all event handler IDL attributes and are declared
/// here rather than twice below, which is the whole reason the interface exists.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class XmlHttpRequestEventTargetPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly XmlHttpRequestEventTargetConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString XmlHttpRequestEventTargetToStringTag = new("XMLHttpRequestEventTarget");

    internal XmlHttpRequestEventTargetPrototype(
        Engine engine,
        Realm realm,
        XmlHttpRequestEventTargetConstructor constructor,
        ObjectInstance eventTargetPrototype) : base(engine, realm)
    {
        _prototype = eventTargetPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>https://xhr.spec.whatwg.org/#handler-xhr-onloadstart.</summary>
    [JsAccessor("onloadstart", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnLoadStartGet(JsValue thisObject)
        => EventHandlerAttributes.Get(Brand(thisObject), JsXmlHttpRequestEventTarget.LoadStartEventType);

    /// <summary>https://xhr.spec.whatwg.org/#handler-xhr-onloadstart, setter half.</summary>
    [JsAccessor("onloadstart", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnLoadStartSet(JsValue thisObject, JsValue value)
        => EventHandlerAttributes.Set(Brand(thisObject), JsXmlHttpRequestEventTarget.LoadStartEventType, value);

    /// <summary>https://xhr.spec.whatwg.org/#handler-xhr-onprogress.</summary>
    [JsAccessor("onprogress", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnProgressGet(JsValue thisObject)
        => EventHandlerAttributes.Get(Brand(thisObject), JsXmlHttpRequestEventTarget.ProgressEventType);

    /// <summary>https://xhr.spec.whatwg.org/#handler-xhr-onprogress, setter half.</summary>
    [JsAccessor("onprogress", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnProgressSet(JsValue thisObject, JsValue value)
        => EventHandlerAttributes.Set(Brand(thisObject), JsXmlHttpRequestEventTarget.ProgressEventType, value);

    /// <summary>https://xhr.spec.whatwg.org/#handler-xhr-onabort.</summary>
    [JsAccessor("onabort", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnAbortGet(JsValue thisObject)
        => EventHandlerAttributes.Get(Brand(thisObject), JsXmlHttpRequestEventTarget.AbortEventType);

    /// <summary>https://xhr.spec.whatwg.org/#handler-xhr-onabort, setter half.</summary>
    [JsAccessor("onabort", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnAbortSet(JsValue thisObject, JsValue value)
        => EventHandlerAttributes.Set(Brand(thisObject), JsXmlHttpRequestEventTarget.AbortEventType, value);

    /// <summary>https://xhr.spec.whatwg.org/#handler-xhr-onerror.</summary>
    [JsAccessor("onerror", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnErrorGet(JsValue thisObject)
        => EventHandlerAttributes.Get(Brand(thisObject), JsXmlHttpRequestEventTarget.ErrorEventType);

    /// <summary>https://xhr.spec.whatwg.org/#handler-xhr-onerror, setter half.</summary>
    [JsAccessor("onerror", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnErrorSet(JsValue thisObject, JsValue value)
        => EventHandlerAttributes.Set(Brand(thisObject), JsXmlHttpRequestEventTarget.ErrorEventType, value);

    /// <summary>https://xhr.spec.whatwg.org/#handler-xhr-onload.</summary>
    [JsAccessor("onload", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnLoadGet(JsValue thisObject)
        => EventHandlerAttributes.Get(Brand(thisObject), JsXmlHttpRequestEventTarget.LoadEventType);

    /// <summary>https://xhr.spec.whatwg.org/#handler-xhr-onload, setter half.</summary>
    [JsAccessor("onload", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnLoadSet(JsValue thisObject, JsValue value)
        => EventHandlerAttributes.Set(Brand(thisObject), JsXmlHttpRequestEventTarget.LoadEventType, value);

    /// <summary>https://xhr.spec.whatwg.org/#handler-xhr-ontimeout.</summary>
    [JsAccessor("ontimeout", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnTimeoutGet(JsValue thisObject)
        => EventHandlerAttributes.Get(Brand(thisObject), JsXmlHttpRequestEventTarget.TimeoutEventType);

    /// <summary>https://xhr.spec.whatwg.org/#handler-xhr-ontimeout, setter half.</summary>
    [JsAccessor("ontimeout", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnTimeoutSet(JsValue thisObject, JsValue value)
        => EventHandlerAttributes.Set(Brand(thisObject), JsXmlHttpRequestEventTarget.TimeoutEventType, value);

    /// <summary>https://xhr.spec.whatwg.org/#handler-xhr-onloadend.</summary>
    [JsAccessor("onloadend", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnLoadEndGet(JsValue thisObject)
        => EventHandlerAttributes.Get(Brand(thisObject), JsXmlHttpRequestEventTarget.LoadEndEventType);

    /// <summary>https://xhr.spec.whatwg.org/#handler-xhr-onloadend, setter half.</summary>
    [JsAccessor("onloadend", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnLoadEndSet(JsValue thisObject, JsValue value)
        => EventHandlerAttributes.Set(Brand(thisObject), JsXmlHttpRequestEventTarget.LoadEndEventType, value);

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is neither an <c>XMLHttpRequest</c> nor
    /// an <c>XMLHttpRequestUpload</c> raises a <c>TypeError</c>.
    /// </summary>
    private JsXmlHttpRequestEventTarget Brand(JsValue thisObject)
    {
        if (thisObject is JsXmlHttpRequestEventTarget target)
        {
            return target;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not an XMLHttpRequestEventTarget");
        return null!;
    }
}
#endif
