using Jint.DevTools;

namespace Jint.Browser.DevTools;

/// <summary>
/// The handle a client reaches a page through: Chrome's <c>tab</c> target, over the page's own engine.
/// </summary>
/// <remarks>
/// <para>
/// <b>It exists because Puppeteer requires it.</b> Modern Chrome puts a tab target between the browser and
/// each page, and Puppeteer is written against that shape: its browser-level
/// <c>Target.setAutoAttach</c> filter is <c>[{ type: "page", exclude: true }, {}]</c> — everything <i>but</i>
/// a page — and it reaches a page by sending <c>setAutoAttach</c> again on the tab's own session. A server
/// that published pages and no tabs is one Puppeteer connects to, discovers a page on, and then waits for a
/// session that never arrives; that is what this closes, and it was found by driving the client rather than
/// by reading the protocol.
/// </para>
/// <para>
/// <b>A tab has no engine of its own and does not pretend to.</b> It answers about the page's — its
/// <see cref="Runtime"/> <i>is</i> the page's, so <c>Runtime.runIfWaitingForDebugger</c> on the tab session,
/// which Puppeteer sends, releases the page. Everything a client actually drives it sends on the page's own
/// session instead, which is what the tab handed it.
/// </para>
/// </remarks>
internal sealed class TabTarget : DevToolsTarget
{
    private readonly PageTarget _page;

    internal TabTarget(PageTarget page)
        : base(
            type: "tab",
            title: page.Title,
            url: page.Url,
            browserContextId: page.BrowserContextId,
            openerId: null,
            describer: null,
            waitForDebuggerOnStart: false)
    {
        _page = page;
    }

    /// <summary>The page this tab holds, which is the target a client ends up driving.</summary>
    internal PageTarget Page => _page;

    /// <inheritdoc/>
    internal override TargetRuntime Runtime => _page.Runtime;

    /// <inheritdoc/>
    internal override IReadOnlyList<DevToolsTarget> Children => [_page];

    /// <inheritdoc/>
    internal override (int Width, int Height) WindowSize => _page.WindowSize;

    /// <inheritdoc/>
    internal override ValueTask CloseAsync() => _page.CloseAsync();

    /// <summary>Keeps what a client is told about the tab in step with the page inside it.</summary>
    internal void Follow() => UpdateInfo(_page.Title, _page.Url);
}
