#nullable enable
using Jint.Browser.Dom.Views;
using Jint.Browser.Accessibility;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser.Css.Model;

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssContainerLayoutTests
{
    [Test]
    public async Task ConditionsReadActualFlexBoxesAndTrackSubsequentWidths()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@container panel (max-width:550px){#child{opacity:.5;flex-direction:column}}"
            + "</style><div style='display:flex'><div id=container style='container:panel / inline-size;width:500px;flex-shrink:0'>"
            + "<div id=child></div></div><div style='flex:1'></div></div>");
        const string read = "[container.getBoundingClientRect().width,getComputedStyle(container).width,"
            + "getComputedStyle(child).opacity,getComputedStyle(child).flexDirection].join('|')";
        (await page.EvaluateAsync<string>(read)).Should().Be("500|500px|0.5|column");
        await page.EvaluateAsync("container.style.width='600px'");
        (await page.EvaluateAsync<string>(read)).Should().Be("600|600px|1|row");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AuthoredBlockWidthNeverSubstitutesForTheSyntheticContainerBox()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@container panel (max-width:550px){#child{opacity:.5}}</style>"
            + "<div id=container style='container:panel / inline-size;width:10px'><div id=child></div></div>");
        (await page.EvaluateAsync<string>("[container.getBoundingClientRect().width,getComputedStyle(child).opacity].join('|')"))
            .Should().Be("1280|1");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            runtime.SetMedia(runtime.Media with { Viewport = new global::Jint.Browser.Viewport(500, 720) });
            return true;
        });
        (await page.EvaluateAsync<string>("[container.getBoundingClientRect().width,getComputedStyle(child).opacity].join('|')"))
            .Should().Be("500|0.5");
    }

    [Test]
    public async Task SharedContextKeepsIrrelevantPropertyReadsGeometryFree()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@container (width:1px){#child{font-size:12px}}</style>"
            + "<div style='container-type:inline-size'><div id=child></div></div>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var diagnostics = new NativeCssQueryDiagnostics();
            var traversal = CssCascade.Traversal.For(runtime.Document, diagnostics: diagnostics)!;
            var child = ContentDom.ElementById(runtime.Document!, "child")!;
            traversal.Of(child).GetPropertyValue("opacity").Should().Be("1");
            diagnostics.Queries.Should().HaveCount(1);
            diagnostics.Queries[0].ComputedPublications.Should().NotContainKey("container-type");
            diagnostics.Queries[0].ComputedPublications.Should().NotContainKey("font-size");
            return true;
        });
    }

    [Test]
    public async Task ShadowFlatTreeGeometryAndVerticalMetricsRemainNamedDependencies()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=host style='container:panel / inline-size'></div>");
        await page.EvaluateAsync("host.attachShadow({mode:'open'}).innerHTML="
            + "'<style>@container panel (width:1px){#child{opacity:.5}}</style><div id=child></div>'");
        await page.RunOnLoopAsync(engine =>
        {
            Assert.Throws<CssIncompleteGrammarException>(() => engine.Evaluate(
                "getComputedStyle(host.shadowRoot.getElementById('child')).opacity"))!
                .Blocker.Should().Be("C6:container-flat-tree-metric");
            return true;
        });
    }

    [Test]
    public async Task CssOmBrandAndConditionMetadataUseTheNativeContainerModel()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@container panel (max-width:550px){div{opacity:.5}}</style><div></div>");
        (await page.EvaluateAsync<string>("(() => { const r=document.styleSheets[0].cssRules[0];"
            + "return [Object.prototype.toString.call(r),r.containerName,r.containerQuery,r.conditionText,r.cssRules.length].join('|') })()"))
            .Should().Be("[object CSSContainerRule]|panel|(max-width:550px)|panel (max-width:550px)|1");
    }
}
