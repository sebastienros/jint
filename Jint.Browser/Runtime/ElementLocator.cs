using System.Globalization;
using AngleSharp.Dom;
using Jint.Browser.Accessibility;

namespace Jint.Browser.Runtime;

/// <summary>
/// What the <c>target</c> of a click, a fill or a key press names: a CSS selector, or a reference an
/// accessibility snapshot printed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two spellings because an agent cannot write the first one.</b> Everything a caller in code has is a
/// selector, and it is the right thing for it to have. Everything a caller reading a snapshot has is a role
/// and a name — <c>- button "Save" [ref=42]</c> — and a CSS selector for that is something it would have to
/// invent. So a target beginning <c>ref=</c> is the identifier the snapshot printed, resolved back through
/// the accessibility tree's own table, and anything else is a selector handed to AngleSharp.
/// </para>
/// <para>
/// <b>A reference belongs to one document.</b> The identifiers are the document's, so a navigation ends
/// every reference a snapshot of the document before it printed. Resolving one afterwards answers nothing
/// rather than the wrong element, because the table is keyed on the document that is gone.
/// </para>
/// </remarks>
internal static class ElementLocator
{
    /// <summary>What a target starts with to be a snapshot reference rather than a selector.</summary>
    internal const string ReferencePrefix = "ref=";

    /// <summary>The first element <paramref name="target"/> names, or <see langword="null"/>.</summary>
    /// <remarks>
    /// A selector AngleSharp cannot parse answers <see langword="null"/> rather than throwing: a caller
    /// asking about an element that is not there and a caller asking wrongly both want "no", and a selector
    /// arriving from a protocol client or an agent is input rather than code.
    /// </remarks>
    internal static IElement? Find(IDocument? document, string target)
    {
        if (document is null || string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        if (target.StartsWith(ReferencePrefix, StringComparison.Ordinal))
        {
            var text = target[ReferencePrefix.Length..].Trim();
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                ? AccessibilityTree.ElementFor(document, id)
                : null;
        }

        try
        {
            return document.QuerySelector(target);
        }
        catch (DomException)
        {
            return null;
        }
    }
}
