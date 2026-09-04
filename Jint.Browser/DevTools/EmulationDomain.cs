using System.Globalization;
using Jint.Browser.Runtime;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Emulation;
using Jint.DevTools.Session;

namespace Jint.Browser.DevTools;

/// <summary>
/// The <c>Emulation</c> domain: what a client asks the page to pretend it is.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every override here is effective, and the interesting question is <i>when</i>.</b> The viewport, the
/// media type and its features, touch, focus, geolocation and the hardware concurrency move the document
/// that is loaded — <c>matchMedia</c> re-evaluates and fires <c>change</c>, <c>navigator</c> answers
/// differently on the next read. The time zone and the locale are <c>Options</c> an engine is
/// <i>constructed</i> from, and a page builds one engine per navigation, so those two take effect on the
/// next document; each says so in its own summary and neither is refused, because a client sets them before
/// it navigates. Script execution is the same shape: the parse is what refuses to run a script.
/// </para>
/// <para>
/// <b>The state is the page's, not this attachment's.</b> Two clients attached to one page share one
/// emulation, last writer wins — a page has one viewport and one time zone, and a per-attachment override
/// would mean a document that was two sizes at once. It is <see cref="Page.Emulation"/> for the same reason
/// the network policy is the target's.
/// </para>
/// <para>
/// Every command runs on the page loop, so it writes the state and re-evaluates the media queries on the one
/// thread allowed to touch the engine.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Emulation/"/>.
/// </para>
/// </remarks>
internal sealed class EmulationDomain : EmulationDomainBase
{
    private readonly PageTarget _target;

    internal EmulationDomain(PageTarget target)
    {
        _target = target;
    }

    private EmulationState State => _target.Emulation;

    /// <summary>Sets what the page believes its window to be, for real.</summary>
    /// <remarks>
    /// The width, the height and the device scale factor are the three a page can read, and they take
    /// effect: <c>innerWidth</c>, <c>devicePixelRatio</c>, <c>screen</c>, every client rectangle and every
    /// dimension media query move with them. <c>scale</c>, the screen dimensions, the window position and
    /// the display feature are accepted and not acted on — there is no renderer for a scale to mean anything
    /// to. <c>mobile</c> and <c>screenOrientation</c> are accepted and reach the page only through the
    /// numbers: a viewport taller than it is wide is <c>(orientation: portrait)</c>, and there is no
    /// <c>navigator.userAgentData</c> to carry <c>mobile</c> because this browser's user agent does not
    /// claim to be Chromium and publishing one would be a claim a page branches on.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetDeviceMetricsOverrideAsync(SetDeviceMetricsOverrideRequest parameters, CommandContext context)
    {
        var scale = parameters.DeviceScaleFactor > 0 ? parameters.DeviceScaleFactor : 1;
        SetViewport(new Viewport(parameters.Width, parameters.Height, scale));
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Puts the page's viewport back to what the browser opened it with.</summary>
    protected override ValueTask<EmptyResult> ClearDeviceMetricsOverrideAsync(EmptyParameters parameters, CommandContext context)
    {
        SetViewport(State.DefaultViewport);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Resizes the viewport, which is the deprecated half of <c>setDeviceMetricsOverride</c>.</summary>
    /// <remarks>
    /// It keeps the device scale factor, because that is the one thing this command does not carry and the
    /// one thing a client that sends it has already set with something else.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetVisibleSizeAsync(SetVisibleSizeRequest parameters, CommandContext context)
    {
        SetViewport(new Viewport(parameters.Width, parameters.Height, State.Viewport.DeviceScaleFactor));
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Makes the page report itself as touch-capable, and re-evaluates its media queries.</summary>
    /// <remarks>
    /// It reaches <c>navigator.maxTouchPoints</c>, the presence of <c>ontouchstart</c> on <c>window</c> and
    /// <c>document</c>, and <c>(hover: none)</c> / <c>(pointer: coarse)</c>. <b>No touch event is ever
    /// dispatched</b>: <c>Input</c> is the mouse and the keyboard, so what this changes is what a page
    /// detects rather than what it receives — <c>Runtime/TouchEmulation</c> states the trade.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetTouchEmulationEnabledAsync(SetTouchEmulationEnabledRequest parameters, CommandContext context)
    {
        State.TouchEnabled = parameters.Enabled;
        State.MaxTouchPoints = parameters.MaxTouchPoints ?? 1;

        if (Runtime() is { } runtime)
        {
            TouchEmulation.Apply(runtime);
            runtime.SetMedia(State.MediaEnvironment);
        }

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Decides whether the page reports itself as focused and visible.</summary>
    /// <remarks>
    /// A headless page is focused already — there is one document and nothing on a screen to take the focus
    /// from it — so enabling this changes nothing a page can see, and it is answered rather than refused
    /// because every Playwright connection sends it. <b>Disabling it is what changes something</b>:
    /// <c>document.hasFocus()</c> answers <see langword="false"/>, <c>document.visibilityState</c> becomes
    /// <c>hidden</c>, <c>document.hidden</c> becomes <see langword="true"/> and a <c>visibilitychange</c>
    /// event is fired at the document — which is how a page that pauses its work in a background tab is
    /// driven into that state.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetFocusEmulationEnabledAsync(SetFocusEmulationEnabledRequest parameters, CommandContext context)
    {
        State.FocusEmulation = parameters.Enabled;

        if (Runtime() is { } runtime)
        {
            var realm = Jint.Browser.Events.BrowserEventRealm.Of(runtime.Engine);
            if (realm.DocumentHasFocus != parameters.Enabled)
            {
                realm.DocumentHasFocus = parameters.Enabled;

                if (runtime.DocumentWrapper is { } document)
                {
                    // https://html.spec.whatwg.org/multipage/interaction.html#page-visibility —
                    // fired at the Document, and it bubbles.
                    Jint.Browser.Runtime.PageEvents.Fire(runtime, document, "visibilitychange", bubbles: true);
                }
            }
        }

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Emulates the media type and the preference features the page is evaluated against.</summary>
    /// <remarks>
    /// <para>
    /// The media type — <c>print</c> or <c>screen</c>, and the empty string to clear — and the feature list
    /// both reach <c>Runtime/PageMediaEnvironment</c>, which is what every <c>matchMedia</c> query is
    /// answered from. So <c>matchMedia('(prefers-color-scheme: dark)').matches</c> flips on this command and
    /// every <c>MediaQueryList</c> whose own answer moved hears <c>change</c>. A feature name this browser
    /// has no default for is still answered once a client has named it, because the client's value is what
    /// the environment reads first.
    /// </para>
    /// <para>
    /// <b>The media type reaches the cascade and a preference does not.</b>
    /// <c>Runtime/PageRenderDevice</c> reports the emulated type, so an <c>@media print</c> rule becomes
    /// active with this command; <c>IRenderDevice</c> has no member for a Level 5 preference, so an
    /// <c>@media (prefers-color-scheme: dark)</c> rule in a style sheet never does. A page that themes
    /// itself reads <c>matchMedia</c>, which is the half that answers.
    /// </para>
    /// </remarks>
    protected override ValueTask<EmptyResult> SetEmulatedMediaAsync(SetEmulatedMediaRequest parameters, CommandContext context)
    {
        State.EmulatedMedia = parameters.Media;
        State.MediaFeatures = Features(parameters.Features);
        Reevaluate();
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Sets the user agent every request carries and <c>navigator.userAgent</c> answers.</summary>
    /// <remarks>
    /// <c>acceptLanguage</c> becomes <c>navigator.language</c> and <c>navigator.languages</c>;
    /// <c>platform</c> becomes <c>navigator.platform</c>. <c>userAgentMetadata</c> is accepted and dropped,
    /// because there is no <c>navigator.userAgentData</c> to put it in.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetUserAgentOverrideAsync(SetUserAgentOverrideRequest parameters, CommandContext context)
    {
        // One override for two commands: Chrome treats Emulation.setUserAgentOverride and
        // Network.setUserAgentOverride as the same setting, and the page's EmulationState is the one place
        // both write — what navigator.userAgent answers and what PageNetworkPolicy puts on every request.
        State.ApplyUserAgentOverride(parameters.UserAgent, parameters.AcceptLanguage, parameters.Platform);

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Puts the <b>next</b> document's engine in a time zone, and says so.</summary>
    /// <remarks>
    /// An engine's time zone is an <c>Options</c> value fixed when it is constructed, and a page constructs
    /// one per navigation — so this reaches <c>new Date()</c>, <c>getTimezoneOffset()</c> and
    /// <c>Intl.DateTimeFormat</c> from the next document onwards, which is where every client sets it. The
    /// empty string clears the override. A name neither the operating system nor Jint's time-zone provider
    /// knows is refused in Chrome's own words, at the moment the client sends it rather than at the
    /// navigation that would otherwise fail for it.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetTimezoneOverrideAsync(SetTimezoneOverrideRequest parameters, CommandContext context)
    {
        if (parameters.TimezoneId.Length == 0)
        {
            State.TimeZone = null;
            return new ValueTask<EmptyResult>(EmptyResult.Instance);
        }

        try
        {
            State.TimeZone = TimeZoneInfo.FindSystemTimeZoneById(parameters.TimezoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Chrome's own wording, which is what a client matches on.
            Jint.DevTools.Throw.ServerError(
                "Invalid timezone id: " + parameters.TimezoneId,
                "the host operating system has no time zone by that name");
        }

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Puts the <b>next</b> document's engine in a locale, and says so.</summary>
    /// <remarks>
    /// The same shape as the time zone and for the same reason: a realm's <c>Intl</c> objects are built from
    /// the engine's culture. It reaches every <c>Intl</c> constructor, <c>toLocaleString</c> and —
    /// unless a user-agent override named an <c>Accept-Language</c> — <c>navigator.language</c>. The empty
    /// string clears it.
    /// <para>
    /// <b>What counts as invalid is .NET's answer rather than Chrome's</b>, and .NET is the more permissive
    /// of the two: <c>CultureInfo.GetCultureInfo</c> builds a culture for a well-formed name it has no data
    /// for and refuses only one that is not well formed. So a name Chrome would reject can be accepted here
    /// and then behave as the invariant culture does, which is what a host asking for an unknown culture
    /// gets from the engine anyway.
    /// </para>
    /// </remarks>
    protected override ValueTask<EmptyResult> SetLocaleOverrideAsync(SetLocaleOverrideRequest parameters, CommandContext context)
    {
        if (parameters.Locale is not { Length: > 0 } locale)
        {
            State.Locale = null;
            return new ValueTask<EmptyResult>(EmptyResult.Instance);
        }

        try
        {
            State.Locale = CultureInfo.GetCultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            Jint.DevTools.Throw.ServerError(
                "Invalid locale: " + locale,
                "the host runtime has no culture by that name");
        }

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Stops the <b>next</b> document's own scripts from running.</summary>
    /// <remarks>
    /// It is the document's rather than the engine's: the parse is what refuses, so the setting is read when
    /// a navigation starts and holds for that document. Nothing of it reaches <c>Runtime.evaluate</c> or
    /// <c>Runtime.callFunctionOn</c>, which is Chrome's behaviour and the whole point — a client turns a
    /// page's own scripts off and then measures the document it got. Three things stop together: a
    /// <c>&lt;script&gt;</c>, a module script, and an <c>onclick=</c> content attribute.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetScriptExecutionDisabledAsync(SetScriptExecutionDisabledRequest parameters, CommandContext context)
    {
        State.ScriptExecutionDisabled = parameters.Value;
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Puts the page somewhere, which <c>navigator.geolocation</c> answers with.</summary>
    /// <remarks>
    /// A parameter set with no latitude and no longitude is Chrome's way of saying "position unavailable",
    /// and it clears the override rather than putting the page at the intersection of the equator and the
    /// prime meridian.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetGeolocationOverrideAsync(SetGeolocationOverrideRequest parameters, CommandContext context)
    {
        if (parameters.Latitude is not { } latitude || parameters.Longitude is not { } longitude)
        {
            State.Geolocation = null;
            return new ValueTask<EmptyResult>(EmptyResult.Instance);
        }

        State.Geolocation = new GeolocationOverride(
            latitude,
            longitude,
            parameters.Accuracy ?? 1,
            parameters.Altitude,
            parameters.AltitudeAccuracy,
            parameters.Heading,
            parameters.Speed);

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Takes the position away, so <c>getCurrentPosition</c> answers its error callback again.</summary>
    protected override ValueTask<EmptyResult> ClearGeolocationOverrideAsync(EmptyParameters parameters, CommandContext context)
    {
        State.Geolocation = null;
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Sets what <c>navigator.hardwareConcurrency</c> answers.</summary>
    /// <remarks>
    /// Zero and negative values are refused rather than reported: a library sizing a worker pool from this
    /// divides by it.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetHardwareConcurrencyOverrideAsync(SetHardwareConcurrencyOverrideRequest parameters, CommandContext context)
    {
        if (parameters.HardwareConcurrency < 1)
        {
            Jint.DevTools.Throw.InvalidParams("Invalid parameters", "hardwareConcurrency: expected a positive integer");
        }

        State.HardwareConcurrency = parameters.HardwareConcurrency;
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Accepted, and there is no automatic dark mode to override.</summary>
    /// <remarks>
    /// Chrome's auto dark mode rewrites a page's own colours in the renderer. There is no renderer here, and
    /// the preference a page reads — <c>prefers-color-scheme</c> — is <c>setEmulatedMedia</c>'s, which is
    /// effective.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetAutoDarkModeOverrideAsync(SetAutoDarkModeOverrideRequest parameters, CommandContext context)
        => new(EmptyResult.Instance);

    /// <summary>Accepted, and there is no idle state to report.</summary>
    /// <remarks>
    /// The Idle Detection API is not implemented — <c>IdleDetector</c> is absent, so feature detection sees
    /// the truth — and this command's only observable effect would be through it.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetIdleOverrideAsync(SetIdleOverrideRequest parameters, CommandContext context)
        => new(EmptyResult.Instance);

    /// <inheritdoc cref="SetIdleOverrideAsync"/>
    protected override ValueTask<EmptyResult> ClearIdleOverrideAsync(EmptyParameters parameters, CommandContext context)
        => new(EmptyResult.Instance);

    /// <summary>Accepted, and nothing is painted for a background colour to be behind.</summary>
    protected override ValueTask<EmptyResult> SetDefaultBackgroundColorOverrideAsync(SetDefaultBackgroundColorOverrideRequest parameters, CommandContext context)
        => new(EmptyResult.Instance);

    /// <summary>Accepted, and the engine is not slowed down.</summary>
    /// <remarks>
    /// Chrome throttles by inserting sleeps in its renderer's main thread. Doing that here would mean
    /// sleeping the page loop, which is the thread every protocol command is answered on — so a client that
    /// throttled a page would be throttling its own conversation with it. What bounds a page's work is
    /// <c>BrowserOptions.MaxTaskDuration</c>, which is the host's decision rather than a client's.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetCPUThrottlingRateAsync(SetCPUThrottlingRateRequest parameters, CommandContext context)
        => new(EmptyResult.Instance);

    /// <summary>Accepted, and there are no scrollbars to hide.</summary>
    /// <remarks>
    /// The flat box model gives every element the full viewport width and the virtual scroll offset takes no
    /// room, so a page's client rectangles are already what they would be with the scrollbars hidden.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetScrollbarsHiddenAsync(SetScrollbarsHiddenRequest parameters, CommandContext context)
        => new(EmptyResult.Instance);

    /// <summary>Accepted, and <c>document.cookie</c> keeps working.</summary>
    /// <remarks>
    /// The jar is the browser context's and the host owns it; a client silently emptying what a page reads
    /// from it would make a cookie login fail with nothing to point at. A host that wants a page with no
    /// cookies gives its context a <c>CookieJar</c> that stores none.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetDocumentCookieDisabledAsync(SetDocumentCookieDisabledRequest parameters, CommandContext context)
        => new(EmptyResult.Instance);

    /// <summary>Accepted, and a mouse event stays a mouse event.</summary>
    /// <remarks>
    /// There is no touch event interface at all here, so translating a mouse event into one would mean
    /// firing an event whose type nothing can construct and whose members a page would read as
    /// <c>undefined</c>. <c>Input.dispatchMouseEvent</c> is the whole of the pointer input.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetEmitTouchEventsForMouseAsync(SetEmitTouchEventsForMouseRequest parameters, CommandContext context)
        => new(EmptyResult.Instance);

    /// <summary>The features a client emulated, as the environment reads them.</summary>
    /// <remarks>
    /// A feature named twice is last writer wins, and a name is matched exactly as CSS writes it — the
    /// protocol carries the CSS name, so no mapping is needed and none is invented.
    /// </remarks>
    private static IReadOnlyDictionary<string, string> Features(MediaFeature[]? features)
    {
        if (features is not { Length: > 0 })
        {
            return System.Collections.Frozen.FrozenDictionary<string, string>.Empty;
        }

        var map = new Dictionary<string, string>(features.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var feature in features)
        {
            map[feature.Name] = feature.Value;
        }

        return map;
    }

    /// <summary>
    /// Writes the viewport straight onto the page runtime, which is safe because this is the loop thread.
    /// </summary>
    /// <remarks>
    /// Every command of a page target is answered on the page's own loop — the target's mailbox is the
    /// engine's, and the engine is the loop's — so the viewport is set where a media query would read it,
    /// with the <c>change</c> events that follow dispatched on the same turn.
    /// </remarks>
    private void SetViewport(Viewport viewport)
    {
        State.Viewport = viewport;
        Reevaluate();
    }

    /// <summary>Hands the page the whole media environment again, so one change is one notification.</summary>
    private void Reevaluate() => Runtime()?.SetMedia(State.MediaEnvironment);

    private PageRuntime? Runtime() => PageRuntime.Find(_target.Runtime.Engine);
}
