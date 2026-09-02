using Jint.Browser;
using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.Views;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>matchMedia</c>'s <c>change</c> event: what a page hears when the viewport moves under it.
/// </summary>
/// <remarks>
/// The viewport is set through the internal runtime seam rather than through a public member, because device
/// emulation (campaign item C5) is what will own the public one. That seam is the whole of "the window
/// resized" in a browser with no window.
/// </remarks>
public sealed class MediaQueryChangeTests
{
    private static Task SetViewportAsync(Page page, Viewport viewport)
        => page.RunOnLoopAsync(engine =>
        {
            PageRuntime.Find(engine)!.SetViewport(viewport);
            return 0;
        });

    [Test]
    public async Task AListWhoseAnswerMovedFiresChangeAndOneWhoseDidNotStaysQuiet()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              window.log = [];
              window.narrow = matchMedia('(max-width: 600px)');
              window.wide = matchMedia('(min-width: 100px)');
              window.narrow.addEventListener('change', e => window.log.push('narrow:' + e.matches + ':' + e.media));
              window.wide.addEventListener('change', () => window.log.push('wide'));
              window.before = window.narrow.matches;
            </script>
            """);

        (await page.EvaluateAsync<bool>("window.before")).Should().BeFalse();

        await SetViewportAsync(page, new Viewport(480, 800));

        (await page.EvaluateAsync<bool>("window.narrow.matches")).Should().BeTrue();
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("narrow:true:(max-width: 600px)");
        (await page.EvaluateAsync<int>("innerWidth")).Should().Be(480);
    }

    [Test]
    public async Task TheOnchangeAttributeIsFiredToo()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              window.seen = null;
              window.list = matchMedia('(min-width: 1000px)');
              window.list.onchange = e => { window.seen = [e.type, e.matches, e.isTrusted, e instanceof MediaQueryListEvent, e instanceof Event].join('|') };
            </script>
            """);

        await SetViewportAsync(page, new Viewport(320, 640));

        (await page.EvaluateAsync<string>("window.seen")).Should().Be("change|false|true|true|true");
    }

    [Test]
    public async Task TheLegacyAddListenerAliasHearsItAsWell()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              window.hits = 0;
              window.list = matchMedia('(orientation: landscape)');
              window.list.addListener(() => { window.hits++ });
            </script>
            """);

        // 1280x720 is landscape; a taller-than-wide viewport is not.
        await SetViewportAsync(page, new Viewport(400, 900));
        (await page.EvaluateAsync<int>("window.hits")).Should().Be(1);
        (await page.EvaluateAsync<bool>("window.list.matches")).Should().BeFalse();

        await SetViewportAsync(page, new Viewport(900, 400));
        (await page.EvaluateAsync<int>("window.hits")).Should().Be(2);
        (await page.EvaluateAsync<bool>("window.list.matches")).Should().BeTrue();
    }

    [Test]
    public async Task SettingTheSameViewportFiresNothing()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              window.hits = 0;
              window.list = matchMedia('(max-width: 600px)');
              window.list.addEventListener('change', () => { window.hits++ });
            </script>
            """);

        await SetViewportAsync(page, new Viewport(1280, 720, 1));
        (await page.EvaluateAsync<int>("window.hits")).Should().Be(0);

        // And a change that leaves this query's answer alone is not this list's business either.
        await SetViewportAsync(page, new Viewport(1400, 900));
        (await page.EvaluateAsync<int>("window.hits")).Should().Be(0);
    }

    [Test]
    public async Task ThePreferenceFeaturesAnswerWhatAHeadlessPageReports()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<bool>("matchMedia('(prefers-color-scheme: light)').matches")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("matchMedia('(prefers-color-scheme: dark)').matches")).Should().BeFalse();
        (await page.EvaluateAsync<bool>("matchMedia('(prefers-reduced-motion: no-preference)').matches")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("matchMedia('(prefers-reduced-motion: reduce)').matches")).Should().BeFalse();
        (await page.EvaluateAsync<bool>("matchMedia('(hover: hover)').matches")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("matchMedia('(any-hover: hover)').matches")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("matchMedia('(pointer: fine)').matches")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("matchMedia('(pointer: coarse)').matches")).Should().BeFalse();
        (await page.EvaluateAsync<bool>("matchMedia('(orientation: landscape)').matches")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("matchMedia('(orientation: portrait)').matches")).Should().BeFalse();
    }
}
