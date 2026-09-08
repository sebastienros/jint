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
            namespaced: false,
            DomConvert.At(arguments, 1));

    /// <summary>https://dom.spec.whatwg.org/#dom-document-createelementns.</summary>
    /// <remarks>
    /// The namespace is DOM's `DOMString? namespace`, so <c>createElementNS(null, 'x')</c> creates an element
    /// in no namespace rather than one in a namespace spelled <c>"null"</c>. It is read here rather than by
    /// the generated conversion because this member's whole body is the host's; every other namespaced
    /// member takes the same argument through <c>DomConvert.NullableText</c>, which the emitter now selects
    /// from AngleSharp's own nullable-reference metadata.
    /// </remarks>
    internal static JsValue CreateElementNS(DomRealm realm, IDocument document, JsValue[] arguments)
        => Create(
            realm,
            document,
            DomConvert.RequiredText(arguments, 1, "Document.createElementNS"),
            DomConvert.NullableText(arguments, 0),
            namespaced: true,
            DomConvert.At(arguments, 2));

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-node-clone — a clone of a custom element is a custom element,
    /// which DOM gets by cloning with the synchronous custom elements flag unset and letting the upgrade
    /// reaction run when the <c>[CEReactions]</c> operation returns.
    /// </summary>
    /// <remarks>
    /// The <c>deep</c> default is DOM's own — <c>optional boolean deep = false</c> — rather than the
    /// <see langword="true"/> AngleSharp's <c>INode.Clone</c> takes when nothing passes one. A shallow clone
    /// is what <c>node.cloneNode()</c> means in every browser, and the difference is observable the moment a
    /// page clones a node that has children.
    /// </remarks>
    internal static JsValue CloneNode(DomRealm realm, INode node, JsValue[] arguments)
    {
        // `new Document()` and an XML parse share AngleSharp's IXmlDocument runtime type, while WebIDL gives
        // only the parse the XMLDocument brand. Carry the source wrapper's choice through DOM's clone steps.
        var documentDefinition = node is IDocument ? realm.WrapNode(node).Definition : null;
        var clone = node.Clone(DomConvert.OptionalBool(arguments, 0, false));

        // DOM's clone steps for a ProcessingInstruction are "set copy's target to node's target and copy's
        // data to node's data". AngleSharp's clone carries the target and drops the data, so
        // `document.createProcessingInstruction('t', 'd').cloneNode().data` was the empty string; the
        // divergence register records it.
        if (node is IProcessingInstruction instruction && clone is IProcessingInstruction copy)
        {
            copy.Data = instruction.Data;
        }

        Dom.Files.FileTransferRealm.ResetCopiedInputs(clone);
        CustomElementRegistry.SubtreeCreated(realm, clone);
        return documentDefinition is null ? realm.WrapNodeValue(clone) : realm.Wrap(clone, documentDefinition);
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

    private static JsValue Create(DomRealm realm, IDocument document, string localName, string? namespaceUri, bool namespaced, JsValue options)
    {
        if (CustomElementRegistry.Of(realm.Engine) is not { HasDefinitions: true } registry)
        {
            return realm.WrapNodeValue(Build(document, localName, namespaceUri, namespaced));
        }

        // The lower-casing AngleSharp does for an HTML document, done here as well because the lookup happens
        // before the element exists. A definition's name can only be lower-case, so this is what lets
        // `createElement('X-THING')` find one.
        var lowered = document is AngleSharp.Html.Dom.IHtmlDocument ? localName.ToLowerInvariant() : localName;
        var isValue = ReadIs(realm, options);
        // A definition is only ever in the HTML namespace, so the namespaced member looks up under the
        // namespace it was given — `createElementNS(null, 'x-thing')` is in *no* namespace and matches none —
        // while `createElement` is the HTML one by definition.
        var definition = registry.Lookup(namespaced ? namespaceUri : CustomElementRegistry.HtmlNamespace, lowered, isValue);

        if (definition is null)
        {
            var plain = Build(document, localName, namespaceUri, namespaced);

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
        var element = Build(document, localName, namespaceUri, namespaced);
        registry.RecordFor(element).IsValue = isValue;
        registry.Upgrade(element, definition);
        registry.Drain();
        return realm.WrapNodeValue(element);
    }

    /// <remarks>
    /// <para>
    /// The two members are two AngleSharp overloads, and which one is called is decided by the *member* and
    /// never by whether the namespace happens to be null: `createElement` is the one-argument overload, which
    /// is where an HTML document lower-cases the name and puts the element in the HTML namespace, and
    /// `createElementNS` is the two-argument one, which takes a null namespace as no namespace and leaves the
    /// name exactly as the script wrote it.
    /// </para>
    /// <para>
    /// <b>Which is why `createElement` on a document that is not an HTML one takes the two-argument
    /// overload.</b> https://dom.spec.whatwg.org/#dom-document-createelement lower-cases the name at step 2
    /// only "if this is an HTML document", and chooses the namespace at step 4 — the HTML namespace when this
    /// is an HTML document or its content type is `application/xhtml+xml`, and null otherwise. AngleSharp's
    /// one-argument overload does both unconditionally, so `xmlDoc.createElement('DIV')` came back as a
    /// lower-cased `div` in the HTML namespace where DOM asks for `DIV` in none.
    /// </para>
    /// </remarks>
    private static IElement Build(IDocument document, string localName, string? namespaceUri, bool namespaced)
    {
        if (namespaced)
        {
            return document.CreateElement(namespaceUri, localName);
        }

        if (document is AngleSharp.Html.Dom.IHtmlDocument)
        {
            return document.CreateElement(localName);
        }

        return document.CreateElement(NamespaceFor(document), localName);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-createelement step 4, for a document that is not an HTML
    /// one: the HTML namespace when its content type is <c>application/xhtml+xml</c>, and no namespace
    /// otherwise.
    /// </summary>
    private static string? NamespaceFor(IDocument document)
        => string.Equals(
            Dom.DomContentType.Of(document) ?? document.ContentType,
            Dom.DomContentType.Xhtml,
            StringComparison.Ordinal)
            ? CustomElementRegistry.HtmlNamespace
            : null;

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
