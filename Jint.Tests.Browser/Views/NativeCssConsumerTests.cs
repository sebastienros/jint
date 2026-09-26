#nullable enable
using Jint.Browser.Dom.Views;
using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

[NonParallelizable]
public sealed class NativeCssConsumerTests
{
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
