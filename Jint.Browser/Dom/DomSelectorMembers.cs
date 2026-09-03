using System.Diagnostics.CodeAnalysis;
using AngleSharp.Dom;
using Jint.Native;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>
/// The five members that take a CSS selector, declared by hand so that a selector the parser refuses is the
/// <c>SyntaxError</c> DOM prescribes rather than a CLR exception nothing in the page can catch.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why these five are not generated.</b> Everything about them is ordinary except their failure: DOM says
/// "if <i>selectors</i> cannot be parsed, throw a <c>SyntaxError</c> <c>DOMException</c>", and a page depends
/// on being able to <i>catch</i> that. AngleSharp raises its own <c>AngleSharp.Dom.DomException</c> instead,
/// which is a CLR exception — it walks straight through a script's <c>try</c>/<c>catch</c>, out of the page
/// loop and into the host. jQuery 3.7 is the proof and it is not an edge case: its support detection asks for
/// <c>:has(*,:jqfake)</c> inside a <c>try</c> and reads the throw as "this engine has no <c>:has()</c>", so
/// on an unwrapped binding jQuery does not load at all.
/// </para>
/// <para>
/// <b>The <see cref="NullReferenceException"/> in the catch is AngleSharp's defect, contained rather than
/// worked around.</b> <c>AngleSharp.Css</c> 1.0.2's <c>CssSelectorConstructor.HasFunctionState.Produce()</c>
/// dereferences null for a functional pseudo-class it cannot complete — <c>:has(*,:jqfake)</c> is one — so
/// that input is not a <c>DomException</c> but an <c>NRE</c>. It is recorded in <c>Jint.Browser/AGENTS.md</c>'s
/// divergence table for the maintainer to raise upstream; what this file does is keep <i>this binding's</i>
/// contract coherent, which is that a selector member never throws a CLR exception at the host. When
/// AngleSharp fixes it, the <c>NRE</c> arm becomes dead and can go.
/// </para>
/// <para>
/// The <c>try</c> is around the call rather than a validating parse in front of it, because a second parse
/// on every <c>querySelectorAll</c> would be a cost every page pays for an error almost none of them make.
/// </para>
/// </remarks>
internal static class DomSelectorMembers
{
    /// <summary>https://dom.spec.whatwg.org/#dom-parentnode-queryselector</summary>
    internal static JsValue QuerySelector(DomRealm realm, IParentNode target, JsValue[] arguments, string member)
    {
        var selectors = DomConvert.RequiredText(arguments, 0, member);

        try
        {
            return realm.WrapNodeValue(target.QuerySelector(selectors));
        }
        catch (Exception exception) when (IsParseFailure(exception))
        {
            return Refuse(realm, member, selectors);
        }
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-parentnode-queryselectorall</summary>
    internal static JsValue QuerySelectorAll(DomRealm realm, IParentNode target, JsValue[] arguments, string member)
    {
        var selectors = DomConvert.RequiredText(arguments, 0, member);

        try
        {
            return realm.WrapCollection<IElement>(target.QuerySelectorAll(selectors));
        }
        catch (Exception exception) when (IsParseFailure(exception))
        {
            return Refuse(realm, member, selectors);
        }
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-element-closest</summary>
    internal static JsValue Closest(DomRealm realm, IElement target, JsValue[] arguments)
    {
        var selectors = DomConvert.RequiredText(arguments, 0, "Element.closest");

        try
        {
            return realm.WrapNodeValue(target.Closest(selectors));
        }
        catch (Exception exception) when (IsParseFailure(exception))
        {
            return Refuse(realm, "Element.closest", selectors);
        }
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-element-matches</summary>
    internal static JsValue Matches(DomRealm realm, IElement target, JsValue[] arguments, string member)
    {
        var selectors = DomConvert.RequiredText(arguments, 0, member);

        try
        {
            return DomConvert.Bool(target.Matches(selectors));
        }
        catch (Exception exception) when (IsParseFailure(exception))
        {
            return Refuse(realm, member, selectors);
        }
    }

    /// <summary>Whether an exception is the selector parser giving up, in either of the two ways it does.</summary>
    private static bool IsParseFailure(Exception exception)
        => exception is DomException or NullReferenceException;

    /// <summary>The <c>SyntaxError</c> DOM prescribes, in the wording a browser uses.</summary>
    [DoesNotReturn]
    private static JsValue Refuse(DomRealm realm, string member, string selectors)
        => DomFailures.Refuse(
            realm.Engine,
            member,
            DomExceptionNames.Syntax,
            "'" + selectors + "' is not a valid selector.");
}
