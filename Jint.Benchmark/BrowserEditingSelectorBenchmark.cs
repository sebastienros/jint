#nullable enable

using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>Current native editing selectors over nested true, false and plaintext hosts.</summary>
/// <remarks>
/// Each row owns a page warmed only by its own query; page construction is excluded. Three nested
/// chains each have Depth inheriting descendants. Queries repeat 100 times to amortise the mailbox.
/// The class selector control traverses the identical tree without requesting editing facts.
/// This measures current ancestor walks, not the historical ContentEditing.HostOf implementation.
/// </remarks>
[MemoryDiagnoser]
public class BrowserEditingSelectorBenchmark
{
    private Browser.Browser _browser = null!;
    private Page _page = null!;
    private string _script = null!;
    private int _expected;

    [Params(8, 64)] public int Depth { get; set; }

    [GlobalSetup(Target = nameof(ReadWrite))] public Task SetupReadWrite() => Setup(":read-write", 2);
    [GlobalSetup(Target = nameof(ReadOnly))] public Task SetupReadOnly() => Setup(":read-only", 1);
    [GlobalSetup(Target = nameof(ClassControl))] public Task SetupControl() => Setup(".candidate", 3);

    private async Task Setup(string selector, int chains)
    {
        _browser = new Browser.Browser(new BrowserOptions { MaxTaskDuration = TimeSpan.FromSeconds(30) });
        _page = await _browser.NewPageAsync();
        // The false barrier is inside an editable host; plaintext-only restores editing inside it.
        var chain = string.Concat(Enumerable.Repeat("<div class=candidate>", Depth));
        var close = string.Concat(Enumerable.Repeat("</div>", Depth));
        await _page.SetContentAsync("<!doctype html><body><section id=root><div contenteditable>"
            + chain + "<div contenteditable=false>" + chain + "<div contenteditable=plaintext-only>"
            + chain + close + "</div>" + close + "</div>" + close + "</div></section></body>");
        await _page.EvaluateAsync("var root=document.getElementById('root');");
        _script = "(()=>{let sum=0;for(let i=0;i<100;i++)sum+=root.querySelectorAll('"
            + (selector == ".candidate" ? selector : ".candidate" + selector) + "').length;return sum;})()";
        _expected = chains * Depth * 100;
        await Run();
        if (_page.Errors.Count != 0) throw new InvalidOperationException("Editing selector setup failed.");
    }

    private async Task<double> Run()
    {
        var result = await _page.EvaluateAsync<double>(_script);
        if (result != _expected) throw new InvalidOperationException("Editing selector result changed.");
        return result;
    }

    [Benchmark] public Task<double> ReadWrite() => Run();
    [Benchmark] public Task<double> ReadOnly() => Run();
    [Benchmark] public Task<double> ClassControl() => Run();
    [GlobalCleanup] public async Task Cleanup() => await _browser.DisposeAsync();
}
