#nullable enable
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.References;

[TestFixture]
public sealed class ReferenceOwnershipTests
{
    [Test]
    public void InputRetainsOriginalStringAfterCallerReassignsVariable()
    {
        var source = "var(--a, /* exact */ red)";
        var input = CssReferenceInput.Parse(source, null, default);
        source = "changed";
        var result = CssReferenceParser.Analyze(input, CssReferenceUse.PropertyValue, new CssValueWork(default));
        result.Program.Input.Source.Should().Be("var(--a, /* exact */ red)");
        result.Program[0].Fallback.Span.Length.Should().BeGreaterThan(0);
    }

    [Test]
    public void PublishedOccurrencesAndComponentListsExposeNoMutableArray()
    {
        var result = CssReferenceParser.Analyze(CssReferenceInput.Parse("var(--a,var(--b))", null, default),
            CssReferenceUse.PropertyValue, new CssValueWork(default));
        result.Program.Count.Should().Be(2);
        Assert.That(result.Program, Is.Not.AssignableTo<CssReferenceOccurrence[]>());
        Assert.That(result.Program.Input.Components, Is.Not.AssignableTo<Jint.HtmlParser.Css.CssComponentValue[]>());
        var copy = result.Program[0];
        copy = default;
        result.Program[0].StaticName.Should().Be("--a");
    }

    [Test]
    public void DefaultResultsCannotExposePayloads()
    {
        var analysis = default(CssReferenceAnalysis);
        analysis.Kind.Should().Be(CssReferenceAnalysisKind.Uninitialized);
        Assert.Throws<InvalidOperationException>(() => _ = analysis.Program);
        Assert.Throws<InvalidOperationException>(() => _ = analysis.Span);
        Assert.Throws<InvalidOperationException>(() => _ = analysis.PendingFunction);

        var custom = default(CssCustomPropertyResult);
        custom.Kind.Should().Be(CssCustomPropertyKind.Uninitialized);
        Assert.Throws<InvalidOperationException>(() => _ = custom.Value);
        Assert.Throws<InvalidOperationException>(() => _ = custom.WideKeyword);
        Assert.Throws<InvalidOperationException>(() => _ = custom.Input);
        Assert.Throws<InvalidOperationException>(() => _ = custom.Span);
    }

    [Test]
    public void InputDoesNotRetainMutableOptionsOrCollector()
    {
        var collector = new Jint.HtmlParser.ParseDiagnosticCollector();
        var options = new Jint.HtmlParser.CssParseOptions
        {
            Diagnostics = collector,
            Limits = new Jint.HtmlParser.ParseLimits { MaxNestingDepth = 2 }
        };
        var input = CssReferenceInput.Parse("var(--a)", options, default);
        input.MaxNestingDepth.Should().Be(2);
        Assert.That(input.GetType().GetProperties(System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
            .Any(p => p.PropertyType == typeof(Jint.HtmlParser.CssParseOptions)), Is.False);
    }
}
