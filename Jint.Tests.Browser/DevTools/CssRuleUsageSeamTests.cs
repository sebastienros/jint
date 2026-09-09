#nullable enable

using Jint.Browser.Dom.Views;
using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.DevTools;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The cascade seam rule-usage coverage records through, and what it costs a page nobody is tracking.
/// </summary>
/// <remarks>
/// <para>
/// The protocol suite next door asserts what a client is told; this asserts the one thing a client cannot
/// see and an edit can silently undo — that <b>nothing is recorded, and no tracker is even consulted, while
/// no window is open</b>. It is written as a record count rather than as a memory measurement on purpose: a
/// cascade allocates plenty on its own, so a byte counter around one would be measuring AngleSharp, while a
/// window that was armed only afterwards and still holds nothing is a direct statement that the disarmed
/// path recorded nothing at all.
/// </para>
/// <para>
/// It is <c>[NonParallelizable]</c> because the seam's arming list is process-wide, and a coverage window
/// opened by another fixture's page would be armed while this one asserts that nothing is.
/// </para>
/// </remarks>
[NonParallelizable]
public sealed class CssRuleUsageSeamTests
{
    private const string Styled =
        "<style>.used { color: rgb(1, 2, 3) } .unused { color: rgb(4, 5, 6) }</style>"
        + "<p id='box' class='used'>text</p>";

    [Test]
    public async Task ACascadeRecordsNothingWhileNoWindowIsOpen()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Styled);

        var recorded = await page.RunOnLoopAsync(engine =>
        {
            var document = PageRuntime.Find(engine)!.Document!;
            var box = document.GetElementById("box")!;

            CssRuleUsage.IsTracking.Should().BeFalse("no client has asked for coverage");

            // Exactly the computation getComputedStyle, every box the flat model measures and the
            // accessibility tree's hidden verdict all come through.
            for (var i = 0; i < 8; i++)
            {
                CssCascade.Of(box).Should().NotBeNull();
            }

            var tracker = new CssRuleUsageTracker();
            tracker.Rebind(document);

            // Armed only now, and asked what the eight cascades above left in it.
            return tracker.TakeDelta().Length;
        });

        recorded.Should().Be(0, "a page nobody is tracking records nothing, and the seam is one static read");
    }

    [Test]
    public async Task AnArmedWindowRecordsTheRulesACascadeMatchedAndStopsWhenDisarmed()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Styled);

        var (whileArmed, afterDisarm) = await page.RunOnLoopAsync(engine =>
        {
            var document = PageRuntime.Find(engine)!.Document!;
            var box = document.GetElementById("box")!;

            var tracker = new CssRuleUsageTracker();
            tracker.Rebind(document);
            CssRuleUsage.Arm(tracker);

            try
            {
                CssRuleUsage.IsTracking.Should().BeTrue();
                CssCascade.Of(box).Should().NotBeNull();

                // A second cascade over the same element adds nothing: a rule is used once.
                CssCascade.Of(box).Should().NotBeNull();
                var armed = tracker.TakeDelta();

                CssRuleUsage.Disarm(tracker);
                CssRuleUsage.IsTracking.Should().BeFalse();

                CssCascade.Of(document.GetElementById("box")!).Should().NotBeNull();
                return (armed.Length, tracker.TakeDelta().Length);
            }
            finally
            {
                CssRuleUsage.Disarm(tracker);
            }
        });

        whileArmed.Should().BeGreaterThan(0, "the cascade matched the page's own .used rule");
        afterDisarm.Should().Be(0, "a disarmed window hears nothing more");
    }

    [Test]
    public async Task ASweepRecordsWhatMatchesTheDocumentWithoutAnyoneComputingACascade()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Styled);

        var swept = await page.RunOnLoopAsync(engine =>
        {
            var document = PageRuntime.Find(engine)!.Document!;

            var tracker = new CssRuleUsageTracker();
            tracker.Rebind(document);
            tracker.Sweep();

            return tracker.TakeDelta().Length;
        });

        swept.Should().BeGreaterThan(0,
            "this is Blink's forced style recalculation on start, and nothing renders here to do it for us");
    }
}
