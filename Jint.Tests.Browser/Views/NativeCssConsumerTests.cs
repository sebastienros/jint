#nullable enable
using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using Jint.Browser.Layout;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

[NonParallelizable]
public sealed class NativeCssConsumerTests
{
    [Test]
    public async Task DeclaredNamedColorsRetainKeywordsWhileComputedColorsSerializeAsRgb()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='box' style='color:ReD'></div>");
        // CSS Color 4 §16.2: declared keywords and computed sRGB values serialize differently.
        (await page.EvaluateAsync<string>("""
            (() => {
                const box = document.getElementById('box');
                return box.style.color + '|' + getComputedStyle(box).color;
            })()
            """)).Should().Be("red|rgb(255, 0, 0)");
        (await page.EvaluateAsync<string>("""
            (() => {
                const box = document.getElementById('box');
                box.style.color = 'blue';
                return box.style.color + '|' + getComputedStyle(box).color;
            })()
            """)).Should().Be("blue|rgb(0, 0, 255)");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task LinkDisabledReflectsItsAttributeWhileStyleDisabledUsesAssociatedMetadata()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='container'></div>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var link = document.CreateElement("link");
            NativeCssBindings.SetStyleDisabled(runtime.Dom, link, true);
            link.GetAttribute("disabled").Should().NotBeNull();
            NativeCssBindings.StyleDisabled(runtime.Dom, link).Should().BeTrue();
            NativeCssBindings.SetStyleDisabled(runtime.Dom, link, false);
            link.GetAttribute("disabled").Should().BeNull();
            var style = document.CreateElement("style");
            NativeCssBindings.SetStyleDisabled(runtime.Dom, style, true);
            NativeCssBindings.StyleDisabled(runtime.Dom, style).Should().BeFalse();
            var container = DomDocumentReads.ById(runtime.Dom, document, "container")!;
            container.AppendChild(style);
            NativeCssStyleSheets.Install(runtime.Dom, style, "", "");
            NativeCssBindings.SetStyleDisabled(runtime.Dom, style, true);
            NativeCssBindings.StyleDisabled(runtime.Dom, style).Should().BeTrue();
            style.GetAttribute("disabled").Should().BeNull();
            return true;
        });
    }

    [Test]
    public async Task SaturatedInlineCssomEditPreservesSourceAndAuthoritativeBlock()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='box' style='--o:hidden scroll;overflow:var(--o)'></div>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var target = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "box")!;
            var work = new CssValueWork(default);
            var factory = typeof(NativeCssStyleSheets).GetMethod("InlineResourceOf", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            var resource = factory.Invoke(null, [target])!;
            resource.GetType().GetField("Version", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(resource, ulong.MaxValue);
            var retained = NativeCssStyleSheets.InlineOf(target, work);
            var source = target.GetAttribute("style");
            Assert.Throws<InvalidOperationException>(() => NativeCssDeclarations.Of(runtime.Dom, target).SetProperty("overflow-x", "visible"));
            target.GetAttribute("style").Should().Be(source);
            NativeCssStyleSheets.InlineOf(target, work).Should().BeSameAs(retained);
            return true;
        });
    }

    [Test]
    public async Task InlineCssomPreservesPendingShorthandAndSameValueSourceWritesReparse()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='box' style='--o:hidden scroll;overflow:var(--o)'></div>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var target = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "box")!;
            var declaration = NativeCssDeclarations.Of(runtime.Dom, target);
            declaration.SetProperty("overflow-x", "visible");
            CssCascade.ValueOf(CssCascade.Of(target)!, "overflow-y").Should().Be("scroll");
            target.SetAttribute("id", "box2");
            CssCascade.ValueOf(CssCascade.Of(target)!, "overflow-y").Should().Be("scroll");
            var source = target.GetAttribute("style")!;
            source.Should().NotContain("overflow-y:");
            target.SetAttribute("style", source);
            declaration.GetPropertyValue("overflow-y").Should().BeEmpty();
            return true;
        });
    }

    [Test]
    public async Task WarmedGeometryRefreshesForCssomAndNativeStyleSourceChanges()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <style id='source'>#box { display:block; width:10px; height:20px; flex-basis:auto; flex-grow:0; flex-shrink:0; }</style>
            <link id='extra' rel='stylesheet' href='data:text/css,'>
            <div style='display:flex'><div id='box'></div></div>
            """);
        (await page.WaitForIdleAsync(TestBudgets.WedgeCeiling)).Should().BeTrue();
        // Install read-only diagnostics; measuring inside RunOnLoopAsync intentionally cannot reuse
        // queries. All measurements below instead use the ordinary public evaluation lane.
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            FlatLayout.SizeQuery? previous = null;
            var queries = 0;
            engine.SetValue("queryStamp", () =>
            {
                var current = runtime.Layout.MeasureSizes();
                if (!ReferenceEquals(previous, current))
                {
                    previous = current;
                    queries++;
                }
                return queries;
            });
            engine.SetValue("nativeStamp", () => document.MutationStamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return true;
        });
        (await page.EvaluateAsync<string>("""
            [document.getElementById('box').clientWidth, queryStamp(), queryStamp()].join(',')
            """)).Should().Be("10,1,1");
        (await page.EvaluateAsync<string>("""
            (() => {
                const before = queryStamp(), nativeBefore = nativeStamp();
                document.getElementById('source').sheet.cssRules[0].style.width = '25px';
                const width = document.getElementById('box').clientWidth, after = queryStamp();
                return [width, before !== after, after === queryStamp(), nativeBefore === nativeStamp()].join(',');
            })()
            """)).Should().Be("25,true,true,true");
        (await page.EvaluateAsync<string>("""
            (() => {
                const before = queryStamp();
                document.getElementById('source').firstChild.data =
                    '#box { display:block; width:40px; height:20px; flex-basis:auto; flex-grow:0; flex-shrink:0; }';
                const width = document.getElementById('box').clientWidth, after = queryStamp();
                return [width, before !== after, after === queryStamp()].join(',');
            })()
            """)).Should().Be("40,true,true");

        foreach (var width in new[] { 50, 60 })
        {
            await page.EvaluateAsync<int>("globalThis.previousQueryStamp = queryStamp()");
            // Publish through the resource-only lane: no DOM write or conservative mutation scope
            // can invalidate the warm query, so the resource revision must refresh it.
            await page.RunResourcePublicationOnLoopAsync(engine =>
            {
                var runtime = PageRuntime.Find(engine)!;
                var document = runtime.Document!;
                var link = DomDocumentReads.ById(runtime.Dom, document, "extra")!;
                NativeCssStyleSheets.SheetOf(runtime.Dom, link).Should().NotBeNull();
                var stamp = document.MutationStamp;
                var resources = NativeCssStyleSheets.Stamp(document);
                NativeCssStyleSheets.Install(document, link, "#box { width:" + width + "px; }", "", "", new CssValueWork(default));
                document.MutationStamp.Should().Be(stamp);
                NativeCssStyleSheets.Stamp(document).Should().NotBe(resources);
                return true;
            });
            (await page.EvaluateAsync<string>("""
                (() => {
                    const width = document.getElementById('box').clientWidth, after = queryStamp();
                    return [width, previousQueryStamp !== after, after === queryStamp()].join(',');
                })()
                """)).Should().Be(width + ",true,true");
        }
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task CoverageSweepIncludesRulesMatchedInsideShadowTrees()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='host'></div>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var host = DomDocumentReads.ById(runtime.Dom, document, "host")!;
            var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
            var style = document.CreateElement("style");
            style.AppendChild(document.CreateTextNode("span { opacity:.5; }"));
            shadow.AppendChild(style);
            shadow.AppendChild(document.CreateElement("span"));
            // Raw native fixture insertion has completed; publish its actual source before sweeping.
            NativeCssStyleSheets.Install(runtime.Dom, style, "span { opacity:.5; }", "");
            var tracker = new CssRuleUsageTracker();
            tracker.Rebind(document);
            tracker.Sweep();
            tracker.TakeDelta().Select(rule => rule.SelectorText).Should().Equal("span");
            tracker.Sweep();
            tracker.TakeDelta().Should().BeEmpty();
            return true;
        });
    }

    [Test]
    public async Task RetainedComputedDeclarationReadsFeedNewRulesIntoCoverageDeltas()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>.before { opacity:.1; } .after { opacity:.5; }</style>"
            + "<p id='box' class='before'>text</p>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var element = DomDocumentReads.ById(runtime.Dom, document, "box")!;
            var retained = new ReadOnlyStyleDeclaration(runtime, element);
            retained.GetPropertyValue("opacity").Should().Be("0.1");
            var tracker = new CssRuleUsageTracker();
            tracker.Rebind(document);
            CssRuleUsage.Arm(tracker);
            try
            {
                retained.GetPropertyValue("opacity").Should().Be("0.1");
                tracker.TakeDelta().Select(rule => rule.SelectorText).Should().Equal(".before");
                element.SetAttribute("class", "after");
                retained.GetPropertyValue("opacity").Should().Be("0.5");
                tracker.TakeDelta().Select(rule => rule.SelectorText).Should().Equal(".after");
                retained.GetPropertyValue("opacity").Should().Be("0.5");
                tracker.TakeDelta().Should().BeEmpty();
            }
            finally
            {
                CssRuleUsage.Disarm(tracker);
            }
            return true;
        });
    }
}
