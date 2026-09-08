namespace Jint.Tests.Browser.Dom;

/// <summary>DOM's live lists of elements by class names.</summary>
public sealed class ClassNameCollectionTests
{
    [TestCase("document")]
    [TestCase("document.getElementById('root')")]
    public void SavedCollectionObservesInsertionRemovalAndClassChanges(string context)
    {
        using var fixture = DomTestFixture.Create("<main id='root'><span id='first' class='match'></span></main>");
        fixture.Execute($$"""
            var root = document.getElementById('root');
            var first = document.getElementById('first');
            var items = {{context}}.getElementsByClassName('match');
            var second = document.createElement('span');
            second.id = 'second';
            second.className = 'match';
            root.appendChild(second);
            """);

        fixture.Number("items.length").Should().Be(2);
        fixture.Bool("items[1] === second && items.namedItem('second') === second").Should().BeTrue();
        fixture.Execute("first.className = 'other'");
        fixture.Text("[...items].map(x => x.id).join(',')").Should().Be("second");
        fixture.Execute("first.classList.add('match')");
        fixture.Number("items.length").Should().Be(2);
        fixture.Execute("root.removeChild(second)");
        fixture.Number("items.length").Should().Be(1);
        fixture.Bool("items[0] === first && items.namedItem('second') === null").Should().BeTrue();
    }

    [Test]
    public void DetachedRootExcludesItselfAndConvertsClassNamesOnce()
    {
        using var fixture = DomTestFixture.Create("<body></body>");
        fixture.Execute("""
            var root = document.createElement('div');
            root.className = 'a b';
            var conversions = 0;
            var items = root.getElementsByClassName({ toString() { conversions++; return 'a b'; } });
            var child = document.createElement('span');
            child.className = 'a';
            root.appendChild(child);
            """);

        fixture.Number("items.length").Should().Be(0);
        fixture.Execute("child.classList.add('b')");
        fixture.Number("items.length").Should().Be(1);
        fixture.Bool("items.item(0) === child").Should().BeTrue();
        fixture.Number("conversions").Should().Be(1);
    }
}
