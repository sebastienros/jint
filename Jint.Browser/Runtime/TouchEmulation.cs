using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Browser.Runtime;

/// <summary>
/// The one thing touch emulation adds that is not a value somebody reads: the presence of
/// <c>ontouchstart</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>'ontouchstart' in window</c> is the test, and it is a presence test.</b> Modernizr, every responsive
/// framework and a great deal of hand-written code branch on it — usually beside
/// <c>navigator.maxTouchPoints &gt; 0</c>, which <see cref="NavigatorInstaller"/> answers — so touch
/// emulation that moved the second and not the first would leave half the world unconvinced. The property is
/// added to the global object and to the <c>document</c> wrapper, and taken away again when a client turns
/// touch emulation off.
/// </para>
/// <para>
/// <b>It is not added to <c>Element.prototype</c>, which a browser also carries it on.</b> The DOM
/// prototypes are generated shaped objects shared with the inline caches, and a property the shape did not
/// declare deoptimizes the prototype for the whole document — a real cost for a test almost nothing writes
/// (<c>'ontouchstart' in document.documentElement</c>). It is a stated gap rather than an oversight.
/// </para>
/// <para>
/// <b>No touch event is ever dispatched.</b> There is no touch input in this browser — <c>Input</c> is the
/// mouse and the keyboard — so what touch emulation changes is what a page <em>detects</em>, not what it
/// receives. A handler assigned to <c>window.ontouchstart</c> is stored and never called, exactly as a
/// listener for <c>touchstart</c> is. That is the honest shape of the trade and a client that asked for
/// touch emulation is the one that asked for it: a page which then binds only touch handlers hears nothing
/// from <c>Input.dispatchMouseEvent</c>.
/// </para>
/// </remarks>
internal static class TouchEmulation
{
    /// <summary>The one event-handler content attribute whose presence decides the question.</summary>
    private const string Handler = "ontouchstart";

    /// <summary>Brings the page's <c>ontouchstart</c> in line with what a client asked for.</summary>
    /// <remarks>
    /// Called when an engine is built, when a document's wrapper is created, and whenever
    /// <c>Emulation.setTouchEmulationEnabled</c> arrives — all three on the page loop.
    /// </remarks>
    internal static void Apply(PageRuntime runtime)
    {
        var enabled = runtime.Emulation.TouchEnabled;
        var global = runtime.Engine._mainRealm.GlobalObject;

        if (enabled)
        {
            // SetProperty rather than an unchecked define, because the global object's own-property version
            // is what the global-identifier inline cache revalidates against: a binding installed any other
            // way would leave a warmed read site answering `undefined` forever.
            global.SetProperty(Handler, new PropertyDescriptor(JsValue.Null, PropertyFlag.ConfigurableEnumerableWritable));
        }
        else if (global.HasOwnProperty(Handler))
        {
            // Only when there is one to take away: removing bumps that same version, and doing it for every
            // engine a browser builds would invalidate the caches of a page nobody emulated anything on.
            global.RemoveOwnProperty(Handler);
        }

        if (runtime.DocumentWrapper is { } document)
        {
            ApplyTo(document, enabled);
        }
    }

    /// <summary>Brings one freshly created wrapper in line, which is what a new document needs.</summary>
    internal static void Attach(PageRuntime runtime, ObjectInstance wrapper)
        => ApplyTo(wrapper, runtime.Emulation.TouchEnabled);

    private static void ApplyTo(ObjectInstance target, bool enabled)
    {
        if (enabled)
        {
            target.DefineOwnPropertyUnchecked(
                Handler,
                new PropertyDescriptor(JsValue.Null, PropertyFlag.ConfigurableEnumerableWritable));
        }
        else if (target.HasOwnProperty(Handler))
        {
            target.RemoveOwnProperty(Handler);
        }
    }
}
