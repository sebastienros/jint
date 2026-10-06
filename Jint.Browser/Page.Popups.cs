using Jint.Browser.Runtime;

namespace Jint.Browser;

public sealed partial class Page
{
    /// <summary>The page that opened this popup, or null for an independent or disowned page.</summary>
    /// <remarks>Closing the opener does not clear this relationship. Links with noopener have no opener.</remarks>
    public Page? Opener => WindowHandle.Opener;

    /// <summary>Raised when this page opens a popup, after it joins <see cref="BrowserContext.Pages"/>.</summary>
    /// <remarks>
    /// Runs off the page loop, without holding context locks. The popup initially shows about:blank;
    /// its requested navigation may not have committed yet. Also raised for noopener popups.
    /// </remarks>
    public event EventHandler<Page>? Popup;

    internal BrowsingContextHandle WindowHandle { get; }

    internal void AnnouncePopup(Page popup) => Popup?.Invoke(this, popup);

    internal void RecordPopupError(Exception exception)
        => _recorder.Add(PageErrorKind.ReportedError, exception.Message, "Window");

    internal async Task CloseFromScriptAsync(bool defer)
    {
        // A self-close must not cancel the script that called close(), or join its own loop thread.
        if (defer) await _loop.PostAsync(static _ => { }).ConfigureAwait(false);
        await CloseAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/browsing-the-web.html#navigate — a cross-document navigate event
    /// fires only when the source document is same origin with the target's active document.
    /// </summary>
    internal Task RequestCrossPageNavigationAsync(CrossPageNavigation navigation)
        => _loop.PostAsync<Task>(engine => Start(new NavigationRequest(
            navigation.Url,
            NavigationOptions.Default,
            navigation.Replace ? HistoryMode.Replace : HistoryMode.Push,
            TraversalIndex: -1,
            navigation.Body,
            navigation.ContentType,
            Reload: navigation.Body is not null,
            navigation.Referrer,
            NavigationEventDispatched: PageRuntime.Find(engine) is not { } runtime
                || !navigation.Origin.IsSameOrigin(Dom.DomDocumentMetadata.CreatorOrigin(runtime.Dom)),
            InitiatorUrl: navigation.SourceUrl,
            Creator: new DocumentCreationFacts(navigation.Origin, navigation.BaseUrl)),
            navigation.Body is null ? PageNavigationReason.ScriptInitiated : PageNavigationReason.FormSubmissionPost)).Unwrap();
}
