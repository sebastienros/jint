using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Values;

// CSS Values 4 §§ 4.2–4.4, Editor's Draft 2026-09-23.
[TestFixture]
public sealed class PrimitiveTextTests
{
    [Test]
    public void IdentifierPreservesDecodedTextAndSourceSpan()
    {
        var value = CssPrimitiveParser.ParseIdentifier(MarkupParser.ParseCssComponentValues(" \\41 B "), new CssValueWork(default));
        value.IsMatch.Should().BeTrue();
        value.Value.Text.Should().Be("AB");
        value.Value.Span.Start.Should().Be(1);
        value.Span.Length.Should().Be(5);
    }

    [TestCase("INITIAL")]
    [TestCase("default")]
    [TestCase("DeFaUlT")]
    [TestCase("blue")]
    [TestCase("revert-rule")]
    public void CustomIdentifierExcludesReservedAndCallerKeywords(string source)
    {
        var values = MarkupParser.ParseCssComponentValues(source);
        var result = CssPrimitiveParser.ParseCustomIdentifier(values, new[] { "BLUE" }, new CssValueWork(default));
        result.IsMatch.Should().BeFalse();
        result.Span.Start.Should().Be(0);
    }

    [Test]
    public void CustomIdentifierRetainsCaseAndAcceptsDashedText()
    {
        var result = CssPrimitiveParser.ParseCustomIdentifier(MarkupParser.ParseCssComponentValues("--Theme"),
            new[] { "other" }, new CssValueWork(default));
        result.IsMatch.Should().BeTrue();
        result.Value.Text.Should().Be("--Theme");
    }

    [TestCase("--Theme", true)]
    [TestCase("\\2d -Theme", true)]
    [TestCase("-Theme", false)]
    [TestCase("Theme", false)]
    public void DashedIdentifierRequiresTwoDecodedDashes(string source, bool expected)
    {
        var result = CssPrimitiveParser.ParseDashedIdentifier(MarkupParser.ParseCssComponentValues(source), new CssValueWork(default));
        result.IsMatch.Should().Be(expected);
    }

    [Test]
    public void StringPreservesDecodedTextAndRejectsBadString()
    {
        var good = CssPrimitiveParser.ParseString(MarkupParser.ParseCssComponentValues("  '\\41 b'  "), new CssValueWork(default));
        good.IsMatch.Should().BeTrue();
        good.Value.Text.Should().Be("Ab");
        good.Span.Start.Should().Be(2);
        var bad = CssPrimitiveParser.ParseString(MarkupParser.ParseCssComponentValues("'bad\n"), new CssValueWork(default));
        bad.IsMatch.Should().BeFalse();
        bad.Span.Start.Should().Be(0);
    }

    [TestCase("name other")]
    [TestCase("f(name)")]
    [TestCase("url(x)")]
    [TestCase("[name]")]
    public void TextMatchersRequireOneAppropriateToken(string source)
    {
        var values = MarkupParser.ParseCssComponentValues(source);
        CssPrimitiveParser.ParseString(values, new CssValueWork(default)).IsMatch.Should().BeFalse();
        CssPrimitiveParser.ParseIdentifier(values, new CssValueWork(default)).IsMatch.Should().BeFalse();
    }
}
