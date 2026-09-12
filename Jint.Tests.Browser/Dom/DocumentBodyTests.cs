namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <c>document.body</c>: the getter is about the document's <i>html element</i>, and the setter is a
/// different algorithm that does not consult it.
/// <para>
/// https://html.spec.whatwg.org/multipage/dom.html#the-body-element
/// </para>
/// </summary>
/// <remarks>
/// "The html element of a document is its document element, if it is an <c>html</c> element, and null
/// otherwise", and the body element is the first <c>body</c> or <c>frameset</c> child of <em>that</em>. So a
/// document whose root is an XHTML <c>div</c> has no body element however many <c>body</c> children the root
/// has — the standard's own example is a body inserted beneath an SVG document element, which the getter
/// still answers null for. AngleSharp 1.8.0 walks <c>DocumentElement.ChildNodes</c> without asking what the
/// document element is, so the binding gates the getter and <c>Dom/divergences.md</c> records it.
/// </remarks>
public sealed class DocumentBodyTests
{
    private const string Page = """
        <!doctype html>
        <html><body><div id="a">hello</div></body></html>
        """;

    private const string Html = "http://www.w3.org/1999/xhtml";

    /// <summary>
    /// An XML document whose document element is an XHTML <c>div</c> has no html element, so it has no body
    /// element either — however many <c>body</c> children that root carries, and by whichever of the two
    /// spellings the document was made.
    /// </summary>
    [Test]
    public async Task ANonHtmlDocumentElementHasNoBodyElement()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Page);

        (await page.EvaluateAsync<string>($$"""
            (function () {
              const HTML = '{{Html}}';

              // A document element of the given name, with a body element nested straight under it.
              function rooted(namespaceUri, name) {
                const made = new Document();
                const root = made.createElementNS(namespaceUri, name);
                made.appendChild(root);
                const nested = made.createElementNS(HTML, 'body');
                nested.id = 'nested';
                root.appendChild(nested);
                return made;
              }

              const constructed = rooted(HTML, 'div');

              // The same shape through DOM's other spelling for an XML document.
              const created = document.implementation.createDocument(HTML, 'div', null);
              const createdBody = created.createElementNS(HTML, 'body');
              createdBody.id = 'nested';
              created.documentElement.appendChild(createdBody);

              // An `html` from some other namespace is not an html element either.
              const foreign = rooted('urn:example:x', 'html');

              return [
                constructed.documentElement.localName,
                constructed.body === null,
                // The node is still there and still reachable; it is simply not the body element.
                constructed.documentElement.firstElementChild.id,
                created.documentElement.localName,
                created.body === null,
                created.documentElement.firstElementChild.id,
                foreign.documentElement.localName,
                foreign.documentElement.namespaceURI,
                foreign.body === null,
              ].join('|');
            })()
            """)).Should().Be("div|true|nested|div|true|nested|html|urn:example:x|true");

        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// The gate is the only thing added: past it the search is still "the first <c>body</c> or
    /// <c>frameset</c> child of the html element", which is where an ordinary page's body comes from.
    /// </summary>
    [Test]
    public async Task AnHtmlDocumentElementStillAnswersItsFirstBodyOrFramesetChild()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Page);

        (await page.EvaluateAsync<string>($$"""
            (function () {
              const HTML = '{{Html}}';

              function rooted(childName) {
                const made = new Document();
                const root = made.createElementNS(HTML, 'html');
                made.appendChild(root);
                const child = made.createElementNS(HTML, childName);
                child.id = childName;
                root.appendChild(child);
                return made;
              }

              const withBody = rooted('body');
              const withFrameset = rooted('frameset');

              // A body that is only a DESCENDANT of the html element is not a child of it.
              const deep = new Document();
              const deepRoot = deep.createElementNS(HTML, 'html');
              deep.appendChild(deepRoot);
              const between = deep.createElementNS(HTML, 'div');
              deepRoot.appendChild(between);
              between.appendChild(deep.createElementNS(HTML, 'body'));

              const parsed = new DOMParser().parseFromString(
                '<html><body id="parsed"><p></p></body></html>', 'text/html');

              return [
                withBody.body.id,
                withFrameset.body.id,
                deep.body === null,
                parsed.body.id,
                document.body.localName,
                document.body === document.querySelector('body'),
              ].join('|');
            })()
            """)).Should().Be("body|frameset|true|parsed|body|true");

        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// The setter is a separate algorithm and keeps AngleSharp's, so it may append a body beneath a document
    /// element the getter will not answer for — which the standard allows and this pins, so that gating the
    /// getter is not read as gating both halves.
    /// </summary>
    [Test]
    public async Task TheSetterStillAppendsUnderANonHtmlDocumentElementTheGetterDeclines()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Page);

        (await page.EvaluateAsync<string>($$"""
            (function () {
              const HTML = '{{Html}}';

              const made = new Document();
              const root = made.createElementNS(HTML, 'div');
              made.appendChild(root);

              const fresh = made.createElementNS(HTML, 'body');
              fresh.id = 'appended';
              made.body = fresh;

              // Its two refusals are unchanged: a value that is neither a body nor a frameset, and a
              // document with no document element to append to.
              let wrongKind;
              const other = new Document();
              const otherRoot = other.createElementNS(HTML, 'html');
              other.appendChild(otherRoot);
              try { other.body = other.createElementNS(HTML, 'div'); } catch (e) { wrongKind = e.name; }

              let noRoot;
              const empty = new Document();
              try { empty.body = empty.createElementNS(HTML, 'body'); } catch (e) { noRoot = e.name; }

              return [
                // The append happened...
                root.firstElementChild.id,
                fresh.parentNode === root,
                // ...and the getter still says the document has no body element.
                made.body === null,
                wrongKind,
                noRoot,
              ].join('|');
            })()
            """)).Should().Be("appended|true|true|HierarchyRequestError|HierarchyRequestError");

        page.Errors.Should().BeEmpty();
    }
}
