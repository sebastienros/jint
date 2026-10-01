#nullable enable
#if NET8_0_OR_GREATER
using Jint.HtmlParser;
using Jint.HtmlParser.Css;

namespace Jint.Tests.PublicInterface;

public sealed class HostCssSyntaxTests
{
    [Test]
    public void AnUnsignedConsumerCanEnumerateStylesheetAndNestedSyntax()
    {
        const string source = "@future f([a]); p{unknown:var(--pending)}";
        CssStyleSheetSyntax sheet = MarkupParser.ParseCss(source);
        sheet.Source.Should().Be(source);
        IReadOnlyList<CssRuleSyntax> rules = sheet.Rules;
        rules.Select(rule => rule.Kind).Should().Equal(CssRuleKind.AtRule, CssRuleKind.QualifiedRule);
        var function = rules.First().Prelude.Single(value => value.Kind == CssComponentKind.Function);
        function.FunctionName.Should().Be("f");
        function.Values.Single().Values.Single().Token.Text.Should().Be("a");
        rules.Last().Block!.Value.Values.Single(value => value.Kind == CssComponentKind.Function)
            .Values.Single().Token.Text.Should().Be("--pending");
        source.Substring(function.Span.Start, function.Span.Length).Should().Be("f([a])");
    }
}
#endif
