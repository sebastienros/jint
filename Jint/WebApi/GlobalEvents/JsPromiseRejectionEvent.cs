#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.WebApi.Events;

namespace Jint.WebApi.GlobalEvents;

/// <summary>
/// A <c>PromiseRejectionEvent</c> instance: the event behind <c>unhandledrejection</c> and
/// <c>rejectionhandled</c>.
/// <para>
/// https://html.spec.whatwg.org/multipage/webappapis.html#the-promiserejectionevent-interface
/// </para>
/// </summary>
internal sealed class JsPromiseRejectionEvent : JsEvent
{
    internal JsPromiseRejectionEvent(Engine engine, JsString type, EventInit init, double timeStamp, JsValue promise, JsValue reason)
        : base(engine, type, init, timeStamp)
    {
        Promise = promise;
        Reason = reason;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#dom-promiserejectionevent-promise — the promise
    /// itself for an event the engine fired, and for a constructed one whatever WebIDL's <c>Promise&lt;any&gt;</c>
    /// conversion made of the dictionary member.
    /// </summary>
    internal JsValue Promise { get; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#dom-promiserejectionevent-reason — the rejection
    /// reason. <c>any</c>, so it defaults to <c>undefined</c>.
    /// </summary>
    internal JsValue Reason { get; }
}
#endif
