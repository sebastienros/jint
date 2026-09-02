using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.WebApi;

namespace Jint.Browser.Events;

/// <summary>
/// Everything the events bridge keeps per engine: the prototype and interface object of each UI event
/// interface, the document's focus state, and the host the activation behaviours hand their default actions
/// to.
/// </summary>
/// <remarks>
/// <para>
/// One engine is one document — a navigation builds a new engine — so per-engine and per-document coincide,
/// which is what lets the focused element live here rather than beside the AngleSharp document. It is stored
/// in a <see cref="ConditionalWeakTable{TKey,TValue}"/> keyed on the engine for the reason
/// <see cref="Dom.DomRealm"/> gives: <c>Engine.HostDefined</c> belongs to the embedder.
/// </para>
/// <para>
/// It is created on demand rather than installed, because a generated prototype member — <c>el.onclick</c>,
/// <c>document.activeElement</c> — has to be able to reach it on an engine that installed the DOM bindings and
/// nothing else. <see cref="Install"/> adds only the globals.
/// </para>
/// </remarks>
internal sealed class BrowserEventRealm
{
    private static readonly ConditionalWeakTable<Engine, BrowserEventRealm> _realms = new();

    private readonly ObjectInstance?[] _prototypes;
    private readonly BrowserEventInterfaceObject?[] _interfaceObjects;
    private List<PendingActivation>? _pending;

    private BrowserEventRealm(Engine engine)
    {
        Engine = engine;
        PrincipalRealm = engine._mainRealm;
        _prototypes = new ObjectInstance?[BrowserEventInterfaces.All.Length];
        _interfaceObjects = new BrowserEventInterfaceObject?[BrowserEventInterfaces.All.Length];
    }

    /// <summary>The engine every object in this realm belongs to.</summary>
    internal Engine Engine { get; }

    /// <summary>
    /// The engine's principal realm, captured once — never <c>Engine.Realm</c>, which answers the realm
    /// currently executing. The reason is <see cref="Dom.DomRealm.PrincipalRealm"/>'s.
    /// </summary>
    internal Realm PrincipalRealm { get; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#dom-document-activeelement — the focused
    /// element, or <see langword="null"/> when focus rests on the body (or on nothing, before a document
    /// exists).
    /// </summary>
    /// <remarks>
    /// Held here rather than read from AngleSharp because AngleSharp never assigns
    /// <c>IDocument.ActiveElement</c> — not even from its own <c>DoFocus</c> — so its answer is <c>null</c> for
    /// the life of every document. See <c>overrides.json</c>'s two <c>Document</c> skip entries.
    /// </remarks>
    internal IElement? FocusedElement { get; set; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#dom-document-hasfocus — whether the document's
    /// viewport has the system focus. True for a headless page unless a host says otherwise: there is one
    /// document and nothing else on screen to take focus from it.
    /// </summary>
    internal bool DocumentHasFocus { get; set; } = true;

    /// <summary>
    /// Where an activation behaviour's default action goes — a hyperlink to follow, a form to submit, a file
    /// chooser to open. Replaced by the navigation layer (campaign item R5); until then the default records.
    /// </summary>
    internal BrowserActivationHost ActivationHost { get; set; } = BrowserActivationHost.Recording;

    /// <summary>
    /// What <see cref="BrowserActivationHost.Recording"/> recorded, oldest first. It is the whole of what a
    /// default action does before the navigation layer exists, and it is what a test asserts on.
    /// </summary>
    internal IReadOnlyList<PendingActivation> PendingActivations => (IReadOnlyList<PendingActivation>?) _pending ?? [];

    /// <summary>Adds one recorded default action.</summary>
    internal void Record(PendingActivation activation) => (_pending ??= []).Add(activation);

    /// <summary>The events-bridge state of <paramref name="engine"/>, created on first use.</summary>
    internal static BrowserEventRealm Of(Engine engine) => _realms.GetValue(engine, static e => new BrowserEventRealm(e));

    /// <summary>
    /// The realm behind a receiver, or <see langword="null"/> when the receiver belongs to no engine. Used by
    /// the shape members, which hold the receiver and nothing else.
    /// </summary>
    internal static BrowserEventRealm? Find(JsValue thisObject)
        => thisObject is ObjectInstance instance ? Of(instance.Engine) : null;

    /// <summary>
    /// Installs every UI event interface object — <c>UIEvent</c>, <c>MouseEvent</c>, <c>KeyboardEvent</c>, … —
    /// as a lazy, non-clobbering global.
    /// </summary>
    /// <remarks>
    /// Lazy and non-clobbering for the reasons <see cref="Dom.DomBindings.Install"/> gives: an engine that
    /// never mentions <c>WheelEvent</c> must not pay for its prototype, and a name the host's own
    /// configuration already took is the host's.
    /// </remarks>
    internal static void Install(Engine engine)
    {
        if (engine is null)
        {
            Throw.ArgumentNullException(nameof(engine));
        }

        var realm = Of(engine);
        var global = engine._mainRealm.GlobalObject;

        foreach (var definition in BrowserEventInterfaces.All)
        {
            if (global.HasOwnProperty(WebApiRegistration.NameOf(definition.Name)))
            {
                continue;
            }

            var captured = definition;
            global.SetProperty(
                definition.Name,
                new LazyPropertyDescriptor<BrowserEventRealm>(realm, r => r.InterfaceObjectOf(captured), PropertyFlag.NonEnumerable));
        }
    }

    /// <summary>
    /// The interface's prototype object in this engine, created on first use along with every prototype above
    /// it, so that <c>Object.getPrototypeOf(PointerEvent.prototype) === MouseEvent.prototype</c> holds however
    /// the two were first reached.
    /// </summary>
    internal ObjectInstance PrototypeOf(BrowserEventDefinition definition)
    {
        var existing = _prototypes[definition.Index];
        if (existing is not null)
        {
            return existing;
        }

        var parent = definition.Parent is { } p
            ? PrototypeOf(p)
            : PrincipalRealm.Intrinsics.Event.PrototypeObject;

        var prototype = definition.Shape.Instantiate(Engine, parent);

        // Published before the interface object is built, because that object asks for this prototype: the two
        // are mutually referential and one of them has to be visible half-built. Same reasoning as DomRealm's.
        _prototypes[definition.Index] = prototype;

        if (_interfaceObjects[definition.Index] is null)
        {
            var parentInterface = definition.Parent is { } q
                ? (JsValue) InterfaceObjectOf(q)
                : PrincipalRealm.Intrinsics.Event;

            _interfaceObjects[definition.Index] = new BrowserEventInterfaceObject(this, definition, prototype, parentInterface);
        }

        // The per-realm `constructor` slot, filled with the sanctioned in-place slot replacement: the name is
        // declared by the shape, so the prototype stays in shared-layout mode.
        prototype.DefineOwnPropertyUnchecked(
            "constructor",
            new PropertyDescriptor(_interfaceObjects[definition.Index], PropertyFlag.NonEnumerable));

        return prototype;
    }

    /// <summary>The interface object — the global <c>MouseEvent</c> — in this engine, created on first use.</summary>
    internal BrowserEventInterfaceObject InterfaceObjectOf(BrowserEventDefinition definition)
    {
        PrototypeOf(definition);
        return _interfaceObjects[definition.Index]!;
    }

    /// <summary>
    /// Builds an event of <paramref name="definition"/> that the engine itself created, so <c>isTrusted</c> is
    /// true — https://dom.spec.whatwg.org/#concept-event-fire step 2.
    /// </summary>
    internal T CreateTrusted<T>(BrowserEventDefinition definition, T instance) where T : Jint.WebApi.Events.JsEvent
    {
        instance.IsTrusted = true;
        instance._prototype = PrototypeOf(definition);
        return instance;
    }

    /// <summary>https://dom.spec.whatwg.org/#inner-event-creation-steps step 3, the event's time stamp.</summary>
    internal double TimeStamp => Engine._webApi?.CurrentHighResolutionTime ?? 0;
}

/// <summary>What kind of default action an activation behaviour asked its host for.</summary>
internal enum PendingActivationKind
{
    /// <summary>A hyperlink was followed — <c>&lt;a href&gt;</c> or <c>&lt;area href&gt;</c>.</summary>
    Navigation,

    /// <summary>A form was submitted.</summary>
    FormSubmission,

    /// <summary>A file chooser was opened by clicking an <c>&lt;input type=file&gt;</c>.</summary>
    FileChooser,
}

/// <summary>
/// One default action an activation behaviour asked for and no host carried out — a link that was followed
/// with no navigation layer behind it, a form that was submitted with no submitter, a file chooser nobody
/// answered.
/// </summary>
/// <param name="Kind">Which default action ran.</param>
/// <param name="Target">The URL for a navigation, the form's action for a submission, the input's name for a file chooser.</param>
/// <param name="Detail">The submit button's name, the link's target attribute, or the empty string.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct PendingActivation(PendingActivationKind Kind, string Target, string Detail)
{
    public override string ToString() => Kind + " " + Target + (Detail.Length == 0 ? "" : " (" + Detail + ")");
}
