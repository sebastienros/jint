#if NET8_0_OR_GREATER
using System.Threading;
using Jint.WebApi.Fetch;

namespace Jint.WebApi.WebSockets;

/// <summary>
/// One socket's side of a <see cref="WebSocketObserver"/>: the identifier its four calls share, and the
/// callbacks wrapped so that an observer can neither break a connection nor be told twice that it closed.
/// </summary>
/// <remarks>
/// Created on the engine thread, called from transport threads, and — like everything else the transport
/// touches — engine-free: it holds the observer and a number and nothing else. The same shape
/// <c>FetchObservation</c> has, for the same reasons, and deliberately not the same object: a socket is not a
/// request (see <see cref="WebSocketId"/>).
/// </remarks>
internal sealed class WebSocketObservation
{
    private static long _lastId;

    private readonly WebSocketObserver _observer;
    private int _closed;

    private WebSocketObservation(WebSocketObserver observer)
    {
        _observer = observer;
        Id = new WebSocketId(Interlocked.Increment(ref _lastId));
    }

    /// <summary>
    /// The observation for one socket, or <see langword="null"/> when the host set no observer — which is
    /// what every call site checks first, so an unobserved engine allocates nothing.
    /// </summary>
    internal static WebSocketObservation? Create(WebSocketObserver? observer)
        => observer is null ? null : new WebSocketObservation(observer);

    internal WebSocketId Id { get; }

    /// <summary>The URL the socket was created for, so the handshake call needs no second copy of it.</summary>
    internal Uri? Url { get; private set; }

    internal void Created(Uri url)
    {
        Url = url;

        try
        {
            _observer.OnCreated(Id, url);
        }
        catch (Exception)
        {
            // See WebSocketObserver's remarks: a socket must not depend on an observer, and there is no
            // engine thread to report a failure to.
        }
    }

    internal void HandshakeRequest(IReadOnlyList<FetchHeader> headers, IReadOnlyList<string> protocols)
    {
        if (Url is not { } url)
        {
            return;
        }

        try
        {
            _observer.OnHandshakeRequest(new ObservedWebSocketHandshake
            {
                Id = Id,
                Url = url,
                Headers = headers,
                Protocols = protocols,
            });
        }
        catch (Exception)
        {
        }
    }

    internal void HandshakeResponse(int status, IReadOnlyList<FetchHeader> headers, string subProtocol)
    {
        try
        {
            _observer.OnHandshakeResponse(new ObservedWebSocketResponse
            {
                Id = Id,
                Status = status,
                Headers = headers,
                SubProtocol = subProtocol,
            });
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// The terminal call, enforced here rather than trusted to the call sites: a socket can end in the
    /// handshake, in the read loop and in an abandonment, and the compare-and-swap is what makes this fire
    /// exactly once between them.
    /// </summary>
    internal void Closed(int code, string reason, bool wasClean)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        try
        {
            _observer.OnClosed(Id, code, reason, wasClean);
        }
        catch (Exception)
        {
        }
    }
}
#endif
