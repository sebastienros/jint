using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>
/// The <c>Element</c> and <c>Node</c> members the DOM standard requires and AngleSharp has no
/// <c>[DomName]</c> for, so nothing could generate them.
/// </summary>
/// <remarks>
/// <para>
/// Every one of them is a member on the shortest path of an ordinary page — a client library that
/// feature-detects <c>toggleAttribute</c> takes a slower path when it is missing and one that does not
/// throws — and each fails the same way before a test can say anything about behaviour:
/// <c>Property '…' of object is not a function</c>.
/// </para>
/// <para>
/// <b>None of them re-implements anything.</b> The <c>Attr</c>-node family is AngleSharp's
/// <c>INamedNodeMap</c>, which has every one of the five under WebIDL's <em>other</em> spelling
/// (<c>getNamedItem</c> for <c>getAttributeNode</c>), and <c>insertAdjacent*</c> is DOM's four-position
/// switch over <c>InsertBefore</c> and <c>AppendChild</c>. What is here is the standard's spelling and the
/// standard's refusals.
/// </para>
/// </remarks>
internal static class DomElementMembers
{
    /// <summary>https://dom.spec.whatwg.org/#dom-node-issamenode.</summary>
    /// <remarks>
    /// Legacy — <c>===</c> answers it, and the wrapper cache makes <c>===</c> reliable — but normative, and
    /// <c>dom/nodes/Node-isSameNode.html</c> is nine assertions of it.
    /// </remarks>
    internal static JsValue IsSameNode(INode target, JsValue[] arguments)
        => DomConvert.Bool(ReferenceEquals(DomBindings.NullableArgument<INode>(arguments, 0, "Node.isSameNode"), target));

    /// <summary>https://dom.spec.whatwg.org/#dom-element-hasattributes.</summary>
    internal static JsValue HasAttributes(IElement target) => DomConvert.Bool(target.Attributes.Length != 0);

    /// <summary>https://dom.spec.whatwg.org/#dom-element-getattributenames.</summary>
    /// <remarks>The qualified names, in the attribute list's own order, which is the order the parser made.</remarks>
    internal static JsValue GetAttributeNames(DomRealm realm, IElement target)
    {
        var names = new List<JsValue>(target.Attributes.Length);

        foreach (var attribute in target.Attributes)
        {
            names.Add(JsString.Create(attribute.Name));
        }

        return realm.PrincipalRealm.Intrinsics.Array.Construct([.. names]);
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-element-toggleattribute.</summary>
    internal static JsValue ToggleAttribute(IElement target, JsValue[] arguments)
    {
        const string Member = "Element.toggleAttribute";

        var name = DomConvert.RequiredText(arguments, 0, Member);

        // Step 2: an HTML element in the HTML namespace lower-cases the name, which is the same fold
        // setAttribute makes and the reason `toggleAttribute("FOO")` and `hasAttribute("foo")` agree.
        if (target is IHtmlElement && string.Equals(target.NamespaceUri, NamespaceNames.HtmlUri, StringComparison.Ordinal))
        {
            name = name.ToLowerInvariant();
        }

        var present = target.HasAttribute(name);
        var given = arguments.Length > 1 && !arguments[1].IsUndefined();
        var force = given && TypeConverter.ToBoolean(arguments[1]);

        if (!present)
        {
            if (given && !force)
            {
                return JsBoolean.False;
            }

            // Step 4: the value is the empty string, so a boolean content attribute reads as present.
            target.SetAttribute(name, "");
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
    internal static JsValue GetAttributeNode(DomRealm realm, IElement target, JsValue[] arguments)
        => realm.Wrap(target.Attributes.GetNamedItem(DomConvert.RequiredText(arguments, 0, "Element.getAttributeNode")));

    /// <summary>https://dom.spec.whatwg.org/#dom-element-getattributenodens.</summary>
    internal static JsValue GetAttributeNodeNS(DomRealm realm, IElement target, JsValue[] arguments)
        => realm.Wrap(target.Attributes.GetNamedItem(
            DomConvert.NullableText(arguments, 0),
            DomConvert.RequiredText(arguments, 1, "Element.getAttributeNodeNS")));

    /// <summary>https://dom.spec.whatwg.org/#dom-element-setattributenode.</summary>
    internal static JsValue SetAttributeNode(DomRealm realm, IElement target, JsValue[] arguments)
        => realm.Wrap(target.Attributes.SetNamedItem(
            DomBindings.Argument<IAttr>(arguments, 0, "Element.setAttributeNode")));

    /// <summary>https://dom.spec.whatwg.org/#dom-element-setattributenodens.</summary>
    internal static JsValue SetAttributeNodeNS(DomRealm realm, IElement target, JsValue[] arguments)
        => realm.Wrap(target.Attributes.SetNamedItemWithNamespaceUri(
            DomBindings.Argument<IAttr>(arguments, 0, "Element.setAttributeNodeNS")));

    /// <summary>https://dom.spec.whatwg.org/#dom-element-removeattributenode.</summary>
    /// <remarks>
    /// DOM removes the attribute <em>node</em> rather than a name: an <c>Attr</c> the element does not hold
    /// is a <c>NotFoundError</c>, which is why this compares the map's own entry by identity rather than
    /// handing the name to <c>RemoveNamedItem</c> and trusting it.
    /// </remarks>
    internal static JsValue RemoveAttributeNode(DomRealm realm, IElement target, JsValue[] arguments)
    {
        const string Member = "Element.removeAttributeNode";

        var attribute = DomBindings.Argument<IAttr>(arguments, 0, Member);
        var held = target.Attributes.GetNamedItem(attribute.NamespaceUri, attribute.LocalName);

        if (!ReferenceEquals(held, attribute))
        {
            return DomFailures.Refuse(
                realm.Engine,
                Member,
                DomExceptionNames.NotFound,
                "The node provided is not an attribute of this element.");
        }

        target.Attributes.RemoveNamedItem(attribute.NamespaceUri, attribute.LocalName);
        return realm.Wrap(attribute);
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-element-insertadjacentelement.</summary>
    internal static JsValue InsertAdjacentElement(DomRealm realm, IElement target, JsValue[] arguments)
    {
        const string Member = "Element.insertAdjacentElement";

        var where = DomConvert.RequiredText(arguments, 0, Member);
        var node = DomBindings.Argument<IElement>(arguments, 1, Member);
        return realm.WrapNodeValue(InsertAdjacent(realm, target, where, node, Member));
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-element-insertadjacenttext.</summary>
    /// <remarks>
    /// The one member outside the exclusion table: upstream's own testharness result renderer calls it, and
    /// its absence is why the browser lane's <c>testharnessreport.js</c> turns that renderer off.
    /// </remarks>
    internal static JsValue InsertAdjacentText(DomRealm realm, IElement target, JsValue[] arguments)
    {
        const string Member = "Element.insertAdjacentText";

        var where = DomConvert.RequiredText(arguments, 0, Member);
        var data = DomConvert.RequiredText(arguments, 1, Member);

        InsertAdjacent(realm, target, where, target.Owner!.CreateTextNode(data), Member);
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
    private static INode? InsertAdjacent(DomRealm realm, IElement target, string where, INode node, string member)
    {
        if (string.Equals(where, "beforebegin", StringComparison.OrdinalIgnoreCase))
        {
            return target.Parent?.InsertBefore(node, target);
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
            return target.Parent?.InsertBefore(node, target.NextSibling);
        }

        DomFailures.Refuse(
            realm.Engine,
            member,
            DomExceptionNames.Syntax,
            "The value provided ('" + where + "') is not one of 'beforeBegin', 'afterBegin', 'beforeEnd', or 'afterEnd'.");
        return null;
    }
}
