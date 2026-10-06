using Jint.Browser.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi.Streams;

namespace Jint.Browser.Navigation;

/// <summary>Lazy per-realm interfaces, https://html.spec.whatwg.org/multipage/nav-history-apis.html#navigation-api.</summary>
internal sealed class NavigationRealm(DomRealm dom)
{
    internal static readonly string[] InterfaceNames =
        ["Navigation", "NavigationHistoryEntry", "NavigationDestination", "NavigationTransition", "NavigationActivation"];

    private readonly Dictionary<string, (ObjectInstance Prototype, HostInterfaceObject Interface)> _interfaces = new(StringComparer.Ordinal);
    private JsNavigation? _window;

    internal DomRealm Dom { get; } = dom;
    internal Engine Engine => Dom.Engine;
    internal Realm Realm => Dom.OwningRealm;
    internal JsNavigation Window => _window ??= new JsNavigation(this);
    internal JsNavigation? ExistingWindow => _window;

    internal ObjectInstance Prototype(string name) => Interface(name).Prototype;
    internal JsValue InterfaceObject(string name) => Interface(name).Interface;

    private (ObjectInstance Prototype, HostInterfaceObject Interface) Interface(string name)
    {
        if (_interfaces.TryGetValue(name, out var value)) return value;
        using var scope = new RealmScope(Engine, Realm);
        var eventTarget = name is "Navigation" or "NavigationHistoryEntry";
        var prototype = NavigationShapes.For(name).Instantiate(Engine,
            eventTarget ? Realm.Intrinsics.EventTarget.PrototypeObject : Realm.Intrinsics.Object.PrototypeObject);
        if (name == "NavigationTransition")
        {
            InstallPromiseAccessor(prototype, "committed", static transition => transition.Committed.PromiseInstance);
            InstallPromiseAccessor(prototype, "finished", static transition => transition.Finished.PromiseInstance);
        }
        var iface = new HostInterfaceObject(Engine, Realm, name, prototype, 0, construct: null,
            eventTarget ? Realm.Intrinsics.EventTarget : null);
        prototype.DefineOwnPropertyUnchecked("constructor", new PropertyDescriptor(iface, PropertyFlag.NonEnumerable));
        value = (prototype, iface);
        _interfaces.Add(name, value);
        return value;
    }

    /// <summary>https://webidl.spec.whatwg.org/#dfn-attribute-getter - promise attributes reject brand errors.</summary>
    private void InstallPromiseAccessor(ObjectInstance prototype, string name, Func<JsNavigationTransition, JsValue> getter)
    {
        prototype.DefineOwnPropertyUnchecked(name, new GetSetPropertyDescriptor(
            new ClrFunction(Engine, Realm, "get " + name, (receiver, _) =>
                receiver is JsNavigationTransition transition ? getter(transition)
                    : StreamPromises.RejectedWith(Engine, Realm, Realm.Intrinsics.TypeError.Construct("Illegal invocation.")), 0),
            null, PropertyFlag.Configurable | PropertyFlag.Enumerable));
    }

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-navigation - [Replaceable].</summary>
    internal static void Install(DomRealm dom)
    {
        var realm = dom.OwningRealm;
        realm.GlobalObject.SetProperty("navigation", new GetSetPropertyDescriptor(
            new ClrFunction(dom.Engine, realm, "get navigation", (receiver, _) =>
            {
                if (!ReferenceEquals(receiver, realm.GlobalObject)) Throw.TypeError(realm, "Illegal invocation of Window.navigation");
                return dom.Navigation.Window;
            }, 0),
            new ClrFunction(dom.Engine, realm, "set navigation", (receiver, args) =>
            {
                if (!ReferenceEquals(receiver, realm.GlobalObject)) Throw.TypeError(realm, "Illegal invocation of Window.navigation");
                realm.GlobalObject.DefineOwnProperty("navigation", new PropertyDescriptor(args.At(0), PropertyFlag.ConfigurableEnumerableWritable));
                return JsValue.Undefined;
            }, 1), PropertyFlag.Configurable | PropertyFlag.Enumerable));
    }
}
