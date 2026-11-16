using System.Text;
using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>
/// The five shapes an axe-core scan of a large page spent its time in (sebastienros/jint#4619), each over a
/// <b>real page</b> of the issue's 700-card document — about 10,500 elements.
/// </summary>
/// <remarks>
/// <para>
/// <b>What each row is.</b>
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="InheritedGlobal"/> — a bare <c>hasOwnProperty</c>, which resolves on <c>Object.prototype</c>
/// below the window's named-properties object. That object used to walk the whole document looking for an
/// element named <c>hasOwnProperty</c> on every read. 200 passes; each one used to be a full document walk.
/// </description></item>
/// <item><description>
/// <see cref="ChildrenIndexedLoop"/> — <c>for (j &lt; ul.children.length) ul.children[j]</c> over the 700-item
/// list, which read each child from the first one. 10 passes of 700 reads.
/// </description></item>
/// <item><description>
/// <see cref="RepeatedMatches"/> — <c>el.matches(sel)</c> with the same three selectors for every card's link,
/// which recompiled each selector per call. One pass of 2,100 calls is already far above the round trip.
/// </description></item>
/// <item><description>
/// <see cref="ArgumentsPastLength"/> — the default-parameter idiom <c>arguments[1] !== undefined</c> on a
/// one-argument call, which materialized the whole <c>arguments</c> object. 20,000 calls, because one is
/// cheap enough that it must be looped.
/// </description></item>
/// <item><description>
/// <see cref="IndexOfNode"/> — <c>nodes.indexOf(node)</c> over an array of every element, searching for the
/// last one: the dense scan that compared every element through a virtual <c>Equals</c>. 20 passes.
/// </description></item>
/// <item><description>
/// <see cref="NthChildOfManySiblings"/> — <c>li.matches('li:nth-child(k)')</c> for every item of the 700-item list
/// and ten values of <c>k</c>, the shape of axe-core's unique-selector generation: each call is its own traversal,
/// which indexed all 700 siblings to answer one position. 7,000 calls.
/// </description></item>
/// <item><description>
/// <see cref="Control"/> — an arithmetic loop that reaches none of the five changes, sized like the others: it
/// is the cross-process floor, and a delta on it is not a result.
/// </description></item>
/// </list>
/// <para>
/// <b>Engine isolation.</b> One <see cref="Page"/> — and therefore one engine and one document — per row, built
/// in <c>[GlobalSetup]</c> and warmed with that row's own script and nothing else. Page construction and the
/// HTML parse stay outside the measurement; what each call adds over its loop is one mailbox round trip, which
/// every row's pass count is sized to make negligible.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class BrowserAuditPatternsBenchmark
{
    private const string InheritedGlobalScript = """
        (function () {
            var n = 0;
            for (var i = 0; i < 200; i++) { if (typeof hasOwnProperty === 'function') n++; }
            return n;
        })()
        """;

    private const string ChildrenScript = """
        (function () {
            var list = document.getElementById('list'), last = list.lastElementChild, n = 0;
            for (var p = 0; p < 10; p++) {
                for (var j = 0; j < list.children.length; j++) { if (list.children[j] === last) n++; }
            }
            return n;
        })()
        """;

    private const string MatchesScript = """
        (function () {
            var links = document.querySelectorAll('article > a'), n = 0;
            for (var i = 0; i < links.length; i++) {
                var el = links[i];
                if (el.matches('a[href]')) n++;
                if (el.matches('button, [role="button"]')) n++;
                if (el.matches('li > article > a:not([hidden])')) n++;
            }
            return n;
        })()
        """;

    private const string ArgumentsScript = """
        (function () {
            function pick(a) { return arguments[1] !== undefined ? arguments[1] : a; }
            var n = 0;
            for (var i = 0; i < 20000; i++) n += pick(1);
            return n;
        })()
        """;

    private const string IndexOfScript = """
        (function () {
            var nodes = globalThis.__nodes || (globalThis.__nodes = Array.from(document.getElementsByTagName('*')));
            var last = nodes[nodes.length - 1], n = 0;
            for (var i = 0; i < 20; i++) n += nodes.indexOf(last);
            return n;
        })()
        """;

    private const string NthChildScript = """
        (function () {
            var items = document.getElementById('list').children, n = 0;
            for (var k = 1; k <= 700; k += 70) {
                var selector = 'li:nth-child(' + k + ')';
                for (var j = 0; j < items.length; j++) { if (items[j].matches(selector)) n++; }
            }
            return n;
        })()
        """;

    private const string ControlScript = """
        (function () {
            var n = 0;
            for (var i = 0; i < 200000; i++) n = (n + i * 2) % 1000003;
            return n;
        })()
        """;

    private Browser.Browser _browser = null!;
    private Page _inheritedGlobal = null!;
    private Page _children = null!;
    private Page _matches = null!;
    private Page _arguments = null!;
    private Page _indexOf = null!;
    private Page _nthChild = null!;
    private Page _control = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _browser = new Browser.Browser(new BrowserOptions { MaxTaskDuration = TimeSpan.FromMinutes(5) });
        _inheritedGlobal = await CreatePageAsync(InheritedGlobalScript);
        _children = await CreatePageAsync(ChildrenScript);
        _matches = await CreatePageAsync(MatchesScript);
        _arguments = await CreatePageAsync(ArgumentsScript);
        _indexOf = await CreatePageAsync(IndexOfScript);
        _nthChild = await CreatePageAsync(NthChildScript);
        _control = await CreatePageAsync(ControlScript);
    }

    private async Task<Page> CreatePageAsync(string script)
    {
        var page = await _browser.NewPageAsync();
        await page.SetContentAsync(Document());
        await page.EvaluateAsync<double>(script);
        return page;
    }

    /// <summary>The issue's document: 700 cards of an image, a heading, a paragraph, a tag list and a checkbox.</summary>
    private static string Document()
    {
        var html = new StringBuilder("<!doctype html><html lang=en><title>audit</title><main><h1>Resources</h1><ul id=list>");
        for (var i = 0; i < 700; i++)
        {
            html.Append("<li><article><a href=\"/r/").Append(i).Append("/\"><img src=\"data:image/gif;base64,R0lGODlhAQABAAAAACw=\" alt=\"Resource ")
                .Append(i).Append("\" width=40 height=40><h3>Resource ").Append(i).Append("</h3></a><p>Description of resource ").Append(i)
                .Append(", with <strong>emphasis</strong> and <code>code</code>.</p><ul><li><span>tag</span></li><li><span>tag</span></li></ul>")
                .Append("<label><input type=checkbox> Pick ").Append(i).Append("</label></article></li>");
        }

        return html.Append("</ul></main>").ToString();
    }

    [Benchmark]
    public Task<double> InheritedGlobal() => _inheritedGlobal.EvaluateAsync<double>(InheritedGlobalScript);

    [Benchmark]
    public Task<double> ChildrenIndexedLoop() => _children.EvaluateAsync<double>(ChildrenScript);

    [Benchmark]
    public Task<double> RepeatedMatches() => _matches.EvaluateAsync<double>(MatchesScript);

    [Benchmark]
    public Task<double> ArgumentsPastLength() => _arguments.EvaluateAsync<double>(ArgumentsScript);

    [Benchmark]
    public Task<double> IndexOfNode() => _indexOf.EvaluateAsync<double>(IndexOfScript);

    [Benchmark]
    public Task<double> NthChildOfManySiblings() => _nthChild.EvaluateAsync<double>(NthChildScript);

    [Benchmark(Baseline = true)]
    public Task<double> Control() => _control.EvaluateAsync<double>(ControlScript);

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
