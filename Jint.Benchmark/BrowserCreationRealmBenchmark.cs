using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>
/// Repeated cross-document adoption of a detached, recorded tree exercises creation-realm bookkeeping.
/// Each row has its own page and warms only its own script. Construction and parsing are outside
/// measurement. One hundred adoptions of 500 attributed elements amortize the mailbox round trip;
/// the plain-script control never reaches DOM recording. Both use only the public browser API.
/// </summary>
[MemoryDiagnoser]
public class BrowserCreationRealmBenchmark
{
    private const string Adoption = """
        (() => { let count = 0; for (let i = 0; i < 100; i++) {
          count += (i % 2 === 0 ? other : document).adoptNode(root) === root;
        } return count; })()
        """;
    private const string Control = """
        (() => { let count = 0; for (let i = 0; i < 100000; i++) {
          count += i % 97;
        } return count; })()
        """;
    private Browser.Browser _browser = null!;
    private Page _adoption = null!;
    private Page _control = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _browser = new Browser.Browser();
        _adoption = await _browser.NewPageAsync();
        await _adoption.SetContentAsync("<main id=root>" + string.Concat(Enumerable.Repeat("<span data-value=x>text</span>", 500)) + "</main>");
        await _adoption.EvaluateAsync("var root = document.querySelector('main'); root.remove(); var other = document.implementation.createHTMLDocument('other');");
        if (await _adoption.EvaluateAsync<int>(Adoption) != 100)
        {
            throw new InvalidOperationException("Adoption checksum failed.");
        }
        _control = await _browser.NewPageAsync();
        await _control.SetContentAsync("<!doctype html><title>Control</title>");
        if (await _control.EvaluateAsync<int>(Control) != 4799685)
        {
            throw new InvalidOperationException("Control checksum failed.");
        }
    }

    [Benchmark]
    public Task<int> RecordedSubtree() => _adoption.EvaluateAsync<int>(Adoption);

    [Benchmark]
    public Task<int> PlainScript() => _control.EvaluateAsync<int>(Control);

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
