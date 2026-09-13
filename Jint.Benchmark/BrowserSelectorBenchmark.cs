using System.Text;
using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>
/// <c>querySelectorAll</c> for the four pseudo-classes <c>PagePseudoClassSelectorFactory</c> resolves against
/// document-wide state — <c>:target</c>, <c>:default</c>, <c>:indeterminate</c> and <c>:valid</c> — over a
/// 2,000-element document, the shape the profile on sebastienros/jint#4013 was taken at (60 rounds, a
/// document URL whose fragment names no element, Ultra at 8190 Hz).
/// </summary>
/// <remarks>
/// <para>
/// <b>What the profile found.</b> <c>:target</c> was roughly 77% of that capture, and none of it was the
/// fragment being re-parsed: <c>TargetSelector.Find</c> resolved "the indicated part of the document" —
/// <c>GetElementById</c>, and on a miss a scan of the whole document for a legacy named anchor — once per
/// candidate element, turning an O(document size) resolution into an O(document size²) query. Inverting the
/// test (ask an O(1) question about the candidate itself before doing any document-wide work) is what
/// <see cref="TargetMissing"/> and <see cref="TargetPresent"/> measure.
/// </para>
/// <para>
/// <b>What each row is.</b> <see cref="TargetMissing"/> is the profile's own worst case: a fragment that
/// names no element, so every one of 2,000 candidates used to run the full resolution and find nothing.
/// <see cref="TargetPresent"/> is the same query against a fragment that does name an element — the case
/// where the O(1) filter now lets exactly one candidate through instead of none.
/// <see cref="DefaultButton"/>, <see cref="Indeterminate"/> and <see cref="Valid"/> are the other three
/// selectors <c>PagePseudoClassSelectorFactory</c> resolves against document- or tree-wide state, over the
/// same document: a hundred unchecked, same-named radio buttons for <c>:indeterminate</c>'s per-candidate
/// group scan, and a nested fieldset around a failing control for <c>:valid</c>'s subtree walk.
/// <see cref="Control"/> — a plain class selector this change touches nothing about — is the row that must
/// not move.
/// </para>
/// <para>
/// <b>Every row loops its query rather than running it once, and the loop count is chosen per row —
/// see <c>Jint.Benchmark/AGENTS.md</c>'s "A row through <c>Page.EvaluateAsync</c> must amortise the mailbox
/// round trip" for why this is load-bearing rather than decorative.</b> A first cut of this class
/// ran each query exactly once per <c>[Benchmark]</c> invocation, the way a page script normally would, and a
/// paired run against it could not tell a real regression from noise: <c>Control</c>, <c>DefaultButton</c>
/// and <c>Valid</c> — three rows this change cannot touch at all — swung +4.7%, +19.5% and +18.6% between
/// two builds with identical code for all three, because each invocation is dominated by the one
/// <c>Page.EvaluateAsync</c> mailbox round trip rather than by the query itself. <see cref="TargetMissing"/>
/// and <see cref="Indeterminate"/> did not have this problem — one pass already costs on the order of a
/// millisecond or more, so the round trip is a rounding error against it — and stay at
/// <see cref="DominantWorkPasses"/> pass. The other four are cheap enough per pass (tens of microseconds
/// against the profile's document) that the round trip would otherwise be most of what is measured, so they
/// loop <see cref="RoundTripAmortizingPasses"/> times and sum every pass's result so none of it is optimised
/// away. <b>Do not collapse these two constants into one</b>: the rows they cover differ in per-pass cost by
/// roughly three orders of magnitude, and one shared pass count would either make the cheap rows still
/// round-trip-dominated or make the expensive ones absurdly slow.
/// </para>
/// <para>
/// <b>Engine isolation.</b> One <see cref="Page"/> — and therefore one engine and one document — per row,
/// built in <c>[GlobalSetup]</c> and warmed with only that row's own query, so no row's number depends on
/// which sibling ran first. Page construction and the HTML parse stay outside the measurement.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class BrowserSelectorBenchmark
{
    /// <summary>The document size the profile was taken at.</summary>
    private const int ElementCount = 2000;

    /// <summary>
    /// Unchecked, same-named radio buttons with no checked member — <c>:indeterminate</c>'s worst case,
    /// since <c>TheRadioButtonGroupOfHasACheckedMember</c>'s document-wide scan runs to completion for every
    /// one of them rather than stopping at an early checked sibling.
    /// </summary>
    private const int RadioCount = 100;

    /// <summary>
    /// The pass count for a row whose single pass is already well above a mailbox round trip on its own —
    /// <see cref="TargetMissing"/> (roughly 880 µs after the fix in this file's own change, and far more
    /// before it) and <see cref="Indeterminate"/> (roughly 1.6 ms after sebastienros/jint#4013's subtree-walk
    /// fix replaced <c>TheRadioButtonGroupOfHasACheckedMember</c>'s O(descendants²) scan with
    /// <c>DomElementWalker</c>, and roughly 100 ms before it — the O(n²) cost of scanning the whole document
    /// for every one of a hundred unchecked radios). Both stay comfortably above the round trip even after
    /// the fix, so one pass is still enough to measure either. Looping either further would only make the
    /// class slower to run for no gain in signal.
    /// </summary>
    private const int DominantWorkPasses = 1;

    /// <summary>
    /// The pass count for a row whose single pass costs on the order of tens of microseconds — cheaper than
    /// the <c>Page.EvaluateAsync</c> mailbox round trip itself, which is exactly what let
    /// <see cref="Control"/>, <see cref="DefaultButton"/> and <see cref="Valid"/> swing up to +19.5% in a
    /// paired run against code that could not have changed their cost at all (see the class remarks). Five
    /// hundred passes puts each of these comfortably into the low tens of milliseconds, the same way
    /// <c>BrowserNodeListBenchmark.Passes</c> amortises its own per-invocation round trip.
    /// </summary>
    private const int RoundTripAmortizingPasses = 500;

    private Browser.Browser _browser = null!;
    private Page _targetMissing = null!;
    private Page _targetPresent = null!;
    private Page _defaultButton = null!;
    private Page _indeterminate = null!;
    private Page _valid = null!;
    private Page _control = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _browser = new Browser.Browser();

        var document = BuildDocument();

        _targetMissing = await CreatePageAsync(document, "https://example.test/#missing", TargetMissingScript);
        _targetPresent = await CreatePageAsync(document, "https://example.test/#present", TargetPresentScript);
        _defaultButton = await CreatePageAsync(document, "https://example.test/", DefaultScript);
        _indeterminate = await CreatePageAsync(document, "https://example.test/", IndeterminateScript);
        _valid = await CreatePageAsync(document, "https://example.test/", ValidScript);
        _control = await CreatePageAsync(document, "https://example.test/", ControlScript);
    }

    private static readonly string TargetMissingScript = Query(":target", DominantWorkPasses);
    private static readonly string TargetPresentScript = Query(":target", RoundTripAmortizingPasses);
    private static readonly string DefaultScript = Query(":default", RoundTripAmortizingPasses);
    private static readonly string IndeterminateScript = Query(":indeterminate", DominantWorkPasses);
    private static readonly string ValidScript = Query(":valid", RoundTripAmortizingPasses);
    private static readonly string ControlScript = Query(".item", RoundTripAmortizingPasses);

    /// <summary>
    /// <paramref name="selector"/>'s <c>querySelectorAll(…).length</c>, run <paramref name="passes"/> times
    /// and summed, so that (a) the one <c>Page.EvaluateAsync</c> mailbox round trip this whole script pays is
    /// amortised over every pass rather than measured once per query, and (b) the total depends on every
    /// pass's result, so nothing in the loop can be optimised away.
    /// </summary>
    private static string Query(string selector, int passes) => $$"""
        (function () {
            var total = 0;
            for (var i = 0; i < {{passes}}; i++) {
                total += document.querySelectorAll('{{selector}}').length;
            }
            return total;
        })()
        """;

    /// <summary>One page holding the shared document, warmed with this row's own query and nothing else.</summary>
    private async Task<Page> CreatePageAsync(string html, string baseUrl, string script)
    {
        var page = await _browser.NewPageAsync();
        await page.SetContentAsync(html, baseUrl);
        await page.EvaluateAsync<double>(script);
        return page;
    }

    /// <summary>
    /// <see cref="ElementCount"/> elements: mostly <c>.item</c> filler (what <see cref="Control"/> queries),
    /// a form whose only submit button is trivially its own default button, a hundred unchecked same-named
    /// radio buttons, and a nested fieldset around one empty required input so <c>:valid</c>'s fieldset arm
    /// has a failing descendant to find rather than an empty subtree.
    /// </summary>
    private static string BuildDocument()
    {
        var html = new StringBuilder("<!doctype html><html><body><div id=\"root\">");

        // The element the "fragment naming an element" row's :target resolves to -- first in tree order,
        // so confirming it costs the O(1) filter and one GetElementById hop rather than a document scan.
        html.Append("<div id=\"present\" class=\"item\"></div>");

        const int explicitElements = RadioCount + 5; // submit button + radios + fieldsets + input
        for (var i = 0; i < ElementCount - explicitElements; i++)
        {
            html.Append("<div class=\"item\"></div>");
        }

        html.Append("<form id=\"mainForm\">");
        html.Append("<button type=\"submit\">Go</button>");

        for (var i = 0; i < RadioCount; i++)
        {
            html.Append("<input type=\"radio\" name=\"opt\">");
        }

        html.Append("<fieldset><fieldset><input required></fieldset></fieldset>");
        html.Append("</form>");

        return html.Append("</div></body></html>").ToString();
    }

    [Benchmark]
    public Task<double> TargetMissing() => _targetMissing.EvaluateAsync<double>(TargetMissingScript);

    [Benchmark]
    public Task<double> TargetPresent() => _targetPresent.EvaluateAsync<double>(TargetPresentScript);

    [Benchmark]
    public Task<double> DefaultButton() => _defaultButton.EvaluateAsync<double>(DefaultScript);

    [Benchmark]
    public Task<double> Indeterminate() => _indeterminate.EvaluateAsync<double>(IndeterminateScript);

    [Benchmark]
    public Task<double> Valid() => _valid.EvaluateAsync<double>(ValidScript);

    [Benchmark(Baseline = true)]
    public Task<double> Control() => _control.EvaluateAsync<double>(ControlScript);

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
