namespace Jint.Tests.Browser.Dom;

/// <summary>
/// The <c>Document</c> members that reflect an attribute of <b>another</b> element: HTML §16.3.3's five
/// obsolete colours, which reflect the <i>body element</i>'s, and §3.2.6.4's <c>dir</c>, which reflects the
/// <i>html element</i>'s.
/// <para>
/// https://html.spec.whatwg.org/multipage/obsolete.html#dom-document-fgcolor
/// </para>
/// </summary>
/// <remarks>
/// Both names are HTML's own defined elements rather than tree positions, and both are gated on the same
/// sentence <c>document.body</c> is: "the html element of a document is its document element, if it is an
/// <c>html</c> element, and null otherwise". "If there is no such element, then the attribute must return the
/// empty string and do nothing on setting." AngleSharp's <c>IDocument.Body</c> asks neither question, so
/// resolving the target through it read a body the document does not have.
/// </remarks>
public sealed class DocumentColourMemberTests
{
    private const string Page = """
        <!doctype html>
        <html dir="rtl"><body text="black" link="blue" vlink="purple" alink="red" bgcolor="white">
        <div id="a">hello</div></body></html>
        """;

    private const string Html = "http://www.w3.org/1999/xhtml";

    /// <summary>
    /// A document rooted at an XHTML <c>div</c> has no html element, so it has no body element either — and
    /// the five colours read the empty string however many coloured <c>body</c> children the root carries.
    /// </summary>
    [Test]
    public async Task ANonHtmlDocumentElementLeavesTheColoursWithNoTarget()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Page);

        (await page.EvaluateAsync<string>($$"""
            (function () {
              const HTML = '{{Html}}';
              const names = ['fgColor', 'linkColor', 'vlinkColor', 'alinkColor', 'bgColor'];

              // A document element of the given name, with a fully coloured body element straight under it.
              function rooted(namespaceUri, name) {
                const made = new Document();
                const root = made.createElementNS(namespaceUri, name);
                made.appendChild(root);
                const nested = made.createElementNS(HTML, 'body');
                nested.setAttribute('text', 'black');
                nested.setAttribute('link', 'blue');
                nested.setAttribute('vlink', 'purple');
                nested.setAttribute('alink', 'red');
                nested.setAttribute('bgcolor', 'white');
                root.appendChild(nested);
                return made;
              }

              const constructed = rooted(HTML, 'div');
              const foreign = rooted('urn:example:x', 'html');

              return [
                constructed.body === null,
                names.every(name => constructed[name] === ''),
                // The attributes are still on the node; it is simply not the body element.
                constructed.documentElement.firstElementChild.getAttribute('bgcolor'),
                foreign.body === null,
                names.every(name => foreign[name] === ''),
              ].join('|');
            })()
            """)).Should().Be("true|true|white|true|true");

        page.Errors.Should().BeEmpty();
    }

    /// <summary>"...and do nothing on setting", which is a write that leaves the tree exactly as it was.</summary>
    [Test]
    public async Task SettingAColourWithNoTargetDoesNothing()
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
              const nested = made.createElementNS(HTML, 'body');
              root.appendChild(nested);

              made.bgColor = 'lime';
              made.fgColor = 'lime';
              made.dir = 'rtl';

              return [
                made.bgColor,
                made.fgColor,
                made.dir,
                nested.hasAttribute('bgcolor'),
                nested.hasAttribute('text'),
                root.hasAttribute('bgcolor'),
                root.hasAttribute('dir'),
                made.documentElement.outerHTML,
              ].join('|');
            })()
            """)).Should().Be("|||false|false|false|false|<div><body></body></div>");

        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// <c>document.dir</c> reflects <i>the html element</i>'s attribute, which the document element is only
    /// while it is an HTML-namespace <c>html</c> — the same gate, one element higher.
    /// </summary>
    [Test]
    public async Task DirIsAboutTheHtmlElementAndNotTheDocumentElement()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Page);

        (await page.EvaluateAsync<string>($$"""
            (function () {
              const HTML = '{{Html}}';

              function rooted(namespaceUri, name) {
                const made = new Document();
                const root = made.createElementNS(namespaceUri, name);
                root.setAttribute('dir', 'rtl');
                made.appendChild(root);
                return made;
              }

              return [
                // An XHTML `div` root is not an html element.
                rooted(HTML, 'div').dir,
                // Nor is an `html` from another namespace.
                rooted('urn:example:x', 'html').dir,
                // An HTML-namespace `html` root is.
                rooted(HTML, 'html').dir,
              ].join('|');
            })()
            """)).Should().Be("||rtl");

        page.Errors.Should().BeEmpty();
    }

    /// <summary>An ordinary page is unchanged: it has an html element, so it has a body element.</summary>
    [Test]
    public async Task AnOrdinaryPageStillReflectsOntoItsBody()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Page);

        (await page.EvaluateAsync<string>("""
            (function () {
              const before = [
                document.fgColor,
                document.linkColor,
                document.vlinkColor,
                document.alinkColor,
                document.bgColor,
                document.dir,
              ].join(',');

              document.bgColor = 'lime';
              document.linkColor = 'teal';
              document.dir = 'ltr';

              return [
                before,
                document.body.getAttribute('bgcolor'),
                document.bgColor,
                document.body.getAttribute('link'),
                document.linkColor,
                document.documentElement.getAttribute('dir'),
                document.dir,
              ].join('|');
            })()
            """)).Should().Be("black,blue,purple,red,white,rtl|lime|lime|teal|teal|ltr|ltr");

        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// The gate is the html element and not the tag name of whatever the body happens to be: a
    /// <c>frameset</c> child of an html element is the body element, and a <c>body</c> that is only a
    /// descendant is not.
    /// </summary>
    [Test]
    public async Task TheColoursFollowTheBodyElementTheGateAnswers()
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
                child.setAttribute('bgcolor', childName);
                root.appendChild(child);
                return made;
              }

              const deep = new Document();
              const deepRoot = deep.createElementNS(HTML, 'html');
              deep.appendChild(deepRoot);
              const between = deep.createElementNS(HTML, 'div');
              deepRoot.appendChild(between);
              const buried = deep.createElementNS(HTML, 'body');
              buried.setAttribute('bgcolor', 'buried');
              between.appendChild(buried);

              return [
                rooted('body').bgColor,
                rooted('frameset').bgColor,
                deep.body === null,
                deep.bgColor,
              ].join('|');
            })()
            """)).Should().Be("body|frameset|true|");

        page.Errors.Should().BeEmpty();
    }
}
