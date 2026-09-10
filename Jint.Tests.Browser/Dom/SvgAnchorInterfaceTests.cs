namespace Jint.Tests.Browser.Dom;

/// <summary>
/// SVG 2 §16.2's <c>SVGAElement</c>, declared by local name over the bare AngleSharp element an SVG
/// <c>&lt;a&gt;</c> is: https://svgwg.org/svg2-draft/linking.html#InterfaceSVGAElement.
/// </summary>
public sealed class SvgAnchorInterfaceTests
{
    private const string Svg = "http://www.w3.org/2000/svg";

    private static DomTestFixture Anchor(string markup = "")
    {
        var fixture = DomTestFixture.Create("<body></body>");
        fixture.Execute($$"""
            var svgA = document.createElementNS('{{Svg}}', 'a');
            {{markup}}
            """);
        return fixture;
    }

    [Test]
    public void AnSvgAnchorTakesTheSvgAElementInterface()
    {
        using var fixture = Anchor();

        fixture.Text("Object.prototype.toString.call(svgA)").Should().Be("[object SVGAElement]");
        fixture.Bool("svgA instanceof SVGAElement").Should().BeTrue();
        fixture.Bool("svgA instanceof SVGElement && svgA instanceof Element && svgA instanceof Node").Should().BeTrue();
        fixture.Text("SVGAElement.name").Should().Be("SVGAElement");
        fixture.Bool("Object.getPrototypeOf(SVGAElement.prototype) === SVGElement.prototype").Should().BeTrue();
        fixture.Bool("svgA.constructor === SVGAElement").Should().BeTrue();
    }

    /// <summary>The interface is chosen by local name, and SVG matches one case-sensitively.</summary>
    [TestCase("a", "[object SVGAElement]")]
    [TestCase("A", "[object SVGElement]")]
    [TestCase("g", "[object SVGElement]")]
    [TestCase("circle", "[object SVGCircleElement]")]
    public void OnlyALowerCaseSvgAnchorTakesIt(string localName, string expected)
    {
        using var fixture = DomTestFixture.Create("<body></body>");
        fixture.Execute($"var el = document.createElementNS('{Svg}', '{localName}');");
        fixture.Text("Object.prototype.toString.call(el)").Should().Be(expected);
    }

    /// <summary>An HTML or MathML anchor is unaffected: no standard gives either this interface.</summary>
    [TestCase("http://www.w3.org/1999/xhtml", "[object HTMLAnchorElement]")]
    [TestCase("http://www.w3.org/1998/Math/MathML", "[object Element]")]
    [TestCase("http://example.com/", "[object Element]")]
    public void AnAnchorInAnotherNamespaceIsUnchanged(string ns, string expected)
    {
        using var fixture = DomTestFixture.Create("<body></body>");
        fixture.Execute($"var el = document.createElementNS('{ns}', 'a');");
        fixture.Text("Object.prototype.toString.call(el)").Should().Be(expected);
    }

    [Test]
    public void RelReflectsTheContentAttribute()
    {
        using var fixture = Anchor("svgA.setAttribute('rel', ' next  prefetch ');");

        fixture.Text("svgA.rel").Should().Be(" next  prefetch ");
        fixture.Execute("svgA.rel = 'noreferrer'");
        fixture.Text("svgA.getAttribute('rel')").Should().Be("noreferrer");
        fixture.Execute("svgA.removeAttribute('rel')");
        fixture.Text("svgA.rel").Should().BeEmpty();
    }

    /// <summary>
    /// What <c>dom/lists/DOMTokenList-coverage-for-attributes.html</c> asks of the member:
    /// <c>assert_class_string(svgA.relList, "DOMTokenList")</c>.
    /// </summary>
    [Test]
    public void RelListIsADomTokenListAndTheSameObjectEveryRead()
    {
        using var fixture = Anchor();

        fixture.Text("Object.prototype.toString.call(svgA.relList)").Should().Be("[object DOMTokenList]");
        fixture.Bool("svgA.relList instanceof DOMTokenList").Should().BeTrue();
        fixture.Bool("svgA.relList === svgA.relList").Should().BeTrue();
        fixture.Bool("svgA.relList !== document.createElementNS(svgA.namespaceURI, 'a').relList").Should().BeTrue();
    }

    /// <summary>
    /// DOM §7.1 over the element's own <c>rel</c> attribute: the ordered set parser drops duplicates and runs
    /// of ASCII whitespace, while <c>value</c> and the stringifier answer the attribute verbatim.
    /// </summary>
    [Test]
    public void RelListIsTheOrderedSetOfTheRelAttribute()
    {
        using var fixture = Anchor("svgA.setAttribute('rel', ' next\\tnext  prefetch ');");

        fixture.Number("svgA.relList.length").Should().Be(2);
        fixture.Text("[...svgA.relList].join(',')").Should().Be("next,prefetch");
        fixture.Text("svgA.relList[0]").Should().Be("next");
        fixture.Bool("svgA.relList.item(2) === null").Should().BeTrue();
        fixture.Bool("svgA.relList.contains('prefetch') && !svgA.relList.contains('PREFETCH')").Should().BeTrue();
        fixture.Text("svgA.relList.value").Should().Be(" next\tnext  prefetch ");
        fixture.Text("String(svgA.relList)").Should().Be(" next\tnext  prefetch ");
    }

    [Test]
    public void RelListWritesTheAttributeBack()
    {
        using var fixture = Anchor();

        fixture.Execute("svgA.relList.add('next', 'prefetch')");
        fixture.Text("svgA.getAttribute('rel')").Should().Be("next prefetch");
        fixture.Text("svgA.rel").Should().Be("next prefetch");

        fixture.Bool("svgA.relList.toggle('next')").Should().BeFalse();
        fixture.Text("svgA.getAttribute('rel')").Should().Be("prefetch");
        fixture.Bool("svgA.relList.toggle('next', false)").Should().BeFalse();
        fixture.Text("svgA.getAttribute('rel')").Should().Be("prefetch");

        fixture.Bool("svgA.relList.replace('prefetch', 'noreferrer')").Should().BeTrue();
        fixture.Text("svgA.getAttribute('rel')").Should().Be("noreferrer");

        fixture.Execute("svgA.relList.remove('noreferrer')");
        fixture.Text("svgA.getAttribute('rel')").Should().BeEmpty();
    }

    /// <summary>The list is live against a write that never went through it.</summary>
    [Test]
    public void RelListReadsTheAttributeOnEveryAccess()
    {
        using var fixture = Anchor("var list = svgA.relList;");

        fixture.Number("list.length").Should().Be(0);
        fixture.Execute("svgA.setAttribute('rel', 'next')");
        fixture.Number("list.length").Should().Be(1);
        fixture.Execute("svgA.rel = 'next prefetch'");
        fixture.Text("[...list].join(',')").Should().Be("next,prefetch");
        fixture.Execute("svgA.removeAttribute('rel')");
        fixture.Number("list.length").Should().Be(0);
    }

    /// <summary>WebIDL's <c>[PutForwards=value]</c>, which every projected token list carries.</summary>
    [Test]
    public void AssigningToRelListWritesTheAttributeVerbatim()
    {
        using var fixture = Anchor();

        fixture.Execute("svgA.relList = ' next  next '");
        fixture.Text("svgA.getAttribute('rel')").Should().Be(" next  next ");
        fixture.Number("svgA.relList.length").Should().Be(1);
    }

    /// <summary>DOM §7.1's validation steps, which are the token list's and not this element's.</summary>
    [Test]
    public void RelListValidatesItsTokens()
    {
        using var fixture = Anchor();

        fixture.Text("(() => { try { svgA.relList.add(''); } catch (e) { return e.name; } })()")
            .Should().Be("SyntaxError");
        fixture.Text("(() => { try { svgA.relList.add('a b'); } catch (e) { return e.name; } })()")
            .Should().Be("InvalidCharacterError");
        fixture.Bool("svgA.hasAttribute('rel')").Should().BeFalse();
    }

    /// <summary>
    /// The member is the interface's, so a receiver of another interface is a <c>TypeError</c> rather than a
    /// read of that element's own <c>rel</c> attribute.
    /// </summary>
    [Test]
    public void RelListRefusesAForeignReceiver()
    {
        using var fixture = Anchor("var div = document.createElement('div'); div.setAttribute('rel', 'next');");

        fixture.Text("""
            (() => {
                const get = Object.getOwnPropertyDescriptor(SVGAElement.prototype, 'relList').get;
                try { get.call(div); } catch (e) { return e.constructor.name; }
                return 'no throw';
            })()
            """).Should().Be("TypeError");
    }
}
