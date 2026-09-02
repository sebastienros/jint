#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Jint.WebApi.Fetch;

/// <summary>
/// One header of a request or a response, as an observer sees it.
/// </summary>
/// <param name="Name">The header's name, byte-lowercased as the Fetch Standard stores it.</param>
/// <param name="Value">The header's value.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct FetchHeader(string Name, string Value);

/// <summary>
/// Identifies one request across every hop of its redirect chain.
/// </summary>
/// <param name="Value">A number unique within the process for the lifetime of the request.</param>
/// <remarks>
/// Stable from the first <c>OnRequest</c> to the terminal <c>OnCompleted</c> or <c>OnFailed</c>, which is
/// what lets an observer correlate a body chunk with the request that asked for it.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly record struct FetchRequestId(long Value)
{
    /// <inheritdoc />
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// What asked for a request.
/// </summary>
/// <remarks>
/// Only <see cref="Script"/> is produced today; the rest name the callers a host layer above Jint adds, and
/// new members may appear, so switch with a default arm.
/// </remarks>
public enum FetchInitiator
{
    /// <summary>A <c>fetch()</c> call made by script.</summary>
    Script = 0,

    /// <summary>A request the host started rather than script — a document or subresource load.</summary>
    Host = 1,
}

/// <summary>
/// A request about to go on the wire, as plain CLR data.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here is engine state.</b> There is no <c>JsValue</c>, no <see cref="Engine"/> and no realm,
/// which is what lets an observer be called from a transport thread while script goes on running.
/// </para>
/// <para>
/// <b>One instance per hop.</b> A redirect produces another with the same <see cref="Id"/>, the new URL and
/// <see cref="RedirectResponse"/> set to the response that caused it.
/// </para>
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public sealed record ObservedFetchRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObservedFetchRequest"/> record.
    /// </summary>
    public ObservedFetchRequest()
    {
    }

    /// <summary>Gets the identifier shared by every hop of this request.</summary>
    public required FetchRequestId Id { get; init; }

    /// <summary>Gets what asked for the request.</summary>
    public required FetchInitiator Initiator { get; init; }

    /// <summary>Gets the absolute URL this hop will be sent to.</summary>
    public required Uri Url { get; init; }

    /// <summary>Gets the HTTP method this hop will use.</summary>
    public required string Method { get; init; }

    /// <summary>Gets the headers this hop will carry, including the ones the engine appended itself.</summary>
    public required IReadOnlyList<FetchHeader> Headers { get; init; }

    /// <summary>Whether the request carries a body.</summary>
    public bool HasBody { get; init; }

    /// <summary>
    /// Gets the first bytes of the request body, at most <see cref="FetchObserver.RequestBodyPreviewBytes"/>
    /// of them and empty unless the observer asked for some.
    /// </summary>
    /// <remarks>
    /// Always empty for a body that is a <c>ReadableStream</c>: those bytes are produced as the socket
    /// drains them and there is nothing to preview before the request is sent.
    /// </remarks>
    public ReadOnlyMemory<byte> BodyPreview { get; init; }

    /// <summary>Gets how many redirects this request has already followed; zero for the first hop.</summary>
    public int RedirectCount { get; init; }

    /// <summary>
    /// Gets the redirect response that produced this hop, or <see langword="null"/> for the first hop.
    /// </summary>
    public ObservedFetchResponse? RedirectResponse { get; init; }
}

/// <summary>
/// A response's headers, as plain CLR data, before its body has been read.
/// </summary>
/// <remarks>
/// Reported for every hop: a redirect this chain is about to follow arrives here first and again on the next
/// hop's <see cref="ObservedFetchRequest.RedirectResponse"/>.
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public sealed record ObservedFetchResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObservedFetchResponse"/> record.
    /// </summary>
    public ObservedFetchResponse()
    {
    }

    /// <summary>Gets the identifier shared by every hop of the request this answers.</summary>
    public required FetchRequestId Id { get; init; }

    /// <summary>Gets the absolute URL that produced this response.</summary>
    public required Uri Url { get; init; }

    /// <summary>Gets the HTTP status code.</summary>
    public required int Status { get; init; }

    /// <summary>Gets the HTTP reason phrase, which may be empty.</summary>
    public required string StatusText { get; init; }

    /// <summary>Gets the response headers, one entry per value.</summary>
    public required IReadOnlyList<FetchHeader> Headers { get; init; }

    /// <summary>Whether an observer produced this response instead of the network.</summary>
    public bool FromInterception { get; init; }

    /// <summary>Whether this is a redirect the request is about to follow.</summary>
    public bool IsRedirect { get; init; }
}

/// <summary>
/// What an observer answers a request with when it does not want the request sent as written.
/// </summary>
/// <remarks>
/// Build one with <see cref="Fulfill"/>, <see cref="Fail"/> or <see cref="Continue"/>; answering
/// <see langword="null"/> instead lets the hop go as it is.
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public sealed class FetchInterception
{
    private FetchInterception()
    {
    }

    internal FetchInterceptionKind Kind { get; private init; }

    internal int Status { get; private init; }

    internal string? StatusText { get; private init; }

    internal IReadOnlyList<FetchHeader>? Headers { get; private init; }

    internal ReadOnlyMemory<byte> Body { get; private init; }

    internal bool HasBody { get; private init; }

    internal Uri? Url { get; private init; }

    internal string? Method { get; private init; }

    internal string? Reason { get; private init; }

    /// <summary>
    /// Answers the request from the observer, without opening a socket.
    /// </summary>
    /// <param name="status">The HTTP status code the script will see.</param>
    /// <param name="headers">The response headers, or <see langword="null"/> for none.</param>
    /// <param name="body">The response body.</param>
    /// <param name="statusText">The reason phrase, or <see langword="null"/> for the empty string.</param>
    /// <remarks>
    /// A fulfilled response ends the chain: a <c>3xx</c> status here is handed to the script as it is rather
    /// than followed, because the redirect loop only follows what the network answered.
    /// </remarks>
    public static FetchInterception Fulfill(
        int status,
        IReadOnlyList<FetchHeader>? headers = null,
        ReadOnlyMemory<byte> body = default,
        string? statusText = null)
        => new()
        {
            Kind = FetchInterceptionKind.Fulfill,
            Status = status,
            StatusText = statusText,
            Headers = headers,
            Body = body,
            HasBody = true,
        };

    /// <summary>
    /// Fails the request, which the script sees as the same <c>TypeError</c> a network failure produces.
    /// </summary>
    /// <param name="reason">Why it failed, for the host's own logs; script never sees it.</param>
    public static FetchInterception Fail(string reason)
        => new() { Kind = FetchInterceptionKind.Fail, Reason = reason ?? string.Empty };

    /// <summary>
    /// Sends the request with some of it rewritten; every argument left <see langword="null"/> keeps what
    /// the hop already had.
    /// </summary>
    /// <param name="url">An absolute URL to send to instead.</param>
    /// <param name="method">An HTTP method to use instead.</param>
    /// <param name="headers">The complete header list to send instead of the hop's own.</param>
    /// <param name="body">A request body to send instead.</param>
    /// <remarks>
    /// A rewritten URL is re-checked against <c>Options.WebApi.Fetch.AllowedSchemes</c> and
    /// <c>UrlFilter</c> exactly as a redirect target is: an observer widens what a request may say, never
    /// where it may go.
    /// </remarks>
    public static FetchInterception Continue(
        Uri? url = null,
        string? method = null,
        IReadOnlyList<FetchHeader>? headers = null,
        ReadOnlyMemory<byte>? body = null)
        => new()
        {
            Kind = FetchInterceptionKind.Continue,
            Url = url,
            Method = method,
            Headers = headers,
            Body = body ?? default,
            HasBody = body is not null,
        };
}

internal enum FetchInterceptionKind
{
    Continue,
    Fulfill,
    Fail,
}

/// <summary>
/// Watches every hop, response and body chunk of the requests one engine makes, and may answer a request
/// itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every callback runs on a transport thread and must never touch the <see cref="Engine"/>.</b> The
/// engine is not thread-safe and the script that started the fetch is still running; an observer that has to
/// reach it posts to the engine's own loop instead.
/// </para>
/// <para>
/// <b>The order is fixed</b>: <c>OnRequest</c> for the first hop, then for a redirect chain
/// <c>OnResponse</c>(the redirect) and <c>OnRequest</c>(the next hop) in turn, then <c>OnResponse</c> for
/// the final response, then <c>OnData</c> per body chunk, then exactly one of <c>OnCompleted</c> and
/// <c>OnFailed</c>.
/// </para>
/// <para>
/// <b>A notification that throws is ignored</b> — there is no engine thread to report it to and a transfer
/// must not depend on an observer. Only <see cref="OnRequestAsync"/> is different: a throw there fails the
/// fetch, because it is the callback that was asked to decide.
/// </para>
/// <para>
/// The shape of this class is a preview and is declared to the compiler as <c>JINT0002</c>; see
/// <see cref="Options.FetchOptions.Observer"/>.
/// </para>
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public abstract class FetchObserver
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FetchObserver"/> class.
    /// </summary>
    protected FetchObserver()
    {
    }

    /// <summary>
    /// Gets how many bytes of a request body to copy into
    /// <see cref="ObservedFetchRequest.BodyPreview"/>. Defaults to zero.
    /// </summary>
    /// <remarks>
    /// Read once per request. The copy is what the observer is charged for, so an observer that wants whole
    /// bodies says so here and accepts the memory.
    /// </remarks>
    public virtual int RequestBodyPreviewBytes => 0;

    /// <summary>
    /// Called before each hop is sent; answer <see langword="null"/> to send it unchanged.
    /// </summary>
    /// <param name="request">The hop about to be sent.</param>
    /// <param name="cancellationToken">Cancelled when the fetch is aborted or times out.</param>
    /// <remarks>
    /// The whole fetch is still bounded by <c>Options.WebApi.Fetch.Timeout</c>, so time spent here comes out
    /// of the request's own deadline.
    /// </remarks>
    public virtual ValueTask<FetchInterception?> OnRequestAsync(ObservedFetchRequest request, CancellationToken cancellationToken)
        => new((FetchInterception?) null);

    /// <summary>
    /// Called once per hop, as soon as that hop's response headers are in and before its body is read.
    /// </summary>
    /// <param name="response">The response headers.</param>
    public virtual void OnResponse(ObservedFetchResponse response)
    {
    }

    /// <summary>
    /// Called for each chunk of the final response's body as it is read off the wire.
    /// </summary>
    /// <param name="id">The request the chunk belongs to.</param>
    /// <param name="chunk">The bytes, valid only for the duration of the call.</param>
    /// <remarks>
    /// The span is a window onto a pooled buffer: an observer that keeps the bytes copies them, and one that
    /// keeps all of them bounds that itself.
    /// </remarks>
    public virtual void OnData(FetchRequestId id, ReadOnlySpan<byte> chunk)
    {
    }

    /// <summary>
    /// Called once, when the final response's body has been read to its end.
    /// </summary>
    /// <param name="id">The request that finished.</param>
    /// <param name="bodyLength">How many bytes the body turned out to be.</param>
    /// <remarks>
    /// A response nobody reads never completes: the body is only pulled when script consumes it, so a
    /// request whose <c>Response</c> is dropped ends at <see cref="OnResponse"/>.
    /// </remarks>
    public virtual void OnCompleted(FetchRequestId id, long bodyLength)
    {
    }

    /// <summary>
    /// Called once, when the request failed instead of finishing.
    /// </summary>
    /// <param name="id">The request that failed.</param>
    /// <param name="reason">Why, in the host's own words; script sees only <c>Failed to fetch</c>.</param>
    /// <param name="exception">The originating CLR exception, when there was one.</param>
    /// <remarks>
    /// Reports a refused URL, a blown <c>MaxRedirects</c> or <c>MaxResponseBytes</c>, a timeout, an abort
    /// and every transport failure — and an interception's own <see cref="FetchInterception.Fail"/>.
    /// </remarks>
    public virtual void OnFailed(FetchRequestId id, string reason, Exception? exception)
    {
    }
}
#endif
