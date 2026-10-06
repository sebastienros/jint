using Jint.Browser.SystemState;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.Browser.Navigation;

/// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#navigation-interface</summary>
internal static class NavigationShapes
{
    private static readonly JsObjectShape _navigation = Base("Navigation")
        .Method("entries", static (t, _) => Nav(t, "entries").Entries())
        .Accessor("currentEntry", static (t, _) => (JsValue?) Nav(t, "currentEntry").CurrentEntry ?? JsValue.Null)
        .Method("updateCurrentEntry", static (t, args) => Nav(t, "updateCurrentEntry").Update(args), 1)
        .Accessor("transition", static (t, _) => (JsValue?) Nav(t, "transition").Transition ?? JsValue.Null)
        .Accessor("activation", static (t, _) => (JsValue?) Nav(t, "activation").Activation ?? JsValue.Null)
        .Accessor("canGoBack", static (t, _) => JsBoolean.Create(Nav(t, "canGoBack").CanTraverse(-1)))
        .Accessor("canGoForward", static (t, _) => JsBoolean.Create(Nav(t, "canGoForward").CanTraverse(1)))
        .Method("navigate", static (t, args) => Nav(t, "navigate").Navigate(args), 1)
        .Method("reload", static (t, args) => Nav(t, "reload").Reload(args))
        .Method("traverseTo", static (t, args) => Nav(t, "traverseTo").Traverse(args), 1)
        .Method("back", static (t, args) => Nav(t, "back").Traverse(args, -1))
        .Method("forward", static (t, args) => Nav(t, "forward").Traverse(args, 1))
        .Handler("onnavigate", "navigate", static (t, name) => Nav(t, name))
        .Handler("onnavigatesuccess", "navigatesuccess", static (t, name) => Nav(t, name))
        .Handler("onnavigateerror", "navigateerror", static (t, name) => Nav(t, name))
        .Handler("oncurrententrychange", "currententrychange", static (t, name) => Nav(t, name))
        .Build();

    private static readonly JsObjectShape _entry = Base("NavigationHistoryEntry")
        .Accessor("url", static (t, _) => Entry(t, "url").Url)
        .Accessor("key", static (t, _) =>
        {
            var entry = Entry(t, "key");
            return JsString.Create(entry.Navigation.Active ? entry.Entry.Key : "");
        })
        .Accessor("id", static (t, _) =>
        {
            var entry = Entry(t, "id");
            return JsString.Create(entry.Navigation.Active ? entry.Entry.Id : "");
        })
        .Accessor("index", static (t, _) => JsNumber.Create(Entry(t, "index").Index))
        .Accessor("sameDocument", static (t, _) => JsBoolean.Create(Entry(t, "sameDocument").SameDocument))
        .Method("getState", static (t, _) => Entry(t, "getState").State())
        .Handler("ondispose", "dispose", static (t, name) => Entry(t, name))
        .Build();

    private static readonly JsObjectShape _destination = Base("NavigationDestination")
        .Accessor("url", static (t, _) => JsString.Create(Destination(t, "url").Url))
        .Accessor("key", static (t, _) => JsString.Create(Destination(t, "key").Entry?.Entry.Key ?? ""))
        .Accessor("id", static (t, _) => JsString.Create(Destination(t, "id").Entry?.Entry.Id ?? ""))
        .Accessor("index", static (t, _) => JsNumber.Create(Destination(t, "index").Entry?.Index ?? -1))
        .Accessor("sameDocument", static (t, _) => JsBoolean.Create(Destination(t, "sameDocument").SameDocument))
        .Method("getState", static (t, _) =>
        {
            var destination = Destination(t, "getState");
            return NavigationValues.Deserialize(destination.Owner, destination.State);
        })
        .Build();

    private static readonly JsObjectShape _transition = Base("NavigationTransition")
        .Accessor("navigationType", static (t, _) => JsString.Create(Transition(t, "navigationType").NavigationType))
        .Accessor("from", static (t, _) => Transition(t, "from").From)
        .Accessor("to", static (t, _) => Transition(t, "to").To)
        .PerRealmSlot("committed", enumerable: true)
        .PerRealmSlot("finished", enumerable: true)
        .Build();

    private static readonly JsObjectShape _activation = Base("NavigationActivation")
        .Accessor("from", static (t, _) => (JsValue?) Activation(t, "from").From ?? JsValue.Null)
        .Accessor("entry", static (t, _) => Activation(t, "entry").Entry)
        .Accessor("navigationType", static (t, _) => JsString.Create(Activation(t, "navigationType").NavigationType))
        .Build();

    internal static JsObjectShape For(string name) => name switch
    {
        "Navigation" => _navigation,
        "NavigationHistoryEntry" => _entry,
        "NavigationDestination" => _destination,
        "NavigationTransition" => _transition,
        "NavigationActivation" => _activation,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static JsObjectShape.Builder Base(string name) => new JsObjectShape.Builder().PerRealmSlot("constructor").ToStringTag(name);
    private static JsNavigation Nav(JsValue t, string member) => SystemBrand.Of<JsNavigation>(t, "Navigation", member);
    private static JsNavigationHistoryEntry Entry(JsValue t, string member) => SystemBrand.Of<JsNavigationHistoryEntry>(t, "NavigationHistoryEntry", member);
    private static JsNavigationDestination Destination(JsValue t, string member) => SystemBrand.Of<JsNavigationDestination>(t, "NavigationDestination", member);
    private static JsNavigationTransition Transition(JsValue t, string member) => SystemBrand.Of<JsNavigationTransition>(t, "NavigationTransition", member);
    private static JsNavigationActivation Activation(JsValue t, string member) => SystemBrand.Of<JsNavigationActivation>(t, "NavigationActivation", member);

    private static JsObjectShape.Builder Handler(this JsObjectShape.Builder builder, string name, string type, Func<JsValue, string, JsEventTarget> brand)
        => builder.Accessor(name, (t, _) => EventHandlerAttributes.Get(brand(t, name), type),
            (t, args) => EventHandlerAttributes.Set(brand(t, name), type, args.At(0)));
}
