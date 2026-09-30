using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi.Events;
using Jint.WebApi.Streams;
using Jint.WebApi.StructuredClone;

namespace Jint.Browser.Navigation;

/// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#the-navigationhistoryentry-interface</summary>
internal sealed class JsNavigationHistoryEntry : JsEventTarget
{
    internal JsNavigationHistoryEntry(JsNavigation navigation, HistoryEntry entry) : base(navigation.Engine, navigation.Owner.Realm)
    {
        Navigation = navigation;
        Entry = entry;
        _prototype = navigation.Owner.Prototype("NavigationHistoryEntry");
    }

    internal JsNavigation Navigation { get; }
    internal HistoryEntry Entry { get; }
    internal int Index => Navigation.IndexOf(Entry);
    internal bool SameDocument => Navigation.Active && Entry.DocumentId == Navigation.DocumentId;
    internal JsValue Url => !Navigation.Active ? JsString.Empty
        : !SameDocument && Entry.ReferrerPolicy is "no-referrer" or "origin" ? Null : JsString.Create(Entry.Url);
    internal JsValue State() => Navigation.Active ? NavigationValues.Deserialize(Navigation.Owner, Entry.NavigationState) : Undefined;
}

/// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#the-navigationdestination-interface</summary>
internal sealed class JsNavigationDestination : ObjectInstance
{
    internal JsNavigationDestination(NavigationRealm owner, string url, bool sameDocument, SerializationRecord? state,
        JsNavigationHistoryEntry? entry = null) : base(owner.Engine)
    {
        Owner = owner;
        Url = url;
        SameDocument = sameDocument;
        State = state;
        Entry = entry;
        _prototype = owner.Prototype("NavigationDestination");
    }

    internal NavigationRealm Owner { get; }
    internal string Url { get; }
    internal bool SameDocument { get; }
    internal SerializationRecord? State { get; }
    internal JsNavigationHistoryEntry? Entry { get; }
}

/// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#navigation-activation-interface</summary>
internal sealed class JsNavigationActivation : ObjectInstance
{
    internal JsNavigationActivation(NavigationRealm owner, JsNavigationHistoryEntry entry, JsNavigationHistoryEntry? from, string type)
        : base(owner.Engine)
    {
        Entry = entry;
        From = from;
        NavigationType = type;
        _prototype = owner.Prototype("NavigationActivation");
    }

    internal JsNavigationHistoryEntry Entry { get; }
    internal JsNavigationHistoryEntry? From { get; }
    internal string NavigationType { get; }
}

/// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#ongoing-navigation-tracking</summary>
internal sealed class JsNavigationTransition : ObjectInstance
{
    internal JsNavigationTransition(NavigationRealm owner, string type, JsNavigationHistoryEntry from, JsNavigationDestination to)
        : base(owner.Engine)
    {
        NavigationType = type;
        From = from;
        To = to;
        Committed = StreamPromises.NewPromise(Engine, owner.Realm);
        Finished = StreamPromises.NewPromise(Engine, owner.Realm);
        StreamPromises.MarkHandled(StreamPromises.PromiseOf(Committed));
        StreamPromises.MarkHandled(StreamPromises.PromiseOf(Finished));
        _prototype = owner.Prototype("NavigationTransition");
    }

    internal string NavigationType { get; }
    internal JsNavigationHistoryEntry From { get; }
    internal JsNavigationDestination To { get; }
    internal PromiseCapability Committed { get; }
    internal PromiseCapability Finished { get; }
}

internal static class NavigationValues
{
    internal static ObjectInstance? Dictionary(Realm realm, JsValue value)
    {
        if (value.IsNullOrUndefined()) return null;
        if (value is ObjectInstance dictionary) return dictionary;
        Throw.TypeError(realm, "Navigation options must be a dictionary.");
        return null;
    }

    internal static JsValue Get(ObjectInstance? dictionary, string key) => dictionary?.Get(key) ?? JsValue.Undefined;

    internal static string Enum(Realm realm, JsValue value, string fallback, params string[] allowed)
    {
        if (value.IsUndefined()) return fallback;
        var text = TypeConverter.ToString(value);
        if (System.Array.IndexOf(allowed, text) < 0) Throw.TypeError(realm, "'" + text + "' is not a valid navigation enum value.");
        return text;
    }

    internal static SerializationRecord Serialize(NavigationRealm owner, JsValue state)
        => new StructuredSerializer(owner.Engine, owner.Realm).Serialize(state, transferList: null);

    internal static JsValue Deserialize(NavigationRealm owner, SerializationRecord? state)
        => state is { } record ? new StructuredDeserializer(owner.Engine, owner.Realm, sharedRecord: true).Deserialize(record) : JsValue.Undefined;
}
