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
/// <c>window.customElements</c> itself is an accessor on <c>Window.prototype</c> rather than a global of its
/// own, which is what WebIDL's <c>[SameObject] readonly attribute</c> asks for — and an accessor is safe
/// there where a shape <i>method</i> would not be, because a bare identifier read goes through the global
/// object's <c>[[Get]]</c> with the global as the receiver. <c>Runtime/AGENTS.md</c> has that trap in full.
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

    /// <summary>Installs the <c>CustomElementRegistry</c> global. Called once, with the window.</summary>
    internal static void Install(PageRuntime runtime)
        => runtime.Engine.AddLazyGlobal(
            "CustomElementRegistry",
            static e => PageRuntime.Find(e)!.CustomElements.InterfaceObject,
            PropertyFlag.NonEnumerable);

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
