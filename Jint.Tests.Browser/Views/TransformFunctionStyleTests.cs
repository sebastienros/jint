#nullable enable

using Jint.HtmlParser.Css.Model;
using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using Jint.Browser.Layout;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

// Transforms 2 §2.1 resolved matrices. Specified CSSOM retains function text.
// These do not claim rendered transforms; geometry continues to use untransformed flat boxes.
public sealed class TransformFunctionStyleTests
{
    [TestCase("translate(10px)", "matrix(1, 0, 0, 1, 10, 0)")]
    [TestCase("translateX(10px)", "matrix(1, 0, 0, 1, 10, 0)")]
    [TestCase("translateY(20px)", "matrix(1, 0, 0, 1, 0, 20)")]
    [TestCase("translateZ(30px)", "matrix3d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 30, 1)")]
    [TestCase("translate(10px, 20px)", "matrix(1, 0, 0, 1, 10, 20)")]
    [TestCase("translate3d(1px, 2px, 3px)", "matrix3d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 1, 2, 3, 1)")]
    [TestCase("scale(2)", "matrix(2, 0, 0, 2, 0, 0)")]
    [TestCase("scale(2, 3)", "matrix(2, 0, 0, 3, 0, 0)")]
    [TestCase("scale(50%, 200%)", "matrix(0.5, 0, 0, 2, 0, 0)")]
    [TestCase("perspective(100px)", "matrix3d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, -0.01, 0, 0, 0, 1)")]
    [TestCase("perspective(.25px)", "matrix3d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, -1, 0, 0, 0, 1)")]
    [TestCase("perspective(none)", "matrix(1, 0, 0, 1, 0, 0)")]
    [TestCase("matrix(2, 3, 4, 5, 6, 7)", "matrix(2, 3, 4, 5, 6, 7)")]
    [TestCase("translate(10px) rotate(90deg)", "matrix(0, 1, -1, 0, 10, 0)")]
    [TestCase("translate(10px, 20px) scale(2, 3)", "matrix(2, 0, 0, 3, 10, 20)")]
    [TestCase("scale(2, 3) translate(10px, 20px)", "matrix(2, 0, 0, 3, 20, 60)")]
    [TestCase("rotate3d(0, 0, 0, 45deg)", "matrix(1, 0, 0, 1, 0, 0)")]
    [TestCase("scale(1)", "matrix(1, 0, 0, 1, 0, 0)")]
    [TestCase("none", "none")]
    public async Task AbsoluteListsResolveWithoutRequestingAnySizeQuery(string declared, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='moved' style='transform:" + declared + "'>a</div>");
        await page.RunOnLoopAsync(engine =>
        {
            PageRuntime.Find(engine)!.Layout.Diagnostics = new();
            return true;
        });
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('moved')).getPropertyValue('transform')"))
            .Should().Be(expected);
        (await page.RunOnLoopAsync(engine => PageRuntime.Find(engine)!.Layout.Diagnostics!.SizeQueryRequests)).Should().Be(0);
        page.Errors.Should().BeEmpty();
    }

    [TestCase("translate(10px)", "translate(10px)")]
    [TestCase("translateX(10px)", "translateX(10px)")]
    [TestCase("scale(50%, 200%)", "scale(0.5, 2)")]
    [TestCase("perspective(.25px)", "perspective(0.25px)")]
    public async Task SpecifiedFunctionTextRemainsSeparateFromResolvedMatrices(string declared, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='moved' style='transform:" + declared + "'>a</div>");
        (await page.EvaluateAsync<string>("document.getElementById('moved').style.getPropertyValue('transform')"))
            .Should().Be(expected);
    }

    [TestCase("view-box")]
    [TestCase("border-box")]
    [TestCase("stroke-box")]
    public async Task PercentageTranslationsUseTheCurrentUntransformedFlatBox(string box)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div style='display:flex'><div id='moved' style='width:99px;height:999px;flex:1;"
            + "transform-box:" + box + ";transform:translate(calc(25% + 10px), -50%)'><span>a</span></div>"
            + "<div style='flex:1'>b</div></div>");
        var expected = await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var element = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "moved")!;
            var sizes = runtime.Layout.MeasureSizes();
            sizes.HasBox(element).Should().BeTrue();
            var measured = sizes.Measure(element);
            var style = CssCascade.Traversal.For(runtime.Document!)!.Of(element);
            // Format the expected coefficients from actual flat measurement, never authored dimensions.
            var work = new CssValueWork(default);
            var x = Jint.HtmlParser.Css.Values.Math.CssMathSerializer.SerializeFiniteNumber(measured.Width * .25 + 10, work);
            var y = Jint.HtmlParser.Css.Values.Math.CssMathSerializer.SerializeFiniteNumber(-measured.Height * .5, work);
            style.GetPropertyValue("transform").Should().Be("translate(calc(25% + 10px), -50%)");
            runtime.Layout.Diagnostics = new();
            return "matrix(1, 0, 0, 1, " + x + ", " + y + ")";
        });
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('moved')).getPropertyValue('transform')"))
            .Should().Be(expected);
        (await page.RunOnLoopAsync(engine => PageRuntime.Find(engine)!.Layout.Diagnostics!.SizeQueryRequests)).Should().Be(1);
        // A fresh live read after flex/DOM writes must use the new measured dimensions.
        await page.EvaluateAsync("document.getElementById('moved').style.flex = '3'; document.getElementById('moved').appendChild(document.createElement('span'))");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var element = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "moved")!;
            var measured = runtime.Layout.MeasureSizes().Measure(element);
            var result = engine.Evaluate("getComputedStyle(document.getElementById('moved')).getPropertyValue('transform')").AsString();
            var work = new CssValueWork(default);
            result.Should().Be("matrix(1, 0, 0, 1, "
                + Jint.HtmlParser.Css.Values.Math.CssMathSerializer.SerializeFiniteNumber(measured.Width * .25 + 10, work) + ", "
                + Jint.HtmlParser.Css.Values.Math.CssMathSerializer.SerializeFiniteNumber(-measured.Height * .5, work) + ")");
            return true;
        });
        page.Errors.Should().BeEmpty();
    }

    [TestCase("<div id='moved' style='display:none;transform:translateX(50%)'></div>", "C6:transform-reference-box")]
    [TestCase("<div id='moved' style='transform-box:content-box;transform:translateX(50%)'></div>", "C6:transform-content-box")]
    [TestCase("<div id='moved' style='transform-box:fill-box;transform:translateX(50%)'></div>", "C6:transform-content-box")]
    [TestCase("<svg><rect id='moved' style='transform:translateX(50%)'></rect></svg>", "C6:transform-svg-reference-box")]
    public async Task MissingReferenceBoxesNeverBecomeFakeZeroMatrices(string markup, string blocker)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(markup);
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var element = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "moved")!;
            var style = CssCascade.Traversal.For(runtime.Document!)!.Of(element);
            Action resolve = () => global::Jint.Browser.Dom.Views.ResolvedStyle.Transform(style, style.GetProperty("transform"), element, runtime);
            resolve.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be(blocker);
            return true;
        });
    }

    [Test]
    public async Task PercentageResolutionRequiresACurrentRuntimeAndAnAttachedBox()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='moved' style='transform:translateX(50%)'>a</div>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var element = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "moved")!;
            var style = CssCascade.Traversal.For(runtime.Document!)!.Of(element);
            var property = style.GetProperty("transform");
            Action noRuntime = () => ResolvedStyle.Transform(style, property, element, null);
            noRuntime.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be("C6:transform-current-document");

            var foreign = Document.CreateHtml();
            var other = foreign.CreateElement("div");
            foreign.AppendChild(other);
            other.SetAttribute("style", "transform:translateX(50%)");
            NativeCssStyleSheets.Associate(runtime.Dom, foreign);
            var foreignStyle = CssCascade.Traversal.For(foreign)!.Of(other);
            Action mismatch = () => ResolvedStyle.Transform(foreignStyle, foreignStyle.GetProperty("transform"), other, runtime);
            mismatch.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be("C6:transform-current-document");

            var detached = runtime.Document!.CreateElement("div");
            detached.SetAttribute("style", "transform:translateX(50%)");
            var detachedStyle = CssCascade.Traversal.For(runtime.Document!)!.Of(detached);
            Action noBox = () => ResolvedStyle.Transform(detachedStyle, detachedStyle.GetProperty("transform"), detached, runtime);
            noBox.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be("C6:transform-reference-box");
            return true;
        });
    }

    [Test]
    public async Task AQueryInvalidatedAfterComponentComputationCannotPublishAMatrix()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='moved' style='transform:translateX(10px)'>a</div>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var element = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "moved")!;
            var style = CssCascade.Traversal.For(runtime.Document!)!.Of(element);
            var property = style.GetProperty("transform");
            using (runtime.Dom.MutateLayout()) element.SetAttribute("style", "transform:translateX(20px)");
            Action stale = () => ResolvedStyle.Transform(style, property, element, runtime);
            stale.Should().Throw<InvalidOperationException>().WithMessage(NativeCssQuery.Invalidated);
            return true;
        });
    }

    [Test]
    public async Task IndividualPropertiesAndOriginsDoNotContributeAndGeometrySurvives()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div style='transform:scale(9);perspective:10px'><div id='moved' "
            + "style='translate:100px;scale:3;rotate:45deg;transform-origin:100px 200px;transform:translateX(10px)'>a</div></div>");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('moved')).getPropertyValue('transform')"))
            .Should().Be("matrix(1, 0, 0, 1, 10, 0)");
        (await page.EvaluateAsync<double>("document.getElementById('moved').getBoundingClientRect().height"))
            .Should().BeGreaterThan(0);
        page.Errors.Should().BeEmpty();
    }
}
