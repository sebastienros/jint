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
}
