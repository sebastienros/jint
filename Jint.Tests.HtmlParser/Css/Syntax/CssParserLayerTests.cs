using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Syntax;

public sealed class CssParserLayerTests
{
    private static CssValueWork Work() => new(default);

    [Test]
    public void MediaRuleReturnsRawConditionAndBodyWithoutInterpretingEither()
    {
        const string source = "/* leading */ @media (width > 10px) and future(foo([bar])) { @scope (.unused) {} }";
        var raw = CssParser.ParseMediaRule(source, Work());
        raw.Prelude.Text.Should().Be(" (width > 10px) and future(foo([bar])) ");
        raw.Body!.Value.Text.Should().Be(" @scope (.unused) {} ");
        raw.Text.Source.Should().BeSameAs(source);
        raw.Prelude.Source.Should().BeSameAs(source);
        raw.Body.Value.Source.Should().BeSameAs(source);
        var media = CssParser.ParseMediaQueryList(raw.Prelude, Work());
        media.MediaText.Should().Contain("future(foo([bar]))");
        var children = CssParser.ParseRuleList(new CssRuleBody(raw.Body.Value, CssRuleBodyKind.Group, raw.IsClosed), Work());
        children.Single().Name.Should().Be("scope");
        Assert.Throws<CssIncompleteRuleGrammarException>(() => CssParser.ParseRule(children[0], Work()))!
            .Blocker.Should().Be("R2:scope");
    }

    [Test]
    public void DeclarationAndConditionEntryPointsAcceptStandaloneStrings()
    {
        CssParser.ParseMediaQueryList("screen and (width > 10px)", Work()).MediaText
            .Should().Be("screen and (width > 10px)");
        var declaration = CssParser.ParseDeclarationList("color:red; display:block", Work());
        declaration.GetPropertyValue("color").Should().Be("red");
        declaration.GetPropertyValue("display").Should().Be("block");
    }

    [TestCase("")]
    [TestCase("p {}")]
    [TestCase("@media screen;")]
    [TestCase("@media screen {} trailing")]
    [TestCase("@media screen {} p {}")]
    [TestCase("--discarded: {}; @media screen {}")]
    public void AStandaloneMediaRuleRequiresExactlyOneCompleteRule(string source)
    {
        Assert.Throws<CssParseException>(() => CssParser.ParseMediaRule(source, Work()));
    }

    [TestCase("")]
    [TestCase("/*comment*/ <!-- -->")]
    [TestCase("@unknown x; p{color:red} @media screen { a{} @supports (color:red) { b{} } }")]
    [TestCase("/*😀*/\r\n@f\\75 ture f([a]); p { --x: f([\"}\"]); }")]
    [TestCase("a{} --bad: {}; b{} trailing")]
    [TestCase("@media (f([x])]) { a{}")]
    [TestCase("p { --x:'unterminated")]
    [TestCase("p { --x:url(unterminated")]
    [TestCase("@import 'a'; a:hover { color:red }")]
    [TestCase("@x f({a:b}); x > [a=\"{\"] {}")]
    public void RawRuleRecoveryAndOriginalSpansMatchTheExplicitSyntaxParser(string source)
    {
        var raw = CssParser.ParseRuleList(source, Work());
        var syntax = MarkupParser.ParseCss(source);
        raw.Length.Should().Be(syntax.Rules.Count);
        for (var i = 0; i < raw.Length; i++)
        {
            raw[i].Kind.Should().Be(syntax.Rules[i].Kind);
            raw[i].Name.Should().Be(syntax.Rules[i].Name);
            raw[i].Text.Span.Should().Be(syntax.Rules[i].Span);
            if (syntax.Rules[i].Block is { } block)
            {
                raw[i].Body!.Value.Span.Start.Should().Be(block.Span.Start + 1);
                raw[i].Body!.Value.End.Should().Be(block.Span.Start + block.Span.Length - (block.IsClosed ? 1 : 0));
            }
            else raw[i].Body.Should().BeNull();
        }
    }

    [Test]
    public void SuccessiveExplicitParsesKeepAbsoluteSpansAndRecoveredSerialization()
    {
        const string source = "/* offset */ @media screen { p { color:red } @supports (color:red) { a {} } }";
        var raw = CssParser.ParseMediaRule(source, Work());
        var group = CssParser.ParseRule(raw, Work())!.Value;
        var children = CssParser.ParseRuleList(group.Children!.Value, Work());
        children.Select(rule => rule.Text.Text).Should().Equal("p { color:red }", "@supports (color:red) { a {} }");
        var style = (CssStyleRule) CssParser.ParseRule(children[0], Work())!.Value.Rule;
        style.SourceSpan.Start.Should().Be(source.IndexOf("p {", StringComparison.Ordinal));
        style.Style.GetPropertyValue("color").Should().Be("red");
        var nested = CssParser.ParseRule(children[1], Work())!.Value;
        var leaf = CssParser.ParseRuleList(nested.Children!.Value, Work()).Single();
        leaf.Text.Span.Start.Should().Be(source.IndexOf("a {}", StringComparison.Ordinal));
        CssParser.ParseRule(leaf, Work())!.Value.Rule.CssText.Should().Be("a { }");
    }

    [Test]
    public void RawScanningStillEnforcesLexicalLimitsBeforeReturningSlices()
    {
        const string source = "@media screen { p { --x:f([a]) } }";
        Assert.Throws<ParseLimitException>(() => CssParser.ParseRuleList(source, Work(),
            new CssParseOptions { Limits = new ParseLimits { MaxNestingDepth = 3 } }))!
            .Kind.Should().Be(ParseLimitKind.NestingDepth);
        Assert.Throws<ParseLimitException>(() => CssParser.ParseRuleList(source, Work(),
            new CssParseOptions { Limits = new ParseLimits { MaxInputCharacters = source.Length - 1 } }))!
            .Kind.Should().Be(ParseLimitKind.InputCharacters);
        Assert.Throws<ParseLimitException>(() => CssParser.ParseRuleList(source, Work(),
            new CssParseOptions { Limits = new ParseLimits { MaxTokenCharacters = 5 } }))!
            .Kind.Should().Be(ParseLimitKind.TokenCharacters);
    }

    [Test]
    public void EveryRawParsingCheckpointHonorsCancellationAndRawResultsRetainNoInvocation()
    {
        const string source = "@media screen { p {color:red} }";
        var calls = 0;
        CssParser.ParseRuleList(source, new CssValueWork(default, () => calls++));
        for (var target = 1; target <= calls; target++)
        {
            using var cancellation = new CancellationTokenSource();
            var checks = 0;
            var work = new CssValueWork(cancellation.Token, () => { if (++checks == target) cancellation.Cancel(); });
            Assert.Throws<OperationCanceledException>(() => CssParser.ParseRuleList(source, work));
        }
        using var lifetime = new CancellationTokenSource();
        var raw = CssParser.ParseMediaRule(source, new CssValueWork(lifetime.Token));
        lifetime.Cancel();
        CssParser.ParseMediaQueryList(raw.Prelude, Work()).MediaText.Should().Be("screen");
        raw.Body!.Value.Text.Should().Be(" p {color:red} ");
    }
}
