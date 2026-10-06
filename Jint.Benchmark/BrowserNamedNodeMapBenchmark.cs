using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>NamedNodeMap lookup and wrapper identity over short and long native attribute lists.</summary>
/// <remarks>
/// The historical #4013 list-per-membership-probe allocation is gone: HasSupportedName scans directly.
/// Named property reads still check support before prototype visibility, then the generated named getter
/// checks support again before retrieving the Attr. Explicit getNamedItem and indexed reads distinguish
/// those costs; PlainProperty cannot execute the map lookup and is the control.
/// Each row owns one page/engine, warmed only with its own script. Page construction, attribute creation
/// and first Attr wrapping are excluded. Ten thousand reads per batch amortise the page mailbox even for
/// the plain-property control, and the checksum depends on every read. Results are per batch.
/// </remarks>
[MemoryDiagnoser]
public class BrowserNamedNodeMapBenchmark
{
    [Params(1, 128)]
    public int AttributeCount { get; set; }

    private Browser.Browser _browser = null!;
    private Row _named = null!;
    private Row _explicit = null!;
    private Row _indexed = null!;
    private Row _plain = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _browser = new Browser.Browser();
        // Counts are sized independently: the cheap indexed/plain reads set the mailbox floor, while
        // the scanning rows must also remain representative at AttributeCount=1. The initial values
        // coincide; keeping them per row allows resizing the expensive cases without weakening controls.
        _named = await CreateRow("map.foo", reads: 10_000);
        _explicit = await CreateRow("map.getNamedItem('foo')", reads: 10_000);
        _indexed = await CreateRow($"map[{AttributeCount - 1}]", reads: 10_000);
        _plain = await CreateRow("plain.foo", reads: 10_000);
    }

    private async Task<Row> CreateRow(string read, int reads)
    {
        var page = await _browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html>");
        await page.EvaluateAsync<double>($$"""
            var element = document.createElement('div');
            for (var i = 1; i < {{AttributeCount}}; i++) element.setAttribute('data-' + i, 'x');
            element.setAttribute('foo', 'x');
            var map = element.attributes, expected = map.getNamedItem('foo'), plain = { foo: expected };
            0;
            """);
        var script = $$"""
            (() => {
                let sum = 0;
                for (let i = 0; i < {{reads}}; i++) if ({{read}} === expected) sum++;
                return sum;
            })()
            """;
        var row = new Row(page, script);
        if (await row.Run() != reads) throw new InvalidOperationException("NamedNodeMap fixture lost attribute identity.");
        return row;
    }

    private sealed class Row(Page page, string script)
    {
        internal Task<double> Run() => page.EvaluateAsync<double>(script);
    }

    [Benchmark]
    public Task<double> NamedProperty() => _named.Run();

    [Benchmark]
    public Task<double> GetNamedItem() => _explicit.Run();

    [Benchmark]
    public Task<double> IndexedProperty() => _indexed.Run();

    [Benchmark(Baseline = true)]
    public Task<double> PlainProperty() => _plain.Run();

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
