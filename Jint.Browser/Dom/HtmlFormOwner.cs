using Jint.HtmlParser;
using Jint.Browser.CustomElements;

namespace Jint.Browser.Dom;

/// <summary>HTML §4.10.18.3: Browser reads the native form-owner identity.</summary>
internal static class HtmlFormOwner
{
    internal static Element? Of(Element element) => HtmlFormState.GetOwner(element);

    // Include image inputs and images for submission/default-button callers. The
    // form.elements collection separately applies the listed-controls filter.
    internal static IEnumerable<Element> ControlsOf(Element form, Action<int>? checkpoint = null, CustomElementRegistry? customElements = null,
        CancellationToken token = default)
    {
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        var root = work.Root(form);
        if (root is Element rootElement && ReferenceEquals(OwnerOf(rootElement), form)) yield return rootElement;
        foreach (var element in NodeTraversal.DescendantElements(root, work.Check, token))
        {
            if (ReferenceEquals(OwnerOf(element), form)) yield return element;
        }
        work.Check();

        Element? OwnerOf(Element element)
            => customElements?.TryGetRecord(element) is { State: CustomElementState.Custom, FormAssociated: true }
                ? OfFormAssociatedCustomElement(element, checkpoint, token) : Of(element);
    }

    internal static bool IsFormAssociated(Element element) => HtmlFormState.IsFormAssociated(element);

    internal static bool IsListed(Element element) => HtmlFormState.IsListed(element);

    // HTML's custom-element definition determines this category in Browser. Native
    // built-in ownership must not be recomputed from current ancestry: parser
    // associations can intentionally differ until a reset trigger occurs.
    internal static Element? OfFormAssociatedCustomElement(Element element, Action<int>? checkpoint = null, CancellationToken token = default)
    {
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        if (work.Attribute(element, "form") is { } id)
        {
            var connectedRoot = work.Root(element);
            while (connectedRoot is ShadowRoot shadow) connectedRoot = work.Root(shadow.Host);
            if (connectedRoot is Document)
            {
                if (id.Length == 0) return null;
                var root = work.Root(element);
                if (root is Element candidate && work.Equal(work.Attribute(candidate, "id"), id))
                    return IsForm(candidate) ? candidate : null;
                foreach (var descendant in NodeTraversal.DescendantElements(root, work.Check, token))
                {
                    if (work.Equal(work.Attribute(descendant, "id"), id))
                        return IsForm(descendant) ? descendant : null;
                }
                return null;
            }
        }
        return NearestAncestor(element, "form", work);
    }

    // The label, legend and option IDL members borrow another element's owner.
    internal static Element? FormIdlOf(Element element, Action<int>? checkpoint = null, CancellationToken token = default)
    {
        if (element.NamespaceUri != Namespaces.Html) return null;
        return element.LocalName switch
        {
            "label" => HtmlLabelAssociation.ControlFor(element, checkpoint, token) is { } control ? Of(control) : null,
            "legend" => element.ParentNode is Element { NamespaceUri: Namespaces.Html, LocalName: "fieldset" } fieldset
                ? Of(fieldset) : null,
            "option" => HtmlSelectAncestry.GetNearestSelect(element, checkpoint, token) is { } select ? Of(select) : null,
            _ => Of(element),
        };
    }

    private static bool IsForm(Element element)
        => element is { NamespaceUri: Namespaces.Html, LocalName: "form" };

    private static Element? NearestAncestor(Element element, string localName, DomReadWork work)
    {
        for (var current = element.ParentNode; current is not null; current = current.ParentNode)
        {
            work.Step();
            if (current is Element { NamespaceUri: Namespaces.Html } ancestor && ancestor.LocalName == localName)
                return ancestor;
        }
        return null;
    }
}
