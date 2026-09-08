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

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-getelementsbyclassname - the comparison is ASCII case-insensitive
    /// while the root's node document is in quirks mode, and exact otherwise.
    /// </summary>
    [Test]
    public void QuirksModeFoldsAsciiCaseAndNothingElse()
    {
        // No doctype, so compatMode is "BackCompat".
        using var fixture = DomTestFixture.Create(
            "<html class='a A'><body class='a a'><span id='ascii' class='K'></span>"
            + "<span id='kelvin' class='\u212A'></span></body></html>");

        fixture.Text("document.compatMode").Should().Be("BackCompat");
        fixture.Text("[...document.getElementsByClassName('A a')].map(x => x.localName).join(',')")
            .Should().Be("html,body");

        // U+212A KELVIN SIGN is outside ASCII, so it keeps its own identity even in quirks mode - which is
        // what OrdinalIgnoreCase would not have done.
        fixture.Text("[...document.getElementsByClassName('K')].map(x => x.id).join(',')").Should().Be("ascii");
        fixture.Text("[...document.getElementsByClassName('\u212A')].map(x => x.id).join(',')").Should().Be("kelvin");
    }

    [Test]
    public void StandardsModeComparesExactly()
    {
        using var fixture = DomTestFixture.Create(
            "<!doctype html><html class='a A'><body class='a a'></body></html>");

        fixture.Text("document.compatMode").Should().Be("CSS1Compat");
        fixture.Text("[...document.getElementsByClassName('A a')].map(x => x.localName).join(',')")
            .Should().Be("html");
    }

    /// <summary>
    /// "If classes is the empty set, return an empty HTMLCollection" - and the split is on ASCII whitespace,
    /// so a run of it is one separator rather than a set of empty tokens.
    /// </summary>
    [Test]
    public void WhitespaceOnlyClassNamesMatchNothingAndRunsOfItSeparate()
    {
        using var fixture = DomTestFixture.Create("<!doctype html><body><p class='a  b'></p></body>");

        fixture.Number("document.getElementsByClassName('').length").Should().Be(0);
        fixture.Number("document.getElementsByClassName('   ').length").Should().Be(0);
        fixture.Number("document.getElementsByClassName('a\\t\\n\\f\\r b').length").Should().Be(1);
    }
}
