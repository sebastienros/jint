#nullable enable

using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Runtime;

/// <summary>
/// The document and node members whose live values come from a page runtime only for the document that
/// runtime is showing.
/// </summary>
public sealed class DocumentHostHookTests
{
    [Test]
    public async Task ParsedAndConstructedDocumentsDoNotInheritThePagesState()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/from", "<title>from</title>")
            .MapHtml(
                "/page/index.html",
                """
                <!doctype html>
                <base href="/page-base/">
                <script id="running">
                  document.cookie = 'page=kept; Path=/';

                  globalThis.secondary = new DOMParser().parseFromString(
                    '<html><head></head><body><p id="child"></p></body></html>',
                    'text/html');
                  globalThis.detached = new Document();

                  globalThis.pageDuringParse = [
                    document.URL,
                    document.documentURI,
                    document.baseURI,
                    document.referrer,
                    document.readyState,
                    document.currentScript && document.currentScript.id,
                    document.cookie,
                  ].join('|');

                  globalThis.secondaryDuringParse = [
                    secondary.URL,
                    secondary.documentURI,
                    secondary.baseURI,
                    secondary.getElementById('child').baseURI,
                    secondary.referrer,
                    secondary.readyState,
                    secondary.currentScript === null,
                    secondary.cookie,
                  ].join('|');

                  globalThis.detachedDuringParse = [
                    detached.URL,
                    detached.documentURI,
                    detached.baseURI,
                    detached.referrer,
                    detached.readyState,
                    detached.currentScript === null,
                    detached.cookie,
                  ].join('|');

                  secondary.cookie = 'secondary=isolated; Path=/';
                  detached.cookie = 'detached=isolated; Path=/';
                  globalThis.pageCookieAfterSecondaryWrites = document.cookie;
                </script>
                """));

        await fixture.Page.NavigateAsync(fixture.Url("/from"));
        await fixture.Page.NavigateAsync(fixture.Url("/page/index.html"));

        (await fixture.Page.EvaluateAsync<string>("pageDuringParse"))
            .Should().Be(string.Join(
                "|",
                fixture.Url("/page/index.html"),
                fixture.Url("/page/index.html"),
                fixture.Url("/page-base/"),
                fixture.Url("/from"),
                "loading",
                "running",
                "page=kept"));

        (await fixture.Page.EvaluateAsync<string>("secondaryDuringParse"))
            .Should().Be("about:blank|about:blank|about:blank|about:blank||complete|true|");

        (await fixture.Page.EvaluateAsync<string>("detachedDuringParse"))
            .Should().Be("about:blank|about:blank|about:blank||loading|true|");

        (await fixture.Page.EvaluateAsync<string>("pageCookieAfterSecondaryWrites"))
            .Should().Be("page=kept", "secondary documents have no access to the page's cookie jar");

        (await fixture.Page.EvaluateAsync<string>(
                """
                const first = secondary.createElement('base');
                first.setAttribute('href', 'https://first.example/root/');
                secondary.head.append(first);
                [secondary.baseURI, secondary.body.baseURI].join('|');
                """))
            .Should().Be("https://first.example/root/|https://first.example/root/");

        (await fixture.Page.EvaluateAsync<string>(
                """
                const second = secondary.createElement('base');
                second.setAttribute('href', 'https://second.example/root/');
                secondary.head.append(second);
                const before = secondary.baseURI;
                first.remove();
                const afterFirst = secondary.baseURI;
                second.remove();
                [before, afterFirst, secondary.baseURI, secondary.body.baseURI].join('|');
                """))
            .Should().Be("https://first.example/root/|https://second.example/root/|about:blank|about:blank");

        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task TheDisplayedDocumentKeepsTheRuntimeUrlAcrossHistoryChanges()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml(
            "/page/index.html",
            "<!doctype html><base href='assets/'><p>page</p>"));

        await fixture.Page.NavigateAsync(fixture.Url("/page/index.html"));
        await fixture.Page.EvaluateAsync("history.pushState(null, '', '/moved/route.html')");

        (await fixture.Page.EvaluateAsync<string>("[document.URL, document.documentURI, document.baseURI].join('|')"))
            .Should().Be(string.Join(
                "|",
                fixture.Url("/moved/route.html"),
                fixture.Url("/moved/route.html"),
                fixture.Url("/moved/assets/")));

        await fixture.Page.EvaluateAsync("location.hash = '#part'");
        (await fixture.Page.WaitForIdleAsync(TimeSpan.FromSeconds(2))).Should().BeTrue();

        (await fixture.Page.EvaluateAsync<string>("[document.URL, document.documentURI, document.baseURI].join('|')"))
            .Should().Be(string.Join(
                "|",
                fixture.Url("/moved/route.html#part"),
                fixture.Url("/moved/route.html#part"),
                fixture.Url("/moved/assets/")));

        fixture.Server.Received.Count(request => request.Path == "/page/index.html").Should().Be(1);
    }

    [Test]
    public async Task NonHtmlDocumentsKeepTheirOwnBaseUriSemantics()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>page</p>", "https://page.example/root/");

        (await page.EvaluateAsync<string>(
                """
                const xml = new DOMParser().parseFromString(
                  '<base href="https://html-base.example/"><child/></base>',
                  'application/xml');
                [xml.baseURI, xml.documentElement.baseURI, xml.documentElement.firstElementChild.baseURI].join('|');
                """))
            .Should().Be("about:blank|about:blank|about:blank");
    }

    /// <summary>
    /// A write into a document the page is not showing operates on <em>that</em> document: the implied
    /// <c>document.open()</c> replaces its content, and the page — which was not involved — hears nothing.
    /// <para>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#document-write-steps
    /// </para>
    /// </summary>
    /// <remarks>
    /// The refusal used to select its page runtime by engine alone, so every secondary document in the
    /// page's engine was treated as the page and given the page's refusal — a silent no-op <em>and</em> an
    /// error against a page that had nothing to do with it (#3954). What decides now is which document the
    /// call targets: an XML document is HTML's own first step and still an <c>InvalidStateError</c>, the
    /// displayed document keeps its no-op, and a secondary HTML document gets the standard's own steps —
    /// run here, because every one of these documents sits in a browsing context with no parent, which is
    /// the reference AngleSharp's <c>Document.Open</c> dereferences.
    /// </remarks>
    [Test]
    public async Task ASecondaryDocumentIsWrittenOnItsOwnTermsAndNotAsThePage()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p id='page'>page</p>");

        (await page.EvaluateAsync<string>(
                """
                (function () {
                  function outcome(document_, call) {
                    try { call(document_); return 'wrote'; } catch (e) { return e.name; }
                  }

                  const parsed = new DOMParser().parseFromString('<p id="old">old</p>', 'text/html');
                  const constructed = new Document();
                  const created = document.implementation.createHTMLDocument('t');
                  const xml = new DOMParser().parseFromString('<root/>', 'text/xml');

                  return [
                    outcome(parsed, d => d.write('<p id="written">written</p>')),
                    // The issue's own reproduction: the implied open() replaced the document.
                    parsed.getElementById('old') !== null,
                    parsed.getElementById('written') !== null,
                    parsed.body.firstElementChild.id,
                    // writeln is the write steps with a newline, and a second call with no close between
                    // them appends at the insertion point rather than reopening.
                    outcome(parsed, d => d.writeln('<p id="second">second</p>')),
                    parsed.getElementById('written') !== null,
                    parsed.getElementById('second') !== null,
                    // A constructed Document is DOM's XML one, which has no dynamic markup insertion at all.
                    outcome(constructed, d => d.write('x')),
                    // A document the implementation created is an HTML one with no browsing context, so it
                    // takes the same steps as a parsed one -- and the title it was created with goes.
                    outcome(created, d => d.write('<p>x</p>')),
                    created.body.childElementCount,
                    created.title,
                    outcome(xml, d => d.write('<p>x</p>')),
                    // The page is untouched, whatever those documents did.
                    document.getElementById('page') !== null,
                  ].join('|');
                })()
                """))
            .Should().Be(
                "wrote|false|true|written|"
                + "wrote|true|true|"
                + "InvalidStateError|wrote|1||InvalidStateError|true");

        page.Errors.Should().BeEmpty("the page was not the document any of those writes targeted");
    }

    /// <summary>
    /// Two writes with no close between them concatenate at the insertion point, which is the whole reason
    /// HTML has one: a page that splits a tag across two calls gets one element, not two documents.
    /// <para>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#insertion-point
    /// </para>
    /// </summary>
    [Test]
    public async Task TwoWritesConcatenateAtTheInsertionPoint()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p id='page'>page</p>");

        (await page.EvaluateAsync<string>(
                """
                (function () {
                  const d = new DOMParser().parseFromString('<p id="old">old</p>', 'text/html');
                  d.write('<p id="split">a');
                  d.write('b</p>');
                  return [
                    d.body.childElementCount,
                    d.getElementById('split').textContent,
                    d.getElementById('old') === null,
                  ].join('|');
                })()
                """))
            .Should().Be("1|ab|true");

        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// <c>open()</c> answers the document it emptied, and <c>close()</c> ends the session so that the next
    /// write opens it afresh rather than appending to what came before.
    /// <para>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#document-open-steps
    /// </para>
    /// </summary>
    [Test]
    public async Task OpenEmptiesTheDocumentAndCloseEndsTheSession()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p id='page'>page</p>");

        (await page.EvaluateAsync<string>(
                """
                (function () {
                  const d = new DOMParser().parseFromString('<p id="old">old</p>', 'text/html');
                  const answered = d.open();
                  const emptied = [answered === d, d.documentElement === null, d.doctype === null].join('|');

                  d.write('<p id="first">first</p>');
                  d.close();
                  d.write('<p id="second">second</p>');

                  return [
                    emptied,
                    d.getElementById('first') === null,
                    d.getElementById('second') !== null,
                    // A close with no session open is HTML's own no-op, not a refusal.
                    (function () { try { d.close(); d.close(); return 'ok'; } catch (e) { return e.name; } })(),
                  ].join('|');
                })()
                """))
            .Should().Be("true|true|true|true|true|ok");

        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// A <c>&lt;script&gt;</c> written into a secondary document is an element with text and nothing more:
    /// the reparse runs with <c>IsScripting</c> false in a browsing context carrying no scripting service,
    /// exactly as <c>DOMParser</c>'s own parse does.
    /// </summary>
    [Test]
    public async Task AScriptWrittenIntoASecondaryDocumentStaysInert()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p id='page'>page</p>");

        (await page.EvaluateAsync<string>(
                """
                (function () {
                  const d = new DOMParser().parseFromString('<p>old</p>', 'text/html');
                  d.write('<script>globalThis.ranFromWrite = true;<\/script><p id="after">after</p>');
                  return [
                    globalThis.ranFromWrite === undefined,
                    d.querySelector('script') !== null,
                    d.querySelector('script').textContent,
                    d.getElementById('after') !== null,
                  ].join('|');
                })()
                """))
            .Should().Be("true|true|globalThis.ranFromWrite = true;|true");

        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// The document object survives the write: the reference the script already holds, its wrapper and its
    /// expandos are all still the one <c>DOMParser</c> handed back. Building a replacement document and
    /// swapping it in would be a different document wearing the same name.
    /// </summary>
    [Test]
    public async Task TheWrittenDocumentIsTheObjectTheScriptAlreadyHeld()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p id='page'>page</p>");

        (await page.EvaluateAsync<string>(
                """
                (function () {
                  const d = new DOMParser().parseFromString('<p id="old">old</p>', 'text/html');
                  d.expando = 'kept';
                  const before = d;
                  const orphan = d.getElementById('old');

                  d.write('<p id="written">written</p>');

                  return [
                    d === before,
                    d.expando,
                    d.getElementById('written').ownerDocument === d,
                    // The nodes the script held are off the tree but still the document's, which is what
                    // "replace all with null within document" leaves behind.
                    orphan.isConnected,
                    d.contains(orphan),
                    orphan.ownerDocument === d,
                  ].join('|');
                })()
                """))
            .Should().Be("true|kept|true|false|false|true");

        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// The displayed document keeps the refusal it has always had — the call does nothing and the page is
    /// told why — because that page is one a host is watching and a throw would break a script that believes
    /// it is still writing into a parse.
    /// </summary>
    [Test]
    public async Task TheDisplayedDocumentStillNoOpsAndRecordsAPageError()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p id='page'>page</p>");

        (await page.EvaluateAsync<string>(
                """
                (function () {
                  let threw = 'no';
                  try { document.write('<p id="late">late</p>'); } catch (e) { threw = e.name; }
                  return [threw, document.getElementById('page') !== null, document.getElementById('late') === null]
                    .join('|');
                })()
                """))
            .Should().Be("no|true|true");

        page.Errors.Should().ContainSingle(error => error.Message.Contains("document.open()", StringComparison.Ordinal));
    }

    /// <summary>
    /// A frame's document is the page's too — it is in the displayed browsing-context tree — so it takes the
    /// page's refusal rather than the secondary one. Selecting on the one displayed document instead would
    /// have sent it to AngleSharp's <c>Document.Open</c>, which fires the frame's unload and empties its tree.
    /// </summary>
    [Test]
    public async Task AFrameDocumentTakesTheDisplayedRefusalRatherThanTheSecondaryOne()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/frames/child.html", "<p id='kept'>frame</p>")
            .MapHtml("/page/index.html", "<iframe src='/frames/child.html'></iframe>"));

        await fixture.Page.NavigateAsync(fixture.Url("/page/index.html"));

        (await fixture.Page.EvaluateAsync<string>(
                """
                (function () {
                  const frame = document.querySelector('iframe').contentDocument;
                  let threw = 'no';
                  try { frame.write('<p id="late">late</p>'); } catch (e) { threw = e.name; }
                  return [threw, frame.getElementById('kept') !== null, frame.getElementById('late') === null]
                    .join('|');
                })()
                """))
            .Should().Be("no|true|true");

        fixture.Page.Errors.Should().ContainSingle(error =>
            error.Message.Contains("document.open()", StringComparison.Ordinal));
    }

    [Test]
    public async Task FrameDocumentsUseTheSharedJarAtTheirOwnUrl()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/frames/child.html", "<p>frame</p>")
            .MapHtml("/page/index.html", "<iframe src='/frames/child.html'></iframe>"));

        await fixture.Page.NavigateAsync(fixture.Url("/page/index.html"));
        await fixture.Page.EvaluateAsync("document.cookie = 'shared=value; Path=/'");

        (await fixture.Page.EvaluateAsync<string>(
                "document.querySelector('iframe').contentDocument.cookie"))
            .Should().Be("shared=value");

        await fixture.Page.EvaluateAsync(
            "document.querySelector('iframe').contentDocument.cookie = 'frame=value; Path=/frames'");

        (await fixture.Page.EvaluateAsync<string>("document.cookie"))
            .Should().Be("shared=value", "the frame's path-scoped cookie does not belong to the page URL");
        (await fixture.Page.EvaluateAsync<string>(
                "document.querySelector('iframe').contentDocument.cookie"))
            .Should().Contain("frame=value");
    }
}
