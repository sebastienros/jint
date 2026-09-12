using Jint.Browser.Dom.Collections;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;

namespace Jint.Browser.Events;

/// <summary>
/// One interface the events bridge owns that is <b>not</b> an <c>Event</c> — <c>Touch</c>, <c>TouchList</c>
/// and the two <c>DeviceMotionEvent</c> readings.
/// </summary>
/// <remarks>
/// <see cref="BrowserEventDefinition"/> cannot carry them: its prototype chain roots at the engine's own
/// <c>Event</c> and its interface object inherits the parent interface's, both of which are wrong for an
/// interface whose <c>[[Prototype]]</c> is <c>%Object.prototype%</c> and <c>%Function.prototype%</c>. What
/// they share with it is everything else — a process-shared shape built at most once, a per-engine prototype
/// and interface object indexed by <see cref="Index"/>, and a lazy non-clobbering global.
/// </remarks>
internal sealed class BrowserHostInterface
{
    private readonly Func<JsObjectShape> _shapeFactory;
    private JsObjectShape? _shape;

    internal BrowserHostInterface(
        string name,
        int constructorLength,
        Func<JsObjectShape> shapeFactory,
        Func<BrowserEventRealm, JsValue[], ObjectInstance>? construct)
    {
        Name = name;
        ConstructorLength = constructorLength;
        _shapeFactory = shapeFactory;
        Construct = construct;
    }

    /// <summary>The WebIDL interface name — <c>TouchList</c>.</summary>
    internal string Name { get; }

    /// <summary>The interface object's <c>length</c>.</summary>
    internal int ConstructorLength { get; }

    /// <summary>The interface's own members. Built at most once per process, on first use.</summary>
    internal JsObjectShape Shape => _shape ??= _shapeFactory();

    /// <summary>
    /// The constructor's body, or <see langword="null"/> for an interface WebIDL gives no constructor —
    /// which <see cref="Dom.HostInterfaceObject"/> answers with <c>Illegal constructor</c>, as a browser does.
    /// </summary>
    internal Func<BrowserEventRealm, JsValue[], ObjectInstance>? Construct { get; }

    /// <summary>Dense index into <see cref="BrowserEventRealm"/>'s per-engine arrays.</summary>
    internal int Index { get; set; } = -1;

    public override string ToString() => Name;
}

/// <summary>
/// The four non-<c>Event</c> interfaces the events bridge builds: Touch Events' <c>Touch</c> and
/// <c>TouchList</c>, and the two readings a <c>DeviceMotionEvent</c> carries.
/// </summary>
internal static class BrowserHostInterfaces
{
    /// <summary>https://w3c.github.io/touch-events/#touch-interface.</summary>
    internal static readonly BrowserHostInterface Touch = new(
        "Touch",
        constructorLength: 1,
        BuildTouch,
        static (realm, args) => realm.NewTouch(EventInitReader.TouchInit(realm, args)));

    /// <summary>https://w3c.github.io/touch-events/#touchlist-interface.</summary>
    internal static readonly BrowserHostInterface TouchList = new(
        "TouchList",
        constructorLength: 0,
        BuildTouchList,
        construct: null);

    /// <summary>https://w3c.github.io/deviceorientation/#devicemotioneventacceleration.</summary>
    internal static readonly BrowserHostInterface DeviceMotionEventAcceleration = new(
        "DeviceMotionEventAcceleration",
        constructorLength: 0,
        BuildDeviceMotionEventAcceleration,
        construct: null);

    /// <summary>https://w3c.github.io/deviceorientation/#devicemotioneventrotationrate.</summary>
    internal static readonly BrowserHostInterface DeviceMotionEventRotationRate = new(
        "DeviceMotionEventRotationRate",
        constructorLength: 0,
        BuildDeviceMotionEventRotationRate,
        construct: null);

    /// <summary>Every one of them, in the order <see cref="BrowserEventRealm"/>'s arrays are sized by.</summary>
    internal static readonly BrowserHostInterface[] All =
    [
        Touch,
        TouchList,
        DeviceMotionEventAcceleration,
        DeviceMotionEventRotationRate,
    ];

    static BrowserHostInterfaces()
    {
        for (var i = 0; i < All.Length; i++)
        {
            All[i].Index = i;
        }
    }

    private static JsObjectShape BuildTouch() => Base("Touch")
        .Accessor("identifier", static (t, _) => JsNumber.Create(Brand(t, "Touch.identifier").Identifier))
        .Accessor("target", static (t, _) => Brand(t, "Touch.target").Target)
        .Accessor("screenX", static (t, _) => JsNumber.Create(Brand(t, "Touch.screenX").ScreenX))
        .Accessor("screenY", static (t, _) => JsNumber.Create(Brand(t, "Touch.screenY").ScreenY))
        .Accessor("clientX", static (t, _) => JsNumber.Create(Brand(t, "Touch.clientX").ClientX))
        .Accessor("clientY", static (t, _) => JsNumber.Create(Brand(t, "Touch.clientY").ClientY))
        .Accessor("pageX", static (t, _) => JsNumber.Create(Brand(t, "Touch.pageX").PageX))
        .Accessor("pageY", static (t, _) => JsNumber.Create(Brand(t, "Touch.pageY").PageY))
        .Accessor("radiusX", static (t, _) => JsNumber.Create(Brand(t, "Touch.radiusX").RadiusX))
        .Accessor("radiusY", static (t, _) => JsNumber.Create(Brand(t, "Touch.radiusY").RadiusY))
        .Accessor("rotationAngle", static (t, _) => JsNumber.Create(Brand(t, "Touch.rotationAngle").RotationAngle))
        .Accessor("force", static (t, _) => JsNumber.Create(Brand(t, "Touch.force").Force))
        .Build();

    /// <remarks>
    /// https://webidl.spec.whatwg.org/#js-iterable — an interface that supports indexed properties carries
    /// <c>@@iterator</c>, and its value is the realm's own <c>%Array.prototype.values%</c>. It is the same
    /// per-realm slot the generated collections take, from the same factory.
    /// </remarks>
    private static JsObjectShape BuildTouchList() => Base("TouchList")
        .PerRealmSlot(GlobalSymbolRegistry.Iterator, DomIterator.ArrayValues)
        .Accessor("length", static (t, _) => JsNumber.Create(BrowserEventInterfaces.Brand<JsTouchList>(t, "TouchList.length").Length))
        .Method("item", static (t, args) => BrowserEventInterfaces.Brand<JsTouchList>(t, "TouchList.item").Item(args), length: 1)
        .Build();

    private static JsObjectShape BuildDeviceMotionEventAcceleration() => Base("DeviceMotionEventAcceleration")
        .Accessor("x", static (t, _) => Optional(BrowserEventInterfaces.Brand<JsDeviceMotionAcceleration>(t, "DeviceMotionEventAcceleration.x").X))
        .Accessor("y", static (t, _) => Optional(BrowserEventInterfaces.Brand<JsDeviceMotionAcceleration>(t, "DeviceMotionEventAcceleration.y").Y))
        .Accessor("z", static (t, _) => Optional(BrowserEventInterfaces.Brand<JsDeviceMotionAcceleration>(t, "DeviceMotionEventAcceleration.z").Z))
        .Build();

    private static JsObjectShape BuildDeviceMotionEventRotationRate() => Base("DeviceMotionEventRotationRate")
        .Accessor("alpha", static (t, _) => Optional(BrowserEventInterfaces.Brand<JsDeviceMotionRotationRate>(t, "DeviceMotionEventRotationRate.alpha").Alpha))
        .Accessor("beta", static (t, _) => Optional(BrowserEventInterfaces.Brand<JsDeviceMotionRotationRate>(t, "DeviceMotionEventRotationRate.beta").Beta))
        .Accessor("gamma", static (t, _) => Optional(BrowserEventInterfaces.Brand<JsDeviceMotionRotationRate>(t, "DeviceMotionEventRotationRate.gamma").Gamma))
        .Build();

    private static JsObjectShape.Builder Base(string name)
        => new JsObjectShape.Builder()
            .ToStringTag(name)
            .PerRealmSlot("constructor", enumerable: false);

    private static JsTouch Brand(JsValue thisObject, string member)
        => BrowserEventInterfaces.Brand<JsTouch>(thisObject, member);

    /// <summary>A <c>double?</c> IDL attribute: the reading, or <c>null</c> where there is none.</summary>
    private static JsValue Optional(double? value) => value is null ? JsValue.Null : JsNumber.Create(value.Value);
}
