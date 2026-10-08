#nullable enable annotations

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
/// <b>Historical profile.</b> There was no row for an <c>HTMLCollection</c> at all, and an
/// <c>HTMLCollection</c> is not a <c>NodeList</c> with a different name: a <c>NodeList</c> arrives at the
/// binding as something that could be indexed in constant time, while every <c>HTMLCollection</c> the
/// former AngleSharp dependency supplied was a lazy tree walk — <c>Length</c> was a <c>Count()</c> and the
/// indexer is a linear scan of it. A profile of this loop on a 1,500-element document put
/// <c>NodeExtensions.GetDescendantsAndSelf.MoveNext</c> at 33.6% inclusive of the page thread, split three
/// ways between the loop's own <c>c.length</c>, the element read, and a bounds pre-check that ran the whole
/// query a second time before each element read. The rows below are what makes that visible; none of the
/// existing browser rows touched it.
/// </para>
/// <para>
/// <b>Current implementation.</b> Browser now uses Jint.HtmlParser. Root-backed tag/class collections and
/// child collections reuse counts under a document-identity/mutation-stamp witness; tag/class indexed
/// reads also retain a weak traversal cursor. Children still scan siblings for indexed access.
/// <c>form.elements</c> remains uncached and walks the ordinary root using native form-owner identities:
/// native custom-element category/owner changes can alter membership without advancing the tree stamp.
/// A document stamp alone is therefore insufficient for a form-membership cache. These rows measure
/// the current paths, not the historical three-walk implementation.
/// </para>
/// <para>
/// <b>What each row is.</b>
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="ClassName"/> — <c>getElementsByClassName</c>, whose filter the binding owns because DOM makes
/// its comparison ASCII case-insensitive in quirks mode. Its
/// document gives every element one three-character class, which is the <i>shortest</i> attribute the
/// filter can be handed.
/// </description></item>
/// <item><description>
/// <see cref="ClassNameMultiToken"/> — the same member over the class attribute a framework actually emits
/// (<c>class="btn btn-primary btn-lg active focus-visible"</c>, queried for its <b>last</b> token) and over
/// a document whose non-matching half carries a long attribute of its own, so the rejections cost what a
/// real page's do rather than being answered by an absent attribute. It is a second row rather than a
/// change to <see cref="ClassName"/> because that row has history: a per-character cost is invisible at
/// three characters, and a per-character <i>saving</i> would be too.
/// </description></item>
/// <item><description>
/// <see cref="TagName"/> — <c>getElementsByTagName</c>, the same live shape with a cheaper per-element test.
/// </description></item>
/// <item><description>
/// <see cref="FormElements"/> — <c>form.elements</c>, the Browser collection over native form-owner
/// identities. Counts and indexed reads each walk the ordinary root, filtering out non-listed controls
/// and image inputs. Membership includes externally associated controls, not just descendants of the form.
/// </description></item>
/// <item><description>
/// <see cref="Children"/> — <c>children</c>, the native sibling-chain view of immediate element children.
/// Counts are cached, but indexed reads still scan from the first child.
/// </description></item>
/// <item><description>
/// <see cref="LiveNodeList"/> — the same loop over <c>childNodes</c>, whose elements are the same hundred
/// nodes. It is a <c>NodeList</c>, so it reaches the generated accessor and native child-node cursor.
/// It is an independent collection lane and a control for changes confined to HTMLCollection reads.
/// </description></item>
/// <item><description>
/// <see cref="PlainArray"/> — the floor and the baseline: the same loop over the <c>Array.from</c> of the
/// same elements, so what a row has above it is the price of the collection being a live projection.
/// </description></item>
/// </list>
/// <para>
/// <b>Engine isolation.</b> One <see cref="Page"/> — and therefore one engine and one document — per row,
/// built in <c>[GlobalSetup]</c> and given only that row's own fixture and its own warm-up, so a row's number
/// never depends on which sibling warmed which lane first. That matters more here than usual: every row
/// drives the same <c>ArrayLikeObject.TryGetIndex</c> call site, whose class profile holds one guess, and a
/// shared engine would hand whichever row ran first a monomorphic read and the rest a cold indirect call.
/// Page construction and the HTML parse stay outside the measurement; what the measured call adds over the
/// loop itself is one mailbox round trip per operation.
/// </para>
/// <para>
/// <b>The pass counts differ per row, deliberately, and must not be tidied into one constant.</b> That
/// round trip is an <i>additive</i> per-invocation cost with its own scheduling jitter, so it does not
/// cancel against the baseline the way a multiplicative one does — see <b>"A row through
/// <c>Page.EvaluateAsync</c> must amortise the mailbox round trip"</b> in
/// <a href="AGENTS.md"><c>Jint.Benchmark/AGENTS.md</c></a>, which carries the rule and the paired run that
/// established it. The original target rows walked the document and were sized at ten passes.
/// The two <i>control</i> rows were the ones the rule first exposed: <c>childNodes</c> and the plain array
/// were much cheaper, so at ten passes each was a few microseconds of work behind a
/// round trip an order of magnitude larger — a row that could not have detected a regression in what it
/// controls for, and that measured as multimodal (<c>MValue</c> 3.56 on <see cref="PlainArray"/>) because
/// what it was mostly reporting was thread scheduling. The counts below retain those historical workloads;
/// count caching and native cursors have since reduced query work, so check mailbox amortisation again
/// before accepting timing evidence for a new change. These comments are not current timing results.
/// </para>
/// <para>
/// <b>What that costs, and it is the one trap here: a row is comparable to itself across builds, and to no
/// other row.</b> Because the counts differ, <c>Ratio</c> is not the price of a live projection per read —
/// it is that price times a pass-count ratio. The counts are chosen so the six means land within about
/// 1.7–2.5 ms of each other, which keeps the table readable, but that near-equality is arranged rather than
/// measured and means nothing on its own; that calibration predates the native migration. Read each row
/// against the same row on the other build; a paired
/// run (<c>measure-paired.ps1</c>) does exactly that and is the right instrument for this class.
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
    /// Historical pass count for the deep collection rows. Tag/class collections now cache counts and
    /// use weak indexed cursors; form.elements still walks. Retain the workload for comparisons across
    /// builds and recheck mailbox amortisation before quoting a result.
    /// </summary>
    private const int WalkPasses = 10;

    /// <summary>
    /// Historical pass count for the multi-token class fixture. Keep it independent of the short-token
    /// fixture so either workload can be resized after measuring its mailbox overhead.
    /// </summary>
    private const int MultiTokenPasses = 10;

    /// <summary>
    /// Historical pass count for the shallow child collection. Its indexed reads scan sibling links,
    /// unlike the weak descendant cursor of the tag/class rows.
    /// </summary>
    private const int ChildrenPasses = 60;

    /// <summary>
    /// <see cref="LiveNodeList"/> reads a <c>NodeList</c>, which is indexed in constant time, so a pass is
    /// microseconds rather than milliseconds and it takes hundreds of them to amortise the round trip. This
    /// is a <b>control</b> row, and a control that cannot resolve a change in what it controls for is worse
    /// than no control at all.
    /// </summary>
    private const int NodeListPasses = 350;

    /// <summary>
    /// <see cref="PlainArray"/> is the floor, and its pass costs very nearly what
    /// <see cref="NodeListPasses"/>' does — measured within about 10% of it, because at 51 iterations with a
    /// <c>c.length</c> read apiece the interpreter's own loop dominates either element read. Sized to land
    /// the floor in the same band as the rows it is a floor for, so the <c>Ratio</c> column means something.
    /// </summary>
    private const int ArrayPasses = 400;

    /// <summary>
    /// The loop, in shape from the wpt helper <see cref="BrowserNodeListBenchmark"/> takes it from: the match
    /// is at index 50, and the <c>break</c> is what makes an operation about half the collection.
    /// </summary>
    /// <remarks>
    /// The result accumulates the index found on <i>every</i> pass rather than overwriting one variable, so
    /// no pass is dead code — the interpreter hoists nothing today, but a row whose answer does not depend
    /// on its own loop is one runtime change away from measuring nothing.
    /// </remarks>
    private static string Loop(int passes) => $$"""
        (function () {
            var total = 0;
            for (var i = 0; i < {{passes}}; i++) {
                for (var j = 0; j < c.length; j++) {
                    if (c[j] === el) { total += j; break; }
                }
            }
            return total;
        })()
        """;

    private Browser.Browser _browser = null!;
    private CollectionRow _className = null!;
    private CollectionRow _classNameMultiToken = null!;
    private CollectionRow _tagName = null!;
    private CollectionRow _formElements = null!;
    private CollectionRow _children = null!;
    private CollectionRow _liveNodeList = null!;
    private CollectionRow _plainArray = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _browser = new Browser.Browser();

        _className = await CreateRowAsync("var c = document.getElementsByClassName('foo');", WalkPasses);
        _classNameMultiToken = await CreateRowAsync(
            "var c = document.getElementsByClassName('focus-visible');", MultiTokenPasses, MultiTokenDocument(Count));
        _tagName = await CreateRowAsync("var c = document.getElementsByTagName('span');", WalkPasses);
        _formElements = await CreateRowAsync("var c = document.getElementById('form').elements;", WalkPasses);
        _children = await CreateRowAsync("var c = document.getElementById('root').children;", ChildrenPasses);
        _liveNodeList = await CreateRowAsync("var c = document.getElementById('root').childNodes;", NodeListPasses);
        _plainArray = await CreateRowAsync(
            "var c = Array.from(document.getElementsByClassName('foo'));", ArrayPasses);
    }

    /// <summary>One row's page and the script it is measured with, whose pass count is this row's own.</summary>
    private sealed class CollectionRow(Page page, string script)
    {
        internal Task<double> Run() => page.EvaluateAsync<double>(script);
    }

    /// <summary>
    /// One page holding <paramref name="bind"/>'s <c>c</c> and the element the loop looks for, warmed with
    /// this row's own loop — at this row's own pass count — and nothing else. <paramref name="html"/> is the
    /// row's own fixture where it needs one; every row that does not name one shares <see cref="Document"/>'s
    /// shape, and none of them shares a page.
    /// </summary>
    private async Task<CollectionRow> CreateRowAsync(string bind, int passes, string? html = null)
    {
        var page = await _browser.NewPageAsync();
        var script = Loop(passes);
        await page.SetContentAsync(html ?? Document(Count));
        await page.EvaluateAsync<double>(bind + " var el = c[50]; 0;");
        await page.EvaluateAsync<double>(script);
        return new CollectionRow(page, script);
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

    /// <summary>
    /// The same <paramref name="count"/> matching elements and as many non-matching ones as
    /// <see cref="Document"/> has, with the class attributes of a page built out of a component library:
    /// five tokens, and the queried one last, so the filter answers only after reading the whole value.
    /// </summary>
    /// <remarks>
    /// The non-matching half carries a long attribute too, and one holding no occurrence of the queried
    /// class at all. In <see cref="Document"/> the non-matching elements are form controls with no class
    /// attribute, so they are rejected before the comparison starts; here a rejection costs a read of the
    /// whole value, which is what rejecting an element of a real page costs.
    /// </remarks>
    private static string MultiTokenDocument(int count)
    {
        var html = new StringBuilder("<!doctype html><html><body><div id=\"root\">");

        for (var i = 0; i < count; i++)
        {
            html.Append("<span class=\"btn btn-primary btn-lg active focus-visible\"></span>");
        }

        html.Append("</div><div id=\"other\">");

        for (var i = 0; i < count; i++)
        {
            html.Append("<span class=\"card card-body shadow-sm rounded border\"></span>");
        }

        return html.Append("</div></body></html>").ToString();
    }

    [Benchmark]
    public Task<double> ClassName() => _className.Run();

    [Benchmark]
    public Task<double> ClassNameMultiToken() => _classNameMultiToken.Run();

    [Benchmark]
    public Task<double> TagName() => _tagName.Run();

    [Benchmark]
    public Task<double> FormElements() => _formElements.Run();

    [Benchmark]
    public Task<double> Children() => _children.Run();

    [Benchmark]
    public Task<double> LiveNodeList() => _liveNodeList.Run();

    [Benchmark(Baseline = true)]
    public Task<double> PlainArray() => _plainArray.Run();

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
