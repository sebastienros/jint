namespace Jint.Tests.Browser;

/// <summary>
/// DOM §4.4's <a href="https://dom.spec.whatwg.org/#concept-node-equals">node equality</a> and the two
/// members whose IDL default decides how much of a tree they copy.
/// </summary>
/// <remarks>
/// One fixture, because the three are one subject: what a page gets back from <c>cloneNode()</c> and
/// <c>importNode()</c> is what it then compares with <c>isEqualNode</c>, and every one of them was
/// AngleSharp's answer rather than the standard's.
/// </remarks>
public sealed class DomNodeEqualityTests
{
    private const string Page = "<!doctype html><html><body><div id='host'><span>a</span><span>b</span></div></body></html>";

    [Test]
    public void ADoctypeIsComparedOnItsPublicAndSystemIdentifiers()
    {
        using var fixture = DomTestFixture.Create(Page);

        // AngleSharp's Node.Equals compares a doctype's name and stops, so a doctype differing only in its
        // public or system identifier was equal to one that does not.
        fixture.Text(
            """
            var make = (n, p, s) => document.implementation.createDocumentType(n, p, s);
            var subject = make('qualifiedName', 'publicId', 'systemId');
            [
              subject.isEqualNode(make('qualifiedName', 'publicId', 'systemId')),
              subject.isEqualNode(make('qualifiedName2', 'publicId', 'systemId')),
              subject.isEqualNode(make('qualifiedName', 'publicId2', 'systemId')),
              subject.isEqualNode(make('qualifiedName', 'publicId', 'systemId3')),
            ].join('|');
            """)
            .Should().Be("true|false|false|false");
    }

    [Test]
    public void CharacterDataIsComparedOnItsData()
    {
        using var fixture = DomTestFixture.Create(Page);

        // A text node, a comment and a processing instruction each carry data the standard compares and
        // AngleSharp does not, so two comments saying different things were equal.
        fixture.Text(
            """
            var xml = new Document();
            [
              document.createTextNode('data').isEqualNode(document.createTextNode('data')),
              document.createTextNode('data').isEqualNode(document.createTextNode('data2')),
              document.createComment('data').isEqualNode(document.createComment('data2')),
              xml.createProcessingInstruction('target', 'data').isEqualNode(xml.createProcessingInstruction('target', 'data')),
              xml.createProcessingInstruction('target', 'data').isEqualNode(xml.createProcessingInstruction('target', 'data2')),
            ].join('|');
            """)
            .Should().Be("true|false|false|true|false");
    }

    [Test]
    public void AnAttributeIsComparedOnItsNamespaceLocalNameAndValueAndNotOnItsPrefix()
    {
        using var fixture = DomTestFixture.Create(Page);

        // The one place the standard's element rule and its attribute rule deliberately disagree: an
        // element's own prefix counts, an attribute's does not.
        fixture.Text(
            """
            var withAttribute = (ns, name, value) => {
              var el = document.createElement('element');
              el.setAttributeNS(ns, name, value);
              return el;
            };
            var subject = withAttribute('namespace', 'prefix:localName', 'value');
            [
              subject.isEqualNode(withAttribute('namespace', 'prefix2:localName', 'value')),
              subject.isEqualNode(withAttribute('namespace2', 'prefix:localName', 'value')),
              subject.isEqualNode(withAttribute('namespace', 'prefix:localName2', 'value')),
              subject.isEqualNode(withAttribute('namespace', 'prefix:localName', 'value2')),
              document.createElementNS('ns', 'p:name').isEqualNode(document.createElementNS('ns', 'p2:name')),
            ].join('|');
            """)
            .Should().Be("true|false|false|false|false");
    }

    [Test]
    public void EqualityIsAboutTheTreeAndNotAboutTheDocumentTheNodeCameFrom()
    {
        using var fixture = DomTestFixture.Create(Page);

        // AngleSharp's Node.Equals opens by comparing the two nodes' base URLs, which the algorithm does not
        // mention: an element built by this document and the same element built by another one were unequal
        // for no reason a page can see.
        fixture.Text(
            """
            var other = document.implementation.createHTMLDocument('t');
            var here = document.createElement('p');
            here.textContent = 'x';
            var there = other.createElement('p');
            there.textContent = 'x';
            [here.isEqualNode(there), here.isEqualNode(null), here.isEqualNode(document.createElement('p'))].join('|');
            """)
            .Should().Be("true|false|false");
    }

    [Test]
    public void CreateHtmlDocumentOnlyCreatesATitleWhenOneIsGiven()
    {
        using var fixture = DomTestFixture.Create(Page);

        // DOM §4.5.1 step 6 is "if title is given, create a title element": AngleSharp's required parameter
        // creates one only for a non-empty string, so createHTMLDocument('') made the same document
        // createHTMLDocument() does and the two spellings were indistinguishable from outside.
        fixture.Text(
            """
            var implied = document.implementation.createHTMLDocument();
            var empty = document.implementation.createHTMLDocument('');
            var named = document.implementation.createHTMLDocument('t');
            [
              implied.head.childNodes.length,
              empty.head.childNodes.length,
              empty.head.firstChild.localName,
              empty.head.firstChild.childNodes.length,
              empty.title,
              named.head.firstChild.firstChild.data,
            ].join('|');
            """)
            .Should().Be("0|1|title|1||t");
    }

    [Test]
    public void TwoDefaultHtmlDocumentsBuiltDifferentWaysAreEqual()
    {
        using var fixture = DomTestFixture.Create(Page);

        // dom/nodes/Node-isEqualNode.html's "documents should not be compared based on properties", which
        // needed both halves: the algorithm, and a createHTMLDocument that adds no title nobody asked for.
        fixture.Text(
            """
            var doctype = document.implementation.createDocumentType('html', '', '');
            var built = document.implementation.createDocument('http://www.w3.org/1999/xhtml', 'html', doctype);
            built.documentElement.appendChild(built.createElement('head'));
            built.documentElement.appendChild(built.createElement('body'));
            String(built.isEqualNode(document.implementation.createHTMLDocument()));
            """)
            .Should().Be("true");
    }

    [Test]
    public void CloneNodeAndImportNodeAreShallowByDefault()
    {
        using var fixture = DomTestFixture.Create(Page);

        // Both IDL declarations are `optional boolean deep = false`; AngleSharp's own default is true, so
        // every argument-less call copied a whole subtree.
        fixture.Text(
            """
            var host = document.getElementById('host');
            [
              host.cloneNode().childNodes.length,
              host.cloneNode(undefined).childNodes.length,
              host.cloneNode(true).childNodes.length,
              document.importNode(host).childNodes.length,
              document.importNode(host, undefined).childNodes.length,
              document.importNode(host, true).childNodes.length,
            ].join('|');
            """)
            .Should().Be("0|0|2|0|0|2");
    }

    [Test]
    public void CloningAProcessingInstructionCarriesItsData()
    {
        using var fixture = DomTestFixture.Create(Page);

        // DOM's clone steps: "if node is a ProcessingInstruction, set copy's target to node's target and
        // copy's data to node's data". AngleSharp's clone carries the target and drops the data.
        fixture.Text(
            """
            var pi = new Document().createProcessingInstruction('target', 'data');
            var copy = pi.cloneNode();
            [copy.target, copy.data, pi.isEqualNode(copy)].join('|');
            """)
            .Should().Be("target|data|true");
    }

    [Test]
    public void NodeNameAndTagNameAnswerTheOneName()
    {
        using var fixture = DomTestFixture.Create(Page);

        // DOM defines nodeName for an element as tagName's HTML-uppercased qualified name, so an element
        // adopted into an XML document has to stop being uppercased in both members at once.
        fixture.Text(
            """
            var xml = new Document();
            var el = document.createElement('div');
            var adopted = xml.adoptNode(document.createElement('div'));
            [el.tagName, el.nodeName, adopted.tagName, adopted.nodeName].join('|');
            """)
            .Should().Be("DIV|DIV|div|div");
    }

    [Test]
    public void AttributesOwnPropertyNamesLeaveOutNamesAnHtmlParseCouldNotHaveMade()
    {
        using var fixture = DomTestFixture.Create(Page);

        // DOM §4.9.1: for an element in the HTML namespace whose node document is an HTML document, a
        // qualified name that is not its own ASCII lowercase is not a supported property name. The attribute
        // is still there, and still reachable by index and by getNamedItemNS.
        fixture.Text(
            """
            var el = document.createElement('div');
            el.setAttributeNS('foo', 'A:B', '');
            el.setAttributeNS('qux', 'g:h', '');
            el.setAttributeNS('', 'I', '');
            el.setAttributeNS('', 'j', '');
            [
              Object.getOwnPropertyNames(el.attributes).join(','),
              el.attributes.length,
              typeof el.attributes['A:B'],
              el.attributes.getNamedItemNS('foo', 'B').value === '',
            ].join('|');
            """)
            .Should().Be("0,1,2,3,g:h,j|4|undefined|true");
    }

    [Test]
    public void AttributesOwnPropertyNamesKeepEveryNameOutsideAnHtmlDocument()
    {
        using var fixture = DomTestFixture.Create(Page);

        // The filter is a question about the element and its node document, not about the name: the same
        // attributes on an element in an XML document keep every qualified name.
        fixture.Text(
            """
            var xml = new Document();
            var el = xml.createElementNS('urn:x', 'x');
            el.setAttributeNS('foo', 'A:B', '');
            el.setAttributeNS('', 'j', '');
            Object.getOwnPropertyNames(el.attributes).join(',');
            """)
            .Should().Be("0,1,A:B,j");
    }
}
