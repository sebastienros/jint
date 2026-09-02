using Jint.Browser;

namespace Jint.Tests.Browser.Layout;

/// <summary>
/// The flat box model, as a page reads it.
/// </summary>
/// <remarks>
/// <para>
/// Every assertion here is arithmetic on one number — the row height — and on tree order, which is the whole
/// point of the model: a client and a page can both work out where a box will be without a layout engine
/// having laid anything out. The document a <c>SetContentAsync</c> produces is
/// <c>html &gt; head, body &gt; …</c>, and <c>&lt;head&gt;</c> is not rendered, so the first three rows are
/// always <c>html</c>, <c>body</c> and whatever the fixture put first in the body.
/// </para>
/// <para>
/// The viewport is small on purpose in the scrolling tests: a document has to be taller than its window
/// before a scroll offset can be anything but zero.
/// </para>
/// </remarks>
public class FlatLayoutTests
{
    private const int Row = 16;

    [Test]
    public async Task EveryRenderedElementOwnsOneRowAndAContainerSpansItsSubtree()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='outer'><span id='inner'>hello</span></div>");

        // html, body, div, span: four rendered elements, so the document is four rows tall.
        (await Rect(page, "document.documentElement")).Should().Be($"0,0,1280,{4 * Row}");
        (await Rect(page, "document.body")).Should().Be($"0,{Row},1280,{3 * Row}");
        (await Rect(page, "document.getElementById('outer')")).Should().Be($"0,{2 * Row},1280,{2 * Row}");
        (await Rect(page, "document.getElementById('inner')")).Should().Be($"0,{3 * Row},1280,{Row}");
    }

    [Test]
    public async Task BoxesNestAndNeverStraddle()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='a'><p id='b'></p><p id='c'></p></div><div id='d'></div>");

        var nested = await page.EvaluateAsync<bool>(
            """
            (() => {
              const box = id => document.getElementById(id).getBoundingClientRect();
              const inside = (child, parent) => child.top >= parent.top && child.bottom <= parent.bottom;
              const a = box('a'), b = box('b'), c = box('c'), d = box('d');
              return inside(b, a) && inside(c, a) && b.bottom <= c.top && a.bottom <= d.top;
            })()
            """);

        nested.Should().BeTrue("a container's box covers its subtree and two siblings never overlap");
    }

    [Test]
    public async Task AnElementWithNoBoxAnswersZerosAndNoClientRects()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="shown">a</div>
            <div id="hidden" hidden><span id="child">b</span></div>
            <div id="none" style="display: none">c</div>
            <div id="invisible" style="visibility: hidden">d</div>
            <script id="code">void 0;</script>
            """);

        foreach (var id in new[] { "hidden", "child", "none", "invisible", "code" })
        {
            (await Rect(page, $"document.getElementById('{id}')")).Should().Be("0,0,0,0", "'{0}' is not rendered", id);
            (await page.EvaluateAsync<int>($"document.getElementById('{id}').getClientRects().length"))
                .Should().Be(0, "'{0}' has no box, so it covers no client rectangles", id);
            (await page.EvaluateAsync<int>($"document.getElementById('{id}').offsetHeight")).Should().Be(0);
        }

        // And the one that is rendered still is, on the row right after the body.
        (await Rect(page, "document.getElementById('shown')")).Should().Be($"0,{2 * Row},1280,{Row}");
    }

    [Test]
    public async Task ElementFromPointHitsTheLeafAtItsCentreAndADescendantAtAContainers()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='outer'><span id='inner'>hello</span></div>");

        // The leaf's own row: its centre hits itself.
        (await page.EvaluateAsync<string>(
            $"document.elementFromPoint(10, {(3 * Row) + (Row / 2)}).id")).Should().Be("inner");

        // The container spans two rows, so the centre of its box falls in its child's row -- which is what a
        // browser does too, and is why a click on a container reaches the thing inside it and bubbles back.
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const box = document.getElementById('outer').getBoundingClientRect();
              return document.elementFromPoint(box.left + box.width / 2, box.top + box.height / 2).id;
            })()
            """)).Should().Be("inner");

        // Below everything, and outside the viewport, nothing is hit.
        (await page.EvaluateAsync<bool>("document.elementFromPoint(10, 900) === null")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("document.elementFromPoint(-1, 10) === null")).Should().BeTrue();
    }

    [Test]
    public async Task ElementsFromPointIsTheHitAndItsRenderedAncestors()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='outer'><span id='inner'>hello</span></div>");

        (await page.EvaluateAsync<string>(
            $"[...document.elementsFromPoint(10, {(3 * Row) + 1})].map(e => e.tagName).join(',')"))
            .Should().Be("SPAN,DIV,BODY,HTML");
    }

    [Test]
    public async Task TheScrollOffsetIsVirtualAndEveryClientRectSubtractsIt()
    {
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions { Viewport = new Viewport(800, 64) });
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(Rows(20));

        // Twenty rows plus html and body is 22 rows of 16, which is 352 against a 64-pixel window.
        (await page.EvaluateAsync<int>("document.scrollingElement.scrollHeight")).Should().Be(22 * Row);
        (await page.EvaluateAsync<int>("document.scrollingElement.clientHeight")).Should().Be(64);

        var before = await page.EvaluateAsync<double>("document.getElementById('r10').getBoundingClientRect().top");

        await page.EvaluateAsync("window.scrollTo(0, 100)");

        (await page.EvaluateAsync<double>("window.scrollY")).Should().Be(100);
        (await page.EvaluateAsync<double>("window.pageYOffset")).Should().Be(100);
        (await page.EvaluateAsync<double>("document.scrollingElement.scrollTop")).Should().Be(100);
        (await page.EvaluateAsync<double>("document.getElementById('r10').getBoundingClientRect().top"))
            .Should().Be(before - 100);

        // The horizontal half does not exist, whatever a page asks for.
        (await page.EvaluateAsync<double>("window.scrollX")).Should().Be(0);

        // And it is clamped to the document: scrolling past the end stops at the end.
        await page.EvaluateAsync("window.scrollTo(0, 100000)");
        (await page.EvaluateAsync<double>("window.scrollY")).Should().Be((22 * Row) - 64);
    }

    [Test]
    public async Task WritingScrollTopOnTheScrollingElementScrollsThePage()
    {
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions { Viewport = new Viewport(800, 64) });
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(Rows(20));

        await page.EvaluateAsync("document.scrollingElement.scrollTop = 48");
        (await page.EvaluateAsync<double>("window.scrollY")).Should().Be(48);

        // And on anything else it does nothing at all, because no element here has content larger than its box.
        await page.EvaluateAsync("document.getElementById('r3').scrollTop = 200");
        (await page.EvaluateAsync<double>("window.scrollY")).Should().Be(48);
        (await page.EvaluateAsync<double>("document.getElementById('r3').scrollTop")).Should().Be(0);
    }

    [Test]
    public async Task ScrollIntoViewBringsAnElementsRowToTheTop()
    {
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions { Viewport = new Viewport(800, 64) });
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(Rows(20));

        await page.EvaluateAsync("document.getElementById('r15').scrollIntoView()");

        // r15 is the seventeenth rendered element (html, body, r0 … r15), so its row starts at 17 * 16.
        (await page.EvaluateAsync<double>("window.scrollY")).Should().Be(17 * Row);
        (await page.EvaluateAsync<double>("document.getElementById('r15').getBoundingClientRect().top")).Should().Be(0);

        // 'nearest' leaves a row that is already inside the window alone.
        await page.EvaluateAsync("document.getElementById('r16').scrollIntoView({ block: 'nearest' })");
        (await page.EvaluateAsync<double>("window.scrollY")).Should().Be(17 * Row);

        // …and 'end' puts it against the bottom of the window instead.
        await page.EvaluateAsync("document.getElementById('r15').scrollIntoView({ block: 'end' })");
        (await page.EvaluateAsync<double>("window.scrollY")).Should().Be((18 * Row) - 64);
    }

    [Test]
    public async Task AScrollFiresOneEventPerTurnAtTheDocument()
    {
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions { Viewport = new Viewport(800, 64) });
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            Rows(20) +
            """
            <script>
              window.log = [];
              window.addEventListener('scroll', e => window.log.push(e.type + ':' + (e.target === document)));
            </script>
            """);

        await page.EvaluateAsync("window.scrollTo(0, 32); window.scrollTo(0, 48); window.scrollBy(0, 16)");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        (await page.EvaluateAsync<double>("window.scrollY")).Should().Be(64);
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("scroll:true");
    }

    [Test]
    public async Task OffsetMetricsAreMeasuredFromTheBodyAndDoNotMoveWithTheScroll()
    {
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions { Viewport = new Viewport(800, 64) });
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(Rows(20));

        (await page.EvaluateAsync<string>("document.getElementById('r3').offsetParent.tagName")).Should().Be("BODY");
        (await page.EvaluateAsync<bool>("document.body.offsetParent === null")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("document.documentElement.offsetParent === null")).Should().BeTrue();

        var top = await page.EvaluateAsync<double>("document.getElementById('r3').offsetTop");
        top.Should().Be(4 * Row, "r3 is the fifth rendered element and the body is the second");

        await page.EvaluateAsync("window.scrollTo(0, 32)");
        (await page.EvaluateAsync<double>("document.getElementById('r3').offsetTop")).Should().Be(top);
        (await page.EvaluateAsync<double>("document.getElementById('r3').offsetWidth")).Should().Be(800);
        (await page.EvaluateAsync<double>("document.getElementById('r3').offsetHeight")).Should().Be(Row);
    }

    /// <summary>The four numbers of a rectangle, as one string, which makes a failure readable.</summary>
    private static async Task<string> Rect(Page page, string expression)
        => await page.EvaluateAsync<string>(
            $"(() => {{ const r = ({expression}).getBoundingClientRect(); return [r.x, r.y, r.width, r.height].join(','); }})()")
           ?? "";

    private static string Rows(int count)
        => string.Concat(Enumerable.Range(0, count).Select(i => $"<div id='r{i}'>row {i}</div>"));
}
