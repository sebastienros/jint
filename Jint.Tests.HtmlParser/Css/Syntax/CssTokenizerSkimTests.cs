#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.Tests.HtmlParser.Css.Syntax;

/// <summary>
/// A skimming tokenizer must behave exactly as a full one apart from the text it leaves unbuilt:
/// the rule scan relies on identical spans, limits, work, diagnostics and EOF recovery.
/// </summary>
[TestFixture]
public sealed class CssTokenizerSkimTests
{
    private static readonly string[] SkimSources =
    [
        "a\r\nb 'x\\\r\n\\41' \U0001F600c url( q\\29 ) 1e3px -\\-v #-\\31 @m\\65 dia /* c",
        "--brand: #123456; -\\-x: 1; \\2d-y: 2; -z: 3",
        "a{background:url(data:image/png;base64,QUJD) no-repeat;content:\"\\201C x\"}",
        "@\\6d edia (min-width: 1px) { u\\rl(x) URL( 'y' ) url(a b) }",
        "'unterminated",
        "url(unterminated",
        "\"bad\nstring\" /* unterminated comment",
        "1 +2 -3.5e+2 4% 5\\70 x 6\U0001F600 .7em",
        "#\U0001F600 #a\\ b \t\r\n\f \u0000 \ud800 x",
        // Long enough to cross the tokenizer's 1024-unit cooperative-work cadence several times.
        "x{a:url(" + new string('q', 3000) + ") 'z" + new string('w', 3000) + "'; --v:" + new string('n', 3000) + "}",
    ];

    [TestCaseSource(nameof(SkimSources))]
    public void ASkimmedStreamMatchesTheFullStreamApartFromText(string source)
    {
        foreach (var limit in new[] { 0, 4 })
        {
            var full = Scan((d, poll) => new CssTokenizer(source, limit, d, default, baseOffset: 7, checkpoint: poll),
                skimmed: false);
            var skim = Scan((d, poll) => new CssTokenizer(source, limit, d, default, baseOffset: 7, checkpoint: poll),
                skimmed: true);
            skim.Should().Equal(full, $"limit {limit}");
        }
    }

    [TestCaseSource(nameof(SkimSources))]
    public void ASkimmedStreamLeavesValueTextUnbuiltButKeepsRuleNames(string source)
    {
        var full = Tokens(new CssTokenizer(source, 0, null, default), skimmed: false);
        var skim = Tokens(new CssTokenizer(source, 0, null, default), skimmed: true);
        skim.Should().HaveCount(full.Count);
        for (var i = 0; i < full.Count; i++)
        {
            switch (skim[i].Kind)
            {
                case CssTokenKind.String or CssTokenKind.Url or CssTokenKind.Whitespace:
                    skim[i].Text.Should().BeEmpty();
                    break;
                case CssTokenKind.Number or CssTokenKind.Percentage or CssTokenKind.Dimension:
                    skim[i].NumberText.Should().BeNullOrEmpty();
                    break;
                case CssTokenKind.AtKeyword or CssTokenKind.Function:
                    skim[i].Text.Should().Be(full[i].Text);
                    break;
            }
        }
    }

    // CSS Syntax 3 §5.4.3: the custom-property check reads the decoded name, so an escaped hyphen
    // still counts though the skimming scan leaves a plain name unbuilt.
    [TestCase("--x:{a:b} p{}", 1)]
    [TestCase("-\\-x:{a:b} p{}", 1)]
    [TestCase("\\2d-x:{a:b} p{}", 1)]
    [TestCase("--:{a:b} p{}", 1)]
    [TestCase("-x:{a:b} p{}", 2)]
    [TestCase("-\\61 --:{a:b} p{}", 2)]
    public void TheRuleScanStillDiscardsCustomPropertyLookalikes(string source, int rules)
    {
        var diagnostics = new ParseDiagnosticCollector();
        var result = CssParser.ParseRuleList(source, new(default), new CssParseOptions { Diagnostics = diagnostics });
        result.Should().HaveCount(rules);
        diagnostics.Items.Any(item => item.Code == "css/discarded-custom-property-rule").Should().Be(rules == 1);
    }

    [Test]
    public void ANestedRuleListKeepsOriginalOffsets()
    {
        const string source = "/* lead */ @media screen { a { color: red } @supports (x: y) { b { c: d } } } tail { }";
        var work = new Jint.HtmlParser.Css.Values.CssValueWork(default);
        var media = CssParser.ParseRuleList(source, work)[0];
        var children = CssParser.ParseRuleList(CssParser.ParseRule(media, work)!.Value.Children!.Value, work);
        children.Select(rule => rule.Text.Text).Should().Equal("a { color: red }", "@supports (x: y) { b { c: d } }");
        children.Select(rule => rule.Text.Span.Start).Should().Equal(
            source.IndexOf("a {", StringComparison.Ordinal), source.IndexOf("@supports", StringComparison.Ordinal));
        children[1].Name.Should().Be("supports");
    }

    private static CssToken Next(CssTokenizer tokenizer, bool skimmed) =>
        skimmed ? tokenizer.NextSkimmed() : tokenizer.Next();

    private static List<CssToken> Tokens(CssTokenizer tokenizer, bool skimmed)
    {
        var tokens = new List<CssToken>();
        CssToken token;
        while ((token = Next(tokenizer, skimmed)).Kind != CssTokenKind.None) tokens.Add(token);
        return tokens;
    }

    // One line per token, then the EOF recovery suffix, the diagnostics, any limit failure and the
    // number of cooperative checkpoints the scan reached.
    private static List<string> Scan(Func<ParseDiagnosticCollector, Action, CssTokenizer> create, bool skimmed)
    {
        var diagnostics = new ParseDiagnosticCollector();
        var lines = new List<string>();
        var checkpoints = 0;
        var tokenizer = create(diagnostics, () => checkpoints++);
        try
        {
            CssToken token;
            while ((token = Next(tokenizer, skimmed)).Kind != CssTokenKind.None)
            {
                var line = $"{token.Kind}@{token.Span.Start}+{token.Span.Length} d={(int) token.Delimiter} " +
                    $"int={token.IsInteger} id={token.IsIdHash} " +
                    $"u={token.UnicodeRangeStart}-{token.UnicodeRangeEnd}";
                if (token.Kind is CssTokenKind.AtKeyword or CssTokenKind.Function) line += $" name={token.Text}";
                lines.Add(line);
            }
        }
        catch (ParseLimitException error)
        {
            lines.Add($"limit {error.Kind} {error.Observed}");
        }
        lines.Add("suffix=" + tokenizer.EofRecoverySuffix);
        lines.AddRange(diagnostics.Items.Select(item => $"{item.Code}@{item.Offset}"));
        lines.Add("checkpoints=" + checkpoints);
        return lines;
    }
}
