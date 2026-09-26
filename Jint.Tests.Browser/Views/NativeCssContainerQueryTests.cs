#nullable enable
using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Tests.Browser.Views;

public sealed class NativeCssContainerQueryTests
{
    [Test]
    public void ActualSwaggerStylesheetRetainsAllTenVendorContainerRules()
    {
        var source = Fixtures.FixtureCorpus.Read("vendor/swagger-ui-5.32.7/swagger-ui.css");
        var sheet = CssStyleSheet.Parse(source);
        var rules = sheet.Rules.OfType<CssContainerRule>().ToArray();
        rules.Should().HaveCount(10);
        rules.Should().OnlyContain(rule => rule.ContainerName == "swagger-ui" && rule.Rules.Count != 0);
        rules.Select(rule => rule.Condition.Instructions.Single().Feature!.Pixels)
            .Distinct().OrderBy(value => value).Should().Equal(550, 640, 768);
        // The source is used as shipped; every vendor child keeps its author declaration block.
        rules.SelectMany(rule => rule.Rules).Should().OnlyContain(rule => rule is CssStyleRule);
    }

    [Test]
    public void UnknownFeatureEliminatesTheWholeContainerSelectionEvenUnderOr()
    {
        using var fixture = Create("<style>@container (future-size:1px) or (width:10px){#child{opacity:.5}}</style>"
            + "<div style='container-type:inline-size'><span id=child></span></div>");
        var input = Query(fixture);
        var provider = new Metrics { Value = 10 };
        input.Query.AttachContainerMetrics(provider);
        input.Query.GetProperty(ContentDom.ElementById(fixture.Document, "child")!, "opacity", ref input.Matching).Text.Should().Be("1");
        provider.BoxReads.Should().Be(0);
        provider.Reads.Should().Be(0);
    }

    [TestCase(550, "column")]
    [TestCase(551, "row")]
    public void EligibleNamedAncestorSuppliesThresholdAndCompletedMetricIsShared(double width, string expected)
    {
        using var fixture = Create("<style>#outer{container:swagger-ui / inline-size} @container swagger-ui (max-width:550px)"
            + "{#child{flex-direction:column;font-size:12px}}</style><div id=outer><div id=child></div></div>");
        var input = Query(fixture);
        var provider = new Metrics { Value = width };
        input.Query.AttachContainerMetrics(provider);
        var child = ContentDom.ElementById(fixture.Document, "child")!;
        input.Query.GetProperty(child, "opacity", ref input.Matching).Text.Should().Be("1");
        provider.Reads.Should().Be(0);
        input.Query.GetProperty(child, "flex-direction", ref input.Matching).Text.Should().Be(expected);
        input.Query.GetProperty(child, "font-size", ref input.Matching);
        provider.Reads.Should().Be(1);
        provider.Last.Should().BeSameAs(ContentDom.ElementById(fixture.Document, "outer"));
    }

    [TestCase("", "")]
    [TestCase("container:x / inline-size", "y")]
    public void AbsenceRemainsUnknownUnderNotAndNeverRequestsMetrics(string declaration, string name)
    {
        using var fixture = Create("<style>@container " + name + " not (width:1px){#child{opacity:.5}}</style>"
            + "<div style='" + declaration + "'><span id=child></span></div>");
        var input = Query(fixture);
        var provider = new Metrics();
        input.Query.AttachContainerMetrics(provider);
        var child = ContentDom.ElementById(fixture.Document, "child")!;
        input.Query.GetProperty(child, "opacity", ref input.Matching).Text.Should().Be("1");
        provider.Reads.Should().Be(0);
    }

    [Test]
    public void NearestEligibleAncestorWinsAndTheSubjectNeverSelectsItself()
    {
        using var fixture = Create("<style>@container x (max-width:100px){.target{opacity:.5}}</style>"
            + "<div id=outer style='container:x / inline-size'><div id=normal style='container-name:x'>"
            + "<div id=nearest style='container:x / inline-size'><span class=target id=child></span></div></div></div>"
            + "<div class=target id=self style='container:x / inline-size'></div>");
        var input = Query(fixture);
        var provider = new Metrics { Value = 99 };
        input.Query.AttachContainerMetrics(provider);
        input.Query.GetProperty(ContentDom.ElementById(fixture.Document, "child")!, "opacity", ref input.Matching).Text.Should().Be("0.5");
        provider.Last.Should().BeSameAs(ContentDom.ElementById(fixture.Document, "nearest"));
        input.Query.GetProperty(ContentDom.ElementById(fixture.Document, "self")!, "opacity", ref input.Matching).Text.Should().Be("1");
        provider.Reads.Should().Be(1);
    }

    [Test]
    public void InactiveUnsupportedValuesStayColdAndNoBoxIsUnknownUnderNot()
    {
        using var fixture = Create("<style>@container x not (width:1px){#child{font:unimplemented}}</style>"
            + "<div style='container:x / inline-size'><span id=child></span></div>");
        var input = Query(fixture);
        var provider = new Metrics { Box = false };
        input.Query.AttachContainerMetrics(provider);
        input.Query.GetProperty(ContentDom.ElementById(fixture.Document, "child")!, "font-size", ref input.Matching).Text.Should().Be("16px");
        provider.BoxReads.Should().Be(1);
        provider.Reads.Should().Be(0);
    }

    [Test]
    public void ProviderMutationRejectsPublicationAndAbortsTheRead()
    {
        using var fixture = Create("<style>@container (width:10px){#child{opacity:.5}}</style>"
            + "<div style='container-type:inline-size'><span id=child></span></div>");
        var input = Query(fixture);
        var child = ContentDom.ElementById(fixture.Document, "child")!;
        input.Query.AttachContainerMetrics(new Metrics { Value = 10, OnWidth = () => child.SetAttribute("class", "changed") });
        Assert.Throws<InvalidOperationException>(() => input.Query.GetProperty(child, "opacity", ref input.Matching));
        Assert.Throws<InvalidOperationException>(() => input.Query.GetProperty(child, "opacity", ref input.Matching))!
            .Message.Should().Contain("aborted");
    }

    [Test]
    public void ReentrantPropertyMetricCycleHasNamedDependencyAndUnwinds()
    {
        using var fixture = Create("<style>@container (width:10px){#child{opacity:.5}}</style>"
            + "<div style='container-type:inline-size'><span id=child></span></div>");
        var input = Query(fixture);
        var child = ContentDom.ElementById(fixture.Document, "child")!;
        input.Query.AttachContainerMetrics(new Metrics { Value = 10,
            OnWidth = () => input.Query.GetProperty(child, "opacity", ref input.Matching) });
        Assert.Throws<CssIncompleteGrammarException>(() => input.Query.GetProperty(child, "opacity", ref input.Matching))!
            .Blocker.Should().Be("C6:container-layout-cycle");
        Assert.Throws<InvalidOperationException>(() => input.Query.Verify())!.Message.Should().Contain("aborted");
    }

    [Test]
    public void NativeQueryWithoutGeometryDistinguishesDependencyFromAbsence()
    {
        using var fixture = Create("<style>@container (width:10px){#child{opacity:.5}}</style>"
            + "<div style='container-type:inline-size'><span id=child></span></div>");
        var input = Query(fixture);
        Assert.Throws<CssIncompleteGrammarException>(() => input.Query.GetProperty(
            ContentDom.ElementById(fixture.Document, "child")!, "opacity", ref input.Matching))!
            .Blocker.Should().Be("C6:container-layout-metric");
    }

    [Test]
    public void UnusedConditionalVariablesAndFallbacksNeverDemandMetrics()
    {
        using var fixture = Create("<style>#child{--used:10px;width:var(--used,var(--unused))}"
            + "@container (width:10px){#child{--unused:20px}}</style>"
            + "<div style='container-type:inline-size'><span id=child></span></div>");
        var input = Query(fixture);
        var metrics = new Metrics { Value = 10 };
        input.Query.AttachContainerMetrics(metrics);
        var child = ContentDom.ElementById(fixture.Document, "child")!;
        input.Query.GetProperty(child, "width", ref input.Matching).Text.Should().Be("10px");
        metrics.Reads.Should().Be(0);
        input.Query.GetProperty(child, "--unused", ref input.Matching).Text.Trim().Should().Be("20px");
        metrics.Reads.Should().Be(1);
    }

    [Test]
    public void InheritedAliasKeepsItsDefiningScopeAndInitialShadowsTheParent()
    {
        using var fixture = Create("<div style='--x:10px;--alias:var(--x)'><span id=child style='--x:20px;width:var(--alias)'></span></div>");
        var child = ContentDom.ElementById(fixture.Document, "child")!;
        var first = Query(fixture);
        first.Query.GetProperty(child, "width", ref first.Matching).Text.Should().Be("10px");
        child.SetAttribute("style", "--alias:initial;width:var(--alias,30px)");
        var second = Query(fixture);
        second.Query.GetProperty(child, "width", ref second.Matching).Text.Should().Be("30px");
        child.SetAttribute("style", "--alias:inherit;--x:20px;width:var(--alias)");
        var third = Query(fixture);
        third.Query.GetProperty(child, "width", ref third.Matching).Text.Should().Be("10px");
    }

    private static (NativeCssQuery Query, SelectorMatchWork Matching) Query(DomTestFixture fixture)
        => NativeCssStyleSheets.CreateQuery(fixture.Document, DomRealm.Of(fixture.Engine));

    private static DomTestFixture Create(string html)
    {
        var fixture = DomTestFixture.Create(html);
        var realm = DomRealm.Of(fixture.Engine);
        NativeCssStyleSheets.Associate(realm, fixture.Document);
        foreach (var owner in ContentDom.Descendants(fixture.Document).Where(element => element.LocalName == "style"))
            NativeCssStyleSheets.Install(realm, owner, ContentDom.TextContent(owner), "about:blank");
        return fixture;
    }

    private sealed class Metrics : INativeCssContainerMetrics
    {
        internal double Value;
        internal bool Box = true;
        internal int BoxReads;
        internal int Reads;
        internal Element? Last;
        internal Action? OnWidth;
        public bool HasBox(Element element, ref SelectorMatchWork matching) { BoxReads++; return Box; }
        public double Width(Element element, ref SelectorMatchWork matching)
        {
            Reads++;
            Last = element;
            OnWidth?.Invoke();
            return Value;
        }
    }
}
