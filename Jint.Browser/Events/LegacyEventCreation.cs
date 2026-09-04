using Jint.Browser.Dom;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// <c>document.createEvent(interface)</c> — DOM's legacy way of building an event, kept alive by the
/// half of the web-platform corpus that predates the constructors.
/// <para>
/// https://dom.spec.whatwg.org/#dom-document-createevent
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>AngleSharp's own <c>createEvent</c> is skipped and this replaces it</b> (the override table says so):
/// its result is an AngleSharp <c>Event</c>, and every script-visible event in this package is a Jint one
/// dispatched through the engine's tree dispatcher. What the standard requires is only the alias table, so
/// nothing here builds an event a constructor could not have built — the difference is entirely in the two
/// steps after it.
/// </para>
/// <para>
/// <b>Those two steps are the whole point of the method.</b> The event's <c>type</c> is the empty string and
/// its <i>initialized flag</i> is unset, which makes it undispatchable until <c>initEvent()</c> has named it —
/// <c>dispatchEvent</c> answers an <c>InvalidStateError</c> for one that has not been. The flag is the
/// engine's (<c>JsEvent.InitializedFlag</c>), and <c>document.createEvent</c> is the only algorithm in the
/// standard that unsets it, which is why the engine leaves the writer to a host.
/// </para>
/// <para>
/// <b>An alias naming an interface this package does not have is a <c>NotSupportedError</c>, which is what the
/// standard says for an alias it does not list at all.</b> Five of the table's rows name interfaces that do
/// not exist here — <c>DragEvent</c> needs drag dispatch, <c>ClipboardEvent</c> a clipboard,
/// <c>StorageEvent</c> a storage area's change notification, <c>TouchEvent</c> a touch input, and the two
/// device-orientation events a sensor — and answering with a plain <c>Event</c> under those names would be a
/// lie a page cannot detect. A browser without the interface refuses in the same way.
/// </para>
/// </remarks>
internal static class LegacyEventCreation
{
    /// <summary>
    /// The alias table of https://dom.spec.whatwg.org/#dom-document-createevent, matched
    /// ASCII-case-insensitively as the standard requires.
    /// </summary>
    /// <remarks>
    /// The value is the package interface to build, or <see langword="null"/> for one of the three the engine
    /// owns — which <see cref="EngineInterface"/> then names. The two spellings of each legacy plural
    /// (<c>events</c>, <c>mouseevents</c>, <c>uievents</c>) are separate rows because that is how the table is
    /// written.
    /// </remarks>
    private static readonly Dictionary<string, BrowserEventDefinition?> _aliases = new(StringComparer.Ordinal)
    {
        ["beforeunloadevent"] = BrowserEventInterfaces.BeforeUnloadEvent,
        ["compositionevent"] = BrowserEventInterfaces.CompositionEvent,
        ["customevent"] = null,
        ["event"] = null,
        ["events"] = null,
        ["focusevent"] = BrowserEventInterfaces.FocusEvent,
        ["hashchangeevent"] = BrowserEventInterfaces.HashChangeEvent,
        ["htmlevents"] = null,
        ["keyboardevent"] = BrowserEventInterfaces.KeyboardEvent,
        ["messageevent"] = null,
        ["mouseevent"] = BrowserEventInterfaces.MouseEvent,
        ["mouseevents"] = BrowserEventInterfaces.MouseEvent,
        ["svgevents"] = null,
        ["textevent"] = BrowserEventInterfaces.CompositionEvent,
        ["uievent"] = BrowserEventInterfaces.UIEvent,
        ["uievents"] = BrowserEventInterfaces.UIEvent,
    };

    /// <summary>Which engine-owned interface an alias whose table entry is null names.</summary>
    private static string EngineInterface(string alias) => alias switch
    {
        "customevent" => "CustomEvent",
        "messageevent" => "MessageEvent",
        _ => "Event",
    };

    /// <summary>
    /// The member's body: one argument, the interface name, and an event of that interface with no type and
    /// its initialized flag unset.
    /// </summary>
    internal static JsValue CreateEvent(DomRealm dom, JsValue[] arguments)
    {
        var realm = dom.PrincipalRealm;
        var alias = DomConvert.RequiredText(arguments, 0, "Document.createEvent").ToLowerInvariant();

        if (!_aliases.TryGetValue(alias, out var definition))
        {
            var notSupported = realm.Intrinsics.DomException.CreateException(
                DomExceptionNames.NotSupported,
                "Failed to execute 'createEvent' on 'Document': the provided event type ('" + alias + "') is invalid.");

            var location = dom.Engine._lastSyntaxElement?.Location ?? default;
            Throw.JavaScriptException(dom.Engine, notSupported, in location);
        }

        var events = BrowserEventRealm.Of(dom.Engine);

        // Step 2's "create an event": the interface's own constructor with no dictionary, which gives the
        // empty type and every member its default. The prototype is the caller's to assign, exactly as the
        // interface object assigns it for `new`.
        var created = definition is not null
            ? Package(events, definition)
            : Engine(realm, EngineInterface(alias));

        // Steps 4 and 6. `isTrusted` is already false — nothing here creates a trusted event — and step 5's
        // time stamp is the constructor's own.
        created.InitializedFlag = false;
        return created;
    }

    private static JsEvent Package(BrowserEventRealm events, BrowserEventDefinition definition)
    {
        var instance = definition.Construct(events, [JsString.Empty]);
        instance._prototype = events.PrototypeOf(definition);
        return instance;
    }

    private static JsEvent Engine(Realm realm, string name)
    {
        var constructor = name switch
        {
            "CustomEvent" => (Native.Constructor) realm.Intrinsics.CustomEvent,
            "MessageEvent" => realm.Intrinsics.MessageEvent,
            _ => realm.Intrinsics.Event,
        };

        return (JsEvent) constructor.Construct([JsString.Empty], constructor);
    }
}
