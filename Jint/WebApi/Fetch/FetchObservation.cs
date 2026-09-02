#if NET8_0_OR_GREATER
using System.Threading;
using System.Threading.Tasks;

namespace Jint.WebApi.Fetch;

/// <summary>
/// One request's side of a <see cref="FetchObserver"/>: the identifier every hop shares, and the callbacks
/// wrapped so that an observer can neither break a transfer nor be told twice that it ended.
/// </summary>
/// <remarks>
/// <para>
/// Created on the engine thread, called from transport threads, and — like everything else the transport
/// touches — engine-free: it holds the observer and a number and nothing else.
/// </para>
/// <para>
/// <b>Terminality is enforced here rather than trusted to the call sites.</b> A request can fail in the
/// redirect loop, in the body stream and in the operation's own classification, and more than one of those
/// can happen for one request; the compare-and-swap is what makes <c>OnCompleted</c>/<c>OnFailed</c> fire
/// exactly once between them.
/// </para>
/// </remarks>
internal sealed class FetchObservation
{
    private static long _lastId;

    private readonly FetchObserver _observer;
    private int _terminated;

    private FetchObservation(FetchObserver observer, FetchInitiator initiator)
    {
        _observer = observer;
        Initiator = initiator;
        Id = new FetchRequestId(Interlocked.Increment(ref _lastId));

        var preview = 0;
        try
        {
            preview = observer.RequestBodyPreviewBytes;
        }
        catch (Exception)
        {
            // Read once, on the engine thread. A property that throws is treated as "no preview wanted"
            // rather than as a failed fetch, for the same reason a notification that throws is ignored.
        }

        RequestBodyPreviewBytes = preview < 0 ? 0 : preview;
    }

    /// <summary>
    /// The observation for one request, or <see langword="null"/> when the host set no observer — which is
    /// what every call site checks first, so an unobserved engine allocates nothing.
    /// </summary>
    internal static FetchObservation? Create(FetchObserver? observer, FetchInitiator initiator)
        => observer is null ? null : new FetchObservation(observer, initiator);

    internal FetchRequestId Id { get; }

    internal FetchInitiator Initiator { get; }

    internal int RequestBodyPreviewBytes { get; }

    /// <summary>
    /// Asks the observer what to do with one hop. Unlike every other callback here, a throw is <b>not</b>
    /// swallowed: this is the call that was asked to decide, and a decision that failed has to fail the
    /// fetch rather than silently become "continue".
    /// </summary>
    internal async Task<FetchInterception?> RequestAsync(ObservedFetchRequest request, CancellationToken cancellationToken)
    {
        var interception = await _observer.OnRequestAsync(request, cancellationToken).ConfigureAwait(false);
        return interception;
    }

    /// <summary>
    /// Reports the final response of an exchange whose body the caller reads itself, then — once those bytes
    /// are counted — <see cref="Completed"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only <c>FetchTransport.SendAsync</c> reports its own final response.</b> Every caller of
    /// <c>SendForStreamAsync</c> takes the exchange and reads the body itself, so the redirect loop has told
    /// the observer about the hops it walked past and nothing about the answer — and an observer owed a
    /// response it never receives shows the request as sent and never answered. Three callers pay that debt
    /// (<c>XhrOperation</c>, and the browser package's document and subresource fetches), and this is the one
    /// shape they pay it in.
    /// </para>
    /// <para>
    /// <paramref name="headers"/> is for a caller that has already collected them for its own use; left out,
    /// they are read from the exchange's response, every value of every header as its own entry.
    /// </para>
    /// </remarks>
    internal void FinalResponse(FetchExchange exchange, IReadOnlyList<FetchHeader>? headers = null)
    {
        var response = exchange.Response;

        Response(new ObservedFetchResponse
        {
            Id = Id,
            Url = exchange.RequestUri,
            Status = (int) response.StatusCode,
            StatusText = response.ReasonPhrase ?? "",
            Headers = headers ?? CollectHeaders(response),
            FromInterception = exchange.FromInterception,

            // The redirect loop is what reports a redirect; what an exchange carries is the answer at its end.
            IsRedirect = false,
        });
    }

    private static List<FetchHeader> CollectHeaders(System.Net.Http.HttpResponseMessage response)
    {
        var headers = new List<FetchHeader>();
        Collect(headers, response.Headers);
        Collect(headers, response.Content.Headers);
        return headers;

        static void Collect(List<FetchHeader> headers, System.Net.Http.Headers.HttpHeaders source)
        {
            foreach (var header in source.NonValidated)
            {
                foreach (var value in header.Value)
                {
                    headers.Add(new FetchHeader(HeaderList.Lowercase(header.Key), value));
                }
            }
        }
    }

    internal void Response(ObservedFetchResponse response)
    {
        try
        {
            _observer.OnResponse(response);
        }
        catch (Exception)
        {
            // See FetchObserver's remarks: there is no engine thread to report to and the transfer must not
            // depend on the observer.
        }
    }

    internal void Data(ReadOnlySpan<byte> chunk)
    {
        try
        {
            _observer.OnData(Id, chunk);
        }
        catch (Exception)
        {
        }
    }

    internal void Completed(long bodyLength)
    {
        if (Interlocked.Exchange(ref _terminated, 1) != 0)
        {
            return;
        }

        try
        {
            _observer.OnCompleted(Id, bodyLength);
        }
        catch (Exception)
        {
        }
    }

    internal void Failed(string reason, Exception? exception)
    {
        if (Interlocked.Exchange(ref _terminated, 1) != 0)
        {
            return;
        }

        try
        {
            _observer.OnFailed(Id, reason, exception);
        }
        catch (Exception)
        {
        }
    }
}
#endif
