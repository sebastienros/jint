#nullable enable

using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>Current element-backed token mutations for short and multi-token class attributes.</summary>
/// <remarks>
/// Each row owns a page warmed only with its workload. Parsing and page construction are excluded.
/// A batch performs 100 add/remove/toggle/replace cycles, with a direct setAttribute reset each cycle
/// to exercise liveness. The control uses the same loop and direct attribute writes without token APIs.
/// The batch amortises the mailbox round trip. Every mutation result contributes to validation.
/// </remarks>
[MemoryDiagnoser]
public class BrowserTokenMutationBenchmark
{
    private Browser.Browser _browser = null!;
    private Page _page = null!;
    private string _script = null!;
    private double _expected;

    [Params(2, 12)] public int TokenCount { get; set; }

    [GlobalSetup(Target = nameof(Mutations))] public Task SetupMutations() => Setup(false);
    [GlobalSetup(Target = nameof(SetAttributeControl))] public Task SetupControl() => Setup(true);

    private async Task Setup(bool control)
    {
        _browser = new Browser.Browser(new BrowserOptions { MaxTaskDuration = TimeSpan.FromSeconds(30) });
        _page = await _browser.NewPageAsync();
        await _page.SetContentAsync("<!doctype html><body></body>");
        var source = string.Join(" ", Enumerable.Range(0, TokenCount).Select(i => "class" + i));
        await _page.EvaluateAsync("var element=document.createElement('div');var list=element.classList;var source="
            + System.Text.Json.JsonSerializer.Serialize(source) + ";");
        _script = control
            ? "(()=>{let sum=0;for(let i=0;i<100;i++){element.setAttribute('class',source);element.setAttribute('class',source+' active');element.setAttribute('class',source);element.setAttribute('class',source+' selected');sum+=element.getAttribute('class').length;}return sum;})()"
            : "(()=>{let sum=0;for(let i=0;i<100;i++){element.setAttribute('class',source);list.add('active');list.remove('class0');sum+=list.toggle('selected')?1:0;sum+=list.replace('active','class0')?1:0;sum+=list.length;}return sum;})()";
        _expected = control ? (source.Length + 9) * 100 : (TokenCount + 3) * 100;
        await Run();
        if (_page.Errors.Count != 0) throw new InvalidOperationException("Token mutation setup failed.");
    }

    private async Task<double> Run()
    {
        var result = await _page.EvaluateAsync<double>(_script);
        if (result != _expected) throw new InvalidOperationException("Token mutation benchmark result changed.");
        return result;
    }

    [Benchmark] public Task<double> Mutations() => Run();
    [Benchmark] public Task<double> SetAttributeControl() => Run();
    [GlobalCleanup] public async Task Cleanup() => await _browser.DisposeAsync();
}
