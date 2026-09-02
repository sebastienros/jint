using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Browser.Observers;

/// <summary>
/// The five observer interfaces a page reaches through its window: <c>MutationObserver</c>,
/// <c>IntersectionObserver</c>, <c>IntersectionObserverEntry</c>, <c>ResizeObserver</c> and
/// <c>ResizeObserverEntry</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each global is lazy and non-clobbering, exactly as every web-API and DOM-interface global is: a page that
/// never mentions <c>ResizeObserver</c> must not pay for its prototype, and a name the host's own
/// configuration already took stays the host's.
/// </para>
/// <para>
/// The shapes are process-shared and the prototypes are per engine, which is the split the whole binding is
/// built on. Every member below is a static lambda that reaches its state through the receiver, which is what
/// lets one shape serve every engine — and unlike a <c>Window</c> member, none of these is ever called
/// unqualified, so a shape method reading <c>thisObj</c> is safe here where it is not there.
/// </para>
/// </remarks>
internal static class ObserverInstaller
{
    private static readonly JsObjectShape _mutationObserver = BuildMutationObserverShape();
    private static readonly JsObjectShape _intersectionObserver = BuildIntersectionObserverShape();
    private static readonly JsObjectShape _intersectionObserverEntry = BuildIntersectionObserverEntryShape();
    private static readonly JsObjectShape _resizeObserver = BuildResizeObserverShape();
    private static readonly JsObjectShape _resizeObserverEntry = BuildResizeObserverEntryShape();

    /// <summary>Installs the five globals on <paramref name="runtime"/>'s engine. Called once, at construction.</summary>
    internal static void Install(PageRuntime runtime)
    {
        var engine = runtime.Engine;

        Add(engine, "MutationObserver", static realm => realm.MutationObserver);
        Add(engine, "IntersectionObserver", static realm => realm.IntersectionObserver);
        Add(engine, "IntersectionObserverEntry", static realm => realm.IntersectionObserverEntry);
        Add(engine, "ResizeObserver", static realm => realm.ResizeObserver);
        Add(engine, "ResizeObserverEntry", static realm => realm.ResizeObserverEntry);
    }

    private static void Add(Engine engine, string name, Func<ObserverRealm, JsValue> factory)
        => engine.AddLazyGlobal(
            name,
            factory,
            static (e, f) => f(PageRuntime.Find(e)!.Observers),
            PropertyFlag.NonEnumerable);

    /// <summary>
    /// Builds an interface prototype and its interface object together, filling the prototype's per-realm
    /// <c>constructor</c> slot with the sanctioned in-place slot replacement so the prototype stays shaped.
    /// </summary>
    internal static ObjectInstance Instantiate(
        Engine engine,
        JsObjectShape shape,
        string name,
        int length,
        Func<JsValue[], ObjectInstance>? construct,
        out HostInterfaceObject interfaceObject)
    {
        var realm = engine._mainRealm;
        var prototype = shape.Instantiate(engine, realm.Intrinsics.Object.PrototypeObject);
        interfaceObject = new HostInterfaceObject(engine, realm, name, prototype, length, construct);

        prototype.DefineOwnPropertyUnchecked(
            "constructor",
            new PropertyDescriptor(interfaceObject, PropertyFlag.NonEnumerable));

        return prototype;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#interface-mutationobserver — <c>observe</c>, <c>disconnect</c> and
    /// <c>takeRecords</c>, and nothing else: the interface has no attributes at all.
    /// </summary>
    private static JsObjectShape BuildMutationObserverShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("MutationObserver")
        .Method("observe", static (t, args) => JsMutationObserver.Brand(t, "observe").Observe(args), length: 2)
        .Method("disconnect", static (t, _) => JsMutationObserver.Brand(t, "disconnect").Disconnect())
        .Method("takeRecords", static (t, _) => JsMutationObserver.Brand(t, "takeRecords").TakeRecords())
        .Build();

    /// <summary>https://w3c.github.io/IntersectionObserver/#intersection-observer-interface.</summary>
    private static JsObjectShape BuildIntersectionObserverShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("IntersectionObserver")
        .Accessor("root", static (t, _) => JsIntersectionObserver.Brand(t, "root").Root)
        .Accessor("rootMargin", static (t, _) => JsString.Create(JsIntersectionObserver.Brand(t, "rootMargin").RootMargin))
        .Accessor("scrollMargin", static (_, _) => JsString.Create("0px 0px 0px 0px"))
        .Accessor("thresholds", static (t, _) =>
        {
            var observer = JsIntersectionObserver.Brand(t, "thresholds");
            var values = new JsValue[observer.Thresholds.Length];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = JsNumber.Create(observer.Thresholds[i]);
            }

            return observer.Engine._mainRealm.Intrinsics.Array.ConstructFast(values);
        })
        .Method("observe", static (t, args) => JsIntersectionObserver.Brand(t, "observe").Observe(args), length: 1)
        .Method("unobserve", static (t, args) => JsIntersectionObserver.Brand(t, "unobserve").Unobserve(args), length: 1)
        .Method("disconnect", static (t, _) => JsIntersectionObserver.Brand(t, "disconnect").Disconnect())
        .Method("takeRecords", static (t, _) => JsIntersectionObserver.Brand(t, "takeRecords").TakeRecords())
        .Build();

    /// <summary>https://w3c.github.io/IntersectionObserver/#intersection-observer-entry.</summary>
    private static JsObjectShape BuildIntersectionObserverEntryShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("IntersectionObserverEntry")
        .Accessor("boundingClientRect", static (t, _) => JsIntersectionObserverEntry.Brand(t, "boundingClientRect").Rect())
        .Accessor("intersectionRatio", static (_, _) => JsNumber.Create(1))
        .Accessor("intersectionRect", static (t, _) => JsIntersectionObserverEntry.Brand(t, "intersectionRect").Rect())
        .Accessor("isIntersecting", static (_, _) => JsBoolean.True)
        .Accessor("rootBounds", static (t, _) => JsIntersectionObserverEntry.Brand(t, "rootBounds").RootBounds())
        .Accessor("target", static (t, _) => JsIntersectionObserverEntry.Brand(t, "target").TargetValue)
        .Accessor("time", static (t, _) => JsNumber.Create(JsIntersectionObserverEntry.Brand(t, "time").Time))
        .Build();

    /// <summary>https://w3c.github.io/csswg-drafts/resize-observer/#resizeobserver.</summary>
    private static JsObjectShape BuildResizeObserverShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("ResizeObserver")
        .Method("observe", static (t, args) => JsResizeObserver.Brand(t, "observe").Observe(args), length: 1)
        .Method("unobserve", static (t, args) => JsResizeObserver.Brand(t, "unobserve").Unobserve(args), length: 1)
        .Method("disconnect", static (t, _) => JsResizeObserver.Brand(t, "disconnect").Disconnect())
        .Build();

    /// <summary>https://w3c.github.io/csswg-drafts/resize-observer/#resizeobserverentry.</summary>
    private static JsObjectShape BuildResizeObserverEntryShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("ResizeObserverEntry")
        .Accessor("borderBoxSize", static (t, _) => JsResizeObserverEntry.Brand(t, "borderBoxSize").BoxSizes())
        .Accessor("contentBoxSize", static (t, _) => JsResizeObserverEntry.Brand(t, "contentBoxSize").BoxSizes())
        .Accessor("contentRect", static (t, _) => JsResizeObserverEntry.Brand(t, "contentRect").Rect())
        .Accessor("devicePixelContentBoxSize", static (t, _) => JsResizeObserverEntry.Brand(t, "devicePixelContentBoxSize").BoxSizes())
        .Accessor("target", static (t, _) => JsResizeObserverEntry.Brand(t, "target").TargetValue)
        .Build();

    internal static JsObjectShape MutationObserverShape => _mutationObserver;

    internal static JsObjectShape IntersectionObserverShape => _intersectionObserver;

    internal static JsObjectShape IntersectionObserverEntryShape => _intersectionObserverEntry;

    internal static JsObjectShape ResizeObserverShape => _resizeObserver;

    internal static JsObjectShape ResizeObserverEntryShape => _resizeObserverEntry;
}
