namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <a href="https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#dom-htmlcollection-nameditem">HTML's
/// <c>HTMLCollection.namedItem</c></a>: the <b>first</b> element in tree order whose ID is the key, or which
/// is in the HTML namespace and whose <c>name</c> content attribute is the key.
/// </summary>
/// <remarks>
/// AngleSharp's string indexer walks every id first and only then every <c>name</c>, and matches <c>name</c>
/// on an element in any namespace — so the two halves of the projection, the lookup and the supported-property
/// name list, could disagree about one object.
/// </remarks>
public sealed class HtmlCollectionNamedItemTests
{
    [Test]
    public void ANameOnANonHtmlElementIsNeitherFoundNorExposed()
    {
        using var fixture = DomTestFixture.Create("<!doctype html><html><body><div id='root'></div></body></html>");

        fixture.Text(
            """
            var root = document.getElementById('root');
            var alien = document.createElementNS('', 'img');
            alien.setAttribute('name', 'qux');
            root.appendChild(alien);
            var children = root.children;
            [children.namedItem('qux'), 'qux' in children, children.qux].map(v => String(v)).join('|');
            """).Should().Be("null|false|undefined");
    }

    [Test]
    public void AnIdOnANonHtmlElementIsStillFound()
    {
        using var fixture = DomTestFixture.Create("<!doctype html><html><body><div id='root'></div></body></html>");

        fixture.Bool(
            """
            var root = document.getElementById('root');
            var alien = document.createElementNS('', 'img');
            alien.setAttribute('id', 'baz');
            root.appendChild(alien);
            root.children.namedItem('baz') === alien && 'baz' in root.children;
            """).Should().BeTrue();
    }

    [Test]
    public void TheFirstMatchInTreeOrderWins()
    {
        using var fixture = DomTestFixture.Create(
            "<!doctype html><html><body><div id='root'><img name='foo'><img id='foo'></div></body></html>");

        // AngleSharp answers the id match because it walks every id before any name; HTML's algorithm is one
        // pass, so the earlier `name` match is the answer.
        fixture.Bool(
            """
            var root = document.getElementById('root');
            root.children.namedItem('foo') === root.children.item(0);
            """).Should().BeTrue();
    }

    [Test]
    public void AnEmptyNameMatchesNothing()
    {
        using var fixture = DomTestFixture.Create(
            "<!doctype html><html><body><div id='root'><img id='' name=''></div></body></html>");

        fixture.Evaluate("document.getElementById('root').children.namedItem('')").IsNull().Should().BeTrue();
    }
}
