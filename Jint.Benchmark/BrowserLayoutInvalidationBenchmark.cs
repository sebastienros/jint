using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>
/// Browser-owned invalidation: repeated geometry, alternating writes/reads, mutation-only work and parsing.
/// Each row owns a page and warms only its own workload. Queries loop 100 times, writes 1,000 times and the
/// arithmetic control 10,000 times to amortize the mailbox. ParseDocument includes engine and DOM creation;
/// other rows exclude them. The arithmetic control cannot reach DOM mutation or layout code.
/// </summary>
[MemoryDiagnoser]
public class BrowserLayoutInvalidationBenchmark
{
    private Browser.Browser _browser = null!;
    private Page _reads = null!;
    private Page _mixed = null!;
    private Page _writes = null!;
    private Page _parsing = null!;
    private Page _control = null!;
    private string _html = null!;

    private const string Reads = """
        (() => { const target = document.getElementById('target'); let sum = 0;
          for (let i = 0; i < 100; i++) sum += target.getBoundingClientRect().top;
          return sum; })()
        """;
    private const string Mixed = """
        (() => { const target = document.getElementById('target'); let sum = 0;
          for (let i = 0; i < 100; i++) {
            target.hidden = !!(i & 1); sum += target.getBoundingClientRect().height;
          } return sum; })()
        """;
    private const string Writes = """
        (() => { const target = document.getElementById('target');
          for (let i = 0; i < 1000; i++) target.className = i & 1 ? 'a' : 'b';
          return target.className; })()
        """;
    private const string Arithmetic = """
        (() => { let sum = 0; for (let i = 0; i < 10000; i++) sum += i; return sum; })()
        """;

    [GlobalSetup]
    public async Task Setup()
    {
        _browser = new Browser.Browser();
        _html = "<style>button {display:block} .row {color:blue}</style><main>"
            + string.Concat(Enumerable.Repeat("<div class='row'><span>row</span></div>", 100))
            + "<button id='target'>Save</button></main>";
        _reads = await Create(Reads);
        _mixed = await Create(Mixed);
        _writes = await Create(Writes);
        _control = await Create(Arithmetic);
        _parsing = await _browser.NewPageAsync();
        await _parsing.SetContentAsync(_html);
    }

    private async Task<Page> Create(string script)
    {
        var page = await _browser.NewPageAsync();
        await page.SetContentAsync(_html);
        await page.EvaluateAsync(script);
        return page;
    }

    [Benchmark]
    public Task<double> RepeatedRectangles() => _reads.EvaluateAsync<double>(Reads);

    [Benchmark]
    public Task<double> MutateAndMeasure() => _mixed.EvaluateAsync<double>(Mixed);

    [Benchmark]
    public Task<string> MutationsOnly() => _writes.EvaluateAsync<string>(Writes);

    [Benchmark]
    public Task ParseDocument() => _parsing.SetContentAsync(_html);

    [Benchmark]
    public Task<double> ArithmeticControl() => _control.EvaluateAsync<double>(Arithmetic);

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
