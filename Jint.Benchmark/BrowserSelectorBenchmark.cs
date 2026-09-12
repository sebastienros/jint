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

        _targetMissing = await CreatePageAsync(document, "https://example.test/#missing", TargetScript);
        _targetPresent = await CreatePageAsync(document, "https://example.test/#present", TargetScript);
        _defaultButton = await CreatePageAsync(document, "https://example.test/", DefaultScript);
        _indeterminate = await CreatePageAsync(document, "https://example.test/", IndeterminateScript);
        _valid = await CreatePageAsync(document, "https://example.test/", ValidScript);
        _control = await CreatePageAsync(document, "https://example.test/", ControlScript);
    }

    private const string TargetScript = "document.querySelectorAll(':target').length";
    private const string DefaultScript = "document.querySelectorAll(':default').length";
    private const string IndeterminateScript = "document.querySelectorAll(':indeterminate').length";
    private const string ValidScript = "document.querySelectorAll(':valid').length";
    private const string ControlScript = "document.querySelectorAll('.item').length";

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
    public Task<double> TargetMissing() => _targetMissing.EvaluateAsync<double>(TargetScript);

    [Benchmark]
    public Task<double> TargetPresent() => _targetPresent.EvaluateAsync<double>(TargetScript);

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
