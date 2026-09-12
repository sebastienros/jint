using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Parsing;

/// <summary>
/// HTML §4.8.4.3's image request, over a real socket: what a page reads off an <c>&lt;img&gt;</c>, when it
/// hears about it, and what a browser with no pixels answers instead.
/// </summary>
public class ImageLoadingTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static LoopbackResponse Image(byte[] bytes, string contentType = "image/png")
        => LoopbackResponse.Raw(bytes, contentType);

    private static Task<LoopbackPage> PageWithImage(
        byte[] bytes,
        string contentType = "image/png",
        string markup = "<img id=\"a\" src=\"/a.img\">",
        Action<BrowserOptions>? configureBrowser = null)
        => LoopbackPage.CreateAsync(
            server => server
                .Map("/a.img", _ => Image(bytes, contentType))
                .MapHtml("/", "<!doctype html><html><body>" + markup + "</body></html>"),
            configureBrowser: configureBrowser);

    [TestCaseSource(nameof(Containers))]
    public async Task EveryContainerThisBrowserReadsAnswersItsIntrinsicSize(string name, byte[] bytes, string type, int width, int height)
    {
        _ = name;
        await using var loopback = await PageWithImage(bytes, type);
        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>(
            "[a.complete, a.naturalWidth, a.naturalHeight].join(':')"))
            .Should().Be("true:" + width + ":" + height);
        (await loopback.Page.EvaluateAsync<string>("a.currentSrc")).Should().Be(loopback.Url("/a.img"));
        loopback.Page.Errors.Should().BeEmpty();
    }

    private static IEnumerable<object[]> Containers()
    {
        yield return ["png", ImageBytes.Png(20, 10), "image/png", 20, 10];
        yield return ["jpeg", ImageBytes.Jpeg(30, 15), "image/jpeg", 30, 15];
        yield return ["gif", ImageBytes.Gif(40, 20), "image/gif", 40, 20];
        yield return ["webp-lossy", ImageBytes.WebPLossy(50, 25), "image/webp", 50, 25];
        yield return ["webp-lossless", ImageBytes.WebPLossless(60, 30), "image/webp", 60, 30];
        yield return ["webp-extended", ImageBytes.WebPExtended(70, 35), "image/webp", 70, 35];
        yield return ["bmp", ImageBytes.Bmp(80, 40), "image/bmp", 80, 40];
        yield return ["ico", ImageBytes.Icon(64, 64), "image/vnd.microsoft.icon", 64, 64];
        yield return ["svg", ImageBytes.Svg("width=\"90\" height=\"45\""), "image/svg+xml", 90, 45];

        // The format is sniffed, not read off Content-Type, which is what makes a blob store's default work.
        yield return ["png-as-octet-stream", ImageBytes.Png(11, 7), "application/octet-stream", 11, 7];
    }

    [Test]
    public async Task AContainerThisBrowserCannotReadIsBrokenAndFiresError()
    {
        await using var loopback = await PageWithImage(
            "not an image at all"u8.ToArray(),
            "image/png",
            """
            <img id="a" src="/a.img" onload="window.result = 'load'" onerror="window.result = 'error'">
            """);
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.WaitForIdleAsync(Timeout);

        (await loopback.Page.EvaluateAsync<string>("window.result")).Should().Be("error");

        // A broken request is complete — there is nothing left to wait for — and states no size, which is
        // the one thing a header reader can never make up. Its currentSrc still names the candidate that
        // was tried: HTML's failure arm sets the current URL to the selected source before firing error.
        (await loopback.Page.EvaluateAsync<string>(
            "[a.complete, a.naturalWidth, a.naturalHeight, a.currentSrc].join('|')"))
            .Should().Be("true|0|0|" + loopback.Url("/a.img"));
        loopback.Page.Errors.Should().ContainSingle(error =>
            error.Message.Contains("container format", StringComparison.Ordinal));
    }

    [Test]
    public async Task AnImageThatAnswersFourOhFourIsBrokenAndFiresError()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server.MapHtml("/", """
            <!doctype html><html><body>
            <img id="a" src="/missing.png" onerror="window.result = 'error:' + a.complete + ':' + a.naturalWidth">
            </body></html>
            """));
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.WaitForIdleAsync(Timeout);

        (await loopback.Page.EvaluateAsync<string>("window.result")).Should().Be("error:true:0");
        loopback.Page.Requests.Should().ContainSingle(request =>
            request.Url.EndsWith("/missing.png", StringComparison.Ordinal) && request.Status == 404);
    }

    [Test]
    public async Task ADataUrlImageIsAnsweredWithoutASocketAndWithoutARequestRow()
    {
        // Fetch §5.2's data URL processor is the one this repository has, and it is what a navigation and a
        // <script src="data:…"> already come through; an image reaches the same one.
        var png = Convert.ToBase64String(ImageBytes.Png(12, 6));

        await using var loopback = await LoopbackPage.CreateAsync(server => server.MapHtml("/", """
            <!doctype html><html><body>
            <img id="a" src="data:image/png;base64,PNG">
            </body></html>
            """.Replace("PNG", png, StringComparison.Ordinal)));
        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("[a.complete, a.naturalWidth, a.naturalHeight].join(':')"))
            .Should().Be("true:12:6");
        (await loopback.Page.EvaluateAsync<bool>("a.currentSrc.startsWith('data:image/png')")).Should().BeTrue();

        // A page that asked for nothing made no request, which is what about:blank and a data: script already do.
        loopback.Page.Requests.Should().ContainSingle(request => request.Initiator == RequestInitiator.Document);
        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnImageLoadArrivesAfterDomContentLoadedAndBeforeTheWindowsLoad()
    {
        // The order HTML gives: an image request delays the load event, so its own event is between the two.
        // It also has to reach a listener a *later* inline script installed, which is the commonest image
        // pattern there is and the one a synchronous parse-time fetch would otherwise lose.
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/a.png", _ => Image(ImageBytes.Png(3, 4)))
            .Map("/b.png", _ => Image(ImageBytes.Gif(5, 6), "image/gif"))
            .MapHtml("/", """
                <!doctype html><html><body>
                <img id="a" src="/a.png">
                <img id="b" src="/b.png">
                <script>
                  window.events = [];
                  document.addEventListener('DOMContentLoaded', () => events.push('dcl'));
                  window.addEventListener('load', () => events.push('load:' + a.complete + b.complete));
                  a.addEventListener('load', () => events.push('a:' + a.naturalWidth));
                  b.addEventListener('load', () => events.push('b:' + b.naturalWidth));
                </script>
                </body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.WaitForIdleAsync(Timeout);

        (await loopback.Page.EvaluateAsync<string>("events.join(',')"))
            .Should().Be("dcl,a:3,b:5,load:truetrue");
        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnImageWithNoSourceIsCompleteAndAnEmptyOneIsToo()
    {
        // https://html.spec.whatwg.org/multipage/embedded-content.html#dom-img-complete, first two
        // conditions: there is nothing to wait for, so a page must not wait for a load event.
        //
        // The third element is the documented gap. HTML's update-the-image-data selects no source for an
        // empty `srcset`, so the request is broken and `complete` is true; AngleSharp asks the resource
        // loader for nothing in that case, so there is no request processor to hang a state on and this
        // browser leaves it unavailable. Jint.Browser/Dom/divergences.md carries the row, and the same gap
        // is why `<img src="">` fires no error here.
        await using var loopback = await LoopbackPage.CreateAsync(server => server.MapHtml("/", """
            <!doctype html><html><body><img id="a"><img id="b" src=""><img id="c" srcset=""></body></html>
            """));
        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("[a.complete, b.complete, c.complete].join(':')"))
            .Should().Be("true:true:false");
        (await loopback.Page.EvaluateAsync<string>("[a.naturalWidth, a.currentSrc].join('|')")).Should().Be("0|");
    }

    [Test]
    public async Task AScriptCreatedImageLoadsAndItsEventArrivesAfterTheScript()
    {
        await using var loopback = await PageWithImage(ImageBytes.Png(9, 3), markup: "");
        await loopback.Page.NavigateAsync(loopback.Url("/"));

        await loopback.Page.EvaluateAsync("""
            window.events = [];
            window.img = new Image(120, 60);
            img.onload = () => events.push('load:' + img.naturalWidth + 'x' + img.naturalHeight);
            img.src = '/a.img';
            events.push('assigned');
            // A listener added after the assignment must still hear the queued element task.
            img.addEventListener('load', () => events.push('late'));
            Promise.resolve().then(() => events.push('microtask'));
            """);
        await loopback.Page.WaitForIdleAsync(Timeout);

        (await loopback.Page.EvaluateAsync<string>("events.join(',')"))
            .Should().Be("assigned,microtask,load:9x3,late");

        // new Image(w, h) sets the content attributes, and a content attribute is what width answers.
        (await loopback.Page.EvaluateAsync<string>("[img.width, img.height].join('x')")).Should().Be("120x60");
        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task WidthAndHeightFallBackToTheIntrinsicSizeAndTheContentAttributeWins()
    {
        // https://html.spec.whatwg.org/multipage/embedded-content.html#dom-dim-width — the getter is not
        // reflection. Before there was an intrinsic size it answered 0 for every image with no attribute,
        // which is what an image submit button's coordinates were blocked on (#3933).
        await using var loopback = await PageWithImage(
            ImageBytes.Png(200, 100),
            markup: """<img id="a" src="/a.img"><img id="b" src="/a.img" width="25"><img id="c">""");
        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("[a.width, a.height].join('x')")).Should().Be("200x100");
        (await loopback.Page.EvaluateAsync<string>("[b.width, b.height].join('x')")).Should().Be("25x100");
        (await loopback.Page.EvaluateAsync<string>("[c.width, c.height].join('x')")).Should().Be("0x0");
    }

    [Test]
    public async Task AnImageInputIsFetchedTooSoThatItHasDimensionsToBeSelectedWithin()
    {
        // HTML 4.10.5.1.20 gives <input type=image> an image request of its own, and 4.10.19.6 selects a
        // coordinate only within an available image the user agent displays (#3933).
        await using var loopback = await PageWithImage(
            ImageBytes.Png(64, 32),
            markup: """<form><input id="a" type="image" src="/a.img" name="go"></form>""");
        await loopback.Page.NavigateAsync(loopback.Url("/"));

        loopback.Page.Requests.Should().ContainSingle(request =>
            request.Url.EndsWith("/a.img", StringComparison.Ordinal) && request.NotFetchedReason == null);
        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task NoImageIsFetchedWhenTheCeilingIsZeroAndNothingIsFiredEither()
    {
        // Zero is the opt-out, and it has to be exactly what this browser did before it had an image model:
        // a reference in the request log, no socket, no event, and complete answering false.
        await using var loopback = await PageWithImage(
            ImageBytes.Png(20, 10),
            markup: """<img id="a" src="/a.img" onload="window.fired = 'load'" onerror="window.fired = 'error'">""",
            configureBrowser: options => options.MaxImageRequests = 0);
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.WaitForIdleAsync(Timeout);

        (await loopback.Page.EvaluateAsync<bool>("window.fired === undefined")).Should().BeTrue();
        (await loopback.Page.EvaluateAsync<bool>("a.complete")).Should().BeFalse();
        loopback.Page.Requests.Should().ContainSingle(request =>
            request.Url.EndsWith("/a.img", StringComparison.Ordinal)
            && request.NotFetchedReason!.Contains("MaxImageRequests is zero", StringComparison.Ordinal));
        loopback.Server.Received.Should().NotContain(request => request.Path == "/a.img");
        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task TheCeilingCountsRequestsOverTheWholeDocumentAndRefusesTheOnePastIt()
    {
        await using var loopback = await LoopbackPage.CreateAsync(
            server => server
                .Map("/a.png", _ => Image(ImageBytes.Png(1, 1)))
                .Map("/b.png", _ => Image(ImageBytes.Png(2, 2)))
                .MapHtml("/", """
                    <!doctype html><html><body>
                    <img id="a" src="/a.png"><img id="b" src="/b.png">
                    </body></html>
                    """),
            configureBrowser: options => options.MaxImageRequests = 1);

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("[a.complete, b.complete].join(':')")).Should().Be("true:false");
        loopback.Page.Requests.Should().ContainSingle(request =>
            request.Url.EndsWith("/b.png", StringComparison.Ordinal)
            && request.NotFetchedReason!.Contains("MaxImageRequests allows", StringComparison.Ordinal));
        loopback.Server.Received.Should().NotContain(request => request.Path == "/b.png");
    }

    [Test]
    public async Task AnImageOverTheSubresourceCeilingIsBrokenRatherThanRead()
    {
        await using var loopback = await PageWithImage(
            ImageBytes.Png(20, 10),
            markup: """<img id="a" src="/a.img" onerror="window.fired = 'error'">""",
            configureBrowser: options => options.MaxSubresourceBytes = 8);
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.WaitForIdleAsync(Timeout);

        (await loopback.Page.EvaluateAsync<string>("window.fired")).Should().Be("error");
        (await loopback.Page.EvaluateAsync<string>("[a.complete, a.naturalWidth].join(':')")).Should().Be("true:0");
    }

    [Test]
    public async Task ALazyImageLoadsEagerlyBecauseNothingCanKnowWhetherItIntersects()
    {
        // HTML defers a loading=lazy image until it is within the lazy load root's scrolling area, which is
        // a question about a layout this browser does not have. Never loading one would leave every image of
        // an infinite-scroll page complete === false for ever, which is the state those libraries block on.
        await using var loopback = await PageWithImage(
            ImageBytes.Png(16, 8),
            markup: """<img id="a" src="/a.img" loading="lazy">""");
        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("[a.loading, a.complete, a.naturalWidth].join(':')"))
            .Should().Be("lazy:true:16");
    }

    [Test]
    public async Task AnImageTheContextsUrlFilterRefusesIsBroken()
    {
        // The context's URL filter is the page's whole network position, and an image is inside it exactly
        // as a script and a style sheet are.
        await using var loopback = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/", """
                <!doctype html><html><body>
                <img id="a" src="http://127.0.0.1:1/blocked.png" onerror="window.fired = 'error'">
                </body></html>
                """));
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.WaitForIdleAsync(Timeout);

        (await loopback.Page.EvaluateAsync<string>("window.fired")).Should().Be("error");
        (await loopback.Page.EvaluateAsync<bool>("a.complete")).Should().BeTrue();
    }

    [Test]
    public async Task RewritingTheSourceStartsASecondRequestAndAnswersTheNewSize()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/a.png", _ => Image(ImageBytes.Png(11, 22)))
            .Map("/b.png", _ => Image(ImageBytes.Png(33, 44)))
            .MapHtml("/", """<!doctype html><html><body><img id="a" src="/a.png"></body></html>"""));
        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("[a.naturalWidth, a.naturalHeight].join('x')")).Should().Be("11x22");

        await loopback.Page.EvaluateAsync("a.src = '/b.png';");
        await loopback.Page.WaitForIdleAsync(Timeout);

        (await loopback.Page.EvaluateAsync<string>("[a.naturalWidth, a.naturalHeight].join('x')")).Should().Be("33x44");
        (await loopback.Page.EvaluateAsync<string>("a.currentSrc")).Should().Be(loopback.Url("/b.png"));
    }

    [Test]
    public async Task ACurrentSourceIsTheSelectedSourceAndNotWhereTheRedirectChainEnded()
    {
        // https://html.spec.whatwg.org/multipage/images.html#update-the-image-data — the image request's
        // current URL is `urlString`, the *selected source*, in the success arm and in the failure arm
        // alike, and never the response's. What currentSrc is for is saying which candidate a page ended
        // up on, so a redirect the fetch followed is not part of the answer.
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/moved.png", _ => LoopbackResponse.Redirect(302, "/real.png"))
            .Map("/real.png", _ => Image(ImageBytes.Png(5, 5)))
            .MapHtml("/", """<!doctype html><html><body><img id="a" src="/moved.png"></body></html>"""));
        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("a.currentSrc")).Should().Be(loopback.Url("/moved.png"));
        (await loopback.Page.EvaluateAsync<string>("a.src")).Should().Be(loopback.Url("/moved.png"));
        (await loopback.Page.EvaluateAsync<int>("a.naturalWidth")).Should().Be(5);
    }
}
