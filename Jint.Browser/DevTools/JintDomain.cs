using AngleSharp.Dom;
using Jint.Browser.Accessibility;
using Jint.Browser.Extraction;
using Jint.Browser.Runtime;
using Jint.DevTools.Domains;
using Jint.DevTools.Session;
using JintProtocol = Jint.DevTools.Protocol.Jint;

namespace Jint.Browser.DevTools;

/// <summary>
/// The <c>Jint</c> domain: what this browser can answer that a rendering one is asked for a picture of.
/// </summary>
/// <remarks>
/// <para>
/// <b>A custom domain, described in this repository's own file.</b>
/// <c>tools/devtools-protocol/jint_protocol.json</c> sits beside the two vendored Chrome ones, in the same
/// format, and the generator reads all three — so a <c>Jint</c> command has data transfer objects, a
/// dispatch base and a manifest entry exactly as a Chrome one does. Lightpanda adds its <c>LP</c> domain the
/// same way, and it is the shape a client library already knows how to send: an unrecognised domain is just
/// a method a raw <c>send</c> reaches.
/// </para>
/// <para>
/// <b>Why it exists.</b> <c>Page.captureScreenshot</c> and <c>Page.printToPDF</c> are refused because this
/// browser renders no pixels, and the honest answer to "show me the page" without pixels is the page: as
/// text, as CommonMark, and as its accessibility tree. All three are computed from the DOM by
/// <c>Extraction/</c> and <c>Accessibility/</c> — no layout, no rendering, and nothing that runs a line of
/// the page's script.
/// </para>
/// <para>
/// Answered on the page loop, like every command of a page target, so the document is read where it lives.
/// </para>
/// </remarks>
internal sealed class JintDomain : JintDomainBase
{
    private readonly PageTarget _target;

    internal JintDomain(PageTarget target)
    {
        _target = target;
    }

    /// <inheritdoc/>
    protected override ValueTask<JintProtocol.GetMarkdownResponse> GetMarkdownAsync(JintProtocol.GetMarkdownRequest parameters, CommandContext context)
    {
        var options = new MarkdownOptions
        {
            MainContentOnly = parameters.MainContentOnly ?? false,
            IncludeImages = parameters.IncludeImages ?? true,
            MaxLength = Math.Max(0, parameters.MaxLength ?? 0),
            UseComputedStyle = parameters.UseComputedStyle ?? true,
        };

        var markdown = Document() is { } document ? MarkdownExtractor.ToMarkdown(document, options) : "";

        return new ValueTask<JintProtocol.GetMarkdownResponse>(new JintProtocol.GetMarkdownResponse
        {
            Markdown = markdown,

            // The marker the extractor appends is what says a result was cut, so a client can tell a short
            // page from a truncated one without counting characters against a limit it may not have set.
            Truncated = markdown.EndsWith(MarkdownExtractor.TruncationMarker, StringComparison.Ordinal),
        });
    }

    /// <inheritdoc/>
    protected override ValueTask<JintProtocol.GetTextResponse> GetTextAsync(JintProtocol.GetTextRequest parameters, CommandContext context)
    {
        var text = Document() is { } document
            ? TextExtractor.InnerText(document, parameters.UseComputedStyle ?? true)
            : "";

        return new ValueTask<JintProtocol.GetTextResponse>(new JintProtocol.GetTextResponse { Text = text });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The three modes are <c>AccessibilityOptions</c>' three presets and they are not interchangeable:
    /// <c>default</c> is the pruned tree, <c>snapshot</c> adds the text between its nodes — which is what
    /// makes a rendering say anything at all — and <c>full</c> keeps every node including the ignored ones.
    /// </remarks>
    protected override ValueTask<JintProtocol.GetAccessibilitySnapshotResponse> GetAccessibilitySnapshotAsync(
        JintProtocol.GetAccessibilitySnapshotRequest parameters,
        CommandContext context)
    {
        var options = Preset(parameters.Mode);
        var snapshot = Document() is { } document
            ? AccessibilitySnapshot.Render(AccessibilityTree.Build(document, options))
            : "";

        return new ValueTask<JintProtocol.GetAccessibilitySnapshotResponse>(
            new JintProtocol.GetAccessibilitySnapshotResponse { Snapshot = snapshot });
    }

    /// <summary>Which preset a client's mode names, refusing one that names none.</summary>
    private static AccessibilityOptions Preset(string? mode) => mode switch
    {
        null or "" or JintProtocol.GetAccessibilitySnapshotRequestModeValues.Snapshot => AccessibilityOptions.Snapshot,
        JintProtocol.GetAccessibilitySnapshotRequestModeValues.Default => AccessibilityOptions.Default,
        JintProtocol.GetAccessibilitySnapshotRequestModeValues.Full => AccessibilityOptions.Full,
        _ => Jint.DevTools.Throw.InvalidParams<AccessibilityOptions>("Invalid parameters", "mode: expected default, snapshot or full"),
    };

    /// <summary>The document showing, read straight off the runtime because this is the loop thread.</summary>
    private IDocument? Document() => PageRuntime.Find(_target.Runtime.Engine)?.Document;
}
