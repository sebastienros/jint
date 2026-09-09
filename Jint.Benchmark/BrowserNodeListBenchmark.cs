using System.Text;
using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>
/// The element read of the loop <c>dom/nodes/support/NodeList-static-length-tampered.js</c> compiles —
/// <c>for (j = 0; j &lt; list.length; j++) if (list[j] === el)</c> — over a <b>real page</b>, so that
/// <c>list[j]</c> is a DOM projection rather than a host object written for a benchmark.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is here and not beside <see cref="HostCollectionLengthBenchmark"/>.</b> That class measures the
/// same loop's <c>length</c> read with a hand-written <c>ArrayLikeObject</c>, which is the right shape for a
/// question about the engine. This one is about what the <i>binding</i> does per element — the wrapper lookup
/// behind <c>DomAccessorNodeList.TryGetIndex</c> — and no host object written here can stand in for it,
/// because the cost is a <c>ConditionalWeakTable</c> probe against the realm's wrapper table.
/// <c>Jint.Benchmark</c> already references <c>Jint.Browser</c> for <see cref="BrowserLayoutBenchmark"/>, so
/// the row lives in the same project as every other row rather than in a browser-side benchmark project of
/// its own — there is none, and <c>tools/browser-comparison</c> measures other engines rather than this one.
/// </para>
/// <para>
/// <b>What each row is.</b>
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="StaticNodeList"/> — the loop over a <c>querySelectorAll</c> result, which DOM §4.2.6 makes a
/// static <c>NodeList</c>. This is the row the change is about.
/// </description></item>
/// <item><description>
/// <see cref="StaticNodeListTamperedLength"/> — the same list after the page has defined an own <c>length</c>
/// accessor answering the same count, which is the half of the wpt documents that makes the pristine-length
/// lane decline. The loop does the same work, so what is left in the delta is the element read.
/// </description></item>
/// <item><description>
/// <see cref="LiveNodeList"/> — the same loop over <c>childNodes</c>, which is live and deliberately keeps the
/// accessor-driven wrapper. It is the control that must not move.
/// </description></item>
/// <item><description>
/// <see cref="PlainArray"/> — the floor and the baseline: the identical loop over the
/// <c>Array.from(list)</c> of the same elements, so the gap is the price of the list being a projection.
/// </description></item>
/// </list>
/// <para>
/// <b>Engine isolation.</b> One <see cref="Page"/> — and therefore one engine and one document — per row,
/// built in <c>[GlobalSetup]</c> and given only that row's own fixture, so a row's number never depends on
/// which sibling warmed which lane first. Page construction and the HTML parse stay outside the measurement;
/// what the measured call adds over the loop itself is one mailbox round trip per operation, which is the
/// same term in every row including the baseline.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class BrowserNodeListBenchmark
{
    /// <summary>
    /// 100 elements, which is what the wpt helper builds and the size the profile on
    /// sebastienros/jint#4013 was taken at.
    /// </summary>
    [Params(100)]
    public int Count { get; set; }

    /// <summary>
    /// 100 passes of the inner loop per operation. The wpt document runs 50,000 and takes seconds; this is
    /// the same shape at a size a benchmark can repeat.
    /// </summary>
    private const string Passes = "100";

    /// <summary>
    /// The loop, verbatim in shape from the wpt helper: the match is at index 50, and the <c>break</c> is
    /// what makes an operation about half the list.
    /// </summary>
    private const string Loop = $$"""
        (function () {
            var index = -1;
            for (var i = 0; i < {{Passes}}; i++) {
                for (var j = 0; j < list.length; j++) {
                    if (list[j] === el) { index = j; break; }
                }
            }
            return index;
        })()
        """;

    private Browser.Browser _browser = null!;
    private Page _staticList = null!;
    private Page _staticListTampered = null!;
    private Page _liveList = null!;
    private Page _plainArray = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _browser = new Browser.Browser();

        _staticList = await CreatePageAsync("var list = document.getElementById('root').querySelectorAll('span');");

        _staticListTampered = await CreatePageAsync(
            "var list = document.getElementById('root').querySelectorAll('span');"
            + $"Object.defineProperty(list, 'length', {{ get: function () {{ return {Count}; }} }});");

        _liveList = await CreatePageAsync("var list = document.getElementById('root').childNodes;");

        _plainArray = await CreatePageAsync(
            "var list = Array.from(document.getElementById('root').querySelectorAll('span'));");
    }

    /// <summary>
    /// One page holding <paramref name="bind"/>'s <c>list</c> and the element the loop looks for, warmed with
    /// this row's own loop and nothing else.
    /// </summary>
    private async Task<Page> CreatePageAsync(string bind)
    {
        var page = await _browser.NewPageAsync();
        await page.SetContentAsync(Document(Count));
        await page.EvaluateAsync<double>(bind + " var el = list[50]; 0;");
        await page.EvaluateAsync<double>(Loop);
        return page;
    }

    /// <summary>A document of <paramref name="count"/> spans under one parent, and nothing else.</summary>
    private static string Document(int count)
    {
        var html = new StringBuilder("<!doctype html><html><body><div id=\"root\">");
        for (var i = 0; i < count; i++)
        {
            html.Append("<span class=\"foo\"></span>");
        }

        return html.Append("</div></body></html>").ToString();
    }

    [Benchmark]
    public Task<double> StaticNodeList() => _staticList.EvaluateAsync<double>(Loop);

    [Benchmark]
    public Task<double> StaticNodeListTamperedLength() => _staticListTampered.EvaluateAsync<double>(Loop);

    [Benchmark]
    public Task<double> LiveNodeList() => _liveList.EvaluateAsync<double>(Loop);

    [Benchmark(Baseline = true)]
    public Task<double> PlainArray() => _plainArray.EvaluateAsync<double>(Loop);

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
