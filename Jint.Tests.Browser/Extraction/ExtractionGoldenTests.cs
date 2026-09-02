using Jint.Browser.Extraction;
using Jint.Tests.Browser.Accessibility;

namespace Jint.Tests.Browser.Extraction;

/// <summary>
/// The two extraction payloads over the same whole pages the accessibility snapshot uses.
/// </summary>
/// <remarks>
/// One fixture set, three renderings: an agent picks one of them per task, and having the three approved
/// side by side is what makes the choice reviewable. <c>JINT_BROWSER_GOLDEN=update</c> rewrites them.
/// </remarks>
public sealed class ExtractionGoldenTests
{
    [TestCase("article")]
    [TestCase("todomvc")]
    [TestCase("nav")]
    public void RendersThePageAsMarkdown(string page)
    {
        using var document = PageFixture.Parse(GoldenFiles.Page(page), "https://jint.test/" + page + ".html");

        GoldenFiles.Approve(page + ".md", MarkdownExtractor.ToMarkdown(document));
    }

    [TestCase("article")]
    [TestCase("todomvc")]
    public void RendersThePageAsText(string page)
    {
        using var document = PageFixture.Parse(GoldenFiles.Page(page), "https://jint.test/" + page + ".html");

        GoldenFiles.Approve(page + ".text.txt", TextExtractor.InnerText(document));
    }

    [Test]
    public void MainContentOnlyDropsTheChromeOfARealPage()
    {
        using var document = PageFixture.Parse(GoldenFiles.Page("article"), "https://jint.test/article.html");

        var whole = MarkdownExtractor.ToMarkdown(document);
        var main = MarkdownExtractor.ToMarkdown(document, MarkdownOptions.Default with { MainContentOnly = true });

        whole.Should().Contain("© 2026");
        main.Should().NotContain("© 2026").And.Contain("# A headless browser on Jint");
        main.Length.Should().BeLessThan(whole.Length);
    }

    [Test]
    public void AriaHiddenContentIsStillTextOnThePage()
    {
        // aria-hidden removes a node from the accessibility tree and changes nothing about the rendering, so
        // innerText and the markdown both keep it while the accessibility snapshot does not.
        using var document = PageFixture.Parse("<p>Kept</p><p aria-hidden=true>Decoration</p><p hidden>Gone</p>");

        var expected = "Kept" + Environment.NewLine + Environment.NewLine + "Decoration";

        TextExtractor.InnerText(document).Replace("\n", Environment.NewLine).Should().Be(expected);
        MarkdownExtractor.ToMarkdown(document).Replace("\n", Environment.NewLine).Should().Be(expected);
    }
}
