using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Parsing;

/// <summary>
/// HTML §4.8.4.3.6's source set: which candidate a <c>srcset</c>, a <c>sizes</c> and a
/// <c>&lt;picture&gt;</c>'s <c>&lt;source&gt;</c> elements actually select, and §4.8.4.2's
/// <c>img.decode()</c> over the result.
/// </summary>
public class ImageSourceSelectionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Four PNGs of distinct sizes at <c>/a.png</c>–<c>/d.png</c>, so a selection is readable off
    /// <c>naturalWidth</c> as well as off <c>currentSrc</c>.
    /// </summary>
    private static Task<LoopbackPage> PageWith(string markup, Action<BrowserOptions>? configureBrowser = null)
        => LoopbackPage.CreateAsync(
            server => server
                .Map("/a.png", _ => LoopbackResponse.Raw(ImageBytes.Png(1, 1), "image/png"))
                .Map("/b.png", _ => LoopbackResponse.Raw(ImageBytes.Png(2, 2), "image/png"))
                .Map("/c.png", _ => LoopbackResponse.Raw(ImageBytes.Png(3, 3), "image/png"))
                .Map("/d.png", _ => LoopbackResponse.Raw(ImageBytes.Png(4, 4), "image/png"))
                .MapHtml("/", "<!doctype html><html><body>" + markup + "</body></html>"),
            configureBrowser: configureBrowser);

    private static async Task<string?> SelectedAsync(LoopbackPage loopback)
    {
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.WaitForIdleAsync(Timeout);
        loopback.Page.Errors.Should().BeEmpty();
        return await loopback.Page.EvaluateAsync<string>("a.currentSrc.replace(/^.*\\//, '') + ':' + a.naturalWidth");
    }

    [Test]
    public async Task ADensityDescriptorIsReadRatherThanIgnored()
    {
        // The candidates are written 2x first, so document order and the descriptors disagree: AngleSharp's
        // SourceSet.GetCandidates yields them in order and never looks at a descriptor, which answered
        // b.png on a 1x device.
        await using var loopback = await PageWith("""<img id="a" srcset="/b.png 2x, /a.png 1x">""");
        (await SelectedAsync(loopback)).Should().Be("a.png:1");
    }

    [Test]
    public async Task TheEmulatedDeviceScaleFactorIsWhatADensityIsSelectedAgainst()
    {
        await using var loopback = await PageWith(
            """<img id="a" srcset="/a.png 1x, /b.png 2x, /c.png 3x">""",
            options => options.Viewport = new Viewport(1280, 720, 2));

        (await SelectedAsync(loopback)).Should().Be("b.png:2");
    }

    [Test]
    public async Task TheSmallestCandidateThatReachesTheDeviceRatioIsTaken()
    {
        // https://html.spec.whatwg.org/multipage/images.html#select-an-image-source leaves the choice
        // implementation-defined, and this browser makes the one every browser makes: round *up*, so a
        // candidate always covers the box it was chosen for. Rounding down would answer a.png here.
        await using var loopback = await PageWith("""<img id="a" srcset="/a.png 0.5x, /b.png 2x">""");
        (await SelectedAsync(loopback)).Should().Be("b.png:2");
    }

    [Test]
    public async Task WithNoCandidateReachingTheDeviceRatioTheLargestIsTaken()
    {
        // Nothing covers a 1x device, so the closest thing to it wins rather than the smallest asset there
        // is -- which is the same rule read from the other end.
        await using var loopback = await PageWith("""<img id="a" srcset="/a.png 0.25x, /b.png 0.5x">""");
        (await SelectedAsync(loopback)).Should().Be("b.png:2");
    }

    [Test]
    public async Task WithEveryCandidateAboveTheDeviceRatioTheSmallestOfThemWins()
    {
        // The "do not download the 3x asset on a 1x screen" half: both candidates are above the ratio, so
        // the smaller of the two is the one fetched.
        await using var loopback = await PageWith("""<img id="a" srcset="/b.png 2x, /c.png 3x">""");
        (await SelectedAsync(loopback)).Should().Be("b.png:2");
    }

    [TestCase("100px", "a.png:1")]
    [TestCase("400px", "b.png:2")]
    [TestCase("(min-width: 5000px) 100px, 400px", "b.png:2")]
    [TestCase("(min-width: 100px) 100px, 400px", "a.png:1")]
    public async Task AWidthDescriptorIsADensityAgainstTheSourceSizeSizesSelects(string sizes, string expected)
    {
        // 200w over a 100px source size is density 2 and over 400px is 0.5, so the same srcset selects
        // differently at the same device ratio. `sizes` was parsed and discarded before this.
        await using var loopback = await PageWith(
            """<img id="a" srcset="/a.png 200w, /b.png 400w" sizes="SIZES">""".Replace("SIZES", sizes, StringComparison.Ordinal));

        (await SelectedAsync(loopback)).Should().Be(expected);
    }

    [Test]
    public async Task WithNoSizesTheSourceSizeIsTheViewportWidth()
    {
        // HTML's default source size is 100vw, so at 1280 CSS pixels a 400w candidate is density 0.3125 and
        // a 2000w one is 1.5625. Only the second reaches a 1x device, and a page that writes those two
        // widths means exactly that: 400 pixels do not fill a 1280 pixel slot.
        await using var loopback = await PageWith("""<img id="a" srcset="/a.png 400w, /b.png 2000w">""");
        (await SelectedAsync(loopback)).Should().Be("b.png:2");
    }

    [Test]
    public async Task ASourcesMediaIsEvaluatedAgainstThePagesOwnEnvironment()
    {
        // AngleSharp never evaluates a <source media> at all, so the first <source> always won and a
        // desktop-only asset was selected on every viewport.
        await using var loopback = await PageWith("""
            <picture>
              <source media="(min-width: 5000px)" srcset="/c.png">
              <source media="(min-width: 100px)" srcset="/b.png">
              <img id="a" src="/a.png">
            </picture>
            """);

        (await SelectedAsync(loopback)).Should().Be("b.png:2");
    }

    [Test]
    public async Task ASourceWhoseTypeThisBrowserCannotReadIsSkipped()
    {
        // The declaration-side half of the same question ImageHeader answers about the bytes: a page that
        // offers AVIF first and PNG as the fallback gets the fallback.
        await using var loopback = await PageWith("""
            <picture>
              <source type="image/avif" srcset="/c.png">
              <source type="image/png" srcset="/b.png">
              <img id="a" src="/a.png">
            </picture>
            """);

        (await SelectedAsync(loopback)).Should().Be("b.png:2");
    }

    [Test]
    public async Task WithEverySourceRuledOutTheImgSrcIsTheFallback()
    {
        await using var loopback = await PageWith("""
            <picture>
              <source media="(min-width: 5000px)" srcset="/c.png">
              <source type="application/x-nothing" srcset="/d.png">
              <img id="a" src="/a.png">
            </picture>
            """);

        (await SelectedAsync(loopback)).Should().Be("a.png:1");
    }

    [Test]
    public async Task AnImgsOwnSrcsetIsConsultedBeforeItsSrc()
    {
        await using var loopback = await PageWith("""<img id="a" src="/a.png" srcset="/b.png 1x">""");
        (await SelectedAsync(loopback)).Should().Be("b.png:2");
    }

    [Test]
    public async Task AViewportEmulationMovesTheSelectionOnTheNextDocument()
    {
        // The media environment a <source media> is evaluated against is the same value matchMedia answers
        // from, so a client that emulates a narrow viewport selects the narrow asset.
        await using var loopback = await PageWith(
            """
            <picture>
              <source media="(min-width: 1000px)" srcset="/c.png">
              <img id="a" src="/a.png">
            </picture>
            """,
            options => options.Viewport = new Viewport(600, 800));

        (await SelectedAsync(loopback)).Should().Be("a.png:1");
    }

    [Test]
    public async Task DecodeResolvesForAnAvailableImageAndSettlesAfterTheScriptThatCalledIt()
    {
        await using var loopback = await PageWith("""<img id="a" src="/a.png">""");
        await loopback.Page.NavigateAsync(loopback.Url("/"));

        await loopback.Page.EvaluateAsync("""
            window.events = [];
            a.decode().then(v => events.push('resolved:' + (v === undefined)), e => events.push('rejected:' + e.name));
            events.push('called');
            """);
        await loopback.Page.WaitForIdleAsync(Timeout);

        // Step 2 queues a microtask before deciding anything, so nothing is settled inside the call.
        (await loopback.Page.EvaluateAsync<string>("events.join(',')")).Should().Be("called,resolved:true");
    }

    [Test]
    public async Task DecodeResolvesForAnImageWhoseSourceWasSetInTheSameScript()
    {
        // `const img = new Image(); img.src = u; await img.decode();` is what a lazy-loading library writes,
        // and it is the one shape that would break if the fetch a `src=` assignment starts did not finish
        // before the microtask decode() queues runs.
        await using var loopback = await PageWith("");
        await loopback.Page.NavigateAsync(loopback.Url("/"));

        await loopback.Page.EvaluateAsync("""
            window.result = '';
            const img = new Image();
            img.src = '/b.png';
            img.decode().then(() => result = 'resolved:' + img.naturalWidth, e => result = e.name);
            """);
        await loopback.Page.WaitForIdleAsync(Timeout);

        (await loopback.Page.EvaluateAsync<string>("result")).Should().Be("resolved:2");
    }

    [Test]
    public async Task DecodeRejectsWithAnEncodingErrorForABrokenImage()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/broken.png", _ => LoopbackResponse.Raw("not an image"u8.ToArray(), "image/png"))
            .MapHtml("/", """<!doctype html><html><body><img id="a" src="/broken.png"></body></html>"""));
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.WaitForIdleAsync(Timeout);

        await loopback.Page.EvaluateAsync("""
            window.result = '';
            a.decode().then(
              () => result = 'resolved',
              e => result = e.name + ':' + (e instanceof DOMException));
            """);
        await loopback.Page.WaitForIdleAsync(Timeout);

        (await loopback.Page.EvaluateAsync<string>("result")).Should().Be("EncodingError:true");
    }

    [Test]
    public async Task DecodeRejectsRatherThanHangingForAnImageThatNeverStartedARequest()
    {
        await using var loopback = await PageWith("");
        await loopback.Page.NavigateAsync(loopback.Url("/"));

        await loopback.Page.EvaluateAsync("""
            window.result = '';
            new Image().decode().then(() => result = 'resolved', e => result = e.name);
            """);
        await loopback.Page.WaitForIdleAsync(Timeout);

        (await loopback.Page.EvaluateAsync<string>("result")).Should().Be("EncodingError");
    }

    [Test]
    public async Task DecodeIsAnOperationOnThePrototypeAndAnswersAPromise()
    {
        await using var loopback = await PageWith("");
        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("""
            [typeof HTMLImageElement.prototype.decode,
             HTMLImageElement.prototype.decode.length,
             new Image().decode() instanceof Promise].join(':')
            """))
            .Should().Be("function:0:true");
    }
}
