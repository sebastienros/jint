#nullable enable
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.References;

[TestFixture]
public sealed class ReferenceSyntaxTests
{
    private static CssReferenceAnalysis Analyze(string source, CssReferenceUse use = CssReferenceUse.PropertyValue) =>
        CssReferenceParser.Analyze(CssReferenceInput.Parse(source, null, default), use, new CssValueWork(default));

    [TestCase("red", (int) CssReferenceAnalysisKind.Literal)]
    [TestCase("foo(var(--a))", (int) CssReferenceAnalysisKind.Deferred)]
    [TestCase("var(--a)", (int) CssReferenceAnalysisKind.Deferred)]
    [TestCase("VAR(--a,)", (int) CssReferenceAnalysisKind.Deferred)]
    [TestCase("var(var(--name), red)", (int) CssReferenceAnalysisKind.Deferred)]
    [TestCase("var(foo, red)", (int) CssReferenceAnalysisKind.Deferred)]
    [TestCase("env(safe-area-inset-top)", (int) CssReferenceAnalysisKind.Deferred)]
    [TestCase("env(viewport-segment-width 999999999999999999999999999)", (int) CssReferenceAnalysisKind.Deferred)]
    [TestCase("env(var(--name) 0)", (int) CssReferenceAnalysisKind.Deferred)]
    [TestCase("var(--x", (int) CssReferenceAnalysisKind.Deferred)]
    [TestCase("var()", (int) CssReferenceAnalysisKind.InvalidSyntax)]
    [TestCase("var(,red)", (int) CssReferenceAnalysisKind.InvalidSyntax)]
    [TestCase("var(   ,red)", (int) CssReferenceAnalysisKind.InvalidSyntax)]
    [TestCase("var(,...var(--args))", (int) CssReferenceAnalysisKind.InvalidSyntax)]
    [TestCase("var(--x) !important", (int) CssReferenceAnalysisKind.InvalidSyntax)]
    [TestCase("red; blue", (int) CssReferenceAnalysisKind.InvalidSyntax)]
    [TestCase("red)", (int) CssReferenceAnalysisKind.InvalidSyntax)]
    [TestCase("'bad\nx", (int) CssReferenceAnalysisKind.InvalidSyntax)]
    [TestCase("url(x y)", (int) CssReferenceAnalysisKind.InvalidSyntax)]
    [TestCase("foo([a))", (int) CssReferenceAnalysisKind.InvalidSyntax)]
    [TestCase("attr(data-x)", (int) CssReferenceAnalysisKind.PendingFeature)]
    [TestCase("if(style(--x),red)", (int) CssReferenceAnalysisKind.PendingFeature)]
    [TestCase("inherit(--x)", (int) CssReferenceAnalysisKind.PendingFeature)]
    [TestCase("ident(foo)", (int) CssReferenceAnalysisKind.PendingFeature)]
    [TestCase("random-item(a,b)", (int) CssReferenceAnalysisKind.PendingFeature)]
    [TestCase("--choice(a,b)", (int) CssReferenceAnalysisKind.PendingFeature)]
    public void ClassifiesWholeValue(string source, int expected) =>
        Analyze(source).Kind.Should().Be((CssReferenceAnalysisKind) expected);

    [Test]
    public void RetainsOrderedPotentialReferencesAcrossHeadersAndUnusedFallbacks()
    {
        var source = "var(var(--name), foo(var(--fallback, env(viewport-segment-width 9999999999999999999999))))";
        var result = Analyze(source);
        result.Kind.Should().Be(CssReferenceAnalysisKind.Deferred);
        var program = result.Program;
        program.Count.Should().Be(4);
        program[0].ParentIndex.Should().Be(-1);
        program[0].HasDynamicHeader.Should().BeTrue();
        program[0].HasNestedFallback.Should().BeTrue();
        program[1].ParentIndex.Should().Be(0);
        program[1].StaticName.Should().Be("--name");
        program[2].ParentIndex.Should().Be(0);
        program[2].StaticName.Should().Be("--fallback");
        program[3].ParentIndex.Should().Be(2);
        program[3].Kind.Should().Be(CssReferenceKind.Env);
        program[3].StaticName.Should().Be("viewport-segment-width");
        source.Substring(program[3].Header.Span.Start, program[3].Header.Span.Length)
            .Should().Be("viewport-segment-width 9999999999999999999999");
    }

    [Test]
    public void FallbackPresenceAndAllFollowingCommasAreRetained()
    {
        var absent = Analyze("var(--a)").Program[0];
        absent.HasFallback.Should().BeFalse();
        var empty = Analyze("var(--a,)").Program[0];
        empty.HasFallback.Should().BeTrue();
        empty.Fallback.Count.Should().Be(0);
        var rich = Analyze("var(--a, red, blue)").Program[0];
        rich.Fallback.Count.Should().Be(5);
        rich.Fallback.Components[rich.Fallback.Start].Token.Kind.Should().Be(Jint.HtmlParser.Css.CssTokenKind.Whitespace);
        rich.Fallback.Span.Length.Should().BeGreaterThan(0);
    }

    [Test]
    public void HeaderAndFallbackSpansIncludeEdgeCommentsAndWhitespace()
    {
        var source = "var(/**/ --x /**/, /**/ red /**/)";
        var reference = Analyze(source).Program[0];
        source.Substring(reference.Header.Span.Start, reference.Header.Span.Length)
            .Should().Be("/**/ --x /**/");
        source.Substring(reference.Fallback.Span.Start, reference.Fallback.Span.Length)
            .Should().Be(" /**/ red /**/");
        var emptySource = "var(--x,/**/)";
        var empty = Analyze(emptySource).Program[0];
        empty.Fallback.Count.Should().Be(0);
        emptySource.Substring(empty.Fallback.Span.Start, empty.Fallback.Span.Length)
            .Should().Be("/**/");
    }

    [Test]
    public void EscapedFunctionNameDoesNotShortenHeaderSpan()
    {
        var source = "v\\61r(--x)";
        var reference = Analyze(source).Program[0];
        source.Substring(reference.Header.Span.Start, reference.Header.Span.Length).Should().Be("--x");
    }

    [TestCase("var(.../**/var(--args))", true)]
    [TestCase("var(./**/../**/var(--args))", true)]
    [TestCase("var(... /**/var(--args))", false)]
    public void SpreadUsesTokenAdjacencyRatherThanSourceGaps(string source, bool expected)
    {
        var result = Analyze(source);
        result.Kind.Should().Be(CssReferenceAnalysisKind.Deferred);
        result.Program[0].HasEarlySubstitution.Should().Be(expected);
        result.Program[0].EarlySubstitutionCount.Should().Be(expected ? 1 : 0);
        if (expected)
        {
            var span = result.Program[0].EarlySubstitutionSpan(0);
            source.Substring(span.Start, span.Length).Should().EndWith("var(--args)");
        }
        result.Program[0].Header.Count.Should().BeGreaterThan(0);
    }

    [Test]
    public void EachSpreadLocationIsRetainedWithoutArrayEscape()
    {
        var source = "var(...var(--a) ...env(foo))";
        var occurrence = Analyze(source).Program[0];
        occurrence.EarlySubstitutionCount.Should().Be(2);
        source.Substring(occurrence.EarlySubstitutionSpan(0).Start,
            occurrence.EarlySubstitutionSpan(0).Length).Should().Be("...var(--a)");
        source.Substring(occurrence.EarlySubstitutionSpan(1).Start,
            occurrence.EarlySubstitutionSpan(1).Length).Should().Be("...env(foo)");
    }

    [Test]
    public void ReferenceInsideSimpleBlockMakesHeaderDynamic()
    {
        var program = Analyze("var([var(--name)], red)").Program;
        program.Count.Should().Be(2);
        program[0].HasDynamicHeader.Should().BeTrue();
        program[1].ParentIndex.Should().Be(0);
    }

    [Test]
    public void SpreadOutsideArgumentContextIsOrdinary()
    {
        var result = Analyze("...var(--x)");
        result.Program[0].HasEarlySubstitution.Should().BeFalse();
    }

    [Test]
    public void StringsAndUrlsAreAtomicAndUnknownFunctionIsOnlyLiteral()
    {
        Analyze("\"var(--x)\" url(\"var(--x)\") foo(1)").Kind.Should().Be(CssReferenceAnalysisKind.Literal);
        Analyze("foo(var(--x))").Program.Count.Should().Be(1);
    }

    [Test]
    public void ContextAllowsEnvButRejectsActualVarInDescriptor()
    {
        Analyze("env(safe-area-inset-top)", CssReferenceUse.DescriptorValue).Kind
            .Should().Be(CssReferenceAnalysisKind.Deferred);
        Analyze("foo(var(--x))", CssReferenceUse.DescriptorValue).Kind
            .Should().Be(CssReferenceAnalysisKind.InvalidSyntax);
        Analyze("var(--x)", CssReferenceUse.DescriptorValue).Span.Start.Should().Be(0);
    }

    [Test]
    public void EscapedAndCaseSensitiveNamesAreDecodedWithoutNormalizing()
    {
        Analyze("VaR(--AbC)").Program[0].StaticName.Should().Be("--AbC");
        Analyze("var(--\\41 bc)").Program[0].StaticName.Should().Be("--Abc");
        Analyze("var(--)").Program[0].StaticName.Should().BeNull();
    }
}
