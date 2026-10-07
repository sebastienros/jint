#nullable enable

using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>Live labels length, first indexed read and enumeration on a form-shaped document.</summary>
/// <remarks>
/// Each target-specific setup creates one page/engine and warms only that row. Parsing and construction
/// are excluded. Sixteen explicit labels precede 5,000 unrelated elements; the control is first, so its
/// duplicate-ID precedence check is short. This exposes repeated full-result materialization and suffix
/// scans without confusing them with first-ID lookup. No collection snapshot/cache is assumed.
/// Getter batches 2,000 attribute reads without enumerating, Length 50 reads, FirstItem 200,
/// Enumeration ten complete 16-label passes; PlainArray batches
/// 20,000 indexed reads and cannot reach label association. Each checksum depends on every pass.
/// Batches amortise the page mailbox, including the cheap candidate first-item path. Results are per
/// batch, not comparable as per-read ratios across rows. Only public Browser/Page APIs are used.
/// </remarks>
[MemoryDiagnoser]
public class BrowserLabelsBenchmark
{
    private Browser.Browser _browser = null!;
    private Page _page = null!;
    private string _script = null!;
    private double _expected;

    [GlobalSetup(Target = nameof(Getter))]
    public Task SetupGetter() => Setup("for(let i=0;i<2000;i++) if(control.labels) sum++;", 2000);

    [GlobalSetup(Target = nameof(Length))]
    public Task SetupLength() => Setup("for(let i=0;i<50;i++) sum += labels.length;", 800);

    [GlobalSetup(Target = nameof(FirstItem))]
    public Task SetupFirst() => Setup("for(let i=0;i<200;i++) if(labels[0] === first) sum++;", 200);

    [GlobalSetup(Target = nameof(Enumeration))]
    public Task SetupEnumeration() => Setup("for(let i=0;i<10;i++) for(let j=0;j<labels.length;j++) if(labels[j] === expected[j]) sum++;", 160);

    [GlobalSetup(Target = nameof(PlainArray))]
    public Task SetupPlain() => Setup("for(let i=0;i<20000;i++) if(expected[0] === first) sum++;", 20000, plain: true);

    private async Task Setup(string loop, double expected, bool plain = false)
    {
        _browser = new Browser.Browser(new BrowserOptions { MaxTaskDuration = TimeSpan.FromSeconds(30) });
        _page = await _browser.NewPageAsync();
        var labels = string.Concat(Enumerable.Range(0, 16).Select(i => $"<label id='label-{i}' for='control'>Label {i}</label>"));
        await _page.SetContentAsync("<!doctype html><input id='control'>" + labels
            + string.Concat(Enumerable.Repeat("<div>unrelated</div>", 5000)));
        // The control row takes its array from a selector, without warming the labels getter.
        await _page.EvaluateAsync("var expected=Array.from(document.querySelectorAll('label')),first=expected[0];"
            + (plain ? "0;" : "var control=document.getElementById('control'),labels=control.labels;0;"));
        _script = "(()=>{let sum=0;" + loop + "return sum;})()";
        _expected = expected;
        if (await Run() != expected || _page.Errors.Count != 0)
            throw new InvalidOperationException("Labels benchmark fixture checksum failed.");
    }

    private async Task<double> Run()
    {
        var result = await _page.EvaluateAsync<double>(_script);
        if (result != _expected) throw new InvalidOperationException("Labels benchmark lost membership/order.");
        return result;
    }

    [Benchmark]
    public Task<double> Getter() => Run();

    [Benchmark]
    public Task<double> Length() => Run();

    [Benchmark]
    public Task<double> FirstItem() => Run();

    [Benchmark]
    public Task<double> Enumeration() => Run();

    [Benchmark]
    public Task<double> PlainArray() => Run();

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
