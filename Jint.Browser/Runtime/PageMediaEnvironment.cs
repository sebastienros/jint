using System.Collections.Frozen;

namespace Jint.Browser.Runtime;

/// <summary>
/// Everything a media query is answered from: the viewport, the media type, and the preferences a client
/// emulated.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is one value, swapped as a unit.</b> A media query's answer depends on the viewport <i>and</i> on
/// the media type <i>and</i> on the preference features together, so a change that moved two of them has to
/// reach <see cref="JsMediaQueryList"/> once — otherwise a <c>change</c> listener runs against half of the
/// next state. <see cref="PageRuntime.SetMedia"/> is the one writer.
/// </para>
/// <para>
/// <b>The Level 5 preference features are the page's own answer, not AngleSharp.Css's.</b> AngleSharp.Css
/// models a render device and evaluates <c>width</c>, <c>height</c> and their kind, and has no notion of
/// <c>prefers-color-scheme</c>, <c>prefers-reduced-motion</c>, <c>forced-colors</c>, <c>hover</c> or
/// <c>pointer</c> at all — and its own <c>CssMediaQueryList.ComputeMatched</c> is a stub that answers
/// <see langword="false"/> for every query. So the answers live here, where <c>Emulation.setEmulatedMedia</c>
/// can move them; the day AngleSharp.Css grows the preference features, this is the table that delegates to
/// it and nothing else moves.
/// </para>
/// <para>
/// A feature a client emulated wins over the value this would compute, whatever the name: Chrome's
/// <c>setEmulatedMedia</c> takes an arbitrary name and value pair, so a feature nothing here knows is still
/// answerable once a client has said what it should be.
/// </para>
/// </remarks>
internal sealed record PageMediaEnvironment
{
    /// <summary>The features a client emulated, keyed by the name a query writes them under.</summary>
    private static readonly FrozenDictionary<string, string> _noFeatures =
        FrozenDictionary<string, string>.Empty;

    /// <summary>A page nobody has emulated anything on, at the default viewport.</summary>
    internal static PageMediaEnvironment Default { get; } = new();

    /// <summary>The size and pixel ratio every dimension feature is answered from.</summary>
    internal Viewport Viewport { get; init; } = Viewport.Default;

    /// <summary>The media type a bare type in a query is matched against; <c>screen</c> unless emulated.</summary>
    internal string MediaType { get; init; } = "screen";

    /// <summary>
    /// Whether the primary pointer is a finger, which is what touch emulation means to a query.
    /// </summary>
    /// <remarks>
    /// <c>Emulation.setTouchEmulationEnabled</c> is the only thing that sets it, and it is what makes
    /// <c>(hover: none)</c> and <c>(pointer: coarse)</c> — the pair a responsive site branches on — true.
    /// </remarks>
    internal bool CoarsePointer { get; init; }

    /// <summary>Whether the document's own scripts run, which is what <c>(scripting)</c> asks.</summary>
    internal bool ScriptingEnabled { get; init; } = true;

    /// <summary>What <c>Emulation.setEmulatedMedia</c>'s feature list asked for, name to value.</summary>
    internal IReadOnlyDictionary<string, string> Features { get; init; } = _noFeatures;

    /// <summary>
    /// The value of a discrete media feature, or <see langword="null"/> when the feature is not one this
    /// knows and no client has said what it should be.
    /// </summary>
    /// <remarks>
    /// <a href="https://drafts.csswg.org/mediaqueries-5/">Media Queries 5</a> is what the defaults are read
    /// from, and every one of them is the value a browser with no user preference and no assistive setting
    /// reports: a light scheme, no reduced motion, no forced colours, a mouse. A headless page has no user,
    /// so those are the truth rather than a guess — which is exactly why a client emulating one has to be
    /// able to say otherwise.
    /// </remarks>
    internal string? ValueOf(string feature)
    {
        // A client's emulation wins over everything, including the two this computes from other state: a
        // client that asked for `(hover: hover)` while touch emulation is on asked for that.
        if (Features.TryGetValue(feature, out var emulated))
        {
            return emulated;
        }

        return feature switch
        {
            "hover" or "any-hover" => CoarsePointer ? "none" : "hover",
            "pointer" or "any-pointer" => CoarsePointer ? "coarse" : "fine",
            "scripting" => ScriptingEnabled ? "enabled" : "none",
            "prefers-color-scheme" => "light",
            "prefers-reduced-motion" => "no-preference",
            "prefers-reduced-transparency" => "no-preference",
            "prefers-reduced-data" => "no-preference",
            "prefers-contrast" => "no-preference",
            "forced-colors" => "none",
            "inverted-colors" => "none",
            "color-gamut" => "srgb",
            "dynamic-range" or "video-dynamic-range" => "standard",
            "display-mode" => "browser",
            "update" => "fast",

            // There is no rendering, so nothing is ever paged and nothing is ever clipped: a page taller
            // than the viewport scrolls, which is what the virtual scroll offset already models.
            "overflow-block" => "scroll",
            "overflow-inline" => "scroll",
            _ => null,
        };
    }
}
