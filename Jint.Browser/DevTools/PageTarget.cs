using System.Globalization;
using Jint.Browser.Runtime;
using Jint.DevTools;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Session;
using Jint.Runtime;

namespace Jint.Browser.DevTools;

/// <summary>
/// One page, as a client sees it: a target it lists, attaches to, navigates and evaluates in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The target is the page and the runtime is the document.</b> A page replaces its engine on every
/// navigation, so everything a client keeps addressing — the identifier, the frame it names, the bindings it
/// added, the scripts it asked to be evaluated on every new document, the emulation it set — lives here, and
/// everything that dies with a document lives on the <c>TargetRuntime</c> the base class replaces.
/// </para>
/// <para>
/// <b>Every protocol command touching the engine runs on the page loop</b>, brought there by the target's own
/// mailbox exactly as an engine target's is: <c>engine.Tasks.Post</c> wakes the loop, the loop drains, and
/// what crosses back is a string. Nothing here weakens the thread rule — a <c>JsValue</c> never leaves the
/// loop thread, and an AngleSharp node never leaves it either.
/// </para>
/// <para>
/// <b>It is the page's one observer.</b> Every event a client hears about the page is one
/// <see cref="IPageObserver"/> call turned into a protocol event, fanned out to whichever attachments have
/// the <c>Page</c> domain enabled.
/// </para>
/// </remarks>
internal sealed partial class PageTarget : DevToolsTarget, IPageObserver
{
    private readonly object _domainGate = new();
    private readonly Action<PageTarget>? _closed;

    private PageDomain[] _domains = [];
    private string? _uncommittedUrl;
    private List<PendingNavigation>? _pendingNavigations;
    private TaskCompletionSource? _navigationPublished;

    /// <summary>How many navigation notices the current parse has deferred, read on the loop.</summary>
    internal int PendingNavigationCount => _pendingNavigations?.Count ?? 0;

    /// <summary>Keep an action's reply behind any navigation notices it queued during the parse.</summary>
    internal Task? NavigationPublicationAfter(int previousCount)
        => PendingNavigationCount > previousCount
            ? (_navigationPublished ??= new(TaskCreationOptions.RunContinuationsAsynchronously)).Task
            : null;

    private PageTarget(Page page, string? browserContextId, bool waitForDebuggerOnStart, Action<PageTarget>? closed)
        : base(
            type: "page",
            title: "",
            url: page.Url,
            browserContextId: browserContextId,
            openerId: null,
            describer: DomRemoteObjectDescriber.Instance,
            waitForDebuggerOnStart: waitForDebuggerOnStart)
    {
        Page = page;
        _closed = closed;
    }

    /// <summary>The page this target speaks for.</summary>
    internal Page Page { get; }

    /// <summary>
    /// The identifier of the page's main frame, which is the target's own.
    /// </summary>
    /// <remarks>
    /// Chrome names a page's main frame with a string of its own, and every client treats the two as opaque
    /// and independent. Making them one string is a decision rather than a shortcut: a client that matches
    /// the frame it navigated against the execution context it evaluates in gets the same value from both,
    /// and there is exactly one scripted frame per page here for a second identifier to distinguish.
    /// </remarks>
    internal string FrameId => TargetId;

    /// <summary>What a client set through the <c>Emulation</c> domain.</summary>
    /// <remarks>
    /// It is the <i>page's</i> rather than the target's, because an override outlives the document it was
    /// set on: a client that emulated a time zone before its first navigation expects every document after
    /// it to be in that time zone, and the target's runtime is replaced by each of them.
    /// </remarks>
    internal EmulationState Emulation => Page.Emulation;

    /// <summary>The scripts <c>Page.addScriptToEvaluateOnNewDocument</c> runs before each document's own.</summary>
    internal NewDocumentScripts NewDocumentScripts { get; } = new();

    /// <summary>
    /// The node identifiers the <c>DOM</c> domain addresses this page's document by.
    /// </summary>
    /// <remarks>
    /// On the target rather than on the runtime, because a <c>backendNodeId</c> outlives one document while
    /// a <c>nodeId</c> does not: the tracker holds both and is told to throw the second away when a document
    /// commits. It is shared by every attachment, the way the remote-object table is.
    /// </remarks>
    internal DomNodeTracker Nodes { get; } = new();

    /// <summary>The tab this page hangs off, which is how a client reaches it. Set by the host.</summary>
    /// <remarks>
    /// A tab shows what its page shows, so everything that moves the page's title or location moves the
    /// tab's too — a client that read one and then the other must not be told two different things.
    /// </remarks>
    internal TabTarget? Tab { get; set; }

    /// <inheritdoc/>
    internal override (int Width, int Height) WindowSize => (Emulation.Viewport.Width, Emulation.Viewport.Height);

    /// <summary>Whether the client asked to accept the next dialog, and with what text.</summary>
    /// <remarks>
    /// <b>A dialog here does not block the page</b>, which is the one place this diverges from Chrome and it
    /// comes straight from the thread rule: <c>alert</c> runs on the page loop, inside the script that called
    /// it, and the loop is the thread a client's <c>Page.handleJavaScriptDialog</c> would be answered on. So
    /// the command sets the standing decision and the next dialog reads it, rather than answering a dialog
    /// that is waiting. A client that sends it before the page opens one — which is what a
    /// <c>page.on('dialog')</c> handler installed up front amounts to — gets what it asked for.
    /// </remarks>
    internal DialogDecision Dialog { get; set; } = DialogDecision.Dismiss;

    /// <summary>Registers a page target over <paramref name="page"/> and starts watching it.</summary>
    /// <param name="page">The page.</param>
    /// <param name="browserContextId">Which context it belongs to, or <see langword="null"/> for the default.</param>
    /// <param name="waitForDebuggerOnStart">Whether it runs nothing until a client releases it.</param>
    /// <param name="closed">What to run when the page closes, so the server can stop publishing it.</param>
    /// <remarks>
    /// The engine is adopted and the observer registered <b>inside one loop request</b>, so a navigation
    /// cannot commit between the two and leave the target watching a document it never saw begin.
    /// </remarks>
    internal static async Task<PageTarget> CreateAsync(
        Page page,
        string? browserContextId,
        bool waitForDebuggerOnStart = false,
        Action<PageTarget>? closed = null)
    {
        var target = new PageTarget(page, browserContextId, waitForDebuggerOnStart, closed);

        await page.RunOnLoopAsync(engine =>
        {
            target.InstallRuntime(engine);
            page.Observe(target);
            return true;
        }).ConfigureAwait(false);

        // The network log is the page's rather than a document's, so it is claimed once here and not on
        // every commit; it hands its notifications straight back on the transport thread they arrive on.
        page.NetworkLog.Listener = target;

        target.Publish(await page.TitleAsync().ConfigureAwait(false), page.Url);
        return target;
    }

    /// <inheritdoc/>
    internal override TargetDomains RegisterDomains(DevToolsSession session, BrowserSession? browser)
    {
        var domains = base.RegisterDomains(session, browser);

        var page = new PageDomain(this);
        var dom = new DomDomain(this);
        var input = new InputDomain(this);
        var emulation = new EmulationDomain(this);
        var network = new NetworkDomain(this);
        var fetch = new FetchDomain(this);
        var storage = new StorageDomain(this);
        var performance = new PerformanceDomain(this);
        var audits = new AuditsDomain();
        var accessibility = new AccessibilityDomain(this);
        var css = new CssDomain(this);
        var security = new SecurityDomain();
        var overlay = new OverlayDomain();
        var jint = new JintDomain(this);

        session
            .Register(page)
            .Register(dom)
            .Register(input)
            .Register(emulation)
            .Register(network)
            .Register(fetch)
            .Register(storage)
            .Register(performance)
            .Register(audits)
            .Register(accessibility)
            .Register(css)
            .Register(security)
            .Register(overlay)
            .Register(jint);

        AddDomain(page);
        Nodes.Add(dom);

        // Every one of these that listens -- the DOM domain hears about the engine being replaced under the
        // target the way the built-in five do -- is observed by `With` and unobserved again by `Detach`.
        return domains.With(
        [
            page, dom, input, emulation, network, fetch, storage, performance, audits, accessibility, css,
            security, overlay, jint,
        ]);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <b>The commands that release a paused request.</b>
    /// <c>FetchDomain.ContinueRequestAsync</c>, <c>FailRequestAsync</c> and <c>FulfillRequestAsync</c> look
    /// one entry up in a <c>ConcurrentDictionary</c> and complete a <c>TaskCompletionSource</c>; between
    /// them they touch no engine, no <c>JsValue</c> and no node, which is the bar the base class sets.
    /// </para>
    /// <para>
    /// <b>What that makes unconditional is that a pause never blocks the answer to it.</b> A
    /// <c>&lt;script src&gt;</c> a running script inserted is fetched with the loop blocked rather than
    /// pumping (<c>Runtime/Parsing/AGENTS.md</c>), so the one command that could end that block used to be
    /// queued behind it and could not be answered until the fetch gave up.
    /// </para>
    /// <para>
    /// <b><c>Fetch.enable</c> and <c>Fetch.disable</c> are deliberately not here.</b> They mark the domain
    /// enabled and emit through the session, which is a domain's own state rather than a request's, so they
    /// keep the ordering every other command has.
    /// </para>
    /// </remarks>
    internal override bool RunsOffThread(string method) => method switch
    {
        // continueResponse joins the three for the same reason and it is not a weaker one: a <script src>
        // a running script inserted is fetched with the loop *blocked* on the whole fetch — ParserDriver's
        // `fetch.GetAwaiter().GetResult()` — so the loop is held from the request stage through the body
        // read, and a response-stage pause holds it exactly as a request-stage pause does. All four look one
        // entry up in a dictionary and complete a promise: no engine, no JsValue, no node. continueWithAuth
        // is the fifth, and its pause is the same one held at the same point: the challenge arrives on the
        // hop's own response, with the loop blocked on that fetch if a running script started it.
        // getResponseBody is the sixth, and it is the one that *must* be answered here rather than merely
        // may be: it reads the socket the loop is blocked on, so answering it on the loop would be the loop
        // waiting for bytes nothing can ask for until it stops waiting. It reaches no engine either — a
        // reader over a transport stream, a byte array and a base64 string, and no JsValue and no node.
        "Fetch.continueRequest" or "Fetch.failRequest" or "Fetch.fulfillRequest" or "Fetch.continueResponse"
            or "Fetch.continueWithAuth" or "Fetch.getResponseBody" => true,
        _ => false,
    };

    /// <inheritdoc/>
    internal override ValueTask CloseAsync() => new(Page.CloseAsync());

    /// <summary>Registers one attachment's <c>Page</c> domain as a listener.</summary>
    internal void AddDomain(PageDomain domain)
    {
        lock (_domainGate)
        {
            _domains = [.. _domains, domain];
        }
    }

    /// <summary>Stops telling one attachment's <c>Page</c> domain anything, which detaching does.</summary>
    internal void RemoveDomain(PageDomain domain)
    {
        lock (_domainGate)
        {
            _domains = [.. _domains.Where(candidate => !ReferenceEquals(candidate, domain))];
        }
    }

    /// <inheritdoc/>
    void IPageObserver.NavigationRequested(string url, PageNavigationReason reason)
    {
        if (_uncommittedUrl is not null)
        {
            (_pendingNavigations ??= []).Add(new PendingNavigation(url, LoaderId: null, reason));
            return;
        }

        NavigationRequested(url, reason);
    }

    private void NavigationRequested(string url, PageNavigationReason reason)
    {
        foreach (var domain in Snapshot())
        {
            domain.NavigationRequested(url, reason);
        }
    }

    /// <inheritdoc/>
    void IPageObserver.NavigationStarted(string url, string loaderId)
    {
        foreach (var domain in Snapshot())
        {
            domain.NavigationStarted(url, loaderId);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The engine swap clears handles and installs the new contexts, bindings and new-document scripts
    /// before the first inline script runs. It is not yet the navigation commit: the parser has not made
    /// the document observable. Announcing the frame here would release a client's click navigation barrier
    /// while the parser can still be blocked before the response's body.
    /// </remarks>
    void IPageObserver.DocumentCreated(PageRuntime runtime, string loaderId)
    {
        _uncommittedUrl = runtime.DocumentUrl;
        Publish(title: null, runtime.DocumentUrl);

        // Before the swap, because the swap is what tells every DOM domain to announce documentUpdated and a
        // client that acted on it must find the identifiers already gone rather than resolving one more time.
        Nodes.DocumentReplaced();

        Replace(runtime.Engine);
        NewDocumentScripts.Run(runtime);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Arms the node tracker and publishes the frame commit after the parser has produced the document.
    /// </remarks>
    void IPageObserver.DocumentParsed(PageRuntime runtime, string loaderId)
    {
        Nodes.Watch(runtime);

        foreach (var domain in Snapshot())
        {
            domain.FrameNavigated(_uncommittedUrl ?? runtime.DocumentUrl, loaderId);
        }

        _uncommittedUrl = null;

        // Scripts can already have requested the next navigation or moved within this document. Publish
        // those after this commit, or a client's navigation barrier consumes this frame for the next one.
        if (_pendingNavigations is { } pending)
        {
            _pendingNavigations = null;
            foreach (var navigation in pending)
            {
                if (navigation.LoaderId is { } sameDocumentLoader)
                {
                    SameDocumentNavigated(navigation.Url, sameDocumentLoader);
                }
                else
                {
                    NavigationRequested(navigation.Url, navigation.Reason);
                }
            }
        }

        _navigationPublished?.TrySetResult();
        _navigationPublished = null;
    }

    /// <inheritdoc/>
    void IPageObserver.DocumentLoadFinished()
    {
        if (_uncommittedUrl is null)
        {
            return;
        }

        _uncommittedUrl = null;
        var pending = _pendingNavigations;
        _pendingNavigations = null;

        // A failed parse must not replay its history changes over the next document. Cross-document
        // requests are still queued on the page, however, and their clients must hear about them.
        if (pending is not null)
        {
            foreach (var navigation in pending)
            {
                if (navigation.LoaderId is null)
                {
                    NavigationRequested(navigation.Url, navigation.Reason);
                }
            }
        }

        _navigationPublished?.TrySetException(new ProtocolException("The document failed to commit during the input action."));
        _navigationPublished = null;
    }

    /// <inheritdoc/>
    void IPageObserver.Phase(NavigationPhase phase, string loaderId)
    {
        foreach (var domain in Snapshot())
        {
            domain.Phase(phase, loaderId);
        }
    }

    /// <inheritdoc/>
    void IPageObserver.SameDocumentNavigated(string url, string loaderId)
    {
        Publish(title: null, url);

        if (_uncommittedUrl is not null)
        {
            (_pendingNavigations ??= []).Add(new PendingNavigation(url, loaderId, default));
            return;
        }

        SameDocumentNavigated(url, loaderId);
    }

    private void SameDocumentNavigated(string url, string loaderId)
    {
        foreach (var domain in Snapshot())
        {
            domain.SameDocumentNavigated(url, loaderId);
        }
    }

    /// <inheritdoc/>
    void IPageObserver.TitleChanged(string title) => Publish(title, url: null);

    /// <summary>Moves what a client is told about this page, and about the tab it is in.</summary>
    private void Publish(string? title, string? url)
    {
        UpdateInfo(title, url);
        Tab?.Follow();
    }

    /// <inheritdoc/>
    void IPageObserver.DialogOpening(DialogEventArgs dialog)
    {
        var decision = Dialog;
        dialog.Accepted = decision.Accept;

        if (decision.Accept && dialog.Kind == DialogKind.Prompt && decision.PromptText.Length != 0)
        {
            dialog.PromptText = decision.PromptText;
        }

        foreach (var domain in Snapshot())
        {
            domain.DialogOpening(dialog);
        }
    }

    /// <inheritdoc/>
    void IPageObserver.DialogClosed(DialogEventArgs dialog)
    {
        foreach (var domain in Snapshot())
        {
            domain.DialogClosed(dialog);
        }
    }

    /// <inheritdoc/>
    void IPageObserver.NetworkIdle(string loaderId)
    {
        foreach (var domain in Snapshot())
        {
            domain.NetworkIdle(loaderId);
        }
    }

    /// <inheritdoc/>
    void IPageObserver.Closed()
    {
        Page.NetworkLog.Listener = null;
        _closed?.Invoke(this);
    }

    private PageDomain[] Snapshot() => Volatile.Read(ref _domains);

    private readonly record struct PendingNavigation(string Url, string? LoaderId, PageNavigationReason Reason);
}

/// <summary>What a client decided about the next dialog the page opens.</summary>
/// <param name="Accept">Whether to accept it.</param>
/// <param name="PromptText">What to answer a <c>prompt</c> with.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DialogDecision(bool Accept, string PromptText)
{
    /// <summary>Dismiss it, which is what a page with nobody watching already gets.</summary>
    internal static DialogDecision Dismiss { get; } = new(Accept: false, PromptText: "");
}

/// <summary>
/// The scripts <c>Page.addScriptToEvaluateOnNewDocument</c> installed, run in order before each document's
/// own.
/// </summary>
/// <remarks>
/// <b>An error in one is the page's, not the navigation's.</b> A client's instrumentation that throws must
/// not stop the document loading — Chrome does not — so a failure becomes a page error and the next script
/// runs.
/// </remarks>
internal sealed class NewDocumentScripts
{
    private readonly object _gate = new();
    private readonly List<(string Id, string Source)> _scripts = [];

    private int _next;

    /// <summary>Adds one and answers the identifier a client removes it by.</summary>
    internal string Add(string source)
    {
        lock (_gate)
        {
            var id = (++_next).ToString(CultureInfo.InvariantCulture);
            _scripts.Add((id, source));
            return id;
        }
    }

    /// <summary>Removes one, answering whether there was one.</summary>
    internal bool Remove(string identifier)
    {
        lock (_gate)
        {
            for (var i = 0; i < _scripts.Count; i++)
            {
                if (string.Equals(_scripts[i].Id, identifier, StringComparison.Ordinal))
                {
                    _scripts.RemoveAt(i);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Runs every script into <paramref name="runtime"/>, in the order they were added.</summary>
    internal void Run(PageRuntime runtime)
    {
        (string Id, string Source)[] scripts;
        lock (_gate)
        {
            if (_scripts.Count == 0)
            {
                return;
            }

            scripts = [.. _scripts];
        }

        foreach (var (id, source) in scripts)
        {
            try
            {
                runtime.Engine.Execute(source, "__jint_new_document_" + id);
            }
            catch (JavaScriptException exception)
            {
                runtime.Recorder.Add(
                    PageErrorKind.ScriptError,
                    exception.Message,
                    "Page.addScriptToEvaluateOnNewDocument#" + id);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                runtime.Recorder.Add(
                    PageErrorKind.ScriptError,
                    exception.Message,
                    "Page.addScriptToEvaluateOnNewDocument#" + id);
            }
        }
    }
}
