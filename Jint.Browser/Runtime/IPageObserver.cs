namespace Jint.Browser.Runtime;

/// <summary>
/// What one watcher of a page is told, in the order a page does it.
/// </summary>
/// <remarks>
/// <para>
/// The seam the protocol layer hangs off, and the only one: a <c>Page</c> target is a
/// <c>Jint.DevTools.DevToolsTarget</c> that implements this, and everything a client is told about a page —
/// the frame it navigated, the document that committed, the lifecycle it reached, the dialog it opened — is
/// one of these calls turned into an event. One observer per page, because a page has one target.
/// </para>
/// <para>
/// <b>The members that carry a <see cref="PageRuntime"/> or a <see cref="DialogEventArgs"/> run on the page
/// loop thread</b>, and <see cref="DocumentCreated"/> runs with the engine of the document that is about to
/// be parsed: that is what makes it the place to replace a target's engine, re-install its bindings, and run
/// the scripts a client asked to be evaluated on every new document. Nothing here may block — the loop it is
/// on is what would have to run whatever it waited for. <see cref="NavigationStarted"/> runs on whichever
/// thread started the navigation, <see cref="NetworkIdle"/> on the page's own quiet-period timer, and
/// <see cref="Closed"/> on whichever thread closed the page; none of the three carries anything that
/// belongs to an engine.
/// </para>
/// <para>
/// <b>A <c>loaderId</c> names one committed document</b> and every signal of that document carries it. It is
/// minted when a navigation starts, so <see cref="NavigationStarted"/> and the <see cref="Phase"/> calls that
/// follow it agree; a same-document move keeps the one the document already had.
/// </para>
/// </remarks>
internal interface IPageObserver
{
    /// <summary>A navigation has been asked for, before anything has been fetched.</summary>
    /// <param name="url">Where the page is going, resolved against the current document.</param>
    /// <param name="loaderId">The identifier the document this produces will carry.</param>
    /// <remarks>Runs off the loop, on whichever thread started the navigation.</remarks>
    void NavigationStarted(string url, string loaderId)
    {
    }

    /// <summary>
    /// The engine of the next document exists, its window is installed, and nothing of the document has been
    /// parsed yet.
    /// </summary>
    /// <param name="runtime">The page runtime of the new engine.</param>
    /// <param name="loaderId">The identifier this document carries.</param>
    void DocumentCreated(PageRuntime runtime, string loaderId)
    {
    }

    /// <summary>The load reached one of its three points.</summary>
    /// <param name="phase">How far it got.</param>
    /// <param name="loaderId">The document that reached it.</param>
    void Phase(NavigationPhase phase, string loaderId)
    {
    }

    /// <summary>A navigation that kept the document moved the page's URL.</summary>
    /// <param name="url">Where the page now says it is.</param>
    /// <param name="loaderId">The document, which is the one that was already showing.</param>
    /// <remarks><c>pushState</c>, <c>replaceState</c>, a fragment navigation and a same-document traversal.</remarks>
    void SameDocumentNavigated(string url, string loaderId)
    {
    }

    /// <summary>The document's title is what a client should now be told it is.</summary>
    /// <param name="title">The title.</param>
    void TitleChanged(string title)
    {
    }

    /// <summary>The page is calling <c>alert</c>, <c>confirm</c> or <c>prompt</c>, and nobody has answered.</summary>
    /// <param name="dialog">What it asked. A watcher may answer it by setting the event's own members.</param>
    /// <remarks>
    /// <para>
    /// Runs inside the script that opened the dialog. <b>The dialog does not wait</b>: the page has no thread
    /// to block, because the thread a client's answer would be delivered on is this one. So a watcher answers
    /// from a decision it already holds rather than from one it goes and asks for.
    /// </para>
    /// <para>
    /// <b>The host's own handler runs after this and wins.</b> <see cref="Page.DialogOpened"/> is the
    /// embedder's, and an embedder that attached one owns the answer; a watcher's decision is the default the
    /// handler starts from.
    /// </para>
    /// </remarks>
    void DialogOpening(DialogEventArgs dialog)
    {
    }

    /// <summary>The dialog is settled, and this is what the page is about to be told.</summary>
    /// <param name="dialog">What it asked and what was decided.</param>
    void DialogClosed(DialogEventArgs dialog)
    {
    }

    /// <summary>The page's network has been quiet since the document loaded.</summary>
    /// <param name="loaderId">The document whose load this belongs to.</param>
    /// <remarks>Runs on the page's own timer rather than on the loop: a page with nothing scheduled never
    /// turns its loop and would never notice the quiet.</remarks>
    void NetworkIdle(string loaderId)
    {
    }

    /// <summary>The page has been closed, and nothing else will arrive.</summary>
    /// <remarks>Runs on whichever thread closed the page, after its loop has stopped.</remarks>
    void Closed()
    {
    }
}
