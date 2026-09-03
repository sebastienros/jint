using System.Globalization;
using System.Net.Sockets;
using Jint.Browser.Runtime;
using Jint.DevTools;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Page;
using Jint.DevTools.Session;
using ProtocolPage = Jint.DevTools.Protocol.Page;

namespace Jint.Browser.DevTools;

/// <summary>
/// The <c>Page</c> domain: the frame a client navigates, the lifecycle it waits on, and the document it ends
/// up with.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every command here runs on the page loop.</b> The target's mailbox is the engine's, and the engine is
/// the page's, so a command reaches this class on the one thread allowed to touch the document — which is
/// what lets <c>getFrameTree</c> and <c>getLayoutMetrics</c> read the DOM without posting anywhere. A command
/// that waits — <c>navigate</c> — waits by <c>await</c>ing rather than blocking, because the loop it is on is
/// what has to run the commit it is waiting for.
/// </para>
/// <para>
/// <b>This browser renders nothing</b>, so <c>captureScreenshot</c> and <c>printToPDF</c> are refused with a
/// sentence that says so and names what to ask for instead. That is Lightpanda's answer and it is the honest
/// one: a blank image would be worse than an error.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Page/"/>.
/// </para>
/// </remarks>
internal sealed partial class PageDomain : PageDomainBase, IDetachableDomain
{
    private readonly PageTarget _target;

    private bool _lifecycleEvents;

    internal PageDomain(PageTarget target)
    {
        _target = target;
    }

    private Page Page => _target.Page;

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> EnableAsync(EnableRequest parameters, CommandContext context)
    {
        await MarkEnabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    void IDetachableDomain.Detach() => _target.RemoveDomain(this);

    /// <summary>
    /// Navigates, and answers at the <b>commit</b>: the response is in and the document is the page's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Chrome answers there too, which is why every client that wants the load waits for a lifecycle event
    /// afterwards rather than for this reply. Waiting here for the parse would make <c>Page.navigate</c> and
    /// <c>Page.lifecycleEvent</c> say the same thing and leave a client with no way to ask for the earlier
    /// one.
    /// </para>
    /// <para>
    /// <b>A status is not a failure.</b> A <c>404</c> navigates and its body is the document, so it answers a
    /// <c>frameId</c> and a <c>loaderId</c> like any other. <c>errorText</c> is for a navigation that produced
    /// nothing at all, and it carries a <c>net::ERR_*</c> string because that is what every client parses.
    /// </para>
    /// </remarks>
    protected override async ValueTask<NavigateResponse> NavigateAsync(NavigateRequest parameters, CommandContext context)
    {
        RequireMainFrame(parameters.FrameId);

        try
        {
            await Page.NavigateAsync(
                parameters.Url,
                new NavigationOptions { WaitUntil = WaitUntilState.Commit, Referrer = parameters.Referrer }).ConfigureAwait(false);
        }
        catch (NavigationFailedException failure)
        {
            return new NavigateResponse
            {
                FrameId = _target.FrameId,
                LoaderId = Page.PendingLoaderId,
                ErrorText = NetworkError(failure),
            };
        }
        catch (OperationCanceledException)
        {
            return new NavigateResponse
            {
                FrameId = _target.FrameId,
                LoaderId = Page.PendingLoaderId,
                ErrorText = "net::ERR_ABORTED",
            };
        }

        return new NavigateResponse { FrameId = _target.FrameId, LoaderId = Page.LoaderId };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <c>ignoreCache</c> and <c>scriptToEvaluateOnLoad</c> are accepted and ignored: there is no HTTP cache
    /// to bypass, and the second is the deprecated half of
    /// <c>addScriptToEvaluateOnNewDocument</c>, which is the one every recorded client sends.
    /// </remarks>
    protected override async ValueTask<EmptyResult> ReloadAsync(ReloadRequest parameters, CommandContext context)
    {
        var url = Page.Url;

        try
        {
            await Page.NavigateAsync(url, new NavigationOptions { WaitUntil = WaitUntilState.Commit }).ConfigureAwait(false);
        }
        catch (NavigationFailedException)
        {
            // A reload that produced nothing leaves the page where it was, and the command has no shape for
            // saying so: Chrome answers a reload with nothing either way.
        }
        catch (OperationCanceledException)
        {
        }

        return EmptyResult.Instance;
    }

    /// <summary>
    /// Answers the page's one frame, which is its main frame.
    /// </summary>
    /// <remarks>
    /// <b>No child frames, and that is the runtime's shape rather than an omission here.</b> An
    /// <c>&lt;iframe&gt;</c> is parsed and is in the page's own frame tree, but it loads nothing and runs no
    /// script, so a client told about it would be told about a frame it could never evaluate in.
    /// <c>childFrames</c> is therefore absent rather than empty-but-present.
    /// </remarks>
    protected override ValueTask<GetFrameTreeResponse> GetFrameTreeAsync(EmptyParameters parameters, CommandContext context)
    {
        return new ValueTask<GetFrameTreeResponse>(new GetFrameTreeResponse
        {
            FrameTree = new FrameTree { Frame = Frame(Page.Url, Page.LoaderId) },
        });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Every entry's identifier is its index, and <c>currentIndex</c> is where the page is. Chrome's
    /// identifiers are opaque and clients only ever hand one straight back to
    /// <c>navigateToHistoryEntry</c>, so an index is as good an identifier as any and is one the page's own
    /// history already keeps.
    /// </remarks>
    protected override ValueTask<GetNavigationHistoryResponse> GetNavigationHistoryAsync(EmptyParameters parameters, CommandContext context)
    {
        var history = Page.History;
        var entries = new List<NavigationEntry>();

        for (var i = 0; ; i++)
        {
            if (history.At(i) is not { } entry)
            {
                break;
            }

            entries.Add(new NavigationEntry
            {
                Id = i,
                Url = entry.Url,
                UserTypedURL = entry.Url,
                Title = i == history.Index ? Title() : "",
                TransitionType = TransitionTypeValues.Link,
            });
        }

        return new ValueTask<GetNavigationHistoryResponse>(new GetNavigationHistoryResponse
        {
            CurrentIndex = Math.Max(0, history.Index),
            Entries = [.. entries],
        });
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> NavigateToHistoryEntryAsync(NavigateToHistoryEntryRequest parameters, CommandContext context)
    {
        var history = Page.History;
        if (history.At(parameters.EntryId) is null)
        {
            // Chrome's own wording for an entry identifier that names nothing.
            Throw.ServerError("Invalid history entry id");
        }

        // Queued, never inline: a traversal is asynchronous in HTML's own model, and a same-document one runs
        // as a job on this very loop.
        Page.RequestTraversal(parameters.EntryId - history.Index);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> SetLifecycleEventsEnabledAsync(SetLifecycleEventsEnabledRequest parameters, CommandContext context)
    {
        _lifecycleEvents = parameters.Enabled;
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <c>worldName</c> is accepted and ignored, and <c>runImmediately</c> with it: a world here is an alias
    /// for the document's own realm, so a script asked for in one is a script in the page.
    /// </remarks>
    protected override ValueTask<AddScriptToEvaluateOnNewDocumentResponse> AddScriptToEvaluateOnNewDocumentAsync(
        AddScriptToEvaluateOnNewDocumentRequest parameters,
        CommandContext context)
    {
        var identifier = _target.NewDocumentScripts.Add(parameters.Source);
        return new ValueTask<AddScriptToEvaluateOnNewDocumentResponse>(new AddScriptToEvaluateOnNewDocumentResponse { Identifier = identifier });
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> RemoveScriptToEvaluateOnNewDocumentAsync(
        RemoveScriptToEvaluateOnNewDocumentRequest parameters,
        CommandContext context)
    {
        // A script nobody knows about is not an error: a client removing what it already removed is tidying.
        _target.NewDocumentScripts.Remove(parameters.Identifier);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A world is a second name for the document's own realm — the divergence
    /// <c>Jint.DevTools/AGENTS.md</c> states — so this mints a context identifier and announces it, and a
    /// script a client evaluates against it runs in the page.
    /// </remarks>
    protected override ValueTask<CreateIsolatedWorldResponse> CreateIsolatedWorldAsync(CreateIsolatedWorldRequest parameters, CommandContext context)
    {
        RequireMainFrame(parameters.FrameId);

        var world = _target.CreateWorldContext(parameters.WorldName);
        return new ValueTask<CreateIsolatedWorldResponse>(new CreateIsolatedWorldResponse { ExecutionContextId = world.Id });
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> SetDocumentContentAsync(SetDocumentContentRequest parameters, CommandContext context)
    {
        RequireMainFrame(parameters.FrameId);

        await Page.SetContentAsync(parameters.Html, Page.Url).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// It sets the standing decision rather than answering a dialog that is waiting; see
    /// <see cref="PageTarget.Dialog"/> for why a dialog here cannot wait.
    /// </remarks>
    protected override ValueTask<EmptyResult> HandleJavaScriptDialogAsync(HandleJavaScriptDialogRequest parameters, CommandContext context)
    {
        _target.Dialog = new DialogDecision(parameters.Accept, parameters.PromptText ?? "");
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Answers the viewport, three times over, because nothing is laid out.
    /// </summary>
    /// <remarks>
    /// A browser answers a content size measured from its layout tree. There is no layout here, so the
    /// content size <i>is</i> the viewport: a client asking how tall the document is is told how tall the
    /// window is. A client that screenshots by these numbers gets a refusal from
    /// <c>captureScreenshot</c> anyway, and one that scrolls by them scrolls nothing.
    /// </remarks>
    protected override ValueTask<GetLayoutMetricsResponse> GetLayoutMetricsAsync(EmptyParameters parameters, CommandContext context)
    {
        var viewport = Viewport();
        var layout = new LayoutViewport { PageX = 0, PageY = 0, ClientWidth = viewport.Width, ClientHeight = viewport.Height };
        var visual = new VisualViewport
        {
            OffsetX = 0,
            OffsetY = 0,
            PageX = 0,
            PageY = 0,
            ClientWidth = viewport.Width,
            ClientHeight = viewport.Height,
            Scale = 1,
        };

        var size = new Jint.DevTools.Protocol.DOM.Rect { X = 0, Y = 0, Width = viewport.Width, Height = viewport.Height };

        return new ValueTask<GetLayoutMetricsResponse>(new GetLayoutMetricsResponse
        {
            LayoutViewport = layout,
            VisualViewport = visual,
            ContentSize = size,
            CssLayoutViewport = layout,
            CssVisualViewport = visual,
            CssContentSize = size,
        });
    }

    /// <inheritdoc/>
    protected override ValueTask<CaptureScreenshotResponse> CaptureScreenshotAsync(CaptureScreenshotRequest parameters, CommandContext context)
        => Throw.ServerError<ValueTask<CaptureScreenshotResponse>>(NoPixels("captureScreenshot"));

    /// <inheritdoc/>
    protected override ValueTask<PrintToPDFResponse> PrintToPDFAsync(PrintToPDFRequest parameters, CommandContext context)
        => Throw.ServerError<ValueTask<PrintToPDFResponse>>(NoPixels("printToPDF"));

    /// <summary>Answers success and stops nothing, because there is no window to raise.</summary>
    /// <remarks>
    /// Every client sends it before driving a page and reads a failure as the page being gone.
    /// </remarks>
    protected override ValueTask<EmptyResult> BringToFrontAsync(EmptyParameters parameters, CommandContext context)
        => new(EmptyResult.Instance);

    /// <summary>Answers success and enforces nothing, because no policy is enforced to bypass.</summary>
    /// <remarks>
    /// Content Security Policy is out of v1 — <c>docs/design/headless-browser.md</c> §2 says so — so a client
    /// asking for it to be bypassed is asking for what it already has.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetBypassCSPAsync(SetBypassCSPRequest parameters, CommandContext context)
        => new(EmptyResult.Instance);

    /// <summary>Answers success and renders nothing, because nothing is rendered.</summary>
    /// <remarks>Playwright sends it while connecting, and a refusal fails an ordinary connection.</remarks>
    protected override ValueTask<EmptyResult> SetFontFamiliesAsync(SetFontFamiliesRequest parameters, CommandContext context)
        => new(EmptyResult.Instance);

    /// <summary>Answers success, and no file chooser is ever opened for a client to intercept.</summary>
    /// <remarks>
    /// A click on an <c>&lt;input type=file&gt;</c> reaches <c>Events/BrowserActivationHost</c>, which
    /// <i>records</i> the request rather than opening anything — there is no file chooser here to intercept,
    /// so <c>Page.fileChooserOpened</c> is never emitted and a client waiting for one waits forever whether
    /// this command is answered or refused. It is answered because a client sets the interception up front,
    /// long before it clicks anything, and reads a refusal as a broken page.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetInterceptFileChooserDialogAsync(
        SetInterceptFileChooserDialogRequest parameters,
        CommandContext context)
        => new(EmptyResult.Instance);

    /// <summary>
    /// Answers success and stops nothing, because a navigation here is not interruptible.
    /// </summary>
    /// <remarks>
    /// The fetch runs off the page's loop under the page's own cancellation token, which closing cancels;
    /// there is no per-navigation handle for a client to pull. A client sends this to make a slow page usable
    /// and then goes on driving it, which still works.
    /// </remarks>
    protected override ValueTask<EmptyResult> StopLoadingAsync(EmptyParameters parameters, CommandContext context)
        => new(EmptyResult.Instance);

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> CloseAsync(EmptyParameters parameters, CommandContext context)
    {
        await Page.CloseAsync().ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <summary>The page's main frame, as the protocol describes one.</summary>
    /// <param name="url">Where the frame is.</param>
    /// <param name="loaderId">The document showing in it.</param>
    /// <remarks>
    /// Both are passed rather than read, because <c>frameNavigated</c> describes the document that has just
    /// committed and a read would race whatever the page has moved on to.
    /// </remarks>
    private ProtocolPage.Frame Frame(string url, string loaderId) => new()
    {
        Id = _target.FrameId,
        LoaderId = loaderId,
        Url = url,
        SecurityOrigin = Origin(url),
        DomainAndRegistry = Registry(url),

        // The parse is always HTML: DocumentFetch has already decided what kind of document a response was,
        // and everything else reaches the page as the markup a browser would have wrapped it in.
        MimeType = "text/html",
        SecureContextType = SecureContextTypeValues.InsecureScheme,
        CrossOriginIsolatedContextType = CrossOriginIsolatedContextTypeValues.NotIsolated,
        GatedAPIFeatures = [],
    };

    /// <summary>The document's title, read straight off it because this is the loop thread.</summary>
    private string Title() => PageRuntime.Find(_target.Runtime.Engine)?.Document?.Title ?? "";

    private Viewport Viewport()
        => PageRuntime.Find(_target.Runtime.Engine)?.Viewport ?? _target.Emulation.DefaultViewport;

    /// <summary>Refuses a frame identifier that is not the page's one scripted frame.</summary>
    private void RequireMainFrame(string? frameId)
    {
        if (frameId is not null && frameId.Length != 0 && !string.Equals(frameId, _target.FrameId, StringComparison.Ordinal))
        {
            // Chrome's wording, which clients match on to tell a frame that went away from a wrong call.
            Throw.ServerError("Frame with the given id was not found.");
        }
    }

    /// <summary>The sentence a client is given instead of an image.</summary>
    private static string NoPixels(string command) => string.Create(
        CultureInfo.InvariantCulture,
        $"Page.{command} is not supported: Jint.Browser renders no pixels. Ask the page for its text or its markup instead — Jint.getText, Jint.getMarkdown or Runtime.evaluate of document.documentElement.outerHTML.");

    /// <summary>
    /// The <c>net::ERR_*</c> string a client parses, for a navigation that produced no document.
    /// </summary>
    /// <remarks>
    /// Five of them, and each is a thing a client acts on differently: a name that did not resolve, a
    /// connection that was refused, a URL the host's own filter blocked, a navigation that was abandoned, and
    /// everything else. The failure's own message is not sent — a client parses this field and shows its own
    /// wording — but it is what the page's error list carries.
    /// </remarks>
    private static string NetworkError(NavigationFailedException failure)
    {
        if (failure.Message.Contains("net::ERR_", StringComparison.Ordinal))
        {
            // A client's own refusal — Network.emulateNetworkConditions(offline), setBlockedURLs, a
            // Fetch.failRequest — names its code in the reason it failed the request with, and that code is
            // what the client that chose it expects to read back here.
            return Runtime.PageNetworkRecorder.NetworkError(failure.Message, failure);
        }

        if (failure.Message.Contains("URL filter", StringComparison.Ordinal))
        {
            return "net::ERR_BLOCKED_BY_CLIENT";
        }

        if (failure.Message.Contains("timed out", StringComparison.Ordinal))
        {
            return "net::ERR_ABORTED";
        }

        for (var inner = failure.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is SocketException socket)
            {
                return socket.SocketErrorCode switch
                {
                    SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain => "net::ERR_NAME_NOT_RESOLVED",
                    SocketError.ConnectionRefused => "net::ERR_CONNECTION_REFUSED",
                    _ => "net::ERR_FAILED",
                };
            }
        }

        return "net::ERR_FAILED";
    }

    /// <summary>The origin of a URL, or the opaque one every URL that has none reports.</summary>
    private static string Origin(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.IsFile)
        {
            return "://";
        }

        return string.Equals(uri.Scheme, "http", StringComparison.Ordinal) || string.Equals(uri.Scheme, "https", StringComparison.Ordinal)
            ? uri.GetLeftPart(UriPartial.Authority)
            : "://";
    }

    /// <summary>
    /// The registrable domain of a URL, which a client uses to group frames of one site.
    /// </summary>
    /// <remarks>
    /// <b>The host, not the registrable domain.</b> Answering the real one means the public-suffix list, and
    /// nothing here ships one; a client that groups by this groups a subdomain apart from its parent, which
    /// is a narrower grouping rather than a wrong one. Named as a divergence rather than left to be found.
    /// </remarks>
    private static string Registry(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && !uri.IsFile ? uri.Host : "";
}
