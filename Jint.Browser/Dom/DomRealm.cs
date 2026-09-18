using System.Runtime.CompilerServices;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Dom.Collections;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.Browser.Dom;

/// <summary>
/// Realm-bound DOM constructors and prototypes, backed by the engine's single native-object wrapper cache.
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
    private readonly ObjectInstance?[] _pristineLengthGetters;
    private readonly ConditionalWeakTable<object, ObjectInstance> _wrappers;
    private readonly DomRealm _principal;
    private readonly ConditionalWeakTable<Realm, DomRealm> _secondaryRealms = new();
    private readonly ConditionalWeakTable<INode, DomRealm> _creationRealms;
    private readonly ConditionalWeakTable<IBrowsingContext, DomRealm> _contexts;
    private readonly ConditionalWeakTable<IElement, AriaElementReflection.Cache> _ariaCaches = new();
    private Dictionary<string, JsString>? _htmlUppercasedTagNames;
    private int _nodes;
    private DomHostHooks _hooks = DomHostHooks.Default;
    private int _maxNodes;
    private bool _scriptingEnabled = true;

    private DomRealm(Engine engine, Realm? realm = null, DomRealm? principal = null)
    {
        Engine = engine;
        OwningRealm = realm ?? engine._mainRealm;
        _principal = principal ?? this;
        _wrappers = principal?._wrappers ?? new();
        _creationRealms = principal?._creationRealms ?? new();
        _contexts = principal?._contexts ?? new();
        if (principal is null)
        {
            engine.Disposed += (_, _) => Release();
        }
        // The manual interfaces continue the generated ones' indices, so the two together stay one dense
        // array; DomManualInterfaces says why there are any.
        var interfaceCount = DomInterfaces.All.Length + DomManualInterfaces.All.Length;
        _prototypes = new ObjectInstance?[interfaceCount];
        _interfaceObjects = new DomInterfaceObject?[interfaceCount];
        _pristineLengthGetters = new ObjectInstance?[interfaceCount];
    }

    private void Release()
    {
        _wrappers.Clear();
        _creationRealms.Clear();
        _contexts.Clear();
        _secondaryRealms.Clear();
        Document = null;
        _realms.Remove(Engine);
    }

    /// <summary>The engine every object in this realm belongs to.</summary>
    internal Engine Engine { get; }

    /// <summary>Brackets a Browser-owned native mutation, including reentrant script and failures.</summary>
    internal Layout.PageLayout.MutationScope MutateLayout()
        => Runtime.PageRuntime.Find(Engine)?.Layout.BeginMutation() ?? default;

    /// <summary>
    /// The realm owning these constructors and prototypes, captured independently of the currently
    /// running realm. Node identity and creation associations are shared across the engine.
    /// </summary>
    internal Realm OwningRealm { get; }

    /// <summary>
    /// Where the members that parse markup into the tree go. The default calls AngleSharp directly, which is
    /// what a binding with no runtime behind it can do; the parser driver replaces it.
    /// </summary>
    internal DomHostHooks Hooks { get => _principal._hooks; set => _principal._hooks = value; }

    /// <summary>
    /// How many nodes this engine may project into script, or zero for no limit
    /// (<see cref="BrowserOptions.MaxDomNodes"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// It counts <b>wrappers</b>, which is deliberately not the same quantity the parse bounds. The wrapper
    /// table is what a script's DOM growth actually costs an engine, and it is the one place every projection
    /// passes through — so this bounds what one document hands to script, while the parse bounds what the
    /// document itself holds. Seeding this from the parsed size instead would make merely <i>walking</i> a
    /// document of the permitted size a refusal, which is a limit no page could live with.
    /// </para>
    /// <para>
    /// Set by the page runtime. A binding used without one — <c>DomBindings.Install</c> alone — counts
    /// nothing, which is what a host embedding the projection on its own asked for.
    /// </para>
    /// </remarks>
    internal int MaxNodes { get => _principal._maxNodes; set => _principal._maxNodes = value; }

    /// <summary>
    /// Whether the document's own markup may run script, which is what HTML calls <i>scripting enabled</i>.
    /// </summary>
    /// <remarks>
    /// <c>Emulation.setScriptExecutionDisabled</c> turns it off for the next document, and what it decides
    /// here is one thing: an <c>onclick=</c> and its kind are not compiled, so a handler content attribute is
    /// text on an element and nothing else. It is a field on the realm rather than a lookup because
    /// <c>WrapperCreated</c> asks it once per wrapper. A host embedding the projection on its own gets the
    /// default, which is that markup handlers work.
    /// </remarks>
    internal bool ScriptingEnabled { get => _principal._scriptingEnabled; set => _principal._scriptingEnabled = value; }

    /// <summary>How many node wrappers this engine has made, for a diagnostic and for the tests.</summary>
    internal int NodeCount => _principal._nodes;

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

    /// <summary>
    /// The frozen arrays this engine last answered for one element's ARIA element-reflecting members,
    /// created on first use.
    /// </summary>
    /// <remarks>
    /// <b>Only the cache is here.</b> The relationships themselves are engine-free and live in
    /// <see cref="AriaElementReferences"/>, which says why; what belongs to a realm is the array object a
    /// getter handed to script, because WebIDL's <c>FrozenArray</c> identity is per engine. A table of its own
    /// rather than a field on the wrapper: the members that need it are eight of the two thousand a document's
    /// elements carry, so an element that never has an ARIA relationship must not pay a reference for one.
    /// Keyed on the AngleSharp element and never on the wrapper, so it dies with the element rather than with
    /// whichever wrapper happened to reach it first.
    /// </remarks>
    internal AriaElementReflection.Cache AriaCacheFor(IElement element) => _ariaCaches.GetOrCreateValue(element);

    /// <summary>
    /// The maximum number of distinct <a
    /// href="https://dom.spec.whatwg.org/#concept-element-qualified-name">qualified names</a> this engine
    /// will memoize an <see cref="HtmlUppercasedTagName"/> answer for.
    /// </summary>
    /// <remarks>
    /// A real document's distinct tag-name set is nowhere near this: HTML, SVG and MathML together define a
    /// few hundred element names, and even a component-heavy page's custom elements number in the tens to
    /// low hundreds. The cap exists for the page that is not a real document — a script that manufactures a
    /// fresh <c>document.createElement('x-' + i)</c> name on every iteration purely to read
    /// <c>tagName</c> — which would otherwise grow this table by one entry per call for the life of the
    /// engine. Past the cap the answer is still correct, computed the way it always was; it is only the memo
    /// that stops growing.
    /// </remarks>
    private const int MaxCachedUppercasedTagNames = 1024;

    /// <summary>
    /// The finished <see cref="JsString"/> for <c>tagName</c>/<c>nodeName</c>'s
    /// <a href="https://dom.spec.whatwg.org/#element-html-uppercased-qualified-name">HTML-uppercased
    /// qualified name</a>, memoized by the element's (not-yet-uppercased) qualified name so a repeated read
    /// of the same element interface allocates nothing after the first.
    /// </summary>
    /// <remarks>
    /// Sound because the uppercasing is a pure function of the qualified name alone: <see cref="DomHostHooks.TagName"/>
    /// only ever calls this once it has already decided the element is in the HTML namespace and its owner is
    /// an <see cref="IHtmlDocument"/>, so every qualified name reaching this cache needs the same answer
    /// regardless of which element asked — an SVG element sharing a local name with an HTML one never reaches
    /// here at all, because that decision is made by the caller before the qualified name is looked up.
    /// </remarks>
    internal JsString HtmlUppercasedTagName(string qualifiedName)
    {
        var cache = _htmlUppercasedTagNames ??= new Dictionary<string, JsString>(StringComparer.Ordinal);
        if (cache.TryGetValue(qualifiedName, out var cached))
        {
            return cached;
        }

        // JsString.CachedCreate is a process-wide cache keyed by value, not per-realm state — sound here
        // because a JsString carries no engine affinity — so it also reuses the wrapper object itself across
        // engines for the common (<= 10 character) tag names, on top of this memo's saving of the uppercase
        // computation.
        var uppercased = JsString.CachedCreate(DomHostHooks.AsciiUppercase(qualifiedName));
        if (cache.Count < MaxCachedUppercasedTagNames)
        {
            cache[qualifiedName] = uppercased;
        }

        return uppercased;
    }

    /// <summary>The binding state of <paramref name="engine"/>, created on first use.</summary>
    internal static DomRealm Of(Engine engine) => _realms.GetValue(engine, static e => new DomRealm(e));

    /// <summary>Constructor/prototype state in a fully initialized same-engine realm.</summary>
    internal static DomRealm Of(Engine engine, Realm realm)
    {
        Validate(engine, realm);
        var principal = Of(engine);
        return ReferenceEquals(realm, engine._mainRealm)
            ? principal
            : principal._secondaryRealms.GetValue(realm, r => new DomRealm(engine, r, principal));
    }

    internal static void Validate(Engine engine, Realm realm)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(realm);
        if (realm.Intrinsics is null || realm.GlobalObject is null || realm.GlobalEnv is null)
        {
            throw new ArgumentException("The realm is not fully initialized.", nameof(realm));
        }
        if (!ReferenceEquals(realm.GlobalObject.Engine, engine))
        {
            throw new ArgumentException("The realm belongs to a different engine.", nameof(realm));
        }
    }

    /// <summary>The document associated with this realm's global, if any.</summary>
    internal IDocument? Document { get; private set; }

    internal IHtmlScriptElement? CurrentScript { get; set; }

    internal string? ReadyState { get; set; }

    internal bool LoadCompleted { get; set; }

    internal void AssociateContext(IBrowsingContext context)
    {
        if (_contexts.TryGetValue(context, out var existing) && !ReferenceEquals(existing, this))
        {
            throw new ArgumentException("The browsing context already belongs to another realm.", nameof(context));
        }
        _contexts.GetValue(context, _ => this);
    }

    internal void AssociateDocument(IDocument document, bool associatedGlobal = false)
    {
        var associated = _creationRealms.TryGetValue(document, out var existing);
        if (associated && !ReferenceEquals(existing, this))
        {
            throw new ArgumentException("The document already belongs to another realm.", nameof(document));
        }
        AssociateContext(document.Context);
        _creationRealms.GetValue(document, _ => this);
        if (!associated)
        {
            RecordSubtree(document);
        }
        if (associatedGlobal)
        {
            Document = document;
        }
    }

    /// <summary>Associates a newly opened window document while retaining old documents' creation brands.</summary>
    internal void AssociateWindowDocument(IDocument document)
    {
        // AngleSharp may open srcdoc again in the same context during element setup. The new global owns
        // subsequent parser nodes; documents and wrappers from the previous opening retain their realm.
        _contexts.Remove(document.Context);
        AssociateDocument(document, associatedGlobal: true);
    }

    internal bool TryGetDocumentRealm(IDocument document, out DomRealm? realm)
        => _creationRealms.TryGetValue(document, out realm);

    internal DomRealm RealmOfDocument(IDocument document)
    {
        if (_creationRealms.TryGetValue(document, out var realm))
        {
            return realm;
        }
        realm = _contexts.TryGetValue(document.Context, out var contextRealm) ? contextRealm : this;
        realm.AssociateDocument(document);
        return realm;
    }

    // https://dom.spec.whatwg.org/#concept-create-node: a node retains its creation realm through
    // adoption. The owner is consulted only at the creation/adoption boundary, never for a known node.
    internal DomRealm CreationRealmOf(INode node)
    {
        if (_creationRealms.TryGetValue(node, out var known))
        {
            return known;
        }
        if (node is IDocument document)
        {
            var documentRealm = _contexts.TryGetValue(document.Context, out var contextRealm) ? contextRealm : this;
            documentRealm.AssociateDocument(document);
            return documentRealm;
        }
        var realm = node.Owner is { } owner ? RealmOfDocument(owner) : this;
        return _creationRealms.GetValue(node, _ => realm);
    }

    /// <summary>Records a new or about-to-be-adopted subtree, including non-light-tree descendants.</summary>
    internal void RecordSubtree(INode root)
    {
        var pending = new Stack<INode>();
        pending.Push(root);
        while (pending.TryPop(out var node))
        {
            CreationRealmOf(node);
            foreach (var child in node.ChildNodes)
            {
                pending.Push(child);
            }
            if (node is IElement element)
            {
                DomNamespaces.Capture(element);
                foreach (var attribute in element.Attributes)
                {
                    _creationRealms.GetValue(attribute, _ => node.Owner is { } owner ? RealmOfDocument(owner) : this);
                }
                if (element.ShadowRoot is { } shadow)
                {
                    pending.Push(shadow);
                }
            }
            if (node is IHtmlTemplateElement template)
            {
                pending.Push(template.Content);
            }
        }
    }

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
                ? OwningRealm.Intrinsics.EventTarget.PrototypeObject
                : OwningRealm.Intrinsics.Object.PrototypeObject;

        using var scope = new RealmScope(Engine, OwningRealm);
        var prototype = definition.Shape.Instantiate(Engine, parent);
        JsObjectShape.SetHostState(prototype, this);

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

        CaptureLengthAccessor(definition, prototype);

        return prototype;
    }

    /// <summary>
    /// Records the <c>length</c> getter a collection interface's prototype was created with, which is what
    /// <see cref="DomCollectionBase.PristineLengthGetter"/> answers and the engine's length lane compares
    /// against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It has to be taken <b>here</b>, while the prototype is exactly what the shape declared. Reading it
    /// later would capture whatever a page had already put there, and the lane would then treat a tampered
    /// accessor as the pristine one. Materializing the accessor pair costs one <c>ClrFunction</c> per
    /// collection prototype per engine and does not move <c>_propertiesVersion</c>, which is what the lane's
    /// guard is measured against.
    /// </para>
    /// <para>
    /// Only the two collection wrapper kinds are asked, because only those are <c>ArrayLikeObject</c>s. A
    /// node that merely carries an indexed getter — <c>form</c>, <c>select</c> — is a
    /// <c>DomIndexedNodeObject</c> and reads its <c>length</c> the ordinary way.
    /// </para>
    /// </remarks>
    private void CaptureLengthAccessor(DomInterfaceDefinition definition, ObjectInstance prototype)
    {
        if (definition.WrapperKind is not (DomWrapperKind.Collection or DomWrapperKind.HtmlCollection))
        {
            return;
        }

        _pristineLengthGetters[definition.Index] = prototype.GetOwnProperty("length").Get as ObjectInstance;
    }

    /// <summary>
    /// The <c>length</c> getter <paramref name="definition"/>'s prototype was created with in this engine, or
    /// <see langword="null"/> when the interface declares none.
    /// </summary>
    internal ObjectInstance? PristineLengthGetterOf(DomInterfaceDefinition definition)
        => _pristineLengthGetters[definition.Index];

    /// <summary>
    /// The interface prototype when it has already been created, without making a page that never reached
    /// the interface pay for it. Conditional installers use this when taking a member away again.
    /// </summary>
    internal ObjectInstance? ExistingPrototypeOf(DomInterfaceDefinition definition)
        => _prototypes[definition.Index];

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
    /// member whose declared return type is already precise calls a typed overload instead. An explicit
    /// definition selects a <c>DomReturnType</c> projection when the object implements multiple IDL interfaces.
    /// </remarks>
    internal JsValue Wrap(object? value, DomInterfaceDefinition? definition = null)
    {
        if (value is null)
        {
            return JsValue.Null;
        }

        if (_wrappers.TryGetValue(value, out var cached))
        {
            return cached;
        }

        definition ??= value is INode node
            ? DomManualInterfaces.For(node) ?? DomTypeMap.For(value.GetType())
            : DomTypeMap.For(value.GetType());
        if (definition is null)
        {
            Throw.TypeError(
                OwningRealm,
                "'" + value.GetType().FullName + "' implements no interface the DOM bindings were generated from.");
        }

        if (value is INode newNode && !_creationRealms.TryGetValue(newNode, out _))
        {
            RecordSubtree(newNode);
        }
        var realm = value is INode createdNode ? CreationRealmOf(createdNode) : this;
        return Cache(value, realm.Create(definition!, value));
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

        if (!_creationRealms.TryGetValue(node, out _))
        {
            RecordSubtree(node);
        }
        var definition = DomManualInterfaces.For(node) ?? DomTypeMap.For(node.GetType()) ?? DomInterfaces.Node;
        return (DomNodeObject) Cache(node, CreationRealmOf(node).NewNode(definition, node));
    }

    /// <summary>
    /// The node wrapper an interface asks for: a plain one, or one carrying the interface's indexed and named
    /// property projection (<c>form[0]</c>, <c>form.username</c>, <c>select[0]</c>). Both are node wrappers,
    /// because a node's wrapper is what the engine's tree-dispatch lane keys on.
    /// </summary>
    private DomNodeObject NewNode(DomInterfaceDefinition definition, INode node)
        => definition.WrapperKind == DomWrapperKind.IndexedNode && definition.CollectionAccessor is { } accessor
            ? new DomIndexedNodeObject(this, definition, node, accessor)
            : new DomNodeObject(this, definition, node);

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

        var definition = DomTypeMap.For(collection.GetType());

        // document.all is the one collection whose own interface decides the wrapper: HTML gives it a named
        // lookup, an item(), a legacy caller and an internal slot no HTMLCollection has, and it arrives here
        // because the generated Document.all getter's declared return type is IHtmlCollection<IElement>.
        if (definition?.WrapperKind == DomWrapperKind.HtmlAllCollection && collection is IHtmlAllCollection all)
        {
            return Cache(collection, new DomHtmlAllCollectionObject(this, definition, all));
        }

        if (definition?.WrapperKind != DomWrapperKind.HtmlCollection)
        {
            // AngleSharp's QueryCollection also implements INodeList. The member's IDL return type,
            // not that extra CLR interface, decides whether named properties belong on this result.
            definition = DomInterfaces.HTMLCollection;
        }

        return Cache(collection, new DomHtmlCollectionObject<T>(this, definition, collection));
    }

    /// <summary>
    /// Projects the <b>static</b> <c>NodeList</c> a selector match produced, as
    /// <a href="https://dom.spec.whatwg.org/#dom-parentnode-queryselectorall">DOM §4.2.6</a> defines it:
    /// "the static result of running scope-match a selectors string".
    /// </summary>
    /// <remarks>
    /// <para>
    /// The snapshot is the binding's own (<see cref="DomStaticNodeList"/>) rather than AngleSharp's, because
    /// nothing about an <see cref="INodeList"/> says whether it is live and the wrapper keeps one element
    /// wrapper per index. It is cached like every other wrapper, so <c>Hooks.WrapperCreated</c> fires once
    /// for it; the snapshot is new on every call, which keeps
    /// <c>el.querySelectorAll('x') !== el.querySelectorAll('x')</c> — DOM's answer, and the one the binding
    /// already gave.
    /// </para>
    /// <para>
    /// The wrapper is the ordinary <see cref="DomCollectionObject"/> every other <c>NodeList</c> gets, and
    /// deliberately so: the memo is a branch inside that one class rather than a second
    /// <c>ArrayLikeObject</c> beside it, so the live <c>NodeList</c>s and this one go on sharing the class
    /// the interpreter's array-like read lane devirtualizes. <see cref="DomCollectionObject"/> records why
    /// that is a contract rather than a preference.
    /// </para>
    /// </remarks>
    internal JsValue WrapStaticNodeList(IHtmlCollection<IElement> matches)
    {
        var snapshot = new DomStaticNodeList(matches);
        return Cache(snapshot, new DomCollectionObject(this, DomInterfaces.NodeList, snapshot, DomAccessorNodeList.Instance));
    }

    /// <summary>Projects the live <c>NodeList</c> of labels associated with a labelable element.</summary>
    internal JsValue WrapLabels(IHtmlElement control)
    {
        var labels = new DomLabelNodeList(control);
        return Cache(labels, new DomCollectionObject(this, DomInterfaces.NodeList, labels, DomAccessorNodeList.Instance));
    }

    /// <summary>Projects an element's <c>dataset</c> through HTML's name conversion algorithms.</summary>
    internal JsValue WrapStringMap(IElement element, IStringMap map)
    {
        if (_wrappers.TryGetValue(map, out var cached))
        {
            return cached;
        }

        var target = new DomStringMapAdapter(this, element);
        return Cache(map, new DomNamedMapObject(this, DomInterfaces.DOMStringMap, target, DomAccessorDOMStringMap.Instance));
    }

    private ObjectInstance Create(DomInterfaceDefinition definition, object value)
    {
        if (definition.WrapperKind is DomWrapperKind.Node or DomWrapperKind.IndexedNode)
        {
            return NewNode(definition, (INode) value);
        }

        if (definition.WrapperKind == DomWrapperKind.HtmlAllCollection)
        {
            return value is IHtmlAllCollection all
                ? new DomHtmlAllCollectionObject(this, definition, all)
                : Unsupported(value, "is projected as HTMLAllCollection but is not an IHtmlAllCollection");
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
        Throw.TypeError(OwningRealm, "'" + value.GetType().FullName + "' " + what + ".");
        return null!;
    }

    private ObjectInstance Cache(object key, ObjectInstance wrapper)
    {
        // GetValue rather than Add: two wrappers for one object can only be built by a re-entrant member, and
        // the table is what decides which one wins, so identity is never split.
        var cached = _wrappers.GetValue(key, _ => wrapper);

        // Only the wrapper that won the race is decorated, and only once, because the table is what decides
        // which one exists at all.
        if (!ReferenceEquals(cached, wrapper))
        {
            return cached;
        }

        Hooks.WrapperCreated((wrapper as IDomWrapper)?.DomRealm ?? this, key, wrapper);

        if (wrapper is DomNodeObject)
        {
            // Counted after the table took it, and the throw is after the count, so a script catching the
            // RangeError and asking again is refused again rather than charged again — and the node it asked
            // for keeps the one wrapper it now has.
            _principal._nodes++;

            if (MaxNodes > 0 && NodeCount > MaxNodes)
            {
                Throw.RangeError(
                    OwningRealm,
                    "The document has reached the " + MaxNodes.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + "-node limit this browser was configured with.");
            }
        }

        return cached;
    }
}
