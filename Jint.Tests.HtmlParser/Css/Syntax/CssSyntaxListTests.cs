#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.Tests.HtmlParser.Css.Syntax;

[TestFixture]
public sealed class CssSyntaxListTests
{
    [TestCase("--x: /* first */ red /* last */;", " /* first */ red /* last */", false)]
    [TestCase("--x: /* only */;", " /* only */", false)]
    [TestCase("--x: /* only */ ! /* gap */ IMPORTANT /* tail */;", " /* only */ ", true)]
    [TestCase("--x: f(!important) !important;", " f(!important) ", true)]
    [TestCase("--x: a\\ ;", " a\\ ", false)]
    [TestCase("--x: red /* eof */", " red /* eof */", false)]
    [TestCase("--x:;", "", false)]
    public void LexicalValueSpanRetainsCommentsAndExcludesOnlyPriority(string source, string expected, bool important)
    {
        var direct = new CssSyntaxParser(source, null, default).ParseDeclaration();
        source.Substring(direct.ValueSourceSpan.Start, direct.ValueSourceSpan.Length).Should().Be(expected);
        direct.IsImportant.Should().Be(important);
        var prefixed = "opacity: .5; " + source;
        var declaration = new CssSyntaxParser(prefixed, null, default).ParseDeclarationList()[1];
        prefixed.Substring(declaration.ValueSourceSpan.Start, declaration.ValueSourceSpan.Length).Should().Be(expected);
        declaration.ValueSourceSpan.Start.Should().Be(direct.ValueSourceSpan.Start + 13);
        declaration.IsImportant.Should().Be(important);
    }

    [Test]
    public void LexicalValueSpanStopsAtContainingBlockClose()
    {
        const string source = "{ --x: /* only */ }";
        var parser = new CssSyntaxParser(source, null, default);
        var block = parser.ParseComponentValue();
        var declaration = parser.ParseBlockContents(block)[0].Declarations[0];
        source.Substring(declaration.ValueSourceSpan.Start, declaration.ValueSourceSpan.Length).Should().Be(" /* only */ ");
        declaration.Value.Count.Should().Be(0);
    }

    [TestCase("--x:  /* first */ red /* last */  ;", "/* first */ red /* last */")]
    [TestCase("--x: /* only */;", "/* only */")]
    [TestCase("--x: /* only */ ! /* gap */ IMPORTANT /* tail */;", "/* only */")]
    [TestCase("--x: a\\ ;", "a\\ ")]
    [TestCase("--x: a\\20 ;", "a\\20 ")]
    [TestCase("--x:/* note   ", "/* note   ")]
    [TestCase("--x: f(a   ", "f(a   ")]
    [TestCase("--x: \t/* gap */ \t;", "/* gap */")]
    [TestCase("--x: \t;", "")]
    public void SerializationSpanTrimsBoundaryWhitespaceTokensWithoutScanningComments(string source, string expected)
    {
        var declaration = new CssSyntaxParser(source, null, default).ParseDeclarationList()[0];
        source.Substring(declaration.ValueSerializationSpan.Start, declaration.ValueSerializationSpan.Length).Should().Be(expected);
    }

    [Test]
    public void StyleSheetSkipsTopLevelCdoCdcAndPreservesUnknownRules()
    {
        const string source = "<!-- @unknown one; --> a { color:red } @other two; @group { x{} }";
        var rules = new CssSyntaxParser(source, null, default).ParseStyleSheet();
        rules.Select(rule => rule.Kind).Should().ContainInOrder(
            CssRuleKind.AtRule, CssRuleKind.QualifiedRule, CssRuleKind.AtRule, CssRuleKind.AtRule);
        rules.Select(rule => rule.Name).Should().ContainInOrder("unknown", "", "other", "group");
        rules[0].Block.Should().BeNull();
        rules[1].Block!.Value.OpeningDelimiter.Should().Be('{');
        rules[3].Block!.Value.Values[1].Token.Text.Should().Be("x");
        rules[0].Span.Start.Should().Be(source.IndexOf("@unknown", StringComparison.Ordinal));
        var recovered = new CssSyntaxParser("} a{} b{}", null, default).ParseStyleSheet();
        recovered.Length.Should().Be(2);
        recovered[0].Prelude[0].Token.Kind.Should().Be(CssTokenKind.CloseCurlyBracket);
        recovered[0].Prelude[2].Token.Text.Should().Be("a");
        recovered[1].Prelude[0].Token.Text.Should().Be("b");
        var semicolonPrelude = new CssSyntaxParser("x; a{} b{}", null, default).ParseStyleSheet();
        semicolonPrelude.Length.Should().Be(2);
        semicolonPrelude[0].Prelude[1].Token.Kind.Should().Be(CssTokenKind.Semicolon);
    }

    [Test]
    public void NestedCdoAndCdcRemainRulePreludeTokens()
    {
        var parser = new CssSyntaxParser("{ <!-- a{} --> b{} }", null, default);
        var block = parser.ParseComponentValue();
        var contents = parser.ParseBlockContents(block);
        contents.Count.Should().Be(2);
        contents[0].Kind.Should().Be(CssBlockItemKind.Rule);
        contents[0].Rule.Prelude.Any(value => value.Kind == CssComponentKind.Token &&
            value.Token.Kind == CssTokenKind.Cdo).Should().BeTrue();
        contents[1].Rule.Prelude.Any(value => value.Kind == CssComponentKind.Token &&
            value.Token.Kind == CssTokenKind.Cdc).Should().BeTrue();
    }

    [Test]
    public void DeclarationListRecoversAndKeepsDuplicatesUnknownsAndNestedSemicolons()
    {
        const string source = "color:red; broken; --X: 1 !important; color:bogus; @unknown x; y:url(a;b); z:'x;y';";
        var diagnostics = new ParseDiagnosticCollector();
        var declarations = new CssSyntaxParser(source, new CssParseOptions { Diagnostics = diagnostics }, default)
            .ParseDeclarationList();
        declarations.Select(declaration => declaration.Name).Should().ContainInOrder(
            "color", "--X", "color", "y", "z");
        declarations[1].IsImportant.Should().BeTrue();
        declarations[2].Value[0].Token.Text.Should().Be("bogus");
        declarations[3].Value[0].Token.Text.Should().Be("a;b");
        declarations[4].Value[0].Token.Text.Should().Be("x;y");
        declarations[4].Span.Start.Should().Be(source.IndexOf("z:", StringComparison.Ordinal));
        diagnostics.Items.Select(item => item.Code).Should().Contain("css/discarded-qualified-rule");
        diagnostics.Items.Select(item => item.Code).Should().Contain("css/discarded-at-rule-in-declaration-list");
        var escaped = new CssSyntaxParser("co\\6cor/*c*/:/*c*/url(a;b);", null, default)
            .ParseDeclarationList();
        escaped.Single().Name.Should().Be("color");
        escaped.Single().Value.Single().Token.Text.Should().Be("a;b");
    }

    [Test]
    public void DeclarationListKeepsDeclarationsAfterNestedRuleBoundaries()
    {
        const string source = ".x{} color:red; width:1px; @unknown { x:y } height:2px;";
        var declarations = new CssSyntaxParser(source, null, default).ParseDeclarationList();
        declarations.Select(declaration => declaration.Name).Should().ContainInOrder(
            "color", "width", "height");
        declarations[0].Span.Start.Should().Be(source.IndexOf("color", StringComparison.Ordinal));
        declarations[2].Span.Start.Should().Be(source.IndexOf("height", StringComparison.Ordinal));
    }

    [Test]
    public void DeclarationListStopsAtTopLevelClosingBrace()
    {
        var declarations = new CssSyntaxParser("color:red}width:1px; height:2px", null, default)
            .ParseDeclarationList();
        declarations.Select(declaration => declaration.Name).Should().ContainInOrder("color");
        declarations.Length.Should().Be(1);

        var diagnostics = new ParseDiagnosticCollector();
        declarations = new CssSyntaxParser("@foo}width:1px", new CssParseOptions
        {
            Diagnostics = diagnostics
        }, default).ParseDeclarationList();
        declarations.Should().BeEmpty();
        diagnostics.Items.Any(item => item.Code == "css/unexpected-eof").Should().BeFalse();
    }

    [Test]
    public void BlockContentsKeepDeclarationRunsAndRulesInOrder()
    {
        const string source = "{ color:red; margin:0; a:hover { x:y } padding:2px; @unknown foo; z:3; @m { x:y } }";
        var parser = new CssSyntaxParser(source, null, default);
        var block = parser.ParseComponentValue();
        var contents = parser.ParseBlockContents(block);
        contents.Select(item => item.Kind).Should().ContainInOrder(
            CssBlockItemKind.Declarations, CssBlockItemKind.Rule,
            CssBlockItemKind.Declarations, CssBlockItemKind.Rule,
            CssBlockItemKind.Declarations, CssBlockItemKind.Rule);
        contents[0].Declarations.Select(declaration => declaration.Name).Should().ContainInOrder("color", "margin");
        contents[1].Rule.Prelude[0].Token.Text.Should().Be("a");
        contents[2].Declarations[0].Name.Should().Be("padding");
        contents[3].Rule.Name.Should().Be("unknown");
        contents[4].Declarations[0].Name.Should().Be("z");
        contents[5].Rule.Name.Should().Be("m");
        contents[0].Declarations[0].Span.Start.Should().Be(source.IndexOf("color", StringComparison.Ordinal));
        contents[5].Rule.Span.Start.Should().Be(source.IndexOf("@m", StringComparison.Ordinal));
    }

    [Test]
    public void InvalidFragmentsDoNotConsumeValidSiblings()
    {
        const string source = "{ broken; good:1; a { b:c } bad; later:2; }";
        var diagnostics = new ParseDiagnosticCollector();
        var parser = new CssSyntaxParser(source, new CssParseOptions { Diagnostics = diagnostics }, default);
        var contents = parser.ParseBlockContents(parser.ParseComponentValue());
        contents.Count.Should().Be(3);
        contents[0].Declarations[0].Name.Should().Be("good");
        contents[1].Rule.Prelude[0].Token.Text.Should().Be("a");
        contents[2].Declarations[0].Name.Should().Be("later");
        diagnostics.Items.Any(item => item.Code == "css/discarded-qualified-rule" &&
            item.Offset == source.IndexOf("broken", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Test]
    public void CustomPropertiesAndBraceOnlyValuesStayDeclarations()
    {
        const string source = "{ --X: red {} blue; x: {} !important; color:red {}; after:3 }";
        var parser = new CssSyntaxParser(source, null, default);
        var contents = parser.ParseBlockContents(parser.ParseComponentValue());
        contents.Count.Should().Be(3);
        contents[0].Declarations.Select(declaration => declaration.Name).Should().ContainInOrder("--X", "x");
        contents[0].Declarations[1].IsImportant.Should().BeTrue();
        contents[1].Kind.Should().Be(CssBlockItemKind.Rule);
        contents[2].Declarations[0].Name.Should().Be("after");
    }

    [Test]
    public void EofRecoveryKeepsOpaqueBlocksAndOriginalDiagnosticOffsets()
    {
        const string source = "a { color:red";
        var diagnostics = new ParseDiagnosticCollector();
        var parser = new CssSyntaxParser(source, new CssParseOptions { Diagnostics = diagnostics }, default);
        var rules = parser.ParseStyleSheet();
        rules.Length.Should().Be(1);
        rules[0].Block!.Value.Span.Start.Should().Be(source.IndexOf('{'));
        rules[0].Block!.Value.Span.Start.Should().Be(2);
        rules[0].Block!.Value.Span.Length.Should().Be(source.Length - 2);
        var contents = parser.ParseBlockContents(rules[0].Block!.Value);
        contents[0].Declarations[0].Name.Should().Be("color");
        diagnostics.Items.Any(item => item.Code == "css/unexpected-eof" && item.Offset == source.Length)
            .Should().BeTrue();
    }

    [Test]
    public void BlockContentsDistinguishOuterClosingTokenFromInnerClosingTokenAtEof()
    {
        foreach (var source in new[] { "{unicode-range:{}}", "{unicode-range:{}" })
        {
            var diagnostics = new ParseDiagnosticCollector();
            var parser = new CssSyntaxParser(source, new CssParseOptions { Diagnostics = diagnostics }, default);
            var contents = parser.ParseBlockContents(parser.ParseComponentValue());
            var value = contents.Single().Declarations.Single().Value.Single();
            value.Kind.Should().Be(CssComponentKind.SimpleBlock);
            value.Span.Length.Should().Be(2);
            var eofCount = diagnostics.Items.Count(item => item.Code == "css/unexpected-eof");
            eofCount.Should().Be(source.EndsWith("}}", StringComparison.Ordinal) ? 0 : 1);
        }
    }

    [Test]
    public void AtRuleAtOuterClosingTokenDoesNotReportUnexpectedEof()
    {
        const string source = "{@foo}";
        var diagnostics = new ParseDiagnosticCollector();
        var parser = new CssSyntaxParser(source, new CssParseOptions { Diagnostics = diagnostics }, default);
        var contents = parser.ParseBlockContents(parser.ParseComponentValue());
        contents.Single().Rule.Name.Should().Be("foo");
        diagnostics.Items.Any(item => item.Code == "css/unexpected-eof").Should().BeFalse();
    }

    [Test]
    public void CommentsEndingInBraceDoNotCloseAnOuterBlock()
    {
        const string declarationSource = "{unicode-range:U+4??/*}";
        var diagnostics = new ParseDiagnosticCollector();
        var parser = new CssSyntaxParser(declarationSource,
            new CssParseOptions { Diagnostics = diagnostics }, default);
        var block = parser.ParseComponentValue();
        block.IsClosed.Should().BeFalse();
        var declaration = parser.ParseBlockContents(block).Single().Declarations.Single();
        declaration.Value.Single().Token.Kind.Should().Be(CssTokenKind.UnicodeRange);
        diagnostics.Items.Any(item => item.Code == "css/unexpected-eof").Should().BeTrue();
        diagnostics.Items.Where(item => item.Code == "css/unexpected-eof")
            .All(item => item.Offset == declarationSource.Length).Should().BeTrue();

        const string atRuleSource = "{@foo/*}";
        diagnostics = new ParseDiagnosticCollector();
        parser = new CssSyntaxParser(atRuleSource,
            new CssParseOptions { Diagnostics = diagnostics }, default);
        block = parser.ParseComponentValue();
        block.IsClosed.Should().BeFalse();
        parser.ParseBlockContents(block).Single().Rule.Name.Should().Be("foo");
        diagnostics.Items.Count(item => item.Code == "css/unexpected-eof" &&
            item.Offset == atRuleSource.Length).Should().Be(3);
    }

    [Test]
    public void ClosedCommentsLeaveTheOuterBlockClosed()
    {
        var diagnostics = new ParseDiagnosticCollector();
        var parser = new CssSyntaxParser("{unicode-range:U+4??/**/}",
            new CssParseOptions { Diagnostics = diagnostics }, default);
        var block = parser.ParseComponentValue();
        block.IsClosed.Should().BeTrue();
        parser.ParseBlockContents(block).Single().Declarations.Single().Value.Single()
            .Token.Kind.Should().Be(CssTokenKind.UnicodeRange);
        diagnostics.Items.Any(item => item.Code == "css/unexpected-eof").Should().BeFalse();

        parser = new CssSyntaxParser("{@foo/**/}",
            new CssParseOptions { Diagnostics = diagnostics }, default);
        block = parser.ParseComponentValue();
        block.IsClosed.Should().BeTrue();
        parser.ParseBlockContents(block).Single().Rule.Name.Should().Be("foo");
        diagnostics.Items.Any(item => item.Code == "css/unexpected-eof").Should().BeFalse();
    }

    [Test]
    public void DiagnosticsAccumulateAcrossOneTopLevelOperation()
    {
        const string source = "{ x:url(a b); bad; y:z }";
        var diagnostics = new ParseDiagnosticCollector();
        var parser = new CssSyntaxParser(source, new CssParseOptions { Diagnostics = diagnostics }, default);
        var contents = parser.ParseBlockContents(parser.ParseComponentValue());
        contents[0].Declarations.Select(declaration => declaration.Name).Should().ContainInOrder("x", "y");
        diagnostics.Items.Any(item => item.Code == "css/bad-url" &&
            item.Offset == source.IndexOf("url", StringComparison.Ordinal)).Should().BeTrue();
        diagnostics.Items.Any(item => item.Code == "css/discarded-qualified-rule" &&
            item.Offset == source.IndexOf("bad", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Test]
    public void LongSiblingListsAndLimitsUseTheSameOperationContext()
    {
        var source = string.Concat(Enumerable.Repeat("a{} ", 1000));
        new CssSyntaxParser(source, null, default).ParseStyleSheet().Length.Should().Be(1000);
        var deep = "a{" + new string('(', 3000) + "x" + new string(')', 3000) + "}";
        new CssSyntaxParser(deep, null, default).ParseStyleSheet().Single().Block.Should().NotBeNull();
        Assert.Throws<ParseLimitException>(() => new CssSyntaxParser("a { b(c) }",
            new CssParseOptions { Limits = new ParseLimits { MaxNestingDepth = 1 } }, default)
            .ParseStyleSheet());
        Assert.Throws<ParseLimitException>(() => new CssSyntaxParser("a { verylong:1 }",
            new CssParseOptions { Limits = new ParseLimits { MaxTokenCharacters = 4 } }, default)
            .ParseStyleSheet());
        Assert.Throws<ParseLimitException>(() => new CssSyntaxParser("a { b:c }",
            new CssParseOptions { Limits = new ParseLimits { MaxInputCharacters = 7 } }, default)
            .ParseStyleSheet());
        using var cancellation = new CancellationTokenSource();
        var parser = new CssSyntaxParser(source, null, cancellation.Token);
        var declarationParser = new CssSyntaxParser("x:y", null, cancellation.Token);
        var blockParser = new CssSyntaxParser("{ x:y }", null, cancellation.Token);
        var block = blockParser.ParseComponentValue();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => parser.ParseStyleSheet());
        Assert.Throws<OperationCanceledException>(() => declarationParser.ParseDeclarationList());
        Assert.Throws<OperationCanceledException>(() => blockParser.ParseBlockContents(block));
    }

    [Test]
    public void BlockResultsExposeOnlyTheirMatchingPayload()
    {
        var parser = new CssSyntaxParser("{ x:y; a{} }", null, default);
        var result = parser.ParseBlockContents(parser.ParseComponentValue());
        ((object) result is IList<CssBlockItemSyntax>).Should().BeFalse();
        ((object) result[0].Declarations is IList<CssDeclarationSyntax>).Should().BeFalse();
        Assert.Throws<InvalidOperationException>(() => _ = result[0].Rule);
        Assert.Throws<InvalidOperationException>(() => _ = result[1].Declarations);
        Assert.Throws<ArgumentException>(() => parser.ParseBlockContents(default));
    }
}
