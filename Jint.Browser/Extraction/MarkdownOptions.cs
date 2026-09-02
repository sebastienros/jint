using Jint.Browser.Accessibility;

namespace Jint.Browser.Extraction;

/// <summary>
/// What <see cref="MarkdownExtractor"/> keeps, and how much of it.
/// </summary>
internal sealed record MarkdownOptions
{
    /// <summary>The whole document, images included, unbounded.</summary>
    internal static MarkdownOptions Default { get; } = new();

    /// <summary>Whether images become <c>![alt](src)</c> rather than their alternative text alone.</summary>
    /// <remarks>Defaults to <see langword="true"/>.</remarks>
    internal bool IncludeImages { get; init; } = true;

    /// <summary>The greatest number of characters to return, or zero for no limit.</summary>
    /// <remarks>
    /// A truncated result ends at the last white space before the limit and carries
    /// <see cref="MarkdownExtractor.TruncationMarker"/>, so a reader can tell a short page from a cut one.
    /// </remarks>
    internal int MaxLength { get; init; }

    /// <summary>
    /// Whether to render only the document's main content — the first <c>&lt;main&gt;</c>,
    /// <c>[role=main]</c> or <c>&lt;article&gt;</c> — when the document has one.
    /// </summary>
    internal bool MainContentOnly { get; init; }

    /// <summary>Whether the CSS cascade is consulted for <c>display</c> and <c>visibility</c>.</summary>
    /// <remarks>Defaults to <see langword="true"/>; see <see cref="AccessibilityOptions.UseComputedStyle"/>.</remarks>
    internal bool UseComputedStyle { get; init; } = true;
}
