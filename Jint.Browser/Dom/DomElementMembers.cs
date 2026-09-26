using Jint.HtmlParser;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Dom;

/// <summary>WebIDL conversions and DOM §4.9 operations over native element identities.</summary>
internal static class DomElementMembers
{
    /// <summary>DOM §4.2.2: the public assigned slot omits a closed shadow root's slot.</summary>
    internal static JsValue AssignedSlot(DomRealm realm, Node target)
        => realm.WrapNodeValue(SlotAssignment.FindSlot(target, openOnly: true, realm.NativeReadCheckpoint, realm.CancellationToken));

    /// <summary>https://dom.spec.whatwg.org/#dom-element-hasattributes.</summary>
    internal static JsValue HasAttributes(Element target) => DomConvert.Bool(target.AttributeCount != 0);

    /// <summary>https://dom.spec.whatwg.org/#dom-element-getattributenames.</summary>
    /// <remarks>The qualified names, in the attribute list's own order, which is the order the parser made.</remarks>
    internal static JsValue GetAttributeNames(DomRealm realm, Element target)
    {
        var names = new List<JsValue>(target.AttributeCount);

        foreach (var attribute in target.Attributes)
        {
            names.Add(JsString.Create(attribute.Name));
        }

        return realm.OwningRealm.Intrinsics.Array.Construct([.. names]);
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-element-toggleattribute.</summary>
    internal static JsValue ToggleAttribute(Element target, JsValue[] arguments)
    {
        const string Member = "Element.toggleAttribute";

        var name = DomConvert.RequiredText(arguments, 0, Member);

        if (!DomNames.IsValidAttributeLocalName(name))
        {
            throw Jint.HtmlParser.DomException.InvalidCharacter();
        }

        // Step 2: an HTML element in the HTML namespace lower-cases the name, which is the same fold
        // setAttribute makes and the reason `toggleAttribute("FOO")` and `hasAttribute("foo")` agree. The
        // namespace is the element's own, which is what makes them go on agreeing for an
        // element created in no namespace under an HTML parent.
        if (target.OwnerDocument?.Kind == DocumentKind.Html && string.Equals(target.NamespaceUri, Namespaces.Html, StringComparison.Ordinal))
        {
            name = UrlCharacters.AsciiLowercase(name);
        }

        var present = target.GetAttributeNode(name) is not null;
        var given = arguments.Length > 1 && !arguments[1].IsUndefined();
        var force = given && TypeConverter.ToBoolean(arguments[1]);

        if (!present)
        {
            if (given && !force)
            {
                return JsBoolean.False;
            }

            // Step 4: the value is the empty string, so a boolean content attribute reads as present.
            target.SetAttribute(name, string.Empty);
            return JsBoolean.True;
        }

        if (given && force)
        {
            return JsBoolean.True;
        }

        target.RemoveAttribute(name);
        return JsBoolean.False;
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-element-getattributenode.</summary>
    internal static JsValue GetAttributeNode(DomRealm realm, Element target, JsValue[] arguments)
        => realm.Wrap(target.GetAttributeNode(DomConvert.RequiredText(arguments, 0, "Element.getAttributeNode")));

    /// <summary>https://dom.spec.whatwg.org/#dom-element-getattributenodens.</summary>
    internal static JsValue GetAttributeNodeNS(DomRealm realm, Element target, JsValue[] arguments)
        => realm.Wrap(target.GetAttributeNodeNS(
            DomConvert.NullableText(arguments, 0),
            DomConvert.RequiredText(arguments, 1, "Element.getAttributeNodeNS")));

    /// <summary>https://dom.spec.whatwg.org/#dom-element-setattributenode.</summary>
    internal static JsValue SetAttributeNode(DomRealm realm, Element target, JsValue[] arguments)
        => realm.Wrap(target.SetAttributeNode(
            DomBindings.Argument<Attr>(arguments, 0, "Element.setAttributeNode")));

    /// <summary>https://dom.spec.whatwg.org/#dom-element-setattributenodens.</summary>
    internal static JsValue SetAttributeNodeNS(DomRealm realm, Element target, JsValue[] arguments)
        => realm.Wrap(target.SetAttributeNode(
            DomBindings.Argument<Attr>(arguments, 0, "Element.setAttributeNodeNS")));

    /// <summary>https://dom.spec.whatwg.org/#dom-element-removeattributenode.</summary>
    /// <remarks>
    /// DOM removes the attribute <em>node</em> rather than a name: an <c>Attr</c> the element does not hold
    /// is a <c>NotFoundError</c>, which is why this compares the map's own entry by identity rather than
    /// handing the name to <c>RemoveNamedItem</c> and trusting it.
    /// </remarks>
    internal static JsValue RemoveAttributeNode(DomRealm realm, Element target, JsValue[] arguments)
    {
        const string Member = "Element.removeAttributeNode";

        var attribute = DomBindings.Argument<Attr>(arguments, 0, Member);
        target.RemoveAttributeNode(attribute);
        return realm.Wrap(attribute);
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-element-insertadjacentelement.</summary>
    internal static JsValue InsertAdjacentElement(DomRealm realm, Element target, JsValue[] arguments)
    {
        const string Member = "Element.insertAdjacentElement";

        var where = DomConvert.RequiredText(arguments, 0, Member);
        var node = DomBindings.Argument<Element>(arguments, 1, Member);
        return realm.WrapNodeValue(InsertAdjacent(realm, target, where, node, Member));
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-element-insertadjacenttext.</summary>
    /// <remarks>
    /// The one member outside the exclusion table: upstream's own testharness result renderer calls it, and
    /// its absence is why the browser lane's <c>testharnessreport.js</c> turns that renderer off.
    /// </remarks>
    internal static JsValue InsertAdjacentText(DomRealm realm, Element target, JsValue[] arguments)
    {
        const string Member = "Element.insertAdjacentText";

        var where = DomConvert.RequiredText(arguments, 0, Member);
        var data = DomConvert.RequiredText(arguments, 1, Member);

        InsertAdjacent(realm, target, where, target.OwnerDocument!.CreateTextNode(data), Member);
        return JsValue.Undefined;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#insert-adjacent — the four positions, ASCII case-insensitively, and a
    /// <c>SyntaxError</c> for anything else.
    /// </summary>
    /// <remarks>
    /// <c>beforebegin</c> and <c>afterend</c> answer <see langword="null"/> for an element with no parent
    /// rather than refusing, which is where this differs from <c>insertAdjacentHTML</c>'s
    /// <c>NoModificationAllowedError</c> for the same position.
    /// </remarks>
    private static Node? InsertAdjacent(DomRealm realm, Element target, string where, Node node, string member)
    {
        if (!ReferenceEquals(node.OwnerDocument, target.OwnerDocument)) realm.RecordSubtree(node);

        if (string.Equals(where, "beforebegin", StringComparison.OrdinalIgnoreCase))
        {
            return target.ParentNode?.InsertBefore(node, target);
        }

        if (string.Equals(where, "afterbegin", StringComparison.OrdinalIgnoreCase))
        {
            return target.InsertBefore(node, target.FirstChild);
        }

        if (string.Equals(where, "beforeend", StringComparison.OrdinalIgnoreCase))
        {
            return target.AppendChild(node);
        }

        if (string.Equals(where, "afterend", StringComparison.OrdinalIgnoreCase))
        {
            return target.ParentNode?.InsertBefore(node, target.NextSibling);
        }

        DomFailures.Refuse(
            realm,
            member,
            DomExceptionNames.Syntax,
            "The value provided ('" + where + "') is not one of 'beforeBegin', 'afterBegin', 'beforeEnd', or 'afterEnd'.");
        return null;
    }
}
