namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#validate-and-extract">DOM §1.4's validate and extract</a> and the
/// predicates it rests on, as the members that run them answer to a page.
/// </summary>
/// <remarks>
/// The two refusals are not interchangeable and a page branches on which one it got: a name whose code
/// points are not allowed is an <c>InvalidCharacterError</c>, while a prefixed name with a null namespace, an
/// <c>xml</c> prefix outside the XML namespace or an <c>xmlns</c> name outside the XMLNS namespace is a
/// <c>NamespaceError</c>. The rows are <c>dom/nodes/Document-createElementNS.js</c>'s own, and they are all of
/// them now: the names AngleSharp's stricter XML <c>Name</c> production used to refuse before the standard's
/// algorithm could accept them are created by <see cref="Jint.Browser.Dom.DomElementFactory"/>, and the test
/// below pins the WebIDL interface each one gets. What is still refused is the doctype half, which has a test
/// of its own here and a row in <c>Jint.Browser/Dom/divergences.md</c>.
/// </remarks>
public sealed class NameValidationTests
{
    private const string Page = """
        <!doctype html>
        <html><body><div id="a">hello</div></body></html>
        """;

    /// <summary>
    /// <c>createElementNS</c>, row by row: the namespace, the qualified name, and the error name or
    /// <see langword="null"/> when the element is created.
    /// </summary>
    [TestCase("null", "'foo'", null)]
    [TestCase("null", "'1foo'", "InvalidCharacterError")]
    [TestCase("null", "'f1oo'", null)]
    [TestCase("null", "'}foo'", "InvalidCharacterError")]
    [TestCase("null", "'f}oo'", null)]
    [TestCase("null", "'\uFFFFfoo'", null)]
    [TestCase("null", "'<foo'", "InvalidCharacterError")]
    [TestCase("null", "'foo>'", "InvalidCharacterError")]
    [TestCase("null", "'fo o'", "InvalidCharacterError")]
    [TestCase("null", "'-foo'", "InvalidCharacterError")]
    [TestCase("null", "':foo'", "InvalidCharacterError")]
    [TestCase("null", "'f:oo'", "NamespaceError")]
    [TestCase("null", "'foo:'", "InvalidCharacterError")]
    [TestCase("null", "'f:o:o'", "NamespaceError")]
    [TestCase("null", "':'", "InvalidCharacterError")]
    [TestCase("null", "'xml'", null)]
    [TestCase("null", "'xmlns'", "NamespaceError")]
    [TestCase("null", "'xmlfoo'", null)]
    [TestCase("null", "'xml:foo'", "NamespaceError")]
    [TestCase("null", "'xmlns:foo'", "NamespaceError")]
    [TestCase("null", "'xmlfoo:bar'", "NamespaceError")]
    [TestCase("null", "'null:xml'", "NamespaceError")]
    [TestCase("''", "':foo'", "InvalidCharacterError")]
    [TestCase("''", "'f:oo'", "NamespaceError")]
    [TestCase("undefined", "'f::oo'", "NamespaceError")]
    [TestCase("'http://example.com/'", "'foo'", null)]
    [TestCase("'http://example.com/'", "'f:oo'", null)]
    [TestCase("'http://example.com/'", "'f::oo'", null)]
    [TestCase("'http://example.com/'", "'0:a'", null)]
    [TestCase("'http://example.com/'", "'a:0'", "InvalidCharacterError")]
    [TestCase("'http://example.com/'", "'xml:test'", "NamespaceError")]
    [TestCase("'http://example.com/'", "'xmlns:test'", "NamespaceError")]
    [TestCase("'http://example.com/'", "'test:xmlns'", null)]
    [TestCase("'http://example.com/'", "'xmlns'", "NamespaceError")]
    [TestCase("'http://example.com/'", "'XMLNS'", null)]
    [TestCase("'http://example.com/'", "'XML:foo'", null)]
    [TestCase("'http://example.com/'", "'prefix::local'", null)]
    [TestCase("'http://example.com/'", "'namespaceURI:{'", "InvalidCharacterError")]
    [TestCase("'http://www.w3.org/XML/1998/namespace'", "'xml:foo'", null)]
    [TestCase("'http://www.w3.org/2000/xmlns/'", "'xmlns'", null)]
    [TestCase("'http://www.w3.org/2000/xmlns/'", "'xmlns:foo'", null)]
    [TestCase("'http://www.w3.org/2000/xmlns/'", "'foo:bar'", "NamespaceError")]
    [TestCase("'http://www.w3.org/2000/xmlns/'", "':foo'", "InvalidCharacterError")]
    public void CreateElementNsValidatesAndExtracts(string namespaceUri, string qualifiedName, string? error)
    {
        Refusal($"document.createElementNS({namespaceUri}, {qualifiedName})").Should().Be(error);
    }

    /// <summary>The attribute half, whose local-name predicate refuses an equals sign as well.</summary>
    [TestCase("null", "'foo'", null)]
    [TestCase("null", "'f=oo'", "InvalidCharacterError")]
    [TestCase("null", "'fo o'", "InvalidCharacterError")]
    [TestCase("null", "'f:oo'", "NamespaceError")]
    [TestCase("null", "':foo'", "InvalidCharacterError")]
    [TestCase("null", "'foo:'", "InvalidCharacterError")]
    [TestCase("null", "''", "InvalidCharacterError")]
    [TestCase("null", "'xmlns'", "NamespaceError")]
    [TestCase("'http://example.com/'", "'f:oo'", null)]
    [TestCase("'http://example.com/'", "'b:'", "InvalidCharacterError")]
    [TestCase("'http://example.com/'", "'xml:foo'", "NamespaceError")]
    [TestCase("'http://example.com/'", "'a:b=c'", "InvalidCharacterError")]
    [TestCase("'http://www.w3.org/2000/xmlns/'", "'b:foo'", "NamespaceError")]
    public void SetAttributeNsValidatesAndExtracts(string namespaceUri, string qualifiedName, string? error)
    {
        Refusal($"document.getElementById('a').setAttributeNS({namespaceUri}, {qualifiedName}, 'v')")
            .Should().Be(error);
    }

    /// <summary><c>createAttributeNS</c> runs the same algorithm in the attribute context.</summary>
    [TestCase("null", "'f:oo'", "NamespaceError")]
    [TestCase("null", "'foo'", null)]
    [TestCase("null", "'f/oo'", "InvalidCharacterError")]
    [TestCase("'http://example.com/'", "'a:b'", null)]
    [TestCase("'http://example.com/'", "'xmlns:b'", "NamespaceError")]
    public void CreateAttributeNsValidatesAndExtracts(string namespaceUri, string qualifiedName, string? error)
    {
        Refusal($"document.createAttributeNS({namespaceUri}, {qualifiedName})").Should().Be(error);
    }

    /// <summary>
    /// The unprefixed members, which validate a local name and never raise a <c>NamespaceError</c>: an
    /// attribute name may hold a colon, and only the code points that would end a tag are refused.
    /// </summary>
    [TestCase("document.createElement('foo')", null)]
    [TestCase("document.createElement('1foo')", "InvalidCharacterError")]
    [TestCase("document.createElement('fo o')", "InvalidCharacterError")]
    [TestCase("document.createElement('foo>')", "InvalidCharacterError")]
    [TestCase("document.createElement('f<oo')", null)]
    [TestCase("document.createElement('f}oo')", null)]
    [TestCase("document.createElement('')", "InvalidCharacterError")]
    [TestCase("document.createAttribute('f=oo')", "InvalidCharacterError")]
    [TestCase("document.createAttribute('b:')", null)]
    [TestCase("document.createAttribute('1foo')", null)]
    [TestCase("document.getElementById('a').setAttributeNS(null, '1foo', 'v')", null)]
    [TestCase("document.getElementById('a').setAttribute('b:', 'v')", null)]
    [TestCase("document.getElementById('a').setAttribute('b=', 'v')", "InvalidCharacterError")]
    [TestCase("document.getElementById('a').setAttribute('b c', 'v')", "InvalidCharacterError")]
    [TestCase("document.getElementById('a').setAttribute('', 'v')", "InvalidCharacterError")]
    public void AnUnprefixedNameIsValidatedAsALocalName(string source, string? error)
    {
        Refusal(source).Should().Be(error);
    }

    /// <summary>
    /// The validation is WebIDL's, so it happens after the brand check and after the arity check and not
    /// before them: an illegal invocation and a missing argument are <c>TypeError</c>s whatever the name
    /// says.
    /// </summary>
    [TestCase("document.createElementNS('http://example.com/')", TestName = "a missing qualifiedName is a TypeError, not a name refusal")]
    [TestCase("document.getElementById('a').setAttributeNS('http://example.com/', 'b=')", TestName = "a missing value is a TypeError, not a name refusal")]
    [TestCase("Element.prototype.setAttribute.call({}, 'b=', 'v')", TestName = "a receiver that is not an element is a TypeError")]
    [TestCase("Element.prototype.setAttribute.call(document, 'b=', 'v')", TestName = "a receiver of the wrong interface is a TypeError")]
    [TestCase("Document.prototype.createElementNS.call(document.getElementById('a'), null, 'f:oo')", TestName = "a document member called on an element is a TypeError")]
    public void TheRefusalComesAfterTheBrandAndArityChecks(string source)
    {
        Refusal(source).Should().Be("TypeError");
    }

    /// <summary>
    /// A name that <c>AngleSharp.Text.XmlExtensions.IsXmlName</c> refuses and DOM allows creates the element
    /// the standard asks for, and it is the WebIDL interface the element-interface rule gives it rather than
    /// a generic one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The interface is what says <see cref="Jint.Browser.Dom.DomElementFactory"/> went through AngleSharp's
    /// own <c>IElementFactory</c> rather than constructing an <c>HtmlElement</c> by hand: an unknown HTML name
    /// only becomes an <c>HtmlUnknownElement</c> — and therefore only reaches
    /// <c>DomManualInterfaces.For</c>'s rule, which sorts it into <c>HTMLUnknownElement</c> or, for a valid
    /// custom element name, <c>HTMLElement</c> — because the factory decides that.
    /// </para>
    /// <para>
    /// <c>createElement</c> on an HTML document lower-cases, which is why the astral name comes back
    /// lower-cased; the surrogate pair is untouched by ASCII lower-casing, which is the point of the row.
    /// </para>
    /// </remarks>
    [TestCase("document.createElement('f<oo')", "[object HTMLUnknownElement]|f<oo|http://www.w3.org/1999/xhtml|null")]
    [TestCase("document.createElement('f}oo')", "[object HTMLUnknownElement]|f}oo|http://www.w3.org/1999/xhtml|null")]
    [TestCase("document.createElement('smallEmoji\\uD83C\\uDD96')", "[object HTMLUnknownElement]|smallemoji🆖|http://www.w3.org/1999/xhtml|null")]
    [TestCase("document.createElement('my-\\uD83C\\uDD96')", "[object HTMLElement]|my-🆖|http://www.w3.org/1999/xhtml|null")]
    [TestCase("document.createElementNS(null, 'f}oo')", "[object Element]|f}oo|null|null")]
    [TestCase("document.createElementNS(null, '\\uFFFFfoo')", "[object Element]|￿foo|null|null")]
    [TestCase("document.createElementNS('http://example.com/', '0:a')", "[object Element]|a|http://example.com/|0")]
    [TestCase("document.createElementNS('http://www.w3.org/1999/xhtml', 'f}oo')", "[object HTMLUnknownElement]|f}oo|http://www.w3.org/1999/xhtml|null")]
    // A document the page made rather than parsed resolves the same factory service, through its own
    // browsing context.
    [TestCase("document.implementation.createHTMLDocument('t').createElement('f<oo')", "[object HTMLUnknownElement]|f<oo|http://www.w3.org/1999/xhtml|null")]
    [TestCase("document.implementation.createDocument(null, '').createElementNS(null, 'f}oo')", "[object Element]|f}oo|null|null")]
    [TestCase("document.createElementNS('http://www.w3.org/2000/svg', 'f}oo')", "[object SVGElement]|f}oo|http://www.w3.org/2000/svg|null")]
    // Every MathML element is an `Element` here, valid name or not: AngleSharp has no `[DomName]` for
    // MathMLElement, so the generator cannot emit the interface and DomTypeMap answers the base one.
    [TestCase("document.createElementNS('http://www.w3.org/1998/Math/MathML', 'f}oo')", "[object Element]|f}oo|http://www.w3.org/1998/Math/MathML|null")]
    [TestCase("document.createElementNS('http://www.w3.org/1998/Math/MathML', 'mrow')", "[object Element]|mrow|http://www.w3.org/1998/Math/MathML|null")]
    public void ANameAngleSharpRefusesIsCreatedWithTheInterfaceTheStandardGivesIt(string source, string expected)
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              var e = {{source}};
              return [Object.prototype.toString.call(e), e.localName, String(e.namespaceURI), String(e.prefix)].join('|');
            })()
            """).Should().Be(expected);
    }

    /// <summary>
    /// One of those elements is an ordinary node of AngleSharp's tree: it serializes, it is found by a
    /// selector, its clone keeps all three names, and a write to <c>innerHTML</c> parses a fragment in its
    /// context.
    /// </summary>
    /// <remarks>
    /// The last of those is the only member a namespace with no AngleSharp factory has to answer for itself,
    /// because <c>Element.ParseSubtree</c> is the base class's one abstract member.
    /// </remarks>
    [Test]
    public void AnElementInANamespaceWithNoFactoryIsAnOrdinaryNode()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              var e = document.createElementNS('http://example.com/', '0:a');
              e.setAttribute('x', '1');
              document.body.appendChild(e);
              e.innerHTML = '<b>hi</b>';
              var clone = e.cloneNode(true);
              return [
                e.outerHTML,
                document.querySelector('[x="1"]') === e,
                clone.localName + '/' + clone.prefix + '/' + clone.namespaceURI,
                clone.firstChild.localName
              ].join('|');
            })()
            """).Should().Be("<0:a x=\"1\"><b>hi</b></0:a>|true|a/0/http://example.com/|b");
    }

    /// <summary>
    /// <a href="https://dom.spec.whatwg.org/#valid-doctype-name">A valid doctype name</a> is the one name
    /// predicate that is not a local name: no ASCII whitespace, no U+0000 and no U+003E, and everything else
    /// — including <c>/</c>, <c>=</c>, a colon and the empty string — allowed.
    /// </summary>
    /// <remarks>
    /// The names DOM allows and AngleSharp refuses are still refused, which is the doctype half of
    /// <see href="https://github.com/sebastienros/jint/issues/3950">#3950</see> and is recorded in
    /// <c>Jint.Browser/Dom/divergences.md</c>: <c>AngleSharp.Dom.DocumentType</c> is <c>internal sealed</c>
    /// and <c>Document.Doctype</c> is a <c>FindChild</c> over that exact class, so there is nothing to build
    /// one with. What this pins is that the refusals the standard <i>requires</i> are made, by DOM's
    /// predicate, and carry DOM's name.
    /// </remarks>
    [TestCase("document.implementation.createDocumentType('a b', '', '')", "InvalidCharacterError")]
    [TestCase("document.implementation.createDocumentType('a\\nb', '', '')", "InvalidCharacterError")]
    [TestCase("document.implementation.createDocumentType('a\\0b', '', '')", "InvalidCharacterError")]
    [TestCase("document.implementation.createDocumentType('a>b', '', '')", "InvalidCharacterError")]
    [TestCase("document.implementation.createDocumentType('a:b', '', '')", null)]
    [TestCase("document.implementation.createDocumentType('foo', '', '')", null)]
    public void CreateDocumentTypeValidatesADoctypeName(string source, string? error)
    {
        Refusal(source).Should().Be(error);
    }

    /// <summary>
    /// <c>createDocument</c> runs the internal createElementNS steps at its step 3, so it makes the same two
    /// refusals — and, since its element is no longer AngleSharp's to refuse, it has to make them itself.
    /// </summary>
    /// <remarks>
    /// The empty qualified name is step 2: it creates no element at all, so nothing is validated. Its
    /// argument is <c>[LegacyNullToEmptyString]</c>, which is why <c>null</c> is that case as well and
    /// <see langword="undefined"/> is the four-letter name.
    /// </remarks>
    [TestCase("document.implementation.createDocument(null, 'f:oo')", "NamespaceError")]
    [TestCase("document.implementation.createDocument('http://example.com/', 'a:0')", "InvalidCharacterError")]
    [TestCase("document.implementation.createDocument('http://example.com/', ':foo')", "InvalidCharacterError")]
    [TestCase("document.implementation.createDocument(null, 'xmlns')", "NamespaceError")]
    [TestCase("document.implementation.createDocument('http://example.com/', '')", null)]
    [TestCase("document.implementation.createDocument('http://example.com/', null)", null)]
    [TestCase("document.implementation.createDocument('http://example.com/', 'smallEmoji\\uD83C\\uDD96:div')", null)]
    public void CreateDocumentValidatesAndExtracts(string source, string? error)
    {
        Refusal(source).Should().Be(error);
    }

    private static string? Refusal(string source)
    {
        using var fixture = DomTestFixture.Create(Page);

        var answer = fixture.Text($$"""
            (function () {
              try { {{source}}; return ''; }
              catch (e) { return e.name || String(e); }
            })()
            """);

        return string.IsNullOrEmpty(answer) ? null : answer;
    }
}
