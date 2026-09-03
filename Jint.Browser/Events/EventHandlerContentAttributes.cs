using System.Runtime.CompilerServices;
using Acornima.Ast;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Dom;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
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

    private static readonly HashSet<string> _elementHandlerLookup = BuildElementHandlerLookup();

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

    /// <summary>
    /// The same question for an <b>element</b>, which is a smaller set: <c>onreadystatechange</c> and
    /// <c>onvisibilitychange</c> are a <c>Document</c>'s IDL attributes and content attributes of nothing.
    /// </summary>
    /// <remarks>
    /// <c>&lt;div onreadystatechange="…"&gt;</c> is an ordinary attribute, which
    /// <c>event-handler-non-content-document-idl-attributes.html</c> checks by dispatching the event and
    /// asserting nothing ran.
    /// </remarks>
    private static bool IsElementHandlerType(string type) => _elementHandlerLookup.Contains(type);

    private static HashSet<string> BuildElementHandlerLookup()
    {
        var names = new HashSet<string>(ElementHandlers, StringComparer.Ordinal);
        names.UnionWith(BodyHandlersOwnedByTheWindow);
        return names;
    }

    private static HashSet<string> BuildHandlerLookup()
    {
        var names = new HashSet<string>(_elementHandlerLookup, StringComparer.Ordinal);
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
            if (name.Length > 2 && name[0] == 'o' && name[1] == 'n' && IsElementHandlerType(name.Substring(2)))
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

        // https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-content-attributes step 3:
        // "if scripting is disabled for element's node document, return". This is the one place that has to
        // check it, because it is the one place every path arrives at — the wrapper's creation, a read or a
        // write of the IDL attribute, and the dispatcher asking for the parent. What stays true either way is
        // that the attribute's text is untouched, so turning scripting back on for the next document compiles
        // exactly the markup that was always there.
        if (!wrapper.DomRealm.ScriptingEnabled)
        {
            return target;
        }

        // A document carries no content attributes, so its handler slot has only the IDL half; the null here
        // removes a handler the markup no longer declares, and for a document there never was one. An
        // element's attribute is read only for a type an element can carry it for.
        var attribute = element is not null && IsElementHandlerType(type) ? element.GetAttribute("on" + type) : null;
        var sources = _sources.GetOrCreateValue(target);
        var known = sources.TryGetLast(type, out var last);

        if (known && string.Equals(last, attribute, StringComparison.Ordinal))
        {
            return target;
        }

        sources.Record(type, attribute);

        // **A slot nobody has looked at, for an attribute that is not there, is not a change.** HTML's "set
        // the content attribute" step is what this reconciliation stands in for, and it observes an attribute
        // appearing, changing or going away — never one that was absent all along. The distinction is only
        // visible where the slot is *shared*: a body's `onerror`, `onload` and their kind are redirected to
        // the window, so the first dispatch through a body of an event whose handler a script assigned to the
        // window would otherwise arrive here, find no `onerror` attribute on the body, and remove it.
        // `dom/events/event-global.html`'s ErrorEvent test is what found that.
        if (!known && attribute is null)
        {
            return target;
        }

        // https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-content-attributes — setting
        // the attribute sets the handler to an internal raw uncompiled handler, and removing it sets the
        // handler to null. Either way whatever was there, compiled or assigned by script, is replaced.
        var existing = target.FindEventHandler(type);

        if (attribute is null)
        {
            // "Deactivate an event handler": the handler is null, so the listener goes with it.
            if (existing is not null)
            {
                target.RemoveListener(existing);
            }

            return target;
        }

        var handler = new UncompiledHandler(wrapper, target, type, attribute);

        // **In place**, because an event handler's position in the listener list is fixed when the handler is
        // first activated and a later value does not move it. Removing and re-adding would put every handler
        // a page rewrites after the listeners registered since, which `inline-event-handler-ordering.html`
        // measures by dispatching.
        if (existing is not null)
        {
            existing.Callback = handler;
            return target;
        }

        target.AddListener(new EventListenerRegistration(type, handler)
        {
            IsEventHandler = true,
        });

        return target;
    }

    /// <summary>
    /// The one write of an attribute this package can see, and what makes a handler's <b>position</b> in the
    /// listener list the one HTML gives it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// HTML activates an event handler when its content attribute is <i>set</i>, and the listener it creates
    /// then keeps its place among the <c>addEventListener</c> listeners for ever. Reconciling only at a
    /// dispatch or an IDL read would create it at the wrong moment — after every listener registered in
    /// between — and no later reconciliation could recover the order.
    /// </para>
    /// <para>
    /// It is reached from <c>DomHostHooks.SetAttribute</c> and <c>RemoveAttribute</c>, which is the seam the
    /// override table already has for a member whose body this package owns. The two are the whole of what a
    /// page uses; <c>setAttributeNS</c>, <c>toggleAttribute</c>, an <c>Attr</c>'s <c>value</c> and
    /// <c>NamedNodeMap</c> write to the same attribute without passing here, and a handler written that way
    /// is still reconciled at the next dispatch or IDL read — it merely takes its position then.
    /// </para>
    /// </remarks>
    internal static void AttributeChanged(DomRealm realm, IElement element, string name)
    {
        if (name.Length <= 2 || (name[0] | 0x20) != 'o' || (name[1] | 0x20) != 'n')
        {
            return;
        }

        var type = name.Substring(2).ToLowerInvariant();
        if (!IsElementHandlerType(type))
        {
            return;
        }

        if (realm.WrapNode(element) is DomNodeObject wrapper)
        {
            Reconcile(wrapper, type);
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes — the getter, which
    /// compiles the content attribute if that is what the slot still holds.
    /// </summary>
    internal static JsValue Get(DomNodeObject wrapper, string type)
        => CurrentValue(Reconcile(wrapper, type), type);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#getting-the-current-value-of-the-event-handler
    /// over a target whose content attribute has already been reconciled — or that has none, which is the
    /// <b>window</b>'s case.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every read of an event handler IDL attribute goes through here, and that is the point: the compile-on-
    /// read placeholder must never escape into script, and a <c>&lt;body onload&gt;</c> is read through
    /// <c>window.onload</c> as often as through <c>body.onload</c>. Reading it as
    /// <c>function onload() { [native code] }</c> is what <c>Body-FrameSet-Event-Handlers.html</c> caught.
    /// </para>
    /// <para>
    /// A body that does not parse sets the handler to null and <b>leaves the registration where it is</b>:
    /// HTML fixes an event handler's position in the listener list when the handler is first set, and a null
    /// callback is skipped rather than removed. Removing it instead would move a later handler up, which
    /// <c>inline-event-handler-ordering.html</c> measures by dispatching.
    /// </para>
    /// </remarks>
    internal static JsValue CurrentValue(JsEventTarget? target, string type)
    {
        if (target?.FindEventHandler(type) is not { } registration)
        {
            return JsValue.Null;
        }

        if (registration.Callback is not UncompiledHandler uncompiled)
        {
            return registration.Callback;
        }

        var compiled = uncompiled.Compile();
        registration.Callback = compiled is null ? JsValue.Null : compiled;
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

        /// <summary>
        /// Whether this slot has ever been reconciled, and against what. The caller needs the two answers
        /// apart: "unchanged" and "never looked at, and still absent" both mean do nothing, but they are not
        /// the same fact and only one of them may leave the recorded value alone.
        /// </summary>
        internal bool TryGetLast(string type, out string? attribute) => _byType.TryGetValue(type, out attribute);

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
        /// <b>The function is exactly the one HTML describes</b>: named for the attribute, taking one
        /// parameter called <c>event</c> — five for a <c>Window</c>'s <c>error</c> handler — and with the
        /// attribute's text as its body between two newlines. That is observable, because
        /// <c>toString()</c> of it is, and the source text is parsed as such rather than assembled by
        /// <c>Function</c>'s constructor.
        /// </para>
        /// <para>
        /// <b>The scope chain is the function's, not its body's</b>, which is the half that cannot be
        /// emulated with a <c>with</c> statement inside the source. HTML's chain is the realm's global
        /// environment, then an object environment over the node document, then one over the form owner, then
        /// one over the element — each with the <i>withEnvironment</i> flag, so <c>Symbol.unscopables</c>
        /// applies to every one of them. Putting that inside the body instead would make the objects shadow
        /// the function's own <b>parameters</b>: a <c>&lt;body onerror&gt;</c> whose first parameter is named
        /// <c>event</c> would read <c>window.event</c> rather than the message, which is precisely what
        /// <c>body-onerror-runtime-error.html</c> catches.
        /// </para>
        /// <para>
        /// Nothing here executes a script. The parse goes through the engine's own parser, so the host's
        /// parsing limits apply, and the function object is created directly — so the compilation is still
        /// not a nested script execution and still cannot re-arm the engine's per-entry constraints. The body
        /// is sloppy-mode, which is what lets an unqualified name reach the object environments at all.
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

            // "If scripting is disabled for eventTarget, then return null." A document this package did not
            // load — a `DOMParser` result, `createHTMLDocument`, `new Document()` — has a browsing context
            // with no scripting service, so its handler attributes are text and nothing else. It is not a
            // failure: nothing is reported and the slot is untouched, so the attribute compiles if the node
            // is ever adopted into a document that does script.
            if (!IsScriptingEnabled())
            {
                return null;
            }

            try
            {
                // HTML's special error event handling: a window `onerror` takes five parameters rather than
                // the event. Everything else, including an element's own `onerror`, takes the event.
                var parameters = _target.IsGlobalScope && string.Equals(_handlerType, "error", StringComparison.Ordinal)
                    ? "event, source, lineno, colno, error"
                    : "event";

                var sourceText = "function on" + _handlerType + "(" + parameters + ") {\n" + _body + "\n}";

                // Retained deliberately and per handler, whatever the engine's own setting: `toString()` of a
                // handler is what HTML specifies, and the text is one attribute long.
                var parser = engine.GetParserFor(ScriptParsingOptions.RetainingDefault);
                var script = parser.ParseScriptGuarded(realm, sourceText, source: SourceName(), strict: false);

                if (script.Body.Count != 1 || script.Body[0] is not FunctionDeclaration declaration)
                {
                    return Failed(engine, null);
                }

                var definition = new JintFunctionDefinition(declaration);
                return realm.Intrinsics.Function.OrdinaryFunctionCreate(
                    realm.Intrinsics.Function.PrototypeObject,
                    definition,
                    definition.ThisMode,
                    ScopeChain(engine, realm),
                    privateScope: null);
            }
            catch (JavaScriptException exception)
            {
                return Failed(engine, exception);
            }
        }

        /// <summary>
        /// The document's URL, so that an exception escaping the handler is reported against the document the
        /// attribute is in — which is what HTML's <c>filename</c> is for an inline handler.
        /// </summary>
        private string SourceName() => _wrapper.Node.Owner?.Url ?? "";

        /// <summary>
        /// https://html.spec.whatwg.org/multipage/webappapis.html#concept-n-noscript — whether scripting is
        /// enabled for the node's document.
        /// </summary>
        /// <remarks>
        /// The page's own document is the only one with a browsing context that scripts; every other document
        /// an engine here can reach was parsed by <c>DOMParser</c>, built by <c>createHTMLDocument</c> or
        /// constructed outright, and each of those gets a context with no scripting service on purpose. A
        /// binding installed with no page runtime behind it has no such distinction to make — the host handed
        /// the binding its document — so it answers true.
        /// </remarks>
        private bool IsScriptingEnabled()
        {
            if (Runtime.PageRuntime.Find(_wrapper.Engine) is not { } runtime)
            {
                return true;
            }

            var document = _wrapper.Node as IDocument ?? _wrapper.Node.Owner;
            return ReferenceEquals(document, runtime.Document);
        }

        /// <summary>
        /// https://html.spec.whatwg.org/multipage/webappapis.html#getting-the-current-value-of-the-event-handler
        /// steps 10 and 11 — the environments an unqualified name in the body resolves through, outermost
        /// first: the global environment, the node document, the form owner and the element.
        /// </summary>
        private Jint.Runtime.Environments.Environment ScopeChain(Engine engine, Realm realm)
        {
            Jint.Runtime.Environments.Environment scope = realm.GlobalEnv;
            var dom = _wrapper.DomRealm;

            if (_wrapper.Node is not IElement element)
            {
                // A document's own handler: the document is the target, so it is the only object environment.
                return _wrapper.Node is IDocument document
                    ? Wrap(engine, dom.WrapNode(document), scope)
                    : scope;
            }

            if (element.Owner is { } owner)
            {
                scope = Wrap(engine, dom.WrapNode(owner), scope);
            }

            if (FormOwner(element) is { } form)
            {
                scope = Wrap(engine, dom.WrapNode(form), scope);
            }

            return Wrap(engine, _wrapper, scope);
        }

        private static ObjectEnvironment Wrap(Engine engine, ObjectInstance binding, Jint.Runtime.Environments.Environment outer)
            => JintEnvironment.NewObjectEnvironment(engine, binding, outer, provideThis: true, withEnvironment: true);

        /// <summary>
        /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#form-owner — the form the
        /// <c>form</c> content attribute names when the element is connected, and otherwise the nearest
        /// ancestor form.
        /// </summary>
        /// <remarks>
        /// Computed rather than read off AngleSharp, whose <c>Form</c> property is declared separately on
        /// each of the eight form-control interfaces and on no common one, so there is nothing to ask an
        /// <c>IElement</c> for.
        /// </remarks>
        private static IHtmlFormElement? FormOwner(IElement element)
        {
            // https://html.spec.whatwg.org/multipage/forms.html#form-associated-element — only these have a
            // form owner at all, which is why a `<div>` inside a `<form>` resolves an unqualified name against
            // the document and the window and never against the form.
            if (element is not (IHtmlButtonElement
                or IHtmlFieldSetElement
                or IHtmlInputElement
                or IHtmlObjectElement
                or IHtmlOutputElement
                or IHtmlSelectElement
                or IHtmlTextAreaElement
                or IHtmlImageElement))
            {
                return null;
            }

            if (element.GetAttribute("form") is { Length: > 0 } id)
            {
                return element.Owner?.GetElementById(id) as IHtmlFormElement;
            }

            for (var current = element.ParentElement; current is not null; current = current.ParentElement)
            {
                if (current is IHtmlFormElement form)
                {
                    return form;
                }
            }

            return null;
        }

        /// <summary>
        /// HTML: a body that does not parse reports the error and leaves the handler null. Reporting is
        /// <i>report an exception</i>, so it fires the global <c>error</c> event before it reaches the sink —
        /// which is what lets <c>window.onerror</c> hear a syntax error in an <c>onclick</c> attribute.
        /// </summary>
        private ObjectInstance? Failed(Engine engine, JavaScriptException? exception)
        {
            _failed = true;

            if (exception is not null)
            {
                engine._webApi?.FireGlobalErrorEvent(exception);
                engine._webApi?.Diagnostics?.Report(
                    DiagnosticEvent.ForUncaughtCallbackError(exception, DiagnosticCallbackSource.EventListener));
            }

            return null;
        }
    }
}
