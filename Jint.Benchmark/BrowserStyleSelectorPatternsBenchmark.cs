using System.Text;
using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>
/// The style-rule shapes that made computed style the largest cost of an axe-core scan of aspire.dev's
/// browse page (about 14,600 elements): each row reads <c>getComputedStyle(h3).color</c> for the 700 cards of
/// <see cref="BrowserAuditPatternsBenchmark"/>'s document under one style sheet.
/// </summary>
/// <remarks>
/// <para>
/// <b>What each row is.</b>
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="HasInsideNot"/> — <c>html:not(:has(.missing)) h3</c>. A <c>:has()</c> inside a logical function
/// kept the whole selector on the frame-allocating evaluator, and the root's <c>:has()</c> walked the document
/// again for every element the rule was tried on.
/// </description></item>
/// <item><description>
/// <see cref="ParentHas"/> — <c>li:has(> article > .missing) h3</c>: each card asks its own item, so the
/// per-anchor answer cannot be reused, and each candidate of the walk allocated frames.
/// </description></item>
/// <item><description>
/// <see cref="NestingReference"/> — <c>.card { &amp; h3 { } }</c>: a nesting reference always used the general
/// evaluator, even when its parent list is one compound.
/// </description></item>
/// <item><description>
/// <see cref="RootHasMiss"/> — <c>:root:has(.missing) { }</c>, tried on every element: the evaluator allocated
/// its frames before it found the subject is not the root.
/// </description></item>
/// <item><description>
/// <see cref="Control"/> — the same reads under ordinary class rules, which none of the changes reach: the
/// cross-process floor, and a delta on it is not a result.
/// </description></item>
/// </list>
/// <para>
/// <b>Engine isolation.</b> One <see cref="Page"/> per row, built in <c>[GlobalSetup]</c> and warmed with that
/// row's own script. Each call first toggles an attribute on <c>&lt;body&gt;</c>, so the cascade is computed
/// afresh rather than read back from the previous call's traversal.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class BrowserStyleSelectorPatternsBenchmark
{
    private const string ReadScript = """
        (function () {
            document.body.toggleAttribute('data-pass');
            var titles = document.getElementsByTagName('h3'), n = 0;
            for (var i = 0; i < titles.length; i++) n += getComputedStyle(titles[i]).color.length;
            return n;
        })()
        """;

    private Browser.Browser _browser = null!;
    private Page _hasInsideNot = null!;
    private Page _parentHas = null!;
    private Page _nesting = null!;
    private Page _rootHasMiss = null!;
    private Page _control = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _browser = new Browser.Browser(new BrowserOptions { MaxTaskDuration = TimeSpan.FromMinutes(5) });
        _hasInsideNot = await CreatePageAsync("html:not(:has(.missing)) h3 { color: rgb(1, 2, 3) }");
        _parentHas = await CreatePageAsync("li:has(> article > .missing) h3 { color: rgb(1, 2, 3) }");
        _nesting = await CreatePageAsync(".card { & h3 { color: rgb(1, 2, 3) } & p { color: rgb(4, 5, 6) } }");
        _rootHasMiss = await CreatePageAsync(":root:has(.missing) { color: rgb(1, 2, 3) } h3 { color: rgb(4, 5, 6) }");
        _control = await CreatePageAsync(".card h3 { color: rgb(1, 2, 3) } .card p { color: rgb(4, 5, 6) }");
    }

    private async Task<Page> CreatePageAsync(string css)
    {
        var page = await _browser.NewPageAsync();
        await page.SetContentAsync(Document(css));
        await page.EvaluateAsync<double>(ReadScript);
        return page;
    }

    private static string Document(string css)
    {
        var html = new StringBuilder("<!doctype html><html lang=en><title>styles</title><style>").Append(css)
            .Append("</style><body><main><h1>Resources</h1><ul id=list>");
        for (var i = 0; i < 700; i++)
        {
            html.Append("<li><article class=card><a href=\"/r/").Append(i).Append("/\"><img src=\"data:image/gif;base64,R0lGODlhAQABAAAAACw=\" alt=\"Resource ")
                .Append(i).Append("\" width=40 height=40><h3>Resource ").Append(i).Append("</h3></a><p>Description of resource ").Append(i)
                .Append(", with <strong>emphasis</strong> and <code>code</code>.</p><ul><li><span>tag</span></li><li><span>tag</span></li></ul>")
                .Append("<label><input type=checkbox> Pick ").Append(i).Append("</label></article></li>");
        }

        return html.Append("</ul></main></body>").ToString();
    }

    [Benchmark]
    public Task<double> HasInsideNot() => _hasInsideNot.EvaluateAsync<double>(ReadScript);

    [Benchmark]
    public Task<double> ParentHas() => _parentHas.EvaluateAsync<double>(ReadScript);

    [Benchmark]
    public Task<double> NestingReference() => _nesting.EvaluateAsync<double>(ReadScript);

    [Benchmark]
    public Task<double> RootHasMiss() => _rootHasMiss.EvaluateAsync<double>(ReadScript);

    [Benchmark(Baseline = true)]
    public Task<double> Control() => _control.EvaluateAsync<double>(ReadScript);

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
