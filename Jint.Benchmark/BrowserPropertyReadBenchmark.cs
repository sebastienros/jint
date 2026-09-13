using BenchmarkDotNet.Attributes;
using Jint.Browser;

namespace Jint.Benchmark;

/// <summary>
/// A repeated read of six DOM properties on one element — <c>nodeName</c>, <c>nodeType</c>, <c>tagName</c>,
/// <c>parentNode</c>, <c>firstChild</c>, <c>ownerDocument</c> — is the workload an Ultra capture on
/// sebastienros/jint#4013 profiled at 8190 Hz (16 M iterations, net10.0). The accessor-invocation subtree was
/// 50.6% of the page-loop thread, and inside it <c>DomHostHooks.TagName</c> (14.1%) and
/// <c>DomHostHooks.NodeName</c> (15.3%, which delegates to <c>TagName</c> for an element) accounted for
/// roughly a quarter of the whole path between them — because both allocate a <c>char[]</c> and a
/// <c>string</c> to ASCII-uppercase the qualified name, and a <c>JsString</c> wrapper around the result, on
/// <b>every single read</b>. This class is the row that workload had none of: a benchmark for a plain DOM
/// property read.
/// </summary>
/// <remarks>
/// <para>
/// <b>What each row is.</b> <see cref="TagName"/> and <see cref="NodeName"/> are the subjects — the two
/// accessors the per-realm <c>DomRealm.HtmlUppercasedTagName</c> memo (internal to <c>Jint.Browser</c>, so
/// named here rather than linked) now caches per qualified name, so only the first read of a given element
/// interface allocates. <see cref="NodeType"/> and
/// <see cref="ParentNode"/> are controls: two other accessors on the same element, reached through the same
/// generated-shape dispatch and the same <c>Page.EvaluateAsync</c> mailbox round trip, that the memo must not
/// move at all — <c>NodeType</c> is a plain integer field with nothing to allocate, and <c>ParentNode</c> goes
/// through <c>DomRealm.WrapNode</c>'s wrapper-table probe rather than any string transform.
/// <see cref="PlainProperty"/> is the floor: the identical loop shape reading a same-named string property off
/// a plain JavaScript object, so the gap between it and the DOM rows is what projecting through the binding
/// costs at all, independent of anything this change touches.
/// </para>
/// <para>
/// <b>Pass count, and why the DOM rows and the floor row use different ones.</b> Per
/// <c>Jint.Benchmark/AGENTS.md</c>'s "A row through <c>Page.EvaluateAsync</c> must amortise the mailbox
/// round trip" (~60-70 µs and additive), every row loops its read rather than taking it once.
/// <see cref="DomPropertyPasses"/> (50,000) puts every DOM row — including <c>TagName</c>/<c>NodeName</c>
/// before this change, at a few hundred nanoseconds each because of the allocation — into the hundreds of
/// microseconds to low milliseconds, comfortably above the round trip. A plain object property read is far
/// cheaper again (single-digit nanoseconds), so <see cref="PlainPropertyPasses"/> (2,000,000) is sized
/// separately to keep the same margin for <see cref="PlainProperty"/>; collapsing the two constants into one
/// would either leave the floor round-trip-dominated or make the DOM rows needlessly slow to run.
/// </para>
/// <para>
/// <b>Engine isolation.</b> One <see cref="Page"/> — and therefore one engine and one document — per row,
/// built in <c>[GlobalSetup]</c> and warmed with only that row's own script, so no row's number depends on
/// which sibling ran first. Page construction and the HTML parse stay outside the measurement.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class BrowserPropertyReadBenchmark
{
    /// <summary>
    /// The pass count for the four DOM property rows. Even the pre-fix, allocating form of
    /// <c>tagName</c>/<c>nodeName</c> costs at most a few hundred nanoseconds per read, so 50,000 passes lands
    /// every row in the hundreds of microseconds to low milliseconds — well above the ~60-70 µs mailbox round
    /// trip <c>Page.EvaluateAsync</c> pays once per invocation.
    /// </summary>
    private const int DomPropertyPasses = 50_000;

    /// <summary>
    /// The pass count for <see cref="PlainProperty"/>. A plain object property read costs single-digit
    /// nanoseconds, so it needs roughly 40× <see cref="DomPropertyPasses"/> to keep the same margin over the
    /// round trip that the DOM rows get.
    /// </summary>
    private const int PlainPropertyPasses = 2_000_000;

    private Browser.Browser _browser = null!;
    private Page _tagName = null!;
    private Page _nodeName = null!;
    private Page _nodeType = null!;
    private Page _parentNode = null!;
    private Page _plainProperty = null!;

    private static readonly string TagNameScript = ReadStringLength("el.tagName", DomPropertyPasses);
    private static readonly string NodeNameScript = ReadStringLength("el.nodeName", DomPropertyPasses);
    private static readonly string NodeTypeScript = ReadNumber("el.nodeType", DomPropertyPasses);
    private static readonly string ParentNodeScript = ReadTruthy("el.parentNode", DomPropertyPasses);
    private static readonly string PlainPropertyScript = ReadStringLength("obj.tagName", PlainPropertyPasses);

    [GlobalSetup]
    public async Task Setup()
    {
        _browser = new Browser.Browser();

        _tagName = await CreatePageAsync(TagNameScript);
        _nodeName = await CreatePageAsync(NodeNameScript);
        _nodeType = await CreatePageAsync(NodeTypeScript);
        _parentNode = await CreatePageAsync(ParentNodeScript);
        _plainProperty = await CreatePageAsync(PlainPropertyScript, bindPlainObject: true);
    }

    /// <summary>
    /// A page holding <c>el</c> — the element every DOM row reads — and, when asked, a plain object of the
    /// same shape for <see cref="PlainProperty"/>, warmed with this row's own script and nothing else.
    /// </summary>
    private async Task<Page> CreatePageAsync(string script, bool bindPlainObject = false)
    {
        var page = await _browser.NewPageAsync();
        await page.SetContentAsync(Document);
        await page.EvaluateAsync<double>("var el = document.getElementById('el'); 0;");
        if (bindPlainObject)
        {
            await page.EvaluateAsync<double>("var obj = { tagName: 'DIV' }; 0;");
        }

        await page.EvaluateAsync<double>(script);
        return page;
    }

    /// <summary>One element under a parent, so <c>parentNode</c> answers a real node rather than null.</summary>
    private const string Document =
        "<!doctype html><html><body><div id=\"parent\"><div id=\"el\" class=\"widget\"></div></div></body></html>";

    /// <summary>
    /// <paramref name="expression"/>'s string length, read <paramref name="passes"/> times and summed, so the
    /// one mailbox round trip this script pays is amortised over every pass and nothing in the loop can be
    /// optimised away.
    /// </summary>
    private static string ReadStringLength(string expression, int passes) => $$"""
        (function () {
            var total = 0;
            for (var i = 0; i < {{passes}}; i++) {
                total += {{expression}}.length;
            }
            return total;
        })()
        """;

    /// <summary><paramref name="expression"/>'s own numeric value, looped and summed the same way.</summary>
    private static string ReadNumber(string expression, int passes) => $$"""
        (function () {
            var total = 0;
            for (var i = 0; i < {{passes}}; i++) {
                total += {{expression}};
            }
            return total;
        })()
        """;

    /// <summary>Whether <paramref name="expression"/> is truthy, looped and summed the same way.</summary>
    private static string ReadTruthy(string expression, int passes) => $$"""
        (function () {
            var total = 0;
            for (var i = 0; i < {{passes}}; i++) {
                total += {{expression}} ? 1 : 0;
            }
            return total;
        })()
        """;

    [Benchmark]
    public Task<double> TagName() => _tagName.EvaluateAsync<double>(TagNameScript);

    [Benchmark]
    public Task<double> NodeName() => _nodeName.EvaluateAsync<double>(NodeNameScript);

    [Benchmark]
    public Task<double> NodeType() => _nodeType.EvaluateAsync<double>(NodeTypeScript);

    [Benchmark]
    public Task<double> ParentNode() => _parentNode.EvaluateAsync<double>(ParentNodeScript);

    [Benchmark(Baseline = true)]
    public Task<double> PlainProperty() => _plainProperty.EvaluateAsync<double>(PlainPropertyScript);

    [GlobalCleanup]
    public async Task Cleanup() => await _browser.DisposeAsync();
}
