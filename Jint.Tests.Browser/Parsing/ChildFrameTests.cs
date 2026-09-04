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
    public async Task ContentWindowIsNullBecauseAFrameHasNoRealm()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/child.html", "<!doctype html><html><body>child</body></html>")
            .MapHtml("/", "<!doctype html><html><body><iframe id=f src=\"/child.html\"></iframe></body></html>"));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        // Null rather than absent: `'contentWindow' in frame` and `if (frame.contentWindow)` disagree about a
        // member that is missing and one that is null, and a page tests both.
        (await loopback.Page.EvaluateAsync<bool>("document.getElementById('f').contentWindow === null"))
            .Should().BeTrue();
        (await loopback.Page.EvaluateAsync<bool>("'contentWindow' in document.getElementById('f')"))
            .Should().BeTrue();
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
