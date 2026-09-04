namespace Jint.Browser;

/// <summary>
/// How far a navigation runs before <see cref="Page.NavigateAsync(string, NavigationOptions?)"/> answers.
/// </summary>
public enum WaitUntilState
{
    /// <summary>The response is in and the new document has been committed as the page's.</summary>
    /// <remarks>
    /// The parse may still be running on the page's own thread when the call returns; the next request a
    /// caller posts queues behind it, so nothing is observable half-parsed.
    /// </remarks>
    Commit,

    /// <summary>The document has been parsed and <c>DOMContentLoaded</c> has been dispatched.</summary>
    DomContentLoaded,

    /// <summary>The <c>load</c> event has been dispatched at the window.</summary>
    Load,
}

/// <summary>
/// What one navigation may do: how long it may take, how far it runs before answering, and whether the
/// document being left is allowed to stop it.
/// </summary>
/// <remarks>
/// <para>
/// Every member has a default that suits an automation caller, so <c>new NavigationOptions()</c> is a
/// reasonable thing to pass and omitting the argument entirely is the same as passing one.
/// </para>
/// <para>
/// An instance is read once, at the start of the navigation it is passed to, and never retained; the same
/// instance may be used for any number of navigations on any number of pages.
/// </para>
/// </remarks>
public sealed class NavigationOptions
{
    private TimeSpan _timeout = TimeSpan.FromSeconds(30);

    /// <summary>How far the navigation runs before the call answers; <see cref="WaitUntilState.Load"/>.</summary>
    public WaitUntilState WaitUntil { get; set; } = WaitUntilState.Load;

    /// <summary>
    /// The ceiling on the whole navigation — the fetch, the parse and the wait; 30 seconds by default.
    /// </summary>
    /// <remarks>
    /// Exceeding it fails the call with a <see cref="NavigationFailedException"/> and leaves the page on
    /// whichever document it had reached: a timeout during the fetch leaves the previous document in place,
    /// and one during the parse leaves the new one part-built, exactly as the page itself would see it.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is neither positive nor <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>.
    /// </exception>
    public TimeSpan Timeout
    {
        get => _timeout;
        set => _timeout = value > TimeSpan.Zero || value == System.Threading.Timeout.InfiniteTimeSpan
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Timeout must be positive or infinite.");
    }

    /// <summary>
    /// Whether the document being left may cancel the navigation from its <c>beforeunload</c> handler; off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>beforeunload</c> is dispatched either way — a page that saves a draft on the way out still runs —
    /// and this decides only whether a cancellation is honoured. Off by default because an automation caller
    /// asking a page to navigate means it, and because a browser's own answer is a dialog nothing here can
    /// show.
    /// </para>
    /// <para>
    /// With it on, a navigation the page cancelled fails the call with a
    /// <see cref="NavigationFailedException"/> rather than answering quietly, so a caller is never told a
    /// navigation happened when it did not. HTML's three ways of cancelling all count:
    /// <c>event.preventDefault()</c>, assigning a non-empty <c>event.returnValue</c>, and returning a
    /// non-null value from an <c>onbeforeunload</c> handler.
    /// </para>
    /// </remarks>
    public bool AllowCancel { get; set; }

    /// <summary>
    /// What the request reports as its <c>Referer</c>, or <see langword="null"/> to use the current document.
    /// </summary>
    /// <remarks>
    /// A navigation started by a page — a link, a form, an assignment to <c>location</c> — always reports the
    /// document that started it, so this is read only for a navigation a host asks for.
    /// </remarks>
    public string? Referrer { get; set; }

    internal static readonly NavigationOptions Default = new();
}
