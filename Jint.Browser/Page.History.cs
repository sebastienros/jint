namespace Jint.Browser;

/// <summary>
/// The three buttons a browser puts above the page: back, forward and reload.
/// </summary>
/// <remarks>
/// <para>
/// They are the session history <c>history.back()</c>, <c>history.forward()</c> and
/// <c>location.reload()</c> move — one history, so a page's own script and a caller here cannot disagree
/// about where it has been — and they are the same entries the protocol's
/// <c>Page.getNavigationHistory</c> and <c>Page.navigateToHistoryEntry</c> address.
/// </para>
/// <para>
/// <b>A traversal is asynchronous, which is HTML's own model.</b> These members wait for it: a step that
/// stays in one document is a <c>popstate</c> or a <c>hashchange</c> on the page's own loop, and one that
/// crosses documents is a navigation like any other, because there is no back/forward cache here.
/// </para>
/// </remarks>
public sealed partial class Page
{
    /// <summary>Goes back one entry in the session history.</summary>
    /// <param name="timeout">The ceiling on the wait for the step to commit.</param>
    /// <returns>
    /// <see langword="true"/> when a step was taken and committed, <see langword="false"/> when there was
    /// nothing to go back to or the timeout won.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> GoBackAsync(TimeSpan timeout) => TraverseAsync(-1, timeout);

    /// <summary>Goes forward one entry in the session history.</summary>
    /// <param name="timeout">The ceiling on the wait for the step to commit.</param>
    /// <returns>
    /// <see langword="true"/> when a step was taken and committed, <see langword="false"/> when there was
    /// nothing to go forward to or the timeout won.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> GoForwardAsync(TimeSpan timeout) => TraverseAsync(1, timeout);

    /// <summary>Loads the current URL again, replacing the document and the engine behind it.</summary>
    /// <param name="options">How far to wait and how long to allow; the defaults when omitted.</param>
    /// <returns>The response the document came from, or <see langword="null"/> for a URL that reached no network.</returns>
    /// <remarks>
    /// It is a reload rather than a navigation to the same address, which matters for a URL with a fragment:
    /// navigating to that would be a same-document fragment move, and this replaces the document. The
    /// history entry is rewritten rather than added, so a reload does not lengthen the history — which is
    /// what a browser does.
    /// </remarks>
    /// <exception cref="NavigationFailedException">There was no document to show.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<PageResponse?> ReloadAsync(NavigationOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_closed, this);

        return NavigateCoreAsync(new NavigationRequest(
            Url,
            options ?? NavigationOptions.Default,
            HistoryMode.Replace,
            TraversalIndex: -1,
            Body: null,
            ContentType: null,
            Reload: true,
            Referrer: null));
    }

    /// <summary>Steps <paramref name="delta"/> entries through the history and waits for the commit.</summary>
    /// <remarks>
    /// <b>The wait is armed before the step, and that order is the whole point.</b> A traversal is queued
    /// rather than run inline — HTML says so — and it commits on the page's own thread, so a wait started
    /// afterwards could miss a commit that has already happened and time out instead.
    /// </remarks>
    private async Task<bool> TraverseAsync(int delta, TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_closed, this);

        // Asked before the wait is armed, so that "there is nothing to go back to" is answered at once rather
        // than by waiting out the whole timeout for a commit that was never going to come.
        var available = await _loop.PostAsync(engine => _history.Peek(delta, out _) is not null).ConfigureAwait(false);
        if (!available)
        {
            return false;
        }

        var committed = WaitForNavigationAsync(timeout);
        await _loop.PostAsync(engine =>
        {
            RequestTraversal(delta);
            return true;
        }).ConfigureAwait(false);

        return await committed.ConfigureAwait(false);
    }
}
