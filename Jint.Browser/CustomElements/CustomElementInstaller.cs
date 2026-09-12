using Jint.Browser.Dom;
using Jint.Browser.Observers;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Runtime.Descriptors;

namespace Jint.Browser.CustomElements;

/// <summary>
/// The <c>CustomElementRegistry</c> interface: one shape for the process, one prototype and one interface
/// object per engine, and the global that names it.
/// </summary>
/// <remarks>
/// <para>
/// <c>window.customElements</c> is an <b>own</b> property of the global object rather than an accessor on
/// <c>Window.prototype</c>, because WebIDL's <c>[Global]</c> puts an interface's members on the global object
/// itself and this is one of the two a page can tell apart — <c>window.event</c> is the other, and
/// <c>Runtime/AGENTS.md</c> says why the rest stay on the shaped prototype. A page that saves
/// <c>Object.getOwnPropertyDescriptor(window, "customElements")</c>, replaces the global and puts the
/// descriptor back is the shape that noticed: with nothing own there, the descriptor was
/// <see langword="undefined"/> and the restore threw.
/// </para>
/// <para>
/// It is a <b>lazy</b> global, so a document that never mentions <c>customElements</c> still builds no
/// registry: the factory runs on the first read and not before.
/// </para>
/// </remarks>
internal static class CustomElementInstaller
{
    private static readonly JsObjectShape _shape = BuildShape();

    /// <summary>Builds the registry of one engine, together with its prototype and interface object.</summary>
    internal static CustomElementRegistry Create(PageRuntime runtime)
    {
        var prototype = ObserverInstaller.Instantiate(
            runtime.Engine,
            _shape,
            "CustomElementRegistry",
            length: 0,
            construct: null,
            out var interfaceObject);

        return new CustomElementRegistry(runtime, prototype, interfaceObject);
    }

    /// <summary>
    /// Installs the <c>CustomElementRegistry</c> interface object and the <c>customElements</c> global.
    /// Called once, with the window.
    /// </summary>
    /// <remarks>
    /// The interface object is <c>NonEnumerable</c> as every interface object on the global is; the attribute
    /// is enumerable, as every WebIDL member is.
    /// </remarks>
    internal static void Install(PageRuntime runtime)
    {
        var engine = runtime.Engine;

        engine.AddLazyGlobal(
            "CustomElementRegistry",
            static e => PageRuntime.Find(e)!.CustomElements.InterfaceObject,
            PropertyFlag.NonEnumerable);

        engine.AddLazyGlobal("customElements", static e => PageRuntime.Find(e)!.CustomElements);
    }

    /// <summary>The interface's shape, which is five operations and no attribute at all.</summary>
    private static JsObjectShape BuildShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("CustomElementRegistry")
        .Method("define", static (t, args) => CustomElementRegistry.Brand(t, "define").Define(args), length: 2)
        .Method("get", static (t, args) => CustomElementRegistry.Brand(t, "get").Get(args), length: 1)
        .Method("getName", static (t, args) => CustomElementRegistry.Brand(t, "getName").GetName(args), length: 1)
        .Method("whenDefined", static (t, args) => CustomElementRegistry.Brand(t, "whenDefined").WhenDefined(args), length: 1)
        .Method("upgrade", static (t, args) => CustomElementRegistry.Brand(t, "upgrade").Upgrade(args), length: 1)
        .Build();
}
