#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.WebApi.Xhr;

/// <summary>
/// The shared base of <c>XMLHttpRequest</c> and <c>XMLHttpRequestUpload</c>: an <c>EventTarget</c> carrying
/// the seven progress-event handler attributes both of them declare.
/// <para>
/// https://xhr.spec.whatwg.org/#xmlhttprequesteventtarget
/// </para>
/// </summary>
/// <remarks>
/// The interface has no member of its own beyond those handlers, which is why this class adds no state: it
/// exists so that <c>XMLHttpRequestEventTarget.prototype</c> has something to brand-check against and so that
/// <c>xhr.upload instanceof XMLHttpRequestEventTarget</c> holds, both of which a script can observe.
/// </remarks>
internal abstract class JsXmlHttpRequestEventTarget : JsEventTarget
{
    private protected JsXmlHttpRequestEventTarget(Engine engine, Realm realm) : base(engine, realm)
    {
    }

    /// <summary>https://xhr.spec.whatwg.org/#event-xhr-loadstart.</summary>
    internal const string LoadStartEventType = "loadstart";

    /// <summary>https://xhr.spec.whatwg.org/#event-xhr-progress.</summary>
    internal const string ProgressEventType = "progress";

    /// <summary>https://xhr.spec.whatwg.org/#event-xhr-abort.</summary>
    internal const string AbortEventType = "abort";

    /// <summary>https://xhr.spec.whatwg.org/#event-xhr-error.</summary>
    internal const string ErrorEventType = "error";

    /// <summary>https://xhr.spec.whatwg.org/#event-xhr-load.</summary>
    internal const string LoadEventType = "load";

    /// <summary>https://xhr.spec.whatwg.org/#event-xhr-timeout.</summary>
    internal const string TimeoutEventType = "timeout";

    /// <summary>https://xhr.spec.whatwg.org/#event-xhr-loadend.</summary>
    internal const string LoadEndEventType = "loadend";

    /// <summary>
    /// Whether any of the eight event types this interface declares has a listener — the question
    /// https://xhr.spec.whatwg.org/#upload-listener-flag asks of the upload object at send() time.
    /// </summary>
    internal bool HasAnyListener
        => HasListenerOfType(LoadStartEventType)
        || HasListenerOfType(ProgressEventType)
        || HasListenerOfType(AbortEventType)
        || HasListenerOfType(ErrorEventType)
        || HasListenerOfType(LoadEventType)
        || HasListenerOfType(TimeoutEventType)
        || HasListenerOfType(LoadEndEventType);

    /// <summary>
    /// https://xhr.spec.whatwg.org/#concept-event-fire-progress — create a <c>ProgressEvent</c>, initialize
    /// its three members, and dispatch it. The event is trusted because the engine created it.
    /// </summary>
    internal void FireProgressEvent(JsString type, double transmitted, double length)
    {
        var ev = _realm.Intrinsics.ProgressEvent.CreateTrustedProgressEvent(
            type,
            lengthComputable: length != 0,
            loaded: transmitted,
            total: length);

        DispatchEvent(ev);
    }
}
#endif
