using Jint.HtmlParser;
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
/// The third creation path, <c>new MyElement()</c>, arrives at <see cref="TryConstruct"/> through
/// <see cref="DomInterfaceObject"/>, which refuses every other <c>new</c>.
/// </para>
/// </remarks>
internal static class CustomElementCreation
{
    /// <summary>https://dom.spec.whatwg.org/#dom-document-createelement.</summary>
    internal static JsValue CreateElement(DomRealm realm, Document document, JsValue[] arguments)
        => Create(
            realm,
            document,
            DomConvert.RequiredText(arguments, 0, "Document.createElement"),
            namespaceUri: null,
            namespaced: false,
            DomConvert.At(arguments, 1));

    /// <summary>https://dom.spec.whatwg.org/#dom-document-createelementns.</summary>
    internal static JsValue CreateElementNS(DomRealm realm, Document document, JsValue[] arguments)
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
    internal static JsValue CloneNode(DomRealm realm, Node node, JsValue[] arguments)
    {
        var documentDefinition = node is Document ? realm.WrapNode(node).Definition : null;
        var deep = DomConvert.OptionalBool(arguments, 0, false);
        var clone = node.CloneNode(deep);
        if (node is Document original && clone is Document documentCopy)
        {
            var sourceState = DomDocumentState.Of(original);
            var copyState = DomDocumentState.Of(documentCopy);
            copyState.Origin = sourceState.Origin;
            copyState.Url = sourceState.Url;
            copyState.Referrer = sourceState.Referrer;
            copyState.CharacterSet = sourceState.CharacterSet;
            copyState.AboutBaseUrl = sourceState.AboutBaseUrl;
        }
        realm.RecordSubtree(clone);
        CustomElementRegistry.Cloned(realm, node, clone);
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
        ObjectInstance activeFunctionObject,
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

        // Step 2: "If NewTarget is equal to the active function object, then throw a TypeError." An
        // interface object registered as its own constructor — `customElements.define('x-y', HTMLElement)` —
        // is the only way to reach the constructor with the two equal, and HTML refuses it because there is
        // no subclass whose prototype the element could take. Without this the definition is found and
        // `new HTMLElement()` quietly answers an element.
        if (ReferenceEquals(target, activeFunctionObject))
        {
            Throw.TypeError(
                realm.Engine._mainRealm,
                "Illegal constructor: '" + interfaceDefinition.Name + "' is registered as its own custom element constructor, so NewTarget is the active function object.");
        }

        instance = registry.ConstructBase(interfaceDefinition, definition, target);
        return true;
    }

    private static JsValue Create(DomRealm realm, Document document, string localName, string? namespaceUri, bool namespaced, JsValue options)
    {
        var isValue = ReadIs(realm, options);
        if (CustomElementRegistry.Of(realm.Engine) is not { HasDefinitions: true } registry)
        {
            return realm.WrapNodeValue(Build(document, localName, namespaceUri, namespaced, isValue));
        }

        var lowered = !namespaced && document.Kind == DocumentKind.Html ? AsciiLower(localName) : localName;
        // https://dom.spec.whatwg.org/#validate-and-extract: `createElementNS` takes a *qualified* name, and
        // everything after it — the definition lookup, the element the constructor has to produce — is about
        // the local name that validate-and-extract splits out of it. `createElement` does no extraction at
        // all, so a colon there is part of the local name and this is the namespaced member's step alone.
        var lookupName = namespaced ? LocalNameOf(lowered) : lowered;
        // A definition is only ever in the HTML namespace, so the namespaced member looks up under the
        // namespace it was given — `createElementNS(null, 'x-thing')` is in *no* namespace and matches none —
        // while `createElement` is the HTML one by definition. The document is create-an-element's own
        // argument, and it is what makes the two members answer an uncustomized element for a document with
        // no browsing context: see CustomElementRegistry.Lookup's step 1.
        var definition = registry.Lookup(document, namespaced ? namespaceUri : CustomElementRegistry.HtmlNamespace, lookupName, isValue);

        if (definition is null)
        {
            var plain = Build(document, localName, namespaceUri, namespaced, isValue);

            if (isValue is not null)
            {
                registry.RecordFor(plain).IsValue = isValue;
            }

            return realm.WrapNodeValue(plain);
        }

        if (definition.IsAutonomous)
        {
            return registry.ConstructAutonomous(definition, document, lookupName, namespaced ? PrefixOf(localName) : null);
        }

        // Step 5: a customized built-in is created as its built-in and then upgraded, so `super()` answers
        // the button that already exists.
        var element = Build(document, localName, namespaceUri, namespaced, isValue);
        registry.RecordFor(element).IsValue = isValue;
        registry.Upgrade(element, definition);
        registry.Drain();
        return realm.WrapNodeValue(element);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#validate-and-extract step 4: the local name of a qualified name is what
    /// follows its first colon.
    /// </summary>
    private static string AsciiLower(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= 'A' and <= 'Z') chars[i] = (char) (chars[i] + ('a' - 'A'));
        }
        return new string(chars);
    }

    private static string LocalNameOf(string qualifiedName)
    {
        var colon = qualifiedName.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? qualifiedName : qualifiedName[(colon + 1)..];
    }

    /// <summary>The other half of validate-and-extract: what precedes the first colon, or nothing.</summary>
    /// <remarks>
    /// Read off the name as the script wrote it rather than off the lower-cased one, because a prefix keeps
    /// its case.
    /// </remarks>
    private static string? PrefixOf(string qualifiedName)
    {
        var colon = qualifiedName.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? null : qualifiedName[..colon];
    }

    private static Element Build(Document document, string localName, string? namespaceUri, bool namespaced, string? isValue)
        => namespaced
            ? document.CreateElementNS(namespaceUri, localName, isValue)
            : document.CreateElement(localName, isValue);

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
