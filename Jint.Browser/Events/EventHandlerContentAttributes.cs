using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Dom;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// HTML's event handler content attributes — <c>onclick="…"</c> — and the IDL attributes that share their slot.
/// <para>
/// https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-attributes
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>One slot, two ways in.</b> HTML gives an element one <i>event handler map</i>, and both
/// <c>&lt;div onclick="f()"&gt;</c> and <c>div.onclick = f</c> write the same entry of it. The engine already
/// models that entry — <c>JsEventTarget</c>'s listener list holds it with <c>IsEventHandler</c> set, so a
/// handler takes its turn in registration order among the <c>addEventListener</c> listeners rather than before
/// or after all of them, and <c>EventHandlerAttributes</c> is the get/set pair. What is added here is the
/// content-attribute half: compiling the attribute's text, and keeping the two in step.
/// </para>
/// <para>
/// <b>How the two are kept in step, and why it needs no notification from AngleSharp.</b> The attribute's text
/// <i>is</i> the state: an element's handler slot records which text it was last reconciled against, and any
/// difference — the attribute appearing, changing or going away — is what HTML's "set the content attribute"
/// step observes. Three points reconcile, and between them they cover everything a page can do:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Wrapper construction</b> scans the element's attributes once, so a handler written in the markup is
/// registered <i>before</i> any listener a script can add — which is the order a browser has, and which no
/// later reconciliation could recover.
/// </description></item>
/// <item><description>
/// <b>Every dispatch through the element</b>, from <c>DomNodeObject.GetParent</c>, which the dispatcher calls
/// exactly once per event path item. It reconciles the one handler name matching the event's type, so the cost
/// is a single <c>GetAttribute</c> per element per dispatch and an attribute a script changed later is seen
/// before the listeners run.
/// </description></item>
/// <item><description>
/// <b>Reading or writing the IDL attribute</b>, so <c>el.onclick</c> answers the compiled function and
/// <c>el.onclick = f</c> replaces it.
/// </description></item>
/// </list>
/// <para>
/// The alternatives were AngleSharp's <c>MutationObserver</c> (a document-wide observer whose records arrive
/// for every attribute mutation, and whose lane campaign item R4 owns) and its <c>IAttributeObserver</c>
/// service (a single registration in the <c>IConfiguration</c>, which the page runtime builds, and which
/// AngleSharp also uses internally). Both would put a notification path in a file another campaign item owns,
/// to learn something the attribute's own text already says.
/// </para>
/// <para>
/// <b>Compilation is lazy.</b> Reconciling registers a placeholder that compiles on first use, so a page with a
/// hundred <c>onclick</c> attributes compiles the ones that fire. That is also what makes a syntax error in an
/// attribute behave as HTML says — reported when the handler is first needed, not when the element is parsed.
/// </para>
/// </remarks>
internal static class EventHandlerContentAttributes
{
    /// <summary>
    /// Which attribute text each of an element's handler slots was last reconciled against. Only elements that
    /// have ever carried a handler get an entry, and it dies with the wrapper.
    /// </summary>
    private static readonly ConditionalWeakTable<JsEventTarget, HandlerSources> _sources = new();

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#globaleventhandlers — every event handler IDL
    /// attribute an element and a document carry, in the mixin's own order.
    /// </summary>
    /// <remarks>
    /// <c>GlobalEventHandlers</c> plus <c>DocumentAndElementEventHandlers</c> (<c>oncopy</c>, <c>oncut</c>,
    /// <c>onpaste</c>). <c>WindowEventHandlers</c> is not here: those belong to the window, and the page
    /// runtime's own installer declares them there.
    /// </remarks>
    internal static readonly string[] ElementHandlers =
    [
        "abort", "auxclick", "beforeinput", "beforematch", "beforetoggle", "blur", "cancel", "canplay",
        "canplaythrough", "change", "click", "close", "contextlost", "contextmenu", "contextrestored", "copy",
        "cuechange", "cut", "dblclick", "drag", "dragend", "dragenter", "dragleave", "dragover", "dragstart",
        "drop", "durationchange", "emptied", "ended", "error", "focus", "formdata", "input", "invalid",
        "keydown", "keypress", "keyup", "load", "loadeddata", "loadedmetadata", "loadstart", "mousedown",
        "mouseenter", "mouseleave", "mousemove", "mouseout", "mouseover", "mouseup", "paste", "pause", "play",
        "playing", "pointercancel", "pointerdown", "pointerenter", "pointerleave", "pointermove", "pointerout",
        "pointerover", "pointerup", "progress", "ratechange", "reset", "resize", "scroll", "scrollend",
        "securitypolicyviolation", "seeked", "seeking", "select", "selectionchange", "selectstart",
        "slotchange", "stalled", "submit", "suspend", "timeupdate", "toggle", "transitioncancel",
        "transitionend", "transitionrun", "transitionstart", "volumechange", "waiting", "wheel",
    ];

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#windoweventhandlers, plus the
    /// <c>GlobalEventHandlers</c> names HTML redirects: a handler content attribute on <c>&lt;body&gt;</c> or
    /// <c>&lt;frameset&gt;</c> with one of these names sets the <b>Window</b>'s handler, not the body's.
    /// </summary>
    /// <remarks>
    /// https://html.spec.whatwg.org/multipage/sections.html#the-body-element is the table this is; it is what
    /// makes <c>&lt;body onload="…"&gt;</c> — the oldest way to run a script after a page loads — work at all,
    /// since the <c>load</c> event fires at the window and never reaches the body.
    /// </remarks>
    internal static readonly string[] BodyHandlersOwnedByTheWindow =
    [
        "afterprint", "beforeprint", "beforeunload", "blur", "error", "focus", "hashchange", "languagechange",
        "load", "message", "messageerror", "offline", "online", "pagehide", "pagereveal", "pageshow",
        "pageswap", "popstate", "rejectionhandled", "resize", "scroll", "storage", "unhandledrejection",
        "unload",
    ];

    private static readonly HashSet<string> _bodyHandlerLookup = new(BodyHandlersOwnedByTheWindow, StringComparer.Ordinal);

    private static readonly HashSet<string> _handlerLookup = BuildHandlerLookup();

    /// <summary>
    /// Whether <c>on</c> + <paramref name="type"/> is an event handler content attribute at all.
    /// </summary>
    /// <remarks>
    /// HTML defines the set; an attribute named <c>onwhatever</c> for an event type the standard does not
    /// declare is an ordinary attribute and never becomes a handler, which is why a page cannot invent one.
    /// Bounding the set is also what keeps a dispatch of a custom event type from recording a slot for it.
    /// </remarks>
    internal static bool IsHandlerType(string type) => _handlerLookup.Contains(type);

    private static HashSet<string> BuildHandlerLookup()
    {
        var names = new HashSet<string>(ElementHandlers, StringComparer.Ordinal);
        names.UnionWith(BodyHandlersOwnedByTheWindow);
        names.Add("readystatechange");
        names.Add("visibilitychange");
        return names;
    }

    /// <summary>
    /// Registers every handler the element's markup declares, in attribute order, at the moment its wrapper is
    /// built — which is before any script could have reached the element to add a listener of its own.
    /// </summary>
    internal static void InstallFromMarkup(DomNodeObject wrapper)
    {
        // The length check is what keeps this off the cost of an ordinary wrapper: most elements in a document
        // carry no attribute at all, and asking is cheaper than taking an enumerator to find that out.
        if (wrapper.Node is not IElement element || element.Attributes.Length == 0)
        {
            return;
        }

        foreach (var attribute in element.Attributes)
        {
            var name = attribute.Name;
            if (name.Length > 2 && name[0] == 'o' && name[1] == 'n' && IsHandlerType(name.Substring(2)))
            {
                Reconcile(wrapper, name.Substring(2));
            }
        }
    }

    /// <summary>
    /// Registers the handlers a document's body declares, which is the one case a wrapper built on demand is
    /// too late for.
    /// </summary>
    /// <remarks>
    /// Every other element's handlers arrive with its wrapper, and a wrapper exists by the time anything can
    /// dispatch through the element: script that reaches it has one, and the dispatcher builds one for every
    /// ancestor on the path. <c>&lt;body onload&gt;</c> is different because the handler HTML redirects belongs
    /// to the <b>window</b>, and <c>load</c> fires at the window without ever touching the body — so a page
    /// whose only script is a <c>body</c> attribute would never have had a body wrapper at all. Building it
    /// once, when the parse ends, is what makes the oldest way of running a script after load work.
    /// </remarks>
    internal static void InstallBodyHandlers(DomRealm dom, IDocument document)
    {
        if (document.Body is { } body)
        {
            dom.WrapNode(body);
        }
    }

    /// <summary>
    /// Brings one handler slot back in step with its content attribute, registering, replacing or removing the
    /// handler when the attribute's text has changed since it was last looked at.
    /// </summary>
    /// <returns>The target the handler belongs to, which is the window for a body handler HTML redirects.</returns>
    internal static JsEventTarget? Reconcile(DomNodeObject wrapper, string type)
    {
        var element = wrapper.Node as IElement;
        var target = TargetFor(wrapper, element, type);
        if (target is null)
        {
            return null;
        }

        // A document carries no content attributes, so its handler slot has only the IDL half; the null here
        // removes a handler the markup no longer declares, and for a document there never was one.
        var attribute = element?.GetAttribute("on" + type);
        var sources = _sources.GetOrCreateValue(target);

        if (!sources.HasChanged(type, attribute))
        {
            return target;
        }

        sources.Record(type, attribute);

        // https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-content-attributes — setting the
        // attribute sets the handler to an internal raw uncompiled handler, and removing it sets the handler to
        // null. Either way whatever was there, compiled or assigned by script, is replaced.
        if (target.FindEventHandler(type) is { } existing)
        {
            target.RemoveListener(existing);
        }

        if (attribute is null)
        {
            return target;
        }

        target.AddListener(new EventListenerRegistration(type, new UncompiledHandler(wrapper, target, type, attribute))
        {
            IsEventHandler = true,
        });

        return target;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes — the getter, which
    /// compiles the content attribute if that is what the slot still holds.
    /// </summary>
    internal static JsValue Get(DomNodeObject wrapper, string type)
    {
        var target = Reconcile(wrapper, type);
        if (target?.FindEventHandler(type) is not { } registration)
        {
            return JsValue.Null;
        }

        if (registration.Callback is UncompiledHandler uncompiled)
        {
            // "Get the current value of the event handler" compiles on read, so el.onclick answers the
            // function object rather than the placeholder — and answers null when the body would not parse.
            var compiled = uncompiled.Compile();
            if (compiled is null)
            {
                target.RemoveListener(registration);
                return JsValue.Null;
            }

            registration.Callback = compiled;
            return compiled;
        }

        return registration.Callback;
    }

    /// <summary>
    /// The setter. Assigning replaces whatever the slot held, content attribute included, and leaves the
    /// content attribute's own text alone — which is why <c>getAttribute('onclick')</c> still answers the
    /// markup after <c>el.onclick = f</c>.
    /// </summary>
    internal static JsValue Set(DomNodeObject wrapper, string type, JsValue value)
    {
        var target = Reconcile(wrapper, type);
        return target is null ? JsValue.Undefined : EventHandlerAttributes.Set(target, type, value);
    }

    /// <summary>
    /// Which target owns this handler: the element, or the window for one of the names HTML redirects from
    /// <c>&lt;body&gt;</c> and <c>&lt;frameset&gt;</c>.
    /// </summary>
    private static JsEventTarget? TargetFor(DomNodeObject wrapper, IElement? element, string type)
    {
        // AngleSharp models <frameset> with the plain IHtmlElement, so the local name is the test; a body and
        // a frameset carry the same redirected handler names.
        if (element is IHtmlBodyElement or IHtmlElement { LocalName: "frameset" } && _bodyHandlerLookup.Contains(type))
        {
            return wrapper.DomRealm.WindowTarget;
        }

        return wrapper;
    }

    /// <summary>Which attribute text each handler slot of one target was last reconciled against.</summary>
    /// <remarks>
    /// A <see cref="Dictionary{TKey,TValue}"/> rather than an array, because the overwhelming majority of
    /// elements carry no handler at all and the ones that do carry one or two. The distinction the value keeps
    /// is three-way: no entry means never looked at, a null value means the attribute was absent when it was,
    /// and a string is the text it held.
    /// </remarks>
    private sealed class HandlerSources
    {
        private readonly Dictionary<string, string?> _byType = new(StringComparer.Ordinal);

        internal bool HasChanged(string type, string? attribute)
            => !_byType.TryGetValue(type, out var last) || !string.Equals(last, attribute, StringComparison.Ordinal);

        internal void Record(string type, string? attribute) => _byType[type] = attribute;
    }

    /// <summary>
    /// The placeholder a reconciled content attribute registers: it holds the attribute's text and compiles it
    /// the first time the handler is needed, which is either a dispatch or a read of the IDL attribute.
    /// </summary>
    /// <remarks>
    /// It never escapes into script — the getter compiles before answering, and the listener list is not
    /// reachable as an object — so nothing can observe a function object that is not the compiled one.
    /// </remarks>
    private sealed class UncompiledHandler : Function
    {
        private readonly DomNodeObject _wrapper;
        private readonly JsEventTarget _target;
        private readonly string _handlerType;
        private readonly string _body;
        private bool _failed;

        internal UncompiledHandler(DomNodeObject wrapper, JsEventTarget target, string type, string body)
            : base(wrapper.Engine, wrapper.DomRealm.PrincipalRealm, new JsString("on" + type))
        {
            _prototype = wrapper.DomRealm.PrincipalRealm.Intrinsics.Function.PrototypeObject;
            _wrapper = wrapper;
            _target = target;
            _handlerType = type;
            _body = body;
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            var compiled = Compile();
            if (compiled is null)
            {
                return JsValue.Undefined;
            }

            // Swap the slot so the next dispatch calls the compiled function directly rather than through
            // here. The registration is still the same entry, so the handler keeps its position in the list.
            if (_target.FindEventHandler(_handlerType) is { } registration && ReferenceEquals(registration.Callback, this))
            {
                registration.Callback = compiled;
            }

            return ((ICallable) compiled).Call(thisObject, arguments);
        }

        /// <summary>
        /// https://html.spec.whatwg.org/multipage/webappapis.html#getting-the-current-value-of-the-event-handler —
        /// compile the attribute's text into a function, or answer <see langword="null"/> when it does not parse.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The scope is HTML's, emulated the way every implementation emulates it: the body runs inside
        /// <c>with (document) { with (this) { … } }</c>, so an unqualified name resolves against the document
        /// and then against the element before it reaches the global — which is what lets
        /// <c>&lt;a onclick="return false"&gt;</c> and <c>&lt;input onchange="form.submit()"&gt;</c> work.
        /// </para>
        /// <para>
        /// <b>One level of HTML's scope is missing</b>: the form owner, which HTML puts between the document
        /// and the element for a form-associated element. A handler naming a sibling control by its <c>name</c>
        /// without going through <c>this.form</c> resolves here where a browser resolves it there.
        /// </para>
        /// <para>
        /// The function is built through <c>Function</c>'s own constructor rather than by evaluating source,
        /// so the compilation is not a nested script execution and cannot re-arm the engine's per-entry
        /// constraints. Its body is sloppy-mode, which is what makes <c>with</c> legal.
        /// </para>
        /// </remarks>
        internal ObjectInstance? Compile()
        {
            if (_failed)
            {
                return null;
            }

            var engine = _wrapper.Engine;
            var realm = _wrapper.DomRealm.PrincipalRealm;

            try
            {
                // HTML's special error event handling: a window `onerror` takes five parameters rather than
                // the event. Everything else, including an element's own `onerror`, takes the event.
                var parameters = _target.IsGlobalScope && string.Equals(_handlerType, "error", StringComparison.Ordinal)
                    ? new JsValue[]
                    {
                        JsString.Create("event"), JsString.Create("source"), JsString.Create("lineno"),
                        JsString.Create("colno"), JsString.Create("error"),
                        JsString.Create("with (document) { with (this) { " + _body + "\n} }"),
                    }
                    : [JsString.Create("event"), JsString.Create("with (document) { with (this) { " + _body + "\n} }")];

                return realm.Intrinsics.Function.Construct(parameters, realm.Intrinsics.Function);
            }
            catch (JavaScriptException exception)
            {
                // HTML: a body that does not parse reports the error and leaves the handler null. Reported
                // through the same sink an uncaught listener error uses, so a page runtime sees it as a page
                // error rather than losing it.
                _failed = true;
                engine._webApi?.Diagnostics?.Report(
                    DiagnosticEvent.ForUncaughtCallbackError(exception, DiagnosticCallbackSource.EventListener));
                return null;
            }
        }
    }
}
