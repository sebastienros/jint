using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Dom;

/// <summary>https://html.spec.whatwg.org/multipage/dom.html#the-directionality</summary>
internal static class HtmlDirectionality
{
    internal static bool IsAutoDirectionalityControl(IElement element) => element is IHtmlTextAreaElement
        || element is IHtmlInputElement input && input.Type is
            "hidden" or "text" or "search" or "tel" or "url" or "email" or "password" or "submit" or "reset" or "button";

    internal static string Of(IElement element)
    {
        while (true)
        {
            var state = State(element);
            if (state is "ltr" or "rtl")
            {
                return state;
            }
            if (state == "auto" || element is IHtmlElement { LocalName: "bdi" })
            {
                return Auto(element) ?? "ltr";
            }
            if (element is IHtmlInputElement { Type: "tel" })
            {
                return "ltr";
            }
            var parent = element.Parent is IShadowRoot shadow ? shadow.Host : element.ParentElement;
            if (parent is null)
            {
                return "ltr";
            }
            element = parent;
        }
    }

    private static string? State(IElement element)
    {
        if (element is not IHtmlElement)
        {
            return null;
        }
        var dir = element.GetAttribute("dir");
        if (Ascii.EqualsIgnoreCase(dir ?? "", "ltr")) return "ltr";
        if (Ascii.EqualsIgnoreCase(dir ?? "", "rtl")) return "rtl";
        if (Ascii.EqualsIgnoreCase(dir ?? "", "auto")) return "auto";
        return null;
    }

    private static string? Auto(IElement element)
    {
        if (IsAutoDirectionalityControl(element))
        {
            var value = element is IHtmlInputElement input ? input.Value : ((IHtmlTextAreaElement) element).Value;
            return string.IsNullOrEmpty(value) ? null : FirstStrong(value) ?? "ltr";
        }
        if (element is IHtmlSlotElement slot && element.GetRoot() is IShadowRoot)
        {
            var any = false;
            foreach (var child in slot.GetDistributedNodes())
            {
                any = true;
                var direction = child is IText text ? FirstStrong(text.Data)
                    : child is IElement assigned ? ContainedText(assigned, excludeRoot: true) : null;
                if (direction is not null) return direction;
            }
            if (any) return null;
        }
        return ContainedText(element, excludeRoot: false);
    }

    private static bool Excluded(IElement element) => State(element) is not null
        || element is IHtmlElement { LocalName: "bdi" or "script" or "style" or "textarea" };

    private static string? ContainedText(IElement root, bool excludeRoot)
    {
        if (excludeRoot && Excluded(root)) return null;

        // Walk without recursion or a materialized descendant list, pruning directionally isolated trees.
        var node = root.FirstChild;
        while (node is not null)
        {
            var excluded = node is IElement candidate && Excluded(candidate);
            if (!excluded)
            {
                if (node is IHtmlSlotElement && node.GetRoot() is IShadowRoot shadow)
                {
                    return Of(shadow.Host);
                }
                if (node is IText text && FirstStrong(text.Data) is { } direction)
                {
                    return direction;
                }
                if (node.FirstChild is { } child)
                {
                    node = child;
                    continue;
                }
            }
            while (node.NextSibling is null)
            {
                node = node.Parent;
                if (node is null || ReferenceEquals(node, root)) return null;
            }
            node = node.NextSibling;
        }
        return null;
    }

    private static string? FirstStrong(string value)
    {
        foreach (var rune in value.EnumerateRunes())
        {
            switch (HtmlBidiData.ClassOf(rune.Value))
            {
                case 1: return "ltr";
                case 2: return "rtl";
            }
        }
        return null;
    }
}
