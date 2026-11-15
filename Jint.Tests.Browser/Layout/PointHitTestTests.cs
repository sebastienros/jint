using Jint.Browser;
using Jint.Browser.Dom;
using Jint.Browser.Layout;
using Jint.Browser.Runtime;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Layout;

public sealed class PointHitTestTests
{
    private const string Markup = """
        <style>
        #row {display:flex;align-items:center}
        #left {width:160px;flex-shrink:0}
        #right {flex-grow:1}
        #nested {display:flex;flex-direction:row-reverse}
        #nested>div {width:120px;flex-shrink:0}
        #tall {align-self:stretch}
        #before {display:none}
        :focus ~ #row {direction:rtl}
        body:has(:active) #right {visibility:hidden}
        #empty:empty ~ #after {display:none}
        </style>
        <button id="focus">focus</button><div id="before">hidden</div>
        <div id="row"><div id="left"><span>A</span><span>B</span></div>
        <div id="right"><div id="nested"><div><span>C</span></div><div>D</div></div></div>
        <div id="tall">E</div></div>
        <div id="empty">text</div><button id="after">after</button>
        """;

    [TestCase("")]
    [TestCase("document.getElementById('row').style.flexDirection='column'")]
    [TestCase("document.getElementById('row').style.alignItems='flex-end'")]
    [TestCase("document.getElementById('before').style.display='block'")]
    [TestCase("document.getElementById('left').hidden=true")]
    [TestCase("document.getElementById('left').style.visibility='hidden'")]
    [TestCase("document.getElementById('empty').firstChild.data=''")]
    [TestCase("document.getElementById('row').append(document.getElementById('focus'))")]
    [TestCase("document.getElementById('focus').focus()")]
    [TestCase("document.styleSheets[0].insertRule('#row {display:none}',0)")]
    [TestCase("document.querySelector('style').media='print'")]
    [TestCase("scrollTo(0,1000);document.getElementById('row').remove()")]
    public async Task PointQueriesAgreeWithCompleteLayoutAfterChanges(string change)
    {
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions { Viewport = new(200, 160) });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        await page.EvaluateAsync("document.getElementById('after').getBoundingClientRect()");
        await page.EvaluateAsync(change);
        await Compare(page);
        await page.SetViewportAsync(new(260, 128));
        await Compare(page);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task MousePhasesKeepFreshHitTargetsWithoutConstructingCompleteLayouts()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <style>button:active{color:red} button:focus{color:blue}</style>
            <button id="target">Save</button>
            <script>
            window.events=[];window.capture=true;
            for(const type of ['pointermove','pointerdown','pointerup','click'])
              document.addEventListener(type,e=>{if(capture)events.push([
                type,e.isTrusted,e.target.id,
                document.elementFromPoint(e.clientX,e.clientY)===e.target
              ].join(':'));});
            </script>
            """);
        await page.RunOnLoopAsync(engine =>
        {
            PageRuntime.Find(engine)!.Layout.Diagnostics = new();
            return 0;
        });
        (await page.ClickAsync("#target")).Should().BeTrue();
        (await page.EvaluateAsync<string>("events.join('|')")).Should().Be(
            "pointermove:true:target:true|pointerdown:true:target:true|pointerup:true:target:true|click:true:target:true");
        // The script's own elementFromPoint calls deliberately request complete layouts.
        // A second click without them proves the native path alone does not.
        await page.EvaluateAsync("capture=false;document.body.innerHTML='<button id=\"target\">Save</button>'");
        var before = await page.RunOnLoopAsync(engine =>
            PageRuntime.Find(engine)!.Layout.Diagnostics!.FullLayouts);
        (await page.ClickAsync("#target")).Should().BeTrue();
        var counts = await page.RunOnLoopAsync(engine =>
        {
            var diagnostics = PageRuntime.Find(engine)!.Layout.Diagnostics!;
            return (diagnostics.FullLayouts, diagnostics.PointQueries);
        });
        counts.FullLayouts.Should().Be(before);
        counts.PointQueries.Should().BeGreaterThanOrEqualTo(8);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task PressAndFocusStylesCanStillChangeTheReleaseTarget()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        foreach (var selector in new[] { "#target:active", "#target:focus", "body:focus-within #target", "body:has(#target:active) #target" })
        {
            await page.SetContentAsync($"<style>{selector}{{display:none}}</style><button id='target'>Save</button><button id='next'>Next</button>");
            await page.EvaluateAsync("""
                window.released='';
                document.addEventListener('pointerup',e=>{
                  released=e.target.id;
                  if(document.elementFromPoint(e.clientX,e.clientY)!==e.target)throw Error('stale hit');
                });
                """);
            (await page.ClickAsync("#target")).Should().BeFalse(selector);
            (await page.EvaluateAsync<string>("released")).Should().Be("next", selector);
            page.Errors.Should().BeEmpty();
        }
    }

    [Test]
    public async Task PointTraversalHonorsCancellationAndCheckpointFailures()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup + string.Concat(Enumerable.Repeat("<div>row</div>", 600)));
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            var sizes = new FlatLayout.SizeQuery(runtime.Document, runtime.Layout.Visibility, 200, null,
                token: canceled.Token);
            Action read = () => sizes.ElementFromPoint(10, 10);
            read.Should().Throw<OperationCanceledException>();
            var failure = new InvalidOperationException("checkpoint");
            sizes = new(runtime.Document, runtime.Layout.Visibility, 200, null, () => throw failure);
            read.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(failure);
            using var during = new CancellationTokenSource();
            var checks = 0;
            sizes = new(runtime.Document, runtime.Layout.Visibility, 200, null,
                () => { if (++checks == 2) during.Cancel(); }, during.Token);
            read.Should().Throw<OperationCanceledException>();
            checks.Should().BeGreaterThanOrEqualTo(2);
            return 0;
        });
    }

    private static async Task Compare(Page page)
    {
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var point = runtime.Layout.ReadPoint();
            var viewport = runtime.Viewport;
            var complete = FlatLayout.Of(runtime.Document, runtime.Layout.Visibility,
                viewport.Width, viewport.Height, runtime.Layout.ScrollY,
                checkpoint: engine.Constraints.Check, token: runtime.Dom.CancellationToken);
            for (var y = -1d; y <= viewport.Height + 1; y += 7)
            for (var x = -1d; x <= viewport.Width + 1; x += 17)
            {
                point.ElementFromPoint(x, y).Should().BeSameAs(complete.ElementFromPoint(x, y), $"point {x},{y}");
            }
            foreach (var edge in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0, viewport.Width })
            {
                point.ElementFromPoint(edge, 0).Should().BeSameAs(complete.ElementFromPoint(edge, 0));
                point.ElementFromPoint(0, edge).Should().BeSameAs(complete.ElementFromPoint(0, edge));
            }
            var pending = new Stack<Node>();
            pending.Push(runtime.Document!);
            while (pending.TryPop(out var node))
            {
                if (node is Element element)
                    point.ClientBoxOf(element).Should().Be(complete.ClientBoxOf(element));
                for (var child = node.FirstChild; child is not null; child = child.NextSibling) pending.Push(child);
            }
            return 0;
        });
    }
}
