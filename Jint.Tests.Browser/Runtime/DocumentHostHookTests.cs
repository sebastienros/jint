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
    /// A write into a document the page is not showing is refused on its own terms: the page was not
    /// involved, so no page error is recorded, and nothing is swallowed either.
    /// <para>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#document-write-steps
    /// </para>
    /// </summary>
    /// <remarks>
    /// The refusal used to select its page runtime by engine alone, so every secondary document in the
    /// page's engine was treated as the page and given the page's refusal — a silent no-op <em>and</em> an
    /// error against a page that had nothing to do with it (#3954). What decides now is which document the
    /// call targets. An XML document is HTML's own first step; anything else with no parser reading it is a
    /// <c>NotSupportedError</c> naming the capability AngleSharp does not have.
    /// </remarks>
    [Test]
    public async Task ASecondaryDocumentIsRefusedOnItsOwnTermsAndNotAsThePage()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p id='page'>page</p>");

        (await page.EvaluateAsync<string>(
                """
                (function () {
                  function refusal(document_, call) {
                    try { call(document_); return 'wrote'; } catch (e) { return e.name; }
                  }

                  const parsed = new DOMParser().parseFromString('<p id="old">old</p>', 'text/html');
                  const constructed = new Document();
                  const created = document.implementation.createHTMLDocument('t');
                  const xml = new DOMParser().parseFromString('<root/>', 'text/xml');

                  return [
                    refusal(parsed, d => d.write('<p id="written">written</p>')),
                    refusal(parsed, d => d.writeln('<p id="written">written</p>')),
                    // The target is untouched: the refusal is a refusal and not a half-open document.
                    parsed.getElementById('old') !== null,
                    parsed.getElementById('written') === null,
                    parsed.readyState,
                    refusal(constructed, d => d.write('x')),
                    refusal(created, d => d.write('<p>x</p>')),
                    created.body.childElementCount,
                    refusal(xml, d => d.write('<p>x</p>')),
                  ].join('|');
                })()
                """))
            .Should().Be(
                "NotSupportedError|NotSupportedError|true|true|complete|"
                + "InvalidStateError|NotSupportedError|0|InvalidStateError");

        page.Errors.Should().BeEmpty("the page was not the document any of those writes targeted");
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
