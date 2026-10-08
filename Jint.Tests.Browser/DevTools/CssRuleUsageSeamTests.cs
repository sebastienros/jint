#nullable enable

using Jint.Browser.Dom.Views;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.Browser.Accessibility;

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
/// cascade allocates plenty on its own, so a byte counter around one would be measuring the former DOM integration, while a
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
    public void CoverageAndComputedValuesShareOneSelectorPassIncludingLosingRules()
    {
        const int rules = 256;
        var css = string.Concat(Enumerable.Range(0, rules).Select(i => $".used {{ --loser:{i}; opacity:.5; }}"))
            + ".unused { opacity:.1; }";
        using var fixture = CreateStyled(css);
        var element = ContentDom.ElementById(fixture.Document, "box")!;
        var tracker = new CssRuleUsageTracker();
        tracker.Rebind(fixture.Document);
        CssRuleUsage.Arm(tracker);
        try
        {
            var diagnostics = new NativeCssQueryDiagnostics();
            var traversal = CssCascade.Traversal.For(fixture.Document, diagnostics: diagnostics)!;
            var view = traversal.Of(element);
            var record = diagnostics.Queries.Single();
            var attempts = record.RuleAttempts;
            attempts.Should().BeGreaterThanOrEqualTo(rules);
            record.StatePublications.Should().Be(1);
            tracker.TakeDelta().Should().HaveCount(rules, "matching counts even when another rule wins");

            for (var i = 0; i < 8; i++)
            {
                view.GetPropertyValue("opacity").Should().Be("0.5");
                view.GetPropertyValue("--loser").Should().Be("255");
                view.MatchedRules().Should().HaveCountGreaterThanOrEqualTo(rules);
                traversal.Of(element).Should().BeSameAs(view);
            }
            record.RuleAttempts.Should().Be(attempts, "coverage and computed values use the same matched state");
            record.StatePublications.Should().Be(1);
            tracker.TakeDelta().Should().BeEmpty();
        }
        finally
        {
            CssRuleUsage.Disarm(tracker);
        }
    }

    [Test]
    public void TrackingAnotherDocumentPreservesUntrackedCascadeReuse()
    {
        using var tracked = CreateStyled(".used { opacity:.5; }");
        using var untracked = CreateStyled(".used { opacity:.5; }");
        var element = ContentDom.ElementById(untracked.Document, "box")!;
        var before = CssCascade.Traversal.Current(untracked.Document);
        var view = before.Of(element);
        view.GetPropertyValue("opacity").Should().Be("0.5");
        CssCascade.Traversal.Current(untracked.Document).Should().BeSameAs(before);

        var tracker = new CssRuleUsageTracker();
        tracker.Rebind(tracked.Document);
        CssRuleUsage.Arm(tracker);
        try
        {
            var diagnostics = new NativeCssQueryDiagnostics();
            var explicitTraversal = CssCascade.Traversal.For(untracked.Document, diagnostics: diagnostics)!;
            var explicitView = explicitTraversal.Of(element);
            diagnostics.Queries.Single().StatePublications.Should().Be(0,
                "another document's coverage cannot force this lazy view to match rules");
            explicitView.GetPropertyValue("opacity").Should().Be("0.5");
            for (var i = 0; i < 8; i++)
            {
                var current = CssCascade.Traversal.Current(untracked.Document);
                current.Should().BeSameAs(before, "another document's coverage cannot retire this query");
                current.Of(element).Should().BeSameAs(view);
                view.GetPropertyValue("opacity").Should().Be("0.5");
            }
            tracker.TakeDelta().Should().BeEmpty("untracked matches never belong to this coverage window");
            var trackedElement = ContentDom.ElementById(tracked.Document, "box")!;
            CssCascade.Of(trackedElement)!.GetPropertyValue("opacity").Should().Be("0.5");
            tracker.TakeDelta().Select(rule => rule.SelectorText).Should().Equal(".used");
        }
        finally
        {
            CssRuleUsage.Disarm(tracker);
        }
        CssCascade.Traversal.Current(untracked.Document).Should().BeSameAs(before);
    }

    private static DomTestFixture CreateStyled(string css)
    {
        var fixture = DomTestFixture.Create($"<!doctype html><style>{css}</style><p id='box' class='used'>text</p>");
        var style = ContentDom.Descendants(fixture.Document).Single(element => element.LocalName == "style");
        NativeCssStyleSheets.Install(DomRealm.Of(fixture.Engine), style, css, "about:blank");
        return fixture;
    }

    [Test]
    public async Task ACascadeRecordsNothingWhileNoWindowIsOpen()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Styled);

        var recorded = await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var box = DomDocumentReads.ById(runtime.Dom, document, "box")!;

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
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var box = DomDocumentReads.ById(runtime.Dom, document, "box")!;

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

                CssCascade.Of(DomDocumentReads.ById(runtime.Dom, document, "box")!).Should().NotBeNull();
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
