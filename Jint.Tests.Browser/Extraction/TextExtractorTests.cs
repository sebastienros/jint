using Jint.Browser.Extraction;
using Jint.Tests.Browser.Accessibility;

namespace Jint.Tests.Browser.Extraction;

/// <summary>
/// HTML's rendered text collection steps, case by case.
/// </summary>
/// <remarks>
/// The cases are written from the algorithm and from what a browser answers for the same markup, not
/// vendored from web-platform-tests: the corpus's <c>innerText</c> files are not in this repository's WPT
/// tree, and its getter tests assert against a real layout, which this has none of.
/// </remarks>
public sealed class TextExtractorTests
{
    private static IEnumerable<TestCaseData> Cases()
    {
        // Collapsing, and the trim at the ends of the root.
        yield return Case("<div id=t>abc</div>", "abc");
        yield return Case("<div id=t>  abc  </div>", "abc");
        yield return Case("<div id=t>a\n  b   c</div>", "a b c");
        yield return Case("<div id=t>a <b>b</b> c</div>", "a b c");
        yield return Case("<div id=t>a<b>b</b>c</div>", "abc");
        yield return Case("<div id=t>a <b> b </b> c</div>", "a b c");

        // <br> is a literal line feed rather than a required line break.
        yield return Case("<div id=t>abc<br>def</div>", "abc\ndef");
        yield return Case("<div id=t>abc<br>  def</div>", "abc\ndef");
        yield return Case("<div id=t>abc<br><br>def</div>", "abc\n\ndef");

        // One required line break around a block-level box, two around a paragraph, then the runs collapse
        // to their maximum and the ends are trimmed.
        yield return Case("<div id=t><div>a</div><div>b</div></div>", "a\nb");
        yield return Case("<div id=t>a<div>b</div>c</div>", "a\nb\nc");
        yield return Case("<div id=t><p>a</p><p>b</p></div>", "a\n\nb");
        yield return Case("<div id=t><div>a</div><p>b</p><div>c</div></div>", "a\n\nb\n\nc");
        yield return Case("<div id=t><h1>Title</h1><p>Body</p></div>", "Title\n\nBody");

        // AngleSharp.Css's default sheet has no rule for the HTML5 flow elements, so this is the table in
        // HtmlDisplay answering rather than the cascade.
        yield return Case("<div id=t><section>a</section><article>b</article></div>", "a\nb");
        yield return Case("<div id=t><nav>a</nav><aside>b</aside><main>c</main></div>", "a\nb\nc");
        yield return Case("<div id=t><figure>a</figure><figcaption>b</figcaption></div>", "a\nb");

        // Lists.
        yield return Case("<ul id=t><li>a</li><li>b</li></ul>", "a\nb");

        // Tables: a tab between cells, a line feed between rows, and nothing extra around either.
        yield return Case("<table id=t><tr><td>a</td><td>b</td></tr><tr><td>c</td><td>d</td></tr></table>", "a\tb\nc\td");
        yield return Case("<table id=t><thead><tr><th>h</th></tr></thead><tbody><tr><td>a</td></tr></tbody></table>", "h\na");
        yield return Case("<table id=t><caption>Cap</caption><tr><td>a</td></tr></table>", "Cap\na");

        // White space is preserved where CSS preserves it.
        yield return Case("<pre id=t>  a  b  </pre>", "  a  b  ");
        yield return Case("<div id=t><pre>a\n  b</pre></div>", "a\n  b");
        yield return Case("<textarea id=t>  keep  this  </textarea>", "  keep  this  ");
        yield return Case("<div id=t style='white-space:pre'>  a  b  </div>", "  a  b  ");

        // Hidden content contributes nothing at all, breaks included.
        yield return Case("<div id=t>a<span style='display:none'>b</span>c</div>", "ac");
        // A `display: none` box is not laid out at all, so it contributes no required line break either.
        yield return Case("<div id=t>a<div hidden>b</div>c</div>", "ac");
        yield return Case("<div id=t>a<span style='visibility:hidden'>b</span>c</div>", "ac");

        // Metadata content is never text.
        yield return Case("<div id=t>a<script>var x=1</script><style>p{color:red}</style>b</div>", "ab");
        yield return Case("<div id=t>a<template>hidden</template>b</div>", "ab");

        // Nothing to say.
        yield return Case("<div id=t></div>", "");
        yield return Case("<div id=t>   </div>", "");
    }

    private static TestCaseData Case(string html, string expected) =>
        new TestCaseData(html, expected).SetArgDisplayNames(html, Escape(expected));

    private static string Escape(string text) => text.Length == 0 ? "(empty)" : text.Replace("\n", "\\n").Replace("\t", "\\t");

    [TestCaseSource(nameof(Cases))]
    public void CollectsTheRenderedText(string html, string expected)
    {
        using var document = PageFixture.Parse(html);
        TextExtractor.InnerText(document.GetElementById("t")!).Should().Be(expected);
    }

    [Test]
    public void ADocumentWithNoElementIdentifiedAnswersItsBody()
    {
        using var document = PageFixture.Parse("<h1>Title</h1><p>Body</p>");

        TextExtractor.InnerText(document).Should().Be("Title\n\nBody");
    }

    [Test]
    public void ADisplayNoneFromAStyleSheetIsSkippedToo()
    {
        using var document = PageFixture.Parse("<style>.gone{display:none}</style><div id=t>a<span class=gone>b</span>c</div>");

        TextExtractor.InnerText(document.GetElementById("t")!).Should().Be("ac");
    }

    [Test]
    public void WithoutTheCascadeTheInlineStyleStillAnswers()
    {
        using var document = PageFixture.ParseWithoutCss("<div id=t>a<span style='display:none'>b</span>c<div>d</div></div>");

        // The block break still lands, because HtmlDisplay's table is what supplies it either way.
        TextExtractor.InnerText(document.GetElementById("t")!).Should().Be("ac\nd");
    }

    [Test]
    public void ASpanTurnedIntoABlockByTheAuthorBreaksTheLine()
    {
        // The declared value only wins where it differs from HTML's suggested rendering, which is what keeps
        // AngleSharp's incomplete default sheet from calling every <section> inline.
        using var document = PageFixture.Parse("<div id=t>a<span style='display:block'>b</span>c</div>");

        TextExtractor.InnerText(document.GetElementById("t")!).Should().Be("a\nb\nc");
    }
}
