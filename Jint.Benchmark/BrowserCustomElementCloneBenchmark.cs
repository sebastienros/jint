#nullable enable

using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>Clone a plain subtree with no registry, an empty registry, or an unrelated definition.</summary>
/// <remarks>
/// Each row owns a page/engine warmed only with its workload; setup and parsing are excluded.
/// Each batch clones a detached 101-element tree 20 times to amortise the mailbox, checking every
/// clone's child count. The source has no is values. PlainScript is an unaffected arithmetic control.
/// All rows use public Page APIs. This measures current native cloning, not historical CarryIsValues.
/// </remarks>
[MemoryDiagnoser]
public class BrowserCustomElementCloneBenchmark
{
    private Browser.Browser _browser = null!;
    private Page _page = null!;
    private string _script = null!;
    private double _expected;

    [GlobalSetup(Target = nameof(NoRegistry))]
    public Task SetupNone() => Setup("");

    [GlobalSetup(Target = nameof(EmptyRegistry))]
    public Task SetupEmpty() => Setup("void customElements;");

    [GlobalSetup(Target = nameof(UnrelatedDefinition))]
    public Task SetupDefined() => Setup("customElements.define('x-unrelated', class extends HTMLElement {});");

    [GlobalSetup(Target = nameof(PlainScript))]
    public Task SetupPlain() => Setup("", plain: true);

    private async Task Setup(string registry, bool plain = false)
    {
        _browser = new Browser.Browser(new BrowserOptions { MaxTaskDuration = TimeSpan.FromSeconds(30) });
        _page = await _browser.NewPageAsync();
        await _page.SetContentAsync("<!doctype html><body></body>");
        await _page.EvaluateAsync("var source=document.createElement('div');for(let i=0;i<100;i++) source.appendChild(document.createElement('span'));" + registry);
        _script = plain
            ? "(()=>{let sum=0;for(let i=0;i<20000;i++) sum+=(i&1);return sum;})()"
            : "(()=>{let sum=0;for(let i=0;i<20;i++) {const copy=source.cloneNode(true);sum+=copy.childNodes.length;}return sum;})()";
        _expected = plain ? 10000 : 2000;
        await Run();
        if (_page.Errors.Count != 0) throw new InvalidOperationException("Clone benchmark setup failed.");
    }

    private async Task<double> Run()
    {
        var result = await _page.EvaluateAsync<double>(_script);
        if (result != _expected) throw new InvalidOperationException("Clone benchmark lost nodes.");
        return result;
    }

    [Benchmark] public Task<double> NoRegistry() => Run();
    [Benchmark] public Task<double> EmptyRegistry() => Run();
    [Benchmark] public Task<double> UnrelatedDefinition() => Run();
    [Benchmark] public Task<double> PlainScript() => Run();

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
