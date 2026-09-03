using AngleSharp.Dom;
using Jint.Browser.Dom;

namespace Jint.Tests.Browser;

/// <summary>
/// What a page sees when a DOM operation refuses: a JavaScript <c>DOMException</c> with the name and the
/// legacy code the standard prescribes, never AngleSharp's CLR exception.
/// </summary>
/// <remarks>
/// Before <see href="https://github.com/sebastienros/jint/issues/3670">#3670</see> every one of these walked
/// through the script's own <c>try</c>/<c>catch</c> and out of the page loop as an
/// <c>AngleSharp.Dom.DomException</c>, so a page could neither read <c>e.name</c> nor catch it at all.
/// </remarks>
public sealed class DomExceptionTests
{
    private const string Page = """
        <!doctype html>
        <html><body><div id="a">hello <b>world</b></div><p id="b">other</p></body></html>
        """;

    /// <summary>
    /// One refusal per row: the script that provokes it, and the
    /// <a href="https://webidl.spec.whatwg.org/#idl-DOMException-error-names">error name</a> plus legacy code
    /// it has to arrive as.
    /// </summary>
    /// <remarks>
    /// <c>querySelector</c> is here although <c>DomSelectorMembers</c> already answered it by hand, because
    /// the wrapping must not change what a member that refuses on its own says.
    /// </remarks>
    [TestCase("document.createElement('1bad')", "InvalidCharacterError", 5, TestName = "createElement with an invalid name is an InvalidCharacterError")]
    [TestCase("document.createAttribute('1bad')", "InvalidCharacterError", 5, TestName = "createAttribute with an invalid name is an InvalidCharacterError")]
    [TestCase("document.getElementById('a').setAttribute('=bad', 'v')", "InvalidCharacterError", 5, TestName = "setAttribute with an invalid name is an InvalidCharacterError")]
    [TestCase("var d = document.getElementById('a'); d.appendChild(d)", "HierarchyRequestError", 3, TestName = "appendChild of an ancestor is a HierarchyRequestError")]
    [TestCase("document.getElementById('a').appendChild(document.body)", "HierarchyRequestError", 3, TestName = "appendChild of the body is a HierarchyRequestError")]
    [TestCase("document.getElementById('a').removeChild(document.getElementById('b'))", "NotFoundError", 8, TestName = "removeChild of an unrelated node is a NotFoundError")]
    [TestCase("document.getElementById('a').insertBefore(document.createElement('i'), document.getElementById('b'))", "NotFoundError", 8, TestName = "insertBefore with an unrelated reference is a NotFoundError")]
    [TestCase("document.createRange().setStart(document.getElementById('a'), 99)", "IndexSizeError", 1, TestName = "setStart past the end is an IndexSizeError")]
    [TestCase("document.getElementById('a').firstChild.splitText(99)", "IndexSizeError", 1, TestName = "splitText past the end is an IndexSizeError")]
    [TestCase("document.getElementById('a').firstChild.substringData(99, 1)", "IndexSizeError", 1, TestName = "substringData past the end is an IndexSizeError")]
    [TestCase("document.querySelector('!!')", "SyntaxError", 12, TestName = "an unparseable selector is a SyntaxError")]
    [TestCase("document.importNode(document, true)", "NotSupportedError", 9, TestName = "importing a document is a NotSupportedError")]
    [TestCase("document.createRange().selectNode(document)", "InvalidNodeTypeError", 24, TestName = "selecting the document node is an InvalidNodeTypeError")]
    [TestCase("document.getElementById('a').attributes.removeNamedItem('nope')", "NotFoundError", 8, TestName = "removeNamedItem of an absent attribute is a NotFoundError")]
    [TestCase("document.createElementNS('http://x/', 'xmlns:b')", "NamespaceError", 14, TestName = "an xmlns prefix is a NamespaceError")]
    [TestCase("document.documentElement.insertAdjacentHTML('beforebegin', '<i>x</i>')", "NoModificationAllowedError", 7, TestName = "insertAdjacentHTML with no parent element is a NoModificationAllowedError")]
    public void ARefusedOperationIsADomException(string source, string name, int code)
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              try { {{source}}; return 'no throw'; }
              catch (e) { return [e.name, e.code].join('/'); }
            })()
            """).Should().Be(name + "/" + code);
    }

    /// <summary>
    /// The whole <see cref="DomError"/> table, so a pin bump that adds, renames or renumbers a value fails
    /// here rather than in one page's <c>catch</c> block.
    /// </summary>
    /// <remarks>
    /// <c>Validation</c> reads <c>InvalidAccessError</c> because AngleSharp numbers it 15, the value of
    /// <c>InvalidAccess</c>, where WebIDL's legacy code for <c>ValidationError</c> is 16. That is the
    /// divergence, asserted rather than described.
    /// </remarks>
    [Test]
    public void EveryDomErrorNamesTheStandardsErrorName()
    {
        var table = string.Join(
            "\n",
            Enum.GetNames<DomError>()
                .Select(field => Enum.Parse<DomError>(field))
                .Select(error => (int) error + " " + Enum.GetName(error) + " -> " + DomFailures.NameOf(new DomException(error))));

        table.Should().Be(
            """
            1 IndexSizeError -> IndexSizeError
            2 DomStringSize -> DOMStringSizeError
            3 HierarchyRequest -> HierarchyRequestError
            4 WrongDocument -> WrongDocumentError
            5 InvalidCharacter -> InvalidCharacterError
            6 NoDataAllowed -> NoDataAllowedError
            7 NoModificationAllowed -> NoModificationAllowedError
            8 NotFound -> NotFoundError
            9 NotSupported -> NotSupportedError
            10 InUse -> InUseAttributeError
            11 InvalidState -> InvalidStateError
            12 Syntax -> SyntaxError
            13 InvalidModification -> InvalidModificationError
            14 Namespace -> NamespaceError
            15 InvalidAccess -> InvalidAccessError
            15 InvalidAccess -> InvalidAccessError
            17 TypeMismatch -> TypeMismatchError
            18 Security -> SecurityError
            19 Network -> NetworkError
            20 Abort -> AbortError
            21 UrlMismatch -> URLMismatchError
            22 QuotaExceeded -> QuotaExceededError
            23 Timeout -> TimeoutError
            24 InvalidNodeType -> InvalidNodeTypeError
            25 DataClone -> DataCloneError
            """);
    }

    /// <summary>
    /// A <c>DomException</c> carrying no <see cref="DomError"/> is DOM's general refusal, because the string
    /// AngleSharp puts in its <c>Name</c> in that case is a sentence rather than an error name.
    /// </summary>
    [Test]
    public void ADomExceptionWithNoCodeIsAnInvalidStateError()
        => DomFailures.NameOf(new DomException("The element has no parent.")).Should().Be("InvalidStateError");

    /// <summary>
    /// <c>QuotaExceededError</c> is an interface of its own rather than a name a <c>DOMException</c> wears,
    /// so a refusal for want of room has to arrive as one.
    /// </summary>
    /// <remarks>
    /// Nothing in AngleSharp raises <c>DomError.QuotaExceeded</c> today, so the interface rather than the
    /// projection is what is asserted: <c>DomFailures</c> routes the name to
    /// <c>QuotaExceededErrorConstructor</c> and this is what makes that reachable at all.
    /// </remarks>
    [Test]
    public void TheQuotaNameIsItsOwnInterface()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              var e = new DOMException('x', 'QuotaExceededError');
              return [e.code, QuotaExceededError.prototype instanceof DOMException].join('/');
            })()
            """).Should().Be("22/true");
    }

    /// <summary>
    /// The exception is the page's own <c>DOMException</c> — its prototype, its interface object, its brand —
    /// and an <c>Error</c> as WebIDL says a <c>DOMException</c> is.
    /// </summary>
    [Test]
    public void TheExceptionIsThePagesOwnDomException()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              var d = document.getElementById('a');
              try { d.appendChild(d); return 'no throw'; }
              catch (e) {
                return [
                  e instanceof DOMException,
                  e instanceof Error,
                  e.constructor === DOMException,
                  Object.prototype.toString.call(e),
                  typeof e.stack
                ].join('|');
              }
            })()
            """).Should().Be("true|true|true|[object DOMException]|string");
    }

    /// <summary>
    /// The message names the member that refused, in the wording <c>DomBindings</c> already uses for an
    /// illegal invocation, and carries AngleSharp's own sentence after it.
    /// </summary>
    [Test]
    public void TheMessageNamesTheMemberThatRefused()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              var d = document.getElementById('a');
              try { d.appendChild(d); return 'no throw'; }
              catch (e) { return e.message; }
            })()
            """).Should().Be("Failed to execute 'Node.appendChild': The operation would yield an incorrect node tree.");

        fixture.Text("""
            (function () {
              try { document.createElement('1bad'); return 'no throw'; }
              catch (e) { return e.message; }
            })()
            """).Should().Be("Failed to execute 'Document.createElement': Invalid character detected.");
    }

    /// <summary>
    /// A refusal is catchable, so a page can go on. The proof a browser-shaped one is worth having: the
    /// operation after the <c>catch</c> runs.
    /// </summary>
    [Test]
    public void APageGoesOnAfterCatchingOne()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            var seen = 'none';
            var d = document.getElementById('a');
            try { d.appendChild(d); } catch (e) { seen = e.name; }
            d.appendChild(document.createElement('i'));
            seen + '/' + d.lastChild.tagName;
            """).Should().Be("HierarchyRequestError/I");
    }

    /// <summary>
    /// What the wrapping must <i>not</i> change: an error the member body raised itself. A bad WebIDL
    /// enumeration value and a wrong receiver are both <c>TypeError</c>s, and they stay that.
    /// </summary>
    [Test]
    public void AnErrorTheBodyRaisedItselfIsUntouched()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              try { document.getElementById('a').insertAdjacentHTML('nope', '<i>x</i>'); return 'no throw'; }
              catch (e) { return e.name + '/' + (e instanceof DOMException); }
            })()
            """).Should().Be("TypeError/false");

        fixture.Text("""
            (function () {
              try { Element.prototype.getAttribute.call({}, 'id'); return 'no throw'; }
              catch (e) { return e.name + ': ' + e.message; }
            })()
            """).Should().Be("TypeError: Failed to execute 'Element.getAttribute': Illegal invocation");
    }
}
