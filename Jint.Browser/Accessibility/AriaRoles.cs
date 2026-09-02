using System.Collections.Frozen;

namespace Jint.Browser.Accessibility;

/// <summary>
/// The WAI-ARIA role vocabulary: which role names exist, and which of them take a name from their content.
/// </summary>
/// <remarks>
/// From <see href="https://w3c.github.io/aria/">WAI-ARIA 1.2</see>'s "Definition of Roles", plus the 1.3
/// additions HTML-AAM's element mapping table already names (<c>comment</c>, <c>mark</c>, <c>suggestion</c>,
/// <c>sectionheader</c>, <c>sectionfooter</c>). Abstract roles are deliberately absent: an author may not
/// use one, so a <c>role="widget"</c> falls back to the implicit role the same way an unknown name does.
/// </remarks>
internal static class AriaRoles
{
    internal const string Generic = "generic";
    internal const string None = "none";
    internal const string Presentation = "presentation";
    internal const string StaticText = "StaticText";
    internal const string RootWebArea = "RootWebArea";

    /// <summary>Every non-abstract role an author may write in a <c>role</c> attribute.</summary>
    internal static FrozenSet<string> All { get; } = new[]
    {
        "alert", "alertdialog", "application", "article", "banner", "blockquote", "button", "caption", "cell",
        "checkbox", "code", "columnheader", "combobox", "comment", "complementary", "contentinfo", "definition",
        "deletion", "dialog", "directory", "document", "emphasis", "feed", "figure", "form", "generic", "grid",
        "gridcell", "group", "heading", "image", "img", "insertion", "link", "list", "listbox", "listitem", "log",
        "main", "mark", "marquee", "math", "menu", "menubar", "menuitem", "menuitemcheckbox", "menuitemradio",
        "meter", "navigation", "none", "note", "option", "paragraph", "presentation", "progressbar", "radio",
        "radiogroup", "region", "row", "rowgroup", "rowheader", "scrollbar", "search", "searchbox",
        "sectionfooter", "sectionheader", "separator", "slider", "spinbutton", "status", "strong", "subscript",
        "suggestion", "superscript", "switch", "tab", "table", "tablist", "tabpanel", "term", "textbox", "time",
        "timer", "toolbar", "tooltip", "tree", "treegrid", "treeitem",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The roles whose "Name From" is "author and contents", so that accname step 2F may walk their subtree.
    /// </summary>
    internal static FrozenSet<string> NameFromContent { get; } = new[]
    {
        "button", "cell", "checkbox", "columnheader", "comment", "gridcell", "heading", "link", "menuitem",
        "menuitemcheckbox", "menuitemradio", "option", "radio", "row", "rowheader", "switch", "tab", "tooltip",
        "treeitem",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The roles accname step 2C treats as an embedded control, whose value replaces its content when it is
    /// reached through a name computation that started somewhere else.
    /// </summary>
    internal static FrozenSet<string> EmbeddedControl { get; } = new[]
    {
        "textbox", "searchbox", "combobox", "listbox", "slider", "spinbutton", "progressbar", "meter",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The roles that stop accname's "name from content" descent, because a control inside a label names
    /// itself rather than the label.
    /// </summary>
    internal static FrozenSet<string> Interactive { get; } = new[]
    {
        "button", "checkbox", "combobox", "link", "listbox", "menuitem", "menuitemcheckbox", "menuitemradio",
        "option", "radio", "searchbox", "slider", "spinbutton", "switch", "tab", "textbox", "treeitem",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Whether the role hides a node from an assistive technology while keeping its children.</summary>
    internal static bool IsPresentational(string role) =>
        string.Equals(role, None, StringComparison.Ordinal) || string.Equals(role, Presentation, StringComparison.Ordinal);

    /// <summary>Whether the role carries nothing on its own, so the node may be replaced by its children.</summary>
    internal static bool IsPrunable(string role) =>
        string.Equals(role, Generic, StringComparison.Ordinal) || IsPresentational(role);

    /// <summary>
    /// Reads the first token of a <c>role</c> attribute that names a real role, or <see langword="null"/>
    /// when the attribute is absent, empty or names nothing.
    /// </summary>
    /// <remarks>
    /// ARIA's <c>role</c> is a token list whose first valid token wins, which is what makes
    /// <c>role="doc-chapter region"</c> a region rather than nothing.
    /// </remarks>
    internal static string? Explicit(string? attribute)
    {
        if (string.IsNullOrWhiteSpace(attribute))
        {
            return null;
        }

        foreach (var token in attribute.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries))
        {
            var lowered = token.ToLowerInvariant();
            if (All.Contains(lowered))
            {
                return lowered;
            }
        }

        return null;
    }
}
