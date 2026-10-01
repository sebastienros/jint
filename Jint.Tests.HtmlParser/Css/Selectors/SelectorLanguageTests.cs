#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Tests.HtmlParser.Css.Selectors;

public sealed class SelectorLanguageTests
{
    private static bool Matches(Element element, string selector)
        => SelectorMatcher.Matches(SelectorCompiler.Compile(selector, null, default), element);

    [TestCase("en-US", ":lang(en)", true)]
    [TestCase("en-US", ":lang(EN-us)", true)]
    [TestCase("en-US", ":lang(fr, en)", true)]
    [TestCase("english", ":lang(en)", false)]
    [TestCase("de-Latn-DE", ":lang('de-DE')", true)]
    [TestCase("de-Latn-DE", ":lang('*-DE')", true)]
    [TestCase("de-Latn-DE", ":lang('de-*-DE')", true)]
    [TestCase("de-x-DE", ":lang('de-DE')", false)]
    [TestCase("de-x-DE", ":lang('de-x-DE')", true)]
    [TestCase("en", ":lang('en-*')", true)]
    [TestCase("", ":lang('*')", false)]
    [TestCase("", ":lang('')", true)]
    public void LanguageRangesUseExtendedFiltering(string language, string selector, bool expected)
    {
        var element = Document.CreateHtml().CreateElement("div");
        element.SetAttribute("lang", language);
        Matches(element, selector).Should().Be(expected);
    }

    [Test]
    public void LanguageInheritsAndAnEmptyOrXmlAttributeOverridesHtml()
    {
        var document = MarkupParser.ParseHtml("<div lang=en><p><span></span></p></div>");
        var element = SelectorMatcher.QuerySelector(SelectorCompiler.Compile("span", null, default), document)!;
        Matches(element, ":lang(en)").Should().BeTrue();
        var parent = (Element) element.ParentNode!;
        parent.SetAttribute("lang", "");
        Matches(element, ":lang(en)").Should().BeFalse();
        Matches(element, ":lang('')").Should().BeTrue();
        parent.SetAttributeNS(Namespaces.Xml, "xml:lang", "fr");
        Matches(element, ":lang(fr)").Should().BeTrue();
        parent.RemoveAttributeNS(Namespaces.Xml, "lang");
        parent.RemoveAttribute("lang");
        Matches(element, ":lang(en)").Should().BeTrue();
    }

    [Test]
    public void XmlElementsUseXmlLanguageRatherThanAnUnqualifiedAttribute()
    {
        var document = MarkupParser.ParseXml("<root xml:lang='fr'><child lang='en'/></root>");
        var child = (Element) document.DocumentElement!.FirstChild!;
        Matches(child, ":lang(fr)").Should().BeTrue();
        Matches(child, ":lang(en)").Should().BeFalse();
    }

    [Test]
    public void LanguageScanningRemainsCancellable()
    {
        var element = Document.CreateHtml().CreateElement("div");
        element.SetAttribute("lang", new string('x', 100_000));
        var program = SelectorCompiler.Compile(":lang(en)", null, default);
        using var cancellation = new CancellationTokenSource();
        var checkpoints = 0;
        Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.Matches(program, element, null, () =>
            {
                if (++checkpoints == 2) cancellation.Cancel();
            }, cancellation.Token));
        checkpoints.Should().Be(2);
    }
}
