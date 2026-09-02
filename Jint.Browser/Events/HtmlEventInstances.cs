using Jint.Native;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// A <c>SubmitEvent</c> instance.
/// <para>
/// https://html.spec.whatwg.org/multipage/form-events.html#submitevent
/// </para>
/// </summary>
internal sealed class JsSubmitEvent : JsEvent
{
    internal JsSubmitEvent(Engine engine, JsString type, EventInit init, double timeStamp, JsValue submitter)
        : base(engine, type, init, timeStamp)
    {
        Submitter = submitter;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-events.html#dom-submitevent-submitter — the button that
    /// started the submission, or <see cref="JsValue.Null"/> when the form submitted itself.
    /// </summary>
    internal JsValue Submitter { get; }
}

/// <summary>
/// A <c>FormDataEvent</c> instance.
/// <para>
/// https://html.spec.whatwg.org/multipage/form-events.html#formdataevent
/// </para>
/// </summary>
/// <remarks>
/// <c>formData</c> is a required dictionary member with no default, so constructing one without it is a
/// <c>TypeError</c> — the only event interface here with that shape.
/// </remarks>
internal sealed class JsFormDataEvent : JsEvent
{
    internal JsFormDataEvent(Engine engine, JsString type, EventInit init, double timeStamp, JsValue formData)
        : base(engine, type, init, timeStamp)
    {
        FormData = formData;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/form-events.html#dom-formdataevent-formdata.</summary>
    internal JsValue FormData { get; }
}

/// <summary>
/// A <c>HashChangeEvent</c> instance.
/// <para>
/// https://html.spec.whatwg.org/multipage/nav-history-apis.html#the-hashchangeevent-interface
/// </para>
/// </summary>
internal sealed class JsHashChangeEvent : JsEvent
{
    internal JsHashChangeEvent(Engine engine, JsString type, EventInit init, double timeStamp, string oldUrl, string newUrl)
        : base(engine, type, init, timeStamp)
    {
        OldUrl = oldUrl;
        NewUrl = newUrl;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-hashchangeevent-oldurl.</summary>
    internal string OldUrl { get; }

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-hashchangeevent-newurl.</summary>
    internal string NewUrl { get; }
}

/// <summary>
/// A <c>PopStateEvent</c> instance.
/// <para>
/// https://html.spec.whatwg.org/multipage/nav-history-apis.html#the-popstateevent-interface
/// </para>
/// </summary>
/// <remarks>
/// <c>state</c> is <c>any</c>, so it is whatever the history entry holds — a structured clone in a browser,
/// and here the value the history layer (campaign item R5) hands over.
/// </remarks>
internal sealed class JsPopStateEvent : JsEvent
{
    internal JsPopStateEvent(Engine engine, JsString type, EventInit init, double timeStamp, JsValue state, bool hasUaVisualTransition)
        : base(engine, type, init, timeStamp)
    {
        State = state;
        HasUaVisualTransition = hasUaVisualTransition;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-popstateevent-state.</summary>
    internal JsValue State { get; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-popstateevent-hasuavisualtransition —
    /// always false here, because there is nothing to animate.
    /// </summary>
    internal bool HasUaVisualTransition { get; }
}

/// <summary>
/// A <c>PageTransitionEvent</c> instance — <c>pageshow</c> and <c>pagehide</c>.
/// <para>
/// https://html.spec.whatwg.org/multipage/nav-history-apis.html#the-pagetransitionevent-interface
/// </para>
/// </summary>
internal sealed class JsPageTransitionEvent : JsEvent
{
    internal JsPageTransitionEvent(Engine engine, JsString type, EventInit init, double timeStamp, bool persisted)
        : base(engine, type, init, timeStamp)
    {
        Persisted = persisted;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-pagetransitionevent-persisted — false
    /// for every page here, because one engine per navigation is the runtime model and nothing is restored
    /// from a back/forward cache.
    /// </summary>
    internal bool Persisted { get; }
}

/// <summary>
/// A <c>BeforeUnloadEvent</c> instance.
/// <para>
/// https://html.spec.whatwg.org/multipage/nav-history-apis.html#the-beforeunloadevent-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The interface has no constructor arguments of its own and no init dictionary: <c>returnValue</c> is a
/// mutable <c>DOMString</c> a listener writes, and it is the legacy half of asking to stay on the page. The
/// modern half is <c>preventDefault()</c>, and both are honoured — HTML says the dialog is shown when the
/// event was canceled <i>or</i> <c>returnValue</c> is not the empty string.
/// </para>
/// <para>
/// A <c>BeforeUnloadEvent</c> is constructible from script (it has the default <c>Event</c> constructor), but
/// only one dispatched by the unload path can stop anything.
/// </para>
/// </remarks>
internal sealed class JsBeforeUnloadEvent : JsEvent
{
    internal JsBeforeUnloadEvent(Engine engine, JsString type, EventInit init, double timeStamp)
        : base(engine, type, init, timeStamp)
    {
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-beforeunloadevent-returnvalue.
    /// </summary>
    internal string ReturnValue { get; set; } = "";

    /// <summary>
    /// Whether a listener asked to stay — HTML's <i>unload prompt</i> condition: the canceled flag, or a
    /// non-empty <c>returnValue</c>.
    /// </summary>
    internal bool WantsPrompt => CanceledFlag || ReturnValue.Length != 0;
}
