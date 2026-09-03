using System.Globalization;
using Jint.Browser.Runtime;
using Jint.DevTools;
using Jint.DevTools.Domains;
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
    private PageRuntime? _runtime;

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

        // The DOM domain hears about the engine being replaced under the target the way the built-in five do,
        // and is unobserved again with them when the attachment detaches.
        Observe(dom);
        Nodes.Add(dom);

        return domains with
        {
            Extra =
            [
                page, dom, input, emulation, network, fetch, storage, performance, audits, accessibility, css,
                security, overlay, jint,
            ],
        };
    }

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
    void IPageObserver.NavigationStarted(string url, string loaderId)
    {
        foreach (var domain in Snapshot())
        {
            domain.NavigationStarted(url, loaderId);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The order here is the whole of a commit as a client sees it: <c>frameNavigated</c> first, because the
    /// document's URL is settled and nothing of it has been parsed; then the engine swap, which is what
    /// clears every handle and announces the new execution context; then the bindings the base class
    /// re-installs, and finally the client's own new-document scripts — every one of which has to be in place
    /// before the document's first inline script runs.
    /// </remarks>
    void IPageObserver.DocumentCreated(PageRuntime runtime, string loaderId)
    {
        Publish(title: null, runtime.DocumentUrl);

        foreach (var domain in Snapshot())
        {
            domain.FrameNavigated(runtime.DocumentUrl, loaderId);
        }

        // Before the swap, because the swap is what tells every DOM domain to announce documentUpdated and a
        // client that acted on it must find the identifiers already gone rather than resolving one more time.
        _runtime = runtime;
        Nodes.DocumentReplaced();

        Replace(runtime.Engine);
        NewDocumentScripts.Run(runtime);
    }

    /// <inheritdoc/>
    void IPageObserver.Phase(NavigationPhase phase, string loaderId)
    {
        if (phase == NavigationPhase.Committed && _runtime is { } runtime)
        {
            // The first moment the document exists and the parse is over, which is when a client's view of
            // the tree can start being kept current. Idempotent, and a no-op while nobody has enabled DOM.
            Nodes.Watch(runtime);
        }

        foreach (var domain in Snapshot())
        {
            domain.Phase(phase, loaderId);
        }
    }

    /// <inheritdoc/>
    void IPageObserver.SameDocumentNavigated(string url, string loaderId)
    {
        Publish(title: null, url);

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
