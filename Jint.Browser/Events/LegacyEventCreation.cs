using Jint.Browser.Dom;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// Implements document.createEvent with the Browser's script-visible event interfaces.
/// </summary>
/// <remarks>
/// https://dom.spec.whatwg.org/#dom-document-createevent
/// Events belong to the Browser dispatcher and are initialized using the legacy event-creation rules.
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
        ["devicemotionevent"] = BrowserEventInterfaces.DeviceMotionEvent,
        ["deviceorientationevent"] = BrowserEventInterfaces.DeviceOrientationEvent,
        ["dragevent"] = BrowserEventInterfaces.DragEvent,
        ["event"] = null,
        ["events"] = null,
        ["focusevent"] = BrowserEventInterfaces.FocusEvent,
        ["hashchangeevent"] = BrowserEventInterfaces.HashChangeEvent,
        ["htmlevents"] = null,
        ["keyboardevent"] = BrowserEventInterfaces.KeyboardEvent,
        ["messageevent"] = null,
        ["mouseevent"] = BrowserEventInterfaces.MouseEvent,
        ["mouseevents"] = BrowserEventInterfaces.MouseEvent,
        ["storageevent"] = BrowserEventInterfaces.StorageEvent,
        ["svgevents"] = null,
        ["textevent"] = BrowserEventInterfaces.TextEvent,
        ["touchevent"] = BrowserEventInterfaces.TouchEvent,
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
        var realm = dom.OwningRealm;
        var alias = DomConvert.RequiredText(arguments, 0, "Document.createEvent").ToLowerInvariant();

        if (!_aliases.TryGetValue(alias, out var definition))
        {
            var notSupported = realm.Intrinsics.DomException.CreateException(
                DomExceptionNames.NotSupported,
                "Failed to execute 'createEvent' on 'Document': the provided event type ('" + alias + "') is invalid.");

            var location = dom.Engine._lastSyntaxElement?.Location ?? default;
            Throw.JavaScriptException(dom.Engine, notSupported, in location);
        }

        var events = BrowserEventRealm.Of(dom.Engine, realm);

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
