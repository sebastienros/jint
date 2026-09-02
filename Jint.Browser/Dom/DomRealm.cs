using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using Jint.Browser.Dom.Collections;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.Browser.Dom;

/// <summary>
/// Everything the DOM binding keeps per engine: the prototype and interface object of each interface, and the
/// wrapper cache that gives a DOM object one identity in script.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where it is stored.</b> In a <see cref="ConditionalWeakTable{TKey,TValue}"/> keyed on the engine, not
/// in <c>Engine.HostDefined</c>. That slot belongs to the embedder — a host putting its request context there
/// is the documented use — and a library that takes it takes it from whoever else wanted it. The table costs
/// one lookup on the paths that need the realm and nothing at all on the paths that already hold a wrapper,
/// which is every generated member.
/// </para>
/// <para>
/// <b>Wrapper identity.</b> One <see cref="ConditionalWeakTable{TKey,TValue}"/> keyed on the AngleSharp
/// object serves every kind of wrapper, and that single choice buys the browsers' wrapper-preservation rule:
/// a node still in the tree keeps its wrapper and therefore its expandos alive (React and Vue rely on that);
/// a node dropped by both the tree and script collects together with its wrapper. Keying on the AngleSharp
/// object rather than on an identity of our own is what extends the same treatment to a short-lived view — a
/// <c>DOMTokenList</c>, a <c>CSSStyleDeclaration</c> — which keeps one wrapper for exactly as long as
/// AngleSharp keeps handing back one object.
/// </para>
/// <para>
/// <b>Where that stops, and it is AngleSharp's answer rather than ours.</b> AngleSharp builds a fresh
/// collection for each call of <c>children</c>, <c>querySelectorAll</c> or <c>getElementsByTagName</c>, so
/// <c>el.children === el.children</c> answers <see langword="false"/> here where a browser answers
/// <see langword="true"/>. Mending it in the binding would mean a second identity keyed on (owner, kind) that
/// nothing could invalidate; it is recorded as a divergence and reported upstream instead.
/// </para>
/// </remarks>
internal sealed class DomRealm
{
    private static readonly ConditionalWeakTable<Engine, DomRealm> _realms = new();

    private readonly ObjectInstance?[] _prototypes;
    private readonly DomInterfaceObject?[] _interfaceObjects;
    private readonly ConditionalWeakTable<object, ObjectInstance> _wrappers = new();

    private DomRealm(Engine engine)
    {
        Engine = engine;
        PrincipalRealm = engine._mainRealm;
        _prototypes = new ObjectInstance?[DomInterfaces.All.Length];
        _interfaceObjects = new DomInterfaceObject?[DomInterfaces.All.Length];
    }

    /// <summary>The engine every object in this realm belongs to.</summary>
    internal Engine Engine { get; }

    /// <summary>
    /// The engine's principal realm, captured once. Deliberately not <c>Engine.Realm</c>, which answers the
    /// <em>running</em> realm: a wrapper first reached from inside a <c>ShadowRealm</c> callback would
    /// otherwise be built against intrinsics its object does not belong to, and every wrapper after it would
    /// disagree with it about what <c>Object.prototype</c> is.
    /// </summary>
    internal Realm PrincipalRealm { get; }

    /// <summary>
    /// Where the members that parse markup into the tree go. The default calls AngleSharp directly, which is
    /// what a binding with no runtime behind it can do; the parser driver replaces it.
    /// </summary>
    internal DomHostHooks Hooks { get; set; } = DomHostHooks.Default;

    /// <summary>
    /// The window an event path continues into above the document, or <see langword="null"/> when the engine
    /// has no window.
    /// </summary>
    /// <remarks>
    /// https://dom.spec.whatwg.org/#get-the-parent — a document's parent is its browsing context's window for
    /// every event but <c>load</c>. The binding does not know what a window is and must not: the runtime that
    /// installs one publishes it here, and a binding used without a runtime keeps answering the document as
    /// the root of every path, which is exactly what a document with no browsing context is.
    /// </remarks>
    internal JsEventTarget? WindowTarget { get; set; }

    /// <summary>The binding state of <paramref name="engine"/>, created on first use.</summary>
    internal static DomRealm Of(Engine engine) => _realms.GetValue(engine, static e => new DomRealm(e));

    /// <summary>
    /// The interface's prototype object in this engine, created on first use along with every prototype above
    /// it — which is what makes <c>Object.getPrototypeOf(HTMLDivElement.prototype) === HTMLElement.prototype</c>
    /// hold however the two were first reached.
    /// </summary>
    internal ObjectInstance PrototypeOf(DomInterfaceDefinition definition)
    {
        var existing = _prototypes[definition.Index];
        if (existing is not null)
        {
            return existing;
        }

        var parent = definition.Parent is { } p
            ? PrototypeOf(p)
            : definition.RootsAtEventTarget
                ? PrincipalRealm.Intrinsics.EventTarget.PrototypeObject
                : PrincipalRealm.Intrinsics.Object.PrototypeObject;

        var prototype = definition.Shape.Instantiate(Engine, parent);

        // Published before the interface object is built, because that object's own constructor asks for this
        // prototype: the two are mutually referential — `C.prototype.constructor === C` — and one of the two
        // has to be visible half-built. The prototype is the safe half, since nothing reads a member of it
        // during the interface object's construction.
        _prototypes[definition.Index] = prototype;

        var interfaceObject = _interfaceObjects[definition.Index];
        if (interfaceObject is null)
        {
            interfaceObject = new DomInterfaceObject(this, definition);
            _interfaceObjects[definition.Index] = interfaceObject;
        }

        // The per-realm `constructor` slot, filled with the sanctioned in-place slot replacement: the name is
        // declared by the shape, so a shaped object never falls back to a dictionary for it and the prototype
        // stays in shared-layout mode. Anything else here — Set, or DefineOwnProperty with a fresh descriptor
        // under a name the shape did not declare — would cost the shape, and with it the inline caching the
        // whole design exists for.
        prototype.DefineOwnPropertyUnchecked(
            "constructor",
            new PropertyDescriptor(interfaceObject, PropertyFlag.NonEnumerable));

        return prototype;
    }

    /// <summary>
    /// The interface object — the global <c>HTMLDivElement</c> — in this engine, created on first use.
    /// </summary>
    /// <remarks>
    /// Routed through <see cref="PrototypeOf"/> rather than creating one here, because the two are built
    /// together and creating one from each side would produce two.
    /// </remarks>
    internal DomInterfaceObject InterfaceObjectOf(DomInterfaceDefinition definition)
    {
        PrototypeOf(definition);
        return _interfaceObjects[definition.Index]!;
    }

    /// <summary>
    /// Projects an AngleSharp object into script, giving back the wrapper it already has when it has one.
    /// </summary>
    /// <remarks>
    /// This is the general entry, reached when a value arrives from outside a generated member — a host
    /// handing over a document, or a member whose declared type is a base of what it returned. A generated
    /// member whose declared return type is already precise calls a typed overload instead.
    /// </remarks>
    internal JsValue Wrap(object? value)
    {
        if (value is null)
        {
            return JsValue.Null;
        }

        if (_wrappers.TryGetValue(value, out var cached))
        {
            return cached;
        }

        var definition = DomTypeMap.For(value.GetType());
        if (definition is null)
        {
            Throw.TypeError(
                PrincipalRealm,
                "'" + value.GetType().FullName + "' implements no interface the DOM bindings were generated from.");
        }

        return Cache(value, Create(definition!, value));
    }

    /// <summary>Projects a node, which is what most generated members return.</summary>
    internal JsValue WrapNodeValue(INode? node) => node is null ? JsValue.Null : WrapNode(node);

    /// <summary>Projects a node, giving back the one wrapper it has for the life of this engine.</summary>
    internal DomNodeObject WrapNode(INode node)
    {
        if (_wrappers.TryGetValue(node, out var cached))
        {
            return (DomNodeObject) cached;
        }

        var definition = DomTypeMap.For(node.GetType()) ?? DomInterfaces.Node;
        return (DomNodeObject) Cache(node, new DomNodeObject(this, definition, node));
    }

    /// <summary>
    /// Projects an <c>IHtmlCollection&lt;T&gt;</c>, whose element type the calling generated member knows.
    /// </summary>
    internal JsValue WrapCollection<T>(IHtmlCollection<T>? collection) where T : class, IElement
    {
        if (collection is null)
        {
            return JsValue.Null;
        }

        if (_wrappers.TryGetValue(collection, out var cached))
        {
            return cached;
        }

        var definition = DomTypeMap.For(collection.GetType()) ?? DomInterfaces.HTMLCollection;
        return Cache(collection, new DomHtmlCollectionObject<T>(this, definition, collection));
    }

    private ObjectInstance Create(DomInterfaceDefinition definition, object value)
    {
        if (definition.WrapperKind == DomWrapperKind.Node)
        {
            return new DomNodeObject(this, definition, (INode) value);
        }

        if (definition.WrapperKind == DomWrapperKind.HtmlCollection)
        {
            return DomTypeMap.WrapHtmlCollection(this, definition, value)
                ?? Unsupported(value, "is an HTMLCollection over an element type the bindings never saw in a member signature");
        }

        var accessor = definition.CollectionAccessor;
        if (accessor is null)
        {
            return new DomObject(this, definition, value);
        }

        return definition.WrapperKind == DomWrapperKind.NamedMap
            ? new DomNamedMapObject(this, definition, value, accessor)
            : new DomCollectionObject(this, definition, value, accessor);
    }

    private ObjectInstance Unsupported(object value, string what)
    {
        Throw.TypeError(PrincipalRealm, "'" + value.GetType().FullName + "' " + what + ".");
        return null!;
    }

    private ObjectInstance Cache(object key, ObjectInstance wrapper)
    {
        // GetValue rather than Add: two wrappers for one object can only be built by a re-entrant member, and
        // the table is what decides which one wins, so identity is never split.
        return _wrappers.GetValue(key, _ => wrapper);
    }
}
