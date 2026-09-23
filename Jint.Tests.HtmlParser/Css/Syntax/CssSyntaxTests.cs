#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.Tests.HtmlParser.Css.Syntax;

[TestFixture]
public sealed class CssSyntaxTests
{
    [Test]
    public void TokenStreamCoversEveryCssSyntaxCategory()
    {
        const string source = "a f( @z #x #1 's' 'bad\nx url(a) url(a b) +1 2% 3px /**/ <!-- --> : ; , ( ) [ ] { } +";
        var scanner = new CssTokenizer(source, 0, null, default);
        var kinds = new List<CssTokenKind>();
        CssToken token;
        while ((token = scanner.Next()).Kind != CssTokenKind.None)
        {
            kinds.Add(token.Kind);
            token.Span.Length.Should().BePositive();
            token.Span.Start.Should().BeInRange(0, source.Length - 1);
        }
        kinds.Should().ContainInOrder(CssTokenKind.Ident, CssTokenKind.Whitespace, CssTokenKind.Function,
            CssTokenKind.Whitespace, CssTokenKind.AtKeyword, CssTokenKind.Whitespace,
            CssTokenKind.Hash, CssTokenKind.Whitespace, CssTokenKind.Hash,
            CssTokenKind.Whitespace, CssTokenKind.String, CssTokenKind.Whitespace,
            CssTokenKind.BadString, CssTokenKind.Whitespace, CssTokenKind.Ident,
            CssTokenKind.Whitespace, CssTokenKind.Url, CssTokenKind.Whitespace,
            CssTokenKind.BadUrl, CssTokenKind.Whitespace, CssTokenKind.Number,
            CssTokenKind.Whitespace, CssTokenKind.Percentage, CssTokenKind.Whitespace,
            CssTokenKind.Dimension, CssTokenKind.Whitespace, CssTokenKind.Whitespace,
            CssTokenKind.Cdo, CssTokenKind.Whitespace, CssTokenKind.Cdc,
            CssTokenKind.Whitespace, CssTokenKind.Colon, CssTokenKind.Whitespace,
            CssTokenKind.Semicolon, CssTokenKind.Whitespace, CssTokenKind.Comma,
            CssTokenKind.Whitespace, CssTokenKind.OpenParenthesis, CssTokenKind.Whitespace,
            CssTokenKind.CloseParenthesis, CssTokenKind.Whitespace,
            CssTokenKind.OpenSquareBracket, CssTokenKind.Whitespace,
            CssTokenKind.CloseSquareBracket, CssTokenKind.Whitespace,
            CssTokenKind.OpenCurlyBracket, CssTokenKind.Whitespace,
            CssTokenKind.CloseCurlyBracket, CssTokenKind.Whitespace,
            CssTokenKind.Delim);
    }

    [Test]
    public void TokenCategoriesAndPayloads()
    {
        var values = MarkupParser.ParseCssComponentValues("a f(x) @z #id 's' url(x) url(\"q\") 1 2% 3px <!-- --> : ; , [ ] { } ( ) +");
        var kinds = values.Select(value => value.Kind == CssComponentKind.Token ? value.Token.Kind : value.Kind == CssComponentKind.Function ? CssTokenKind.Function : CssTokenKind.OpenParenthesis).ToArray();
        kinds.Should().Contain(CssTokenKind.Ident);
        kinds.Should().Contain(CssTokenKind.Function);
        kinds.Should().Contain(CssTokenKind.AtKeyword);
        kinds.Should().Contain(CssTokenKind.Hash);
        kinds.Should().Contain(CssTokenKind.String);
        kinds.Should().Contain(CssTokenKind.Url);
        kinds.Should().Contain(CssTokenKind.Number);
        kinds.Should().Contain(CssTokenKind.Percentage);
        kinds.Should().Contain(CssTokenKind.Dimension);
        kinds.Should().Contain(CssTokenKind.Cdo);
        kinds.Should().Contain(CssTokenKind.Cdc);
        kinds.Should().Contain(CssTokenKind.Colon);
        kinds.Should().Contain(CssTokenKind.Semicolon);
        kinds.Should().Contain(CssTokenKind.Comma);
        kinds.Should().Contain(CssTokenKind.Delim);
        var hash = values.Single(value => value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Hash).Token;
        hash.Text.Should().Be("id");
        hash.IsIdHash.Should().BeTrue();
        var dimension = values.Single(value => value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Dimension).Token;
        dimension.NumberText.Should().Be("3");
        dimension.Unit.Should().Be("px");
        dimension.IsInteger.Should().BeTrue();
        values.Single(value => value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Number).Token.NumberText.Should().Be("1");
        values.Single(value => value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Percentage).Token.NumberText.Should().Be("2");
        values.Single(value => value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Url).Token.Text.Should().Be("x");
        values.Single(value => value.Kind == CssComponentKind.Function && value.FunctionName == "url").Values[0].Token.Text.Should().Be("q");
    }

    [Test]
    public void EscapesNumbersAndOriginalSpans()
    {
        var values = MarkupParser.ParseCssComponentValues("\\41  \r\n-1.2e+3px #\\31 a 😀");
        values[0].Token.Text.Should().Be("A");
        values[0].Span.Start.Should().Be(0);
        values[0].Span.Length.Should().Be(4);
        values[1].Token.Kind.Should().Be(CssTokenKind.Whitespace);
        values[1].Token.Text.Should().Be(" \n");
        values[2].Token.NumberText.Should().Be("-1.2e+3");
        values[2].Token.Unit.Should().Be("px");
        values[2].Token.IsInteger.Should().BeFalse();
        values[4].Token.Text.Should().Be("1a");
        values[4].Token.IsIdHash.Should().BeTrue();
        values[^1].Token.Text.Should().Be("😀");
        values[^1].Span.Length.Should().Be(2);
    }

    [Test]
    public void CssPreprocessingKeepsOriginalUtf16Offsets()
    {
        var values = MarkupParser.ParseCssComponentValues("a\0b\r\nc\ud800d");
        values[0].Token.Text.Should().Be("a\ufffdb");
        values[0].Span.Length.Should().Be(3);
        values[1].Token.Text.Should().Be("\n");
        values[1].Span.Length.Should().Be(2);
        values[2].Token.Text.Should().Be("c\ufffdd");
        values[2].Span.Start.Should().Be(5);
    }

    [Test]
    public void CommentsBadTokensAndUnmatchedClosingRecover()
    {
        var values = MarkupParser.ParseCssComponentValues("a/**/b 'x\ny url(x y) [a) ]");
        values[0].Token.Text.Should().Be("a");
        values[1].Token.Text.Should().Be("b");
        values[3].Token.Kind.Should().Be(CssTokenKind.BadString);
        values.Any(value => value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.BadUrl).Should().BeTrue();
        var block = values.Single(value => value.Kind == CssComponentKind.SimpleBlock);
        block.OpeningDelimiter.Should().Be('[');
        block.Values.Any(value => value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.CloseParenthesis).Should().BeTrue();
    }

    [Test]
    public void UrlLookaheadAndEscapeAtEofFollowTokenizerRecovery()
    {
        var quoted = MarkupParser.ParseCssComponentValue("url(  \"x\")");
        quoted.Kind.Should().Be(CssComponentKind.Function);
        quoted.Values.Count.Should().Be(2);
        quoted.Values[0].Token.Text.Should().Be(" ");
        quoted.Values[1].Token.Text.Should().Be("x");
        var escaped = MarkupParser.ParseCssComponentValue("a\\");
        escaped.Token.Kind.Should().Be(CssTokenKind.Ident);
        escaped.Token.Text.Should().Be("a\ufffd");
    }

    [Test]
    public void NestedAndEofRecoveredContainersUseAnExplicitStack()
    {
        var value = MarkupParser.ParseCssComponentValue("f([x])");
        value.Kind.Should().Be(CssComponentKind.Function);
        value.FunctionName.Should().Be("f");
        value.Values[0].OpeningDelimiter.Should().Be('[');
        value.Values[0].Values[0].Token.Text.Should().Be("x");
        value.Span.Length.Should().Be(6);
        var recovered = MarkupParser.ParseCssComponentValue("f([x");
        recovered.Span.Length.Should().Be(4);
        recovered.Values[0].Values[0].Token.Text.Should().Be("x");
    }

    [Test]
    public void SingleRuleAndDeclarationStayStructural()
    {
        var at = MarkupParser.ParseCssRule(" @unknown something; ");
        at.Kind.Should().Be(CssRuleKind.AtRule);
        at.Name.Should().Be("unknown");
        at.Block.Should().BeNull();
        at.Prelude.Any(value => value.Kind == CssComponentKind.Token && value.Token.Text == "something").Should().BeTrue();
        var qualified = MarkupParser.ParseCssRule("x { color: not-a-color }");
        qualified.Kind.Should().Be(CssRuleKind.QualifiedRule);
        qualified.Name.Should().BeEmpty();
        qualified.Block!.Value.OpeningDelimiter.Should().Be('{');
        var declaration = MarkupParser.ParseCssDeclaration("  --X: red nonsense ! IMPORTANT ; ");
        declaration.Name.Should().Be("--X");
        declaration.IsImportant.Should().BeTrue();
        declaration.Value.Select(value => value.Token.Text).Where(text => text.Length > 0).Should().ContainInOrder("red", "nonsense");
        declaration.Span.Start.Should().Be(2);
        MarkupParser.ParseCssDeclaration("bogus-property: 9qux;").Value.Single().Token.Unit.Should().Be("qux");
        var recovered = MarkupParser.ParseCssRule("@unknown [x");
        recovered.Kind.Should().Be(CssRuleKind.AtRule);
        recovered.Prelude[1].Span.Length.Should().Be(2);
        var trailing = MarkupParser.ParseCssDeclaration("x: a !important");
        trailing.Span.Length.Should().Be("x: a !important".Length);
        trailing.Value.Single().Token.Text.Should().Be("a");
    }

    [Test]
    public void CardinalityAndFailuresAreSpecific()
    {
        Assert.Throws<ArgumentNullException>(() => MarkupParser.ParseCssComponentValues(null!));
        Assert.Throws<CssParseException>(() => MarkupParser.ParseCssComponentValue("  "));
        Assert.Throws<CssParseException>(() => MarkupParser.ParseCssComponentValue("a b"));
        Assert.Throws<CssParseException>(() => MarkupParser.ParseCssRule("a"));
        Assert.Throws<CssParseException>(() => MarkupParser.ParseCssRule("a{} b{}"));
        Assert.Throws<CssParseException>(() => MarkupParser.ParseCssDeclaration("a b"));
        Assert.Throws<CssParseException>(() => MarkupParser.ParseCssDeclaration("a:b; c:d"));
        MarkupParser.ParseCssComponentValues(string.Empty).Count.Should().Be(0);
        MarkupParser.ParseCssComponentValue("  ]  ").Token.Kind.Should().Be(CssTokenKind.CloseSquareBracket);
    }

    [Test]
    public void DefaultUnionAndListsAreImmutable()
    {
        default(CssComponentValue).Kind.Should().Be(CssComponentKind.None);
        default(CssComponentValue).Span.Length.Should().Be(0);
        Assert.Throws<InvalidOperationException>(() => _ = default(CssComponentValue).Token);
        Assert.Throws<InvalidOperationException>(() => _ = default(CssComponentValue).FunctionName);
        Assert.Throws<InvalidOperationException>(() => _ = default(CssComponentValue).OpeningDelimiter);
        Assert.Throws<InvalidOperationException>(() => _ = default(CssComponentValue).Values);
        var tokenValue = MarkupParser.ParseCssComponentValue("x");
        Assert.Throws<InvalidOperationException>(() => _ = tokenValue.FunctionName);
        Assert.Throws<InvalidOperationException>(() => _ = tokenValue.OpeningDelimiter);
        Assert.Throws<InvalidOperationException>(() => _ = tokenValue.Values);
        var functionValue = MarkupParser.ParseCssComponentValue("f()");
        Assert.Throws<InvalidOperationException>(() => _ = functionValue.Token);
        Assert.Throws<InvalidOperationException>(() => _ = functionValue.OpeningDelimiter);
        var blockValue = MarkupParser.ParseCssComponentValue("[]");
        Assert.Throws<InvalidOperationException>(() => _ = blockValue.Token);
        Assert.Throws<InvalidOperationException>(() => _ = blockValue.FunctionName);
        default(CssToken).Text.Should().BeEmpty();
        default(CssToken).NumberText.Should().BeEmpty();
        default(CssToken).Unit.Should().BeEmpty();
        default(CssToken).Delimiter.Should().Be('\0');
        var result = MarkupParser.ParseCssComponentValues("a b");
        ((object) result is IList<CssComponentValue>).Should().BeFalse();
        MarkupParser.ParseCssComponentValues("c");
        result[0].Token.Text.Should().Be("a");
    }

    [Test]
    public void LimitsCancellationAndDiagnostics()
    {
        var limits = new ParseLimits { MaxInputCharacters = 3 };
        var exception = Assert.Throws<ParseLimitException>(() => MarkupParser.ParseCssComponentValues("abcd", new CssParseOptions { Limits = limits }));
        exception!.Kind.Should().Be(ParseLimitKind.InputCharacters);
        Assert.Throws<ParseLimitException>(() => MarkupParser.ParseCssComponentValues("abcdefgh", new CssParseOptions { Limits = new ParseLimits { MaxTokenCharacters = 3 } }));
        Assert.Throws<ParseLimitException>(() => MarkupParser.ParseCssComponentValue("((a))", new CssParseOptions { Limits = new ParseLimits { MaxNestingDepth = 1 } }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => MarkupParser.ParseCssComponentValues("a", cancellationToken: cancellation.Token));
        var diagnostics = new ParseDiagnosticCollector();
        MarkupParser.ParseCssComponentValues("'x\ny", new CssParseOptions { Diagnostics = diagnostics });
        diagnostics.Items.Select(item => item.Code).Should().Contain("css/bad-string");
        MarkupParser.ParseCssComponentValues("ok", new CssParseOptions { Diagnostics = diagnostics });
        diagnostics.Items.Should().BeEmpty();
    }

    [Test]
    public void DeepInputsDoNotRecurse()
    {
        const int depth = 5000;
        var result = MarkupParser.ParseCssComponentValue(new string('(', depth) + "x" + new string(')', depth));
        var current = result;
        for (var index = 0; index < depth; index++) current = current.Values[0];
        current.Token.Text.Should().Be("x");
    }

    [Test]
    public void IndependentCallsDoNotShareResults()
    {
        var inputs = Enumerable.Range(0, 64).Select(index => $"x{index} [y{index}]").ToArray();
        var results = inputs.AsParallel().Select(input => MarkupParser.ParseCssComponentValues(input)).ToArray();
        results.Length.Should().Be(64);
        results.Select(result => result[0].Token.Text).Should().BeEquivalentTo(
            Enumerable.Range(0, 64).Select(index => $"x{index}"));
        foreach (var result in results) result[2].Values.Count.Should().Be(1);
    }
}
