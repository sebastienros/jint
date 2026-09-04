namespace Jint.Tests.Browser;

/// <summary>
/// The two members of DOM's document-creation surface that AngleSharp does not have:
/// <a href="https://dom.spec.whatwg.org/#dom-document-createcdatasection"><c>createCDATASection</c></a> and
/// <a href="https://dom.spec.whatwg.org/#dom-domimplementation-createdocument"><c>createDocument</c></a>.
/// </summary>
/// <remarks>
/// They are one subject rather than two, because it takes both of them for <c>dom/common.js</c> — the
/// fixture builder the whole of the web-platform-tests <c>dom/ranges/</c> suite and half of
/// <c>dom/traversal/</c> load — to reach its first <c>test()</c>: it builds a CDATA section on a
/// <c>new Document()</c> and an XML document through <c>document.implementation</c>, both at file scope.
/// </remarks>
public sealed class DocumentCreationTests
{
    private const string Page = "<!doctype html><html><body><div id='host'></div></body></html>";

    [Test]
    public void CreateCdataSectionOnAnHtmlDocumentIsANotSupportedError()
    {
        using var fixture = DomTestFixture.Create(Page);

        // DOM §4.5 step 1, and the whole reason the member has to exist rather than merely be correct: a
        // caller guarding with try/catch catches this, where a TypeError for an absent member arrives before
        // the guard can mean anything.
        fixture.Text(
            """
            (() => { try { document.createCDATASection('x') } catch (e) { return e.name + '|' + (e instanceof DOMException) } return 'no throw' })()
            """)
            .Should().Be("NotSupportedError|true");
    }

    [Test]
    public void CreateCdataSectionOnAnXmlDocumentMakesACdataSection()
    {
        using var fixture = DomTestFixture.Create(Page);

        // `new Document()` is DOM's own constructor and makes an XML document, which is exactly the shape
        // dom/common.js reaches for.
        fixture.Text(
            """
            var xml = new Document();
            var section = xml.createCDATASection('12]]34');
            [section.nodeType, section.nodeName, section.data, section.length, section.ownerDocument === xml].join('|');
            """)
            .Should().Be("4|#cdata-section|12]]34|6|true");
    }

    [Test]
    public void CreateCdataSectionRefusesDataThatWouldCloseTheSection()
    {
        using var fixture = DomTestFixture.Create(Page);

        // DOM §4.5 step 2. It is checked before the node is made, which is what makes the order the
        // standard's rather than AngleSharp's Data setter's.
        fixture.Text(
            """
            (() => { try { new Document().createCDATASection('a]]>b') } catch (e) { return e.name } return 'no throw' })()
            """)
            .Should().Be("InvalidCharacterError");

        fixture.Text("(() => { try { document.createCDATASection() } catch (e) { return e.constructor.name } return 'no throw' })()")
            .Should().Be("TypeError");
    }

    [Test]
    public void ACdataSectionCanBeAdoptedIntoThePage()
    {
        using var fixture = DomTestFixture.Create(Page);

        // What dom/common.js does with it: two sections built on a document of their own, appended to a
        // paragraph of the page's.
        fixture.Text(
            """
            var xml = new Document();
            var host = document.getElementById('host');
            host.appendChild(xml.createCDATASection('1234'));
            host.appendChild(xml.createCDATASection('5678'));
            [host.childNodes.length, host.firstChild.data, host.lastChild.data, host.firstChild.ownerDocument === document].join('|');
            """)
            .Should().Be("2|1234|5678|true");
    }

    [Test]
    public void CreateDocumentMakesAnEmptyXmlDocument()
    {
        using var fixture = DomTestFixture.Create(Page);

        // DOM §4.5.1 steps 2 and 3: an empty qualified name means no document element at all, which is the
        // shape dom/common.js asks for.
        fixture.Text(
            """
            var doc = document.implementation.createDocument(null, '', null);
            [doc.documentElement, doc.childNodes.length, doc.doctype].join('|');
            """)
            .Should().Be("|0|");
    }

    [Test]
    public void CreateDocumentTakesANamespaceAQualifiedNameAndADoctype()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            var doctype = document.implementation.createDocumentType('qorflesnorf', 'abcde', 'x"\'y');
            var doc = document.implementation.createDocument('http://example.com/ns', 'pre:root', doctype);
            [
              doc.childNodes.length,
              doc.doctype === doctype,
              doc.doctype.name,
              doc.documentElement.tagName,
              doc.documentElement.namespaceURI,
              doc.documentElement.prefix,
              doc.documentElement.localName,
              doc.documentElement.ownerDocument === doc,
            ].join('|');
            """)
            // The doctype is appended before the element, so a document built with both has them in the order
            // a parse would.
            .Should().Be("2|true|qorflesnorf|pre:root|http://example.com/ns|pre|root|true");
    }

    [Test]
    public void CreateDocumentAppliesWebIdlsOwnArgumentRules()
    {
        using var fixture = DomTestFixture.Create(Page);

        // `[LegacyNullToEmptyString] DOMString qualifiedName`: null is the empty string, and undefined is
        // still the five letters of "undefined" — the one place the two do not behave alike.
        fixture.Text("document.implementation.createDocument(null, null).childNodes.length.toString()").Should().Be("0");
        fixture.Text("document.implementation.createDocument(null, undefined).documentElement.tagName").Should().Be("undefined");

        // Two arguments are required.
        fixture.Text(
            "(() => { try { document.implementation.createDocument(null) } catch (e) { return e.constructor.name } return 'no throw' })()")
            .Should().Be("TypeError");

        // And DOM's validate-and-extract refuses a prefixed name with no namespace.
        fixture.Text(
            "(() => { try { document.implementation.createDocument(null, 'a:b') } catch (e) { return e.name } return 'no throw' })()")
            .Should().Be("NamespaceError");
    }
}
