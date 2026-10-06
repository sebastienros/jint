using Jint.HtmlParser;
using Jint.Browser.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.CustomElements;

/// <summary>
/// The two halves of construction: the <c>HTMLElement</c> constructor a registered class reaches through
/// <c>super()</c>, and the synchronous creation <c>document.createElement</c> makes of an autonomous element.
/// </summary>
internal sealed partial class CustomElementRegistry
{
    /// <summary>
    /// The namespace prefix the <c>createElementNS</c> in progress owes the element its constructor is about
    /// to make, taken by the first <see cref="NewElement"/> of that construction and cleared by it.
    /// </summary>
    private string? _pendingPrefix;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/custom-elements.html#html-element-constructors, from step 5.
    /// </summary>
    /// <param name="interfaceDefinition">The interface object <c>super()</c> reached — the active function object.</param>
    /// <param name="definition">The definition <c>NewTarget</c> is registered with.</param>
    /// <param name="newTarget">The constructor the <c>new</c> started at.</param>
    internal ObjectInstance ConstructBase(
        DomInterfaceDefinition interfaceDefinition,
        CustomElementDefinition definition,
        ObjectInstance newTarget)
    {
        var realm = _runtime.Engine._mainRealm;

        // Step 5, both branches at once: an autonomous element's definition records HTMLElement and a
        // customized built-in's records the interface its local name maps to, so "the interface of the
        // active function object" is answered from the definition rather than from a table of valid local
        // names.
        if (!Implements(definition.Interface, interfaceDefinition))
        {
            Throw.TypeError(
                realm,
                "Failed to construct '" + definition.Name + "': the custom element is registered for '"
                + definition.Interface.Name + "', not for '" + interfaceDefinition.Name + "'.");
        }

        // Steps 7 and 8: the element's prototype is NewTarget's, and the interface prototype object only when
        // NewTarget has none of its own — which is what makes an upgrade swap a wrapper's prototype.
        var prototype = newTarget.Get("prototype") as ObjectInstance
            ?? definition.Prototype
            ?? _runtime.Dom.PrototypeOf(interfaceDefinition);

        if (definition.ConstructionStack.Count == 0)
        {
            return NewElement(definition, prototype);
        }

        // Steps 10 to 14: the element being upgraded, replaced with the already-constructed marker so that a
        // constructor reaching the base twice is told rather than handed a second element.
        var last = definition.ConstructionStack.Count - 1;
        var wrapper = definition.ConstructionStack[last];

        // "If element is an already constructed marker, then throw a TypeError." A plain TypeError and not a
        // DOMException: this is the one refusal in the HTML element constructor that a page reaches by
        // constructing its own class again from inside its constructor, and the corpus asserts the name.
        if (wrapper is null)
        {
            Throw.TypeError(
                realm,
                "Failed to construct '" + definition.Name + "': the element has already been constructed.");
        }

        definition.ConstructionStack[last] = null;
        wrapper!.Prototype = prototype;
        return wrapper;
    }

    /// <summary>
    /// Whether an element of <paramref name="element"/>'s interface may be constructed through
    /// <paramref name="active"/> — the interface itself, or one it inherits from.
    /// </summary>
    private static bool Implements(DomInterfaceDefinition element, DomInterfaceDefinition active)
    {
        for (DomInterfaceDefinition? current = element; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, active))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Step 9: the element a bare <c>new MyElement()</c> makes, which is created here rather than by the
    /// caller because only the definition knows its local name.
    /// </summary>
    private DomNodeObject NewElement(CustomElementDefinition definition, ObjectInstance prototype)
    {
        var document = _runtime.Document;

        if (document is null)
        {
            Throw.TypeError(
                _runtime.Engine._mainRealm,
                "Failed to construct '" + definition.Name + "': the window has no document to create an element in.");
        }

        var prefix = _pendingPrefix;
        _pendingPrefix = null;

        var element = prefix is null
            ? document!.CreateElement(definition.LocalName, definition.IsAutonomous ? null : definition.Name)
            : document!.CreateElementNS(HtmlNamespace, prefix + ":" + definition.LocalName, definition.IsAutonomous ? null : definition.Name);
        var record = RecordFor(element);

        record.Definition = definition;
        record.IsValue = definition.IsAutonomous ? null : definition.Name;
        record.FormAssociated = definition.IsAutonomous && definition.FormAssociated;
        if (record.FormAssociated) HtmlFormState.SetFormAssociatedCustomElement(element, true);
        record.State = CustomElementState.Custom;
        Snapshot(element, record, definition);

        var wrapper = _runtime.Dom.WrapNode(element);
        wrapper.Prototype = prototype;
        return wrapper;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-create-element step 6.1: with the synchronous custom elements
    /// flag set, the constructor is what makes the element, and what it answers is what
    /// <c>createElement</c> answers.
    /// </summary>
    /// <remarks>
    /// A constructor that throws is <b>reported</b> rather than propagated, and the caller is handed an
    /// element in the failed state — which is what keeps <c>document.createElement</c> from throwing at a
    /// page that only asked for an element.
    /// </remarks>
    internal JsValue ConstructAutonomous(CustomElementDefinition definition, Document document, string localName, string? prefix = null)
    {
        var enclosing = _pendingPrefix;
        _pendingPrefix = prefix;

        try
        {
            var constructed = _runtime.Engine.Construct(definition.Constructor, [], definition.Constructor, null);

            if (constructed is not DomNodeObject { Node: Element element }
                || !string.Equals(element.LocalName, localName, StringComparison.Ordinal)
                || !string.Equals(element.NamespaceUri, HtmlNamespace, StringComparison.Ordinal)
                || !ReferenceEquals(element.OwnerDocument, document)
                || element.AttributeCount > 0
                || element.FirstChild is not null
                || element.ParentNode is not null)
            {
                var engine = _runtime.Engine;
                var error = engine._mainRealm.Intrinsics.DomException.CreateException(
                    DomExceptionNames.NotSupported,
                    "Failed to execute 'createElement' on 'Document': the constructor of '" + definition.Name
                    + "' did not produce a new, empty '" + localName + "' element.");
                var location = engine._lastSyntaxElement?.Location ?? default;
                Throw.JavaScriptException(engine, error, in location);
            }

            Drain();
            return constructed;
        }
        catch (JavaScriptException exception)
        {
            Report(exception, definition.Name);

            var failed = document.CreateElement(localName);
            RecordFor(failed).State = CustomElementState.Failed;
            return _runtime.Dom.WrapNode(failed);
        }
        finally
        {
            _pendingPrefix = enclosing;
        }
    }
}
