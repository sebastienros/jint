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

    [Test]
    public void CreateDocumentTakesItsContentTypeFromTheNamespace()
    {
        using var fixture = DomTestFixture.Create(Page);

        // DOM §4.5.1 step 7. AngleSharp answers text/xml for every document its XML parser builds, and its
        // ContentType setter is protected, so the value rides on the browsing context the document was
        // parsed into — see DomContentType.
        fixture.Text(
            """
            [
              document.implementation.createDocument(null, null, null).contentType,
              document.implementation.createDocument('http://www.w3.org/1999/xhtml', 'html', null).contentType,
              document.implementation.createDocument('http://www.w3.org/2000/svg', 'svg', null).contentType,
              document.implementation.createDocument('http://example.com/ns', 'root', null).contentType,
              new Document().contentType,
            ].join('|');
            """)
            .Should().Be("application/xml|application/xhtml+xml|image/svg+xml|application/xml|application/xml");

        // And a clone keeps it, because DOM's clone steps make the clone's content type its source's.
        fixture.Text(
            "document.implementation.createDocument('http://www.w3.org/2000/svg', 'svg', null).cloneNode().contentType")
            .Should().Be("image/svg+xml");
    }

    [Test]
    public void ADocumentWithNoBrowsingContextHasNoLocation()
    {
        using var fixture = DomTestFixture.Create(Page);

        // HTML's Document.location getter: the Location object while the document is fully active, and
        // null otherwise. None of these three is showing anywhere.
        fixture.Text(
            """
            [
              document.implementation.createDocument(null, null, null).location,
              document.implementation.createHTMLDocument('t').location,
              new Document().location,
            ].map(String).join('|');
            """)
            .Should().Be("null|null|null");

        // The document that is showing keeps its Location, and WebIDL's [PutForwards=href] setter is a
        // TypeError on the ones that have none rather than a navigation nobody can see.
        fixture.Bool("document.location !== null").Should().BeTrue();
        fixture.Text(
            "(() => { const d = new Document(); try { d.location = '/x' } catch (e) { return e.constructor.name } return 'no throw' })()")
            .Should().Be("TypeError");
    }

    [Test]
    public void CharacterSetIsTheEncodingStandardsNameAndCarriesItsTwoAliases()
    {
        using var fixture = DomTestFixture.Create(Page);

        // DOM §4.5 declares characterSet, charset and inputEncoding, the last two "legacy alias of
        // .characterSet". The name is the Encoding Standard's own spelling, which is UTF-8 and not the
        // ASCII-lowercased label AngleSharp answers from .NET's Encoding.WebName.
        fixture.Text(
            """
            const doc = document.implementation.createDocument(null, null, null);
            [document.characterSet, doc.characterSet, doc.charset, doc.inputEncoding].join('|');
            """)
            .Should().Be("UTF-8|UTF-8|UTF-8|UTF-8");
    }

    [Test]
    public void CreateElementOnADocumentThatIsNotAnHtmlOneKeepsTheNameAndItsNamespace()
    {
        using var fixture = DomTestFixture.Create(Page);

        // DOM §4.5 createElement steps 2 and 4: the name is ASCII-lowercased only for an HTML document,
        // and the namespace is HTML's only for an HTML document or one whose content type is
        // application/xhtml+xml. AngleSharp's one-argument overload does both unconditionally.
        fixture.Text(
            """
            const xml = document.implementation.createDocument(null, null, null);
            const el = xml.createElement('DIV');
            [el.localName, String(el.namespaceURI), document.createElement('DIV').localName].join('|');
            """)
            .Should().Be("DIV|null|div");
    }

    [Test]
    public void TagNameIsUppercasedOnlyWhileTheElementIsInAnHtmlDocument()
    {
        using var fixture = DomTestFixture.Create(Page);

        // DOM §4.9's HTML-uppercased qualified name asks two questions, and AngleSharp asks only the
        // first: the namespace, and the node document. importNode is what moves the second one — DOM's
        // import steps clone "with document set to this", which AngleSharp's Import does not do.
        fixture.Text(
            """
            const xml = document.implementation.createDocument(
              'http://www.w3.org/1999/xhtml', 'foo:div', null);
            const before = xml.documentElement.tagName;
            const imported = document.importNode(xml.documentElement, true);
            [before, imported.tagName, imported.ownerDocument === document].join('|');
            """)
            .Should().Be("foo:div|FOO:DIV|true");

        // And the other direction: an element of the page adopted into an XML document loses the case.
        fixture.Text(
            """
            const adopting = document.implementation.createDocument(null, null, null);
            const moved = document.createElement('div');
            const was = moved.tagName;
            adopting.appendChild(moved);
            [was, moved.tagName].join('|');
            """)
            .Should().Be("DIV|div");
    }
}
