namespace Jint.Tests.Browser.Dom;

/// <summary>DOM's live lists of elements by class names.</summary>
public sealed class ClassNameCollectionTests
{
    [TestCase("", "BackCompat", "upper,lower")]
    [TestCase("<!doctype html>", "CSS1Compat", "upper")]
    [TestCase("<!DOCTYPE html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN' 'http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd'>", "CSS1Compat", "upper")]
    public void DocumentAndElementQueriesUseTheDocumentMode(string doctype, string mode, string expected)
    {
        using var fixture = DomTestFixture.Create(doctype + """
            <main id="root"><span id="upper" class="A B"></span><span id="lower" class="a b"></span></main>
            """);

        fixture.Text("document.compatMode").Should().Be(mode);
        fixture.Text("[...document.getElementsByClassName('A B')].map(x => x.id).join(',')").Should().Be(expected);
        fixture.Text("[...document.getElementById('root').getElementsByClassName('A B')].map(x => x.id).join(',')").Should().Be(expected);
    }

    [TestCase("")]
    [TestCase("<!doctype html>")]
    public void ClassMatchingPreservesNonAsciiCharactersAndTokenBoundaries(string doctype)
    {
        using var fixture = DomTestFixture.Create(doctype + "<main id='root'></main>");
        fixture.Execute("""
            var root = document.getElementById('root');
            for (const [id, classes] of [
                ['ascii', 'K'], ['kelvin', '\u212A'], ['upper', '\u00C4'], ['lower', '\u00E4'],
                ['tokens', 'a\tb\nc\fd\re'], ['nbsp', 'a\u00A0b'], ['vertical', 'a\vb'], ['punctuation', '.a#b']
            ]) {
                const child = document.createElement('span');
                child.id = id;
                child.className = classes;
                root.appendChild(child);
            }
            function ids(query) { return [...root.getElementsByClassName(query)].map(x => x.id).join(','); }
            """);

        fixture.Text("""ids('K')""").Should().Be("ascii");
        fixture.Text("""ids('\u212A')""").Should().Be("kelvin");
        fixture.Text("""ids('\u00C4')""").Should().Be("upper");
        fixture.Text("""ids('\u00E4')""").Should().Be("lower");
        fixture.Text("""ids(' \ta b\nc\fd\re a ')""").Should().Be("tokens");
        fixture.Text("""ids('a\u00A0b')""").Should().Be("nbsp");
        fixture.Text("""ids('a\vb')""").Should().Be("vertical");
        fixture.Text("""ids('.a#b')""").Should().Be("punctuation");
        fixture.Text("""ids('') + ids(' \t\n\f\r') + ids('missing')""").Should().BeEmpty();
    }

    [TestCase("document")]
    [TestCase("document.getElementById('root')")]
    public void QuirksCollectionRemainsLiveWithoutChangingClassListOrIdMatching(string context)
    {
        using var fixture = DomTestFixture.Create("<main id='root'><span id='UPPER' class='a b'></span></main>");
        fixture.Execute($$"""
            var root = document.getElementById('root');
            var first = document.getElementById('UPPER');
            var conversions = 0;
            var items = {{context}}.getElementsByClassName({ toString() { conversions++; return 'A a B'; } });
            """);
        fixture.Number("items.length").Should().Be(1);
        fixture.Bool("first.classList.contains('A')").Should().BeFalse();
        fixture.Bool("document.getElementById('upper') === null && items.namedItem('upper') === null").Should().BeTrue();
        fixture.Execute("first.classList.remove('b')");
        fixture.Number("items.length").Should().Be(0);
        fixture.Execute("first.setAttribute('class', 'A B')");
        fixture.Bool("items.item(0) === first && items.namedItem('UPPER') === first").Should().BeTrue();
        fixture.Execute("root.innerHTML += '<span id=second class=\"a b\"></span>'");
        fixture.Text("[...items].map(x => x.id).join(',')").Should().Be("UPPER,second");
        fixture.Execute("root.lastElementChild.remove()");
        fixture.Number("items.length").Should().Be(1);
        fixture.Number("conversions").Should().Be(1);
    }

    [Test]
    public void SavedElementCollectionUsesItsCurrentNodeDocumentAfterAdoption()
    {
        using var fixture = DomTestFixture.Create("<body></body>");
        fixture.Execute("""
            var root = document.createElement('div');
            root.className = 'A';
            root.innerHTML = '<span class=a></span>';
            var items = root.getElementsByClassName('A');
            var standards = document.implementation.createHTMLDocument('standards');
            """);
        fixture.Number("items.length").Should().Be(1);
        fixture.Execute("standards.adoptNode(root)");
        fixture.Text("root.ownerDocument.compatMode").Should().Be("CSS1Compat");
        fixture.Number("items.length").Should().Be(0);
        fixture.Execute("document.adoptNode(root)");
        fixture.Number("items.length").Should().Be(1);
    }

    [Test]
    public void XmlDocumentRemainsCaseSensitive()
    {
        using var fixture = DomTestFixture.Create("<body></body>");
        fixture.Execute("""
            var xml = document.implementation.createDocument(null, 'root');
            for (const name of ['a', 'A']) {
                const item = xml.createElement('item');
                item.setAttribute('class', name);
                xml.documentElement.appendChild(item);
            }
            """);
        fixture.Number("xml.getElementsByClassName('A').length").Should().Be(1);
        fixture.Number("xml.documentElement.getElementsByClassName('A').length").Should().Be(1);
    }

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
