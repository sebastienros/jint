#nullable enable
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Values.References;

[TestFixture]
public sealed class ReferenceOwnershipTests
{
    [Test]
    public void DeclarationInputOwnsOnlyItsHullWithOriginalCoordinates()
    {
        var source = new string(' ', 100_000) + "opacity: var(--X, env(foo, var(--Y)));";
        var declaration = new CssSyntaxParser(source, null, default).ParseDeclarationList()[0];
        var work = new CssValueWork(default);
        var input = CssReferenceInput.FromComponents(source, declaration.Value, 0, work);
        input.Components.Should().BeSameAs(declaration.Value);
        input.Source.Should().Be("var(--X, env(foo, var(--Y)))");
        input.SourceOffset.Should().Be(source.IndexOf("var", StringComparison.Ordinal));
        input.SourceSlice(declaration.Value[0].Span).ToString().Should().Be(input.Source);
        var result = CssPropertyParser.Parse("opacity", input, CssDeclarationContext.Style, work);
        result.Status.Should().Be(CssPropertyStatus.Deferred);
        result.Value.Serialize().Should().Be(input.Source);
        result.Value.References.Count.Should().Be(3);
        result.Value.References[0].Span.Start.Should().Be(input.SourceOffset);
        Assert.Throws<ArgumentOutOfRangeException>(() => input.SourceSlice(new CssSourceSpan(0, 1)).ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => input.SourceSlice(new CssSourceSpan(input.SourceOffset, int.MaxValue)).ToString());
    }

    [Test]
    public void EmptyInputAndCopyCancellationDoNotPublishPartialInput()
    {
        var components = new CssComponentValueList([]);
        var input = CssReferenceInput.FromComponents(new string(' ', 100_000), components, 0);
        input.Source.Should().BeEmpty();
        input.SourceSlice(default).IsEmpty.Should().BeTrue();
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++calls == 3) cancellation.Cancel(); });
        var source = new string('a', 10_000);
        var values = new CssSyntaxParser(source, null, default).ParseComponentValues();
        Assert.Throws<OperationCanceledException>(() => CssReferenceInput.FromComponents(source, values, 0, work));
    }

    [Test]
    public void SubstitutionKeepsMixedCompactOwnersAndSyntheticCloserOrigins()
    {
        const string source = "opacity: 0; --x: f\\6f o(var(--a)";
        const string supplied = "display: block; --a: a\\ ;";
        var declaration = new CssSyntaxParser(source, null, default).ParseDeclarationList()[1];
        var binding = new CssSyntaxParser(supplied, null, default).ParseDeclarationList()[1];
        var work = new CssValueWork(default);
        var input = CssReferenceInput.FromComponents(source, declaration.Value, 0, work);
        var replacement = CssReferenceInput.FromComponents(supplied, binding.Value, 0, work);
        var result = CssSubstitutionExecutor.Resolve(input,
            CssSubstitutionSnapshot.Create([CssSubstitutionBinding.Specified("--a", replacement, false)], work),
            CssEnvironmentSnapshot.Create([], work),
            new CssSubstitutionContext("opacity", CssReferenceUse.PropertyValue, true), work);
        var origins = result.Value.OriginsFor(new CssSourceSpan(0, result.Value.SpellingLength));
        origins.Count.Should().Be(3);
        origins[0].Source.Should().BeSameAs(input);
        origins[0].SourceSpan.Start.Should().Be(input.SourceOffset);
        input.SourceSlice(origins[0].SourceSpan).ToString().Should().Be("f\\6f o(");
        origins[1].Source.Should().BeSameAs(replacement);
        replacement.SourceSlice(origins[1].SourceSpan).ToString().Should().Be("a\\ ");
        origins[2].Source.Should().BeSameAs(input);
        origins[2].IsSyntheticCloser.Should().BeTrue();
        origins[2].SourceSpan.Start.Should().Be(source.Length);
        input.SourceSlice(origins[2].SourceSpan).IsEmpty.Should().BeTrue();
    }

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
        Assert.Throws<InvalidOperationException>(() => _ = custom.PendingFunction);
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
