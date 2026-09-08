namespace Jint.Tests.Browser.Dom;

/// <summary>
/// The four element interfaces HTML declares that AngleSharp models with a plain <c>HTMLElement</c>:
/// <a href="https://html.spec.whatwg.org/multipage/grouping-content.html#the-dl-element">§4.4.9's
/// <c>HTMLDListElement</c></a> and
/// <a href="https://html.spec.whatwg.org/multipage/obsolete.html#htmldirectoryelement">§16.3.3's</a>
/// <c>HTMLDirectoryElement</c>, <c>HTMLFontElement</c> and <c>HTMLFrameElement</c>.
/// </summary>
/// <remarks>
/// <para>
/// They are <c>DomManualInterfaces</c> rows, the way <c>HTMLFrameSetElement</c> already was, and for the same
/// reason: <c>HtmlDefinitionListElement</c>, <c>HtmlDirectoryElement</c>, <c>HtmlFontElement</c> and
/// <c>HtmlFrameElement</c> are internal sealed classes whose only public interface is <c>IHtmlElement</c>, so
/// <c>DomTypeMap</c> — which keys on the CLR type — cannot tell any of them from a <c>&lt;div&gt;</c>. The
/// wrapper decides the shape by <b>local name</b> over the same AngleSharp element; there is no second DOM.
/// </para>
/// <para>
/// What the two halves of each case are worth stating separately: the interface has to exist <i>and</i> the
/// members have to be on it and nowhere else. Putting <c>compact</c> on <c>HTMLElement</c> would satisfy
/// every reflection assertion in <c>html/dom/reflection-obsolete.html</c> and give the member to every
/// element in the document, which is why <see cref="TheMembersAreOnTheirOwnInterfaceAndNotOnHTMLElement"/>
/// is here.
/// </para>
/// </remarks>
public sealed class ObsoleteElementInterfaceTests
{
    private const string Page = """
        <!doctype html>
        <html><body><dl id="list"></dl></body></html>
        """;

    /// <summary>
    /// WebIDL's interface object: a name on the window, a <c>function</c>, and the prototype of the elements
    /// that take it.
    /// </summary>
    [TestCase("dl", "HTMLDListElement")]
    [TestCase("dir", "HTMLDirectoryElement")]
    [TestCase("font", "HTMLFontElement")]
    [TestCase("frame", "HTMLFrameElement")]
    [TestCase("frameset", "HTMLFrameSetElement")]
    public void TheInterfaceObjectIsOnTheWindowAndNamesTheElementSPrototype(string localName, string interfaceName)
    {
        Answer($$"""
            [
              typeof {{interfaceName}},
              {{interfaceName}}.name,
              String(Object.getPrototypeOf({{interfaceName}}.prototype) === HTMLElement.prototype),
              String({{interfaceName}}.prototype.constructor === {{interfaceName}}),
              String(Object.getPrototypeOf(document.createElement('{{localName}}')) === {{interfaceName}}.prototype)
            ].join('|')
            """).Should().Be($"function|{interfaceName}|true|true|true");
    }

    /// <summary>
    /// The brand a page reads: <c>instanceof</c> down the whole chain, and
    /// <a href="https://webidl.spec.whatwg.org/#dfn-class-string">WebIDL's class string</a> through
    /// <c>Object.prototype.toString</c>.
    /// </summary>
    [TestCase("dl", "HTMLDListElement")]
    [TestCase("dir", "HTMLDirectoryElement")]
    [TestCase("font", "HTMLFontElement")]
    [TestCase("frame", "HTMLFrameElement")]
    public void AnElementIsBrandedByItsLocalName(string localName, string interfaceName)
    {
        Answer($$"""
            (function () {
              var el = document.createElement('{{localName}}');
              return [
                String(el instanceof {{interfaceName}}),
                String(el instanceof HTMLElement),
                String(el instanceof Element),
                String(el instanceof Node),
                String(el instanceof EventTarget),
                Object.prototype.toString.call(el)
              ].join('|');
            })()
            """).Should().Be($"true|true|true|true|true|[object {interfaceName}]");
    }

    /// <summary>
    /// No other element may take one of these interfaces, which is the half a member declared on
    /// <c>HTMLElement</c> would fail.
    /// </summary>
    [Test]
    public void NoOtherElementTakesOneOfTheseInterfaces()
    {
        Answer("""
            ['div', 'ol', 'ul', 'iframe', 'dt', 'dd'].map(function (name) {
              var el = document.createElement(name);
              return String(
                el instanceof HTMLDListElement || el instanceof HTMLDirectoryElement
                  || el instanceof HTMLFontElement || el instanceof HTMLFrameElement);
            }).join('|')
            """).Should().Be("false|false|false|false|false|false");
    }

    /// <summary>
    /// <a href="https://html.spec.whatwg.org/multipage/dom.html#element-interface">HTML's element interface
    /// rule is about a name <i>in the HTML namespace</i></a>, so SVG's own <c>&lt;font&gt;</c> and a
    /// namespaceless <c>&lt;dl&gt;</c> are <c>SVGElement</c> and <c>Element</c> and take none of this.
    /// </summary>
    [Test]
    public void ANameInAnotherNamespaceTakesNoneOfThem()
    {
        Answer("""
            (function () {
              var svg = document.createElementNS('http://www.w3.org/2000/svg', 'font');
              var none = document.createElementNS(null, 'dl');
              var html = document.createElementNS('http://www.w3.org/1999/xhtml', 'font');
              return [
                String(svg instanceof HTMLFontElement),
                String(none instanceof HTMLDListElement),
                String(html instanceof HTMLFontElement)
              ].join('|');
            })()
            """).Should().Be("false|false|true");
    }

    /// <summary>
    /// The members are prototype accessors of their own interface, and of no other — an element that is not
    /// one of the four sees nothing.
    /// </summary>
    [Test]
    public void TheMembersAreOnTheirOwnInterfaceAndNotOnHTMLElement()
    {
        Answer("""
            (function () {
              function accessor(proto, name) {
                var d = Object.getOwnPropertyDescriptor(proto, name);
                return String(!!d && typeof d.get === 'function' && typeof d.set === 'function'
                  && d.enumerable === true && d.configurable === true);
              }
              return [
                accessor(HTMLDListElement.prototype, 'compact'),
                accessor(HTMLDirectoryElement.prototype, 'compact'),
                accessor(HTMLFontElement.prototype, 'color'),
                accessor(HTMLFrameElement.prototype, 'noResize'),
                String('compact' in document.createElement('div')),
                String('color' in document.createElement('div')),
                String('noResize' in document.createElement('div')),
                String('compact' in document.createElement('dl'))
              ].join('|');
            })()
            """).Should().Be("true|true|true|true|false|false|false|true");
    }

    /// <summary>
    /// HTML §2.6.1's boolean reflection: the IDL attribute is the content attribute's presence, and writing
    /// it adds or removes the attribute with the empty string as its value.
    /// </summary>
    [TestCase("dl", "compact")]
    [TestCase("dir", "compact")]
    [TestCase("frame", "noResize")]
    public void ABooleanMemberIsTheAttributeSPresence(string localName, string member)
    {
        Answer($$"""
            (function () {
              var el = document.createElement('{{localName}}');
              var absent = el.{{member}};
              el.setAttribute('{{member}}', '');
              var present = el.{{member}};
              el.{{member}} = false;
              var removed = el.hasAttribute('{{member}}');
              el.{{member}} = true;
              return [typeof absent, String(absent), String(present), String(removed),
                      String(el.hasAttribute('{{member}}')), el.getAttribute('{{member}}')].join('|');
            })()
            """).Should().Be("boolean|false|true|false|true|");
    }

    /// <summary>
    /// A plain reflected <c>DOMString</c>: absent reads as the empty string, and the value round-trips
    /// through the content attribute case-preservingly.
    /// </summary>
    [TestCase("font", "face", "face")]
    [TestCase("font", "size", "size")]
    [TestCase("frame", "name", "name")]
    [TestCase("frame", "scrolling", "scrolling")]
    [TestCase("frame", "frameBorder", "frameborder")]
    public void AStringMemberReflectsItsContentAttribute(string localName, string member, string attribute)
    {
        Answer($$"""
            (function () {
              var el = document.createElement('{{localName}}');
              var absent = el.{{member}};
              el.{{member}} = 'AbC';
              var written = el.getAttribute('{{attribute}}');
              el.setAttribute('{{attribute}}', 'xYz');
              return [typeof absent, JSON.stringify(absent), written, el.{{member}}].join('|');
            })()
            """).Should().Be("string|\"\"|AbC|xYz");
    }

    /// <summary>
    /// WebIDL's <c>[LegacyNullToEmptyString]</c>, which HTML puts on <c>font.color</c> and on the frame's two
    /// margins and on nothing else in this family: <c>null</c> converts to the empty string, where an
    /// unannotated <c>DOMString</c> converts it to <c>"null"</c>.
    /// </summary>
    [TestCase("font", "color", "")]
    [TestCase("frame", "marginHeight", "")]
    [TestCase("frame", "marginWidth", "")]
    [TestCase("font", "face", "null")]
    [TestCase("frame", "name", "null")]
    public void ANullWriteIsTheEmptyStringOnlyWhereHTMLSaysSo(string localName, string member, string expected)
    {
        Answer($$"""
            (function () {
              var el = document.createElement('{{localName}}');
              el.{{member}} = null;
              return el.{{member}};
            })()
            """).Should().Be(expected);
    }

    /// <summary>
    /// HTML §2.6.1's URL reflection, which <c>frame.src</c> and <c>frame.longDesc</c> take: the getter
    /// answers the absolute URL the content attribute resolves to, and the setter writes the value as given.
    /// </summary>
    [TestCase("src", "src")]
    [TestCase("longDesc", "longdesc")]
    public void AUrlMemberAnswersTheResolvedUrl(string member, string attribute)
    {
        Answer($$"""
            (function () {
              var el = document.createElement('frame');
              el.{{member}} = 'https://example.com/a/b?q#f';
              return [el.getAttribute('{{attribute}}'), el.{{member}}].join('|');
            })()
            """).Should().Be("https://example.com/a/b?q#f|https://example.com/a/b?q#f");
    }

    /// <summary>
    /// <c>HTMLFrameElement.contentDocument</c> and <c>contentWindow</c> are deliberately absent: the page
    /// runtime fetches no <c>&lt;frame&gt;</c>, so both could only ever answer <c>null</c>, and AngleSharp's
    /// own <c>ContentDocument</c> is on an internal class no public interface reaches. This pins the gap so
    /// that supplying it later is a visible change rather than a silent one.
    /// </summary>
    [Test]
    public void TheFrameSContentMembersAreNotDeclared()
    {
        Answer("""
            ['contentDocument', 'contentWindow'].map(function (name) {
              return String(name in document.createElement('frame'));
            }).join('|')
            """).Should().Be("false|false");
    }

    /// <summary>
    /// A clone is the same interface, because the interface is decided by the local name the clone carries
    /// and not by anything held on the wrapper — which is what <c>dom/nodes/Node-cloneNode.html</c> asks.
    /// </summary>
    [Test]
    public void ACloneKeepsTheInterfaceAndTheAttribute()
    {
        Answer("""
            (function () {
              var el = document.getElementById('list');
              el.compact = true;
              var copy = el.cloneNode();
              return [String(copy instanceof HTMLDListElement), String(copy.compact), String(copy !== el)].join('|');
            })()
            """).Should().Be("true|true|true");
    }

    /// <summary>
    /// A parsed element takes the interface too, and an element that arrived through the wrapper cache keeps
    /// the one wrapper it has.
    /// </summary>
    [Test]
    public void AParsedElementTakesTheInterfaceAndKeepsOneWrapper()
    {
        Answer("""
            (function () {
              var el = document.getElementById('list');
              return [
                String(el instanceof HTMLDListElement),
                String(el === document.querySelector('dl')),
                Object.prototype.toString.call(el)
              ].join('|');
            })()
            """).Should().Be("true|true|[object HTMLDListElement]");
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
