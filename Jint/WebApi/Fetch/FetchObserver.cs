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
/// When one hop went out, and how long it was until its response headers were in.
/// </summary>
/// <param name="SentAt">
/// The wall-clock instant the hop was handed to the transport, read immediately before the send.
/// </param>
/// <param name="TimeToHeaders">
/// How long it took from that instant until the hop's response headers were in — the hop's time to first
/// byte, and the only duration this engine can measure.
/// </param>
/// <remarks>
/// <para>
/// <b>Two clocks, deliberately.</b> <paramref name="SentAt"/> is wall-clock, so a host reporting to a tool
/// can put the hop on the same timeline as everything else it timestamps; <paramref name="TimeToHeaders"/> is
/// measured monotonically between two readings taken either side of the one call that knows, so a system
/// clock adjusted mid-request cannot turn a time to first byte negative. Adding the two therefore gives an
/// instant that is only as accurate as the wall clock was when the hop went out, which is why
/// <see cref="HeadersAt"/> is derived rather than measured.
/// </para>
/// <para>
/// <b>These two readings are all there is, and the omissions are not an oversight.</b> The request goes out
/// through the host's own <see cref="System.Net.Http.HttpClient"/>, which reports neither when DNS resolved,
/// nor when the socket connected, nor when the TLS handshake finished — nothing in this process can measure a
/// phase it does not own the handler for. A host mapping this onto a protocol that names those phases must
/// report them as <i>absent</i> rather than as zero: a duration of zero is a claim that the phase happened
/// instantly, and a phase this engine never saw did not happen here at all.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly record struct FetchTiming(DateTimeOffset SentAt, TimeSpan TimeToHeaders)
{
    /// <summary>
    /// Gets the wall-clock instant the hop's response headers were in, derived from <see cref="SentAt"/> and
    /// <see cref="TimeToHeaders"/>.
    /// </summary>
    public DateTimeOffset HeadersAt => SentAt + TimeToHeaders;
}

/// <summary>
/// What asked for a request.
/// </summary>
/// <remarks>
/// Only <see cref="Script"/>, <see cref="XmlHttpRequest"/> and <see cref="EventSource"/> are produced by the
/// engine itself; the rest name the callers a host layer above Jint adds, and new members may appear, so
/// switch with a default arm.
/// </remarks>
public enum FetchInitiator
{
    /// <summary>A <c>fetch()</c> call made by script.</summary>
    Script = 0,

    /// <summary>A request the host started rather than script — a document or subresource load.</summary>
    Host = 1,

    /// <summary>An <c>XMLHttpRequest</c> made by script.</summary>
    /// <remarks>
    /// Told apart from <see cref="Script"/> because a host reporting a request to a tool has to name the
    /// interface that asked for it: the Chrome DevTools Protocol has one resource type for <c>fetch()</c>
    /// and another for <c>XMLHttpRequest</c>, and nothing on the wire distinguishes the two.
    /// </remarks>
    XmlHttpRequest = 2,

    /// <summary>An <c>EventSource</c> connection made by script.</summary>
    /// <remarks>
    /// One per connection rather than one per <c>EventSource</c>: a reconnect is a second request with its
    /// own identifier, which is what it is on the wire and what a network panel shows. The Chrome DevTools
    /// Protocol has its own resource type for one, for the same reason <see cref="XmlHttpRequest"/> is told
    /// apart from <see cref="Script"/>.
    /// </remarks>
    EventSource = 3,
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

    /// <summary>
    /// Gets when the hop that produced this response went out and when its headers came back, or
    /// <see langword="null"/> for a response that never went on the wire.
    /// </summary>
    /// <remarks>
    /// <b><see langword="null"/> is itself information.</b> An observer's own
    /// <see cref="FetchInterception.Fulfill"/> answers a request without opening a socket, so there is no
    /// send to time and reporting a zero-length one would be a measurement of something that did not happen.
    /// Every response the network produced carries a timing — a redirect the loop walked past included, each
    /// hop timed on its own.
    /// </remarks>
    public FetchTiming? Timing { get; init; }
}

/// <summary>
/// An authentication challenge a server answered a request with, before it is delivered as a <c>401</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every challenge is reported, including the ones this engine cannot answer</b>, because the alternative
/// is the silence this seam exists to remove: an observer that is never asked cannot tell a scheme it does
/// not support from a server that never challenged. <see cref="CanProvideCredentials"/> is what separates
/// the two, and it is answered <i>before</i> the observer decides rather than after.
/// </para>
/// <para>
/// <b>Only a server challenge is reported.</b> A <c>407</c> is a proxy's, and the proxy belongs to the
/// <c>HttpClient</c> the host supplied — setting <c>Proxy-Authorization</c> under a handler that re-frames
/// the request is not this engine's to do — so <see cref="Source"/> is always <c>Server</c>. The name is
/// carried anyway, because it is the protocol's own and a client reads it.
/// </para>
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public sealed record ObservedFetchAuthChallenge
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObservedFetchAuthChallenge"/> record.
    /// </summary>
    public ObservedFetchAuthChallenge()
    {
    }

    /// <summary>Gets the identifier shared by every hop of the request that was challenged.</summary>
    public required FetchRequestId Id { get; init; }

    /// <summary>Gets the absolute URL of the hop that was challenged.</summary>
    public required Uri Url { get; init; }

    /// <summary>Gets the status the challenge arrived with, which is always <c>401</c>.</summary>
    public required int Status { get; init; }

    /// <summary>Gets who is challenging, which is always <c>Server</c> here.</summary>
    public string Source { get; init; } = "Server";

    /// <summary>
    /// Gets the authentication scheme, as the server spelled it — <c>Basic</c>, <c>Digest</c>,
    /// <c>Negotiate</c> and so on.
    /// </summary>
    public required string Scheme { get; init; }

    /// <summary>Gets the realm the challenge named, or the empty string when it named none.</summary>
    public string Realm { get; init; } = string.Empty;

    /// <summary>
    /// Whether this engine can turn a username and a password into an answer for <see cref="Scheme"/>.
    /// </summary>
    /// <remarks>
    /// <b>True only for <c>Basic</c>.</b> It is the one scheme whose answer is a function of the credentials
    /// alone — one <c>Authorization</c> header, no state carried between legs — whereas <c>Digest</c> needs
    /// a nonce exchange and <c>Negotiate</c> and <c>NTLM</c> need a handshake bound to the connection, and
    /// none of those is something a transport that hands the socket back after every response can hold.
    /// <b>An observer that answers <see cref="FetchAuthDecision.ProvideCredentials"/> when this is
    /// <see langword="false"/> has its answer refused and the <c>401</c> delivered</b>, which is deliberate:
    /// an ask that cannot be honoured must fail visibly, because an ask that silently does nothing is the
    /// defect this seam exists to remove. A host driving this through a protocol sees that refusal as an
    /// error on the command it answered with.
    /// </remarks>
    public required bool CanProvideCredentials { get; init; }
}

/// <summary>
/// What an observer answers an authentication challenge with.
/// </summary>
/// <remarks>
/// Build one with <see cref="ProvideCredentials"/> or <see cref="Cancel"/>; answering <see langword="null"/>
/// instead is the protocol's <c>Default</c> — the challenge is left alone and the <c>401</c> is delivered to
/// whoever asked for the resource.
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public sealed class FetchAuthDecision
{
    private FetchAuthDecision()
    {
    }

    internal bool HasCredentials { get; private init; }

    internal string Username { get; private init; } = string.Empty;

    internal string Password { get; private init; } = string.Empty;

    /// <summary>
    /// Answers the challenge and re-sends the request once with the credentials on it.
    /// </summary>
    /// <param name="username">The user name.</param>
    /// <param name="password">The password.</param>
    /// <remarks>
    /// <b>Exactly one retry.</b> If the re-sent request is challenged again, that second response is
    /// delivered rather than asked about: an observer has one credential to offer, so a second ask has
    /// nothing new to answer with and a loop is worse than a <c>401</c>.
    /// </remarks>
    public static FetchAuthDecision ProvideCredentials(string username, string password)
        => new()
        {
            HasCredentials = true,
            Username = username ?? string.Empty,
            Password = password ?? string.Empty,
        };

    /// <summary>
    /// Declines the challenge, which delivers the <c>401</c> unchanged.
    /// </summary>
    /// <remarks>
    /// The same outcome as answering <see langword="null"/>, and it exists so that a host mapping a protocol
    /// onto this seam has something to map <c>CancelAuth</c> to — the difference between "I decline" and "I
    /// am not interested" is worth keeping at the surface even where the bytes agree.
    /// </remarks>
    public static FetchAuthDecision Cancel() => new();
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
/// What an observer answers a <i>response</i> with when it does not want the response delivered as it came
/// off the wire.
/// </summary>
/// <remarks>
/// <para>
/// Build one with <see cref="Fulfill"/>, <see cref="Fail"/> or <see cref="Continue"/>; answering
/// <see langword="null"/> instead delivers the response as it is.
/// </para>
/// <para>
/// <b>It is asked once, for the response that ends the chain.</b> A redirect the loop is about to follow is
/// reported to <see cref="FetchObserver.OnResponse"/> and is not answerable: what it produces is the next
/// hop, and the hop is answerable through <see cref="FetchObserver.OnRequestAsync"/> already.
/// </para>
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public sealed class FetchResponseInterception
{
    private FetchResponseInterception()
    {
    }

    internal FetchInterceptionKind Kind { get; private init; }

    internal int Status { get; private init; }

    internal string? StatusText { get; private init; }

    internal IReadOnlyList<FetchHeader>? Headers { get; private init; }

    internal ReadOnlyMemory<byte> Body { get; private init; }

    internal string? Reason { get; private init; }

    /// <summary>
    /// Replaces the response with one of the observer's own; the bytes the server sent are discarded unread.
    /// </summary>
    /// <param name="status">The HTTP status code the caller will see.</param>
    /// <param name="headers">The response headers, or <see langword="null"/> for none.</param>
    /// <param name="body">The response body.</param>
    /// <param name="statusText">The reason phrase, or <see langword="null"/> for the empty string.</param>
    /// <remarks>
    /// The substitute is not a redirect however it is numbered: the chain has already ended, and the loop
    /// only follows what the network answered.
    /// </remarks>
    public static FetchResponseInterception Fulfill(
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
        };

    /// <summary>
    /// Fails the request, which the caller sees as the same failure a network error produces.
    /// </summary>
    /// <param name="reason">Why it failed, for the host's own logs; script never sees it.</param>
    public static FetchResponseInterception Fail(string reason)
        => new() { Kind = FetchInterceptionKind.Fail, Reason = reason ?? string.Empty };

    /// <summary>
    /// Delivers the response the server sent, with its status line or headers rewritten; every argument left
    /// <see langword="null"/> keeps what the response already had.
    /// </summary>
    /// <param name="status">A status code to report instead.</param>
    /// <param name="statusText">A reason phrase to report instead.</param>
    /// <param name="headers">The complete header list to report instead of the response's own.</param>
    /// <remarks>
    /// <b>The body is the one thing this cannot rewrite</b>, and that is the difference from
    /// <see cref="Fulfill"/>: the bytes have not been read yet and are still coming off the socket, so a
    /// rewritten header list describes a body the observer has not seen. Rewriting
    /// <c>Content-Length</c> here therefore describes the transfer rather than changing it.
    /// </remarks>
    public static FetchResponseInterception Continue(
        int? status = null,
        string? statusText = null,
        IReadOnlyList<FetchHeader>? headers = null)
        => new()
        {
            Kind = FetchInterceptionKind.Continue,
            Status = status ?? 0,
            StatusText = statusText,
            Headers = headers,
        };
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
/// <c>OnResponse</c>(the redirect) and <c>OnRequest</c>(the next hop) in turn, then <c>OnResponseAsync</c>
/// for the final response, then <c>OnData</c> per body chunk, then exactly one of <c>OnCompleted</c> and
/// <c>OnFailed</c>. Two of those may answer rather than merely watch — <c>OnRequestAsync</c> decides a hop
/// and <c>OnResponseAsync</c> decides the response that ends the chain.
/// </para>
/// <para>
/// <b>A notification that throws is ignored</b> — there is no engine thread to report it to and a transfer
/// must not depend on an observer. The two that <i>decide</i> are different: a throw from
/// <see cref="OnRequestAsync"/> or <see cref="OnResponseAsync"/> fails the fetch, because a decision that
/// failed must not silently become "continue".
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
    /// <remarks>
    /// A notification: it cannot change what the caller receives, and for the final response it arrives
    /// <i>after</i> <see cref="OnResponseAsync"/> has been asked — so what it carries is what the answer
    /// produced. <see cref="OnResponseAsync"/> is the one that can change it.
    /// </remarks>
    public virtual void OnResponse(ObservedFetchResponse response)
    {
    }

    /// <summary>
    /// Called once, for the response that ends the chain, before its body is read; answer
    /// <see langword="null"/> to deliver it unchanged.
    /// </summary>
    /// <param name="response">The response headers.</param>
    /// <param name="cancellationToken">Cancelled when the fetch is aborted or times out.</param>
    /// <returns>What to do with the response, or <see langword="null"/> to leave it alone.</returns>
    /// <remarks>
    /// <para>
    /// <b>This asks; <see cref="OnResponse"/> reports.</b> They are separate calls and both happen: the ask
    /// comes first, with the headers as the socket gave them, and the notification follows with whatever the
    /// answer produced. So an observer that only watches overrides <see cref="OnResponse"/> and is unaffected
    /// by this existing, and one that rewrote a status sees its own value in the notification afterwards.
    /// </para>
    /// <para>
    /// <b>It is asked for the final response only.</b> A redirect the loop is about to follow reaches
    /// <see cref="OnResponse"/> and is not answerable: what it produces is the next hop, and that hop is
    /// offered to <see cref="OnRequestAsync"/> already.
    /// </para>
    /// <para>
    /// <b>The whole fetch is still bounded</b> by <c>Options.WebApi.Fetch.Timeout</c>, so time spent here
    /// comes out of the request's own deadline — and the socket is open and the body unread while it is
    /// spent. Like <see cref="OnRequestAsync"/>, a throw here fails the fetch rather than becoming
    /// "continue": this is the call that was asked to decide.
    /// </para>
    /// </remarks>
    public virtual ValueTask<FetchResponseInterception?> OnResponseAsync(
        ObservedFetchResponse response,
        CancellationToken cancellationToken)
        => new((FetchResponseInterception?) null);

    /// <summary>
    /// Called when a server answers a hop with <c>401</c> and an authentication challenge; answer
    /// <see langword="null"/> to leave the challenge alone and let the <c>401</c> be delivered.
    /// </summary>
    /// <param name="challenge">What the server asked for.</param>
    /// <param name="cancellationToken">Cancelled when the fetch is aborted or times out.</param>
    /// <remarks>
    /// <para>
    /// <b>Asked per hop, beside <see cref="OnRequestAsync"/> rather than <see cref="OnResponseAsync"/>.</b>
    /// A <c>401</c> on a document fetch, on a subresource and on an <c>XMLHttpRequest</c> is exactly the case
    /// this is for, and only <c>fetch()</c> takes the lane <see cref="OnResponseAsync"/> is asked in — an ask
    /// placed there would have served the one caller that needed it least.
    /// </para>
    /// <para>
    /// <b>Answering with credentials re-sends that hop once</b>, carrying an <c>Authorization</c> header this
    /// engine builds. The retry is not a redirect and spends none of <c>MaxRedirects</c>; a challenge on the
    /// retry is delivered rather than asked about again. The header rides with the request from there on and
    /// is dropped if a later redirect crosses to another origin, which is
    /// <see href="https://fetch.spec.whatwg.org/#http-redirect-fetch">HTTP-redirect fetch</see> step 13 and
    /// costs nothing here because the transport already strips it.
    /// </para>
    /// <para>
    /// <b>Only <c>Basic</c> can be answered</b>, and
    /// <see cref="ObservedFetchAuthChallenge.CanProvideCredentials"/> says so before the decision is made.
    /// Every other scheme is still reported — being asked is how an observer tells "unsupported" from "never
    /// challenged" — and credentials offered for one are refused rather than quietly dropped.
    /// </para>
    /// <para>
    /// Like <see cref="OnRequestAsync"/> and <see cref="OnResponseAsync"/>, a throw here fails the fetch:
    /// this is a call that was asked to decide.
    /// </para>
    /// </remarks>
    public virtual ValueTask<FetchAuthDecision?> OnAuthRequiredAsync(
        ObservedFetchAuthChallenge challenge,
        CancellationToken cancellationToken)
        => new((FetchAuthDecision?) null);

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
