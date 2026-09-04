using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Jint.Browser.Accessibility;

/// <summary>
/// HTML-AAM's element-to-role mapping: what role an element has when its <c>role</c> attribute does not say.
/// </summary>
/// <remarks>
/// From <see href="https://w3c.github.io/html-aam/#html-element-role-mappings">HTML-AAM</see>'s "HTML element
/// role mappings" table. One simplification is deliberate and applies to every row: where the table names a
/// computed role that is not a WAI-ARIA role — <c>html-abbr</c>, <c>html-audio</c>, <c>keyboard</c>,
/// <c>variable</c> and their kind — this maps the element to <c>generic</c> instead, which is what a consumer
/// of this tree can act on and what the pruning rules already understand.
/// </remarks>
internal static class ImplicitRole
{
    /// <summary>The tags that produce no accessibility node at all, children included.</summary>
    /// <remarks>
    /// These are not "ignored" nodes with a reason — an assistive technology never sees a
    /// <c>&lt;script&gt;</c>'s text, and neither should a snapshot. <c>&lt;template&gt;</c> is here for a
    /// second reason: its contents live in a separate document fragment, so a child walk never reaches them.
    /// </remarks>
    internal static bool IsMetadataContent(IElement element) => element.LocalName switch
    {
        "head" or "meta" or "link" or "style" or "script" or "template" or "title" or "base" or "noscript"
            or "param" or "source" or "track" or "col" or "colgroup" or "slot" => true,
        _ => false,
    };

    /// <summary>
    /// Returns the element's implicit role, or <see langword="null"/> when HTML-AAM maps it to no role at all.
    /// </summary>
    internal static string? For(IElement element)
    {
        if (!string.Equals(element.NamespaceUri, NamespaceNames.HtmlUri, StringComparison.Ordinal))
        {
            return element.LocalName switch
            {
                "svg" => "graphics-document",
                "math" => "math",
                _ => AriaRoles.Generic,
            };
        }

        return element.LocalName switch
        {
            "a" or "area" => element.HasAttribute("href") ? "link" : AriaRoles.Generic,
            "article" => "article",
            "aside" => Complementary(element),
            "blockquote" => "blockquote",
            "button" => "button",
            "caption" or "figcaption" => "caption",
            "code" => "code",
            "datalist" => "listbox",
            "dd" => "definition",
            "del" => "deletion",
            "details" => "group",
            "dfn" or "dt" => "term",
            "dialog" => "dialog",
            "dl" or "menu" or "ol" or "ul" or "dir" => "list",
            "em" => "emphasis",
            "address" or "fieldset" or "hgroup" or "optgroup" => "group",
            "figure" => "figure",
            "form" => "form",
            "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => "heading",
            "header" => IsScopedToBody(element) ? "banner" : "sectionheader",
            "footer" => IsScopedToBody(element) ? "contentinfo" : "sectionfooter",
            "hr" => "separator",
            "img" => Image(element),
            "input" => Input(element),
            "ins" => "insertion",
            "li" => IsListItem(element) ? "listitem" : AriaRoles.Generic,
            "main" => "main",
            "mark" => "mark",
            "math" => "math",
            "meter" => "meter",
            "nav" => "navigation",
            "option" => "option",
            "output" => "status",
            "p" => "paragraph",
            "progress" => "progressbar",
            "s" => "strikethrough",
            "search" => "search",
            "section" => HasNamingAttribute(element) ? "region" : AriaRoles.Generic,
            "select" => IsListBox(element) ? "listbox" : "combobox",
            "strong" => "strong",
            "sub" => "subscript",
            "summary" => IsDetailsSummary(element) ? "button" : AriaRoles.Generic,
            "sup" => "superscript",
            "svg" => "graphics-document",
            "table" => "table",
            "tbody" or "tfoot" or "thead" => "rowgroup",
            "td" => "cell",
            "textarea" => "textbox",
            "th" => Header(element),
            "time" => "time",
            "tr" => "row",
            "br" or "wbr" => null,
            _ => IsMetadataContent(element) ? null : AriaRoles.Generic,
        };
    }

    /// <summary>Whether the element carries an attribute that would give it an author-supplied name.</summary>
    /// <remarks>
    /// <c>aside</c> and <c>section</c> take their role from whether they are named, and the name computation
    /// takes its starting point from the role. This breaks that circle the way browsers do, by asking whether
    /// a naming attribute is present rather than what it computes to.
    /// <c>aria-labelledby</c> is present in two spellings: as idrefs, and as an <c>ariaLabelledByElements</c>
    /// relationship whose content attribute is the empty string by construction — which is why the second is
    /// asked for separately rather than read off the attribute.
    /// </remarks>
    internal static bool HasNamingAttribute(IElement element) =>
        !string.IsNullOrWhiteSpace(element.GetAttribute("aria-label"))
        || !string.IsNullOrWhiteSpace(element.GetAttribute("aria-labelledby"))
        || Dom.AriaElementReferences.Explicit(element, "aria-labelledby") is { Length: > 0 }
        || !string.IsNullOrWhiteSpace(element.GetAttribute("title"));

    private static string Complementary(IElement element) =>
        IsScopedToBody(element, mainIsRoot: true) || HasNamingAttribute(element) ? "complementary" : AriaRoles.Generic;

    private static string Image(IElement element) =>
        element.HasAttribute("alt") && element.GetAttribute("alt")!.Length == 0 ? AriaRoles.None : "image";

    private static string Header(IElement element)
    {
        var scope = element.GetAttribute("scope");
        if (string.Equals(scope, "row", StringComparison.OrdinalIgnoreCase) || string.Equals(scope, "rowgroup", StringComparison.OrdinalIgnoreCase))
        {
            return "rowheader";
        }

        return "columnheader";
    }

    private static string? Input(IElement element)
    {
        var type = (element as IHtmlInputElement)?.Type ?? element.GetAttribute("type") ?? "text";
        var hasList = element.HasAttribute("list");

        return type.ToLowerInvariant() switch
        {
            "hidden" => null,
            "button" or "image" or "reset" or "submit" => "button",
            "checkbox" => "checkbox",
            "radio" => "radio",
            "number" => "spinbutton",
            "range" => "slider",
            "search" => hasList ? "combobox" : "searchbox",
            "email" or "tel" or "text" or "url" => hasList ? "combobox" : "textbox",
            // color, date, datetime-local, file, month, password, time and week are HTML-AAM's html-input-*
            // computed roles, which are not ARIA roles; see the class remarks.
            "color" or "date" or "datetime-local" or "file" or "month" or "password" or "time" or "week" => AriaRoles.Generic,
            _ => hasList ? "combobox" : "textbox",
        };
    }

    private static bool IsListItem(IElement element) => element.ParentElement?.LocalName switch
    {
        "ul" or "ol" or "menu" or "dir" => true,
        _ => false,
    };

    private static bool IsListBox(IElement element)
    {
        if (element is not IHtmlSelectElement select)
        {
            return element.HasAttribute("multiple");
        }

        // AngleSharp answers 0 for an absent size where HTML's reflected default is 0 too; the rendered
        // default a browser applies is 1, so anything above 1 is what makes a select a list box.
        return select.IsMultiple || select.Size > 1;
    }

    private static bool IsDetailsSummary(IElement element)
    {
        var parent = element.ParentElement;
        if (parent is null || !string.Equals(parent.LocalName, "details", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var child in parent.Children)
        {
            if (string.Equals(child.LocalName, "summary", StringComparison.Ordinal))
            {
                return ReferenceEquals(child, element);
            }
        }

        return false;
    }

    private static bool IsScopedToBody(IElement element, bool mainIsRoot = false)
    {
        for (var ancestor = element.ParentElement; ancestor is not null; ancestor = ancestor.ParentElement)
        {
            switch (ancestor.LocalName)
            {
                case "main" when mainIsRoot:
                case "body":
                    return true;
                case "article":
                case "aside":
                case "main":
                case "nav":
                case "section":
                    return false;
            }

            var explicitRole = AriaRoles.Explicit(ancestor.GetAttribute("role"));
            if (explicitRole is "main" && mainIsRoot)
            {
                return true;
            }

            if (explicitRole is "article" or "complementary" or "main" or "navigation" or "region")
            {
                return false;
            }
        }

        return true;
    }
}
