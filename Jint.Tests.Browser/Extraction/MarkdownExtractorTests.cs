using Jint.Browser.Extraction;
using Jint.Tests.Browser.Accessibility;

namespace Jint.Tests.Browser.Extraction;

/// <summary>
/// The CommonMark rendering, one construct at a time and then the whole-page options.
/// </summary>
public sealed class MarkdownExtractorTests
{
    private static IEnumerable<TestCaseData> Constructs()
    {
        // Headings and paragraphs.
        yield return Case("<h1>Title</h1>", "# Title");
        yield return Case("<h3>Sub</h3>", "### Sub");
        yield return Case("<p>One.</p><p>Two.</p>", "One.\n\nTwo.");
        yield return Case("<p>  spread   over\n  lines  </p>", "spread over lines");

        // Emphasis and code.
        yield return Case("<p>a <strong>b</strong> c</p>", "a **b** c");
        yield return Case("<p>a <b>b</b> c</p>", "a **b** c");
        yield return Case("<p>a <em>b</em> c</p>", "a *b* c");
        yield return Case("<p>a <i>b</i> c</p>", "a *b* c");
        yield return Case("<p>a <del>b</del> c</p>", "a ~~b~~ c");
        yield return Case("<p>use <code>x = 1</code> here</p>", "use `x = 1` here");
        // A backtick inside a code span needs a longer delimiter, not a doubled one.
        yield return Case("<p>use <code>a`b</code> here</p>", "use ``a`b`` here");
        yield return Case("<p>use <code>`x`</code> here</p>", "use `` `x` `` here");
        yield return Case("<p>a<strong></strong>b</p>", "ab");

        // Fenced code, with the language from the class.
        yield return Case("<pre><code class='language-csharp'>var x = 1;</code></pre>", "```csharp\nvar x = 1;\n```");
        yield return Case("<pre><code>plain</code></pre>", "```\nplain\n```");
        yield return Case("<pre>  kept  \n  as is</pre>", "```\n  kept  \n  as is\n```");

        // Links and images, resolved against the document's base.
        yield return Case("<p><a href='/docs'>Docs</a></p>", "[Docs](https://example.com/docs)");
        yield return Case("<p><a href='https://x.test/'>X</a></p>", "[X](https://x.test/)");
        yield return Case("<p><a>no href</a></p>", "no href");
        yield return Case("<p><img src='pic.png' alt='A cat'></p>", "![A cat](https://example.com/dir/pic.png)");
        yield return Case("<p><a href='/x'><img src='pic.png' alt='Logo'></a></p>", "[![Logo](https://example.com/dir/pic.png)](https://example.com/x)");
        // An empty alt is HTML's way of saying the image is decoration, so it is not content.
        yield return Case("<p>a<img src='deco.png' alt=''>b</p>", "ab");
        yield return Case("<p><img src='pic.png'></p>", "![](https://example.com/dir/pic.png)");

        // Lists.
        yield return Case("<ul><li>a</li><li>b</li></ul>", "- a\n- b");
        yield return Case("<ol><li>a</li><li>b</li></ol>", "1. a\n2. b");
        yield return Case("<ol start=3><li>a</li></ol>", "3. a");
        yield return Case("<ul><li>a<ul><li>b</li></ul></li></ul>", "- a\n\n  - b");
        yield return Case("<ul><li><p>a</p><p>b</p></li></ul>", "- a\n\n  b");

        // Block quotes.
        yield return Case("<blockquote><p>quoted</p></blockquote>", "> quoted");
        yield return Case("<blockquote><p>a</p><p>b</p></blockquote>", "> a\n>\n> b");

        // Rules and hard breaks.
        yield return Case("<hr>", "---");
        yield return Case("<p>a<br>b</p>", "a\\\nb");

        // Definition lists.
        yield return Case("<dl><dt>Term</dt><dd>Meaning</dd></dl>", "**Term**\\\n  Meaning");

        // Disclosure.
        yield return Case("<details><summary>More</summary><p>Body</p></details>", "**More**\n\nBody");

        // Escaping.
        yield return Case("<p>a * b _ c [d] `e`</p>", "a \\* b \\_ c \\[d\\] \\`e\\`");
        yield return Case("<p>snake_case stays</p>", "snake_case stays");
        yield return Case("<p>2 &lt; 3</p>", "2 \\< 3");

        // Skipped content.
        yield return Case("<p>a</p><script>var x=1</script><style>p{color:red}</style><noscript>b</noscript>", "a");
        yield return Case("<p>a</p><div hidden><p>b</p></div>", "a");
        yield return Case("<p>a</p><div style='display:none'><p>b</p></div>", "a");
    }

    private static TestCaseData Case(string html, string expected) =>
        new TestCaseData(html, expected).SetArgDisplayNames(html, expected.Replace("\n", "\\n"));

    [TestCaseSource(nameof(Constructs))]
    public void RendersTheConstruct(string html, string expected)
    {
        using var document = PageFixture.Parse(html, "https://example.com/dir/index.html");
        MarkdownExtractor.ToMarkdown(document).Should().Be(expected);
    }

    [Test]
    public void RendersAGitHubFlavouredTable()
    {
        using var document = PageFixture.Parse(
            "<table><thead><tr><th>Name</th><th>Qty</th></tr></thead>" +
            "<tbody><tr><td>Apples</td><td>3</td></tr><tr><td>Pears</td><td>12</td></tr></tbody></table>");

        MarkdownExtractor.ToMarkdown(document).Should().Be(
            """
            | Name | Qty |
            | --- | --- |
            | Apples | 3 |
            | Pears | 12 |
            """.Replace("\r\n", "\n"));
    }

    [Test]
    public void ATableWithNoHeaderRowStillGetsTheSeparatorGfmRequires()
    {
        using var document = PageFixture.Parse("<table><tr><td>a</td><td>b</td></tr></table>");

        MarkdownExtractor.ToMarkdown(document).Should().Be(
            """
            |  |  |
            | --- | --- |
            | a | b |
            """.Replace("\r\n", "\n"));
    }

    [Test]
    public void ATableCaptionBecomesABoldLineAboveIt()
    {
        using var document = PageFixture.Parse("<table><caption>Stock</caption><tr><th>a</th></tr></table>");

        MarkdownExtractor.ToMarkdown(document).Should().StartWith("**Stock**\n\n| a |");
    }

    [Test]
    public void ANestedTablesRowsStayInTheNestedTable()
    {
        using var document = PageFixture.Parse("<table><tr><td>outer<table><tr><td>inner</td></tr></table></td></tr></table>");

        var markdown = MarkdownExtractor.ToMarkdown(document);

        markdown.Split('\n').Should().HaveCount(3, "the outer table has one header row, one separator and one body row");
        markdown.Should().Contain("outer").And.Contain("inner");
    }

    [Test]
    public void APipeInACellIsEscaped()
    {
        using var document = PageFixture.Parse("<table><tr><td>a|b</td></tr></table>");

        MarkdownExtractor.ToMarkdown(document).Should().Contain("a\\|b");
    }

    [Test]
    public void ImagesCanBeReducedToTheirAlternativeText()
    {
        using var document = PageFixture.Parse("<p>see <img src=pic.png alt='the cat'> now</p>");

        MarkdownExtractor.ToMarkdown(document, MarkdownOptions.Default with { IncludeImages = false })
            .Should().Be("see the cat now");
    }

    [Test]
    public void MainContentOnlyPrefersMainThenRoleMainThenArticle()
    {
        const string Chrome = "<nav><a href='/'>Home</a></nav><footer><p>Footer</p></footer>";

        using var withMain = PageFixture.Parse("<main><p>Main body</p></main>" + Chrome);
        Markdown(withMain).Should().Be("Main body");

        using var withRole = PageFixture.Parse("<div role=main><p>Role body</p></div>" + Chrome);
        Markdown(withRole).Should().Be("Role body");

        using var withArticle = PageFixture.Parse("<article><p>Article body</p></article>" + Chrome);
        Markdown(withArticle).Should().Be("Article body");

        using var withNeither = PageFixture.Parse("<p>Everything</p>" + Chrome);
        Markdown(withNeither).Should().Contain("Everything").And.Contain("Home").And.Contain("Footer");

        static string Markdown(AngleSharp.Dom.IDocument document) =>
            MarkdownExtractor.ToMarkdown(document, MarkdownOptions.Default with { MainContentOnly = true });
    }

    [Test]
    public void MaxLengthTruncatesAtAWordBoundaryAndSaysSo()
    {
        using var document = PageFixture.Parse("<p>" + string.Join(" ", Enumerable.Repeat("word", 200)) + "</p>");

        var full = MarkdownExtractor.ToMarkdown(document);
        var cut = MarkdownExtractor.ToMarkdown(document, MarkdownOptions.Default with { MaxLength = 100 });

        full.Length.Should().BeGreaterThan(100);
        cut.Length.Should().BeLessThanOrEqualTo(100);
        cut.Should().EndWith(MarkdownExtractor.TruncationMarker);
        cut.Should().StartWith("word word");
        cut[..^MarkdownExtractor.TruncationMarker.Length].Should().NotEndWith("wor");
    }

    [Test]
    public void MaxLengthLeavesAShortDocumentAlone()
    {
        using var document = PageFixture.Parse("<p>short</p>");

        MarkdownExtractor.ToMarkdown(document, MarkdownOptions.Default with { MaxLength = 100 }).Should().Be("short");
    }

    [Test]
    public void RendersAWholePageInOnePass()
    {
        using var document = PageFixture.Parse(
            """
            <html><head><title>Release notes</title></head><body>
            <header><nav><a href="/">Home</a> <a href="/docs">Docs</a></nav></header>
            <main>
              <h1>Release notes</h1>
              <p>The <strong>headline</strong> change is <a href="/pr/1">the parser baton</a>.</p>
              <h2>Fixed</h2>
              <ul><li>A <code>null</code> reference.</li><li>A hang under load.</li></ul>
              <pre><code class="language-bash">dotnet test -c Release</code></pre>
              <blockquote><p>Upgrade before the next release.</p></blockquote>
            </main>
            <footer><p>&copy; 2026</p></footer>
            </body></html>
            """,
            "https://jint.test/notes.html");

        var markdown = MarkdownExtractor.ToMarkdown(document, MarkdownOptions.Default with { MainContentOnly = true });

        markdown.Should().Be(
            """
            # Release notes

            The **headline** change is [the parser baton](https://jint.test/pr/1).

            ## Fixed

            - A `null` reference.
            - A hang under load.

            ```bash
            dotnet test -c Release
            ```

            > Upgrade before the next release.
            """.Replace("\r\n", "\n"));
    }
}
