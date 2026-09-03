namespace Jint.Tests.Browser;

/// <summary>
/// The collection wrappers: indexed access, iteration, named access, and the WebIDL shape of both halves.
/// </summary>
public sealed class DomCollectionTests
{
    private const string Page = """
        <form id="f">
          <input id="user" name="username" value="ada">
          <input id="pass" name="password" type="password">
        </form>
        <select id="s"><option value="a">A</option><option value="b" selected>B</option></select>
        <div id="d" class="x y z" data-a="1" data-b="2" title="t"></div>
        """;

    [Test]
    public void AnHtmlCollectionSupportsIndexAndNamedAccess()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Number("document.querySelector('#f').elements.length").Should().Be(2);
        fixture.Text("document.querySelector('#f').elements[0].id").Should().Be("user");
        fixture.Text("document.querySelector('#f').elements.namedItem('username').id").Should().Be("user");
        fixture.Text("document.querySelector('#f').elements['password'].id").Should().Be("pass");
        fixture.Text("document.querySelector('#f').elements.item(1).id").Should().Be("pass");
        fixture.Evaluate("document.querySelector('#f').elements.namedItem('nope')").IsNull().Should().BeTrue();
    }

    [Test]
    public void AnHtmlCollectionsNamedPropertiesExistButDoNotEnumerate()
    {
        using var fixture = DomTestFixture.Create(Page);

        // https://webidl.spec.whatwg.org/#LegacyUnenumerableNamedProperties, which HTMLCollection and
        // HTMLFormControlsCollection both carry: the names answer `in` and `hasOwnProperty` and stay out of
        // Object.keys.
        fixture.Bool("'username' in document.querySelector('#f').elements").Should().BeTrue();
        fixture.Bool("Object.keys(document.querySelector('#f').elements).includes('username')").Should().BeFalse();
        fixture.Text("Object.keys(document.querySelector('#f').elements).join(',')").Should().Be("0,1");
    }

    [Test]
    public void ANodeListIsIndexedIterableAndArrayLikeWithoutBeingAnArray()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Number("document.querySelector('#f').childNodes.length").Should().BeGreaterThan(0);
        fixture.Bool("Array.isArray(document.querySelector('#f').childNodes)").Should().BeFalse();
        fixture.Bool("Array.from(document.querySelector('#f').childNodes).length === document.querySelector('#f').childNodes.length").Should().BeTrue();

        // The Array.prototype generics work against it through the engine's array-like lane.
        fixture.Number("Array.prototype.filter.call(document.querySelector('#f').childNodes, n => n.nodeType === 1).length").Should().Be(2);
    }

    [Test]
    public void ACollectionsIndicesAreReadOnlyEnumerableAndConfigurable()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            var d = Object.getOwnPropertyDescriptor(document.querySelector('#d').classList, '0');
            [d.writable, d.enumerable, d.configurable].join(',');
            """).Should().Be("false,true,true");

        // Writing an index is ignored in sloppy mode and a TypeError in strict mode — the WebIDL
        // platform-object shape ArrayLikeObject derives.
        fixture.Text("""
            'use strict';
            try { document.querySelector('#d').classList[0] = 'q'; 'no throw'; } catch (e) { e.constructor.name; }
            """).Should().Be("TypeError");
    }

    [Test]
    public void NamedNodeMapProjectsAttributesByNameAndByIndex()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Number("document.querySelector('#d').attributes.length").Should().Be(5);
        fixture.Text("document.querySelector('#d').attributes.getNamedItem('title').value").Should().Be("t");
        fixture.Text("document.querySelector('#d').attributes['title'].value").Should().Be("t");
        fixture.Bool("'title' in document.querySelector('#d').attributes").Should().BeTrue();

        // Unenumerable named properties again, so the keys are the indices.
        fixture.Text("Object.keys(document.querySelector('#d').attributes).join(',')").Should().Be("0,1,2,3,4");
    }

    [Test]
    public void ACssStyleDeclarationIsIndexedByPropertyPosition()
    {
        using var fixture = DomTestFixture.Create("<div id='d' style='color: red; font-weight: bold'></div>");

        fixture.Number("document.querySelector('#d').style.length").Should().Be(2);
        fixture.Text("document.querySelector('#d').style[0]").Should().Be("color");
        fixture.Text("document.querySelector('#d').style.item(1)").Should().Be("font-weight");
        fixture.Text("[...document.querySelector('#d').style].join(',')").Should().Be("color,font-weight");
    }

    [Test]
    public void SymbolIteratorIsOnTheInterfacePrototypeAndIsTheArrayIterator()
    {
        using var fixture = DomTestFixture.Create(Page);

        // https://webidl.spec.whatwg.org/#js-iterable — an interface that supports indexed properties has
        // @@iterator on its prototype, and its value is the realm's own %Array.prototype.values%.
        fixture.Bool("NodeList.prototype[Symbol.iterator] === Array.prototype[Symbol.iterator]").Should().BeTrue();
        fixture.Bool("HTMLCollection.prototype[Symbol.iterator] === Array.prototype[Symbol.iterator]").Should().BeTrue();
        fixture.Bool("DOMTokenList.prototype[Symbol.iterator] === Array.prototype[Symbol.iterator]").Should().BeTrue();
        fixture.Bool("CSSStyleDeclaration.prototype[Symbol.iterator] === Array.prototype[Symbol.iterator]").Should().BeTrue();
        fixture.Bool("NamedNodeMap.prototype[Symbol.iterator] === Array.prototype[Symbol.iterator]").Should().BeTrue();

        // And on the prototype only: a collection instance carries no own symbol, which is what a browser
        // answers and what the instance-level workaround this replaced could not.
        fixture.Bool("document.querySelectorAll('input').hasOwnProperty(Symbol.iterator)").Should().BeFalse();
        fixture.Number("Object.getOwnPropertySymbols(document.querySelector('#f').childNodes).length").Should().Be(0);

        // https://webidl.spec.whatwg.org/#es-iterator — { writable: true, enumerable: false, configurable: true }.
        fixture.Bool("Object.getOwnPropertyDescriptor(NodeList.prototype, Symbol.iterator).writable").Should().BeTrue();
        fixture.Bool("Object.getOwnPropertyDescriptor(NodeList.prototype, Symbol.iterator).enumerable").Should().BeFalse();
        fixture.Bool("Object.getOwnPropertyDescriptor(NodeList.prototype, Symbol.iterator).configurable").Should().BeTrue();

        // An interface that only *inherits* the indexed getter inherits the iterator with it, which is also
        // what a browser answers.
        fixture.Bool("HTMLOptionsCollection.prototype.hasOwnProperty(Symbol.iterator)").Should().BeFalse();
        fixture.Bool("HTMLFormControlsCollection.prototype.hasOwnProperty(Symbol.iterator)").Should().BeFalse();
        fixture.Bool("document.querySelector('#s').options[Symbol.iterator] === Array.prototype[Symbol.iterator]").Should().BeTrue();

        // The prototypes are still shaped: a symbol key never disturbs the shared string-keyed layout.
        fixture.Engine.Advanced.HasSharedShape(fixture.Evaluate("NodeList.prototype").AsObject()).Should().BeTrue();
    }

    [Test]
    public void IterationStillReachesEveryCollectionKind()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Number("[...document.querySelectorAll('input')].length").Should().Be(2);
        fixture.Text("[...document.querySelector('#d').classList].join(',')").Should().Be("x,y,z");
        fixture.Text("[...document.querySelector('#s').options].map(o => o.value).join(',')").Should().Be("a,b");
        fixture.Number("[...document.querySelector('#f').elements].length").Should().Be(2);

        // for..of and destructuring take the same lane.
        fixture.Text("(() => { let n = ''; for (const o of document.querySelector('#s').options) { n += o.value; } return n; })()").Should().Be("ab");
        fixture.Text("(() => { const [first] = document.querySelector('#s').options; return first.value; })()").Should().Be("a");
    }

    [Test]
    public void ASelectsOptionsAreAnHtmlCollectionOfOptions()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Number("document.querySelector('#s').options.length").Should().Be(2);
        fixture.Text("document.querySelector('#s').options[1].value").Should().Be("b");
        fixture.Bool("document.querySelector('#s').options[1] instanceof HTMLOptionElement").Should().BeTrue();
        fixture.Bool("document.querySelector('#s').options instanceof HTMLOptionsCollection").Should().BeTrue();
        fixture.Bool("document.querySelector('#s').options instanceof HTMLCollection").Should().BeTrue();
    }
}
