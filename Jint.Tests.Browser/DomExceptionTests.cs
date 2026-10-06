using NativeDomException = Jint.HtmlParser.DomException;
using Jint.HtmlParser;
using Jint.Runtime.Interop;
using Jint.WebApi;
using Jint.Browser.Dom;

namespace Jint.Tests.Browser;

/// <summary>
/// What a page sees when a DOM operation refuses: a JavaScript <c>DOMException</c> with the name and the
/// legacy code the standard prescribes, never the former DOM integration's CLR exception.
/// </summary>
/// <remarks>
/// Before <see href="https://github.com/sebastienros/jint/issues/3670">#3670</see> every one of these walked
/// through the script's own <c>try</c>/<c>catch</c> and out of the page loop as an
/// <c>Jint.HtmlParser.DomException</c>, so a page could neither read <c>e.name</c> nor catch it at all.
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
    [TestCase("document.createElement('1bad')", "InvalidCharacterError", 5, TestName = "createElement with an invalid name is an InvalidCharacterError")]
    [TestCase("document.createAttribute('bad name')", "InvalidCharacterError", 5, TestName = "createAttribute with an invalid name is an InvalidCharacterError")]
    [TestCase("document.getElementById('a').setAttribute('=bad', 'v')", "InvalidCharacterError", 5, TestName = "setAttribute with an invalid name is an InvalidCharacterError")]
    [TestCase("var d = document.getElementById('a'); d.appendChild(d)", "HierarchyRequestError", 3, TestName = "appendChild of an ancestor is a HierarchyRequestError")]
    [TestCase("document.getElementById('a').appendChild(document.body)", "HierarchyRequestError", 3, TestName = "appendChild of the body is a HierarchyRequestError")]
    [TestCase("document.getElementById('a').removeChild(document.getElementById('b'))", "NotFoundError", 8, TestName = "removeChild of an unrelated node is a NotFoundError")]
    [TestCase("document.getElementById('a').insertBefore(document.createElement('i'), document.getElementById('b'))", "NotFoundError", 8, TestName = "insertBefore with an unrelated reference is a NotFoundError")]
    [TestCase("document.createRange().setStart(document.getElementById('a'), 99)", "IndexSizeError", 1, TestName = "setStart past the end is an IndexSizeError")]
    [TestCase("document.getElementById('a').firstChild.splitText(99)", "IndexSizeError", 1, TestName = "splitText past the end is an IndexSizeError")]
    [TestCase("document.getElementById('a').firstChild.substringData(99, 1)", "IndexSizeError", 1, TestName = "substringData past the end is an IndexSizeError")]
    [TestCase("document.querySelector('!!')", "SyntaxError", 12, TestName = "an unparseable selector is a SyntaxError")]
    [TestCase("document.querySelectorAll('[')", "SyntaxError", 12)]
    [TestCase("document.body.querySelector('[')", "SyntaxError", 12)]
    [TestCase("document.body.querySelectorAll('[')", "SyntaxError", 12)]
    [TestCase("var f = document.createDocumentFragment(); f.appendChild(document.createElement('div')); f.querySelector('[')", "SyntaxError", 12)]
    [TestCase("var f = document.createDocumentFragment(); f.appendChild(document.createElement('div')); f.querySelectorAll('[')", "SyntaxError", 12)]
    [TestCase("document.body.matches('[')", "SyntaxError", 12)]
    [TestCase("document.body.closest('[')", "SyntaxError", 12)]
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

    /// <summary>WebIDL's historical name/code table through actual native failure translation.</summary>
    /// <remarks>Native failures carry explicit names, including ValidationError (16); no legacy enum adapter is involved.</remarks>
    [TestCase("IndexSizeError", 1)]
    [TestCase("DOMStringSizeError", 2)]
    [TestCase("HierarchyRequestError", 3)]
    [TestCase("WrongDocumentError", 4)]
    [TestCase("InvalidCharacterError", 5)]
    [TestCase("NoDataAllowedError", 6)]
    [TestCase("NoModificationAllowedError", 7)]
    [TestCase("NotFoundError", 8)]
    [TestCase("NotSupportedError", 9)]
    [TestCase("InUseAttributeError", 10)]
    [TestCase("InvalidStateError", 11)]
    [TestCase("SyntaxError", 12)]
    [TestCase("InvalidModificationError", 13)]
    [TestCase("NamespaceError", 14)]
    [TestCase("InvalidAccessError", 15)]
    [TestCase("ValidationError", 16)]
    [TestCase("TypeMismatchError", 17)]
    [TestCase("SecurityError", 18)]
    [TestCase("NetworkError", 19)]
    [TestCase("AbortError", 20)]
    [TestCase("URLMismatchError", 21)]
    [TestCase("QuotaExceededError", 22)]
    [TestCase("TimeoutError", 23)]
    [TestCase("InvalidNodeTypeError", 24)]
    [TestCase("DataCloneError", 25)]
    [TestCase("OperationError", 0)]
    public void NativeFailureTranslationPreservesNameCodeReceiverRealmAndContinuation(string name, int code)
    {
        using var fixture = DomTestFixture.Create(Page);
        var engine = fixture.Engine;
        var second = engine._host.CreateRealm();
        WebApiRegistration.InstallInRealm(engine, second);
        DomBindings.Install(engine, second);
        var realm = DomRealm.Of(engine, second);
        var document = Document.CreateHtml();
        realm.AssociateDocument(document);
        engine.SetValue("receiverDocument", realm.WrapNode(document));
        engine.SetValue("ReceiverDOMException", second.Intrinsics.DomException);
        engine.SetValue("ReceiverError", second.Intrinsics.Error);
        engine.SetValue("ReceiverQuota", second.Intrinsics.QuotaExceededError);
        engine.SetValue("expectedName", name);
        engine.SetValue("expectedCode", code);
        var guarded = DomFailures.Guard("Native.failure", (_, _) => throw new NativeDomException(name, "native refusal detail."));
        engine.SetValue("nativeFailure", new ClrFunction(engine, "nativeFailure", (receiver, arguments) => guarded(receiver, arguments)));
        fixture.Bool("""
            (() => {
                let caught=false, continued=false;
                try { nativeFailure.call(receiverDocument); }
                catch (error) {
                    caught = error.name===expectedName && error.code===expectedCode &&
                        error.message==="Failed to execute 'Native.failure': native refusal detail." &&
                        error instanceof ReceiverDOMException && error instanceof ReceiverError &&
                        !(error instanceof DOMException) &&
                        (expectedName==='QuotaExceededError'
                            ? error instanceof ReceiverQuota && error.constructor===ReceiverQuota
                            : !(error instanceof ReceiverQuota));
                }
                continued=true;
                return caught && continued;
            })()
            """).Should().BeTrue();
    }

    /// <summary>
    /// <c>QuotaExceededError</c> is an interface of its own rather than a name a <c>DOMException</c> wears,
    /// so a refusal for want of room has to arrive as one.
    /// </summary>
    /// <remarks>
    /// Nothing in the former DOM integration raises <c>DomError.QuotaExceeded</c> today, so the interface rather than the
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
    /// illegal invocation, and carries the actual native refusal detail. Legacy the former DOM integration sentences are not copied into the
    /// native parser; the precise prefix and native detail remain asserted.
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
            """).Should().Be("Failed to execute 'Node.appendChild': The requested tree structure is invalid.");

        fixture.Text("""
            (function () {
              try { document.createElement('1bad'); return 'no throw'; }
              catch (e) { return e.message; }
            })()
            """).Should().Be("Failed to execute 'Document.createElement': the name '1bad' is not a valid element name.");

        fixture.Text("""
            (function () {
              try { document.getElementById('a').removeChild(document.getElementById('b')); return 'no throw'; }
              catch (e) { return e.message; }
            })()
            """).Should().Be("Failed to execute 'Node.removeChild': The node is not a child of this parent.");
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
