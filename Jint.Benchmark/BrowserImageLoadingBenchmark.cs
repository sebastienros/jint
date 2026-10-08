using System.Net;
using System.Net.Http;
using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>End-to-end document loading with repeated and distinct image URLs, plus an images-off floor.</summary>
/// <remarks>
/// Each row owns a page, browser and deterministic in-memory HTTP handler. SetContent replaces the engine
/// on every invocation: engine construction, parsing, full bounded body reads and completion events are
/// intentionally measured together. The 300-image document exposes per-element traffic and body-copy
/// work. This measures no network latency, HTTP cache or pixel decoding.
/// Each document load already includes engine construction and parsing, so it needs no script batching
/// to amortise the mailbox. Setup warms each page only with its own workload and validates availability.
/// Results are per document; ImagesDisabled uses the same repeated-URL markup but cannot fetch an image.
/// </remarks>
[MemoryDiagnoser]
public class BrowserImageLoadingBenchmark
{
    [Params(300)]
    public int ImageCount { get; set; }

    [Params(65_536)]
    public int ResponseBytes { get; set; }

    private Row _repeated = null!;
    private Row _distinct = null!;
    private Row _disabled = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _repeated = await CreateRow(distinct: false, enabled: true);
        _distinct = await CreateRow(distinct: true, enabled: true);
        _disabled = await CreateRow(distinct: false, enabled: false);
    }

    private async Task<Row> CreateRow(bool distinct, bool enabled)
    {
        // A PNG IHDR header is enough for the header-only image model. The padding is deliberately read
        // too: MaxSubresourceBytes bounds the full response, not just the recognised header prefix.
        var bytes = new byte[ResponseBytes];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82,
            0, 0, 0, 20, 0, 0, 0, 10, 8, 6, 0, 0, 0 }.CopyTo(bytes, 0);
        var client = new HttpClient(new ImageHandler(bytes));
        var browser = new Browser.Browser(new BrowserOptions
        {
            MaxImageRequests = enabled ? ImageCount : 0,
            MaxSubresourceBytes = ResponseBytes
        });
        var context = await browser.NewContextAsync(new BrowserContextOptions
        {
            HttpClient = client,
            UrlFilter = uri => uri.Host == "images.test"
        });
        var page = await context.NewPageAsync();
        var html = "<!doctype html><body>" + string.Concat(Enumerable.Range(0, ImageCount)
            .Select(i => $"<img src='https://images.test/{(distinct ? i : 0)}.png'>"));
        var row = new Row(browser, client, page, html);
        await row.Load();
        var available = await page.EvaluateAsync<double>(
            "Array.from(document.images).filter(i => i.complete && i.naturalWidth === 20 && i.naturalHeight === 10).length");
        if (available != (enabled ? ImageCount : 0) || page.Errors.Count != 0)
            throw new InvalidOperationException("Image-loading fixture lost availability or recorded an error.");
        return row;
    }

    private sealed class ImageHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes),
                RequestMessage = request
            });
        }
    }

    private sealed class Row(Browser.Browser browser, HttpClient client, Page page, string html)
    {
        internal Task Load() => page.SetContentAsync(html);
        internal async Task Dispose()
        {
            await browser.DisposeAsync();
            client.Dispose();
        }
    }

    [Benchmark]
    public Task RepeatedUrl() => _repeated.Load();

    [Benchmark]
    public Task DistinctUrls() => _distinct.Load();

    [Benchmark(Baseline = true)]
    public Task ImagesDisabled() => _disabled.Load();

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _repeated.Dispose();
        await _distinct.Dispose();
        await _disabled.Dispose();
    }
}
