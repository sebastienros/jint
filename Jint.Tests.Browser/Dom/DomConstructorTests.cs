namespace Jint.Tests.Browser.Dom;

/// <summary>
/// The interfaces WebIDL really does give a constructor, and the ones it does not.
/// </summary>
/// <remarks>
/// AngleSharp puts <c>[DomConstructor]</c> on no <c>[DomName]</c> interface, so the generator can never
/// learn that an interface is constructible and <c>DomConstructors</c> is the table it is written in by
/// hand. The point of these rows is that the table is short and that everything outside it is still
/// <c>Illegal constructor</c>, which is what a browser answers too.
/// </remarks>
public sealed class DomConstructorTests
{
    private const string Page = """
        <!doctype html>
        <html><body><div id="a">hello</div></body></html>
        """;

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-comment-comment and
    /// https://dom.spec.whatwg.org/#dom-text-text: <c>constructor(optional DOMString data = "")</c>, whose
    /// node document is the current global object's associated <c>Document</c>.
    /// </summary>
    [TestCase("new Comment('x').data", "x")]
    [TestCase("new Comment().data", "")]
    [TestCase("new Comment('x').nodeType", "8")]
    [TestCase("new Comment('x') instanceof Comment", "true")]
    [TestCase("new Comment('x') instanceof CharacterData", "true")]
    [TestCase("Object.prototype.toString.call(new Comment('x'))", "[object Comment]")]
    [TestCase("new Comment('x').ownerDocument !== null", "true")]
    [TestCase("new Text('x').data", "x")]
    [TestCase("new Text().data", "")]
    [TestCase("new Text('x').nodeType", "3")]
    [TestCase("new Text('x') instanceof Text", "true")]
    [TestCase("Object.prototype.toString.call(new Text('x'))", "[object Text]")]
    [TestCase("new Text('x').ownerDocument !== null", "true")]
    [TestCase("document.getElementById('a').appendChild(new Text('!')).data", "!")]
    public void CommentAndTextTakeTheirData(string source, string expected)
    {
        Answer(source).Should().Be(expected);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-range-range: the new range's start and end are that document at
    /// offset 0, so it is collapsed on the document node.
    /// </summary>
    [TestCase("new Range() instanceof Range", "true")]
    [TestCase("Object.prototype.toString.call(new Range())", "[object Range]")]
    [TestCase("new Range().collapsed", "true")]
    [TestCase("new Range().startOffset", "0")]
    [TestCase("new Range().startContainer.nodeType", "9")]
    [TestCase("new Range().endContainer.nodeType", "9")]
    public void ARangeStartsCollapsedOnTheDocument(string source, string expected)
    {
        Answer(source).Should().Be(expected);
    }

    /// <summary>
    /// And everything else still refuses, which is what keeps the table meaningful rather than a habit.
    /// </summary>
    [TestCase("new Node()")]
    [TestCase("new Element()")]
    [TestCase("new CharacterData()")]
    [TestCase("new NodeList()")]
    [TestCase("new HTMLDivElement()")]
    public void EveryOtherInterfaceObjectIsStillIllegal(string source)
    {
        Answer(source).Should().Be("TypeError: Illegal constructor");
    }

    /// <summary>
    /// On a real page the associated <c>Document</c> is the page's, which is the answer the standard names
    /// and the one every wpt row asserts. The fixture above is the binding on its own, where there is no
    /// page and the node gets an empty document of its own instead — the same fallback
    /// <c>new DocumentFragment()</c> has always taken.
    /// </summary>
    [Test]
    public async Task OnAPageTheNodeDocumentIsThePagesOwn()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id=\"a\"></div>");

        (await page.EvaluateAsync<string>("""
            [
              new Comment('x').ownerDocument === document,
              new Text('x').ownerDocument === document,
              new Range().startContainer === document,
              new Range().endContainer === document,
            ].join('|')
            """)).Should().Be("true|true|true|true");
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
