namespace Jint.Tests.Browser.Dom;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#dom-element-namespaceuri">DOM §4.9's <c>namespaceURI</c></a>: the
/// namespace an element was <b>created</b> with, which no insertion, adoption or import ever changes.
/// </summary>
/// <remarks>
/// <a href="https://github.com/sebastienros/jint/issues/3949">#3949</a> read as though adoption mutated the
/// element. It never did: AngleSharp's stored namespace is a <c>private readonly</c> field it exposes as
/// <c>GivenNamespaceUri</c>, and what moved was the <i>read</i> — <c>IElement.NamespaceUri</c> falls back to
/// an ancestor walk when nothing was stored. So every case here asserts the same element before and after it
/// is moved, and asserts that the query and <c>namespaceURI</c> give one answer rather than two.
/// </remarks>
public sealed class ElementNamespaceTests
{
    [Test]
    public void ANullNamespaceElementStaysInNoNamespaceWhenItIsAppended()
    {
        using var fixture = DomTestFixture.Create("<main id='root'></main>");

        fixture.Execute("""
            var root = document.getElementById('root');
            var detached = document.createElementNS(null, 'body');
            var empty = document.createElementNS('', 'body');
            """);

        fixture.Text("detached.namespaceURI").Should().BeNull();
        fixture.Text("empty.namespaceURI").Should().BeNull();

        fixture.Execute("root.appendChild(detached); root.appendChild(empty);");

        // The whole of #3949: appending to an HTML element used to make both of these read as XHTML.
        fixture.Text("detached.namespaceURI").Should().BeNull();
        fixture.Text("empty.namespaceURI").Should().BeNull();
    }

    [Test]
    public void TheQueryAndNamespaceUriAgreeAboutANullNamespaceElement()
    {
        using var fixture = DomTestFixture.Create("<main id='root'></main>");

        fixture.Execute("""
            var root = document.getElementById('root');
            var none = root.appendChild(document.createElementNS('', 'body'));
            var html = root.appendChild(document.createElementNS('http://www.w3.org/1999/xhtml', 'body'));
            """);

        // getElementsByTagNameNS('', '*') is the assertion the corpus names "Empty string namespace".
        fixture.Number("root.getElementsByTagNameNS('', '*').length").Should().Be(1);
        fixture.Bool("root.getElementsByTagNameNS('', '*')[0] === none").Should().BeTrue();
        fixture.Number("root.getElementsByTagNameNS(null, 'body').length").Should().Be(1);
        fixture.Number("root.getElementsByTagNameNS('http://www.w3.org/1999/xhtml', 'body').length").Should().Be(1);
        fixture.Bool("root.getElementsByTagNameNS('http://www.w3.org/1999/xhtml', 'body')[0] === html").Should().BeTrue();

        // A qualified-name query folds case only for the HTML-namespace element, and tagName has to agree
        // with it or an element answers a name no list of that name contains.
        fixture.Text("none.tagName").Should().Be("body");
        fixture.Text("html.tagName").Should().Be("BODY");
        fixture.Number("root.getElementsByTagName('BODY').length").Should().Be(1);
        fixture.Bool("root.getElementsByTagName('BODY')[0] === html").Should().BeTrue();
    }

    [Test]
    public void AnHtmlNamespaceElementIsUnaffected()
    {
        using var fixture = DomTestFixture.Create("<main id='root'><p id='parsed'></p></main>");

        fixture.Execute("""
            var root = document.getElementById('root');
            var created = root.appendChild(document.createElement('span'));
            var svg = root.appendChild(document.createElementNS('http://www.w3.org/2000/svg', 'circle'));
            """);

        fixture.Text("document.getElementById('parsed').namespaceURI").Should().Be("http://www.w3.org/1999/xhtml");
        fixture.Text("created.namespaceURI").Should().Be("http://www.w3.org/1999/xhtml");
        fixture.Text("svg.namespaceURI").Should().Be("http://www.w3.org/2000/svg");
        fixture.Text("document.documentElement.namespaceURI").Should().Be("http://www.w3.org/1999/xhtml");
        fixture.Text("created.tagName").Should().Be("SPAN");
        fixture.Text("svg.tagName").Should().Be("circle");
    }

    [Test]
    public void AdoptAndImportCarryTheCreationNamespace()
    {
        using var fixture = DomTestFixture.Create("<main id='root'></main>");

        fixture.Execute("""
            var root = document.getElementById('root');
            var other = document.implementation.createHTMLDocument('other');
            var made = other.createElementNS('', 'body');
            other.body.appendChild(made);
            var adopted = document.adoptNode(made);
            root.appendChild(adopted);
            var imported = document.importNode(other.createElementNS('', 'section'), true);
            root.appendChild(imported);
            """);

        fixture.Bool("adopted === made").Should().BeTrue();
        fixture.Text("adopted.namespaceURI").Should().BeNull();
        fixture.Text("imported.namespaceURI").Should().BeNull();
        fixture.Number("root.getElementsByTagNameNS('', '*').length").Should().Be(2);

        // Cloning copies the creation namespace rather than re-deriving one from the copy's new parent.
        fixture.Execute("var clone = root.appendChild(adopted.cloneNode(true));");
        fixture.Text("clone.namespaceURI").Should().BeNull();
        fixture.Number("root.getElementsByTagNameNS('', '*').length").Should().Be(3);
    }

    [Test]
    public void ANullNamespaceElementIsNotEqualToAnHtmlOneOfTheSameName()
    {
        using var fixture = DomTestFixture.Create("<main id='root'></main>");

        fixture.Execute("""
            var root = document.getElementById('root');
            var none = root.appendChild(document.createElementNS('', 'span'));
            var alsoNone = root.appendChild(document.createElementNS('', 'span'));
            var html = root.appendChild(document.createElementNS('http://www.w3.org/1999/xhtml', 'span'));
            """);

        // DOM §4.4 compares "A's namespace", which is the identity namespaceURI reports.
        fixture.Bool("none.isEqualNode(alsoNone)").Should().BeTrue();
        fixture.Bool("none.isEqualNode(html)").Should().BeFalse();
    }

    [Test]
    public async Task AnXmlnsAncestorStillGivesAParsedElementItsNamespace()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        // AngleSharp's XML parser records no creation namespace at all, so an element parsed from XML has
        // only the xmlns declarations in scope to resolve against. Reading the stored namespace alone would
        // answer null here and lose a namespace the parse really did resolve.
        await page.SetContentAsync(
            """
            <script>
              const doc = new DOMParser().parseFromString(
                '<svg xmlns="http://www.w3.org/2000/svg"><g><circle/></g></svg>', 'text/xml');
              const root = doc.documentElement;
              window.log = [
                root.namespaceURI,
                root.getElementsByTagName('circle')[0].namespaceURI,
                doc.getElementsByTagNameNS('http://www.w3.org/2000/svg', '*').length,
                doc.getElementsByTagNameNS('', '*').length,
                document.importNode(root, true).namespaceURI,
              ].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log"))
            .Should().Be("http://www.w3.org/2000/svg|http://www.w3.org/2000/svg|3|0|http://www.w3.org/2000/svg");
        page.Errors.Should().BeEmpty();
    }
}
