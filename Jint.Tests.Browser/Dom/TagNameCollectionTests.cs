namespace Jint.Tests.Browser.Dom;

/// <summary>
/// DOM §4.4's live lists of elements by qualified name and by namespace/local name.
/// </summary>
public sealed class TagNameCollectionTests
{
    [TestCase("document", TestName = "Document collection stays live")]
    [TestCase("document.getElementById('root')", TestName = "Element collection stays live")]
    public void ATagNameCollectionReadsTheCurrentTree(string context)
    {
        using var fixture = DomTestFixture.Create("<main id='root'><x-item id='first'></x-item></main>");

        fixture.Execute($$"""
            var context = {{context}};
            var items = context.getElementsByTagName('x-item');
            var root = document.getElementById('root');
            var second = document.createElement('x-item');
            root.appendChild(second);
            """);

        fixture.Number("items.length").Should().Be(2);
        fixture.Bool("items[1] === second").Should().BeTrue();

        fixture.Execute("root.removeChild(second)");
        fixture.Number("items.length").Should().Be(1);
    }

    [Test]
    public void QualifiedNamesFoldAsciiOnlyForHtmlElementsInAnHtmlDocument()
    {
        using var fixture = DomTestFixture.Create("<main id='root'><aÇ id='html'></aÇ></main>");

        fixture.Execute("""
            var root = document.getElementById('root');
            var foreign = document.createElementNS('test', 'AÇ');
            root.appendChild(foreign);
            """);

        fixture.Text("[...root.getElementsByTagName('AÇ')].map(x => x.id || 'foreign').join(',')")
            .Should().Be("html,foreign");
        fixture.Text("[...root.getElementsByTagName('aÇ')].map(x => x.id || 'foreign').join(',')")
            .Should().Be("html");
        fixture.Number("root.getElementsByTagName('aç').length").Should().Be(0);
    }

    [Test]
    public void NamespaceAndLocalNameWildcardsStayLive()
    {
        using var fixture = DomTestFixture.Create("<main id='root'></main>");

        fixture.Execute("""
            var root = document.getElementById('root');
            var allBodies = root.getElementsByTagNameNS('*', 'body');
            var allForeign = root.getElementsByTagNameNS('test', '*');
            root.appendChild(document.createElementNS('test', 'body'));
            root.appendChild(document.createElementNS('other', 'body'));
            """);

        fixture.Number("allBodies.length").Should().Be(2);
        fixture.Number("allForeign.length").Should().Be(1);

        fixture.Execute("root.appendChild(document.createElementNS('test', 'aside'))");
        fixture.Number("allForeign.length").Should().Be(2);
    }
}
