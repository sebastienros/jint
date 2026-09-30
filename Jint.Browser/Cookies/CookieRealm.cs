using Jint.Browser.Dom;
using Jint.Browser.SystemState;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi.Events;
using Jint.WebApi.Streams;

namespace Jint.Browser.Cookies;

/// <summary>Per-window interfaces and singleton: https://cookiestore.spec.whatwg.org/#CookieStore.</summary>
internal sealed class CookieRealm
{
    internal static readonly string[] InterfaceNames = ["CookieStore"];
    private static readonly JsObjectShape _shape = new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("CookieStore")
        .PerRealmSlot("get", static p => Operation(p, "get", 0), enumerable: true)
        .PerRealmSlot("getAll", static p => Operation(p, "getAll", 0), enumerable: true)
        .PerRealmSlot("set", static p => Operation(p, "set", 1), enumerable: true)
        .PerRealmSlot("delete", static p => Operation(p, "delete", 1), enumerable: true)
        .Accessor("onchange",
            static (t, _) => EventHandlerAttributes.Get(SystemBrand.Of<JsCookieStore>(t, "CookieStore", "onchange"), "change"),
            static (t, args) => EventHandlerAttributes.Set(SystemBrand.Of<JsCookieStore>(t, "CookieStore", "onchange"), "change", args.At(0)))
        .Build();

    private ObjectInstance? _prototype;
    private HostInterfaceObject? _interface;
    private JsCookieStore? _store;

    internal CookieRealm(DomRealm dom) => Dom = dom;
    internal DomRealm Dom { get; }
    internal JsCookieStore Store => _store ??= new JsCookieStore(this);
    internal ObjectInstance Prototype
    {
        get
        {
            if (_prototype is null)
            {
                var realm = Dom.OwningRealm;
                using var scope = new RealmScope(Dom.Engine, realm);
                _prototype = _shape.Instantiate(Dom.Engine, realm.Intrinsics.EventTarget.PrototypeObject);
                JsObjectShape.SetHostState(_prototype, Dom);
                _interface = new HostInterfaceObject(Dom.Engine, realm, "CookieStore", _prototype, 0,
                    parent: realm.Intrinsics.EventTarget);
                _prototype.DefineOwnPropertyUnchecked("constructor", new PropertyDescriptor(_interface, PropertyFlag.NonEnumerable));
            }
            return _prototype;
        }
    }

    internal JsValue InterfaceObject(string name)
    {
        _ = Prototype;
        return _interface!;
    }

    /// <summary>https://webidl.spec.whatwg.org/#es-operations - even invalid receivers reject promises.</summary>
    private static ClrFunction Operation(ObjectInstance prototype, string name, int length)
    {
        var dom = (DomRealm) JsObjectShape.GetHostState(prototype)!;
        var realm = dom.OwningRealm;
        return new ClrFunction(dom.Engine, realm, name, (receiver, args) =>
        {
            try
            {
                if (receiver is not JsCookieStore)
                {
                    Throw.TypeError(realm, "Illegal invocation of CookieStore." + name);
                }
                return SystemBrand.Of<JsCookieStore>(receiver, "CookieStore", name).Invoke(name, args);
            }
            catch (JavaScriptException exception)
            {
                return StreamPromises.RejectedWith(dom.Engine, realm, exception.Error);
            }
            catch (TypeErrorException exception)
            {
                return StreamPromises.RejectedWith(dom.Engine, realm, realm.Intrinsics.TypeError.Construct(exception.Message));
            }
        }, length);
    }
}
