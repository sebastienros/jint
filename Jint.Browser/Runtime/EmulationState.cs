using System.Collections.Frozen;
using System.Runtime.InteropServices;

namespace Jint.Browser.Runtime;

/// <summary>
/// What a client asked one page to pretend it is, and which of it takes effect when.
/// </summary>
/// <remarks>
/// <para>
/// <b>It belongs to the page, not to the protocol.</b> The <c>Emulation</c> domain is one way to write it —
/// every command of that domain runs on the page loop and sets a property here — and the page's engine
/// factory, its window installer and its media environment are what read it. Keeping it on the page is what
/// lets a value survive the navigation that replaces the engine, which is the whole point of an override:
/// a client sets the time zone once and every document after it is in that time zone.
/// </para>
/// <para>
/// <b>Three groups, and what separates them is when they become effective.</b> The viewport, the media
/// features, touch, focus, geolocation, the user agent, the platform, the language and the hardware
/// concurrency are read on every access, so setting one moves the document that is already loaded - the user
/// agent by being published to the engine showing it, and the rest because <c>NavigatorInstaller</c> and the
/// media environment read this object per get. The time zone and the locale are
/// <c>Options</c> the engine is <i>constructed</i> from, so they reach the next document and the command
/// says so. Script execution is the document's, decided when its parse starts.
/// </para>
/// <para>
/// Written on the page loop and read from a transport thread by <c>Browser.getWindowForTarget</c>, which is
/// why the viewport is a plain property of a reference type rather than something torn: a window's size is
/// two integers a client asked for moments ago, and a round trip to the loop for it would be a command
/// waiting behind a page's own script.
/// </para>
/// </remarks>
internal sealed class EmulationState
{
    private static readonly FrozenDictionary<string, string> _noFeatures =
        FrozenDictionary<string, string>.Empty;

    internal EmulationState(Viewport defaultViewport, string defaultUserAgent)
    {
        DefaultViewport = defaultViewport;
        DefaultUserAgent = defaultUserAgent;
        Viewport = defaultViewport;
    }

    /// <summary>The viewport the page opened with, which clearing an override restores.</summary>
    internal Viewport DefaultViewport { get; }

    /// <summary><see cref="BrowserOptions.UserAgent"/>, which is what a page with no override reports.</summary>
    internal string DefaultUserAgent { get; }

    /// <summary>What the page currently believes its window to be.</summary>
    /// <remarks>
    /// Kept here as well as on the page runtime so that <c>Browser.getWindowForTarget</c> can answer it from
    /// a transport thread: the runtime's copy belongs to the page loop, and a window's size is not worth a
    /// round trip to it.
    /// </remarks>
    internal Viewport Viewport { get; set; }

    /// <summary>Whether a client told the page it is offline, which is what <c>navigator.onLine</c> answers.</summary>
    /// <remarks>
    /// <b>The page's belief, not its transport.</b> Chrome keeps the two apart and so does this:
    /// <c>Network.overrideNetworkState</c> writes this and fails no request,
    /// <c>Network.emulateNetworkConditionsByRule</c> fails requests and leaves this alone, and the deprecated
    /// <c>Network.emulateNetworkConditions</c> writes both. It is read per get, so a change reaches the document
    /// already loaded.
    /// </remarks>
    internal bool Offline { get; set; }

    /// <summary>Whether <c>Emulation.setTouchEmulationEnabled</c> asked for touch.</summary>
    /// <remarks>
    /// It reaches <c>navigator.maxTouchPoints</c>, the presence of <c>ontouchstart</c> on <c>window</c>,
    /// <c>document</c> and <c>Element.prototype</c>, and <c>(hover: none)</c> / <c>(pointer: coarse)</c> in the
    /// media environment.
    /// </remarks>
    internal bool TouchEnabled { get; set; }

    /// <summary>How many touch points a client asked for, reported as <c>navigator.maxTouchPoints</c>.</summary>
    internal int MaxTouchPoints { get; set; } = 1;

    /// <summary>Whether the page is to behave as focused.</summary>
    /// <remarks>
    /// A headless page is focused already — there is one document and nothing on a screen to take the focus
    /// from it — so this makes the flag a client set observable rather than changing what the page reports.
    /// Turning it <i>off</i> is what changes something: <c>document.hasFocus()</c> answers
    /// <see langword="false"/> and <c>document.visibilityState</c> becomes <c>hidden</c>.
    /// </remarks>
    internal bool? FocusEmulation { get; set; }

    /// <summary>
    /// The media type the page opened with — <see cref="BrowserOptions.MediaEmulation"/> under
    /// <see cref="BrowserContextOptions.MediaEmulation"/> — or <see langword="null"/> for <c>screen</c>.
    /// </summary>
    /// <remarks>
    /// <b>The host's defaults are a layer of their own, beneath the overrides</b>, as
    /// <see cref="DefaultViewport"/> is beneath <see cref="Viewport"/>: they stand where a real browser's
    /// operating-system preference stands, so clearing an override — <c>Emulation.setEmulatedMedia</c> with
    /// nothing in it, or an empty value from <see cref="Page.EmulateMediaAsync"/> — goes back to them rather
    /// than past them to the headless baseline.
    /// </remarks>
    internal string? DefaultEmulatedMedia { get; private set; }

    /// <summary>The media features the page opened with, name to value; see <see cref="DefaultEmulatedMedia"/>.</summary>
    internal IReadOnlyDictionary<string, string> DefaultMediaFeatures { get; private set; } = _noFeatures;

    /// <summary>The media type a client or host emulated, or <see langword="null"/> for the page's default.</summary>
    internal string? EmulatedMedia { get; private set; }

    /// <summary>The media features a client or host emulated, name to value.</summary>
    internal IReadOnlyDictionary<string, string> MediaFeatures { get; private set; } = _noFeatures;

    /// <summary>
    /// <see cref="MediaFeatures"/> over <see cref="DefaultMediaFeatures"/>, built only when either moves.
    /// </summary>
    /// <remarks>
    /// Never mutated once built: <see cref="MediaEnvironment"/> hands this very instance to the page, and
    /// <see cref="PageRuntime.SetMedia"/> compares environments by it, so an unchanged state has to hand out
    /// the same reference again for a repeated call to be the no-op it is.
    /// </remarks>
    private IReadOnlyDictionary<string, string> _effectiveMediaFeatures = _noFeatures;

    /// <summary>
    /// Folds the host's browser and context defaults into the layer a page opens with. Called by the page's
    /// constructor, before the loop builds the first engine, so there is no runtime to tell yet.
    /// </summary>
    internal void ApplyMediaDefaults(MediaEmulation? emulation)
    {
        if (emulation is null)
        {
            return;
        }

        var media = DefaultEmulatedMedia;
        var features = DefaultMediaFeatures;
        if (Merge(ref media, ref features, emulation))
        {
            DefaultEmulatedMedia = media;
            DefaultMediaFeatures = features;
            _effectiveMediaFeatures = Layer(DefaultMediaFeatures, MediaFeatures);
        }
    }

    /// <summary>
    /// <see cref="Page.EmulateMediaAsync"/>: merges into the overrides — a <see langword="null"/> property is
    /// left as it is, an empty one removes its override, any other value replaces it — and re-evaluates the
    /// page's media queries. Called on the page loop.
    /// </summary>
    /// <remarks>
    /// <b>One state, two writers.</b> The protocol's <see cref="ReplaceMedia"/> replaces the media type and
    /// the whole feature list, because that is what Chrome's command means; this merges, because that is
    /// what Playwright's <c>emulateMedia</c> means. A feature a protocol client emulated that this does not
    /// name survives it.
    /// </remarks>
    internal void EmulateMedia(MediaEmulation emulation, PageRuntime? runtime)
    {
        var media = EmulatedMedia;
        var features = MediaFeatures;
        if (Merge(ref media, ref features, emulation))
        {
            SetOverrides(media, features);
        }

        runtime?.SetMedia(MediaEnvironment);
    }

    /// <summary>
    /// <c>Emulation.setEmulatedMedia</c>: replaces the media type and every feature override, then
    /// re-evaluates the page's media queries. Called on the page loop.
    /// </summary>
    /// <param name="media">The media type, or <see langword="null"/> or empty for the page's default.</param>
    /// <param name="features">Every feature override; an empty value is no override, as Chromium reads it.</param>
    /// <param name="runtime">The page's current runtime, if it has one.</param>
    internal void ReplaceMedia(string? media, IEnumerable<KeyValuePair<string, string>>? features, PageRuntime? runtime)
    {
        Dictionary<string, string>? map = null;
        foreach (var (name, value) in features ?? [])
        {
            map ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (value.Length == 0)
            {
                // Playwright sends every preference it does not emulate with an empty value: no override.
                map.Remove(name);
            }
            else
            {
                map[name] = value;
            }
        }

        SetOverrides(media is { Length: > 0 } ? media : null, map is { Count: > 0 } ? map : _noFeatures);
        runtime?.SetMedia(MediaEnvironment);
    }

    private void SetOverrides(string? media, IReadOnlyDictionary<string, string> features)
    {
        EmulatedMedia = media;
        if (!ReferenceEquals(features, MediaFeatures))
        {
            MediaFeatures = features;
            _effectiveMediaFeatures = Layer(DefaultMediaFeatures, MediaFeatures);
        }
    }

    /// <summary>
    /// Applies <paramref name="emulation"/> to one layer, copying its features only when one actually changes.
    /// </summary>
    /// <returns>Whether anything changed.</returns>
    private static bool Merge(ref string? media, ref IReadOnlyDictionary<string, string> features, MediaEmulation emulation)
    {
        var changed = false;
        if (emulation.Media is { } type)
        {
            var next = type.Length == 0 ? null : type;
            changed = !string.Equals(next, media, StringComparison.Ordinal);
            media = next;
        }

        Dictionary<string, string>? copy = null;
        foreach (var (name, value) in emulation.FeatureChanges())
        {
            var present = features.TryGetValue(name, out var current);
            if (value.Length == 0 ? !present : present && string.Equals(current, value, StringComparison.Ordinal))
            {
                continue;
            }

            copy ??= new Dictionary<string, string>(features, StringComparer.OrdinalIgnoreCase);
            if (value.Length == 0)
            {
                copy.Remove(name);
            }
            else
            {
                copy[name] = value;
            }
        }

        if (copy is not null)
        {
            features = copy.Count == 0 ? _noFeatures : copy;
            changed = true;
        }

        return changed;
    }

    /// <summary><paramref name="overrides"/> over <paramref name="defaults"/>, sharing either when the other is empty.</summary>
    private static IReadOnlyDictionary<string, string> Layer(
        IReadOnlyDictionary<string, string> defaults,
        IReadOnlyDictionary<string, string> overrides)
    {
        if (defaults.Count == 0)
        {
            return overrides;
        }

        if (overrides.Count == 0)
        {
            return defaults;
        }

        var merged = new Dictionary<string, string>(defaults, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in overrides)
        {
            merged[name] = value;
        }

        return merged;
    }

    /// <summary>The user agent a client set, or <see langword="null"/> for the browser's own.</summary>
    /// <remarks>
    /// One override for two commands: Chrome treats <c>Emulation.setUserAgentOverride</c> and
    /// <c>Network.setUserAgentOverride</c> as the same setting, and so does this. It is what
    /// <c>navigator.userAgent</c> answers and what every request the page makes carries.
    /// </remarks>
    internal string? UserAgent { get; set; }

    /// <summary>
    /// The user agent this page reports and sends: the client's override, or the browser's own.
    /// </summary>
    /// <remarks>
    /// <b>There is exactly one answer to that question and this is it.</b> <c>navigator.userAgent</c>, the
    /// document's own request, every subresource, a script's <c>fetch</c> and <c>XMLHttpRequest</c> and a
    /// worker's engine all read this one property — a second resolution of the same precedence somewhere
    /// else is how a page came to report one string and send another
    /// ([#3720](https://github.com/sebastienros/jint/issues/3720)).
    /// </remarks>
    internal string EffectiveUserAgent => UserAgent is { Length: > 0 } overridden ? overridden : DefaultUserAgent;

    /// <summary>
    /// Called on the page loop whenever <see cref="EffectiveUserAgent"/> may have changed, so the engine
    /// showing the document can be told. Set by the page runtime for the engine it currently owns.
    /// </summary>
    /// <remarks>
    /// <b>It is a publish rather than a second resolution.</b> <c>navigator.userAgent</c> is now the engine's
    /// own accessor, and an engine holds a string rather than a view of this object — so something has to
    /// carry the new value across, and this is the one place that does. The resolution above stays the only
    /// one there is, which is the property <see cref="EffectiveUserAgent"/>'s remarks are about.
    /// </remarks>
    internal Action<string>? UserAgentChanged { get; set; }

    /// <summary>
    /// <c>Emulation.setUserAgentOverride</c> and <c>Network.setUserAgentOverride</c>, which Chrome treats as
    /// one setting and which are one write here.
    /// <para>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Emulation/#method-setUserAgentOverride
    /// </para>
    /// </summary>
    /// <remarks>
    /// All three reach the document that is <i>already loaded</i>: the user agent because the engine is told,
    /// and the language and the platform because <c>NavigatorInstaller</c> reads this object per get. An
    /// absent <c>acceptLanguage</c> or <c>platform</c> leaves the one already set, which is what a client
    /// sending only a user agent expects.
    /// </remarks>
    internal void ApplyUserAgentOverride(
        string? userAgent,
        string? acceptLanguage,
        string? platform,
        UserAgentClientHints? metadata = null)
    {
        UserAgent = userAgent;
        UserAgentMetadata = userAgent is { Length: > 0 } ? metadata : null;

        if (acceptLanguage is { Length: > 0 } language)
        {
            AcceptLanguage = language;
        }

        if (platform is { Length: > 0 } named)
        {
            Platform = named;
        }

        UserAgentChanged?.Invoke(EffectiveUserAgent);
    }

    /// <summary>
    /// The client hints a user-agent override described, which <c>navigator.userAgentData</c> answers; see
    /// <see cref="UserAgentClientHints"/> for what it answers without them.
    /// </summary>
    internal UserAgentClientHints? UserAgentMetadata { get; private set; }

    /// <summary>What <c>navigator.language</c> answers when a user agent override named one.</summary>
    internal string? AcceptLanguage { get; set; }

    /// <summary>What <c>navigator.platform</c> answers, or <see langword="null"/> for the empty string.</summary>
    /// <remarks>
    /// Read per get by <c>NavigatorInstaller</c>, so it moves the document that is already loaded rather than
    /// the next one. Unlike the user agent it needs no publish, because it is one of the page's own members
    /// and not one the engine's <c>Navigator</c> declares.
    /// </remarks>
    internal string? Platform { get; set; }

    /// <summary>Whether the client asked for HTTP cache reads and writes to be bypassed.</summary>
    internal bool CacheDisabled { get => Volatile.Read(ref _cacheDisabled); set => Volatile.Write(ref _cacheDisabled, value); }
    private bool _cacheDisabled;

    /// <summary>The IANA time zone the next document's engine is built in, or <see langword="null"/>.</summary>
    internal TimeZoneInfo? TimeZone { get; set; }

    /// <summary>The culture the next document's engine is built in, or <see langword="null"/>.</summary>
    internal System.Globalization.CultureInfo? Locale { get; set; }

    /// <summary>Whether the next document's own scripts are refused.</summary>
    /// <remarks>
    /// <c>Runtime.evaluate</c> and <c>Runtime.callFunctionOn</c> are unaffected, which is Chrome's behaviour
    /// and what makes the setting usable: a client turns a page's own scripts off and then measures the
    /// document it got.
    /// </remarks>
    internal bool ScriptExecutionDisabled { get; set; }

    /// <summary>What <c>navigator.hardwareConcurrency</c> answers, or <see langword="null"/> for the host's.</summary>
    internal int? HardwareConcurrency { get; set; }

    /// <summary>Where the page is, or <see langword="null"/> when nobody has said.</summary>
    /// <remarks>
    /// A page with no override is not a page at latitude zero: <c>getCurrentPosition</c> answers its error
    /// callback with <c>POSITION_UNAVAILABLE</c>, which is what a browser does when it has no fix.
    /// </remarks>
    internal GeolocationOverride? Geolocation { get; set; }

    /// <summary>The environment every media query of this page is answered from.</summary>
    /// <remarks>
    /// Rebuilt rather than mutated, because <see cref="PageRuntime.SetMedia"/> compares the whole value and
    /// notifies every <c>MediaQueryList</c> whose answer moved: a change that touched the viewport and a
    /// preference at once has to reach a listener once, with both already in place.
    /// </remarks>
    internal PageMediaEnvironment MediaEnvironment => new()
    {
        Viewport = Viewport,
        MediaType = EmulatedMedia ?? DefaultEmulatedMedia ?? "screen",
        CoarsePointer = TouchEnabled,
        ScriptingEnabled = !ScriptExecutionDisabled,
        Features = _effectiveMediaFeatures,
    };
}

/// <summary>Where <c>Emulation.setGeolocationOverride</c> put the page.</summary>
/// <param name="Latitude">Degrees north of the equator.</param>
/// <param name="Longitude">Degrees east of the prime meridian.</param>
/// <param name="Accuracy">The radius of the 95% confidence circle, in metres.</param>
/// <param name="Altitude">Metres above the ellipsoid, or <see langword="null"/> when unknown.</param>
/// <param name="AltitudeAccuracy">The altitude's own accuracy in metres, or <see langword="null"/>.</param>
/// <param name="Heading">Degrees clockwise from true north, or <see langword="null"/>.</param>
/// <param name="Speed">Metres per second, or <see langword="null"/>.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct GeolocationOverride(
    double Latitude,
    double Longitude,
    double Accuracy,
    double? Altitude,
    double? AltitudeAccuracy,
    double? Heading,
    double? Speed);
