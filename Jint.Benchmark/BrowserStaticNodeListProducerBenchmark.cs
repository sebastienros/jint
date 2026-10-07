using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>
/// Remaining static NodeList reads from issue #4013: item() on a selector snapshot and indexed/item()
/// reads on MutationRecord snapshots. ParentNode probes the canonical node-wrapper lookup; PlainArray
/// is an unaffected floor. SelectorIndex records the existing memo for context, not an ablation of it.
/// </summary>
/// <remarks>
/// Each row has its own page, document and engine, warmed only with its measured script. Construction,
/// parsing, selector evaluation and mutation delivery are outside measurement. Each call performs 10,000
/// reads to amortize the page mailbox; every read contributes to the checksum. The plain array loop uses
/// the same passes so its additive mailbox cost is visible rather than subtracted from the DOM rows.
/// These are warm retained-list workloads, not a measurement of snapshot production or cold wrapping.
/// </remarks>
[MemoryDiagnoser]
public class BrowserStaticNodeListProducerBenchmark
{
    private const string Fixture = """
        var root=document.getElementById('root');
        var observer=new MutationObserver(function(){});
        observer.observe(root,{childList:true});
        var fragment=document.createDocumentFragment();
        for(var k=0;k<100;k++) fragment.append(document.createElement('i'));
        root.append(fragment);
        var record=observer.takeRecords()[0];
        observer.disconnect();
        """;

    private static readonly string IndexLoop = Loop("list[j]", "el");
    private static readonly string ItemLoop = Loop("list.item(j)", "el");
    private static readonly string ParentLoop = Loop("el.parentNode", "root");
    private Browser.Browser _browser = null!;
    private Page _selectorIndex = null!;
    private Page _selectorItem = null!;
    private Page _mutationIndex = null!;
    private Page _mutationItem = null!;
    private Page _parent = null!;
    private Page _plain = null!;

    private static string Loop(string read, string expected) => $$"""
        (function(){var sum=0;for(var i=0;i<100;i++)for(var j=0;j<100;j++)
        if({{read}}==={{expected}})sum++;return sum;})()
        """;

    [GlobalSetup]
    public async Task Setup()
    {
        _browser = new Browser.Browser();
        _selectorIndex = await Create("root.querySelectorAll('span')", IndexLoop, 100);
        _selectorItem = await Create("root.querySelectorAll('span')", ItemLoop, 100);
        _mutationIndex = await Create("record.addedNodes", IndexLoop, 100);
        _mutationItem = await Create("record.addedNodes", ItemLoop, 100);
        _parent = await Create("root.querySelectorAll('span')", ParentLoop, 10_000);
        _plain = await Create("Array.from(root.querySelectorAll('span'))", IndexLoop, 100);
    }

    private async Task<Page> Create(string list, string script, double expected)
    {
        var page = await _browser.NewPageAsync();
        await page.SetContentAsync("<main id='root'>" + string.Concat(Enumerable.Repeat("<span></span>", 100)) + "</main>");
        await page.EvaluateAsync<double>(Fixture + "var list=" + list + ";var el=list[50];0;");
        if (await page.EvaluateAsync<double>(script) != expected || page.Errors.Count != 0)
        {
            throw new InvalidOperationException("Static NodeList benchmark fixture failed.");
        }
        return page;
    }

    [Benchmark]
    public Task<double> SelectorIndex() => _selectorIndex.EvaluateAsync<double>(IndexLoop);

    [Benchmark]
    public Task<double> SelectorItem() => _selectorItem.EvaluateAsync<double>(ItemLoop);

    [Benchmark]
    public Task<double> MutationIndex() => _mutationIndex.EvaluateAsync<double>(IndexLoop);

    [Benchmark]
    public Task<double> MutationItem() => _mutationItem.EvaluateAsync<double>(ItemLoop);

    [Benchmark]
    public Task<double> ParentNode() => _parent.EvaluateAsync<double>(ParentLoop);

    [Benchmark(Baseline = true)]
    public Task<double> PlainArray() => _plain.EvaluateAsync<double>(IndexLoop);

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
