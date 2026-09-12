using System.Text;
using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>
/// The same loop <see cref="BrowserNodeListBenchmark"/> measures over a <c>NodeList</c> —
/// <c>for (j = 0; j &lt; c.length; j++) if (c[j] === el)</c> — over the four <b>live
/// <c>HTMLCollection</c></b> shapes a page actually reaches: <c>getElementsByClassName</c>,
/// <c>getElementsByTagName</c>, <c>form.elements</c> and <c>children</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the suite needed it.</b> There was no row for an <c>HTMLCollection</c> at all, and an
/// <c>HTMLCollection</c> is not a <c>NodeList</c> with a different name: a <c>NodeList</c> arrives at the
/// binding as something that can be indexed in constant time, while every <c>HTMLCollection</c> AngleSharp
/// hands over is a lazy view over a tree walk — <c>Length</c> is a <c>Count()</c> of that walk and the
/// indexer is a linear scan of it. A profile of this loop on a 1,500-element document put
/// <c>NodeExtensions.GetDescendantsAndSelf.MoveNext</c> at 33.6% inclusive of the page thread, split three
/// ways between the loop's own <c>c.length</c>, the element read, and a bounds pre-check that ran the whole
/// query a second time before each element read. The rows below are what makes that visible; none of the
/// existing browser rows touched it.
/// </para>
/// <para>
/// <b>What each row is.</b>
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="ClassName"/> — <c>getElementsByClassName</c>, whose filter the binding owns because DOM makes
/// its comparison ASCII case-insensitive in quirks mode and AngleSharp offers no seam for that.
/// </description></item>
/// <item><description>
/// <see cref="TagName"/> — <c>getElementsByTagName</c>, the same live shape with a cheaper per-element test.
/// </description></item>
/// <item><description>
/// <see cref="FormElements"/> — <c>form.elements</c>, which is AngleSharp's own
/// <c>HtmlFormControlsCollection</c>: a <c>Where</c> over the form controls of the whole document, re-run per
/// read. The binding cannot make that walk cheaper, only stop asking for it twice.
/// </description></item>
/// <item><description>
/// <see cref="Children"/> — <c>children</c>, AngleSharp's <c>HtmlCollection</c> over one node's element
/// children. Shallow, so it isolates the second walk from the cost of the walk itself.
/// </description></item>
/// <item><description>
/// <see cref="LiveNodeList"/> — the identical loop over <c>childNodes</c>, whose elements are the same
/// hundred nodes. It is a <c>NodeList</c>, so it reaches the generated accessor and a constant-time indexer,
/// and nothing in this change touches that lane. <b>The control that must not move.</b>
/// </description></item>
/// <item><description>
/// <see cref="PlainArray"/> — the floor and the baseline: the identical loop over the <c>Array.from</c> of
/// the same elements, so the gap is the price of the collection being a live projection.
/// </description></item>
/// </list>
/// <para>
/// <b>Engine isolation.</b> One <see cref="Page"/> — and therefore one engine and one document — per row,
/// built in <c>[GlobalSetup]</c> and given only that row's own fixture and its own warm-up, so a row's number
/// never depends on which sibling warmed which lane first. That matters more here than usual: every row
/// drives the same <c>ArrayLikeObject.TryGetIndex</c> call site, whose class profile holds one guess, and a
/// shared engine would hand whichever row ran first a monomorphic read and the rest a cold indirect call.
/// Page construction and the HTML parse stay outside the measurement; what the measured call adds over the
/// loop itself is one mailbox round trip per operation, which is the same term in every row including the
/// baseline.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class BrowserHtmlCollectionBenchmark
{
    /// <summary>
    /// 100 matching elements, the size <see cref="BrowserNodeListBenchmark"/> uses, in a document of about
    /// twice that many nodes so that a deep collection's filter rejects as well as accepts.
    /// </summary>
    [Params(100)]
    public int Count { get; set; }

    /// <summary>
    /// 10 passes of the inner loop per operation. The read is quadratic in the collection's length by
    /// construction — these collections are live, so nothing may memoize — so this is what keeps an
    /// operation in the low milliseconds rather than the tens.
    /// </summary>
    private const string Passes = "10";

    /// <summary>
    /// The loop, in shape from the wpt helper <see cref="BrowserNodeListBenchmark"/> takes it from: the match
    /// is at index 50, and the <c>break</c> is what makes an operation about half the collection.
    /// </summary>
    private const string Loop = $$"""
        (function () {
            var index = -1;
            for (var i = 0; i < {{Passes}}; i++) {
                for (var j = 0; j < c.length; j++) {
                    if (c[j] === el) { index = j; break; }
                }
            }
            return index;
        })()
        """;

    private Browser.Browser _browser = null!;
    private Page _className = null!;
    private Page _tagName = null!;
    private Page _formElements = null!;
    private Page _children = null!;
    private Page _liveNodeList = null!;
    private Page _plainArray = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _browser = new Browser.Browser();

        _className = await CreatePageAsync("var c = document.getElementsByClassName('foo');");
        _tagName = await CreatePageAsync("var c = document.getElementsByTagName('span');");
        _formElements = await CreatePageAsync("var c = document.getElementById('form').elements;");
        _children = await CreatePageAsync("var c = document.getElementById('root').children;");
        _liveNodeList = await CreatePageAsync("var c = document.getElementById('root').childNodes;");
        _plainArray = await CreatePageAsync("var c = Array.from(document.getElementsByClassName('foo'));");
    }

    /// <summary>
    /// One page holding <paramref name="bind"/>'s <c>c</c> and the element the loop looks for, warmed with
    /// this row's own loop and nothing else.
    /// </summary>
    private async Task<Page> CreatePageAsync(string bind)
    {
        var page = await _browser.NewPageAsync();
        await page.SetContentAsync(Document(Count));
        await page.EvaluateAsync<double>(bind + " var el = c[50]; 0;");
        await page.EvaluateAsync<double>(Loop);
        return page;
    }

    /// <summary>
    /// <paramref name="count"/> spans under one parent and <paramref name="count"/> controls in one form, so
    /// that every row's collection holds the same number of elements and every deep filter has as many
    /// non-matching elements to walk past as matching ones.
    /// </summary>
    private static string Document(int count)
    {
        var html = new StringBuilder("<!doctype html><html><body><div id=\"root\">");

        for (var i = 0; i < count; i++)
        {
            html.Append("<span class=\"foo\"></span>");
        }

        html.Append("</div><form id=\"form\">");

        for (var i = 0; i < count; i++)
        {
            html.Append("<input type=\"text\">");
        }

        return html.Append("</form></body></html>").ToString();
    }

    [Benchmark]
    public Task<double> ClassName() => _className.EvaluateAsync<double>(Loop);

    [Benchmark]
    public Task<double> TagName() => _tagName.EvaluateAsync<double>(Loop);

    [Benchmark]
    public Task<double> FormElements() => _formElements.EvaluateAsync<double>(Loop);

    [Benchmark]
    public Task<double> Children() => _children.EvaluateAsync<double>(Loop);

    [Benchmark]
    public Task<double> LiveNodeList() => _liveNodeList.EvaluateAsync<double>(Loop);

    [Benchmark(Baseline = true)]
    public Task<double> PlainArray() => _plainArray.EvaluateAsync<double>(Loop);

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
