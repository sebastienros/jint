using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Jint.Browser.Accessibility;

/// <summary>
/// The accessible name and description computation of
/// <see href="https://w3c.github.io/accname/">Accessible Name and Description Computation 1.2</see>.
/// </summary>
/// <remarks>
/// Four parts of the algorithm are deliberately simplified, and every one of them needs a layout or a style
/// resolver this package does not have: CSS generated content (<c>::before</c>, <c>::after</c>,
/// <c>::marker</c>) contributes nothing; <c>text-transform</c> is not applied; SVG <c>title</c>/<c>desc</c>
/// children are not read; and the inter-child spacing rule uses HTML's suggested <c>display</c> rather than
/// a used one. Everything else — 2A through 2I, the recursion, the visited guard and the whitespace
/// flattening — is the algorithm as written.
/// </remarks>
internal sealed class AccessibleName
{
    private readonly ElementVisibility _visibility;

    internal AccessibleName(ElementVisibility visibility) => _visibility = visibility;

    /// <summary>Computes the accessible name of <paramref name="element"/>, or the empty string.</summary>
    internal string Compute(IElement element, string role)
    {
        var context = new Context(new HashSet<INode>());
        var name = Flatten(FromElement(element, role, context, referenced: false, descendant: false));

        // Step 2I: a title is the last resort, and only for the element the computation started on.
        if (name.Length == 0)
        {
            name = Flatten(element.GetAttribute("title") ?? string.Empty);
        }

        return name;
    }

    /// <summary>
    /// Computes the accessible description of <paramref name="element"/>, given the name it already has.
    /// </summary>
    /// <remarks>
    /// <c>aria-describedby</c> first, then the fallbacks HTML-AAM names — a <c>title</c> that the name did
    /// not consume, and for a text control a <c>placeholder</c> that the name did not consume.
    /// </remarks>
    internal string ComputeDescription(IElement element, string name)
    {
        var describedBy = References(element, "aria-describedby");
        if (describedBy.Count > 0)
        {
            var context = new Context(new HashSet<INode>());
            var builder = new StringBuilder();
            foreach (var target in describedBy)
            {
                // A referenced node is traversed for its content the same way step 2B's targets are.
                Append(builder, FromNode(target, context, referenced: true, descendant: true));
            }

            var described = Flatten(builder.ToString());
            if (described.Length > 0)
            {
                return described;
            }
        }

        var title = Flatten(element.GetAttribute("title") ?? string.Empty);
        if (title.Length > 0 && !string.Equals(title, name, StringComparison.Ordinal))
        {
            return title;
        }

        var placeholder = Flatten(element.GetAttribute("placeholder") ?? string.Empty);
        if (placeholder.Length > 0 && !string.Equals(placeholder, name, StringComparison.Ordinal))
        {
            return placeholder;
        }

        return string.Empty;
    }

    private string FromNode(INode node, Context context, bool referenced, bool descendant)
    {
        if (node is IText text)
        {
            // Step 2G.
            return text.Data;
        }

        if (node is not IElement element)
        {
            return string.Empty;
        }

        return FromElement(element, ResolveRole(element), context, referenced, descendant);
    }

    private string FromElement(IElement element, string role, Context context, bool referenced, bool descendant)
    {
        if (!context.Visited.Add(element))
        {
            return string.Empty;
        }

        try
        {
            // Step 2A. A hidden node contributes nothing unless it is the direct target of the reference
            // that reached it, which is what makes an off-screen <span id="label" hidden> a legal label.
            if (!referenced && _visibility.ReasonFor(element) != AxIgnoredReason.None)
            {
                return string.Empty;
            }

            // Step 2B.
            if (!context.InLabelledBy)
            {
                var labelledBy = References(element, "aria-labelledby");
                if (labelledBy.Count > 0)
                {
                    var builder = new StringBuilder();
                    var nested = context with { InLabelledBy = true };
                    foreach (var target in labelledBy)
                    {
                        Append(builder, FromNode(target, nested, referenced: true, descendant: true));
                    }

                    var result = Flatten(builder.ToString());
                    if (result.Length > 0)
                    {
                        return result;
                    }
                }
            }

            // Step 2C. Reached from somewhere else, a control contributes its value rather than its label.
            var embedded = descendant && AriaRoles.EmbeddedControl.Contains(role);
            if (embedded)
            {
                var value = ControlValue.For(element, role);
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            // Step 2D.
            var ariaLabel = element.GetAttribute("aria-label");
            if (!string.IsNullOrWhiteSpace(ariaLabel) && !embedded)
            {
                return Flatten(ariaLabel);
            }

            // Step 2E.
            if (!AriaRoles.IsPresentational(role))
            {
                var native = NativeLabel(element, role, context);
                if (native.Length > 0)
                {
                    return native;
                }
            }

            // Step 2F and 2H. Name from content, either because the role allows it or because the walk got
            // here through a reference or a subtree descent.
            if (descendant || AriaRoles.NameFromContent.Contains(role))
            {
                var content = FromContent(element, context);
                if (content.Length > 0)
                {
                    return content;
                }
            }

            // Step 2I, for a node the recursion reached rather than the one it started on.
            if (descendant)
            {
                var title = element.GetAttribute("title");
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return Flatten(title);
                }
            }

            return string.Empty;
        }
        finally
        {
            context.Visited.Remove(element);
        }
    }

    private string FromContent(IElement element, Context context)
    {
        var builder = new StringBuilder();
        foreach (var child in element.ChildNodes)
        {
            if (child is IElement childElement)
            {
                if (ImplicitRole.IsMetadataContent(childElement))
                {
                    continue;
                }

                var childRole = ResolveRole(childElement);
                var contribution = FromElement(childElement, childRole, context, referenced: false, descendant: true);

                // Without a used display there is nothing to measure, so HTML's suggested rendering decides
                // whether two children run together: "<b>a</b><b>b</b>" is "ab", "<p>a</p><p>b</p>" is "a b".
                if (!HtmlDisplay.IsInlineLevel(HtmlDisplay.Resolve(childElement, _visibility.Style(childElement).Display)))
                {
                    Append(builder, contribution);
                }
                else
                {
                    builder.Append(contribution);
                }
            }
            else if (child is IText text)
            {
                builder.Append(text.Data);
            }
        }

        return Flatten(builder.ToString());
    }

    private string NativeLabel(IElement element, string role, Context context)
    {
        switch (element.LocalName)
        {
            case "input":
                return InputLabel(element, role, context);

            case "textarea":
            case "select":
            case "progress":
            case "meter":
            case "output":
                return LabelElements(element, context);

            case "img":
            case "area":
                return Flatten(element.GetAttribute("alt") ?? string.Empty);

            case "fieldset":
                return FromFirstChild(element, "legend", context);

            case "table":
                return FromFirstChild(element, "caption", context);

            case "figure":
                return FromFirstChild(element, "figcaption", context);

            case "optgroup":
                return Flatten(element.GetAttribute("label") ?? string.Empty);

            default:
                return string.Empty;
        }
    }

    private string InputLabel(IElement element, string role, Context context)
    {
        var type = ((element as IHtmlInputElement)?.Type ?? element.GetAttribute("type") ?? "text").ToLowerInvariant();

        switch (type)
        {
            case "button":
                return Flatten(element.GetAttribute("value") ?? string.Empty);

            case "submit":
                {
                    var value = Flatten(element.GetAttribute("value") ?? string.Empty);
                    return value.Length > 0 ? value : "Submit";
                }

            case "reset":
                {
                    var value = Flatten(element.GetAttribute("value") ?? string.Empty);
                    return value.Length > 0 ? value : "Reset";
                }

            case "image":
                {
                    var alt = Flatten(element.GetAttribute("alt") ?? string.Empty);
                    if (alt.Length > 0)
                    {
                        return alt;
                    }

                    var value = Flatten(element.GetAttribute("value") ?? string.Empty);
                    return value.Length > 0 ? value : "Submit Query";
                }

            default:
                {
                    var label = LabelElements(element, context);
                    if (label.Length > 0)
                    {
                        return label;
                    }

                    // HTML-AAM's last native fallback for a text control.
                    return Flatten(element.GetAttribute("placeholder") ?? string.Empty);
                }
        }
    }

    private string LabelElements(IElement element, Context context)
    {
        var builder = new StringBuilder();

        var id = element.Id;
        var document = element.Owner;
        if (!string.IsNullOrEmpty(id) && document is not null)
        {
            foreach (var candidate in document.QuerySelectorAll("label[for]"))
            {
                if (string.Equals(candidate.GetAttribute("for"), id, StringComparison.Ordinal))
                {
                    Append(builder, FromElement(candidate, AriaRoles.Generic, context, referenced: false, descendant: true));
                }
            }
        }

        for (var ancestor = element.ParentElement; ancestor is not null; ancestor = ancestor.ParentElement)
        {
            if (string.Equals(ancestor.LocalName, "label", StringComparison.Ordinal) && !ancestor.HasAttribute("for"))
            {
                Append(builder, FromElement(ancestor, AriaRoles.Generic, context, referenced: false, descendant: true));
                break;
            }
        }

        return Flatten(builder.ToString());
    }

    private string FromFirstChild(IElement element, string localName, Context context)
    {
        foreach (var child in element.Children)
        {
            if (string.Equals(child.LocalName, localName, StringComparison.Ordinal))
            {
                return FromElement(child, AriaRoles.Generic, context, referenced: false, descendant: true);
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// The elements an <c>aria-labelledby</c>/<c>aria-describedby</c> relationship points at: the ones a page
    /// set through the IDL attribute if it did, and the content attribute's idrefs otherwise.
    /// </summary>
    /// <remarks>
    /// <b>The IDL half has to be asked first, and cannot be inferred from the attribute.</b> Setting
    /// <c>el.ariaLabelledByElements</c> writes the <b>empty string</b> to the content attribute and holds the
    /// elements by reference, precisely so that a page may name an element no id could name — so reading the
    /// attribute alone answers "no references" for exactly the case a page went out of its way to express.
    /// <c>Dom/AriaElementReferences</c> is engine-free for this: it takes an <c>IElement</c> and nothing else,
    /// the same standing <c>Dom/Views/CssCascade</c> has, so this file's "neither touches an engine" holds.
    /// The idref path below is untouched, and a relationship written as ids still resolves through it.
    /// </remarks>
    private static List<INode> References(IElement element, string attribute)
    {
        if (Dom.AriaElementReferences.Explicit(element, attribute) is { } associated)
        {
            var explicitly = new List<INode>(associated.Length);
            foreach (var target in associated)
            {
                explicitly.Add(target);
            }

            return explicitly;
        }

        var value = element.GetAttribute(attribute);
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var document = element.Owner;
        if (document is null)
        {
            return [];
        }

        var targets = new List<INode>();
        foreach (var idref in value.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries))
        {
            var target = document.GetElementById(idref);
            if (target is not null)
            {
                targets.Add(target);
            }
        }

        return targets;
    }

    /// <summary>Resolves a role the way the tree builder does, so a name computation agrees with it.</summary>
    internal static string ResolveRole(IElement element) =>
        AriaRoles.Explicit(element.GetAttribute("role")) ?? ImplicitRole.For(element) ?? AriaRoles.Generic;

    private static void Append(StringBuilder builder, string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(text);
    }

    /// <summary>Collapses every run of white space to one space and trims the ends, as accname requires.</summary>
    internal static string Flatten(string text)
    {
        if (text.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private readonly record struct Context(HashSet<INode> Visited)
    {
        internal bool InLabelledBy { get; init; }
    }
}
