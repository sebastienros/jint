namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#validate-and-extract">DOM §1.4's validate and extract</a> and the
/// predicates it rests on, as the members that run them answer to a page.
/// </summary>
/// <remarks>
/// The two refusals are not interchangeable and a page branches on which one it got: a name whose code
/// points are not allowed is an <c>InvalidCharacterError</c>, while a prefixed name with a null namespace, an
/// <c>xml</c> prefix outside the XML namespace or an <c>xmlns</c> name outside the XMLNS namespace is a
/// <c>NamespaceError</c>. The rows are <c>dom/nodes/Document-createElementNS.js</c>'s own, minus the ones
/// AngleSharp's stricter name check refuses before the standard's algorithm can accept them — those are the
/// last test in this file, and a row of <c>Jint.Browser/Dom/AGENTS.md</c>'s divergence table.
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
    [TestCase("document.createElement('')", "InvalidCharacterError")]
    [TestCase("document.createAttribute('f=oo')", "InvalidCharacterError")]
    [TestCase("document.createAttribute('b:')", null)]
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
    /// The other half of the algorithm, which is AngleSharp's and stays a divergence: it holds a name to
    /// XML's <c>Name</c> production, which DOM deliberately stopped doing, so the names below are refused
    /// where a browser creates the element or attribute.
    /// </summary>
    /// <remarks>
    /// The refusal is now DOM's <c>InvalidCharacterError</c> rather than whatever AngleSharp chose, which is
    /// as far as this side of the binding can go: nothing here can build an element whose local name
    /// AngleSharp will not accept, because the element factories are internal to that assembly. Recorded in
    /// <c>Jint.Browser/Dom/AGENTS.md</c>'s divergence table and in
    /// <see href="https://github.com/sebastienros/jint/issues/3772">#3772</see>.
    /// </remarks>
    [TestCase("document.createElement('f<oo')", TestName = "createElement with a code point XML forbids")]
    [TestCase("document.createElement('f}oo')", TestName = "createElement with a brace")]
    [TestCase("document.createAttribute('1foo')", TestName = "createAttribute with a leading digit")]
    [TestCase("document.createElementNS(null, 'f}oo')", TestName = "createElementNS with a brace")]
    [TestCase("document.createElementNS(null, '\\uFFFFfoo')", TestName = "createElementNS with a non-character")]
    [TestCase("document.createElementNS('http://example.com/', '0:a')", TestName = "createElementNS with a digit prefix")]
    [TestCase("document.getElementById('a').setAttributeNS(null, '1foo', 'v')", TestName = "setAttributeNS with a leading digit")]
    public void ANameAngleSharpRefusesStaysARefusal(string source)
    {
        Refusal(source).Should().Be("InvalidCharacterError");
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
