using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.CustomElements;

/// <summary>
/// <a href="https://html.spec.whatwg.org/multipage/custom-elements.html#customelementregistry">The
/// <c>CustomElementRegistry</c></a> a document reaches as <c>window.customElements</c>: the definitions, the
/// <c>whenDefined</c> promises, and the reaction lane that runs a definition's callbacks.
/// </summary>
/// <remarks>
/// <para>
/// <b>One registry per document, which here is one per engine</b>, because a navigation builds a new engine:
/// a definition does not survive a navigation, which is what a browser does too.
/// </para>
/// <para>
/// <b>The element state is a side table.</b> AngleSharp's element carries no custom element state, no
/// definition and no reaction queue, and adding one is not this package's to do — so
/// <see cref="CustomElementRecord"/> hangs off a
/// <see cref="ConditionalWeakTable{TKey,TValue}"/> keyed on the element, exactly as the wrapper cache does.
/// A record is made only for an element that could become custom, so an ordinary document allocates none.
/// </para>
/// </remarks>
internal sealed partial class CustomElementRegistry : ObjectInstance
{
    private readonly PageRuntime _runtime;
    private readonly Dictionary<string, CustomElementDefinition> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<ObjectInstance, CustomElementDefinition> _byConstructor = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, PromiseCapability> _whenDefined = new(StringComparer.Ordinal);
    private readonly ConditionalWeakTable<IElement, CustomElementRecord> _records = new();
    private bool _definitionIsRunning;

    internal CustomElementRegistry(PageRuntime runtime, ObjectInstance prototype, HostInterfaceObject interfaceObject)
        : base(runtime.Engine)
    {
        _runtime = runtime;
        InterfaceObject = interfaceObject;
        _checkpoint = RunCheckpoint;
        Prototype = prototype;
    }

    /// <summary>The global <c>CustomElementRegistry</c>, built together with this object's prototype.</summary>
    internal HostInterfaceObject InterfaceObject { get; }

    /// <summary>Whether any definition exists, which is what every hot path tests before doing anything.</summary>
    internal bool HasDefinitions => _byName.Count > 0;

    /// <inheritdoc />
    public override string ToString() => "[object CustomElementRegistry]";

    /// <summary>The registry of the engine <paramref name="engine"/> is, if it has ever been asked for.</summary>
    /// <remarks>
    /// Deliberately not the lazy accessor: a page that never mentions <c>customElements</c> must not build a
    /// registry merely because something wrapped a node or wrote an attribute.
    /// </remarks>
    internal static CustomElementRegistry? Of(Engine engine) => PageRuntime.Find(engine)?.CustomElementsIfCreated;

    /// <summary>The receiver check every member of the interface starts with.</summary>
    internal static CustomElementRegistry Brand(JsValue thisObject, string member)
    {
        if (thisObject is CustomElementRegistry registry)
        {
            return registry;
        }

        var message = "Failed to execute '" + member + "' on 'CustomElementRegistry': Illegal invocation";

        if (thisObject is ObjectInstance instance)
        {
            Throw.TypeError(instance.Engine.Realm, message);
        }

        Throw.TypeErrorNoEngine(message);
        return null!;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/custom-elements.html#dom-customelementregistry-define.
    /// </summary>
    internal JsValue Define(JsValue[] arguments)
    {
        var realm = _runtime.Engine._mainRealm;
        var name = TypeConverter.ToString(arguments.At(0));
        var constructorValue = arguments.At(1);

        if (constructorValue is not ObjectInstance constructor || !constructorValue.IsConstructor)
        {
            Throw.TypeError(realm, "Failed to execute 'define' on 'CustomElementRegistry': parameter 2 is not a constructor.");
            return JsValue.Undefined;
        }

        if (!CustomElementNames.IsValid(name))
        {
            ThrowDomException("define", DomExceptionNames.Syntax, "'" + name + "' is not a valid custom element name.");
        }

        if (_byName.ContainsKey(name))
        {
            ThrowDomException("define", DomExceptionNames.NotSupported, "the name '" + name + "' has already been used with this registry.");
        }

        if (_byConstructor.ContainsKey(constructor))
        {
            ThrowDomException("define", DomExceptionNames.NotSupported, "this constructor has already been used with this registry.");
        }

        var localName = name;
        var interfaceDefinition = DomInterfaces.HTMLElement;

        if (arguments.At(2) is ObjectInstance options && options.Get("extends") is { } extendsValue && !extendsValue.IsUndefined() && !extendsValue.IsNull())
        {
            localName = TypeConverter.ToString(extendsValue);

            if (CustomElementNames.IsValid(localName))
            {
                ThrowDomException("define", DomExceptionNames.NotSupported, "'" + localName + "' is a custom element name, so it cannot be extended.");
            }

            interfaceDefinition = BuiltInInterface(localName)
                ?? ThrowDomExceptionAndReturn<DomInterfaceDefinition>(
                    "'" + localName + "' is not the name of an element this browser has an interface for.");
        }

        if (_definitionIsRunning)
        {
            ThrowDomException("define", DomExceptionNames.NotSupported, "a definition is already being processed.");
        }

        _definitionIsRunning = true;
        CustomElementDefinition definition;

        try
        {
            definition = Read(name, localName, constructor, interfaceDefinition);
        }
        finally
        {
            _definitionIsRunning = false;
        }

        _byName[name] = definition;
        _byConstructor[constructor] = definition;

        // https://html.spec.whatwg.org/multipage/custom-elements.html#upgrade-candidates: every element of
        // the document that now matches, in tree order. A detached element is deliberately not a candidate —
        // it becomes custom when it enters the document, which is DOM's own insertion step.
        if (_runtime.Document is { } document)
        {
            EnsureWatching(document);
            Walk(document, element =>
            {
                if (Matches(element, definition))
                {
                    EnqueueUpgrade(element, definition);
                }
            });
        }

        if (_whenDefined.TryGetValue(name, out var capability))
        {
            _whenDefined.Remove(name);
            capability.Resolve(constructor);
        }

        // `define` is a [CEReactions] operation, so its upgrades run before it returns to the script.
        Drain();
        return JsValue.Undefined;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/custom-elements.html#dom-customelementregistry-get.</summary>
    internal JsValue Get(JsValue[] arguments)
        => _byName.TryGetValue(TypeConverter.ToString(arguments.At(0)), out var definition)
            ? definition.Constructor
            : JsValue.Undefined;

    /// <summary>https://html.spec.whatwg.org/multipage/custom-elements.html#dom-customelementregistry-getname.</summary>
    internal JsValue GetName(JsValue[] arguments)
        => arguments.At(0) is ObjectInstance constructor && _byConstructor.TryGetValue(constructor, out var definition)
            ? JsString.Create(definition.Name)
            : JsValue.Null;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/custom-elements.html#dom-customelementregistry-whendefined.
    /// </summary>
    /// <remarks>
    /// A promise per name, kept until the definition arrives, so several callers waiting on one name share
    /// it — which is what makes the resolution one call rather than a list to walk.
    /// </remarks>
    internal JsValue WhenDefined(JsValue[] arguments)
    {
        var engine = _runtime.Engine;
        var realm = engine._mainRealm;
        var name = TypeConverter.ToString(arguments.At(0));

        if (!CustomElementNames.IsValid(name))
        {
            var capability = PromiseConstructor.NewPromiseCapability(engine, realm.Intrinsics.Promise);
            capability.Reject(realm.Intrinsics.DomException.CreateException(
                DomExceptionNames.Syntax,
                "Failed to execute 'whenDefined' on 'CustomElementRegistry': '" + name + "' is not a valid custom element name."));
            return capability.PromiseInstance;
        }

        if (_byName.TryGetValue(name, out var definition))
        {
            var settled = PromiseConstructor.NewPromiseCapability(engine, realm.Intrinsics.Promise);
            settled.Resolve(definition.Constructor);
            return settled.PromiseInstance;
        }

        if (!_whenDefined.TryGetValue(name, out var pending))
        {
            pending = PromiseConstructor.NewPromiseCapability(engine, realm.Intrinsics.Promise);
            _whenDefined[name] = pending;
        }

        return pending.PromiseInstance;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/custom-elements.html#dom-customelementregistry-upgrade — the
    /// shadow-including inclusive descendants of <paramref name="arguments"/>'s node, in tree order.
    /// </summary>
    internal JsValue Upgrade(JsValue[] arguments)
    {
        if (arguments.At(0) is not IDomWrapper { DomTarget: INode node })
        {
            Throw.TypeError(
                _runtime.Engine._mainRealm,
                "Failed to execute 'upgrade' on 'CustomElementRegistry': parameter 1 is not of type 'Node'.");
            return JsValue.Undefined;
        }

        UpgradeSubtree(node);
        Drain();
        return JsValue.Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/custom-elements.html#look-up-a-custom-element-definition.
    /// </summary>
    /// <remarks>
    /// The autonomous clause is tried first and the customized-built-in clause second, which is the order the
    /// standard gives: a name that is both a definition's name and a definition's local name cannot happen,
    /// since a valid custom element name may not be extended.
    /// </remarks>
    internal CustomElementDefinition? Lookup(string? namespaceUri, string localName, string? isValue)
    {
        if (_byName.Count == 0)
        {
            return null;
        }

        if (namespaceUri is not null && !string.Equals(namespaceUri, HtmlNamespace, StringComparison.Ordinal))
        {
            return null;
        }

        if (_byName.TryGetValue(localName, out var autonomous) && autonomous.IsAutonomous)
        {
            return autonomous;
        }

        if (isValue is not null
            && _byName.TryGetValue(isValue, out var customized)
            && string.Equals(customized.LocalName, localName, StringComparison.Ordinal))
        {
            return customized;
        }

        return null;
    }

    /// <summary>The definition <paramref name="constructor"/> was registered with, if it was.</summary>
    internal CustomElementDefinition? DefinitionOf(ObjectInstance constructor)
        => _byConstructor.TryGetValue(constructor, out var definition) ? definition : null;

    /// <summary>The HTML namespace, which is the only one a custom element lives in.</summary>
    internal const string HtmlNamespace = "http://www.w3.org/1999/xhtml";

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/custom-elements.html#element-definition, steps 11.1 to 11.13:
    /// everything read off the constructor, in the order the standard reads it.
    /// </summary>
    private CustomElementDefinition Read(
        string name,
        string localName,
        ObjectInstance constructor,
        DomInterfaceDefinition interfaceDefinition)
    {
        var realm = _runtime.Engine._mainRealm;
        var prototypeValue = constructor.Get("prototype");

        if (prototypeValue is not ObjectInstance prototype)
        {
            Throw.TypeError(realm, "Failed to execute 'define' on 'CustomElementRegistry': constructor prototype is not an object.");
            return null!;
        }

        var definition = new CustomElementDefinition(name, localName, constructor, prototype, interfaceDefinition)
        {
            ConnectedCallback = Callback(prototype, "connectedCallback"),
            DisconnectedCallback = Callback(prototype, "disconnectedCallback"),
            AdoptedCallback = Callback(prototype, "adoptedCallback"),
            AttributeChangedCallback = Callback(prototype, "attributeChangedCallback"),
        };

        if (definition.AttributeChangedCallback is not null)
        {
            definition.ObservedAttributes = Sequence(constructor.Get("observedAttributes"));
        }

        foreach (var feature in Sequence(constructor.Get("disabledFeatures")))
        {
            definition.DisableInternals |= string.Equals(feature, "internals", StringComparison.Ordinal);
            definition.DisableShadow |= string.Equals(feature, "shadow", StringComparison.Ordinal);
        }

        definition.FormAssociated = TypeConverter.ToBoolean(constructor.Get("formAssociated"));
        return definition;
    }

    /// <summary>One lifecycle callback, which WebIDL converts to a <c>Function</c> or refuses.</summary>
    private ICallable? Callback(ObjectInstance prototype, string name)
    {
        var value = prototype.Get(name);

        if (value.IsUndefined())
        {
            return null;
        }

        if (value is ICallable callable)
        {
            return callable;
        }

        Throw.TypeError(
            _runtime.Engine._mainRealm,
            "Failed to execute 'define' on 'CustomElementRegistry': " + name + " is not a function.");
        return null;
    }

    /// <summary>A WebIDL <c>sequence&lt;DOMString&gt;</c>, or an empty one when the member was absent.</summary>
    private string[] Sequence(JsValue value)
    {
        if (value.IsUndefined())
        {
            return [];
        }

        var names = new List<string>();
        var iterator = value.GetIterator(_runtime.Engine._mainRealm);

        try
        {
            while (iterator.TryIteratorStepValue(out var element))
            {
                names.Add(TypeConverter.ToString(element));
            }
        }
        catch
        {
            iterator.CloseIfNotDone(CompletionType.Throw);
            throw;
        }

        return [.. names];
    }

    /// <summary>
    /// The interface an element of <paramref name="localName"/> would get, or <see langword="null"/> when
    /// HTML has none — which is what makes <c>{ extends: 'bogus' }</c> a <c>NotSupportedError</c>.
    /// </summary>
    /// <remarks>
    /// The element is created and thrown away rather than looked up in a table, because the table would be a
    /// second answer to a question <c>DomTypeMap</c> already answers: the interface a local name gets is
    /// whatever AngleSharp builds for it. <c>define</c> is rare enough for one element to cost nothing.
    /// </remarks>
    private DomInterfaceDefinition? BuiltInInterface(string localName)
    {
        if (_runtime.Document is not { } document)
        {
            return null;
        }

        IElement probe;

        try
        {
            probe = document.CreateElement(localName);
        }
        catch (AngleSharp.Dom.DomException)
        {
            return null;
        }

        if (probe is AngleSharp.Html.Dom.IHtmlUnknownElement)
        {
            return null;
        }

        return DomManualInterfaces.For(probe) ?? DomTypeMap.For(probe.GetType());
    }

    private void ThrowDomException(string member, string name, string detail)
    {
        var engine = _runtime.Engine;
        var exception = engine._mainRealm.Intrinsics.DomException.CreateException(
            name,
            "Failed to execute '" + member + "' on 'CustomElementRegistry': " + detail);

        var location = engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(engine, exception, in location);
    }

    private T ThrowDomExceptionAndReturn<T>(string detail)
    {
        ThrowDomException("define", DomExceptionNames.NotSupported, detail);
        return default!;
    }
}
