using AngleSharp.Dom;
using Jint.Browser.Accessibility;

namespace Jint.Browser.Extraction;

/// <summary>
/// The three representations a browser that renders nothing answers "show me the page" with.
/// </summary>
/// <remarks>
/// <para>
/// <c>Page.MarkdownAsync</c>, <c>Page.TextAsync</c> and <c>Page.AccessibilitySnapshotAsync</c> are this
/// class, and so is the <c>jint-browser fetch --dump</c> command behind them. It exists so that the two
/// knobs every caller wants — the document's main content, and a ceiling on the answer — are applied the
/// same way to all three rather than by whichever consumer remembered to.
/// </para>
/// <para>
/// Like the rest of <c>Extraction/</c> and <c>Accessibility/</c> this is pure C# over
/// <see cref="IDocument"/>: it touches no engine, runs no script, and is therefore safe to call from
/// anywhere the document itself is safe to read — which, for a page, means the page's own loop.
/// </para>
/// </remarks>
internal static class PageContent
{
    /// <summary>Renders <paramref name="document"/> as CommonMark.</summary>
    internal static string Markdown(IDocument document, bool mainContentOnly, int maxLength)
        => MarkdownExtractor.ToMarkdown(document, new MarkdownOptions
        {
            MainContentOnly = mainContentOnly,
            MaxLength = maxLength,
        });

    /// <summary>Renders <paramref name="document"/> as its rendered text.</summary>
    internal static string Text(IDocument document, bool mainContentOnly, int maxLength)
    {
        var root = mainContentOnly ? MarkdownExtractor.MainContentOf(document) : null;
        var text = root is null
            ? TextExtractor.InnerText(document)
            : TextExtractor.InnerText(root);

        return MarkdownExtractor.Truncate(text, maxLength);
    }

    /// <summary>Renders <paramref name="document"/>'s accessibility tree as an indented snapshot.</summary>
    /// <remarks>
    /// <see cref="AccessibilityOptions.Snapshot"/> is the preset, which is the pruned tree plus the text
    /// between its nodes — the other two presets are a protocol client's business, and a snapshot rendered
    /// from <see cref="AccessibilityOptions.Default"/> would carry no text to read at all.
    /// </remarks>
    internal static string AccessibilitySnapshot(IDocument document, bool mainContentOnly, int maxLength)
    {
        var root = mainContentOnly ? MarkdownExtractor.MainContentOf(document) : null;
        var tree = root is null
            ? AccessibilityTree.Build(document, AccessibilityOptions.Snapshot)
            : AccessibilityTree.Build(root, AccessibilityOptions.Snapshot);

        var snapshot = tree is null ? "" : Accessibility.AccessibilitySnapshot.Render(tree);
        return MarkdownExtractor.Truncate(snapshot, maxLength);
    }
}
