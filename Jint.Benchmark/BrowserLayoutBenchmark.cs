using BenchmarkDotNet.Attributes;
using Jint.Browser;
using Jint.Tests.Browser.Layout;

namespace Jint.Benchmark;

/// <summary>
/// A nested admin form with inherited theme tokens and grouped CSS selectors. Engine construction and HTML
/// parsing are excluded; this row owns one page and warms only its own geometry query.
/// </summary>
[MemoryDiagnoser]
public class BrowserLayoutBenchmark
{
    private Browser.Browser _browser = null!;
    private Page _page = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _browser = new Browser.Browser();
        _page = await _browser.NewPageAsync();
        await _page.SetContentAsync(AdminSettingsDocument.Create());
    }

    [Benchmark]
    public Task<double> QuerySaveButton()
        => _page.EvaluateAsync<double>("document.querySelector('.btn.save').getBoundingClientRect().top");

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
