using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Browser.Events;

/// <summary>HTML submit-button classification and the form's first submit button in tree order.</summary>
internal static class BrowserFormDefaults
{
    // https://html.spec.whatwg.org/multipage/form-elements.html#attr-button-type
    internal static bool IsSubmitButton(Element element, DomReadWork work)
    {
        work.Step();
        if (element.NamespaceUri != Namespaces.Html) return false;
        if (element.LocalName == "input")
            return HtmlInputTypes.Parse(work.Attribute(element, "type")) is HtmlInputType.Submit or HtmlInputType.Image;
        if (element.LocalName != "button") return false;
        var type = work.Attribute(element, "type");
        if (work.EqualAsciiIgnoreCase(type, "submit")) return true;
        if (work.EqualAsciiIgnoreCase(type, "reset") || work.EqualAsciiIgnoreCase(type, "button")) return false;
        // Missing and invalid button type is Auto, not an unconditional Submit state.
        return work.Attribute(element, "command") is null && work.Attribute(element, "commandfor") is null
            && element.ParentNode is not Element { NamespaceUri: Namespaces.Html, LocalName: "select" };
    }

    internal static bool IsSubmitButton(Element element)
    {
        var work = new DomReadWork(null, default);
        var result = IsSubmitButton(element, work);
        work.Check();
        return result;
    }

    // https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#default-button
    internal static Element? DefaultButton(DomRealm realm, Element form)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        var root = work.Root(form);
        foreach (var element in InclusiveElements(root, work))
        {
            if (!IsSubmitButton(element, work) || !ReferenceEquals(HtmlFormState.GetOwner(element), form)) continue;
            work.Check();
            return element;
        }
        work.Check();
        return null;
    }

    // Charge every link, including non-element runs and a final ascent that yields no element.
    // Also used by the demand-only selector root index; no second form-owner algorithm lives here.
    internal static IEnumerable<Element> InclusiveElements(Node root, DomReadWork work)
    {
        Node? current = root;
        while (current is not null)
        {
            work.Step();
            if (current is Element element) yield return element;
            work.Step();
            if (current.FirstChild is { } child)
            {
                current = child;
                continue;
            }
            while (current is not null && !ReferenceEquals(current, root))
            {
                work.Step();
                if (current.NextSibling is { } sibling)
                {
                    current = sibling;
                    break;
                }
                work.Step();
                current = current.ParentNode;
            }
            if (ReferenceEquals(current, root)) current = null;
        }
        work.Check();
    }
}
