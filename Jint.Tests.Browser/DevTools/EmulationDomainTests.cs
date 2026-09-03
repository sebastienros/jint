using System.Text.Json;
using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// Every <c>Emulation</c> command, and what the page can see of it afterwards.
/// </summary>
/// <remarks>
/// <para>
/// The question each of these asks is the one that separates an override a client's bookkeeping merely holds
/// from one the page is actually running under: after the command, what does a script read? So every
/// assertion goes through <c>Runtime.evaluate</c> on the attachment rather than through the domain's own
/// state.
/// </para>
/// <para>
/// The overrides that reach the <i>next</i> document — the time zone, the locale, script execution — are
/// tested across a <c>Page.setDocumentContent</c>, which builds an engine exactly as a navigation does. That
/// is the boundary they are documented to take effect at, so a test that set one and read it back without
/// crossing it would be asserting nothing.
/// </para>
/// </remarks>
[NonParallelizable]
public class EmulationDomainTests
{
    [Test]
    public async Task DeviceMetricsMoveTheWindowAndClearingThemPutsItBack()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await session.ResultAsync(
            "Emulation.setDeviceMetricsOverride",
            """{"width":390,"height":844,"deviceScaleFactor":3,"mobile":true}""",
            attachment);

        (await Number(session, attachment, "innerWidth")).Should().Be(390);
        (await Number(session, attachment, "innerHeight")).Should().Be(844);
        (await Number(session, attachment, "devicePixelRatio")).Should().Be(3);
        (await Number(session, attachment, "screen.width")).Should().Be(390);
        (await Flag(session, attachment, "matchMedia('(max-width: 600px)').matches")).Should().BeTrue();
        (await Flag(session, attachment, "matchMedia('(orientation: portrait)').matches")).Should().BeTrue();

        await session.ResultAsync("Emulation.clearDeviceMetricsOverride", null, attachment);

        (await Number(session, attachment, "innerWidth")).Should().Be(1280);
        (await Number(session, attachment, "devicePixelRatio")).Should().Be(1);
        (await Flag(session, attachment, "matchMedia('(max-width: 600px)').matches")).Should().BeFalse();
    }

    /// <summary>
    /// The metrics override moves the cascade too, because the render device is a live view of the viewport.
    /// </summary>
    /// <remarks>
    /// <c>Runtime/PageRenderDevice</c> holds no numbers of its own, so a percentage and an <c>@media</c>
    /// dimension query in a style sheet answer from the same viewport <c>matchMedia</c> does — at the
    /// moment the cascade asks, with nothing to re-register on a document whose browsing context was built
    /// before the override arrived (<see href="https://github.com/sebastienros/jint/issues/3721">#3721</see>).
    /// </remarks>
    [Test]
    public async Task DeviceMetricsMoveWhatTheCascadeSaysAsWellAsWhatMatchMediaDoes()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await SetContentAsync(
            session,
            attachment,
            """
            <style>
              #t { width: 50%; position: relative }
              @media (max-width: 600px) { #t { position: absolute } }
            </style>
            <div id="t">g</div>
            """);

        var width = "getComputedStyle(document.getElementById('t')).width";
        var position = "getComputedStyle(document.getElementById('t')).position";

        (await Text(session, attachment, width)).Should().Be("640px", "half of the 1280px the page opened with");
        (await Text(session, attachment, position)).Should().Be("relative");
        (await Flag(session, attachment, "matchMedia('(max-width: 600px)').matches")).Should().BeFalse();

        await session.ResultAsync(
            "Emulation.setDeviceMetricsOverride",
            """{"width":390,"height":844,"deviceScaleFactor":3,"mobile":true}""",
            attachment);

        (await Text(session, attachment, width)).Should().Be("195px", "the same document, against the emulated viewport");
        (await Text(session, attachment, position)).Should().Be("absolute", "the @media rule is active now");
        (await Flag(session, attachment, "matchMedia('(max-width: 600px)').matches")).Should().BeTrue();
    }

    [Test]
    public async Task SetVisibleSizeResizesTheViewportAndKeepsTheDeviceScaleFactor()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await session.ResultAsync(
            "Emulation.setDeviceMetricsOverride",
            """{"width":800,"height":600,"deviceScaleFactor":2,"mobile":false}""",
            attachment);

        await session.ResultAsync("Emulation.setVisibleSize", """{"width":500,"height":400}""", attachment);

        (await Number(session, attachment, "innerWidth")).Should().Be(500);
        (await Number(session, attachment, "innerHeight")).Should().Be(400);
        (await Number(session, attachment, "devicePixelRatio")).Should().Be(2);
    }

    [Test]
    public async Task AnEmulatedPreferenceFlipsMatchMediaAndFiresChange()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await session.EvaluateAsync(
            """
            window.log = [];
            window.dark = matchMedia('(prefers-color-scheme: dark)');
            window.dark.addEventListener('change', e => window.log.push(e.matches + ':' + e.media));
            window.motion = matchMedia('(prefers-reduced-motion: reduce)');
            window.motion.addEventListener('change', () => window.log.push('motion'));
            window.dark.matches
            """,
            attachment);

        (await Flag(session, attachment, "window.dark.matches")).Should().BeFalse("a page with no user reports the light scheme");

        await session.ResultAsync(
            "Emulation.setEmulatedMedia",
            """{"media":"","features":[{"name":"prefers-color-scheme","value":"dark"}]}""",
            attachment);

        (await Flag(session, attachment, "window.dark.matches")).Should().BeTrue();
        (await Flag(session, attachment, "matchMedia('(prefers-color-scheme: light)').matches")).Should().BeFalse();
        (await Text(session, attachment, "window.log.join('|')"))
            .Should().Be("true:(prefers-color-scheme: dark)", "only a list whose own answer moved hears change");

        // And an emulation that takes the preference away puts the page back and fires the other way.
        await session.ResultAsync("Emulation.setEmulatedMedia", """{"media":"","features":[]}""", attachment);

        (await Flag(session, attachment, "window.dark.matches")).Should().BeFalse();
        (await Text(session, attachment, "window.log.join('|')"))
            .Should().Be("true:(prefers-color-scheme: dark)|false:(prefers-color-scheme: dark)");
    }

    [Test]
    public async Task EveryLevelFivePreferenceFeatureIsEmulatable()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await session.ResultAsync(
            "Emulation.setEmulatedMedia",
            """
            {"media":"","features":[
              {"name":"prefers-reduced-motion","value":"reduce"},
              {"name":"prefers-contrast","value":"more"},
              {"name":"forced-colors","value":"active"},
              {"name":"color-gamut","value":"p3"}
            ]}
            """,
            attachment);

        (await Flag(session, attachment, "matchMedia('(prefers-reduced-motion: reduce)').matches")).Should().BeTrue();
        (await Flag(session, attachment, "matchMedia('(prefers-reduced-motion)').matches")).Should().BeTrue();
        (await Flag(session, attachment, "matchMedia('(prefers-contrast: more)').matches")).Should().BeTrue();
        (await Flag(session, attachment, "matchMedia('(forced-colors: active)').matches")).Should().BeTrue();
        (await Flag(session, attachment, "matchMedia('(forced-colors)').matches")).Should().BeTrue();

        // color-gamut is an "at least" comparison: a P3 display can do sRGB.
        (await Flag(session, attachment, "matchMedia('(color-gamut: srgb)').matches")).Should().BeTrue();
        (await Flag(session, attachment, "matchMedia('(color-gamut: p3)').matches")).Should().BeTrue();
        (await Flag(session, attachment, "matchMedia('(color-gamut: rec2020)').matches")).Should().BeFalse();
    }

    [Test]
    public async Task TheEmulatedMediaTypeIsWhatABareTypeInAQueryIsMatchedAgainst()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        (await Flag(session, attachment, "matchMedia('screen').matches")).Should().BeTrue();
        (await Flag(session, attachment, "matchMedia('print').matches")).Should().BeFalse();

        await session.ResultAsync("Emulation.setEmulatedMedia", """{"media":"print"}""", attachment);

        (await Flag(session, attachment, "matchMedia('print').matches")).Should().BeTrue();
        (await Flag(session, attachment, "matchMedia('screen').matches")).Should().BeFalse();
        (await Flag(session, attachment, "matchMedia('all').matches")).Should().BeTrue();
        (await Flag(session, attachment, "matchMedia('print and (min-width: 100px)').matches")).Should().BeTrue();

        await session.ResultAsync("Emulation.setEmulatedMedia", """{"media":""}""", attachment);

        (await Flag(session, attachment, "matchMedia('screen').matches")).Should().BeTrue();
    }

    [Test]
    public async Task TouchEmulationReachesTheNavigatorTheHandlerAndTheMediaEnvironment()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        (await Number(session, attachment, "navigator.maxTouchPoints")).Should().Be(0);
        (await Flag(session, attachment, "'ontouchstart' in window")).Should().BeFalse();

        await session.ResultAsync(
            "Emulation.setTouchEmulationEnabled",
            """{"enabled":true,"maxTouchPoints":5}""",
            attachment);

        (await Number(session, attachment, "navigator.maxTouchPoints")).Should().Be(5);
        (await Flag(session, attachment, "'ontouchstart' in window")).Should().BeTrue();
        (await Flag(session, attachment, "'ontouchstart' in document")).Should().BeTrue();
        (await Flag(session, attachment, "matchMedia('(pointer: coarse)').matches")).Should().BeTrue();
        (await Flag(session, attachment, "matchMedia('(hover: none)').matches")).Should().BeTrue();
        (await Flag(session, attachment, "matchMedia('(hover)').matches")).Should().BeFalse();

        await session.ResultAsync("Emulation.setTouchEmulationEnabled", """{"enabled":false}""", attachment);

        (await Number(session, attachment, "navigator.maxTouchPoints")).Should().Be(0);
        (await Flag(session, attachment, "'ontouchstart' in window")).Should().BeFalse();
        (await Flag(session, attachment, "matchMedia('(pointer: fine)').matches")).Should().BeTrue();
    }

    [Test]
    public async Task FocusEmulationDecidesWhetherTheDocumentIsFocusedAndVisible()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await session.EvaluateAsync(
            "window.visibility = []; document.addEventListener('visibilitychange', () => window.visibility.push(document.visibilityState)); 1",
            attachment);

        (await Flag(session, attachment, "document.hasFocus()")).Should().BeTrue();
        (await Text(session, attachment, "document.visibilityState")).Should().Be("visible");
        (await Flag(session, attachment, "document.hidden")).Should().BeFalse();

        await session.ResultAsync("Emulation.setFocusEmulationEnabled", """{"enabled":false}""", attachment);

        (await Flag(session, attachment, "document.hasFocus()")).Should().BeFalse();
        (await Text(session, attachment, "document.visibilityState")).Should().Be("hidden");
        (await Flag(session, attachment, "document.hidden")).Should().BeTrue();
        (await Text(session, attachment, "window.visibility.join('|')")).Should().Be("hidden");

        await session.ResultAsync("Emulation.setFocusEmulationEnabled", """{"enabled":true}""", attachment);

        (await Flag(session, attachment, "document.hasFocus()")).Should().BeTrue();
        (await Text(session, attachment, "window.visibility.join('|')")).Should().Be("hidden|visible");
    }

    [Test]
    public async Task TheUserAgentOverrideIsWhatTheNavigatorAnswers()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        (await Text(session, attachment, "navigator.userAgent")).Should().Contain("Jint.Browser");
        (await Text(session, attachment, "navigator.platform")).Should().BeEmpty();

        await session.ResultAsync(
            "Emulation.setUserAgentOverride",
            """{"userAgent":"Pretend/1.0","acceptLanguage":"fr-CA,fr;q=0.9,en;q=0.8","platform":"MacIntel"}""",
            attachment);

        (await Text(session, attachment, "navigator.userAgent")).Should().Be("Pretend/1.0");
        (await Text(session, attachment, "navigator.language")).Should().Be("fr-CA");
        (await Text(session, attachment, "navigator.languages.join('|')")).Should().Be("fr-CA|fr|en");
        (await Text(session, attachment, "navigator.platform")).Should().Be("MacIntel");
    }

    /// <summary>
    /// The two commands that set the user agent set one thing, and the page and the wire read it together.
    /// </summary>
    /// <remarks>
    /// Chrome treats <c>Emulation.setUserAgentOverride</c> and <c>Network.setUserAgentOverride</c> as one
    /// override, and here they are one field of the page's own emulation state — so a client that used either
    /// gets a <c>navigator.userAgent</c> and a <c>User-Agent</c> header that cannot disagree. This is the
    /// half a second copy of the value would silently break.
    /// </remarks>
    [Test]
    public async Task EitherUserAgentCommandMovesBothTheNavigatorAndTheHeader()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var session = await PageSession.CreateAsync(new BrowserContextOptions { UrlFilter = server.Owns });
        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);
        var attachment = await session.AttachAsync(target);
        await session.EnablePageAsync(attachment);

        // Set through Network, read through the navigator.
        await session.ResultAsync(
            "Network.setUserAgentOverride",
            """{"userAgent":"ViaNetwork/1.0","acceptLanguage":"pt-BR"}""",
            attachment);

        (await Text(session, attachment, "navigator.userAgent")).Should().Be("ViaNetwork/1.0");
        (await Text(session, attachment, "navigator.language")).Should().Be("pt-BR");

        // Set through Emulation, read off the wire.
        await session.ResultAsync(
            "Emulation.setUserAgentOverride",
            """{"userAgent":"ViaEmulation/2.0","acceptLanguage":"nl-NL"}""",
            attachment);

        await page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });

        var request = server.Received.Single(received => received.Path == "/page");
        request.Header("User-Agent").Should().Be("ViaEmulation/2.0");
        request.Header("Accept-Language").Should().Be("nl-NL");

        (await Text(session, attachment, "navigator.userAgent")).Should().Be("ViaEmulation/2.0");
    }

    /// <summary>
    /// A page a client attached to but never overrode still names itself: the document's own request and a
    /// <c>fetch</c> the page makes both carry <see cref="BrowserOptions.UserAgent"/>, and an override set
    /// afterwards moves the next request without a reload.
    /// </summary>
    /// <remarks>
    /// The two halves are two mechanisms — <c>Options.WebApi.Fetch.UserAgent</c>, fixed when this document's
    /// engine was built, and <c>PageNetworkPolicy.Apply</c> rewriting the header per hop — and before #3720
    /// the first of them did not exist, so a page nobody had overridden sent no <c>User-Agent</c> at all.
    /// </remarks>
    [Test]
    public async Task WithNoOverrideThePagesOwnUserAgentIsWhatEveryRequestCarries()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><body>ok</body></html>");
        server.Map("/data.json", _ => LoopbackResponse.Json("{\"ok\":true}"));

        await using var session = await PageSession.CreateAsync(new BrowserContextOptions { UrlFilter = server.Owns });
        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);
        var attachment = await session.AttachAsync(target);
        await session.EnablePageAsync(attachment);

        await page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await page.EvaluateAsync("window.first = false; fetch('/data.json').then(() => { window.first = true; });");
        (await page.WaitForAsync("window.first === true", TimeSpan.FromSeconds(10))).Should().BeTrue();

        var expected = await page.EvaluateAsync<string>("navigator.userAgent");
        expected.Should().Contain("Jint.Browser");
        server.Received.Single(received => received.Path == "/page").Header("User-Agent").Should().Be(expected);
        server.Received.Single(received => received.Path == "/data.json").Header("User-Agent").Should().Be(expected);

        // An override reaches the document that is already loaded, which is what makes it an override.
        await session.ResultAsync(
            "Emulation.setUserAgentOverride",
            "{\"userAgent\":\"Late/3.0\"}",
            attachment);

        await page.EvaluateAsync("window.second = false; fetch('/data.json?again').then(() => { window.second = true; });");
        (await page.WaitForAsync("window.second === true", TimeSpan.FromSeconds(10))).Should().BeTrue();

        server.Received.Single(received => received.Query == "again").Header("User-Agent").Should().Be("Late/3.0");
        (await Text(session, attachment, "navigator.userAgent")).Should().Be("Late/3.0");
    }

    [Test]
    public async Task TheTimeZoneOverrideReachesTheNextDocument()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await session.ResultAsync("Emulation.setTimezoneOverride", """{"timezoneId":"Asia/Tokyo"}""", attachment);
        await SetContentAsync(session, attachment, "<html><body>tokyo</body></html>");

        // Japan is UTC+9 all year and observes no daylight saving, so the offset is exactly -540 minutes.
        (await Number(session, attachment, "new Date().getTimezoneOffset()")).Should().Be(-540);

        await session.ResultAsync("Emulation.setTimezoneOverride", """{"timezoneId":"UTC"}""", attachment);
        await SetContentAsync(session, attachment, "<html><body>utc</body></html>");

        (await Number(session, attachment, "new Date().getTimezoneOffset()")).Should().Be(0);
    }

    [Test]
    public async Task ATimeZoneNothingKnowsIsRefusedInChromesWording()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        var error = await session.ErrorAsync("Emulation.setTimezoneOverride", """{"timezoneId":"Mars/Olympus"}""", attachment);

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Contain("Invalid timezone id");
    }

    [Test]
    public async Task TheLocaleOverrideReachesIntlAndTheNavigatorOnTheNextDocument()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await session.ResultAsync("Emulation.setLocaleOverride", """{"locale":"de-DE"}""", attachment);
        await SetContentAsync(session, attachment, "<html><body>de</body></html>");

        (await Text(session, attachment, "new Intl.NumberFormat().resolvedOptions().locale")).Should().StartWith("de");
        (await Text(session, attachment, "navigator.language")).Should().Be("de-DE");
        (await Text(session, attachment, "navigator.languages.join('|')")).Should().Be("de-DE");
    }

    [Test]
    public async Task ALocaleNothingKnowsIsRefused()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        var error = await session.ErrorAsync("Emulation.setLocaleOverride", """{"locale":"not@a@locale"}""", attachment);
        error.GetProperty("message").GetString().Should().Contain("Invalid locale");
    }

    [Test]
    public async Task DisablingScriptExecutionStopsTheNextDocumentsScriptsAndNotRuntimeEvaluate()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await session.ResultAsync("Emulation.setScriptExecutionDisabled", """{"value":true}""", attachment);
        await SetContentAsync(
            session,
            attachment,
            "<html><body><script>window.ran = true;</script><button onclick=\"window.clicked = true\">go</button></body></html>");

        (await Text(session, attachment, "typeof window.ran")).Should().Be("undefined", "the document's own script did not run");
        (await Text(session, attachment, "typeof document.querySelector('button').onclick"))
            .Should().Be("object", "a handler content attribute is not compiled either");
        (await Number(session, attachment, "2 + 2")).Should().Be(4, "Runtime.evaluate is unaffected, which is Chrome's behaviour");
        (await Flag(session, attachment, "matchMedia('(scripting: none)').matches")).Should().BeTrue();

        await session.ResultAsync("Emulation.setScriptExecutionDisabled", """{"value":false}""", attachment);
        await SetContentAsync(session, attachment, "<html><body><script>window.ran = true;</script></body></html>");

        (await Flag(session, attachment, "window.ran === true")).Should().BeTrue();
        (await Flag(session, attachment, "matchMedia('(scripting: enabled)').matches")).Should().BeTrue();
    }

    [Test]
    public async Task GeolocationAnswersTheOverrideAndAnErrorWithoutOne()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        (await Text(session, attachment, "typeof navigator.geolocation.getCurrentPosition")).Should().Be("function");

        await session.EvaluateAsync(
            """
            window.fix = null;
            window.failure = null;
            navigator.geolocation.getCurrentPosition(p => { window.fix = p }, e => { window.failure = e.code });
            1
            """,
            attachment);

        await Drain(session, attachment);
        (await Number(session, attachment, "window.failure")).Should().Be(2, "POSITION_UNAVAILABLE is what a browser with no fix answers");

        await session.ResultAsync(
            "Emulation.setGeolocationOverride",
            """{"latitude":60.17,"longitude":24.94,"accuracy":12}""",
            attachment);

        await session.EvaluateAsync(
            "window.fix = null; navigator.geolocation.getCurrentPosition(p => { window.fix = p }); 1",
            attachment);

        await Drain(session, attachment);
        (await Number(session, attachment, "window.fix.coords.latitude")).Should().Be(60.17);
        (await Number(session, attachment, "window.fix.coords.longitude")).Should().Be(24.94);
        (await Number(session, attachment, "window.fix.coords.accuracy")).Should().Be(12);
        (await Flag(session, attachment, "window.fix.coords.altitude === null")).Should().BeTrue();
        (await Flag(session, attachment, "window.fix.timestamp > 0")).Should().BeTrue();

        await session.ResultAsync("Emulation.clearGeolocationOverride", null, attachment);

        await session.EvaluateAsync(
            "window.failure = null; navigator.geolocation.getCurrentPosition(() => {}, e => { window.failure = e.code === e.POSITION_UNAVAILABLE }); 1",
            attachment);

        await Drain(session, attachment);
        (await Flag(session, attachment, "window.failure")).Should().BeTrue();
    }

    [Test]
    public async Task HardwareConcurrencyIsOverriddenAndAnImpossibleOneIsRefused()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        (await Flag(session, attachment, "navigator.hardwareConcurrency >= 1")).Should().BeTrue();

        await session.ResultAsync("Emulation.setHardwareConcurrencyOverride", """{"hardwareConcurrency":4}""", attachment);
        (await Number(session, attachment, "navigator.hardwareConcurrency")).Should().Be(4);

        var error = await session.ErrorAsync("Emulation.setHardwareConcurrencyOverride", """{"hardwareConcurrency":0}""", attachment);
        error.GetProperty("code").GetInt32().Should().Be(-32602);
    }

    [Test]
    public async Task TheAcceptedNoOpsAnswerSuccessRatherThanMinusThirtyTwoSixHundredAndOne()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        (string Method, string Parameters)[] commands =
        [
            ("Emulation.setAutoDarkModeOverride", """{"enabled":true}"""),
            ("Emulation.setIdleOverride", """{"isUserActive":false,"isScreenUnlocked":false}"""),
            ("Emulation.clearIdleOverride", "{}"),
            ("Emulation.setDefaultBackgroundColorOverride", """{"color":{"r":0,"g":0,"b":0}}"""),
            ("Emulation.setCPUThrottlingRate", """{"rate":4}"""),
            ("Emulation.setScrollbarsHidden", """{"hidden":true}"""),
            ("Emulation.setDocumentCookieDisabled", """{"disabled":true}"""),
            ("Emulation.setEmitTouchEventsForMouse", """{"enabled":true}"""),
        ];

        foreach (var (method, parameters) in commands)
        {
            await session.ResultAsync(method, parameters, attachment);
        }

        // …and the one that is a no-op is a no-op: the jar keeps working.
        (await Flag(session, attachment, "navigator.cookieEnabled")).Should().BeTrue();
    }

    /// <summary>Replaces the document, which builds an engine exactly as a navigation does.</summary>
    private static async Task SetContentAsync(PageSession session, string attachment, string html)
    {
        var payload = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["frameId"] = await FrameAsync(session, attachment).ConfigureAwait(false),
            ["html"] = html,
        });

        await session.ResultAsync("Page.setDocumentContent", payload, attachment).ConfigureAwait(false);
    }

    /// <summary>The page's own frame identifier, read the way a client reads it.</summary>
    private static async Task<string> FrameAsync(PageSession session, string attachment)
    {
        var tree = await session.ResultAsync("Page.getFrameTree", null, attachment).ConfigureAwait(false);
        return tree.GetProperty("frameTree").GetProperty("frame").GetProperty("id").GetString()!;
    }

    /// <summary>
    /// Runs a turn of the page's loop, so a callback the engine queued has been delivered.
    /// </summary>
    /// <remarks>
    /// A geolocation callback is a task on the engine's own queue, exactly as the specification requires, so
    /// a test that read the result in the same turn would be asserting that it had <i>not</i> been queued.
    /// </remarks>
    private static Task Drain(PageSession session, string attachment)
        => session.ResultAsync(
            "Runtime.evaluate",
            """{"expression":"new Promise(resolve => setTimeout(resolve, 0))","awaitPromise":true}""",
            attachment);

    private static async Task<double> Number(PageSession session, string attachment, string expression)
        => (await session.EvaluateAsync(expression, attachment)).GetProperty("value").GetDouble();

    private static async Task<bool> Flag(PageSession session, string attachment, string expression)
        => (await session.EvaluateAsync(expression, attachment)).GetProperty("value").GetBoolean();

    private static async Task<string> Text(PageSession session, string attachment, string expression)
        => (await session.EvaluateAsync(expression, attachment)).GetProperty("value").GetString()!;
}
