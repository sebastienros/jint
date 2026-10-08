#if NET8_0_OR_GREATER
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Jint.WebApi.Fetch;

// A private cache. RFC 9111 §§3–4. Only complete 200 GET representations are stored;
// redirects, HEAD, ranges, caller conditions and streaming responses pass through.
internal sealed partial class HttpResponseCache : IDisposable
{
    private readonly System.Threading.Lock _gate = new();
    private readonly Dictionary<string, List<Entry>> _entries = new(StringComparer.Ordinal);
    private static readonly string[] _credentialHeaders = ["Cookie", "Authorization", "Origin"];
    private static readonly string[] _invalidationHeaders = ["Location", "Content-Location"];
    private readonly TimeProvider _clock;
    private readonly HttpClient? _client;
    private readonly long _maxBytes;
    private readonly int _maxEntries;
    private readonly int _maxEntryBytes;
    private long _bytes;
    private int _count;
    private long _reserved;
    private long _sequence;
    private long _generation;
    private bool _disposed;

    internal HttpResponseCache(long maxBytes, int maxEntries, int maxEntryBytes, TimeProvider clock,
        string? directory = null, bool temporary = false, HttpClient? client = null)
    {
        _maxBytes = maxBytes;
        _maxEntries = maxEntries;
        _maxEntryBytes = maxEntryBytes;
        _clock = clock;
        _client = client;
        OpenDisk(directory, temporary);
    }

    internal Task<CacheResult> SendAsync(HttpClient client, HttpRequestMessage request, string partition,
        string mode, bool hasRequestBody, bool allowCache, CancellationToken cancellationToken)
        => SendAsync(request, partition, mode,
            (message, token) => client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token),
            cancellationToken, hasRequestBody, allowCache && (_client is null || ReferenceEquals(_client, client)));

    internal async Task<CacheResult> SendAsync(HttpRequestMessage request, string partition, string mode,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send, CancellationToken cancellationToken,
        bool hasRequestBody = false, bool allowCache = true)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = Key(request.RequestUri!);
        var requestHeaders = Headers(request);
        var directives = request.Headers.CacheControl;
        var eligible = allowCache && request.Method == HttpMethod.Get && !hasRequestBody && request.RequestUri!.UserInfo.Length == 0
            && !request.Headers.Contains("Range") && !request.Headers.Contains("If-Range")
            && !request.Headers.Contains("If-None-Match") && !request.Headers.Contains("If-Modified-Since")
            && !request.Headers.Contains("If-Match") && !request.Headers.Contains("If-Unmodified-Since");
        var noStore = mode is "no-store" || directives?.NoStore == true;
        var reload = mode is "reload" || noStore;
        var onlyCached = mode is "only-if-cached" || directives?.OnlyIfCached == true;
        Entry? stored = null;
        long generation;
        lock (_gate)
        {
            generation = _generation;
            if (!_disposed && eligible && !noStore && _entries.TryGetValue(key, out var variants))
            {
                stored = variants.LastOrDefault(e => e.Matches(partition, requestHeaders));
                if (stored is not null) stored.Used = ++_sequence;
            }
        }

        if (stored is not null && !reload)
        {
            var age = stored.Age(_clock.GetUtcNow());
            var fresh = age < stored.Lifetime && !stored.NoCache;
            var requestValidates = mode is "no-cache" || directives?.NoCache == true
                || request.Headers.Contains("Cache-Control") && directives is null
                || directives?.MaxAge is { } maxAge && age > maxAge
                || directives?.MinFresh is { } minFresh && stored.Lifetime - age < minFresh
                || directives is null && request.Headers.Pragma.Any(p => p.Name.Equals("no-cache", StringComparison.OrdinalIgnoreCase));
            var mayUseStale = mode is "force-cache" or "only-if-cached" || directives?.MaxStale == true
                && (directives.MaxStaleLimit is null || age - stored.Lifetime <= directives.MaxStaleLimit);
            if (!requestValidates && (fresh || mayUseStale && !stored.MustRevalidate && !stored.NoCache))
                return new CacheResult(stored.Response(_clock.GetUtcNow()), true, false);
        }
        if (onlyCached)
            throw new FetchFailureException(FetchFailureKind.Network, "No matching reusable HTTP cache entry.");

        var validates = stored is not null && !reload && (stored.ETag is not null || stored.LastModified is not null);
        if (validates)
        {
            if (stored!.ETag is { } tag) request.Headers.TryAddWithoutValidation("If-None-Match", tag);
            if (stored.LastModified is { } modified) request.Headers.TryAddWithoutValidation("If-Modified-Since", modified);
        }
        var sent = _clock.GetUtcNow();
        var response = await send(request, cancellationToken).ConfigureAwait(false);
        var received = _clock.GetUtcNow();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head
                && request.Method != HttpMethod.Options && request.Method != HttpMethod.Trace
                && (int) response.StatusCode is >= 200 and < 400)
            {
                Invalidate(key);
                foreach (var name in _invalidationHeaders)
                {
                    if (Get(response, name) is { } location && Uri.TryCreate(request.RequestUri, location, out var target)
                        && string.Equals(target.GetLeftPart(UriPartial.Authority), request.RequestUri!.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal))
                        Invalidate(Key(target));
                }
            }
            if (validates && response.StatusCode == HttpStatusCode.NotModified)
            {
                // RFC 9111 §4.3.4: use the retained body, replace supplied end-to-end metadata.
                // An incompatible validator is not proof that this stored representation is current.
                if (Get(response, "ETag") is { } returnedTag && stored!.ETag is { } originalTag
                    && !string.Equals(returnedTag.StartsWith("W/", StringComparison.Ordinal) ? returnedTag[2..] : returnedTag,
                        originalTag.StartsWith("W/", StringComparison.Ordinal) ? originalTag[2..] : originalTag, StringComparison.Ordinal))
                    throw new FetchFailureException(FetchFailureKind.Network, "A 304 response changed the cache validator.");
                var merged = stored!.Response(received);
                merged.Version = response.Version;
                var connectionFields = Get(response, "Connection")?.Split(',').Select(n => n.Trim()).ToArray() ?? [];
                foreach (var header in Headers(response))
                {
                    if (connectionFields.Contains(header.Key, StringComparer.OrdinalIgnoreCase) || Excluded(header.Key) || header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
                    if (merged.Headers.NonValidated.Contains(header.Key)) merged.Headers.Remove(header.Key);
                    if (merged.Content.Headers.NonValidated.Contains(header.Key)) merged.Content.Headers.Remove(header.Key);
                    Add(merged, header.Key, header.Value);
                }
                if (!response.Headers.Contains("Date")) merged.Headers.Date = received;
                // The old Age describes the old response, not this new validation.
                merged.Headers.Remove("Age");
                if (response.Headers.Age is { } newAge) merged.Headers.Age = newAge;
                var updated = Entry.Create(key, partition, requestHeaders, merged, sent, received, stored.Body);
                lock (_gate)
                {
                    if (generation == _generation && !_disposed)
                    {
                        Remove(stored);
                        if (updated is not null) Store(updated);
                    }
                }
                // Only this validation's cookies travel to the jar; stored Set-Cookie was discarded.
                if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
                    merged.Headers.TryAddWithoutValidation("Set-Cookie", cookies);
                response.Dispose();
                return new CacheResult(merged, false, true);
            }
            if (eligible && !noStore && response.StatusCode == HttpStatusCode.OK)
            {
                var candidate = Entry.Create(key, partition, requestHeaders, response, sent, received, []);
                var captureLimit = (int) Math.Min(_maxEntryBytes, response.Content.Headers.ContentLength ?? _maxEntryBytes);
                var reservationSize = (candidate?.Size ?? 0) + 2L * captureLimit;
                if (candidate is not null && (response.Content.Headers.ContentLength is not { } length || length <= _maxEntryBytes)
                    && Reserve(reservationSize))
                {
                    response.Content = new CacheCaptureContent(response.Content, captureLimit, bytes =>
                    {
                        lock (_gate)
                        {
                            _reserved -= reservationSize;
                            if (bytes is not null && !_disposed && generation == _generation)
                            {
                                candidate.Body = bytes;
                                Store(candidate);
                            }
                        }
                    });
                }
                else if (stored is not null)
                {
                    lock (_gate) Remove(stored);
                }
            }
            return new CacheResult(response, false, false);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private bool Reserve(long size)
    {
        lock (_gate)
        {
            if (_disposed || size > _maxBytes - _reserved) return false;
            while (_bytes > _maxBytes - _reserved - size)
            {
                var oldest = _entries.Values.SelectMany(v => v).MinBy(e => e.Used);
                if (oldest is null) return false;
                Remove(oldest);
            }
            _reserved += size;
            return true;
        }
    }

    private void Store(Entry entry)
    {
        var size = entry.Size;
        if (size > _maxBytes - _reserved || entry.Body.Length > _maxEntryBytes) return;
        if (_entries.TryGetValue(entry.Key, out var variants))
            foreach (var previous in variants.ToArray())
                if (string.Equals(previous.Partition, entry.Partition, StringComparison.Ordinal) && previous.Matches(entry.Partition, entry.RequestHeaders)) Remove(previous);
        while (_count >= _maxEntries || _bytes > _maxBytes - _reserved - size)
        {
            var oldest = _entries.Values.SelectMany(v => v).MinBy(e => e.Used);
            if (oldest is null) return;
            Remove(oldest);
        }
        if (!_entries.TryGetValue(entry.Key, out variants)) _entries.Add(entry.Key, variants = []);
        entry.Used = ++_sequence;
        variants.Add(entry);
        _count++;
        _bytes += size;
        WriteDisk(entry);
    }

    private void Remove(Entry entry)
    {
        if (!_entries.TryGetValue(entry.Key, out var variants) || !variants.Contains(entry)) return;
        // Keep a failed deletion reachable so a later Clear cannot report success while leaving it on disk.
        DeleteDisk(entry);
        variants.Remove(entry);
        _bytes -= entry.Size;
        _count--;
        if (variants.Count == 0) _entries.Remove(entry.Key);
    }

    private void Invalidate(string key)
    {
        lock (_gate)
        {
            // Retire in-flight captures too: an unsafe request must not be undone by an older GET.
            _generation++;
            if (_entries.TryGetValue(key, out var entries)) foreach (var entry in entries.ToArray()) Remove(entry);
        }
    }

    internal void Clear()
    {
        lock (_gate)
        {
            _generation++;
            foreach (var entry in _entries.Values.SelectMany(v => v).ToArray()) Remove(entry);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _generation++;
            _entries.Clear();
            _bytes = 0;
            _count = 0;
            CloseDisk();
        }
    }

    private static string Key(Uri uri) => uri.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped);
    private static Dictionary<string, string[]> Headers(HttpRequestMessage request)
        => request.Headers.Concat(request.Content?.Headers.AsEnumerable() ?? []).ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, string[]> Headers(HttpResponseMessage response)
        => response.Headers.Concat(response.Content.Headers).ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    private static string? Get(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values)
            ? string.Join(", ", values) : null;
    private static void Add(HttpResponseMessage response, string name, IEnumerable<string> values)
    {
        if (!response.Headers.TryAddWithoutValidation(name, values)) response.Content.Headers.TryAddWithoutValidation(name, values);
    }
    private static bool Excluded(string name) => name.ToLowerInvariant() is "set-cookie" or "connection" or "keep-alive"
        or "proxy-authenticate" or "proxy-authorization" or "te" or "trailer" or "transfer-encoding" or "upgrade";

    internal sealed class Entry
    {
        internal string Key { get; set; } = "";
        internal string Partition { get; set; } = "";
        internal Dictionary<string, string[]> RequestHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, string[]> ResponseHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        internal string[] Vary { get; set; } = [];
        internal byte[] Body { get; set; } = [];
        internal string Reason { get; set; } = "OK";
        internal DateTimeOffset Received { get; set; }
        internal double InitialAgeSeconds { get; set; }
        internal double LifetimeSeconds { get; set; }
        internal bool NoCache { get; set; }
        internal bool MustRevalidate { get; set; }
        internal string? ETag { get; set; }
        internal string? LastModified { get; set; }
        internal long Used { get; set; }
        internal string? FileName { get; set; }
        internal TimeSpan Lifetime => TimeSpan.FromSeconds(LifetimeSeconds);
        internal long Size => Body.LongLength + 3L * (Key.Length + Partition.Length + Reason.Length
            + (ETag?.Length ?? 0) + (LastModified?.Length ?? 0) + Vary.Sum(v => v.Length)
            + RequestHeaders.Sum(h => h.Key.Length + h.Value.Sum(v => v.Length))
            + ResponseHeaders.Sum(h => h.Key.Length + h.Value.Sum(v => v.Length)))
            + 128L * (RequestHeaders.Count + ResponseHeaders.Count + Vary.Length)
            + 32L * (RequestHeaders.Sum(h => h.Value.Length) + ResponseHeaders.Sum(h => h.Value.Length)) + 512;
        internal TimeSpan Age(DateTimeOffset now) => TimeSpan.FromSeconds(Math.Min(int.MaxValue,
            InitialAgeSeconds + Math.Max(0, (now - Received).TotalSeconds)));
        internal bool Matches(string partition, Dictionary<string, string[]> headers)
        {
            if (!string.Equals(Partition, partition, StringComparison.Ordinal)) return false;
            foreach (var name in Vary.Concat(_credentialHeaders))
            {
                var a = RequestHeaders.GetValueOrDefault(name);
                var b = headers.GetValueOrDefault(name);
                if (a is null ? b is not null : b is null || !a.SequenceEqual(b, StringComparer.Ordinal)) return false;
            }
            return true;
        }
        internal HttpResponseMessage Response(DateTimeOffset now)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Body),
                ReasonPhrase = Reason,
            };
            foreach (var header in ResponseHeaders) Add(response, header.Key, header.Value);
            response.Headers.Age = TimeSpan.FromSeconds(Math.Floor(Age(now).TotalSeconds));
            return response;
        }
        internal static Entry? Create(string key, string partition, Dictionary<string, string[]> request,
            HttpResponseMessage response, DateTimeOffset sent, DateTimeOffset received, byte[] body)
        {
            var cc = response.Headers.CacheControl;
            if (cc?.NoStore == true || response.Headers.Contains("Cache-Control") && cc is null
                || response.Content.Headers.ContentRange is not null
                || response.Content.Headers.ContentType?.MediaType is "text/event-stream") return null;
            var vary = response.Headers.Vary.SelectMany(v => v.Split(',')).Select(v => v.Trim()).ToArray();
            if (vary.Contains("*")) return null;
            var headers = Headers(response);
            var connection = Get(response, "Connection")?.Split(',').Select(n => n.Trim()).ToArray() ?? [];
            foreach (var name in headers.Keys.ToArray()) if (Excluded(name) || connection.Contains(name, StringComparer.OrdinalIgnoreCase)) headers.Remove(name);
            var date = response.Headers.Date ?? received;
            // RFC 9110 §6.6.1: a cache with a clock supplies a missing or invalid Date.
            if (response.Headers.Date is null) headers["Date"] = [date.ToString("r", CultureInfo.InvariantCulture)];
            var ageValue = 0d;
            if (Get(response, "Age") is { } ageHeader)
                ageValue = double.TryParse(ageHeader, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedAge)
                    ? Math.Min(int.MaxValue, parsedAge) : int.MaxValue;
            var age = Math.Max(Math.Max(0, (received - date).TotalSeconds),
                ageValue + Math.Max(0, (received - sent).TotalSeconds));
            // No heuristic freshness: absent or invalid expiry is immediately stale.
            var lifetime = cc?.MaxAge?.TotalSeconds
                ?? (response.Content.Headers.Expires is { } expires ? Math.Max(0, (expires - date).TotalSeconds) : 0);
            return new Entry
            {
                Key = key,
                Partition = partition,
                RequestHeaders = request,
                ResponseHeaders = headers,
                Vary = vary,
                Body = body,
                Reason = response.ReasonPhrase ?? "",
                Received = received,
                InitialAgeSeconds = Math.Min(int.MaxValue, age),
                LifetimeSeconds = Math.Min(int.MaxValue, lifetime),
                NoCache = cc?.NoCache == true,
                MustRevalidate = cc?.MustRevalidate == true,
                ETag = Get(response, "ETag"),
                LastModified = Get(response, "Last-Modified"),
            };
        }
    }
}

[StructLayout(LayoutKind.Auto)]
internal readonly record struct CacheResult(HttpResponseMessage Response, bool FromCache, bool Revalidated);
#endif
