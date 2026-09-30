using Jint.Browser.Dom;
using Jint.Browser.Navigation;
using Jint.Browser.Runtime;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Abort;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#the-navigateevent-interface</summary>
internal sealed class JsNavigateEvent : JsEvent
{
    internal JsNavigateEvent(Engine engine, JsString type, EventInit init, double timeStamp, string navigationType,
        JsNavigationDestination destination, JsAbortSignal signal) : base(engine, type, init, timeStamp)
    {
        NavigationType = navigationType;
        Destination = destination;
        Signal = signal;
    }

    internal string NavigationType { get; }
    internal JsNavigationDestination Destination { get; }
    internal JsAbortSignal Signal { get; }
    internal bool CanIntercept { get; init; }
    internal bool UserInitiated { get; init; }
    internal bool HashChange { get; init; }
    internal JsValue FormData { get; init; } = Null;
    internal string? DownloadRequest { get; init; }
    internal JsValue Info { get; init; } = Undefined;
    internal bool HasUAVisualTransition { get; init; }
    internal JsValue SourceElement { get; init; } = Null;
    internal string InterceptionState { get; set; } = "none";
    internal List<ICallable> Handlers { get; } = [];
    internal JsNavigation? Navigation { get; init; }
    private string? _scroll;
    private string? _focusReset;
    private Element? _focusedBefore;

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-navigateevent-intercept</summary>
    internal JsValue Intercept(JsValue options)
    {
        var realm = Destination.Owner.Realm;
        var dictionary = NavigationValues.Dictionary(realm, options);
        var focus = NavigationValues.Get(dictionary, "focusReset");
        var focusReset = NavigationValues.Enum(realm, focus, "after-transition", "after-transition", "manual");
        var handler = NavigationValues.Get(dictionary, "handler");
        if (!handler.IsUndefined() && handler is not ICallable) Throw.TypeError(realm, "handler must be callable.");
        var precommit = NavigationValues.Get(dictionary, "precommitHandler");
        if (!precommit.IsUndefined() && precommit is not ICallable) Throw.TypeError(realm, "precommitHandler must be callable.");
        var scroll = NavigationValues.Get(dictionary, "scroll");
        var scrollBehavior = NavigationValues.Enum(realm, scroll, "after-transition", "after-transition", "manual");
        SharedChecks();
        if (!CanIntercept) Refuse(DomExceptionNames.Security, "This navigation cannot be intercepted.");
        if (!DispatchFlag) Refuse(DomExceptionNames.InvalidState, "intercept() must be called during dispatch.");
        if (!precommit.IsUndefined())
        {
            if (!Cancelable) Refuse(DomExceptionNames.InvalidState, "This navigation cannot defer its commit.");
            Refuse(DomExceptionNames.NotSupported, "Navigation precommit handlers are not supported.");
        }
        if (InterceptionState == "none") _focusedBefore = BrowserEventRealm.Of(Engine).FocusedElement;
        InterceptionState = "intercepted";
        if (handler is ICallable callback) Handlers.Add(callback);
        if (!focus.IsUndefined()) _focusReset = focusReset;
        if (!scroll.IsUndefined()) _scroll = scrollBehavior;
        return Undefined;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-navigateevent-scroll</summary>
    internal JsValue Scroll()
    {
        SharedChecks();
        if (InterceptionState != "committed")
            Refuse(DomExceptionNames.InvalidState, "The intercepted navigation is not ready to scroll.");
        ProcessScroll();
        return Undefined;
    }

    private void SharedChecks()
    {
        if (Navigation is { Active: false }) Refuse(DomExceptionNames.InvalidState, "The document is not active.");
        if (!IsTrusted) Refuse(DomExceptionNames.Security, "An untrusted NavigateEvent cannot control navigation.");
        if (CanceledFlag) Refuse(DomExceptionNames.InvalidState, "The navigation was canceled.");
    }

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#finish-a-navigate-event</summary>
    internal void Finish(bool success)
    {
        if (InterceptionState == "none") return;
        if (Navigation?.Runtime is { } runtime)
        {
            var events = BrowserEventRealm.Of(Engine);
            if (_focusReset != "manual" && ReferenceEquals(events.FocusedElement, _focusedBefore))
            {
                if (events.FocusedElement is { } focused) FocusController.Blur(runtime.Dom, focused);
                if (runtime.Document is { } document
                    && DomSelectors.QuerySelector(runtime.Dom, document, "[autofocus]") is Element autofocus)
                    FocusController.Focus(runtime.Dom, autofocus);
            }
        }
        if (success && InterceptionState != "scrolled" && _scroll != "manual") ProcessScroll();
        InterceptionState = "finished";
        _focusedBefore = null;
    }

    private void ProcessScroll()
    {
        if (Navigation?.Runtime is { } runtime && NavigationType is "push" or "replace")
        {
            var fragment = PageUrl.FragmentOf(Destination.Url);
            var target = runtime.Document is { } document && fragment.Length != 0
                ? DomDocumentReads.ById(runtime.Dom, document, Uri.UnescapeDataString(fragment)) : null;
            if (target is not null) runtime.Layout.ScrollIntoView(target, "start");
            else runtime.Layout.ScrollTo(0);
        }
        InterceptionState = "scrolled";
    }

    private void Refuse(string name, string message) => DomFailures.Refuse(Destination.Owner.Dom, "NavigateEvent", name, message);
}

/// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#the-navigationcurrententrychangeevent-interface</summary>
internal sealed class JsNavigationCurrentEntryChangeEvent : JsEvent
{
    internal JsNavigationCurrentEntryChangeEvent(Engine engine, JsString type, EventInit init, double timeStamp,
        string? navigationType, JsNavigationHistoryEntry from) : base(engine, type, init, timeStamp)
    {
        NavigationType = navigationType;
        From = from;
    }

    internal string? NavigationType { get; }
    internal JsNavigationHistoryEntry From { get; }

    internal static JsNavigationCurrentEntryChangeEvent CreateTrusted(DomRealm dom, string? type, JsNavigationHistoryEntry from)
        => BrowserEventRealm.Of(dom.Engine, dom.OwningRealm).CreateTrusted(BrowserEventInterfaces.NavigationCurrentEntryChangeEvent,
            new JsNavigationCurrentEntryChangeEvent(dom.Engine, new JsString("currententrychange"), new EventInit(false, false, false),
                EventConstructor.TimeStampNow(dom.Engine), type, from));
}
