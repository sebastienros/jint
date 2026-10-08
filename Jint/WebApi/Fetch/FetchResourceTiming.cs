#if NET8_0_OR_GREATER
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Fetch;

/// <summary>Engine-free timing facts for https://fetch.spec.whatwg.org/#finalize-and-report-timing.</summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ResourceTimingInfo(
    string Name,
    string InitiatorType,
    double StartTime,
    double FetchStart,
    double ResponseStart,
    double ResponseEnd,
    string NextHopProtocol,
    long EncodedBodySize,
    long DecodedBodySize,
    int ResponseStatus,
    bool TimingAllowed,
    bool RenderBlocking,
    string ContentType,
    bool FromCache = false,
    bool Revalidated = false);

/// <summary>Collects transport facts without reading engine state or creating JavaScript values.</summary>
internal sealed class FetchResourceTiming
{
    private readonly TimeProvider _clock;
    private readonly long _started;
    private readonly string? _origin;
    private readonly bool _includeCredentials;
    private readonly Action<ResourceTimingInfo>? _report;
    private ResourceTimingInfo _info;
    private long? _encodedLength;
    private int _completed;

    internal FetchResourceTiming(TimeProvider clock, double startTime, string url, string initiatorType,
        string? origin, string credentials, bool renderBlocking = false, Action<ResourceTimingInfo>? report = null)
    {
        _clock = clock;
        _started = clock.GetTimestamp();
        TimeOrigin = clock.GetUtcNow();
        _origin = origin;
        _includeCredentials = string.Equals(credentials, JsRequest.CredentialsInclude, StringComparison.Ordinal);
        _report = report;
        _info = new ResourceTimingInfo(url, initiatorType, startTime, startTime, 0, 0, "", 0, 0, 0, true, renderBlocking, "");
    }

    internal DateTimeOffset TimeOrigin { get; }
    internal TimeSpan Elapsed => _clock.GetElapsedTime(_started);
    private double Now => _info.StartTime + Elapsed.TotalMilliseconds;
    internal ResourceTimingInfo? Result { get; private set; }

    internal void StartHop() => _info = _info with { FetchStart = Now };

    // The timing-allow check is sticky across redirects, including a chain returning to its first origin.
    // https://fetch.spec.whatwg.org/#timing-allow-check
    internal void Redirect(HttpResponseMessage response, UrlRecord url)
        => _info = _info with { TimingAllowed = _info.TimingAllowed && AllowsTiming(response, url) };

    internal void Response(FetchExchange exchange)
    {
        var response = exchange.Response;
        _encodedLength = response.Content.Headers.ContentLength;
        var version = response.Version;
        var protocol = exchange.FromInterception || exchange.FromCache ? "" : version.Major switch
        {
            1 => version.Minor == 0 ? "http/1.0" : "http/1.1",
            2 => "h2",
            3 => "h3",
            _ => "",
        };
        _info = _info with
        {
            ResponseStart = Now,
            FromCache = exchange.FromCache,
            Revalidated = exchange.Revalidated,
            ResponseStatus = (int) response.StatusCode,
            NextHopProtocol = protocol,
            TimingAllowed = _info.TimingAllowed && AllowsTiming(response, exchange.Url),
            ContentType = response.Content.Headers.ContentType?.MediaType ?? "",
        };
    }

    private bool AllowsTiming(HttpResponseMessage response, UrlRecord url)
    {
        if (_origin is not null && string.Equals(_origin, url.SerializeOrigin(), StringComparison.Ordinal))
        {
            return true;
        }

        if (response.Headers.TryGetValues("Timing-Allow-Origin", out var values))
        {
            foreach (var value in values)
            {
                foreach (var token in value.Split(','))
                {
                    var origin = token.Trim();
                    if (string.Equals(origin, _origin ?? "null", StringComparison.Ordinal)
                        || string.Equals(origin, "*", StringComparison.Ordinal) && !_includeCredentials)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    internal void Complete(long bodyLength, bool failed = false)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
        {
            return;
        }

        // Automatic decompression can hide the encoded length. In that case the bytes actually read
        // are the only available size; never substitute Content-Length for a HEAD/null response body.
        var info = _info with
        {
            ResponseEnd = Now,
            EncodedBodySize = bodyLength == 0 ? 0 : _encodedLength ?? bodyLength,
            DecodedBodySize = bodyLength,
            TimingAllowed = !failed && _info.TimingAllowed,
            ResponseStatus = failed ? 0 : _info.ResponseStatus,
        };
        Result = info;
        _report?.Invoke(info);
    }
}
#endif
