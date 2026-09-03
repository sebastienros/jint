namespace Jint.Browser.Mcp;

/// <summary>Where the page is now, which is what every tool that moves it answers.</summary>
/// <param name="Url">The page's URL after the call.</param>
/// <param name="Title">The document's title.</param>
/// <param name="Status">The HTTP status the document came from, or zero when it came from no network.</param>
public sealed record PageState(string Url, string Title, int Status);

/// <summary>The page as the agent should read it.</summary>
/// <param name="Url">The page's URL.</param>
/// <param name="Title">The document's title.</param>
/// <param name="Mode">Which representation this is: <c>markdown</c>, <c>text</c> or <c>ax</c>.</param>
/// <param name="Content">The representation itself.</param>
/// <param name="Truncated">Whether it was cut to fit the length limit.</param>
public sealed record PageSnapshot(string Url, string Title, string Mode, string Content, bool Truncated);

/// <summary>What an action did, and where the page ended up.</summary>
/// <param name="Done">Whether the target was found and acted on.</param>
/// <param name="Url">The page's URL afterwards, which a click on a link will have moved.</param>
/// <param name="Note">What happened, in one sentence an agent can act on.</param>
public sealed record ActionOutcome(bool Done, string Url, string Note);

/// <summary>One request the page made.</summary>
/// <param name="Url">The request's URL.</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="Status">The status the response carried, or zero while it is in flight.</param>
/// <param name="Initiator">What asked for it: <c>Document</c>, <c>Script</c> or <c>Subresource</c>.</param>
/// <param name="Failed">Whether it failed rather than answered.</param>
/// <param name="Reason">Why it failed, or why it was never fetched at all.</param>
public sealed record RequestLine(string Url, string Method, int Status, string Initiator, bool Failed, string? Reason);

/// <summary>One cookie the session holds for an origin.</summary>
/// <param name="Name">The cookie's name.</param>
/// <param name="Value">Its value.</param>
/// <param name="Domain">The domain it is scoped to.</param>
/// <param name="Path">The path it is scoped to.</param>
/// <param name="HttpOnly">Whether script may not read it.</param>
/// <param name="Secure">Whether it is sent only over <c>https</c>.</param>
public sealed record CookieLine(string Name, string Value, string Domain, string Path, bool HttpOnly, bool Secure);

/// <summary>
/// What a tool could not do, said in one sentence rather than thrown through the transport.
/// </summary>
/// <remarks>
/// A model reads a tool's failure and decides what to try next, so the message is written for it: what was
/// asked, what happened, and — where there is one — what to do instead. It is never a stack trace.
/// </remarks>
public sealed class BrowserToolException : Exception
{
    /// <summary>Creates an exception carrying <paramref name="message"/>.</summary>
    /// <param name="message">What could not be done, in one sentence.</param>
    public BrowserToolException(string message) : base(message)
    {
    }

    /// <summary>Creates an exception carrying <paramref name="message"/> and what caused it.</summary>
    /// <param name="message">What could not be done, in one sentence.</param>
    /// <param name="innerException">The failure underneath.</param>
    public BrowserToolException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>Creates an exception with no message of its own.</summary>
    public BrowserToolException()
    {
    }
}
