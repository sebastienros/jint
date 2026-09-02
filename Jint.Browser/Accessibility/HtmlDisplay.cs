using System.Collections.Frozen;
using AngleSharp.Dom;

namespace Jint.Browser.Accessibility;

/// <summary>
/// What HTML's suggested rendering gives an element for <c>display</c> and <c>white-space</c>.
/// </summary>
/// <remarks>
/// <para>
/// This table, and not the cascade, is what decides whether a box is block-level. AngleSharp.Css's default
/// style sheet has no rules for the HTML5 flow and sectioning elements — <c>section</c>, <c>article</c>,
/// <c>nav</c>, <c>aside</c>, <c>header</c>, <c>footer</c>, <c>main</c>, <c>figure</c>, <c>figcaption</c>,
/// <c>details</c>, <c>summary</c>, <c>dialog</c>, <c>hgroup</c> — so asking it would call every one of them
/// inline.
/// </para>
/// <para>
/// The cascade still wins where it says something this table does not: <see cref="Resolve"/> prefers a
/// declared value that differs from the default, which is what makes
/// <c>&lt;span style="display:block"&gt;</c> a block and leaves <c>&lt;section&gt;</c> alone.
/// </para>
/// </remarks>
internal static class HtmlDisplay
{
    private static readonly FrozenDictionary<string, string> s_defaults = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["address"] = "block",
        ["article"] = "block",
        ["aside"] = "block",
        ["blockquote"] = "block",
        ["body"] = "block",
        ["caption"] = "table-caption",
        ["center"] = "block",
        ["col"] = "table-column",
        ["colgroup"] = "table-column-group",
        ["dd"] = "block",
        ["details"] = "block",
        ["dialog"] = "block",
        ["dir"] = "block",
        ["div"] = "block",
        ["dl"] = "block",
        ["dt"] = "block",
        ["fieldset"] = "block",
        ["figcaption"] = "block",
        ["figure"] = "block",
        ["footer"] = "block",
        ["form"] = "block",
        ["h1"] = "block",
        ["h2"] = "block",
        ["h3"] = "block",
        ["h4"] = "block",
        ["h5"] = "block",
        ["h6"] = "block",
        ["header"] = "block",
        ["hgroup"] = "block",
        ["hr"] = "block",
        ["html"] = "block",
        ["legend"] = "block",
        ["li"] = "list-item",
        ["listing"] = "block",
        ["main"] = "block",
        ["menu"] = "block",
        ["nav"] = "block",
        ["ol"] = "block",
        ["optgroup"] = "block",
        ["option"] = "block",
        ["p"] = "block",
        ["plaintext"] = "block",
        ["pre"] = "block",
        ["search"] = "block",
        ["section"] = "block",
        ["summary"] = "block",
        ["table"] = "table",
        ["tbody"] = "table-row-group",
        ["td"] = "table-cell",
        ["tfoot"] = "table-footer-group",
        ["th"] = "table-cell",
        ["thead"] = "table-header-group",
        ["tr"] = "table-row",
        ["ul"] = "block",
        ["xmp"] = "block",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenSet<string> s_preserveWhitespace = new[]
    {
        "pre", "listing", "plaintext", "xmp", "textarea",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Returns HTML's suggested <c>display</c> for the element, or <c>inline</c>.</summary>
    internal static string DefaultFor(IElement element) =>
        s_defaults.TryGetValue(element.LocalName, out var display) ? display : "inline";

    /// <summary>
    /// Returns the element's effective <c>display</c>: the declared value when it differs from HTML's
    /// suggested rendering, and the suggested rendering otherwise.
    /// </summary>
    internal static string Resolve(IElement element, string? declared)
    {
        var fallback = DefaultFor(element);
        if (string.IsNullOrEmpty(declared) || string.Equals(declared, fallback, StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        return declared;
    }

    /// <summary>Whether a box with this <c>display</c> is block-level, so it starts and ends a line.</summary>
    /// <remarks>
    /// The internal table displays are deliberately absent. A <c>table-row-group</c> or <c>table-cell</c> box
    /// is not a block-level box, and <c>innerText</c> separates rows and cells with its own line feeds and
    /// tabs rather than with the required line breaks a block-level box contributes.
    /// </remarks>
    internal static bool IsBlockLevel(string display) => display switch
    {
        "block" or "flow-root" or "list-item" or "table" or "flex" or "grid" => true,
        _ => false,
    };

    /// <summary>Whether a box with this <c>display</c> runs together with the text beside it.</summary>
    /// <remarks>
    /// This is the coarser question the name computation asks. A table cell is not a block-level box, but two
    /// cells still read as two words rather than one, so anything that is not inline-level separates.
    /// </remarks>
    internal static bool IsInlineLevel(string display) => display switch
    {
        "inline" or "inline-block" or "inline-flex" or "inline-grid" or "inline-table" or "contents"
            or "ruby" or "ruby-base" or "ruby-text" => true,
        _ => false,
    };

    /// <summary>Whether the element's content keeps its white space verbatim.</summary>
    /// <remarks>
    /// The element list is HTML's; AngleSharp.Css's default sheet carries <c>pre { white-space: pre }</c> but
    /// not the <c>textarea</c> rule, so asking the cascade alone would collapse a text area's content.
    /// </remarks>
    internal static bool PreservesWhitespace(IElement element, string? declaredWhiteSpace)
    {
        if (!string.IsNullOrEmpty(declaredWhiteSpace))
        {
            if (declaredWhiteSpace.StartsWith("pre", StringComparison.OrdinalIgnoreCase)
                || declaredWhiteSpace.StartsWith("break-spaces", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (declaredWhiteSpace.StartsWith("normal", StringComparison.OrdinalIgnoreCase)
                || declaredWhiteSpace.StartsWith("nowrap", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return s_preserveWhitespace.Contains(element.LocalName);
    }
}
