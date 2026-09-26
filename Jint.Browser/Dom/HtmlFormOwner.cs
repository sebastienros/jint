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
        if (root is Element rootElement && ReferenceEquals(Of(rootElement), form)) yield return rootElement;
        foreach (var element in NodeTraversal.DescendantElements(root, work.Check, token))
        {
            if (ReferenceEquals(Of(element), form)) yield return element;
        }
        work.Check();

    }

    internal static bool IsFormAssociated(Element element) => HtmlFormState.IsFormAssociated(element);

    internal static bool IsListed(Element element) => HtmlFormState.IsListed(element);

    // The existing native category bit and owner history cover FACE as well as built-ins.
    internal static Element? OfFormAssociatedCustomElement(Element element, Action<int>? checkpoint = null, CancellationToken token = default)
    {
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        var owner = Of(element);
        work.Check();
        return owner;
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

}
