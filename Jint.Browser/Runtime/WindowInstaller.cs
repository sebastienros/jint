using AngleSharp.Dom;
using Jint.Browser.Dom;
using Jint.Browser.Events;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi.Events;
using Jint.WebApi.StructuredClone;

namespace Jint.Browser.Runtime;

/// <summary>
/// Turns a Jint global object into a <c>Window</c>: one prototype in its chain, and the per-document
/// singletons as own globals.
/// </summary>
/// <remarks>
/// <para>
/// <b>The global stays <c>GlobalObject</c>.</b> Substituting a host global would forfeit the global-identifier
/// inline cache and mean re-installing every intrinsic; instead the global's <c>[[Prototype]]</c> becomes
/// <c>Window.prototype → EventTarget.prototype → Object.prototype</c>, which the global environment record
/// already resolves names through. That is what makes <c>window instanceof EventTarget</c> hold and what lets
/// <c>window.addEventListener</c> reach the engine's own global event target.
/// </para>
/// <para>
/// <b>The singletons are own lazy globals</b> — <c>window</c>, <c>self</c>, <c>frames</c>, <c>top</c>,
/// <c>parent</c>, <c>document</c>, <c>location</c>, <c>screen</c> — installed through the public
/// <c>Engine.AddLazyGlobal</c>, which keeps them on the identifier cache the way every web-API global is.
/// Putting them on the prototype instead would answer, and would cost every read of <c>document</c> a
/// prototype walk.
/// </para>
/// <para>
/// Every member below is a static lambda: it reaches its page through <see cref="PageRuntime.Of"/>, so the
/// shapes are built once for the process and shared by every engine, which is what a shaped prototype is for.
/// </para>
/// </remarks>
internal static class WindowInstaller
{
    /// <summary>
    /// The event-handler content attributes a <c>Window</c> carries, from HTML's <c>GlobalEventHandlers</c>
    /// and <c>WindowEventHandlers</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The list is the subset a page reasonably reaches through the window; a handler this does not name is
    /// still reachable with <c>addEventListener</c>, which is what a page written after 2015 uses. It is
    /// declared above the shapes because a static field initializer runs in declaration order and
    /// <see cref="BuildWindowShape"/> reads it.
    /// </para>
    /// <para>
    /// <c>onerror</c> is the ordinary one-argument handler rather than HTML's legacy
    /// <c>(message, source, lineno, colno, error)</c> form, so a page reading its extra arguments reads
    /// <see langword="undefined"/>. It is here rather than absent because the engine really does fire
    /// <c>error</c> at the global target, and a handler assigned to a name nothing listens on would fail
    /// without a sound.
    /// </para>
    /// </remarks>
    private static readonly string[] _eventHandlers =
    [
        // GlobalEventHandlers
        "abort", "animationend", "animationiteration", "animationstart", "auxclick", "beforeinput",
        "blur", "cancel", "canplay", "canplaythrough", "change", "click", "close", "contextmenu",
        "copy", "cuechange", "cut", "dblclick", "drag", "dragend", "dragenter", "dragleave", "dragover",
        "dragstart", "drop", "durationchange", "emptied", "ended", "focus", "formdata", "input",
        "invalid", "keydown", "keypress", "keyup", "load", "loadeddata", "loadedmetadata", "loadstart",
        "mousedown", "mouseenter", "mouseleave", "mousemove", "mouseout", "mouseover", "mouseup",
        "paste", "pause", "play", "playing", "pointercancel", "pointerdown", "pointerenter",
        "pointerleave", "pointermove", "pointerout", "pointerover", "pointerup", "progress",
        "ratechange", "reset", "resize", "scroll", "scrollend", "securitypolicyviolation", "seeked",
        "seeking", "select", "selectionchange", "selectstart", "slotchange", "stalled", "submit",
        "suspend", "timeupdate", "toggle", "transitionend", "volumechange", "waiting", "wheel",
        // WindowEventHandlers
        "afterprint", "beforeprint", "beforeunload", "hashchange", "languagechange", "message",
        "messageerror", "offline", "online", "pagehide", "pageshow", "popstate", "storage", "unload",
        // The three the engine's global event target fires itself.
        "error", "unhandledrejection", "rejectionhandled",
    ];

    private static readonly JsObjectShape _windowShape = BuildWindowShape();
    private static readonly JsObjectShape _screenShape = BuildScreenShape();
    private static readonly JsObjectShape _mediaQueryListShape = BuildMediaQueryListShape();


    /// <summary>Installs <c>Window</c> on <paramref name="runtime"/>'s engine. Called once, at construction.</summary>
    internal static void Install(PageRuntime runtime)
    {
        var engine = runtime.Engine;
        var realm = engine._mainRealm;
        var global = realm.GlobalObject;

        var windowPrototype = _windowShape.Instantiate(engine, realm.Intrinsics.EventTarget.PrototypeObject);
        var windowInterface = new WindowInterfaceObject(engine, realm, windowPrototype);

        // The per-realm `constructor` slot, filled the way the DOM binding fills its own: the shape declared
        // the name, so this is the sanctioned in-place slot replacement and the prototype stays shaped.
        windowPrototype.DefineOwnPropertyUnchecked(
            "constructor",
            new PropertyDescriptor(windowInterface, PropertyFlag.NonEnumerable));

        runtime.WindowPrototype = windowPrototype;

        var mediaQueryListPrototype = _mediaQueryListShape.Instantiate(engine, realm.Intrinsics.EventTarget.PrototypeObject);
        var mediaQueryListInterface = new MediaQueryListInterfaceObject(engine, realm, mediaQueryListPrototype);

        mediaQueryListPrototype.DefineOwnPropertyUnchecked(
            "constructor",
            new PropertyDescriptor(mediaQueryListInterface, PropertyFlag.NonEnumerable));

        runtime.MediaQueryListPrototype = mediaQueryListPrototype;

        // Before anything reads a property off the global, so no cached lookup predates the new chain.
        global.Prototype = windowPrototype;

        // The window is the engine's global event target, and a document's event path ends at it. Publishing
        // it here rather than in the binding is what keeps the binding free of any notion of a window.
        var windowTarget = engine._webApi?.GlobalEventTarget;
        runtime.Dom.WindowTarget = windowTarget;

        if (windowTarget is not null)
        {
            // This global scope is a `Window`, which is the fact two DOM rules turn on and which no engine
            // without a document can claim: the default passive value
            // (https://dom.spec.whatwg.org/#default-passive-value) and the current event
            // (https://dom.spec.whatwg.org/#window-current-event) the accessor below reads.
            windowTarget.IsWindow = true;

            // `window.event` is an *own* property of the global rather than an accessor on `Window.prototype`,
            // which is where every other Window member here lives. WebIDL's [Global] puts an interface's
            // members on the global object itself, and this is the one a page can tell apart:
            // `dom/events/event-global.html` opens with `assert_own_property(window, "event")`. One property
            // per engine, installed the way `PageStorage` installs its own.
            var scope = windowTarget;
            global.SetProperty(
                "event",
                new GetSetPropertyDescriptor(
                    new ClrFunction(engine, "get event", (_, _) => scope.CurrentEvent),
                    set: null,
                    PropertyFlag.Configurable | PropertyFlag.Enumerable));
        }

        foreach (var name in (string[]) ["window", "self", "frames", "top", "parent"])
        {
            engine.AddLazyGlobal(name, static e => e._mainRealm.GlobalObject);
        }

        engine.AddLazyGlobal("document", static e => (JsValue?) PageRuntime.Find(e)?.DocumentWrapper ?? JsValue.Null);
        engine.AddLazyGlobal("location", static e => LocationOf(e));
        engine.AddLazyGlobal("history", static e => HistoryInstaller.Create(e));
        engine.AddLazyGlobal("screen", static e => _screenShape.Instantiate(e, e._mainRealm.Intrinsics.Object.PrototypeObject));
        // Both interface objects are handed in as state rather than built by the factory, because the one the
        // global names has to be the one the prototype's `constructor` slot already holds.
        engine.AddLazyGlobal("Window", windowInterface, static (_, value) => value, PropertyFlag.NonEnumerable);
        engine.AddLazyGlobal("MediaQueryList", mediaQueryListInterface, static (_, value) => value, PropertyFlag.NonEnumerable);

        // The observers and the views the window carries, each as lazy globals of their own so that a page
        // touching none of them builds none of their prototypes.
        Observers.ObserverInstaller.Install(runtime);
        Dom.Views.ViewInstaller.Install(runtime);

        // The UI event interface objects — MouseEvent, KeyboardEvent and their family — as lazy, non-clobbering
        // globals, the same way DomBindings.Install adds the DOM's.
        BrowserEventRealm.Install(engine);
    }

    /// <summary>
    /// Adds the two document members the binding cannot generate, as own properties of the document wrapper.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>defaultView</c> is not generated because AngleSharp's <c>IWindow</c> is excluded from the binding —
    /// the window is the runtime's. <c>currentScript</c> is generated, but AngleSharp answers the head of its
    /// deferred-script queue rather than the script that is running, so it is <see langword="null"/> for
    /// exactly the case a page uses it in; this own property shadows it with the running script.
    /// </para>
    /// <para>
    /// Both are own properties of <c>document</c> rather than accessors on <c>Document.prototype</c>, which is
    /// where a browser has them. It is one object rather than a shared prototype, so nothing shaped is
    /// deoptimized.
    /// </para>
    /// <para>
    /// They are also <b>non-enumerable</b>, which is deliberately not WebIDL: an interface member is
    /// enumerable, and this package holds every generated member to that. An own enumerable accessor would
    /// show up in <c>Object.keys(document)</c>, in object spread and in <c>JSON.stringify(document)</c>, where
    /// a browser shows nothing because the members are inherited rather than own — and <c>defaultView</c>
    /// would take a conversion of the document into the window and back, which is a cycle. The price is
    /// <c>for..in</c>, which walks the prototype chain and so does yield both names in a browser and neither
    /// here. Moving the two onto <c>Document.prototype</c> settles all four at once and is not a regression.
    /// </para>
    /// </remarks>
    internal static void AttachDocumentMembers(PageRuntime runtime, ObjectInstance wrapper)
    {
        var engine = runtime.Engine;

        wrapper.DefineOwnPropertyUnchecked(
            "defaultView",
            new GetSetPropertyDescriptor(
                new ClrFunction(engine, "get defaultView", static (thisObject, _) => ((ObjectInstance) thisObject).Engine._mainRealm.GlobalObject),
                set: null,
                PropertyFlag.OnlyConfigurable));

        wrapper.DefineOwnPropertyUnchecked(
            "currentScript",
            new GetSetPropertyDescriptor(
                new ClrFunction(engine, "get currentScript", static (thisObject, _) =>
                {
                    var runtime = PageRuntime.Of(thisObject, "currentScript");
                    return runtime.CurrentScript is { } script ? runtime.Dom.WrapNode(script) : JsValue.Null;
                }),
                set: null,
                PropertyFlag.OnlyConfigurable));

        // https://html.spec.whatwg.org/multipage/dom.html#current-document-readiness. AngleSharp advances
        // its own readiness on its own schedule and its setter is not reachable from outside its assembly
        // (AngleSharp#1309), so the three transitions a page observes are the parser driver's.
        wrapper.DefineOwnPropertyUnchecked(
            "readyState",
            new GetSetPropertyDescriptor(
                new ClrFunction(engine, "get readyState", static (thisObject, _) =>
                    JsString.Create(PageRuntime.Of(thisObject, "readyState").ReadyState)),
                set: null,
                PropertyFlag.OnlyConfigurable));

        // The four members whose answer is the page's URL rather than AngleSharp's document address. They
        // are the same divergence `location` is: pushState and a fragment navigation move the URL without
        // reloading, and AngleSharp's address cannot follow without raising its own navigation.
        DocumentUrlMember(engine, wrapper, "URL", static runtime => runtime.DocumentUrl);
        DocumentUrlMember(engine, wrapper, "documentURI", static runtime => runtime.DocumentUrl);
        DocumentUrlMember(engine, wrapper, "baseURI", BaseUri);
        DocumentUrlMember(engine, wrapper, "referrer", static runtime => runtime.Referrer);

        DocumentCookies.Attach(runtime, wrapper);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-node-baseuri — the document's base URL, which <c>&lt;base href&gt;</c>
    /// moves.
    /// </summary>
    /// <remarks>
    /// The first <c>&lt;base&gt;</c> with an <c>href</c> wins, resolved against the document's own URL, and
    /// one that does not parse is ignored. AngleSharp computes the same thing from the same element; it is
    /// recomputed here because the URL it resolves against has to be the page's.
    /// </remarks>
    private static string BaseUri(PageRuntime runtime)
    {
        var href = runtime.Document?.QuerySelector("base[href]")?.GetAttribute("href");
        if (string.IsNullOrEmpty(href))
        {
            return runtime.DocumentUrl;
        }

        return PageUrl.Resolve(href!, runtime.DocumentUrl) ?? runtime.DocumentUrl;
    }

    private static void DocumentUrlMember(Engine engine, ObjectInstance wrapper, string name, Func<PageRuntime, string> read)
    {
        var member = name;

        wrapper.DefineOwnPropertyUnchecked(
            name,
            new GetSetPropertyDescriptor(
                new ClrFunction(engine, "get " + name, (thisObject, _) =>
                    JsString.Create(read(PageRuntime.Of(thisObject, member)))),
                set: null,
                PropertyFlag.OnlyConfigurable));
    }

    /// <summary>Adds every <c>Location</c> member to the location wrapper; see <see cref="LocationInstaller"/>.</summary>
    internal static void AttachLocationMembers(PageRuntime runtime, ObjectInstance wrapper)
        => LocationInstaller.Attach(runtime, wrapper);

    private static JsValue LocationOf(Engine engine)
    {
        var runtime = PageRuntime.Find(engine);
        if (runtime?.Document is not { } document)
        {
            return JsValue.Null;
        }

        var wrapper = runtime.Dom.Wrap(document.Location);
        if (wrapper is ObjectInstance instance && !LocationInstaller.IsInstalled(instance))
        {
            LocationInstaller.Attach(runtime, instance);
        }

        return wrapper;
    }

    private static JsEventTarget WindowTargetOf(JsValue thisObject, string member, string verb)
    {
        if (thisObject is ObjectInstance instance
            && ReferenceEquals(instance, instance.Engine._mainRealm.GlobalObject)
            && instance.Engine._webApi is { } webApi)
        {
            return webApi.GlobalEventTarget;
        }

        if (thisObject is JsEventTarget target)
        {
            return target;
        }

        var message = "Failed to " + verb + " the '" + member + "' property on 'Window': Illegal invocation";

        if (thisObject is ObjectInstance other)
        {
            Throw.TypeError(other.Engine.Realm, message);
        }

        Throw.TypeErrorNoEngine(message);
        return null!;
    }

    /// <summary>
    /// The <c>Window</c> interface: the members a page reads off its window, minus the singletons, which are
    /// own globals instead.
    /// </summary>
    /// <remarks>
    /// <c>event</c> is not here: DOM's <i>current event</i> is the one member a page can tell apart from an
    /// accessor on <c>Window.prototype</c>, because WebIDL's <c>[Global]</c> makes it an own property of the
    /// global object. <see cref="Install"/> installs it there.
    /// </remarks>
    private static JsObjectShape BuildWindowShape()
    {
        var builder = new JsObjectShape.Builder()
            .PerRealmSlot("constructor")
            .ToStringTag("Window")
            .Accessor("innerWidth", static (t, _) => JsNumber.Create(PageRuntime.Of(t, "innerWidth").Viewport.Width))
            .Accessor("innerHeight", static (t, _) => JsNumber.Create(PageRuntime.Of(t, "innerHeight").Viewport.Height))
            .Accessor("outerWidth", static (t, _) => JsNumber.Create(PageRuntime.Of(t, "outerWidth").Viewport.Width))
            .Accessor("outerHeight", static (t, _) => JsNumber.Create(PageRuntime.Of(t, "outerHeight").Viewport.Height))
            .Accessor("devicePixelRatio", static (t, _) => JsNumber.Create(PageRuntime.Of(t, "devicePixelRatio").Viewport.DeviceScaleFactor))
            .Accessor("scrollX", static (_, _) => JsNumber.PositiveZero)
            .Accessor("scrollY", static (t, _) => JsNumber.Create(PageRuntime.Of(t, "scrollY").Layout.ScrollY))
            .Accessor("pageXOffset", static (_, _) => JsNumber.PositiveZero)
            .Accessor("pageYOffset", static (t, _) => JsNumber.Create(PageRuntime.Of(t, "pageYOffset").Layout.ScrollY))
            .Accessor("screenX", static (_, _) => JsNumber.PositiveZero)
            .Accessor("screenY", static (_, _) => JsNumber.PositiveZero)
            .Accessor("screenLeft", static (_, _) => JsNumber.PositiveZero)
            .Accessor("screenTop", static (_, _) => JsNumber.PositiveZero)
            .Accessor("closed", static (t, _) => PageRuntime.Of(t, "closed").Page.IsClosed ? JsBoolean.True : JsBoolean.False)
            .Accessor("length", static (t, _) => JsNumber.Create(FrameCount(PageRuntime.Of(t, "length"))))
            .Accessor("name", static (t, _) => JsString.Create(PageRuntime.Of(t, "name").WindowName),
                static (t, args) =>
                {
                    PageRuntime.Of(t, "name").WindowName = TypeConverter.ToString(args.At(0));
                    return JsValue.Undefined;
                })
            .Accessor("origin", static (t, _) => JsString.Create(PageRuntime.Of(t, "origin").Document?.Origin ?? "null"))
            .Method("stop", static (_, _) => JsValue.Undefined)
            .Method("focus", static (_, _) => JsValue.Undefined)
            .Method("blur", static (_, _) => JsValue.Undefined)
            .Method("close", static (_, _) => JsValue.Undefined)
            .Method("open", static (_, _) => JsValue.Null, length: 3)
            .PerRealmSlot("scrollTo", Operation("scrollTo", 0, static (runtime, args) =>
            {
                runtime.Layout.ScrollTo(ScrollTarget(args, runtime.Layout.ScrollY, absolute: true));
                return JsValue.Undefined;
            }), enumerable: true)
            .PerRealmSlot("scroll", Operation("scroll", 0, static (runtime, args) =>
            {
                runtime.Layout.ScrollTo(ScrollTarget(args, runtime.Layout.ScrollY, absolute: true));
                return JsValue.Undefined;
            }), enumerable: true)
            .PerRealmSlot("scrollBy", Operation("scrollBy", 0, static (runtime, args) =>
            {
                runtime.Layout.ScrollBy(ScrollTarget(args, 0, absolute: false));
                return JsValue.Undefined;
            }), enumerable: true)
            .PerRealmSlot("getSelection", Operation("getSelection", 0, static (runtime, _) => runtime.Views.Selection), enumerable: true)
            .PerRealmSlot("alert", Operation("alert", 1, static (runtime, args) =>
            {
                runtime.Page.RaiseDialog(DialogKind.Alert, Text(args, 0), "");
                return JsValue.Undefined;
            }), enumerable: true)
            .PerRealmSlot("confirm", Operation("confirm", 1, static (runtime, args) =>
            {
                var dialog = runtime.Page.RaiseDialog(DialogKind.Confirm, Text(args, 0), "");
                return dialog.Accepted ? JsBoolean.True : JsBoolean.False;
            }), enumerable: true)
            .PerRealmSlot("prompt", Operation("prompt", 2, static (runtime, args) =>
            {
                var dialog = runtime.Page.RaiseDialog(DialogKind.Prompt, Text(args, 0), Text(args, 1));
                return dialog.Accepted ? JsString.Create(dialog.PromptText) : JsValue.Null;
            }), enumerable: true)
            .PerRealmSlot("matchMedia", Operation("matchMedia", 1, static (runtime, args) =>
                new JsMediaQueryList(runtime, runtime.MediaQueryListPrototype!, TypeConverter.ToString(args.At(0)))), enumerable: true)
            .PerRealmSlot("getComputedStyle", Operation("getComputedStyle", 1, GetComputedStyle), enumerable: true)
            .PerRealmSlot("requestAnimationFrame", Operation("requestAnimationFrame", 1, static (runtime, args) =>
            {
                if (args.At(0) is not ICallable callback)
                {
                    Throw.TypeError(runtime.Engine.Realm, "Failed to execute 'requestAnimationFrame' on 'Window': parameter 1 is not of type 'Function'.");
                    return JsValue.Undefined;
                }

                return JsNumber.Create(runtime.AnimationFrames.Request(callback));
            }), enumerable: true)
            .PerRealmSlot("cancelAnimationFrame", Operation("cancelAnimationFrame", 1, static (runtime, args) =>
            {
                runtime.AnimationFrames.Cancel(TypeConverter.ToInt32(args.At(0)));
                return JsValue.Undefined;
            }), enumerable: true)
            .PerRealmSlot("postMessage", Operation("postMessage", 1, PostMessage), enumerable: true);

        foreach (var type in _eventHandlers)
        {
            var handler = new EventHandlerAccessor(type);
            builder.Accessor("on" + type, handler.Get, handler.Set);
        }

        return builder.Build();
    }

    private static JsObjectShape BuildScreenShape() => new JsObjectShape.Builder()
        .ToStringTag("Screen")
        .Accessor("width", static (t, _) => JsNumber.Create(PageRuntime.Of(t, "width").Viewport.Width))
        .Accessor("height", static (t, _) => JsNumber.Create(PageRuntime.Of(t, "height").Viewport.Height))
        .Accessor("availWidth", static (t, _) => JsNumber.Create(PageRuntime.Of(t, "availWidth").Viewport.Width))
        .Accessor("availHeight", static (t, _) => JsNumber.Create(PageRuntime.Of(t, "availHeight").Viewport.Height))
        .Accessor("availLeft", static (_, _) => JsNumber.PositiveZero)
        .Accessor("availTop", static (_, _) => JsNumber.PositiveZero)
        .Accessor("colorDepth", static (_, _) => JsNumber.Create(24))
        .Accessor("pixelDepth", static (_, _) => JsNumber.Create(24))
        .Build();

    /// <summary>
    /// The <c>MediaQueryList</c> interface. <c>addListener</c> and <c>removeListener</c> are the aliases a
    /// page written before 2018 uses; they forward to <c>EventTarget</c> rather than reimplementing it, so
    /// the listener list stays the one the engine owns.
    /// </summary>
    private static JsObjectShape BuildMediaQueryListShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("MediaQueryList")
        .Accessor("media", static (t, _) => JsString.Create(JsMediaQueryList.Brand(t, "media").Media))
        .Accessor("matches", static (t, _) => JsMediaQueryList.Brand(t, "matches").Matches ? JsBoolean.True : JsBoolean.False)
        .Accessor("onchange",
            static (t, _) => EventHandlerAttributes.Get(JsMediaQueryList.Brand(t, "onchange"), JsMediaQueryList.ChangeEventType),
            static (t, args) => EventHandlerAttributes.Set(JsMediaQueryList.Brand(t, "onchange"), JsMediaQueryList.ChangeEventType, args.At(0)))
        .Method("addListener", static (t, args) => Forward(t, "addEventListener", args), length: 1)
        .Method("removeListener", static (t, args) => Forward(t, "removeEventListener", args), length: 1)
        .Build();

    private static JsValue Forward(JsValue thisObject, string member, JsValue[] arguments)
    {
        var target = JsMediaQueryList.Brand(thisObject, member);
        var method = target.Engine._mainRealm.Intrinsics.EventTarget.PrototypeObject.Get(member);
        return target.Engine.Call(method, thisObject, [JsString.Create(JsMediaQueryList.ChangeEventType), arguments.At(0)]);
    }

    /// <summary>
    /// A window operation that needs its page, declared as a per-realm slot holding a function bound to the
    /// engine rather than as a shape method reading its receiver.
    /// </summary>
    /// <remarks>
    /// A page calls <c>alert(…)</c> and <c>requestAnimationFrame(…)</c> unqualified, and an unqualified call
    /// to a member of the global object passes <see langword="undefined"/> as the receiver — the global
    /// environment record has no <c>with</c> base object. A shape method reading its receiver therefore has
    /// no engine to find the page through and could only answer <c>Illegal invocation</c>. One function per
    /// engine, made on first read of the slot, carries the engine instead.
    /// </remarks>
    private static Func<ObjectInstance, JsValue> Operation(string name, int length, Func<PageRuntime, JsValue[], JsValue> body)
        => prototype =>
        {
            var engine = prototype.Engine;
            var realm = prototype.Engine._mainRealm;
            return new ClrFunction(engine, realm, name, (_, arguments) => body(RuntimeOf(engine, name), arguments), length);
        };

    /// <summary>The page behind an engine, or a <c>TypeError</c> when the engine is not a page's.</summary>
    private static PageRuntime RuntimeOf(Engine engine, string member)
    {
        if (PageRuntime.Find(engine) is { } runtime)
        {
            return runtime;
        }

        Throw.TypeError(engine.Realm, "Failed to execute '" + member + "' on 'Window': Illegal invocation");
        return null!;
    }

    private static string Text(JsValue[] arguments, int index)
    {
        var value = arguments.At(index);
        return value.IsUndefined() ? "" : TypeConverter.ToString(value);
    }

    private static int FrameCount(PageRuntime runtime)
        => runtime.Document is { } document ? document.QuerySelectorAll("iframe, frame").Length : 0;

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-window-scroll — the vertical coordinate the two argument
    /// forms mean, which is <c>(x, y)</c> or a <c>ScrollToOptions</c> dictionary's <c>top</c>.
    /// </summary>
    /// <param name="arguments">What the page passed.</param>
    /// <param name="fallback">What an omitted <c>top</c> means: where the page already is, or no movement.</param>
    /// <param name="absolute">
    /// Whether the answer is a position (<c>scrollTo</c>, <c>scroll</c>) or a delta (<c>scrollBy</c>). It
    /// decides only what an absent <c>top</c> falls back to; both forms read the same member.
    /// </param>
    /// <remarks>
    /// The horizontal half is read and discarded: every box is exactly as wide as the viewport, so there is
    /// never anything to scroll to the side. <c>behavior</c> is likewise accepted and ignored — a page that
    /// asks for a smooth scroll gets the destination immediately, which is the only honest answer with
    /// nothing to animate.
    /// </remarks>
    private static double ScrollTarget(JsValue[] arguments, double fallback, bool absolute)
    {
        var first = arguments.At(0);

        if (first is ObjectInstance options)
        {
            var top = options.Get("top");
            return top.IsUndefined() ? (absolute ? fallback : 0) : TypeConverter.ToNumber(top);
        }

        var y = arguments.At(1);
        return y.IsUndefined() ? (absolute ? fallback : 0) : TypeConverter.ToNumber(y);
    }

    private static JsValue GetComputedStyle(PageRuntime runtime, JsValue[] arguments)
    {
        if (arguments.At(0) is not IDomWrapper { DomTarget: IElement element })
        {
            Throw.TypeError(
                runtime.Engine.Realm,
                "Failed to execute 'getComputedStyle' on 'Window': parameter 1 is not of type 'Element'.");
            return JsValue.Undefined;
        }

        // AngleSharp.Css's cascade, with no layout behind it: specified and computed values resolve, and
        // anything that would need a box — used values, offsetWidth's kind — does not. The pseudo-element
        // argument is ignored, because the extension that takes one needs an AngleSharp IWindow and the
        // window here is Jint's.
        //
        // Wrapped read-only, because CSSOM gives the result a computed flag and AngleSharp's declaration is
        // an ordinary writable one that is also detached — so an unwrapped write would neither throw nor
        // change anything a page can read. See Dom/Views/ReadOnlyStyleDeclaration.
        return runtime.Dom.Wrap(new Dom.Views.ReadOnlyStyleDeclaration(runtime.Engine, element.ComputeCurrentStyle()));
    }

    private static JsValue PostMessage(PageRuntime runtime, JsValue[] arguments)
    {
        var engine = runtime.Engine;
        var realm = engine._mainRealm;

        // https://html.spec.whatwg.org/multipage/web-messaging.html#window-post-message-steps step 8: the
        // message is serialized now, in the caller's turn, and deserialized into the event later — so a
        // mutation between the two is not observed by the listener.
        var message = StructuredCloner.Clone(engine, realm, arguments.At(0), transferList: null);
        var origin = JsString.Create(runtime.Document?.Origin ?? "");

        engine.Tasks.Post(() =>
        {
            var target = engine._webApi?.GlobalEventTarget;
            if (target is null)
            {
                return;
            }

            target.DispatchEvent(realm.Intrinsics.MessageEvent.CreateTrustedMessageEvent(
                JsString.Create("message"),
                message,
                origin,
                JsString.Empty));
        });

        return JsValue.Undefined;
    }

    /// <summary>One <c>on<i>type</i></c> attribute pair, holding the event type and nothing engine-specific.</summary>
    private sealed class EventHandlerAccessor
    {
        private readonly string _type;

        internal EventHandlerAccessor(string type)
        {
            _type = type;
        }

        internal JsValue Get(JsValue thisObject, JsValue[] arguments)
            => EventHandlerAttributes.Get(WindowTargetOf(thisObject, "on" + _type, "read"), _type);

        internal JsValue Set(JsValue thisObject, JsValue[] arguments)
            => EventHandlerAttributes.Set(WindowTargetOf(thisObject, "on" + _type, "set"), _type, arguments.At(0));
    }

    /// <summary>The global <c>Window</c>, so that <c>window instanceof Window</c> holds.</summary>
    private sealed class WindowInterfaceObject : Constructor
    {
        internal WindowInterfaceObject(Engine engine, Realm realm, ObjectInstance prototype)
            : base(engine, realm, new JsString("Window"))
        {
            // Object.getPrototypeOf(Window) === EventTarget, as WebIDL says for an interface that inherits.
            _prototype = realm.Intrinsics.EventTarget;
            _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
            _prototypeDescriptor = new PropertyDescriptor(prototype, PropertyFlag.AllForbidden);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            Throw.TypeError(_realm, "Illegal constructor");
            return JsValue.Undefined;
        }

        public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            Throw.TypeError(_realm, "Illegal constructor");
            return null!;
        }

        public override string ToString() => "function Window() { [native code] }";
    }

    /// <summary>The global <c>MediaQueryList</c>, for the same reason.</summary>
    private sealed class MediaQueryListInterfaceObject : Constructor
    {
        internal MediaQueryListInterfaceObject(Engine engine, Realm realm, ObjectInstance prototype)
            : base(engine, realm, new JsString("MediaQueryList"))
        {
            _prototype = realm.Intrinsics.EventTarget;
            _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
            _prototypeDescriptor = new PropertyDescriptor(prototype, PropertyFlag.AllForbidden);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            Throw.TypeError(_realm, "Illegal constructor");
            return JsValue.Undefined;
        }

        public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            Throw.TypeError(_realm, "Illegal constructor");
            return null!;
        }

        public override string ToString() => "function MediaQueryList() { [native code] }";
    }
}
