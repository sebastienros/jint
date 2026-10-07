#nullable enable

using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>Import inert-document subtrees into a page with matching custom-element definitions.</summary>
/// <remarks>
/// Each invocation creates a page, installs definitions and an inert source, runs its own batch,
/// and closes the page. Page construction, parsing, setup and disposal enter the measurement.
/// A batch deeply imports a 21-element tree 20 times to amortise the mailbox. Custom rows check
/// all 400 constructors, target ownership and prototype identity. Customized sources have internal
/// is values and no is attributes. OrdinaryImport has the same tree size and registry but no matching
/// definition. PlainScript shares page setup but runs 20,000 arithmetic steps instead of imports.
/// Pages are bounded because repeated custom-element creation accumulates registry subscription
/// bookkeeping on a long-lived page. Only public Page APIs are used. No production fix is measured.
/// </remarks>
[MemoryDiagnoser]
public class BrowserCustomElementImportBenchmark
{
    private Browser.Browser _browser = null!;
    private string _setup = null!;
    private string _script = null!;
    private double _expected;

    [GlobalSetup(Target = nameof(AutonomousImport))]
    public void SetupAutonomous() => Setup("other.createElement('x-import')", "Autonomous", 400);

    [GlobalSetup(Target = nameof(CustomizedImport))]
    public void SetupCustomized() => Setup("other.createElement('button',{is:'import-button'})", "Customized", 400);

    [GlobalSetup(Target = nameof(OrdinaryImport))]
    public void SetupOrdinary() => Setup("other.createElement('span')", "HTMLSpanElement", 0);

    [GlobalSetup(Target = nameof(PlainScript))]
    public void SetupPlain() => Setup("other.createElement('span')", "HTMLSpanElement", 0, plain: true);

    private void Setup(string create, string type, int expectedCalls, bool plain = false)
    {
        _browser = new Browser.Browser(new BrowserOptions { MaxTaskDuration = TimeSpan.FromSeconds(30) });
        _setup = """
            var calls=0;
            class Autonomous extends HTMLElement { constructor(){super();calls++;} }
            class Customized extends HTMLButtonElement { constructor(){super();calls++;} }
            customElements.define('x-import',Autonomous);
            customElements.define('import-button',Customized,{extends:'button'});
            var other=document.implementation.createHTMLDocument();
            var source=other.createElement('section');
            """ + "for(let i=0;i<20;i++)source.appendChild(" + create + ");";
        _script = plain
            ? "(()=>{let sum=0;for(let i=0;i<20000;i++)sum+=(i&1);return sum;})()"
            : "(()=>{calls=0;let sum=0;for(let i=0;i<20;i++){const copy=document.importNode(source,true);"
              + "for(const child of copy.children){if(!(child instanceof " + type
              + ") || child.ownerDocument!==document || child.getAttribute('is')!==null)throw Error('bad import');sum++;}}"
              + "if(calls!==" + expectedCalls + ")throw Error('bad constructor count');return sum;})()";
        _expected = plain ? 10000 : 400;
    }

    private async Task<double> Run()
    {
        var page = await _browser.NewPageAsync();
        try
        {
            await page.SetContentAsync("<!doctype html><body></body>");
            await page.EvaluateAsync(_setup);
            var result = await page.EvaluateAsync<double>(_script);
            if (page.Errors.Count != 0) throw new InvalidOperationException("Import benchmark page failed.");
            if (result != _expected) throw new InvalidOperationException("Import benchmark lost nodes.");
            return result;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Benchmark] public Task<double> AutonomousImport() => Run();
    [Benchmark] public Task<double> CustomizedImport() => Run();
    [Benchmark] public Task<double> OrdinaryImport() => Run();
    [Benchmark] public Task<double> PlainScript() => Run();

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
