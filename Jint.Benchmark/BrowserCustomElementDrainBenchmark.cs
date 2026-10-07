#nullable enable

using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>Repeated custom-element reaction drains, including nested callbacks and empty drains.</summary>
/// <remarks>
/// Each row creates its own page/engine and warms only its workload. Construction is excluded.
/// Observed batches 1,000 writes/callbacks; Nested batches 250 writes through four distinct elements
/// (1,000 callbacks), preserving synchronous nesting. Unobserved batches 1,000 writes with no reactions;
/// Adoption alternates 500 round trips between two documents (1,000 callbacks), checking their
/// arguments. SameDocumentAdoption makes 1,000 adopt calls with no reactions. These rows reuse
/// warmed per-element reaction storage: they do not measure first-allocation capacity overhead.
/// PlainScript batches 20,000 arithmetic steps and cannot reach DOM or reaction delivery.
/// These loops amortise the page mailbox. Results are per batch, with checked callback counts.
/// Only public Browser/Page APIs are used; no direct access to the registry's queue is measured.
/// </remarks>
[MemoryDiagnoser]
public class BrowserCustomElementDrainBenchmark
{
    private Browser.Browser _browser = null!;
    private Page _page = null!;
    private string _script = null!;
    private double _expected;

    [GlobalSetup(Target = nameof(Observed))]
    public Task SetupObserved() => Setup("for(let i=0;i<1000;i++) nodes[0].setAttribute('a', (i&1)?'1':'0'); return calls;", 1000);

    [GlobalSetup(Target = nameof(Nested))]
    public Task SetupNested() => Setup("for(let i=0;i<250;i++) nodes[0].setAttribute('a', (i&1)?'1':'0'); return calls;", 1000, nested: true);

    [GlobalSetup(Target = nameof(Unobserved))]
    public Task SetupUnobserved() => Setup("let sum=0;for(let i=0;i<1000;i++) {nodes[0].setAttribute('b', (i&1)?'1':'0');sum++;} return sum+calls;", 1000);

    [GlobalSetup(Target = nameof(PlainScript))]
    public Task SetupPlain() => Setup("let sum=0;for(let i=0;i<20000;i++) sum+=(i&1);return sum+calls;", 10000);

    [GlobalSetup(Target = nameof(Adoption))]
    public Task SetupAdoption() => SetupAdoptionRow(sameDocument: false);

    [GlobalSetup(Target = nameof(SameDocumentAdoption))]
    public Task SetupSameDocumentAdoption() => SetupAdoptionRow(sameDocument: true);

    private async Task SetupAdoptionRow(bool sameDocument)
    {
        _browser = new Browser.Browser(new BrowserOptions { MaxTaskDuration = TimeSpan.FromSeconds(30) });
        _page = await _browser.NewPageAsync();
        await _page.SetContentAsync("<!doctype html><body></body>");
        await _page.EvaluateAsync("""
            var calls=0,bad=0;
            class AdoptionElement extends HTMLElement {
              adoptedCallback(oldDocument,newDocument) {
                calls++;
                if(arguments.length!==2 || oldDocument===newDocument ||
                   newDocument!==this.ownerDocument ||
                   !((oldDocument===document && newDocument===other) ||
                     (oldDocument===other && newDocument===document))) bad++;
              }
            }
            customElements.define('x-drain-adoption',AdoptionElement);
            var node=document.createElement('x-drain-adoption');
            var other=document.implementation.createHTMLDocument();
            """);
        _script = sameDocument
            ? "(()=>{calls=bad=0;let sum=0;for(let i=0;i<1000;i++)sum+=document.adoptNode(node)===node;return sum+calls+bad*100000;})()"
            : "(()=>{calls=bad=0;for(let i=0;i<500;i++){other.adoptNode(node);document.adoptNode(node);}return calls+bad*100000;})()";
        _expected = 1000;
        await Run();
        if (_page.Errors.Count != 0) throw new InvalidOperationException("Adoption benchmark setup failed.");
    }

    private async Task Setup(string loop, double expected, bool nested = false)
    {
        _browser = new Browser.Browser(new BrowserOptions { MaxTaskDuration = TimeSpan.FromSeconds(30) });
        _page = await _browser.NewPageAsync();
        await _page.SetContentAsync("<!doctype html><body></body>");
        await _page.EvaluateAsync("""
            var calls=0,nodes=[];
            class DrainElement extends HTMLElement {
              static observedAttributes=['a'];
              attributeChangedCallback(name,oldValue,newValue) {
                calls++;
                if(this.next) this.next.setAttribute('a',newValue);
              }
            }
            customElements.define('x-drain',DrainElement);
            for(let i=0;i<4;i++) nodes.push(document.createElement('x-drain'));
            """ + (nested ? "for(let i=0;i<3;i++) nodes[i].next=nodes[i+1];" : ""));
        _script = "(()=>{calls=0;" + loop + "})()";
        _expected = expected;
        await Run();
        if (_page.Errors.Count != 0) throw new InvalidOperationException("Custom-element benchmark setup failed.");
    }

    private async Task<double> Run()
    {
        var result = await _page.EvaluateAsync<double>(_script);
        if (result != _expected) throw new InvalidOperationException("Reaction benchmark lost a callback.");
        return result;
    }

    [Benchmark] public Task<double> Observed() => Run();
    [Benchmark] public Task<double> Nested() => Run();
    [Benchmark] public Task<double> Unobserved() => Run();
    [Benchmark] public Task<double> PlainScript() => Run();
    [Benchmark] public Task<double> Adoption() => Run();
    [Benchmark] public Task<double> SameDocumentAdoption() => Run();

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
