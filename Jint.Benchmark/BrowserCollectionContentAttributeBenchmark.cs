#nullable enable

using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>Current null-namespace content-attribute named lookup, after the native binding migration.</summary>
/// <remarks>
/// Each selected row owns one page/engine, constructed outside measurement and warmed only with its own
/// script. The fixture has 100 images with both a foreign namespaced name and an ordinary name. The
/// HTMLCollection lookup scans to the final image; document.all scans all images to reject a foreign
/// namespaced name. Each operation repeats 100 reads to amortise the mailbox. The ordinary-object control repeats
/// 10,000 reads to amortise its cheaper work and cannot execute either collection's attribute lookup.
/// These are current-code workloads, not an isolated attribute primitive or evidence of a production fix.
/// </remarks>
[MemoryDiagnoser]
public class BrowserCollectionContentAttributeBenchmark
{
    private Browser.Browser _browser = null!;
    private Page _page = null!;
    private string _script = null!;

    [GlobalSetup(Target = nameof(HtmlNamedItem))]
    public Task SetupHtml() => Setup("var c = document.getElementsByTagName('img');", "c.namedItem('target99') === last", 100, 100);

    [GlobalSetup(Target = nameof(AllNamedItem))]
    public Task SetupAll() => Setup("", "document.all.namedItem('foreign') === null", 100, 100);

    [GlobalSetup(Target = nameof(PlainPropertyControl))]
    public Task SetupControl() => Setup("var c = { target99: last };", "c.target99 === last", 10000, 10000);

    private async Task Setup(string bind, string expression, int passes, double expected)
    {
        _browser = new Browser.Browser();
        _page = await _browser.NewPageAsync();
        await _page.SetContentAsync("<!doctype html><body>" + string.Concat(Enumerable.Repeat("<img>", 100)));
        await _page.EvaluateAsync<double>("""
            var images = document.getElementsByTagName('img');
            for(var j=0; j<images.length; j++) {
                images[j].setAttributeNS('urn:foreign', 'name', 'foreign');
                images[j].setAttributeNS(null, 'name', 'target' + j);
            }
            var last = images[99];
            """ + bind + " 0;");
        _script = $$"""
            (function() { var sum = 0; for(var i=0;i<{{passes}};i++) sum += {{expression}}; return sum; })()
            """;
        if (await Run() != expected) throw new InvalidOperationException("Collection lookup checksum changed.");
    }

    private Task<double> Run() => _page.EvaluateAsync<double>(_script);
    [Benchmark] public Task<double> HtmlNamedItem() => Run();
    [Benchmark] public Task<double> AllNamedItem() => Run();
    [Benchmark] public Task<double> PlainPropertyControl() => Run();
    [GlobalCleanup] public async Task Cleanup() => await _browser.DisposeAsync();
}
