#nullable enable

using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using Jint.Browser.Styling;

namespace Jint.Tests.Browser.Views;

public sealed class NativeCssQueryDiagnosticsTests
{
    [Test]
    public void BrowserQueryAndTraversalRemainLazyAndRecordRepeatedRequestedReads()
    {
        using var fixture = DomTestFixture.Create("<style>#box { opacity:.5; width:10px; }</style><div id=box></div>");
        var document = fixture.Document;
        NativeCssStyleSheets.Associate(DomRealm.Of(fixture.Engine), document);
        var element = ContentDom.ElementById(document, "box")!;
        var diagnostics = new NativeCssQueryDiagnostics();
        var input = NativeCssStyleSheets.CreateQuery(document, DomRealm.Of(fixture.Engine), diagnostics);
        var queryRecord = diagnostics.Queries.Single();
        queryRecord.StatePublications.Should().Be(0);
        queryRecord.ComputedPublications.Should().BeEmpty();
        input.Query.GetProperty(element, "opacity", ref input.Matching).Text.Should().Be("0.5");
        queryRecord.ComputedPublications.Should().NotContainKey("width");

        var traversal = CssCascade.Traversal.For(document, diagnostics: diagnostics)!;
        diagnostics.Queries.Should().HaveCount(2);
        var record = diagnostics.Queries[1];
        var style = traversal.Of(element);
        record.StatePublications.Should().Be(0);
        style.GetPropertyValue("opacity").Should().Be("0.5");
        var attempts = record.RuleAttempts;
        var publications = record.ComputedPublications["opacity"];
        traversal.Of(element).Should().BeSameAs(style);
        style.GetPropertyValue("OPACITY").Should().Be("0.5");
        record.StatePublications.Should().Be(1);
        record.RuleAttempts.Should().Be(attempts);
        record.ComputedPublications["opacity"].Should().Be(publications);
        record.CacheHits["opacity"].Should().Be(1);
    }

    [Test]
    public void AccessibilitySnapshotsUseOneFreshQueryAndDoNotComputeUnrequestedProperties()
    {
        using var fixture = DomTestFixture.Create("<style>button { visibility:visible; width:10px; opacity:.5; }</style>"
            + "<div><button id=target>Save</button></div>");
        NativeCssStyleSheets.Associate(DomRealm.Of(fixture.Engine), fixture.Document);
        var diagnostics = new NativeCssQueryDiagnostics(captureDetails: true);
        var first = AccessibilityTree.Build(fixture.Document, diagnostics: diagnostics);
        AccessibilitySnapshot.Render(first).Should().Contain("Save");
        var record = diagnostics.Queries.Single();
        record.StatePublications.Should().BeGreaterThan(0);
        record.RuleAttempts.Should().BeGreaterThan(0);
        record.ComputedPublications.Should().ContainKey("display").And.ContainKey("visibility");
        record.ComputedPublications.Should().NotContainKey("width").And.NotContainKey("opacity");
        record.Elements!.Values.Should().OnlyContain(element => element.StatePublications == 1);
        var states = record.StatePublications;
        var attempts = record.RuleAttempts;
        var publications = record.ComputedPublications.ToArray();

        var second = AccessibilityTree.Build(fixture.Document, diagnostics: diagnostics);
        AccessibilitySnapshot.Render(second).Should().Be(AccessibilitySnapshot.Render(first));
        diagnostics.Queries.Should().HaveCount(2);
        diagnostics.Queries[1].Should().NotBeSameAs(record);
        diagnostics.Queries[1].StatePublications.Should().Be(states);
        record.RuleAttempts.Should().Be(attempts);
        record.ComputedPublications.ToArray().Should().Equal(publications);

        // The element entry point also forwards to its snapshot's single visibility query.
        var button = ContentDom.ElementById(fixture.Document, "target")!;
        AccessibilityTree.Build(button, diagnostics: diagnostics)!.Name.Should().Be("Save");
        diagnostics.Queries.Should().HaveCount(3);
    }

    [Test]
    public void InlineOnlyVisibilityDoesNotCreateADiagnosticQuery()
    {
        using var fixture = DomTestFixture.Create("<button style='display:none'>Hidden</button>");
        var diagnostics = new NativeCssQueryDiagnostics();
        var visibility = new ElementVisibility(useComputedStyle: false, diagnostics: diagnostics);
        visibility.CreateTraversal(fixture.Document).Should().BeNull();
        AccessibilityTree.Build(fixture.Document, AccessibilityOptions.Default with { UseComputedStyle = false }, diagnostics);
        diagnostics.Queries.Should().BeEmpty();
    }
}
