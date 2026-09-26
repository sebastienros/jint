#nullable enable
using Jint.Browser.Dom.Views;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

[NonParallelizable]
public sealed class NativeCssConsumerTests
{
    [Test]
    public async Task WarmedGeometryRefreshesForCssomAndNativeStyleSourceChanges()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style id='source'>#box { display:block; width:10px; height:20px; }</style><div id='box'></div>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var target = document.GetElementById("box")!;
            var owner = document.GetElementById("source")!;
            runtime.Layout.ClientBoxOf(target)!.Value.Width.Should().Be(10);
            var warmed = runtime.Layout.MeasureSizes();
            runtime.Layout.MeasureSizes().Should().BeSameAs(warmed);
            var stamp = document.MutationStamp;
            var sheet = NativeCssStyleSheets.SheetOf(runtime.Dom, owner)!;
            ((CssStyleRule) sheet.Rules[0]).Style.SetProperty("width", "25px");
            document.MutationStamp.Should().Be(stamp);
            runtime.Layout.ClientBoxOf(target)!.Value.Width.Should().Be(25);
            runtime.Layout.MeasureSizes().Should().NotBeSameAs(warmed);
            ((Text) owner.FirstChild!).Data = "#box { display:block; width:40px; height:20px; }";
            runtime.Layout.ClientBoxOf(target)!.Value.Width.Should().Be(40);

            var link = document.CreateElement("link");
            link.SetAttribute("rel", "stylesheet");
            document.DocumentElement!.AppendChild(link);
            var work = new CssValueWork(default);
            NativeCssStyleSheets.Install(document, link, "#box { width:50px; }", "", "", work);
            runtime.Layout.ClientBoxOf(target)!.Value.Width.Should().Be(50);
            stamp = document.MutationStamp;
            NativeCssStyleSheets.Install(document, link, "#box { width:60px; }", "", "", work);
            document.MutationStamp.Should().Be(stamp);
            runtime.Layout.ClientBoxOf(target)!.Value.Width.Should().Be(60);
            return true;
        });
    }

    [Test]
    public async Task CoverageSweepIncludesRulesMatchedInsideShadowTrees()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='host'></div>");
        await page.RunOnLoopAsync(engine =>
        {
            var document = PageRuntime.Find(engine)!.Document!;
            var host = document.GetElementById("host")!;
            var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
            var style = document.CreateElement("style");
            style.AppendChild(document.CreateTextNode("span { opacity:.5; }"));
            shadow.AppendChild(style);
            shadow.AppendChild(document.CreateElement("span"));
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
            var element = document.GetElementById("box")!;
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
