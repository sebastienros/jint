using Jint.Browser;
using static AwesomeAssertions.FluentActions;
using Jint.Tests.Browser.DevTools;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Runtime;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>Page.EmulateMediaAsync</c> and the browser and context defaults beside it (#4621): the in-process half
/// of <c>Emulation.setEmulatedMedia</c>, over the same page state.
/// </summary>
/// <remarks>
/// Every assertion reads what a script or the cascade sees — <c>matchMedia</c>, a <c>change</c> listener,
/// <c>getComputedStyle</c> — rather than the emulation state itself, because an override the page is not
/// actually answered from is the failure this exists to catch.
/// </remarks>
public sealed class PageMediaEmulationTests
{
    private const string Probe =
        "JSON.stringify({ dark: matchMedia('(prefers-color-scheme: dark)').matches, reduce: matchMedia('(prefers-reduced-motion: reduce)').matches })";

    private const string ThemedPage =
        """
        <!doctype html>
        <style>
          p { color: rgb(1, 2, 3); animation-name: spin; outline-style: none; border-top-style: none; }
          @media (prefers-color-scheme: dark) { p { color: rgb(250, 250, 250); } }
          @media (prefers-reduced-motion: reduce) { p { animation-name: none; } }
          @media (forced-colors: active) { p { outline-style: solid; } }
          @media (prefers-contrast: more) { p { border-top-style: dashed; } }
          @media print { p { display: none; } }
        </style>
        <p id='p'>x</p>
        """;

    /// <summary>A bound on a popup that never arrives, never an assertion about how quickly one does.</summary>
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(30);

    /// <summary>The issue's own repro, with the call it asked for.</summary>
    [Test]
    public async Task TheIssueReproReportsTheEmulatedPreferences()
    {
        await using var browser = new Browser(new BrowserOptions());
        var page = await browser.NewPageAsync();
        await page.EmulateMediaAsync(new MediaEmulation { ReducedMotion = "reduce", ColorScheme = "dark" });
        await page.SetContentAsync("<!doctype html><p>x</p>", "http://127.0.0.1/");

        (await page.EvaluateAsync<string>(Probe)).Should().Be("""{"dark":true,"reduce":true}""");
        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// The loaded document moves: each list whose answer changed hears one <c>change</c>, a list whose
    /// answer did not stays quiet, and the task completes after the listeners ran.
    /// </summary>
    [Test]
    public async Task AChangeFiresForEveryListWhoseAnswerMovedBeforeTheTaskCompletes()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <script>
              window.log = [];
              for (const query of ['(prefers-color-scheme: dark)', '(prefers-reduced-motion: reduce)', 'print', '(min-width: 1px)']) {
                matchMedia(query).addEventListener('change', e => log.push(e.media + '=' + e.matches));
              }
            </script>
            """);

        await page.EmulateMediaAsync(new MediaEmulation { ColorScheme = "dark" });
        (await page.EvaluateAsync<string>("log.join('|')")).Should().Be("(prefers-color-scheme: dark)=true");

        await page.EmulateMediaAsync(new MediaEmulation { Media = "print", ReducedMotion = "reduce" });
        (await page.EvaluateAsync<string>("log.join('|')")).Should().Be(
            "(prefers-color-scheme: dark)=true|(prefers-reduced-motion: reduce)=true|print=true");

        // The empty string clears one override and leaves the others in force.
        await page.EmulateMediaAsync(new MediaEmulation { ColorScheme = "", Media = "" });
        (await page.EvaluateAsync<string>("log.slice(3).join('|')")).Should().Be(
            "(prefers-color-scheme: dark)=false|print=false");
        (await page.EvaluateAsync<string>(Probe)).Should().Be("""{"dark":false,"reduce":true}""");
        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// A later call that names one preference keeps the ones an earlier call set — Playwright's shape, and
    /// what a suite that emulates reduced motion in its set-up and a theme in its test body relies on.
    /// </summary>
    [Test]
    public async Task ACallThatNamesOnePreferenceKeepsTheOthers()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.EmulateMediaAsync(new MediaEmulation { ReducedMotion = "reduce" });
        await page.EmulateMediaAsync(new MediaEmulation { ColorScheme = "dark" });
        await page.EmulateMediaAsync(new MediaEmulation());

        (await page.EvaluateAsync<string>(Probe)).Should().Be("""{"dark":true,"reduce":true}""");

        await page.EmulateMediaAsync(new MediaEmulation { ColorScheme = "no-preference" });
        (await page.EvaluateAsync<string>(
            "[matchMedia('(prefers-color-scheme: dark)').matches, matchMedia('(prefers-color-scheme: light)').matches].join()"))
            .Should().Be("false,false", "no-preference is the scheme that matches neither");
    }

    /// <summary>
    /// <c>no-preference</c> matches neither scheme in style sheets either, so the two evaluators agree on the
    /// one value Media Queries 5 no longer defines.
    /// </summary>
    [Test]
    public async Task NoPreferenceMatchesNeitherSchemeInAStyleSheet()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <style>
              p { color: rgb(1, 2, 3); }
              @media (prefers-color-scheme: light) { p { color: rgb(9, 9, 9); } }
              @media (prefers-color-scheme: dark) { p { color: rgb(250, 250, 250); } }
            </style>
            <p id='p'>x</p>
            """);
        const string Color = "getComputedStyle(document.getElementById('p')).color";

        (await page.EvaluateAsync<string>(Color)).Should().Be("rgb(9, 9, 9)");
        await page.EmulateMediaAsync(new MediaEmulation { ColorScheme = "no-preference" });
        (await page.EvaluateAsync<string>(Color)).Should().Be("rgb(1, 2, 3)");
    }

    /// <summary>
    /// A call that changes nothing hands the page the environment it already has, so nothing is
    /// re-evaluated and the layout cache keyed on it survives — a suite that emulates in every set-up pays
    /// nothing for the repetition.
    /// </summary>
    [Test]
    public async Task ACallThatChangesNothingLeavesThePageEnvironmentAsItIs()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.EmulateMediaAsync(new MediaEmulation { ColorScheme = "dark", ReducedMotion = "reduce" });
        var before = await page.RunOnLoopAsync(engine => (object) global::Jint.Browser.Runtime.PageRuntime.Find(engine)!.Media);

        await page.EmulateMediaAsync(new MediaEmulation { ColorScheme = "dark" });
        await page.EmulateMediaAsync(new MediaEmulation { ReducedMotion = "reduce", Contrast = "" });

        var after = await page.RunOnLoopAsync(engine => (object) global::Jint.Browser.Runtime.PageRuntime.Find(engine)!.Media);
        after.Should().BeSameAs(before);
    }

    /// <summary>
    /// The host's defaults are a layer beneath the overrides, like an operating-system preference: clearing
    /// an override goes back to the browser's or context's value, not past it to the headless one.
    /// </summary>
    [Test]
    public async Task ClearingAnOverrideGoesBackToTheHostDefault()
    {
        await using var browser = new Browser(new BrowserOptions
        {
            MediaEmulation = new MediaEmulation { ColorScheme = "dark", Media = "print" },
        });
        var page = await browser.NewPageAsync();

        await page.EmulateMediaAsync(new MediaEmulation { ColorScheme = "light", Media = "screen" });
        (await page.EvaluateAsync<string>("[matchMedia('(prefers-color-scheme: dark)').matches, matchMedia('print').matches].join()"))
            .Should().Be("false,false");

        await page.EmulateMediaAsync(new MediaEmulation { ColorScheme = "", Media = "" });
        (await page.EvaluateAsync<string>("[matchMedia('(prefers-color-scheme: dark)').matches, matchMedia('print').matches].join()"))
            .Should().Be("true,true");
    }

    /// <summary>
    /// A protocol client's own <c>setEmulatedMedia</c> — Playwright sends one on every page with an empty
    /// value for each preference it is not emulating — overrides the host's defaults without erasing them.
    /// </summary>
    [Test]
    [NonParallelizable]
    public async Task AProtocolClientOverridesTheHostDefaultsWithoutErasingThem()
    {
        await using var session = await PageSession.CreateAsync(options: new BrowserOptions
        {
            MediaEmulation = new MediaEmulation { ColorScheme = "dark", ReducedMotion = "reduce" },
        });
        var page = await session.NewPageAsync();
        var attachment = await session.AttachAsync(await session.TargetForAsync(page));

        await session.ResultAsync(
            "Emulation.setEmulatedMedia",
            """{"media":"","features":[{"name":"prefers-color-scheme","value":""},{"name":"prefers-reduced-motion","value":""}]}""",
            attachment);
        (await page.EvaluateAsync<string>(Probe)).Should().Be("""{"dark":true,"reduce":true}""");

        await session.ResultAsync(
            "Emulation.setEmulatedMedia",
            """{"features":[{"name":"prefers-color-scheme","value":"light"}]}""",
            attachment);
        (await page.EvaluateAsync<string>(Probe)).Should().Be("""{"dark":false,"reduce":true}""");

        await session.ResultAsync("Emulation.setEmulatedMedia", """{"media":"","features":[]}""", attachment);
        (await page.EvaluateAsync<string>(Probe)).Should().Be("""{"dark":true,"reduce":true}""");
    }

    /// <summary>
    /// The style sheet half: <c>@media</c> rules are answered from the same environment, on the next style
    /// read, for the type and all four preferences.
    /// </summary>
    [Test]
    public async Task StyleSheetMediaRulesFollowTheEmulation()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(ThemedPage);
        const string Style =
            "(() => { const s = getComputedStyle(document.getElementById('p')); return [s.color, s.animationName, s.outlineStyle, s.borderTopStyle, s.display].join('|'); })()";

        (await page.EvaluateAsync<string>(Style)).Should().Be("rgb(1, 2, 3)|spin|none|none|block");

        await page.EmulateMediaAsync(new MediaEmulation
        {
            ColorScheme = "dark",
            ReducedMotion = "reduce",
            ForcedColors = "active",
            Contrast = "more",
        });
        (await page.EvaluateAsync<string>(Style)).Should().Be("rgb(250, 250, 250)|none|solid|dashed|block");

        await page.EmulateMediaAsync(new MediaEmulation { Media = "print" });
        (await page.EvaluateAsync<string>(Style)).Should().EndWith("|none");

        await page.EmulateMediaAsync(new MediaEmulation { Media = "", ColorScheme = "", ReducedMotion = "", ForcedColors = "", Contrast = "" });
        (await page.EvaluateAsync<string>(Style)).Should().Be("rgb(1, 2, 3)|spin|none|none|block");
        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// The override is the page's, so the next document parses in it: an inline script that measures during
    /// the parse sees the emulated values on every navigation after the call.
    /// </summary>
    [Test]
    public async Task TheEmulationSurvivesNavigationAndTheNextDocumentParsesInIt()
    {
        const string Measuring = "<!doctype html><script>window.atParse = " + Probe + ";</script>";
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/one", Measuring)
            .MapHtml("/two", Measuring));

        await fixture.Page.NavigateAsync(fixture.Url("/one"));
        (await fixture.Page.EvaluateAsync<string>("window.atParse")).Should().Be("""{"dark":false,"reduce":false}""");

        await fixture.Page.EmulateMediaAsync(new MediaEmulation { ColorScheme = "dark", ReducedMotion = "reduce" });

        await fixture.Page.NavigateAsync(fixture.Url("/two"));
        (await fixture.Page.EvaluateAsync<string>("window.atParse")).Should().Be("""{"dark":true,"reduce":true}""");

        await fixture.Page.NavigateAsync(fixture.Url("/one"));
        (await fixture.Page.EvaluateAsync<string>("window.atParse")).Should().Be("""{"dark":true,"reduce":true}""");
        fixture.Page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// <c>BrowserOptions.MediaEmulation</c> is in force before the first document parses — the blank one a
    /// page opens on and the first one it is given.
    /// </summary>
    [Test]
    public async Task BrowserDefaultsAreInForceForThePageFirstDocument()
    {
        await using var browser = new Browser(new BrowserOptions
        {
            MediaEmulation = new MediaEmulation { ColorScheme = "dark", ReducedMotion = "reduce" },
        });
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<string>(Probe)).Should().Be("""{"dark":true,"reduce":true}""", "the blank document is already emulated");

        await page.SetContentAsync("<!doctype html><script>window.atParse = " + Probe + ";</script>" + ThemedPage);
        (await page.EvaluateAsync<string>("window.atParse")).Should().Be("""{"dark":true,"reduce":true}""");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('p')).color")).Should().Be("rgb(250, 250, 250)");

        // A page-level call still moves one page from there.
        await page.EmulateMediaAsync(new MediaEmulation { ColorScheme = "light" });
        (await page.EvaluateAsync<string>(Probe)).Should().Be("""{"dark":false,"reduce":true}""");
        var sibling = await browser.NewPageAsync();
        (await sibling.EvaluateAsync<string>(Probe)).Should().Be("""{"dark":true,"reduce":true}""", "another page keeps the browser's defaults");
    }

    /// <summary>
    /// A context's defaults are applied over the browser's property by property: a null keeps the browser's
    /// value, an empty string clears it, and the default context is untouched.
    /// </summary>
    [Test]
    public async Task ContextDefaultsApplyOverTheBrowserDefaultsPropertyByProperty()
    {
        await using var browser = new Browser(new BrowserOptions
        {
            MediaEmulation = new MediaEmulation { ColorScheme = "dark", ReducedMotion = "reduce" },
        });
        var context = await browser.NewContextAsync(new BrowserContextOptions
        {
            MediaEmulation = new MediaEmulation { ColorScheme = "", Contrast = "more" },
        });

        var page = await context.NewPageAsync();
        (await page.EvaluateAsync<string>(Probe)).Should().Be("""{"dark":false,"reduce":true}""");
        (await page.EvaluateAsync<bool>("matchMedia('(prefers-contrast: more)').matches")).Should().BeTrue();

        var outside = await browser.NewPageAsync();
        (await outside.EvaluateAsync<string>(Probe)).Should().Be("""{"dark":true,"reduce":true}""");
        (await outside.EvaluateAsync<bool>("matchMedia('(prefers-contrast: more)').matches")).Should().BeFalse();
    }

    /// <summary>A popup is a page of its context, so it opens in the context's emulated media too.</summary>
    [Test]
    public async Task APopupOpensInItsContextDefaults()
    {
        const string Measuring = "<!doctype html><title>popup</title><script>window.atParse = " + Probe + ";</script>";
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/", "<title>opener</title>").MapHtml("/popup", Measuring),
            context => context.MediaEmulation = new MediaEmulation { ColorScheme = "dark" });
        await fixture.Page.NavigateAsync(fixture.Url("/"));

        var opened = new TaskCompletionSource<Page>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Page.Popup += (_, popup) => opened.TrySetResult(popup);
        await fixture.Page.EvaluateAsync("window.w = open('/popup'); void 0");
        var popup = await opened.Task.WaitAsync(Ceiling);

        (await popup.WaitForAsync("window.atParse !== undefined", Ceiling)).Should().BeTrue();
        (await popup.EvaluateAsync<string>("window.atParse")).Should().Be("""{"dark":true,"reduce":false}""");
    }

    /// <summary>
    /// The in-process call and the protocol command write one state: a feature a client emulated that the
    /// call does not name survives it, and the call is visible to the client's own evaluation.
    /// </summary>
    [Test]
    [NonParallelizable]
    public async Task TheProtocolCommandAndThePageMemberShareOneState()
    {
        await using var session = await PageSession.CreateAsync();
        var page = await session.NewPageAsync();
        var attachment = await session.AttachAsync(await session.TargetForAsync(page));

        await session.ResultAsync(
            "Emulation.setEmulatedMedia",
            """{"media":"print","features":[{"name":"prefers-reduced-transparency","value":"reduce"}]}""",
            attachment);
        await page.EmulateMediaAsync(new MediaEmulation { ColorScheme = "dark" });

        (await page.EvaluateAsync<string>(
            "[matchMedia('print').matches, matchMedia('(prefers-reduced-transparency: reduce)').matches, matchMedia('(prefers-color-scheme: dark)').matches].join()"))
            .Should().Be("true,true,true");
        (await session.EvaluateAsync("matchMedia('(prefers-color-scheme: dark)').matches", attachment))
            .GetProperty("value").GetBoolean().Should().BeTrue();

        // And the protocol's command still replaces the whole list, as Chrome's does.
        await session.ResultAsync("Emulation.setEmulatedMedia", """{"media":"","features":[]}""", attachment);
        (await page.EvaluateAsync<string>(Probe)).Should().Be("""{"dark":false,"reduce":false}""");
    }

    /// <summary>A keyword is validated where it is written, and canonicalized to the lower case CSS writes.</summary>
    [Test]
    public void AKeywordIsValidatedWhenItIsAssigned()
    {
        new MediaEmulation { ColorScheme = "DARK", Media = "Print", Contrast = "Less" }
            .Should().BeEquivalentTo(new { ColorScheme = "dark", Media = "print", Contrast = "less" });

        Invoking(() => new MediaEmulation { ColorScheme = "drak" })
            .Should().Throw<ArgumentException>().WithParameterName("ColorScheme");
        Invoking(() => new MediaEmulation { Media = "tv" })
            .Should().Throw<ArgumentException>().WithParameterName("Media");
        Invoking(() => new MediaEmulation { ReducedMotion = "yes" })
            .Should().Throw<ArgumentException>().WithParameterName("ReducedMotion");
        Invoking(() => new MediaEmulation { ForcedColors = "on" })
            .Should().Throw<ArgumentException>().WithParameterName("ForcedColors");
        Invoking(() => new MediaEmulation { Contrast = "high" })
            .Should().Throw<ArgumentException>().WithParameterName("Contrast");
    }

    /// <summary>A closed page refuses the call rather than leaving it queued.</summary>
    [Test]
    public async Task AClosedPageRefusesTheCall()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.CloseAsync();

        await Awaiting(() => page.EmulateMediaAsync(new MediaEmulation { ColorScheme = "dark" }))
            .Should().ThrowAsync<ObjectDisposedException>();
        await Awaiting(() => page.EmulateMediaAsync(null!)).Should().ThrowAsync<ArgumentNullException>();
    }
}
