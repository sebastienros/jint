using Jint.Browser.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi;
using Jint.WebApi.Performance;

namespace Jint.Browser.Runtime;

/// <summary>
/// The document's legacy navigation information, https://www.w3.org/TR/navigation-timing/#sec-navigation-info-interface.
/// Navigation Timing entries are not implemented; the legacy interface must not fabricate one.
/// </summary>
internal sealed class PerformanceNavigation
{
    internal static readonly (string Name, int Value)[] Constants =
    [
        ("TYPE_NAVIGATE", 0),
        ("TYPE_RELOAD", 1),
        ("TYPE_BACK_FORWARD", 2),
        ("TYPE_RESERVED", 255),
    ];

    private static readonly JsObjectShape _shape = BuildShape();
    private static readonly JsObjectLayout _json = new JsObjectLayout.Builder()
        .Add("type")
        .Add("redirectCount")
        .Build();

    internal PerformanceNavigation(PageRuntime runtime)
    {
        var engine = runtime.Engine;
        var realm = engine._mainRealm;
        var prototype = _shape.Instantiate(engine);
        Constructor = new HostInterfaceObject(engine, realm, "PerformanceNavigation", prototype, 0, constants: Constants);
        prototype.DefineOwnPropertyUnchecked("constructor", new PropertyDescriptor(Constructor, PropertyFlag.NonEnumerable));
        Instance = new JsPerformanceNavigation(runtime, prototype);
    }

    internal ObjectInstance Constructor { get; }

    internal ObjectInstance Instance { get; }

    internal static void Install(PageRuntime runtime)
    {
        var engine = runtime.Engine;
        if ((engine.Options.WebApi.Features & WebApiFeatures.Performance) == 0)
        {
            return;
        }

        engine.AddLazyGlobal("PerformanceNavigation", static e => PageRuntime.Find(e)!.Navigation.Constructor,
            PropertyFlag.NonEnumerable);

        // A checked define uses the shaped prototype's hybrid addition lane. An unchecked raw descriptor
        // would discard its shared shape and deoptimize the existing Performance methods.
        engine._mainRealm.Intrinsics.Performance.PrototypeObject.DefinePropertyOrThrow(
            "navigation",
            new GetSetPropertyDescriptor(
                new ClrFunction(engine, "get navigation", (receiver, _) =>
                {
                    if (receiver is JsPerformance performance && PageRuntime.Find(performance.Engine) is { } owner)
                    {
                        return owner.Navigation.Instance;
                    }

                    Throw.TypeError(engine._mainRealm, "Illegal invocation");
                    return JsValue.Undefined;
                }),
                set: null,
                PropertyFlag.Configurable | PropertyFlag.Enumerable));
    }

    private static JsObjectShape BuildShape()
    {
        var builder = new JsObjectShape.Builder()
            .PerRealmSlot("constructor")
            .ToStringTag("PerformanceNavigation")
            .Accessor("type", static (receiver, _) => JsNumber.Create(Brand(receiver).Runtime.NavigationType))
            .Accessor("redirectCount", static (receiver, _) => JsNumber.Create(Brand(receiver).Runtime.NavigationRedirectCount))
            .Method("toJSON", static (receiver, _) =>
            {
                var runtime = Brand(receiver).Runtime;
                return JsObject.Create(runtime.Engine, _json,
                    [JsNumber.Create(runtime.NavigationType), JsNumber.Create(runtime.NavigationRedirectCount)]);
            }, 0);

        foreach (var (name, value) in Constants)
        {
            builder.Constant(name, JsNumber.Create(value));
        }

        return builder.Build();
    }

    private static JsPerformanceNavigation Brand(JsValue receiver)
    {
        if (receiver is JsPerformanceNavigation navigation)
        {
            return navigation;
        }

        if (receiver is ObjectInstance instance)
        {
            Throw.TypeError(instance.Engine.Realm, "Illegal invocation");
        }

        Throw.TypeErrorNoEngine("Illegal invocation");
        return null!;
    }

    private sealed class JsPerformanceNavigation : ObjectInstance
    {
        internal JsPerformanceNavigation(PageRuntime runtime, ObjectInstance prototype) : base(runtime.Engine)
        {
            Runtime = runtime;
            Prototype = prototype;
        }

        internal PageRuntime Runtime { get; }
    }
}
