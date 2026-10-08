#nullable enable

using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>Steady-state SVG relList reads over an unchanged native attribute.</summary>
/// <remarks>
/// Each row owns a page warmed only with its own workload; construction and initial token indexing
/// are excluded. Each batch performs 1,000 length/index/contains groups to amortise the mailbox trip.
/// The array control uses the same loop and checksum without DOM source validation. Both token counts
/// probe unchanged-source reuse, not mutation or cold parsing; contains intentionally finds the first token.
/// </remarks>
[MemoryDiagnoser]
public class BrowserRelTokenReadBenchmark
{
    private Browser.Browser _browser = null!;
    private Page _page = null!;
    private string _script = null!;
    private double _expected;

    [Params(2, 32)] public int TokenCount { get; set; }

    [GlobalSetup(Target = nameof(WarmRelList))] public Task SetupRelList() => Setup(false);
    [GlobalSetup(Target = nameof(ArrayControl))] public Task SetupArray() => Setup(true);

    private async Task Setup(bool control)
    {
        _browser = new Browser.Browser(new BrowserOptions { MaxTaskDuration = TimeSpan.FromSeconds(30) });
        _page = await _browser.NewPageAsync();
        await _page.SetContentAsync("<!doctype html><body></body>");
        var tokens = Enumerable.Range(0, TokenCount).Select(i => "token" + i).ToArray();
        await _page.EvaluateAsync(control
            ? "var list=" + System.Text.Json.JsonSerializer.Serialize(tokens) + ";"
            : "var element=document.createElementNS('http://www.w3.org/2000/svg','a');element.setAttribute('rel',"
                + System.Text.Json.JsonSerializer.Serialize(string.Join(" ", tokens)) + ");var list=element.relList;");
        _script = "(()=>{let sum=0;for(let i=0;i<1000;i++){sum+=list.length;sum+=list[0].length;sum+="
            + (control ? "list.includes('token0')" : "list.contains('token0')") + "?1:0;}return sum;})()";
        _expected = (TokenCount + 7) * 1000;
        await Run();
        if (_page.Errors.Count != 0) throw new InvalidOperationException("Rel token read setup failed.");
    }

    private async Task<double> Run()
    {
        var result = await _page.EvaluateAsync<double>(_script);
        if (result != _expected) throw new InvalidOperationException("Rel token read result changed.");
        return result;
    }

    [Benchmark] public Task<double> WarmRelList() => Run();
    [Benchmark] public Task<double> ArrayControl() => Run();
    [GlobalCleanup] public async Task Cleanup() => await _browser.DisposeAsync();
}
