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
/// features, touch, focus, geolocation, the user agent and the hardware concurrency are read on every
/// access, so setting one moves the document that is already loaded. The time zone and the locale are
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

    internal EmulationState(Viewport defaultViewport)
    {
        DefaultViewport = defaultViewport;
        Viewport = defaultViewport;
    }

    /// <summary>The viewport the page opened with, which clearing an override restores.</summary>
    internal Viewport DefaultViewport { get; }

    /// <summary>What the page currently believes its window to be.</summary>
    /// <remarks>
    /// Kept here as well as on the page runtime so that <c>Browser.getWindowForTarget</c> can answer it from
    /// a transport thread: the runtime's copy belongs to the page loop, and a window's size is not worth a
    /// round trip to it.
    /// </remarks>
    internal Viewport Viewport { get; set; }

    /// <summary>Whether <c>Emulation.setTouchEmulationEnabled</c> asked for touch.</summary>
    /// <remarks>
    /// It reaches <c>navigator.maxTouchPoints</c>, the presence of <c>ontouchstart</c> on <c>window</c> and
    /// <c>document</c>, and <c>(hover: none)</c> / <c>(pointer: coarse)</c> in the media environment.
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

    /// <summary>The media type a client emulated, or <see langword="null"/> for the page's own.</summary>
    internal string? EmulatedMedia { get; set; }

    /// <summary>The media features a client emulated, name to value.</summary>
    internal IReadOnlyDictionary<string, string> MediaFeatures { get; set; } = _noFeatures;

    /// <summary>The user agent a client set, or <see langword="null"/> for the browser's own.</summary>
    /// <remarks>
    /// One override for two commands: Chrome treats <c>Emulation.setUserAgentOverride</c> and
    /// <c>Network.setUserAgentOverride</c> as the same setting, and so does this. It is what
    /// <c>navigator.userAgent</c> answers and what every request the page makes carries.
    /// </remarks>
    internal string? UserAgent { get; set; }

    /// <summary>What <c>navigator.language</c> answers when a user agent override named one.</summary>
    internal string? AcceptLanguage { get; set; }

    /// <summary>What <c>navigator.platform</c> answers, or <see langword="null"/> for the empty string.</summary>
    internal string? Platform { get; set; }

    /// <summary>Whether the client asked for the cache to be bypassed. There is no cache to bypass.</summary>
    internal bool CacheDisabled { get; set; }

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
        MediaType = EmulatedMedia is { Length: > 0 } media ? media : "screen",
        CoarsePointer = TouchEnabled,
        ScriptingEnabled = !ScriptExecutionDisabled,
        Features = MediaFeatures,
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
