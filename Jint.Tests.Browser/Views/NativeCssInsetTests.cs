#nullable enable

using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssInsetTests
{
    [Test]
    public async Task StaticInsetsKeepFontLengthsPercentagesAndMixedMathWithoutLayout()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<span id=target style='font-size:20px;top:-9999em;right:-25%;bottom:0;left:calc(25% - 2em)'>text</span>");
        await page.RunOnLoopAsync(engine => { PageRuntime.Find(engine)!.Layout.Diagnostics = new(); return true; });
        (await page.EvaluateAsync<string>("(() => { const s=getComputedStyle(document.getElementById('target'));"
            + "return [s.top,s.right,s.bottom,s.left].join('|'); })()"))
            .Should().Be("-199980px|-25%|0px|calc(25% - 40px)");
        (await page.RunOnLoopAsync(engine => PageRuntime.Find(engine)!.Layout.Diagnostics!.SizeQueryRequests)).Should().Be(0);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AllFourInitialValuesAreAutoAndTheSetterSupportsTheJQueryHiddenFormValue()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<form id=target></form>");
        (await page.EvaluateAsync<string>("(() => { const s=getComputedStyle(document.getElementById('target'));"
            + "return [s.top,s.right,s.bottom,s.left].join('|'); })()"))
            .Should().Be("auto|auto|auto|auto");
        (await page.EvaluateAsync<string>("(() => { const f=document.getElementById('target');"
            + "f.style.position='absolute'; f.style.left='-9999em'; return f.style.position+'|'+f.style.left; })()"))
            .Should().Be("absolute|-9999em");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("relative")]
    [TestCase("absolute")]
    [TestCase("fixed")]
    [TestCase("sticky")]
    public async Task PositionedBoxesRequireTheNamedPositioningDependency(string position)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=target style='position:" + position + ";left:25%'>text</div>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var element = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "target")!;
            var style = CssCascade.Traversal.For(runtime.Document!)!.Of(element);
            style.GetProperty("left").Text.Should().Be("25%");
            Action resolve = () => ResolvedStyle.Inset(style, style.GetProperty("left"), element, runtime);
            resolve.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be("C6:positioned-inset");
            return true;
        });
    }

    [TestCase("none")]
    [TestCase("contents")]
    public async Task APositionedElementWithoutItsOwnBoxKeepsItsComputedInsetWithoutLayout(string display)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=target style='display:" + display + ";position:relative;left:25%'>text</div>");
        await page.RunOnLoopAsync(engine => { PageRuntime.Find(engine)!.Layout.Diagnostics = new(); return true; });
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('target')).left"))
            .Should().Be("25%");
        (await page.RunOnLoopAsync(engine => PageRuntime.Find(engine)!.Layout.Diagnostics!.SizeQueryRequests)).Should().Be(0);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task APositionedElementInAHiddenOrDisconnectedTreeKeepsItsComputedInset()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div style='display:none'><span id=target style='position:absolute;left:25%'>text</span></div>");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('target')).left"))
            .Should().Be("25%");
        (await page.EvaluateAsync<string>("(() => { const e=document.createElement('div');"
            + "e.style.position='absolute';e.style.left='-5%';return getComputedStyle(e).left; })()"))
            .Should().Be("-5%");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public void ADeepDisconnectedConnectionWalkPollsBeyondTheShallowReadAndCancels()
    {
        var shallowPolls = Read(0, null);
        shallowPolls.Should().BeGreaterThan(0);
        // Cancel after more polls than the entire shallow read needs. With all three
        // properties warm, the extra work is the connection walk through detached ancestors.
        Action deep = () => Read(32768, shallowPolls + 1);
        deep.Should().Throw<OperationCanceledException>();

        static int Read(int depth, int? cancelAt)
        {
            var document = Document.CreateHtml();
            var element = document.CreateElement("span");
            var root = element;
            for (var i = 0; i < depth; i++)
            {
                var parent = document.CreateElement("span");
                parent.AppendChild(root);
                root = parent;
            }
            using var cancellation = new CancellationTokenSource();
            var armed = false;
            var polls = 0;
            var work = new CssValueWork(cancellation.Token, () =>
            {
                if (armed && ++polls == cancelAt) cancellation.Cancel();
            });
            var query = new NativeCssQuery(document, [],
                [(element, CssDeclarationBlock.Parse("position:relative;display:inline;left:25%"))],
                new CssMediaEnvironment(), new(document, null, null, null), CssEnvironmentSnapshot.Create([], work), work);
            var style = new NativeCssComputedStyle(query, element, new SelectorMatchWork(document, cancellation.Token));
            var property = style.GetProperty("left");
            style.GetProperty("position");
            style.GetProperty("display");
            armed = true;
            ResolvedStyle.Inset(style, property, element, null).Should().Be("25%");
            return polls;
        }
    }
}
