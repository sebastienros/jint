using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>
/// A complete page load of each HTML parser corpus document through the public <see cref="Page.SetContentAsync"/>:
/// the native parse plus everything Jint.Browser layers on it while the parser runs (resource discovery,
/// creation realms, base URL, load events). Pair it with <see cref="HtmlParserComparisonBenchmark"/>'s
/// <c>ParseNative</c> row for the same case: the difference is the browser's per-page overhead.
/// Each row owns one page; the corpus has no scripts, so no script engine work is measured.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("HtmlParserComparison")]
public class BrowserPageParseBenchmark
{
    private Browser.Browser _browser = null!;
    private Page _page = null!;

    [ParamsSource(nameof(Cases))]
    public ParserCorpusCase Case { get; set; } = null!;
    public static IEnumerable<ParserCorpusCase> Cases => ParserCorpus.Cases.Where(item => item.Kind == ParserCorpusKind.Html);

    [GlobalSetup]
    public async Task Setup()
    {
        _browser = new Browser.Browser();
        _page = await _browser.NewPageAsync();
        await _page.SetContentAsync(Case.Source);
        if (await _page.EvaluateAsync<int>("document.getElementsByTagName('*').length") == 0)
        {
            throw new InvalidOperationException($"{Case} produced no elements.");
        }
    }

    [Benchmark]
    public Task SetContent() => _page.SetContentAsync(Case.Source);

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
