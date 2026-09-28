#nullable enable

using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css;

[TestFixture]
public sealed class NativeCssQueryDiagnosticsTests
{
    [Test]
    public void DisabledDiagnosticsKeepNoCollectorRecord()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        var query = Query(document, CssStyleSheet.Parse("div { opacity:.5; }"));
        query.Diagnostics.Should().BeNull();
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(element, "opacity", ref matching).Text.Should().Be("0.5");
        query.Diagnostics.Should().BeNull();
    }

    [Test]
    public void RepeatedRequestedReadsHitTheCacheWithoutMatchingOrPublishingAgain()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        var sheet = CssStyleSheet.Parse("div { opacity:.5; width:10px; color:red; }");
        var diagnostics = new NativeCssQueryDiagnostics(captureDetails: true);
        var query = Query(document, sheet, diagnostics);
        var record = diagnostics.Queries.Single();
        record.StatePublications.Should().Be(0);
        record.RuleAttempts.Should().Be(0);
        record.ComputedPublications.Should().BeEmpty();
        var matching = new SelectorMatchWork(document, default);
        var result = query.GetProperty(element, "opacity", ref matching);
        result.Text.Should().Be("0.5");
        record.StatePublications.Should().Be(1);
        record.RuleAttempts.Should().Be(1);
        record.RuleMatches.Should().Be(1);
        record.ComputedPublications["opacity"].Should().Be(1);
        record.ComputedPublications.Should().NotContainKey("width");
        record.ComputedPublications.Should().NotContainKey("color");
        query.GetProperty(element, "OPACITY", ref matching).Should().BeSameAs(result);
        record.StatePublications.Should().Be(1);
        record.RuleAttempts.Should().Be(1);
        record.RuleMatches.Should().Be(1);
        record.ComputedPublications["opacity"].Should().Be(1);
        record.CacheHits["opacity"].Should().Be(1);
        record.Elements![element].CacheHits["opacity"].Should().Be(1);
        record.Rules![(CssStyleRule) sheet.Rules[0]].Attempts.Should().Be(1);
        Query(document, sheet, diagnostics);
        diagnostics.Queries.Should().HaveCount(2);
        diagnostics.Queries[1].StatePublications.Should().Be(0);
    }

    [Test]
    public void InheritedCustomAndCoupledPropertiesRecordTheirRealCacheOperations()
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("main");
        var child = document.CreateElement("div");
        document.AppendChild(parent);
        parent.AppendChild(child);
        var sheet = CssStyleSheet.Parse("main { visibility:hidden; --X:foo; } div { overflow:hidden scroll; }");
        var diagnostics = new NativeCssQueryDiagnostics();
        var query = Query(document, sheet, diagnostics);
        var record = diagnostics.Queries.Single();
        record.Elements.Should().BeNull();
        record.Rules.Should().BeNull();
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "visibility", ref matching).Text.Should().Be("hidden");
        record.ComputedPublications["visibility"].Should().Be(2);
        query.GetProperty(child, "VISIBILITY", ref matching).Text.Should().Be("hidden");
        record.CacheHits["visibility"].Should().Be(1);
        query.GetProperty(child, "--X", ref matching).Text.Should().Be("foo");
        query.GetProperty(child, "--X", ref matching).Text.Should().Be("foo");
        record.ComputedPublications["--X"].Should().Be(2);
        record.CacheHits["--X"].Should().Be(1);
        query.GetProperty(child, "overflow-x", ref matching).Text.Should().Be("hidden");
        record.ComputedPublications.Should().NotContainKey("overflow-y");
        query.GetProperty(child, "OVERFLOW-Y", ref matching).Text.Should().Be("scroll");
        record.ComputedPublications["overflow-y"].Should().Be(1);
        query.GetProperty(child, "overflow-y", ref matching).Text.Should().Be("scroll");
        record.CacheHits["overflow-y"].Should().Be(1);
    }

    [Test]
    public void CanceledConstructionDoesNotPublishASuccessfulQuery()
    {
        var document = Document.CreateHtml();
        var diagnostics = new NativeCssQueryDiagnostics();
        using var cancellation = new CancellationTokenSource();
        var work = new CssValueWork(cancellation.Token, cancellation.Cancel);
        Action construct = () => new NativeCssQuery(document, [], [], new CssMediaEnvironment(),
            new SelectorEnvironment(document, null, null, null), work,
            diagnostics: diagnostics);
        construct.Should().Throw<OperationCanceledException>();
        diagnostics.Queries.Should().BeEmpty();
    }

    [Test]
    public void CanceledFirstReadDoesNotPublishAState()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        var diagnostics = new NativeCssQueryDiagnostics();
        using var cancellation = new CancellationTokenSource();
        var work = new CssValueWork(cancellation.Token);
        var query = new NativeCssQuery(document, [], [], new CssMediaEnvironment(),
            new SelectorEnvironment(document, null, null, null), work,
            diagnostics: diagnostics);
        var matching = new SelectorMatchWork(document, cancellation.Token);
        cancellation.Cancel();
        Action read = () => query.GetProperty(element, "opacity", ref matching);
        read.Should().Throw<OperationCanceledException>();
        var record = diagnostics.Queries.Single();
        record.StatePublications.Should().Be(0);
        record.RuleAttempts.Should().Be(0);
        record.ComputedPublications.Should().BeEmpty();
    }

    private static NativeCssQuery Query(Document document, CssStyleSheet sheet,
        NativeCssQueryDiagnostics? diagnostics = null)
    {
        var work = new CssValueWork(default);
        return new NativeCssQuery(document, [new NativeCssSheet(sheet, NativeCssOrigin.Author)], [],
            new CssMediaEnvironment(), new SelectorEnvironment(document, null, null, null),
            work, diagnostics: diagnostics);
    }
}
