using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Parsing;

/// <summary>
/// <a href="https://html.spec.whatwg.org/multipage/document-lifecycle.html#read-xml">HTML's read XML</a>:
/// which responses a page reads with the XML parser, what the document it produces answers, and the one
/// thing such a document deliberately does not do.
/// </summary>
/// <remarks>
/// <para>
/// <b>The frame half is what <c>NeedsXmlDocuments</c> was.</b> <c>AngleSharp.Xml</c>'s <c>WithXml()</c>
/// registers <c>text/xml</c>, <c>application/xml</c> and <c>image/svg+xml</c> and leaves
/// <c>application/xhtml+xml</c> on the HTML creator, so an XHTML frame came back wrapped in a second HTML
/// skeleton — which is the trailing newline two hundred and forty-four wpt rows died on.
/// </para>
/// <para>
/// <b>The navigation half is the other side of the same rule.</b> A top-level XML response used to be
/// refused outright, so <c>DOMParser</c>, a frame and a navigation gave three different answers for one
/// sequence of bytes; all three read the same bytes the same way now.
/// </para>
/// </remarks>
public class XmlDocumentLoadTests
{
    /// <summary>The wpt corpus's own XHTML fixture, whose trailing newline is what an HTML parse adds.</summary>
    private const string Xhtml =
        "<!DOCTYPE html>\n"
        + "<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>Dummy XHTML document</title></head><body /></html>\n";

    [Test]
    public async Task AFrameServedXhtmlGetsAnXmlDocumentAndNotAnHtmlOne()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/dummy.xhtml", _ => LoopbackResponse.Bytes(Xhtml, "application/xhtml+xml"))
            .MapHtml("/", "<!doctype html><html><body><iframe id=f src=\"/dummy.xhtml\"></iframe></body></html>"));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        // The assertion the corpus makes, and the one an HTML parse fails: an HTML parse puts the file's own
        // trailing newline inside the <body> it invents, so the answer was "Dummy XHTML document\n".
        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementById('f').contentDocument.documentElement.textContent"))
            .Should().Be("Dummy XHTML document");

        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementById('f').contentDocument.contentType")).Should().Be("application/xhtml+xml");

        // An XHTML document is not an HTML document, so DOM §4.9 leaves tagName as the qualified name.
        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementById('f').contentDocument.createElement('FOO').tagName")).Should().Be("FOO");

        // DOM §4.5 createElement step 4: the HTML namespace for a document whose content type is
        // application/xhtml+xml, and no namespace for any other XML document.
        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementById('f').contentDocument.createElement('foo').namespaceURI"))
            .Should().Be("http://www.w3.org/1999/xhtml");

        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AScriptInAnXmlDocumentDoesNotRun()
    {
        // AngleSharp's XML parser prepares no script element and never asks for the scripting service, which
        // is the same reason DOMParser's documents are inert. HTML's read XML does run them, so this is a
        // stated divergence rather than a decision: Dom/divergences.md carries the row.
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/scripted.xhtml", _ => LoopbackResponse.Bytes(
                "<html xmlns=\"http://www.w3.org/1999/xhtml\"><head>"
                + "<script>window.parent.ran = true;</script></head><body /></html>",
                "application/xhtml+xml"))
            .MapHtml("/", """
                <!doctype html><html><body>
                <script>window.ran = false;</script>
                <iframe id=f src="/scripted.xhtml"></iframe>
                </body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<bool>("window.ran")).Should().BeFalse();

        // The element is still in the tree with its text, which is what makes this an unprepared script and
        // not a lost one.
        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementById('f').contentDocument.getElementsByTagName('script')[0].textContent"))
            .Should().Be("window.parent.ran = true;");

        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AScriptInThePagesOwnXmlDocumentDoesNotRunEither()
    {
        // The page's own configuration *does* register the scripting service, so this is the half that says
        // the parser is what declines rather than the configuration: AngleSharp's XML tree construction has
        // no prepare-a-script step to ask it. It is the one thing HTML's read XML asks for that this does
        // not do, and it is why no `.xhtml` wpt document is vendorable — none of them could load
        // `testharness.js`.
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/", _ => LoopbackResponse.Bytes(
                "<html xmlns=\"http://www.w3.org/1999/xhtml\"><head>"
                + "<script>document.documentElement.setAttribute('ran', 'yes');</script></head><body /></html>",
                "application/xhtml+xml")));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<bool>("document.documentElement.hasAttribute('ran')")).Should().BeFalse();
        (await loopback.Page.EvaluateAsync<int>("document.getElementsByTagName('script').length")).Should().Be(1);

        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task APageNavigatedToAnXmlContentTypeGetsAnXmlDocument()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/", _ => LoopbackResponse.Bytes("<foo>Dummy XML document</foo>", "text/xml")));

        var response = await loopback.Page.NavigateAsync(loopback.Url("/"));

        response!.Ok.Should().BeTrue();

        // Without read XML this was a NavigationFailedException naming the type, and with only a document
        // factory it would have been an <html><body> skeleton around the text.
        (await loopback.Page.EvaluateAsync<string>("document.documentElement.tagName")).Should().Be("foo");
        (await loopback.Page.EvaluateAsync<string>("document.contentType")).Should().Be("text/xml");
        (await loopback.Page.EvaluateAsync<string>("document.documentElement.textContent"))
            .Should().Be("Dummy XML document");

        // HTML §3.1.3's body element is the first body or frameset child of the *html* element, and an XML
        // document rooted at <foo> has none — so a host reading a page cannot be handed the wrong tree.
        (await loopback.Page.EvaluateAsync<bool>("document.body === null")).Should().BeTrue();

        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task APageNavigatedToXhtmlGetsAnXmlDocumentWithAnHtmlNamespacedTree()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/", _ => LoopbackResponse.Bytes(Xhtml, "application/xhtml+xml")));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("document.contentType")).Should().Be("application/xhtml+xml");
        (await loopback.Page.EvaluateAsync<string>("document.documentElement.namespaceURI"))
            .Should().Be("http://www.w3.org/1999/xhtml");
        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementsByTagName('title')[0].textContent")).Should().Be("Dummy XHTML document");
        (await loopback.Page.EvaluateAsync<string>("document.documentElement.textContent"))
            .Should().Be("Dummy XHTML document");

        // `document.title` is the empty string here and a browser answers the <title>: AngleSharp's
        // XmlDocument inherits Document.GetTitle's `return String.Empty`. Dom/divergences.md carries the row;
        // it is the same answer a `text/xml` frame and a DOMParser document already gave.
        (await loopback.Page.EvaluateAsync<string>("document.title")).Should().BeEmpty();

        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnXmlMimeTypeAngleSharpDoesNotNameIsStillReadAsXml()
    {
        // https://mimesniff.spec.whatwg.org/#xml-mime-type — every `+xml` subtype is one, and AngleSharp's
        // own table names three. The rest fall through to PageDocumentFactory's default, which is what makes
        // the rule the standard's rather than the dependency's.
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/feed", _ => LoopbackResponse.Bytes(
                "<rss version=\"2.0\"><channel><title>feed</title></channel></rss>",
                "application/rss+xml")));

        await loopback.Page.NavigateAsync(loopback.Url("/feed"));

        (await loopback.Page.EvaluateAsync<string>("document.documentElement.tagName")).Should().Be("rss");
        (await loopback.Page.EvaluateAsync<string>("document.contentType")).Should().Be("application/rss+xml");
        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementsByTagName('title')[0].textContent")).Should().Be("feed");

        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AFrameServedSvgGetsAnSvgDocument()
    {
        // image/svg+xml is AngleSharp's own mapping and is deliberately left alone: WithXml() routes it to a
        // creator that builds an SvgDocument rather than a plain XML one, which is the document DOM §4.5.1
        // names for the SVG namespace. Widening the default to every XML MIME type must not take it over.
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/mark.svg", _ => LoopbackResponse.Bytes(
                "<svg xmlns=\"http://www.w3.org/2000/svg\"><title>a mark</title></svg>",
                "image/svg+xml"))
            .MapHtml("/", "<!doctype html><html><body><iframe id=f src=\"/mark.svg\"></iframe></body></html>"));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementById('f').contentDocument.documentElement.tagName")).Should().Be("svg");
        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementById('f').contentDocument.documentElement.namespaceURI"))
            .Should().Be("http://www.w3.org/2000/svg");

        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ATextDocumentIsStillWrappedAndReadAsHtml()
    {
        // The other half of the read decision, kept here because it is the one an XML arm is easiest to
        // break: HTML's read text produces a <pre> skeleton, and what the parse is handed is therefore
        // markup whatever the response's own type was.
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/notes.txt", _ => LoopbackResponse.Text("<foo>not markup</foo>")));

        await loopback.Page.NavigateAsync(loopback.Url("/notes.txt"));

        (await loopback.Page.EvaluateAsync<string>("document.contentType")).Should().Be("text/html");
        (await loopback.Page.EvaluateAsync<string>("document.body.textContent")).Should().Be("<foo>not markup</foo>");
        (await loopback.Page.EvaluateAsync<int>("document.getElementsByTagName('pre').length")).Should().Be(1);

        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AContentTypeThatIsNeitherMarkupNorTextIsStillRefused()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/data", _ => LoopbackResponse.Bytes("unsupported", "application/octet-stream")));

        var navigate = async () => await loopback.Page.NavigateAsync(loopback.Url("/data"));

        (await navigate.Should().ThrowAsync<NavigationFailedException>())
            .Which.Message.Should().Contain("application/octet-stream");
    }
}
