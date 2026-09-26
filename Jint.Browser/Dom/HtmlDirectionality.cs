using System.Text;
using Jint.HtmlParser;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Dom;

/// <summary>https://html.spec.whatwg.org/multipage/dom.html#the-directionality</summary>
internal static class HtmlDirectionality
{
    internal static bool IsAutoDirectionalityControl(Element element, Action<int>? checkpoint = null, CancellationToken token = default)
        => IsAutoDirectionalityControl(element, new DomReadWork(checkpoint, token));

    private static bool IsAutoDirectionalityControl(Element element, DomReadWork work)
        => element.NamespaceUri == Namespaces.Html && (element.LocalName == "textarea"
            || element.LocalName == "input" && HtmlInputTypes.Parse(work.Attribute(element, "type")) is HtmlInputType.Hidden or HtmlInputType.Text
                or HtmlInputType.Search or HtmlInputType.Tel or HtmlInputType.Url or HtmlInputType.Email
                or HtmlInputType.Password or HtmlInputType.Submit or HtmlInputType.Reset or HtmlInputType.Button);

    internal static string Of(Element element, Action<int>? checkpoint = null, CancellationToken token = default)
        => Of(element, new DomReadWork(checkpoint, token));

    private static string Of(Element element, DomReadWork work)
    {
        work.Check();
        while (true)
        {
            work.Step();
            var state = State(element, work);
            if (state is "ltr" or "rtl") return state;
            if (state == "auto" || element is { NamespaceUri: Namespaces.Html, LocalName: "bdi" })
                return Auto(element, work) ?? "ltr";
            if (element is { NamespaceUri: Namespaces.Html, LocalName: "input" }
                && HtmlInputTypes.Parse(work.Attribute(element, "type")) == HtmlInputType.Tel) return "ltr";
            var parent = element.ParentNode is ShadowRoot shadow ? shadow.Host : element.ParentNode as Element;
            if (parent is null) return "ltr";
            element = parent;
        }
    }

    private static string? State(Element element, DomReadWork work)
    {
        if (element.NamespaceUri != Namespaces.Html) return null;
        var dir = work.Attribute(element, "dir");
        if (work.EqualAsciiIgnoreCase(dir, "ltr")) return "ltr";
        if (work.EqualAsciiIgnoreCase(dir, "rtl")) return "rtl";
        if (work.EqualAsciiIgnoreCase(dir, "auto")) return "auto";
        return null;
    }

    private static string? Auto(Element element, DomReadWork work)
    {
        if (IsAutoDirectionalityControl(element, work))
        {
            var value = element.LocalName == "input"
                ? element.GetHtmlState()!.InputValue!.GetValue(work.Token)
                : element.GetHtmlState()!.TextArea!.GetValue(work.Token);
            return value.Length == 0 ? null : FirstStrong(value, work) ?? "ltr";
        }
        if (element is { NamespaceUri: Namespaces.Html, LocalName: "slot" } && work.Root(element) is ShadowRoot)
        {
            var assigned = SlotAssignment.AssignedNodes(element, false, _ => work.Check(), work.Token);
            foreach (var child in assigned)
            {
                work.Step();
                var direction = child is Text text ? FirstStrong(text.Data, work)
                    : child is Element candidate ? ContainedText(candidate, true, work) : null;
                if (direction is not null) return direction;
            }
            if (assigned.Count != 0) return null;
        }
        return ContainedText(element, false, work);
    }

    private static bool Excluded(Element element, DomReadWork work)
        => State(element, work) is not null
            || element is { NamespaceUri: Namespaces.Html, LocalName: "bdi" or "script" or "style" or "textarea" };

    private static string? ContainedText(Element root, bool excludeRoot, DomReadWork work)
    {
        if (excludeRoot && Excluded(root, work)) return null;
        var node = root.FirstChild;
        while (node is not null)
        {
            work.Step();
            var excluded = node is Element candidate && Excluded(candidate, work);
            if (!excluded)
            {
                if (node is Element { NamespaceUri: Namespaces.Html, LocalName: "slot" }
                    && work.Root(node) is ShadowRoot shadow) return Of(shadow.Host, work);
                var text = node switch { Text value => value.Data, CDataSection value => value.Data, _ => null };
                if (text is not null && FirstStrong(text, work) is { } direction) return direction;
                if (node.FirstChild is { } child) { node = child; continue; }
            }
            while (node.NextSibling is null)
            {
                work.Step();
                node = node.ParentNode;
                if (node is null || ReferenceEquals(node, root)) return null;
            }
            node = node.NextSibling;
        }
        return null;
    }

    private static string? FirstStrong(string value, DomReadWork work)
    {
        for (var i = 0; i < value.Length; i++)
        {
            work.Step();
            // DOMString retains lone surrogates. Classify their code points instead of replacing them
            // with U+FFFD (whose bidi class differs), while decoding valid surrogate pairs normally.
            var codePoint = (int) value[i];
            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                codePoint = char.ConvertToUtf32(value[i], value[i + 1]);
                i++;
            }
            switch (HtmlBidiData.ClassOf(codePoint))
            {
                case 1: return "ltr";
                case 2: return "rtl";
            }
        }
        return null;
    }
}
