#nullable enable

using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser.Css.Model;

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

// CSSOM §9, CSS Sizing 3 §3.1 and CSS2 §10.1 over the documented synthetic flat renderer.
public sealed class ResolvedValueStyleTests
{
    [TestCase("auto", "auto")]
    [TestCase("40px", "12px")]
    [TestCase("50%", "25%")]
    [TestCase("calc(50% - 10px)", "calc(25% + 3px)")]
    [TestCase("20ch", "20ex")]
    public async Task ApplicableBlockDimensionsAgreeWithRectanglesForEveryAuthoredValue(string width, string height)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<div id=t style='width:{width};height:{height}'>a</div>");
        (await page.EvaluateAsync<string>("(() => { const el = document.getElementById('t'); const s = getComputedStyle(el);"
            + "const r = el.getBoundingClientRect(); return [s.width,s.height,r.width,r.height].join('|'); })()"))
            .Should().Be("1280px|16px|1280|16");
        (await page.EvaluateAsync<string>("t.style.width")).Should().Be(width);
        (await page.EvaluateAsync<string>("t.style.height")).Should().Be(height);
        page.Errors.Should().BeEmpty();
    }

    [TestCase("inline", "auto", "auto")]
    [TestCase("inline", "50%", "25%")]
    [TestCase("none", "40px", "12px")]
    [TestCase("contents", "40px", "12px")]
    public async Task InapplicableDimensionsKeepComputedValues(string display, string width, string height)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<span id=t style='display:{display};width:{width};height:{height}'>a</span>");
        (await page.EvaluateAsync<string>("[getComputedStyle(t).width,getComputedStyle(t).height].join('|')"))
            .Should().Be(width + "|" + height);
    }

    [Test]
    public async Task SupportedReplacedInlineUsesTheSameSyntheticBox()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<img id=t style='width:40px;height:12px'>");
        (await page.EvaluateAsync<string>("[getComputedStyle(t).width,getComputedStyle(t).height].join('|')"))
            .Should().Be("1280px|16px");
    }

    [Test]
    public async Task NestedPercentageEdgesUseFlexAssignedContainingWidthOnAllFourSides()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div style='display:flex'><div id=cb style='width:200px;flex-shrink:0'>"
            + "<span><div id=t style='margin:10%;padding:calc(10% - 2px)'>a</div></span></div>"
            + "<div style='width:100px'>b</div></div>");
        (await page.EvaluateAsync<string>("(() => { const s = getComputedStyle(t); return "
            + "['top','right','bottom','left'].map(side => s.getPropertyValue('margin-'+side)+'/'+s.getPropertyValue('padding-'+side)).join('|'); })()"))
            .Should().Be("20px/18px|20px/18px|20px/18px|20px/18px");
        (await page.EvaluateAsync<string>("[getComputedStyle(cb).width,cb.getBoundingClientRect().width,getComputedStyle(t).width].join('|')"))
            .Should().Be("200px|200|200px");
        (await page.EvaluateAsync<string>("t.style.margin")).Should().Be("10%");
    }

    [TestCase("margin-left", "calc(10% - 200px)", "-72px")]
    [TestCase("padding-bottom", "calc(10% - 200px)", "0px")]
    [TestCase("margin-top", "auto", "0px")]
    [TestCase("min-height", "auto", "0px")]
    [TestCase("min-width", "auto", "0px")]
    [TestCase("max-width", "50%", "50%")]
    public async Task OrdinaryUsedEdgesAndMinimaFollowTheirFinitePolicy(string name, string value, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<div id=t style='{name}:{value}'>a</div>");
        (await page.EvaluateAsync<string>($"getComputedStyle(t).getPropertyValue('{name}')")).Should().Be(expected);
    }

    [TestCase("<div style='display:flex'><div id=t></div></div>", "min-width", "C6:automatic-minimum")]
    [TestCase("<div style='display:grid'><div id=t></div></div>", "min-height", "C6:automatic-minimum")]
    [TestCase("<div id=t style='aspect-ratio:2/1'></div>", "min-width", "V2:aspect-ratio")]
    [TestCase("<style>#t { aspect-ratio:auto }</style><div id=t></div>", "min-height", "V2:aspect-ratio")]
    [TestCase("<div style='display:flex'><div id=t style='margin-left:auto'></div></div>", "margin-left", "C6:auto-margin-used-value")]
    [TestCase("<div id=t style='position:absolute;margin-top:10%'></div>", "margin-top", "C6:positioned-containing-block")]
    [TestCase("<div id=t style='position:relative;margin-top:auto'></div>", "margin-top", "C6:auto-margin-used-value")]
    [TestCase("<svg><rect id=t style='padding-top:10%'></rect></svg>", "padding-top", "C6:svg-containing-block")]
    [TestCase("<div id=t style='writing-mode:vertical-rl;padding-top:10%'></div>", "padding-top", "C6:writing-mode")]
    public async Task MissingUsedMetricsStayExplicitAndDemandLocal(string content, string name, string blocker)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(content);
        (await page.EvaluateAsync<string>("getComputedStyle(t).opacity")).Should().Be("1");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var element = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "t")!;
            var style = CssCascade.Traversal.For(runtime.Document)!.Of(element);
            Action read = () => ResolvedStyle.ValueOf(name, style, element, runtime);
            read.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be(blocker);
            return true;
        });
    }

    [Test]
    public async Task UnrelatedReadsAndAbsoluteEdgesNeverRequestSizes()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=t style='width:20ch;margin-left:-2px;padding-top:3px'>a</div>");
        await page.RunOnLoopAsync(engine => { PageRuntime.Find(engine)!.Layout.Diagnostics = new(); return true; });
        (await page.EvaluateAsync<string>("(() => { const s=getComputedStyle(t);return [s.opacity,s.color,s.marginLeft,s.paddingTop].join('|'); })()"))
            .Should().Be("1|rgb(0, 0, 0)|-2px|3px");
        (await page.RunOnLoopAsync(engine => PageRuntime.Find(engine)!.Layout.Diagnostics!.SizeQueryRequests)).Should().Be(0);
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var element = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "t")!;
            Action native = () => CssCascade.Traversal.For(runtime.Document)!.Of(element).GetPropertyValue("width");
            native.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be("C6:zero-advance");
            return true;
        });
    }

    [Test]
    public async Task LiveReadsObserveCssomCustomPropertiesValidityAndMediaAfterWarmGeometry()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#cb{width:var(--size);flex-shrink:0} #cb:invalid{width:300px}"
            + "@media (max-width:600px){#cb{width:400px}}</style><div style='display:flex'>"
            + "<input id=cb style='--size:200px'><div id=t style='margin-top:10%'>a</div></div>");
        // Each percentage edge's containing block is the flex row, while cb's width follows its own cascade.
        (await page.EvaluateAsync<string>("getComputedStyle(cb).width")).Should().Be("200px");
        await page.EvaluateAsync("cb.style.setProperty('--size','250px')");
        (await page.EvaluateAsync<string>("getComputedStyle(cb).width")).Should().Be("250px");
        await page.EvaluateAsync("cb.required=true");
        (await page.EvaluateAsync<string>("getComputedStyle(cb).width")).Should().Be("300px");
        await page.EvaluateAsync("document.styleSheets[0].cssRules[1].style.width='350px'");
        (await page.EvaluateAsync<string>("getComputedStyle(cb).width")).Should().Be("350px");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            runtime.SetMedia(runtime.Media with { Viewport = new global::Jint.Browser.Viewport(500, 720) });
            return true;
        });
        (await page.EvaluateAsync<string>("getComputedStyle(cb).width")).Should().Be("400px");
        (await page.EvaluateAsync<string>("getComputedStyle(t).marginTop")).Should().Be("50px");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReentrantMutationOrMediaChangeCannotPublishAUsedSize(bool media)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=t>a</div>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            runtime.Layout.Diagnostics = new();
            var element = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "t")!;
            var armed = false;
            var changed = false;
            var style = CssCascade.Traversal.For(runtime.Document, checkpoint: () =>
            {
                if (!armed || changed || runtime.Layout.Diagnostics.SizeQueryRequests == 0) return;
                changed = true;
                if (media) runtime.SetMedia(runtime.Media with { Viewport = new global::Jint.Browser.Viewport(600, 720) });
                else { using (runtime.Dom.MutateLayout()) element.SetAttribute("hidden", ""); }
            })!.Of(element);
            armed = true;
            Action read = () => ResolvedStyle.ValueOf("width", style, element, runtime);
            read.Should().Throw<InvalidOperationException>().WithMessage(NativeCssQuery.Invalidated);
            changed.Should().BeTrue();
            return true;
        });
    }

    [Test]
    public async Task WidthDoesNotVisitDescendantStylesOrRequestItsComputedAuthoredValue()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=t style='width:20ch'><div id=child>a</div></div>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var element = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "t")!;
            var child = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "child")!;
            var diagnostics = new NativeCssQueryDiagnostics(captureDetails: true);
            var cascade = CssCascade.Traversal.For(runtime.Document, diagnostics: diagnostics)!;
            var sizes = new global::Jint.Browser.Layout.FlatLayout.SizeQuery(runtime.Document, runtime.Layout.Visibility,
                runtime.Viewport.Width, cascade, engine.Constraints.Check, runtime.Dom.CancellationToken);
            sizes.Width(element).Should().Be(1280);
            var query = diagnostics.Queries.Single();
            query.Elements!.ContainsKey(child).Should().BeFalse();
            query.ComputedPublications.ContainsKey("width").Should().BeFalse();
            query.ComputedPublications.ContainsKey("height").Should().BeFalse();
            sizes.Height(element).Should().Be(32);
            query.ComputedPublications.ContainsKey("width").Should().BeFalse();
            query.Elements!.ContainsKey(child).Should().BeTrue();
            return true;
        });
    }

    [Test]
    public async Task BoxlessAndDetachedDimensionsAndMinimaKeepTheirOwnRules()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div hidden><div id=t style='width:40px;height:12px;aspect-ratio:2/1'></div></div>");
        (await page.EvaluateAsync<string>("(() => { const s=getComputedStyle(t);return [s.width,s.height,s.minWidth].join('|'); })()"))
            .Should().Be("40px|12px|0px");
        (await page.EvaluateAsync<string>("(() => { const el=document.createElement('div');el.style.width='50%';"
            + "const s=getComputedStyle(el);return [s.width,s.minHeight].join('|'); })()"))
            .Should().Be("50%|0px");
    }

    [Test]
    public async Task TheRootUsesTheInitialContainingBlocksActualViewport()
    {
        await using var browser = new Browser(new global::Jint.Browser.BrowserOptions
            { Viewport = new global::Jint.Browser.Viewport(500, 720) });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>html { padding-top:10%;margin-bottom:10% }</style><div>a</div>");
        (await page.EvaluateAsync<string>("(() => { const s=getComputedStyle(document.documentElement);return [s.paddingTop,s.marginBottom].join('|'); })()"))
            .Should().Be("50px|50px");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CancellationCannotPublishAUsedSize(bool duringMeasurement)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=t>a</div>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var element = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "t")!;
            using var cancellation = new CancellationTokenSource();
            runtime.Layout.Diagnostics = new();
            var armed = false;
            var style = CssCascade.Traversal.For(runtime.Document, cancellationToken: cancellation.Token, checkpoint: () =>
            {
                if (armed && runtime.Layout.Diagnostics.SizeQueryRequests != 0) cancellation.Cancel();
            })!.Of(element);
            armed = duringMeasurement;
            if (!duringMeasurement) cancellation.Cancel();
            Action read = () => ResolvedStyle.ValueOf("height", style, element, runtime);
            read.Should().Throw<OperationCanceledException>();
            return true;
        });
    }
}
