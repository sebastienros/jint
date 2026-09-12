using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Browser.Dom;
using Jint.Browser.Events;

namespace Jint.Browser.Runtime;

/// <summary>
/// What touch emulation adds that is not a value somebody reads: the four event handler IDL attributes Touch
/// Events puts on <c>GlobalEventHandlers</c>, and with them the presence of <c>ontouchstart</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>'ontouchstart' in window</c> is the test, and it is a presence test.</b> Modernizr, every responsive
/// framework and a great deal of hand-written code branch on it — usually beside
/// <c>navigator.maxTouchPoints &gt; 0</c>, which <see cref="NavigatorInstaller"/> answers — so touch
/// emulation that moved the second and not the first would leave half the world unconvinced. The four
/// properties are added to the global object and to the <c>document</c> wrapper, and taken away again when a
/// client turns touch emulation off.
/// </para>
/// <para>
/// They are also added to <c>Element.prototype</c>, which is where every element inherits them from.
/// (<a href="https://w3c.github.io/touch-events/#element-touchstart">Touch Events §5.4</a> extends
/// <c>GlobalEventHandlers</c>, so HTML's own mixin puts them on <c>HTMLElement</c> and SVG's on
/// <c>SVGElement</c>; one declaration on the interface both inherit from is the same answer for every element
/// a page can touch, and there is no third one.) That prototype's shared shape serves its generated slots
/// while these conditional properties live in the shape's hybrid side dictionary, so enabling touch does not
/// give up prototype-method inline caching. Disabling touch removes those side entries directly and likewise
/// keeps the shared layout intact.
/// </para>
/// <para>
/// <b>They are real handler slots, not markers.</b> Each is the get/set pair HTML defines for an event
/// handler IDL attribute — the same one <c>onclick</c> has, over the same event handler map — so
/// <c>window.ontouchstart = f</c> is a listener that runs when <c>Input.dispatchTouchEvent</c> delivers a
/// touch, and reading it back answers the function. A property that existed only to be detected would tell a
/// framework this is a touch device and then drop the handler it went on to assign.
/// </para>
/// <para>
/// <b>This decides what a page detects, and nothing else.</b> Touch events are dispatched by
/// <c>Input.dispatchTouchEvent</c> (<c>Events/InputDispatcher.Touch</c>) and that command does not consult
/// this flag: a client that sends a touch is asking for one, and a page that added a <c>touchstart</c>
/// listener hears it whether or not anybody said the device has a digitizer. What emulation adds is the half
/// a page can <em>ask about</em> before deciding to listen at all — which is why turning it off leaves
/// <c>window.ontouchstart</c> absent while a touch dispatched into that page still arrives. The
/// <c>ontouchstart="…"</c> <i>content</i> attribute is unconditional for the same reason
/// (<c>Events/EventHandlerContentAttributes</c>): markup a page wrote is not a capability probe.
/// </para>
/// <para>
/// The pairing is the client's to make, and it is the pairing a real device has: a browser with a touch
/// screen both reports one and delivers touches. A client driving a responsive page sends
/// <c>Emulation.setTouchEmulationEnabled</c> and then taps; one testing a page's mouse path taps without it
/// and gets the compatibility mouse events a tap leaves behind either way.
/// </para>
/// </remarks>
internal static class TouchEmulation
{
    /// <summary>
    /// https://w3c.github.io/touch-events/#element-touchstart — the four the specification adds, of which the
    /// first is the one whose presence decides the question every framework asks.
    /// </summary>
    private static readonly string[] _handlers = EventHandlerContentAttributes.TouchHandlers;

    /// <summary>
    /// What <c>Emulation.setTouchEmulationEnabled</c> and <see cref="Page.SetTouchEmulationAsync"/> both do:
    /// record what the client asked for, then bring the document that is already loaded in line with it.
    /// </summary>
    /// <remarks>
    /// One implementation with two entry points, so a host in this process and a protocol client on the
    /// socket cannot leave a page in two different states. Called on the page loop by both.
    /// </remarks>
    internal static void Set(EmulationState state, PageRuntime? runtime, bool enabled, int maxTouchPoints)
    {
        state.TouchEnabled = enabled;
        state.MaxTouchPoints = maxTouchPoints;

        if (runtime is not null)
        {
            Apply(runtime);

            // The media environment reads TouchEnabled per get, but a MediaQueryList only tells its listeners
            // when the page announces a change — so `(pointer: coarse)` moving is a notification somebody owes.
            runtime.SetMedia(state.MediaEnvironment);
        }
    }

    /// <summary>Brings the page's touch handler attributes in line with what a client asked for.</summary>
    /// <remarks>
    /// Called when an engine is built, when a document's wrapper is created, and whenever
    /// <c>Emulation.setTouchEmulationEnabled</c> arrives — all three on the page loop.
    /// </remarks>
    internal static void Apply(PageRuntime runtime)
    {
        var enabled = runtime.Emulation.TouchEnabled;
        var engine = runtime.Engine;
        var global = engine._mainRealm.GlobalObject;

        foreach (var type in _handlers)
        {
            var name = "on" + type;

            if (enabled)
            {
                // SetProperty rather than an unchecked define, because the global object's own-property
                // version is what the global-identifier inline cache revalidates against: a binding installed
                // any other way would leave a warmed read site answering `undefined` forever.
                global.SetProperty(name, WindowHandler(engine, type));
            }
            else if (global.HasOwnProperty(name))
            {
                // Only when there is one to take away: removing bumps that same version, and doing it for
                // every engine a browser builds would invalidate the caches of a page nobody emulated
                // anything on.
                global.RemoveOwnProperty(name);
            }
        }

        if (runtime.DocumentWrapper is { } document)
        {
            ApplyTo(engine, document, enabled);
        }

        if (enabled)
        {
            ApplyTo(engine, runtime.Dom.PrototypeOf(DomInterfaces.Element), enabled: true);
        }
        else if (runtime.Dom.ExistingPrototypeOf(DomInterfaces.Element) is { } elementPrototype)
        {
            ApplyTo(engine, elementPrototype, enabled: false);
        }
    }

    /// <summary>Brings one freshly created wrapper in line, which is what a new document needs.</summary>
    internal static void Attach(PageRuntime runtime, ObjectInstance wrapper)
        => ApplyTo(runtime.Engine, wrapper, runtime.Emulation.TouchEnabled);

    private static void ApplyTo(Engine engine, ObjectInstance target, bool enabled)
    {
        foreach (var type in _handlers)
        {
            var name = "on" + type;

            if (enabled)
            {
                target.DefineOwnPropertyUnchecked(name, NodeHandler(engine, type));
            }
            else if (target.HasOwnProperty(name))
            {
                target.RemoveOwnProperty(name);
            }
        }
    }

    /// <summary>
    /// The window's pair, which is <c>WindowInstaller</c>'s own — the handler belongs to the global event
    /// target, and none of these four is a name HTML redirects there from a <c>&lt;body&gt;</c>.
    /// </summary>
    private static GetSetPropertyDescriptor WindowHandler(Engine engine, string type)
    {
        var accessor = new WindowInstaller.EventHandlerAccessor(type);
        return Pair(engine, type, accessor.Get, accessor.Set);
    }

    /// <summary>
    /// A node's pair, which is <c>DomShapeAdditions</c>' own: the element or document the receiver wraps owns
    /// the slot, and a wrong receiver is the same <c>Illegal invocation</c> every other handler answers with.
    /// </summary>
    private static GetSetPropertyDescriptor NodeHandler(Engine engine, string type)
        => Pair(
            engine,
            type,
            (thisObject, _) => Receiver(thisObject, type) is { } wrapper
                ? EventHandlerContentAttributes.Get(wrapper, type)
                : JsValue.Undefined,
            (thisObject, arguments) => Receiver(thisObject, type) is { } wrapper
                ? EventHandlerContentAttributes.Set(wrapper, type, arguments.At(0))
                : JsValue.Undefined);

    /// <summary>
    /// The node whose handler map a get or a set is about, refusing anything else the way every other DOM
    /// member does.
    /// </summary>
    /// <remarks>
    /// <see cref="DomBindings.Bind{T}"/> is what raises <c>Illegal invocation</c> — for the interface
    /// prototype itself, which is the receiver a page reaches these through when it is feature-detecting
    /// rather than using them. It never returns for a receiver that is not a node wrapper, so the pattern
    /// below is a compiler's requirement rather than a second check.
    /// </remarks>
    private static DomNodeObject? Receiver(JsValue thisObject, string type)
    {
        DomBindings.Bind<AngleSharp.Dom.INode>(thisObject, "on" + type);
        return thisObject as DomNodeObject;
    }

    /// <summary>
    /// One event handler IDL attribute, as WebIDL declares every attribute of an interface: an enumerable,
    /// configurable accessor pair.
    /// </summary>
    private static GetSetPropertyDescriptor Pair(
        Engine engine,
        string type,
        Func<JsValue, JsValue[], JsValue> get,
        Func<JsValue, JsValue[], JsValue> set)
        => new GetSetPropertyDescriptor(
            new ClrFunction(engine, "get on" + type, get, length: 0),
            new ClrFunction(engine, "set on" + type, set, length: 1),
            PropertyFlag.Configurable | PropertyFlag.Enumerable);
}
