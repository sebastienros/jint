#if NET8_0_OR_GREATER
#nullable enable
using System.Net;
using System.Net.Http;
using Jint.WebApi.Fetch;

namespace Jint.Tests.Runtime.WebApi;

public sealed class HttpResponseCacheTests
{
    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly Clock _clock = new();
    private int _requests;
    private HttpResponseCache Cache(int entries = 10, int entryBytes = 1024, long bytes = 65536, string? directory = null, bool temporary = false)
        => new(bytes, entries, entryBytes, _clock, directory, temporary);

    private HttpResponseMessage Response(string cacheControl = "max-age=60", string body = "first", HttpStatusCode status = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body) };
        response.Headers.Date = _clock.Now;
        if (cacheControl.Length > 0) response.Headers.TryAddWithoutValidation("Cache-Control", cacheControl);
        return response;
    }

    private async Task<(string Body, bool Hit, bool Validated)> Get(HttpResponseCache cache, string mode = "default", string url = "https://example.org/a",
        string partition = "site|include", Action<HttpRequestMessage>? configure = null,
        Func<HttpRequestMessage, HttpResponseMessage>? response = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        configure?.Invoke(request);
        var result = await cache.SendAsync(request, partition, mode, (r, _) =>
        {
            _requests++;
            return Task.FromResult(response?.Invoke(r) ?? Response());
        }, CancellationToken.None);
        using (result.Response)
            return (await result.Response.Content.ReadAsStringAsync(), result.FromCache, result.Revalidated);
    }

    [Test]
    public async Task FreshResponsesHaveIndependentReadersAndAnUpdatedAge()
    {
        using var cache = Cache();
        (await Get(cache)).Hit.Should().BeFalse();
        _clock.Now += TimeSpan.FromSeconds(20);
        (await Get(cache, url: "https://EXAMPLE.org:443/a#fragment")).Should().Be(("first", true, false));
        _requests.Should().Be(1);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.org/a");
        var a = await cache.SendAsync(request, "site|include", "default", (_, _) => throw new AssertionException("Unexpected network"), CancellationToken.None);
        var b = await cache.SendAsync(request, "site|include", "default", (_, _) => throw new AssertionException("Unexpected network"), CancellationToken.None);
        using (a.Response)
        using (b.Response)
        {
            a.Response.Headers.Age.Should().Be(TimeSpan.FromSeconds(20));
            var one = await a.Response.Content.ReadAsStreamAsync();
            var two = await b.Response.Content.ReadAsStreamAsync();
            one.ReadByte().Should().Be((int) 'f');
            two.ReadByte().Should().Be((int) 'f');
        }
    }

    [Test]
    public async Task UrlCredentialsNeverShareAStoredRepresentation()
    {
        using var cache = Cache();
        foreach (var url in new[] { "https://alice:secret@example.org/a", "https://bob:secret@example.org/a", "https://example.org/a" })
            (await Get(cache, url: url)).Hit.Should().BeFalse();
        _requests.Should().Be(3);
        (await Get(cache)).Hit.Should().BeTrue();
    }

    [TestCase("ETag", "\"one\"", "If-None-Match")]
    [TestCase("Last-Modified", "Wed, 31 Dec 2025 00:00:00 GMT", "If-Modified-Since")]
    public async Task StaleResponsesAreConditionallyValidatedAndRetainTheirBody(string validator, string value, string condition)
    {
        using var cache = Cache();
        await Get(cache, response: _ => { var r = Response("max-age=1"); if (!r.Headers.TryAddWithoutValidation(validator, value)) r.Content.Headers.TryAddWithoutValidation(validator, value); return r; });
        _clock.Now += TimeSpan.FromSeconds(2);
        var validated = await Get(cache, response: request =>
        {
            request.Headers.GetValues(condition).Single().Should().Be(value);
            var r = Response("max-age=120", "", HttpStatusCode.NotModified);
            r.Headers.TryAddWithoutValidation("X-Updated", "yes");
            return r;
        });
        validated.Should().Be(("first", false, true));
        _clock.Now += TimeSpan.FromSeconds(10);
        (await Get(cache)).Hit.Should().BeTrue();
        _requests.Should().Be(2);
    }

    [Test]
    public async Task RevalidationCanReplaceTheBodyWithAChanged200()
    {
        using var cache = Cache();
        await Get(cache, response: _ => { var r = Response("max-age=0"); r.Headers.ETag = new("\"one\""); return r; });
        (await Get(cache, response: _ => Response(body: "second"))).Body.Should().Be("second");
        (await Get(cache)).Body.Should().Be("second");
        _requests.Should().Be(2);
    }

    [TestCase("no-store, max-age=60", false)]
    [TestCase("no-cache, max-age=60", false)]
    [TestCase("must-revalidate, max-age=60", true)]
    [TestCase("private, max-age=60, s-maxage=0", true)]
    [TestCase("public, max-age=60", true)]
    [TestCase("s-maxage=60", false)]
    public async Task PrivateCacheRespectsResponseDirectives(string directive, bool hit)
    {
        using var cache = Cache();
        await Get(cache, response: _ => Response(directive));
        (await Get(cache)).Hit.Should().Be(hit);
        _requests.Should().Be(hit ? 1 : 2);
    }

    [Test]
    public async Task AgeIncludesApparentAgeResponseDelayAndResidence()
    {
        using var cache = Cache();
        await Get(cache, response: _ =>
        {
            var r = Response("max-age=60");
            r.Headers.Date = _clock.Now - TimeSpan.FromSeconds(20);
            r.Headers.Age = TimeSpan.FromSeconds(30);
            _clock.Now += TimeSpan.FromSeconds(10);
            return r;
        });
        _clock.Now += TimeSpan.FromSeconds(19);
        (await Get(cache)).Hit.Should().BeTrue();
        _clock.Now += TimeSpan.FromSeconds(1);
        (await Get(cache)).Hit.Should().BeFalse();
    }

    [Test]
    public async Task MissingDateIsSuppliedByTheCacheClock()
    {
        using var cache = Cache();
        await Get(cache, response: _ => { var r = Response(); r.Headers.Date = null; return r; });
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.org/a");
        var hit = await cache.SendAsync(request, "site|include", "default", (_, _) => throw new AssertionException("Unexpected network"), CancellationToken.None);
        using (hit.Response) hit.Response.Headers.Date.Should().Be(_clock.Now);
    }

    [Test]
    public async Task ExpiresIsRelativeToDateAndMissingFreshnessHasNoHeuristic()
    {
        using var cache = Cache();
        await Get(cache, response: _ => { var r = Response(""); r.Content.Headers.Expires = _clock.Now + TimeSpan.FromSeconds(10); return r; });
        (await Get(cache)).Hit.Should().BeTrue();
        _clock.Now += TimeSpan.FromSeconds(10);
        (await Get(cache, response: _ => Response(""))).Hit.Should().BeFalse();
        (await Get(cache)).Hit.Should().BeFalse();
    }

    [TestCase("reload", false)]
    [TestCase("no-store", false)]
    [TestCase("no-cache", false)]
    [TestCase("force-cache", true)]
    [TestCase("only-if-cached", true)]
    [TestCase("default", false)]
    public async Task CacheModesControlStaleReuse(string mode, bool hit)
    {
        using var cache = Cache();
        await Get(cache, response: _ => Response("max-age=0"));
        (await Get(cache, mode)).Hit.Should().Be(hit);
    }

    [Test]
    public async Task OnlyIfCachedNeverSendsOnMissAndMustRevalidateForbidsStaleReuse()
    {
        using var cache = Cache();
        await Assert.ThrowsAsync<FetchFailureException>(async () => await Get(cache, "only-if-cached"));
        _requests.Should().Be(0);
        await Get(cache, response: _ => Response("max-age=0, must-revalidate"));
        await Assert.ThrowsAsync<FetchFailureException>(async () => await Get(cache, "only-if-cached"));
        _requests.Should().Be(1);
        (await Get(cache, "force-cache")).Hit.Should().BeFalse();
    }

    [TestCase("no-cache")]
    [TestCase("no-store")]
    [TestCase("max-age=0")]
    [TestCase("min-fresh=100")]
    public async Task RequestDirectivesOverrideFreshness(string directive)
    {
        using var cache = Cache();
        await Get(cache);
        _clock.Now += TimeSpan.FromSeconds(1);
        (await Get(cache, configure: r => r.Headers.TryAddWithoutValidation("Cache-Control", directive))).Hit.Should().BeFalse();
    }

    [Test]
    public async Task RequestFreshnessBoundariesIncludeTheSpecifiedAgeAndRemainingLifetime()
    {
        using var cache = Cache();
        await Get(cache);
        _clock.Now += TimeSpan.FromSeconds(10);
        (await Get(cache, configure: r => r.Headers.TryAddWithoutValidation("Cache-Control", "max-age=10, min-fresh=50"))).Hit.Should().BeTrue();
        _clock.Now += TimeSpan.FromSeconds(1);
        (await Get(cache, configure: r => r.Headers.TryAddWithoutValidation("Cache-Control", "max-age=10"))).Hit.Should().BeFalse();
    }

    [Test]
    public async Task VaryAndCredentialsAndPartitionsArePartOfMatching()
    {
        using var cache = Cache();
        HttpResponseMessage Vary(HttpRequestMessage _) { var r = Response(); r.Headers.Vary.Add("Accept-Language"); return r; }
        await Get(cache, configure: r => r.Headers.Add("Accept-Language", "en"), response: Vary);
        (await Get(cache, configure: r => r.Headers.Add("Accept-Language", "fr"), response: Vary)).Hit.Should().BeFalse();
        (await Get(cache, configure: r => r.Headers.Add("accept-language", "en"))).Hit.Should().BeTrue();
        (await Get(cache, configure: r => { r.Headers.Add("Accept-Language", "en"); r.Headers.Add("Cookie", "session=other"); })).Hit.Should().BeFalse();
        (await Get(cache, partition: "different", configure: r => r.Headers.Add("Accept-Language", "en"))).Hit.Should().BeFalse();
        using var other = Cache();
        (await Get(other)).Hit.Should().BeFalse();
    }

    [Test]
    public async Task VaryStarAndSetCookieAreNeverReplayed()
    {
        using var cache = Cache();
        await Get(cache, response: _ => { var r = Response(); r.Headers.Vary.Add("*"); return r; });
        (await Get(cache, response: _ => { var r = Response(); r.Headers.Add("Set-Cookie", "session=one"); return r; })).Hit.Should().BeFalse();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.org/a");
        var hit = await cache.SendAsync(request, "site|include", "default", (_, _) => throw new AssertionException("Unexpected network"), CancellationToken.None);
        using (hit.Response) hit.Response.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [Test]
    public async Task ValidationCookiesAreAppliedOnceAndHopHeadersAreDiscarded()
    {
        using var cache = Cache();
        await Get(cache, response: _ => { var r = Response("max-age=0"); r.Headers.ETag = new("\"one\""); return r; });
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.org/a");
        var validated = await cache.SendAsync(request, "site|include", "default", (_, _) =>
        {
            var r = Response("max-age=60", "", HttpStatusCode.NotModified);
            r.Headers.Add("Set-Cookie", "session=new");
            r.Headers.Connection.Add("X-Hop");
            r.Headers.Add("X-Hop", "secret");
            return Task.FromResult(r);
        }, CancellationToken.None);
        using (validated.Response)
        {
            validated.Response.Headers.GetValues("Set-Cookie").Single().Should().Be("session=new");
            validated.Response.Headers.Contains("X-Hop").Should().BeFalse();
        }
        using var nextRequest = new HttpRequestMessage(HttpMethod.Get, "https://example.org/a");
        var hit = await cache.SendAsync(nextRequest, "site|include", "default", (_, _) => throw new AssertionException("Unexpected network"), CancellationToken.None);
        using (hit.Response) hit.Response.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [Test]
    public async Task AuthorizationChangesNeverReuseAnotherCredentialsRepresentation()
    {
        using var cache = Cache();
        await Get(cache, configure: r => r.Headers.Add("Authorization", "Bearer first"));
        (await Get(cache, configure: r => r.Headers.Add("Authorization", "Bearer second"))).Hit.Should().BeFalse();
        (await Get(cache, configure: r => r.Headers.Add("authorization", "Bearer first"))).Hit.Should().BeTrue();
        (await Get(cache)).Hit.Should().BeFalse();
    }

    [Test]
    public async Task CallerConditionsArePreservedAndNeverMergedWithAnUnrelatedStoredBody()
    {
        using var cache = Cache();
        await Get(cache, response: _ => { var r = Response("max-age=0"); r.Headers.ETag = new("\"one\""); return r; });
        var result = await Get(cache, configure: r => r.Headers.Add("If-None-Match", "\"caller\""), response: r =>
        {
            r.Headers.GetValues("If-None-Match").Single().Should().Be("\"caller\"");
            return Response("", "", HttpStatusCode.NotModified);
        });
        result.Should().Be(("", false, false));
    }

    [TestCase("HEAD", HttpStatusCode.OK)]
    [TestCase("GET", HttpStatusCode.PartialContent)]
    [TestCase("GET", HttpStatusCode.MovedPermanently)]
    public async Task UnsupportedMethodsAndStatusesAreNotStored(string method, HttpStatusCode status)
    {
        using var cache = Cache();
        for (var i = 0; i < 2; i++)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), "https://example.org/a");
            var result = await cache.SendAsync(request, "site", "default", (_, _) => { _requests++; return Task.FromResult(Response(status: status)); }, CancellationToken.None);
            using (result.Response) await result.Response.Content.ReadAsByteArrayAsync();
            result.FromCache.Should().BeFalse();
        }
        _requests.Should().Be(2);
    }

    [Test]
    public async Task UnsafeSuccessInvalidatesAllVariantsAndSameOriginLocations()
    {
        using var cache = Cache();
        await Get(cache);
        await Get(cache, url: "https://example.org/b");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.org/a");
        var result = await cache.SendAsync(request, "site", "default", (_, _) =>
        {
            var r = Response(); r.Headers.Location = new("/b", UriKind.Relative); return Task.FromResult(r);
        }, CancellationToken.None);
        result.Response.Dispose();
        (await Get(cache)).Hit.Should().BeFalse();
        (await Get(cache, url: "https://example.org/b")).Hit.Should().BeFalse();
    }

    [Test]
    public async Task EntryAndByteLimitsEvictAndOversizeResponsesPassThrough()
    {
        using var cache = Cache(entries: 1, entryBytes: 16, bytes: 8192);
        await Get(cache);
        await Get(cache, url: "https://example.org/b");
        (await Get(cache)).Hit.Should().BeFalse();
        await Get(cache, response: _ => Response(body: new string('x', 17)), mode: "reload");
        (await Get(cache)).Hit.Should().BeFalse();
        using var tiny = Cache(bytes: 16);
        await Get(tiny);
        (await Get(tiny)).Hit.Should().BeFalse();
    }

    [Test]
    public async Task AbandonedAndClearedAndCancelledCapturesAreNotPublished()
    {
        using var cache = Cache();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.org/a");
        var result = await cache.SendAsync(request, "site|include", "default", (_, _) => Task.FromResult(Response()), CancellationToken.None);
        result.Response.Dispose();
        (await Get(cache)).Hit.Should().BeFalse();
        cache.Clear();
        result = await cache.SendAsync(request, "site|include", "default", (_, _) => Task.FromResult(Response()), CancellationToken.None);
        cache.Clear();
        using (result.Response) await result.Response.Content.ReadAsByteArrayAsync();
        (await Get(cache)).Hit.Should().BeFalse();
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await cache.SendAsync(request, "site", "default", (_, _) => throw new AssertionException("Unexpected send"), cancelled.Token));
    }

    [TestCase(null)]
    [TestCase(2)]
    public async Task UnknownAndMisleadingLengthsDoNotPublishAnOversizeBody(int? declared)
    {
        using var cache = Cache(entryBytes: 8);
        var result = await Get(cache, response: _ =>
        {
            var r = Response();
            r.Content = new StreamContent(new NonSeekingStream(new byte[16]));
            if (declared is { } length) r.Content.Headers.ContentLength = length;
            return r;
        });
        result.Body.Length.Should().Be(16);
        (await Get(cache)).Hit.Should().BeFalse();
    }

    [Test]
    public async Task CancellingOneReaderLeavesOtherRequestsIndependent()
    {
        using var cache = Cache();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.org/a");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = await cache.SendAsync(request, "site|include", "default", (_, _) =>
        {
            var r = Response(); r.Content = new StreamContent(new NonSeekingStream(new byte[16], gate.Task)); return Task.FromResult(r);
        }, CancellationToken.None);
        using (result.Response)
        using (var cancel = new CancellationTokenSource())
        {
            var stream = await result.Response.Content.ReadAsStreamAsync();
            var read = stream.ReadAsync(new byte[16], cancel.Token).AsTask();
            cancel.Cancel();
            await Assert.CatchAsync<OperationCanceledException>(async () => await read);
            (await Get(cache)).Hit.Should().BeFalse();
            (await Get(cache)).Hit.Should().BeTrue();
        }
    }

    private sealed class NonSeekingStream(byte[] bytes, Task? gate = null) : Stream
    {
        private readonly MemoryStream _source = new(bytes);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (gate is not null) await gate.WaitAsync(cancellationToken);
            return await _source.ReadAsync(buffer, cancellationToken);
        }
        public override int Read(byte[] buffer, int offset, int count) => _source.Read(buffer, offset, count);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _source.Dispose(); base.Dispose(disposing); }
    }

    [Test]
    public async Task DiskReopensWithFreshnessAndRejectsConcurrentOwnersAndCorruptFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "jint-cache-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var cache = Cache(directory: directory))
            {
                await Get(cache);
                Assert.Throws<IOException>(() => Cache(directory: directory));
            }
            File.WriteAllText(Path.Combine(directory, "corrupt.entry"), "truncated");
            File.WriteAllText(Path.Combine(directory, "unfinished.tmp"), "partial");
            using (var cache = Cache(directory: directory))
            {
                (await Get(cache)).Hit.Should().BeTrue();
                File.Exists(Path.Combine(directory, "corrupt.entry")).Should().BeFalse();
                File.Exists(Path.Combine(directory, "unfinished.tmp")).Should().BeFalse();
                cache.Clear();
            }
            using (var cache = Cache(directory: directory)) (await Get(cache)).Hit.Should().BeFalse();
            var path = Directory.GetFiles(directory, "*.entry").Single();
            var bytes = File.ReadAllBytes(path);
            bytes[^33] ^= 1;
            File.WriteAllBytes(path, bytes);
            using (var cache = Cache(directory: directory)) (await Get(cache)).Hit.Should().BeFalse();
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public void TemporaryDiskStorageIsRemovedOnDispose()
    {
        var directory = Path.Combine(Path.GetTempPath(), "jint-cache-test-" + Guid.NewGuid().ToString("N"));
        Cache(directory: directory, temporary: true).Dispose();
        Directory.Exists(directory).Should().BeFalse();
    }
}
#endif
