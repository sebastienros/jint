#nullable enable
using System.Collections;
using Jint.HtmlParser;
using Jint.HtmlParser.Css;

namespace Jint.Tests.HtmlParser.Css.Syntax;

[TestFixture]
public sealed class CssPublicStyleSheetTests
{
    [TestCase("")]
    [TestCase(" /* comment */ \r\n <!-- -->")]
    public void EmptySheetsRetainTheirExactSource(string source)
    {
        var sheet = MarkupParser.ParseCss(source);
        sheet.Source.Should().BeSameAs(source);
        sheet.Rules.Should().BeEmpty();
    }

    [Test]
    public void WholeSheetRetainsUnknownRulesAndUnvalidatedNestedContents()
    {
        const string source = "<!-- @future one; --> ??? { width: nonsense; --x: var(--missing); unknown: f([a]); } @group { <!-- x{} --> }";
        var sheet = MarkupParser.ParseCss(source);
        sheet.Rules.Select(rule => rule.Name).Should().Equal("future", "", "group");
        sheet.Rules.Select(rule => rule.Kind).Should().Equal(
            CssRuleKind.AtRule, CssRuleKind.QualifiedRule, CssRuleKind.AtRule);
        sheet.Rules[0].Block.Should().BeNull();
        var body = sheet.Rules[1].Block!.Value.Values;
        body.Where(value => value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Ident)
            .Select(value => value.Token.Text).Should().Equal("width", "nonsense", "--x", "unknown");
        var functions = body.Where(value => value.Kind == CssComponentKind.Function).ToArray();
        functions.Select(value => value.FunctionName).Should().Equal("var", "f");
        functions[0].Values[0].Token.Text.Should().Be("--missing");
        functions[1].Values[0].OpeningDelimiter.Should().Be('[');
        functions[1].Values[0].Values[0].Token.Text.Should().Be("a");
        var group = sheet.Rules[2].Block!.Value.Values;
        group.Where(value => value.Kind == CssComponentKind.Token).Select(value => value.Token.Kind)
            .Should().Contain(CssTokenKind.Cdo).And.Contain(CssTokenKind.Cdc);
    }

    [Test]
    public void RecoveryDiscardsMalformedRulesAndKeepsValidSiblings()
    {
        var diagnostics = new ParseDiagnosticCollector();
        var sheet = MarkupParser.ParseCss("a{} --bad: {}; b{} trailing", new CssParseOptions { Diagnostics = diagnostics });
        sheet.Rules.Should().HaveCount(2);
        sheet.Rules[0].Prelude[0].Token.Text.Should().Be("a");
        // A top-level semicolon remains part of a qualified rule's prelude.
        sheet.Rules[1].Prelude.Last().Token.Text.Should().Be("b");
        diagnostics.Items.Select(item => item.Code).Should().Contain("css/discarded-custom-property-rule")
            .And.Contain("css/expected-rule-block");
        MarkupParser.ParseCss("ok{}", new CssParseOptions { Diagnostics = diagnostics });
        diagnostics.Items.Should().BeEmpty();
        sheet.Rules.Should().HaveCount(2);
    }

    [Test]
    public void SourceSpansReferToOriginalUtf16InputIncludingCommentsAndEscapes()
    {
        const string source = "/*😀*/\r\n@f\\75 ture f([a]);\r\np { --x: f([\"b\"]); }";
        var sheet = MarkupParser.ParseCss(source);
        sheet.Source.Should().BeSameAs(source);
        var atRule = sheet.Rules[0];
        atRule.Name.Should().Be("future");
        Slice(atRule.Span).Should().Be("@f\\75 ture f([a]);");
        var function = atRule.Prelude.Single(value => value.Kind == CssComponentKind.Function);
        Slice(function.Span).Should().Be("f([a])");
        Slice(function.Values[0].Span).Should().Be("[a]");
        Slice(function.Values[0].Values[0].Span).Should().Be("a");
        Slice(sheet.Rules[1].Span).Should().Be("p { --x: f([\"b\"]); }");
        Slice(sheet.Rules[1].Block!.Value.Span).Should().Be("{ --x: f([\"b\"]); }");

        string Slice(CssSourceSpan span) => sheet.Source.Substring(span.Start, span.Length);
    }

    [Test]
    public void RulesCannotBeMutatedThroughCollectionCastsOrSubsequentParses()
    {
        var sheet = MarkupParser.ParseCss("a{ --x:f([b]); }");
        var rule = sheet.Rules[0];
        (sheet.Rules is CssRuleSyntax[]).Should().BeFalse();
        var generic = (IList<CssRuleSyntax>) sheet.Rules;
        generic.IsReadOnly.Should().BeTrue();
        Assert.Throws<NotSupportedException>(() => generic[0] = MarkupParser.ParseCssRule("x{}"));
        Assert.Throws<NotSupportedException>(() => generic.Clear());
        Assert.Throws<NotSupportedException>(() => ((IList) sheet.Rules).RemoveAt(0));
        ((object) rule.Prelude is IList<CssComponentValue>).Should().BeFalse();
        ((object) rule.Block!.Value.Values is IList<CssComponentValue>).Should().BeFalse();
        MarkupParser.ParseCss("x{}").Rules[0].Should().NotBeSameAs(rule);
        sheet.Rules[0].Should().BeSameAs(rule);
        typeof(CssStyleSheetSyntax).GetConstructors().Should().BeEmpty();
    }

    [Test]
    public void ApplicableLimitsRemainInclusive()
    {
        const string source = "a{f(b)}";
        MarkupParser.ParseCss(source, new CssParseOptions
        {
            Limits = new ParseLimits { MaxInputCharacters = source.Length, MaxTokenCharacters = 2, MaxNestingDepth = 2 }
        }).Rules.Should().ContainSingle();
        Assert.Throws<ParseLimitException>(() => MarkupParser.ParseCss(source, new CssParseOptions
        {
            Limits = new ParseLimits { MaxInputCharacters = source.Length - 1 }
        }))!.Kind.Should().Be(ParseLimitKind.InputCharacters);
        Assert.Throws<ParseLimitException>(() => MarkupParser.ParseCss("long{}", new CssParseOptions
        {
            Limits = new ParseLimits { MaxTokenCharacters = 3 }
        }))!.Kind.Should().Be(ParseLimitKind.TokenCharacters);
        Assert.Throws<ParseLimitException>(() => MarkupParser.ParseCss(source, new CssParseOptions
        {
            Limits = new ParseLimits { MaxNestingDepth = 1 }
        }))!.Kind.Should().Be(ParseLimitKind.NestingDepth);
    }

    [Test]
    public void NullAndPreCanceledInputsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => MarkupParser.ParseCss(null!));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => MarkupParser.ParseCss("", cancellationToken: cancellation.Token));
    }

    [TestCase("")]
    [TestCase("a{f([b])} @unknown x;")]
    public void EveryCheckpointIncludingCompletionHonorsCancellation(string source)
    {
        var checkpoints = 0;
        MarkupParser.ParseCssCore(source, null, default, () => checkpoints++);
        checkpoints.Should().BeGreaterThan(0);
        for (var target = 1; target <= checkpoints; target++)
        {
            using var cancellation = new CancellationTokenSource();
            var calls = 0;
            Assert.Throws<OperationCanceledException>(() => MarkupParser.ParseCssCore(source, null,
                cancellation.Token, () => { if (++calls == target) cancellation.Cancel(); }));
            calls.Should().Be(target);
        }
    }

    [Test]
    public void CancellationIsPolledDuringLargeInputs()
    {
        var source = string.Concat(Enumerable.Repeat("a{f([b])} ", 4096));
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        Assert.Throws<OperationCanceledException>(() => MarkupParser.ParseCssCore(source, null,
            cancellation.Token, () => { if (++calls == 10) cancellation.Cancel(); }));
        calls.Should().Be(10);
    }
}
