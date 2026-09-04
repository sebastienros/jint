namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#interface-htmlcollection">DOM §4.2.10.2's supported property
/// names</a> and
/// <a href="https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#dom-htmlcollection-nameditem">HTML's
/// <c>namedItem</c></a>, in the one place the two used to disagree: the empty string.
/// </summary>
/// <remarks>
/// An element may carry <c>id=""</c> or <c>name=""</c> — the HTML parser builds both — and DOM's supported
/// property names skip it explicitly, "neither the empty string nor already in result". HTML's
/// <c>namedItem</c> makes the same refusal as its own first step, before it looks at a single element. The
/// two halves have to agree, because <c>'' in collection</c>, <c>collection['']</c> and
/// <c>collection.namedItem('')</c> are three views of one answer.
/// </remarks>
public sealed class HtmlCollectionEmptyNameTests
{
    /// <summary>
    /// The corpus's own markup: three elements whose <c>id</c> or <c>name</c> is present and empty, and one
    /// of each carrying a real value so the positive case is pinned beside the negative one.
    /// </summary>
    private const string Page = """
        <!doctype html>
        <html><body>
        <div id="test">
        <div class="a" id></div>
        <div class="a" name></div>
        <a class="a" name></a>
        <div class="a" id="real"></div>
        <a class="a" name="named"></a>
        </div>
        </body></html>
        """;

    /// <summary>
    /// Every collection an element or the document hands out. The empty string is not a supported property
    /// name on any of them, so all three views of the answer agree.
    /// </summary>
    [TestCase("document.getElementsByTagName('*')", TestName = "Document.getElementsByTagName")]
    [TestCase("document.getElementById('test').getElementsByTagName('*')", TestName = "Element.getElementsByTagName")]
    [TestCase("document.getElementsByTagNameNS('http://www.w3.org/1999/xhtml', 'a')", TestName = "Document.getElementsByTagNameNS")]
    [TestCase("document.getElementById('test').getElementsByTagNameNS('http://www.w3.org/1999/xhtml', 'a')", TestName = "Element.getElementsByTagNameNS")]
    [TestCase("document.getElementsByClassName('a')", TestName = "Document.getElementsByClassName")]
    [TestCase("document.getElementById('test').getElementsByClassName('a')", TestName = "Element.getElementsByClassName")]
    [TestCase("document.getElementById('test').children", TestName = "Element.children")]
    public void TheEmptyStringIsNeverASupportedPropertyName(string collection)
    {
        Answer($$"""
            (function () {
              var c = {{collection}};
              return [
                '' in c,
                c[''] === undefined,
                c.namedItem('') === null,
              ].join('|');
            })()
            """).Should().Be("false|true|true");
    }

    /// <summary>
    /// The other half of the same rule, so the refusal above cannot be satisfied by refusing every name: a
    /// real <c>id</c> and a real <c>name</c> still answer, and still answer the same element three ways.
    /// </summary>
    [Test]
    public void ARealIdOrNameStillAnswers()
    {
        Answer("""
            (function () {
              var c = document.getElementById('test').getElementsByClassName('a');
              return [
                'real' in c,
                c['real'] === c.namedItem('real'),
                c.namedItem('real').id,
                'named' in c,
                c.namedItem('named').localName,
              ].join('|');
            })()
            """).Should().Be("true|true|real|true|a");
    }

    /// <summary>
    /// The name list and the lookup are two halves of one projection, so the empty string is absent from
    /// both: an own-property enumeration must not report a key the getter answers <c>undefined</c> for.
    /// </summary>
    [Test]
    public void TheEmptyStringIsAbsentFromTheOwnPropertyNames()
    {
        Answer("""
            (function () {
              var c = document.getElementById('test').getElementsByClassName('a');
              return Object.getOwnPropertyNames(c).indexOf('');
            })()
            """).Should().Be("-1");
    }

    /// <summary>
    /// <c>namedItem</c> answers <c>null</c> and the named getter answers <c>undefined</c> — a distinction
    /// HTML makes deliberately, and one the empty-string refusal must not flatten.
    /// </summary>
    [Test]
    public void TheTwoMissAnswersStayDifferent()
    {
        Answer("""
            (function () {
              var c = document.getElementById('test').getElementsByClassName('a');
              return [
                String(c.namedItem('')),
                String(c['']),
                String(c.namedItem('nothing')),
                String(c['nothing']),
              ].join('|');
            })()
            """).Should().Be("null|undefined|null|undefined");
    }

    private static string? Answer(string source)
    {
        using var fixture = DomTestFixture.Create(Page);

        return fixture.Text($$"""
            (function () {
              try { return String({{source}}); }
              catch (e) { return e.constructor.name + ': ' + e.message; }
            })()
            """);
    }
}
