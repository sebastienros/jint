#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Tests.HtmlParser.Css.Selectors;

// Selectors 4 §3.6.1: a ::before/::after subject matches through its originating element only on the cascade's
// pseudo-element lane; element-only matching still never returns the originating element.
[TestFixture]
public sealed class SelectorPseudoElementTests
{
    private static CompiledSelector Parse(string source) => SelectorCompiler.Compile(source, null, default);

    private static bool MatchPseudo(string source, Element element, SelectorPseudoElement pseudo, out SelectorSpecificity specificity)
    {
        var work = new SelectorMatchWork(element, default);
        return SelectorMatcher.TryMatchPseudoElement(Parse(source), element, pseudo, out specificity, null, default, ref work);
    }

    private static bool MatchElement(string source, Element element)
    {
        var work = new SelectorMatchWork(element, default);
        return SelectorMatcher.Matches(Parse(source), element, null, default, ref work);
    }

    [TestCase("#x::after", true)]
    [TestCase("p#x::after", true)]
    [TestCase("div > #x::after", true)]
    [TestCase("#x:after", true)]
    [TestCase("#x::AFTER", true)]
    [TestCase("::after", true)]
    [TestCase("#x::before", false)]
    [TestCase("#y::after", false)]
    [TestCase("span > #x::after", false)]
    [TestCase("#x", false)]
    [TestCase("#x::after:hover", false)]
    [TestCase("#x::selection", false)]
    public void AfterMatchesThroughItsOriginatingElement(string selector, bool expected)
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        var element = document.CreateElement("p");
        element.SetAttribute("id", "x");
        parent.AppendChild(element);
        document.AppendChild(document.CreateElement("html")).AppendChild(parent);

        MatchPseudo(selector, element, SelectorPseudoElement.After, out _).Should().Be(expected);
        if (selector.Contains(':')) MatchElement(selector, element).Should().BeFalse();
    }

    [Test]
    public void EachBranchKeepsItsOwnSubjectAndSpecificity()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("p");
        element.SetAttribute("id", "x");
        document.AppendChild(document.CreateElement("html")).AppendChild(element);

        // The highest-specificity branch whose subject is the pseudo-element wins; #x.missing never matches.
        MatchPseudo("p::before, #x::before, #x.missing::before, #x", element, SelectorPseudoElement.Before, out var specificity)
            .Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(1, 0, 1));
        MatchElement("#x::before, #x", element).Should().BeTrue();
        MatchPseudo("#x", element, SelectorPseudoElement.Before, out _).Should().BeFalse();
    }
}
