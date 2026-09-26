#nullable enable

using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser.Css.Model;

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

    [Test]
    public async Task APositionedElementWithoutABoxKeepsItsComputedInset()
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
}
