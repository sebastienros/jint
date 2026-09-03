using AngleSharp.Dom;
using Jint.Browser.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.CustomElements;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#concept-create-element">Create an element</a> for the two members a
/// script creates one through, and the <c>HTMLElement</c> constructor those two and every upgrade end in.
/// </summary>
/// <remarks>
/// <para>
/// <c>document.createElement</c> and <c>createElementNS</c> are <c>skip</c>ped in the binding's override
/// table and re-declared over these bodies, because the element a defined name produces is the
/// <b>constructor's</b> and not AngleSharp's: with the synchronous custom elements flag set, "create an
/// element" runs the constructor and answers whatever it made. Everything else about the two members —
/// the name validation, the lower-casing, the namespace — stays AngleSharp's call, so a document with no
/// definition at all behaves exactly as it did before.
/// </para>
/// <para>
/// The third creation path, <c>new MyElement()</c>, arrives at <see cref="TryConstruct"/> through
/// <see cref="DomInterfaceObject"/>, which refuses every other <c>new</c>.
/// </para>
/// </remarks>
internal static class CustomElementCreation
{
    /// <summary>https://dom.spec.whatwg.org/#dom-document-createelement.</summary>
    internal static JsValue CreateElement(DomRealm realm, IDocument document, JsValue[] arguments)
        => Create(
            realm,
            document,
            DomConvert.RequiredText(arguments, 0, "Document.createElement"),
            namespaceUri: null,
            DomConvert.At(arguments, 1));

    /// <summary>https://dom.spec.whatwg.org/#dom-document-createelementns.</summary>
    internal static JsValue CreateElementNS(DomRealm realm, IDocument document, JsValue[] arguments)
        => Create(
            realm,
            document,
            DomConvert.RequiredText(arguments, 1, "Document.createElementNS"),
            DomConvert.RequiredText(arguments, 0, "Document.createElementNS"),
            DomConvert.At(arguments, 2));

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-node-clone — a clone of a custom element is a custom element,
    /// which DOM gets by cloning with the synchronous custom elements flag unset and letting the upgrade
    /// reaction run when the <c>[CEReactions]</c> operation returns.
    /// </summary>
    /// <remarks>
    /// The <c>deep</c> default is AngleSharp's rather than DOM's, unchanged from what the generated member
    /// passed: this replaces the body to upgrade what it produced and nothing else.
    /// </remarks>
    internal static JsValue CloneNode(DomRealm realm, INode node, JsValue[] arguments)
    {
        var clone = node.Clone(DomConvert.OptionalBool(arguments, 0, true));
        CustomElementRegistry.SubtreeCreated(realm, clone);
        return realm.WrapNodeValue(clone);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/custom-elements.html#html-element-constructors — what
    /// <c>super()</c> does inside a registered constructor.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the engine has no registry, or when <paramref name="newTarget"/> is not a
    /// registered constructor — which is what makes <c>new HTMLElement()</c> and <c>new HTMLDivElement()</c>
    /// go on answering <c>Illegal constructor</c>.
    /// </returns>
    internal static bool TryConstruct(
        DomRealm realm,
        DomInterfaceDefinition interfaceDefinition,
        JsValue newTarget,
        out ObjectInstance instance)
    {
        instance = null!;

        if (newTarget is not ObjectInstance target
            || CustomElementRegistry.Of(realm.Engine) is not { } registry
            || registry.DefinitionOf(target) is not { } definition)
        {
            return false;
        }

        instance = registry.ConstructBase(interfaceDefinition, definition, target);
        return true;
    }

    private static JsValue Create(DomRealm realm, IDocument document, string localName, string? namespaceUri, JsValue options)
    {
        if (CustomElementRegistry.Of(realm.Engine) is not { HasDefinitions: true } registry)
        {
            return realm.WrapNodeValue(Build(document, localName, namespaceUri));
        }

        // The lower-casing AngleSharp does for an HTML document, done here as well because the lookup happens
        // before the element exists. A definition's name can only be lower-case, so this is what lets
        // `createElement('X-THING')` find one.
        var lowered = document is AngleSharp.Html.Dom.IHtmlDocument ? localName.ToLowerInvariant() : localName;
        var isValue = ReadIs(realm, options);
        var definition = registry.Lookup(namespaceUri ?? CustomElementRegistry.HtmlNamespace, lowered, isValue);

        if (definition is null)
        {
            var plain = Build(document, localName, namespaceUri);

            if (isValue is not null)
            {
                registry.RecordFor(plain).IsValue = isValue;
            }

            return realm.WrapNodeValue(plain);
        }

        if (definition.IsAutonomous)
        {
            // Step 6.1: the constructor is called with an empty construction stack, so `super()` is what
            // creates the element — which is why a constructor may call createElement of its own name.
            return registry.ConstructAutonomous(definition, document, lowered);
        }

        // Step 5: a customized built-in is created as its built-in and then upgraded, so `super()` answers
        // the button that already exists.
        var element = Build(document, localName, namespaceUri);
        registry.RecordFor(element).IsValue = isValue;
        registry.Upgrade(element, definition);
        registry.Drain();
        return realm.WrapNodeValue(element);
    }

    private static IElement Build(IDocument document, string localName, string? namespaceUri)
        => namespaceUri is null ? document.CreateElement(localName) : document.CreateElement(namespaceUri, localName);

    /// <summary>
    /// <c>ElementCreationOptions</c>'s one member. A dictionary is only read when it is an object, which is
    /// what WebIDL says for an optional dictionary argument.
    /// </summary>
    private static string? ReadIs(DomRealm realm, JsValue options)
    {
        if (options is not ObjectInstance dictionary)
        {
            return null;
        }

        var value = dictionary.Get("is");
        return value.IsUndefined() ? null : TypeConverter.ToString(value);
    }
}
