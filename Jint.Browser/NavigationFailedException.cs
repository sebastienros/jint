namespace Jint.Browser;

/// <summary>
/// A navigation that never produced a document: a refused URL, a network failure, a timeout, or a page that
/// cancelled it.
/// </summary>
/// <remarks>
/// <para>
/// A response is <b>not</b> a failure. A <c>404</c>, a <c>500</c> and an empty body all navigate: the status
/// is on <see cref="Page.Response"/> and the body is the document, which is what a browser does and what a
/// caller scraping an error page needs. This is thrown only when there is nothing to show.
/// </para>
/// <para>
/// A page closed while a navigation was in flight ends with <see cref="OperationCanceledException"/> instead,
/// because the caller's request did not fail — it was abandoned.
/// </para>
/// </remarks>
public sealed class NavigationFailedException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="NavigationFailedException"/> class.</summary>
    public NavigationFailedException()
    {
        Url = "";
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">Why the navigation failed.</param>
    public NavigationFailedException(string message) : base(message)
    {
        Url = "";
    }

    /// <summary>Initializes a new instance with a message and the failure underneath it.</summary>
    /// <param name="message">Why the navigation failed.</param>
    /// <param name="innerException">The failure this one wraps.</param>
    public NavigationFailedException(string message, Exception innerException) : base(message, innerException)
    {
        Url = "";
    }

    internal NavigationFailedException(string url, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Url = url;
    }

    /// <summary>The URL the navigation was aimed at, or the empty string when none was known.</summary>
    public string Url { get; }
}
