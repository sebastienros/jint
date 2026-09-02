using Jint.Browser.Runtime;
using Jint.DevTools;
using Jint.DevTools.Protocol.Page;
using ProtocolPageEvents = Jint.DevTools.Domains.PageEvents;

namespace Jint.Browser.DevTools;

/// <summary>
/// What the <c>Page</c> domain says without being asked: the frame it navigated and how far the load got.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order is Chrome's, taken from the recordings rather than from the specification.</b> For a
/// cross-document navigation it is <c>frameStartedNavigating</c>, <c>frameStartedLoading</c>,
/// <c>lifecycleEvent(init)</c>, <c>frameNavigated</c>, then the engine swap the base target performs
/// (<c>Runtime.executionContextsCleared</c> and <c>executionContextCreated</c>),
/// <c>lifecycleEvent(commit)</c>, <c>domContentEventFired</c> + <c>lifecycleEvent(DOMContentLoaded)</c>,
/// <c>loadEventFired</c> + <c>lifecycleEvent(load)</c>, <c>frameStoppedLoading</c>, and — once the network
/// has been quiet for half a second — <c>lifecycleEvent(networkAlmostIdle)</c> and
/// <c>lifecycleEvent(networkIdle)</c>. <c>Jint.Tests.Browser</c> pins it.
/// </para>
/// <para>
/// One divergence from the recording, and it comes from where the commit is announced: Chrome interleaves
/// <c>frameNavigated</c> between <c>executionContextsCleared</c> and <c>executionContextCreated</c>, and here
/// both the frame and the engine swap are one <see cref="Jint.Browser.Runtime.IPageObserver.DocumentCreated"/>
/// call — the moment the next document's engine exists and nothing of it has been parsed — so
/// <c>frameNavigated</c> is emitted just before the swap rather than inside it. Every other relative order is
/// the recording's.
/// </para>
/// <para>
/// <b>A lifecycle event goes out only while <c>setLifecycleEventsEnabled</c> is on</b>, which is the
/// protocol's rule and what every client turns on before it waits for one; everything else here follows
/// <c>Page.enable</c>.
/// </para>
/// </remarks>
internal sealed partial class PageDomain
{
    /// <summary>Chrome's own lifecycle names, which are what a client matches on.</summary>
    private const string Init = "init";

    /// <inheritdoc cref="Init"/>
    private const string Commit = "commit";

    /// <inheritdoc cref="Init"/>
    private const string DomContentLoaded = "DOMContentLoaded";

    /// <inheritdoc cref="Init"/>
    private const string Load = "load";

    /// <inheritdoc cref="Init"/>
    private const string NetworkAlmostIdle = "networkAlmostIdle";

    /// <inheritdoc cref="Init"/>
    private const string NetworkIdleName = "networkIdle";

    /// <summary>A navigation has been asked for and nothing has been fetched.</summary>
    internal void NavigationStarted(string url, string loaderId)
    {
        if (!IsEnabled)
        {
            return;
        }

        EmitDetached(ProtocolPageEvents.FrameStartedNavigating(new FrameStartedNavigatingEvent
        {
            FrameId = _target.FrameId,
            Url = url,
            LoaderId = loaderId,

            // Every navigation here reloads the document: there is no back/forward cache to restore one from,
            // which docs/design/headless-browser.md states and the session history's own comment repeats.
            NavigationType = FrameStartedNavigatingEventNavigationTypeValues.DifferentDocument,
        }));

        EmitDetached(ProtocolPageEvents.FrameStartedLoading(new FrameStartedLoadingEvent { FrameId = _target.FrameId }));
        Lifecycle(Init, loaderId);
    }

    /// <summary>The document has committed: its URL is settled and nothing of it has been parsed.</summary>
    internal void FrameNavigated(string url, string loaderId)
    {
        if (!IsEnabled)
        {
            return;
        }

        EmitDetached(ProtocolPageEvents.FrameNavigated(new FrameNavigatedEvent
        {
            Frame = Frame(url, loaderId),
            Type = NavigationTypeValues.Navigation,
        }));
    }

    /// <summary>The load reached one of its three points.</summary>
    internal void Phase(NavigationPhase phase, string loaderId)
    {
        if (!IsEnabled)
        {
            return;
        }

        switch (phase)
        {
            case NavigationPhase.Committed:
                Lifecycle(Commit, loaderId);
                break;

            case NavigationPhase.DomContentLoaded:
                EmitDetached(ProtocolPageEvents.DomContentEventFired(new DomContentEventFiredEvent { Timestamp = Timestamp() }));
                Lifecycle(DomContentLoaded, loaderId);
                break;

            default:
                EmitDetached(ProtocolPageEvents.LoadEventFired(new LoadEventFiredEvent { Timestamp = Timestamp() }));
                Lifecycle(Load, loaderId);
                EmitDetached(ProtocolPageEvents.FrameStoppedLoading(new FrameStoppedLoadingEvent { FrameId = _target.FrameId }));
                break;
        }
    }

    /// <summary>The page moved without replacing its document — <c>pushState</c>, a fragment, a traversal.</summary>
    internal void SameDocumentNavigated(string url, string loaderId)
    {
        if (!IsEnabled)
        {
            return;
        }

        EmitDetached(ProtocolPageEvents.NavigatedWithinDocument(new NavigatedWithinDocumentEvent
        {
            FrameId = _target.FrameId,
            Url = url,
            NavigationType = NavigatedWithinDocumentEventNavigationTypeValues.HistoryApi,
        }));
    }

    /// <summary>The network has been quiet since the document loaded.</summary>
    /// <remarks>
    /// Both names at once. Chrome's are two thresholds — two connections and none — measured over the same
    /// half-second; there is one quiet here and no connection pool to count, so a page that reaches
    /// <c>networkIdle</c> has passed <c>networkAlmostIdle</c> by definition and a client waiting on either is
    /// answered. A client waiting on <c>networkAlmostIdle</c> therefore waits marginally longer than Chrome
    /// would make it.
    /// </remarks>
    internal void NetworkIdle(string loaderId)
    {
        if (!IsEnabled)
        {
            return;
        }

        Lifecycle(NetworkAlmostIdle, loaderId);
        Lifecycle(NetworkIdleName, loaderId);
    }

    /// <summary>The page is opening a dialog and nothing has answered it yet.</summary>
    /// <remarks>
    /// <c>hasBrowserHandler</c> is <see langword="false"/>, which is what tells a client that the page's own
    /// default applies rather than a dialog waiting for it — and here that default is what
    /// <c>Page.handleJavaScriptDialog</c> last said. The two events therefore arrive together rather than
    /// with a round trip between them: the page has no thread to block, and the thread a client's answer
    /// would be delivered on is the one inside the script that opened it.
    /// </remarks>
    internal void DialogOpening(DialogEventArgs dialog)
    {
        if (!IsEnabled)
        {
            return;
        }

        EmitDetached(ProtocolPageEvents.JavascriptDialogOpening(new JavascriptDialogOpeningEvent
        {
            Url = Page.Url,
            FrameId = _target.FrameId,
            Message = dialog.Message,
            Type = DialogType(dialog.Kind),
            HasBrowserHandler = false,
            DefaultPrompt = dialog.Kind == DialogKind.Prompt ? dialog.DefaultPromptText : null,
        }));
    }

    /// <summary>The dialog is settled, and this is what the page is about to be given.</summary>
    internal void DialogClosed(DialogEventArgs dialog)
    {
        if (!IsEnabled)
        {
            return;
        }

        EmitDetached(ProtocolPageEvents.JavascriptDialogClosed(new JavascriptDialogClosedEvent
        {
            FrameId = _target.FrameId,
            Result = dialog.Accepted,
            UserInput = dialog.PromptText,
        }));
    }

    private void Lifecycle(string name, string loaderId)
    {
        if (!_lifecycleEvents)
        {
            return;
        }

        EmitDetached(ProtocolPageEvents.LifecycleEvent(new LifecycleEventEvent
        {
            FrameId = _target.FrameId,
            LoaderId = loaderId,
            Name = name,
            Timestamp = Timestamp(),
        }));
    }

    /// <summary>Which of the protocol's dialog types a page dialog is.</summary>
    private static string DialogType(DialogKind kind) => kind switch
    {
        DialogKind.Confirm => DialogTypeValues.Confirm,
        DialogKind.Prompt => DialogTypeValues.Prompt,
        _ => DialogTypeValues.Alert,
    };

    /// <summary>The protocol's monotonic timestamp, in seconds.</summary>
    private static double Timestamp() => DevToolsTarget.UnixMilliseconds() / 1000d;
}
