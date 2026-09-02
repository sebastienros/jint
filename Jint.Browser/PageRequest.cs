namespace Jint.Browser;

/// <summary>What asked for a request the page made.</summary>
public enum RequestInitiator
{
    /// <summary>A document load — a navigation, or a form submission that became one.</summary>
    Document,

    /// <summary>Script: a <c>fetch</c> call, an <c>XMLHttpRequest</c>, or a worker's module load.</summary>
    Script,
}

/// <summary>
/// One request the page made and what became of it, recorded as plain text on the transport thread.
/// </summary>
/// <remarks>
/// <para>
/// It is a <b>summary rather than a transcript</b>: the URL of the last hop, the method, the status and the
/// response headers. Bodies are not kept — a page fetching a large document would otherwise be a memory
/// budget nobody set — and neither are request headers, which the transport composes per hop.
/// </para>
/// <para>
/// A record is created when the first hop goes out and completed when the exchange ends, so a request in
/// flight is already in <see cref="Page.Requests"/> with <see cref="Status"/> still zero. Reading the list
/// while a request is running is therefore safe and gives the entries as far as they have got.
/// </para>
/// </remarks>
public sealed class PageRequest
{
    private volatile Snapshot _snapshot;

    internal PageRequest(long id, RequestInitiator initiator, string url, string method)
    {
        Id = id;
        Initiator = initiator;
        _snapshot = new Snapshot(url, method, 0, "", [], false, null, 0, 0);
    }

    /// <summary>A number unique within the process, shared by every hop of one request.</summary>
    public long Id { get; }

    /// <summary>What asked for the request.</summary>
    public RequestInitiator Initiator { get; }

    /// <summary>The URL of the most recent hop.</summary>
    public string Url => _snapshot.Url;

    /// <summary>The method of the most recent hop; a redirect can rewrite <c>POST</c> to <c>GET</c>.</summary>
    public string Method => _snapshot.Method;

    /// <summary>The status of the final response, or <c>0</c> while the request is still in flight.</summary>
    public int Status => _snapshot.Status;

    /// <summary>The reason phrase of the final response, which may be empty.</summary>
    public string StatusText => _snapshot.StatusText;

    /// <summary>The final response's headers, one entry per value.</summary>
    public IReadOnlyList<PageHeader> ResponseHeaders => _snapshot.Headers;

    /// <summary>How many redirects the request followed.</summary>
    public int RedirectCount => _snapshot.RedirectCount;

    /// <summary>Whether the request ended in a failure rather than a response.</summary>
    public bool Failed => _snapshot.Failed;

    /// <summary>Why it failed, in the transport's own words, or <see langword="null"/>.</summary>
    public string? FailureReason => _snapshot.FailureReason;

    /// <summary>How many bytes of body were read, once the body has been read to its end.</summary>
    public long BodyLength => _snapshot.BodyLength;

    /// <inheritdoc />
    public override string ToString()
    {
        var state = _snapshot;
        if (state.Failed)
        {
            return state.Method + " " + state.Url + " failed: " + state.FailureReason;
        }

        return state.Status == 0
            ? state.Method + " " + state.Url + " (pending)"
            : state.Method + " " + state.Url + " " + state.Status.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    internal void Hop(string url, string method, int redirectCount)
    {
        var state = _snapshot;
        _snapshot = state with { Url = url, Method = method, RedirectCount = redirectCount };
    }

    internal void Responded(int status, string statusText, IReadOnlyList<PageHeader> headers)
    {
        var state = _snapshot;
        _snapshot = state with { Status = status, StatusText = statusText, Headers = headers };
    }

    internal void Completed(long bodyLength)
    {
        var state = _snapshot;
        _snapshot = state with { BodyLength = bodyLength };
    }

    internal void Failure(string reason)
    {
        var state = _snapshot;
        _snapshot = state with { Failed = true, FailureReason = reason };
    }

    /// <summary>
    /// Every mutable field in one immutable object, swapped as a unit.
    /// </summary>
    /// <remarks>
    /// The observer's callbacks arrive on transport threads while a host reads the list from its own, so the
    /// alternative would be a lock on every property. One volatile reference to a record makes a reader see a
    /// coherent request rather than half of two.
    /// </remarks>
    private sealed record Snapshot(
        string Url,
        string Method,
        int Status,
        string StatusText,
        IReadOnlyList<PageHeader> Headers,
        bool Failed,
        string? FailureReason,
        long BodyLength,
        int RedirectCount);
}
