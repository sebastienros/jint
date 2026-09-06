using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Parsing;

/// <summary>
/// What a child frame is once it has a document of its own
/// (<a href="https://github.com/sebastienros/jint/issues/3771">#3771</a>): the fetch, the
/// <c>load</c> at the element, what <c>contentDocument</c> answers and to whom — and the realm it still
/// does not have, which is what keeps its scripts from running in the page's.
/// </summary>
public class ChildFrameTests
{
    [Test]
    public async Task AFrameGetsADocumentAndLoadArrivesAtTheElement()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/child.html", "<!doctype html><html><body><p id=inner>child</p></body></html>")
            .MapHtml("/", """
                <!doctype html><html><body>
                <iframe id=f src="/child.html"></iframe>
                <script>
                  window.log = [];
                  document.getElementById('f').onload = function (e) {
                    window.log.push('load:' + e.target.id + ':' + e.target.contentDocument.getElementById('inner').textContent);
                  };
                </script>
                </body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("load:f:child");

        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AFramesLoadArrivesBeforeTheWindows()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/child.html", "<!doctype html><html><body>child</body></html>")
            .MapHtml("/", """
                <!doctype html><html><body>
                <iframe id=f src="/child.html"></iframe>
                <script>
                  window.log = [];
                  document.addEventListener('DOMContentLoaded', function () {
                    window.log.push('dcl:' + (document.getElementById('f').contentDocument === null ? 'none' : 'doc'));
                  });
                  document.getElementById('f').onload = function () { window.log.push('frame:' + document.readyState); };
                  window.addEventListener('load', function () { window.log.push('window:' + document.readyState); });
                </script>
                </body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        // https://html.spec.whatwg.org/multipage/parsing.html#the-end — DOMContentLoaded does not wait for a
        // frame, step 6 spins until nothing delays the load event (an <iframe> does), and only then does
        // readyState become "complete" and load fire at the window.
        (await loopback.Page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("dcl:doc|frame:interactive|window:complete");
    }

    [Test]
    public async Task ANestedFrameLoadsBeforeTheFrameThatHoldsIt()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/inner.html", "<!doctype html><html><body>inner</body></html>")
            .MapHtml("/outer.html", "<!doctype html><html><body><iframe id=inner src=\"/inner.html\"></iframe></body></html>")
            .MapHtml("/", """
                <!doctype html><html><body>
                <iframe id=outer src="/outer.html"></iframe>
                <script>
                  window.log = [];
                  var outer = document.getElementById('outer');
                  outer.onload = function () {
                    var inner = outer.contentDocument.getElementById('inner');
                    window.log.push('outer:' + (inner.contentDocument === null ? 'none' : inner.contentDocument.body.textContent));
                  };
                </script>
                </body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("outer:inner");
        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AFrameDocumentRunsNoScriptAndLeavesThePageAlone()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/inframe.js", _ => LoopbackResponse.Script("window.leakedExternal = true;"))
            .MapHtml(
                "/child.html",
                "<!doctype html><html><body><script>window.leakedInline = true;</script>"
                + "<script src=\"/inframe.js\"></script><p id=inner>child</p></body></html>")
            .MapHtml("/", """
                <!doctype html><html><body>
                <iframe id=f src="/child.html"></iframe>
                </body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        // A frame has a document and no realm, so neither half of a script in it runs — and neither runs in
        // the page's realm, which is the only other realm there is.
        (await loopback.Page.EvaluateAsync<bool>("typeof window.leakedInline === 'undefined'")).Should().BeTrue();
        (await loopback.Page.EvaluateAsync<bool>("typeof window.leakedExternal === 'undefined'")).Should().BeTrue();

        // The external one names a reference the page did not follow, so it is in the request log; the
        // inline one is not a reference and is not.
        loopback.Page.Requests.Should().ContainSingle(r => r.Url.EndsWith("/inframe.js", StringComparison.Ordinal)
            && r.NotFetchedReason != null);
        loopback.Server.Received.Should().NotContain(request => request.Path == "/inframe.js");

        // And the frame's tree is the frame's: it never reached the page's document.
        (await loopback.Page.EvaluateAsync<bool>("document.getElementById('inner') === null")).Should().BeTrue();
        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementById('f').contentDocument.getElementById('inner').textContent"))
            .Should().Be("child");
    }

    [Test]
    public async Task ASrcdocFramesScriptDoesNotReplaceThePagesOwnDocument()
    {
        // The regression this pins: srcdoc needs no fetch, so nothing gated it — the frame's script ran on
        // the page's Window and the srcdoc markup replaced the page's own tree, with nothing recorded.
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", """
                <!doctype html><html><body>
                <iframe id=f srcdoc="&lt;script&gt;window.leaked = 'yes'; document.title = 'set-by-frame';&lt;/script&gt;&lt;p id=inner&gt;hi&lt;/p&gt;"></iframe>
                </body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<bool>("typeof window.leaked === 'undefined'")).Should().BeTrue();
        (await loopback.Page.EvaluateAsync<string>("document.title")).Should().BeEmpty();
        (await loopback.Page.EvaluateAsync<bool>("document.getElementById('inner') === null")).Should().BeTrue();
        (await loopback.Page.EvaluateAsync<bool>("document.getElementById('f') !== null")).Should().BeTrue();

        // A srcdoc document is the frame's own, and it inherits the page's URL — so it is same origin and
        // readable, and its own script is not in it.
        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementById('f').contentDocument.getElementById('inner').textContent"))
            .Should().Be("hi");
    }

    [Test]
    public async Task AnAboutBlankFrameGetsAnEmptyDocumentAndNoRequest()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", """
                <!doctype html><html><body>
                <iframe id=f src="about:blank"></iframe>
                </body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<bool>("document.getElementById('f').contentDocument !== null"))
            .Should().BeTrue();
        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementById('f').contentDocument.body.innerHTML")).Should().BeEmpty();

        loopback.Page.Errors.Should().BeEmpty();
        loopback.Page.Requests.Should().NotContain(r => r.Url.Contains("about:blank", StringComparison.Ordinal));
    }

    [Test]
    public async Task AFrameServedXmlGetsAnXmlDocument()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/dummy.xml", _ => LoopbackResponse.Bytes("<foo>Dummy XML document</foo>", "text/xml"))
            .MapHtml("/", "<!doctype html><html><body><iframe id=f src=\"/dummy.xml\"></iframe></body></html>"));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        // https://html.spec.whatwg.org/multipage/document-lifecycle.html#read-xml — a document whose content
        // type is an XML MIME type is parsed by the XML parser. Without AngleSharp.Xml's factory registered
        // the response came back as an *HTML* document with the text inside an <html><body> skeleton, so the
        // root element was HTML and every XML rule a page then asked about was the wrong document's.
        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementById('f').contentDocument.documentElement.tagName")).Should().Be("foo");

        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementById('f').contentDocument.contentType")).Should().Be("text/xml");

        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementById('f').contentDocument.documentElement.textContent"))
            .Should().Be("Dummy XML document");

        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task RegisteringTheXmlFactoryDoesNotMakeAPageNavigableToXml()
    {
        // The XML factory is registered on the whole browsing context, so this is the half that says only a
        // *frame* can reach it. A top-level navigation to an XML content type is refused before any parser
        // sees it — `DocumentFetch` decides that, and registering a document factory does not change it.
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/", _ => LoopbackResponse.Bytes("<foo>not a page</foo>", "text/xml")));

        var navigate = async () => await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await navigate.Should().ThrowAsync<NavigationFailedException>())
            .Which.Message.Should().Contain("text/xml");
    }

    [Test]
    public async Task AFrameHasAWindowOfItsOwnOnThePagesRealm()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/child.html", "<!doctype html><html><body><p id=inner>child</p></body></html>")
            .MapHtml("/", "<!doctype html><html><body><iframe id=f name=side src=\"/child.html\"></iframe></body></html>"));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        // A window of its own — the property the corpus tests, and the reason a frame gets an object at all.
        (await loopback.Page.EvaluateAsync<bool>("document.getElementById('f').contentWindow !== window"))
            .Should().BeTrue();
        (await loopback.Page.EvaluateAsync<bool>(
            "document.getElementById('f').contentWindow === document.getElementById('f').contentWindow"))
            .Should().BeTrue("a frame answers the same window every time");

        // Its document, and the same object `contentDocument` answers.
        (await loopback.Page.EvaluateAsync<bool>(
            "document.getElementById('f').contentWindow.document === document.getElementById('f').contentDocument"))
            .Should().BeTrue();
        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementById('f').contentWindow.document.getElementById('inner').textContent"))
            .Should().Be("child");

        // The three names that mean "this window", and the two that mean "the one above".
        (await loopback.Page.EvaluateAsync<bool>(
            "var w = document.getElementById('f').contentWindow; w.window === w && w.self === w && w.frames === w"))
            .Should().BeTrue();
        (await loopback.Page.EvaluateAsync<bool>(
            "var w = document.getElementById('f').contentWindow; w.parent === window && w.top === window"))
            .Should().BeTrue();
        (await loopback.Page.EvaluateAsync<bool>(
            "document.getElementById('f').contentWindow.frameElement === document.getElementById('f')"))
            .Should().BeTrue();

        // Indexed and named access on the page's own window reach the same object.
        (await loopback.Page.EvaluateAsync<bool>("frames[0] === document.getElementById('f').contentWindow"))
            .Should().BeTrue();
        (await loopback.Page.EvaluateAsync<bool>("window.side === document.getElementById('f').contentWindow"))
            .Should().BeTrue("HTML answers a named frame's window, not its element");
        (await loopback.Page.EvaluateAsync<double>("window.length")).Should().Be(1);
        (await loopback.Page.EvaluateAsync<bool>("frames[1] === undefined")).Should().BeTrue();
    }

    [Test]
    public async Task AFrameDocumentHasThatWindowAsItsDefaultView()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/child.html", "<!doctype html><html><body>child</body></html>")
            .MapHtml("/", "<!doctype html><html><body><iframe id=f src=\"/child.html\"></iframe></body></html>"));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<bool>(
            "document.getElementById('f').contentDocument.defaultView === document.getElementById('f').contentWindow"))
            .Should().BeTrue();

        // What a large part of the DOM corpus reaches `defaultView` for: a constructor to compare a refusal
        // against. It is the page's, because there is one realm — Runtime/FrameWindows argues it and
        // Dom/divergences.md records it.
        (await loopback.Page.EvaluateAsync<bool>(
            "document.getElementById('f').contentDocument.defaultView.DOMException === DOMException"))
            .Should().BeTrue();
    }

    [Test]
    public async Task AFramesLocationReadsItsOwnUrlAndRefusesAWriteOutLoud()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/child.html", "<!doctype html><html><body>child</body></html>")
            .MapHtml("/", "<!doctype html><html><body><iframe id=f src=\"/child.html?q=1#h\"></iframe></body></html>"));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        // The frame's URL, not the page's — the reason `location` is shadowed rather than inherited.
        (await loopback.Page.EvaluateAsync<bool>(
            "document.getElementById('f').contentWindow.location.href.indexOf('/child.html') !== -1"))
            .Should().BeTrue();
        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementById('f').contentWindow.location.pathname")).Should().Be("/child.html");
        (await loopback.Page.EvaluateAsync<string>(
            "document.getElementById('f').contentWindow.location.search")).Should().Be("?q=1");
        (await loopback.Page.EvaluateAsync<bool>(
            "document.getElementById('f').contentWindow.location.href !== location.href")).Should().BeTrue();

        // And a write throws rather than doing nothing. A silent no-op is what turned a wpt document that
        // navigates a frame into one that times out; a refusal a page can see fails it fast instead.
        (await loopback.Page.EvaluateAsync<string>(
            "(function () { try { document.getElementById('f').contentWindow.location.href = '/other'; return 'no throw'; } "
            + "catch (e) { return e.constructor.name; } })()"))
            .Should().Be("TypeError");
    }

    [Test]
    public async Task TheCustomElementCorpusHelperResolvesWithAWindowWhoseConstructorsAreThePages()
    {
        // `create_window_in_test` out of wpt's own `custom-elements/resources/custom-elements-helpers.js`,
        // in the shape that file uses it: a frame made in script, `srcdoc`, and the window read out of
        // `onload`. Thirty-seven `custom-elements/` documents are built on it, and the reason they are in the
        // not-vendored table was that it resolved with nothing. It resolves now — and the last line is why
        // that is not the same as those documents reporting: what they compare across the frame is a
        // *constructor*, and a frame shares the page's realm, so it is the page's constructor. That is the
        // realm half of #3771, and `Dom/divergences.md` records the identity it makes true.
        const string page = """
            <!doctype html><html><body><script>
            window.result = 'pending';
            var f = document.createElement('iframe');
            f.srcdoc = '<p id=inner>frame</p>';
            f.onload = function () {
              var w = f.contentWindow;
              window.result = !w ? 'no window'
                : w.document.getElementById('inner') === null ? 'no document'
                : w.HTMLElement === HTMLElement ? 'same constructor' : 'own constructor';
            };
            document.body.appendChild(f);
            </script></body></html>
            """;

        await using var loopback = await LoopbackPage.CreateAsync(server => server.MapHtml("/", page));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("window.result")).Should().Be("same constructor");
    }

    [Test]
    public async Task ACrossOriginFramesDocumentIsNotReadable()
    {
        using var other = new LoopbackServer();
        other.MapHtml("/other.html", "<!doctype html><html><body><p id=inner>other</p></body></html>");

        await using var loopback = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/", "<!doctype html><html><body><iframe id=f src=\"" + other.Url("/other.html")
                + "\"></iframe></body></html>"),
            context => context.UrlFilter = uri => uri.IsLoopback);

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        // The frame really loaded — the other origin answered it — and the page still may not read it.
        other.Received.Should().Contain(request => request.Path == "/other.html");
        (await loopback.Page.EvaluateAsync<bool>("document.getElementById('f').contentDocument === null"))
            .Should().BeTrue();
    }

    [Test]
    public async Task MaxFrameDocumentsBoundsWhatOneLoadFetches()
    {
        await using var loopback = await LoopbackPage.CreateAsync(
            server => server
                .MapHtml("/a.html", "<!doctype html><html><body>a</body></html>")
                .MapHtml("/b.html", "<!doctype html><html><body>b</body></html>")
                .MapHtml("/", """
                    <!doctype html><html><body>
                    <iframe id=a src="/a.html"></iframe>
                    <iframe id=b src="/b.html"></iframe>
                    </body></html>
                    """),
            configureBrowser: options => options.MaxFrameDocuments = 1);

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<bool>("document.getElementById('a').contentDocument !== null"))
            .Should().BeTrue();
        (await loopback.Page.EvaluateAsync<bool>("document.getElementById('b').contentDocument === null"))
            .Should().BeTrue();

        loopback.Server.Received.Should().NotContain(request => request.Path == "/b.html");
        loopback.Page.Requests.Should().ContainSingle(r => r.Url.EndsWith("/b.html", StringComparison.Ordinal)
            && r.NotFetchedReason != null
            && r.NotFetchedReason.Contains("MaxFrameDocuments", StringComparison.Ordinal));
    }

    [Test]
    public async Task ZeroMaxFrameDocumentsLeavesEveryFrameWithoutADocument()
    {
        await using var loopback = await LoopbackPage.CreateAsync(
            server => server
                .MapHtml("/a.html", "<!doctype html><html><body>a</body></html>")
                .MapHtml("/", "<!doctype html><html><body><iframe id=a src=\"/a.html\"></iframe></body></html>"),
            configureBrowser: options => options.MaxFrameDocuments = 0);

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<bool>("document.getElementById('a').contentDocument === null"))
            .Should().BeTrue();
        loopback.Server.Received.Should().NotContain(request => request.Path == "/a.html");
    }

    [Test]
    public async Task AFrameWhoseDocumentCannotBeFetchedHasNoneAndSaysSo()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", """
                <!doctype html><html><body>
                <iframe id=f src="/missing.html"></iframe>
                <script>
                  window.log = [];
                  document.getElementById('f').onload = function () { window.log.push('load'); };
                </script>
                </body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<bool>("document.getElementById('f').contentDocument === null"))
            .Should().BeTrue();

        // `load` is what a frame that got a document hears, so a frame that did not hears none of it.
        (await loopback.Page.EvaluateAsync<string>("window.log.join('|')")).Should().BeEmpty();

        // What the page is told instead is the failure itself, named with the URL that failed.
        loopback.Page.Errors.Should().ContainSingle(e => e.Message.Contains("/missing.html", StringComparison.Ordinal));

        // `ParserDriver.FailSubresource` dispatches `error` at the element as well, and nothing a page could
        // have registered is there to hear it: a frame's fetch happens the moment AngleSharp applies `src`,
        // which is before the element is in the document and before any script below it has run. That timing
        // is this browser's — a browser's frame load is asynchronous — and it is the same one an <img> that
        // fails already has, so what a page can act on is `Page.Errors`.
    }
}
