namespace Jint.Browser;

/// <summary>
/// The media type and user preferences a page is to be answered as having — what
/// <see cref="Page.EmulateMediaAsync"/> changes, and what <see cref="BrowserOptions.MediaEmulation"/> and
/// <see cref="BrowserContextOptions.MediaEmulation"/> open a page with.
/// </summary>
/// <remarks>
/// <para>
/// <b>Each property is one override, and three states.</b> <see langword="null"/> — the default — leaves
/// whatever is already in force alone; the empty string clears the override, so the page answers its
/// default again — the browser's or context's when one was configured, the headless one otherwise; any other
/// value is the CSS keyword the media feature is to report. That is Playwright's
/// <c>page.emulateMedia</c> shape: a test that emulates reduced motion in its set-up and a dark scheme in its
/// body ends up with both, and setting one never resets the other.
/// </para>
/// <para>
/// A value is one of the keywords Media Queries 5 defines for its feature — plus Playwright's
/// <c>no-preference</c> for the colour scheme — matched ASCII case-insensitively and stored in lower case;
/// anything else is refused with an
/// <see cref="ArgumentException"/> when it is assigned, so a misspelt keyword fails at the call site rather
/// than as a page that silently never matches.
/// </para>
/// <para>
/// <b>What it moves.</b> Every <c>matchMedia</c> list and every style sheet <c>@media</c> rule of the page
/// is answered from the same media environment, so both follow an override, and each
/// <c>MediaQueryList</c> whose answer changes fires <c>change</c>. The page's defaults, with nothing
/// emulated, are <c>screen</c>, a <c>light</c> scheme, no reduced motion, no forced colours and no contrast
/// preference.
/// </para>
/// </remarks>
public sealed class MediaEmulation
{
    private static readonly string[] _mediaTypes = ["screen", "print"];
    private static readonly string[] _colorSchemes = ["light", "dark", "no-preference"];
    private static readonly string[] _reducedMotions = ["reduce", "no-preference"];
    private static readonly string[] _forcedColors = ["active", "none"];
    private static readonly string[] _contrasts = ["no-preference", "more", "less", "custom"];

    private readonly string? _media;
    private readonly string? _colorScheme;
    private readonly string? _reducedMotion;
    private readonly string? _forcedColorsValue;
    private readonly string? _contrast;

    /// <summary>The media type: <c>screen</c> or <c>print</c>.</summary>
    /// <remarks>
    /// What a bare type in a query — <c>matchMedia('print')</c>, <c>@media print { … }</c> — is matched
    /// against. <see langword="null"/> leaves it alone and the empty string goes back to <c>screen</c>.
    /// </remarks>
    /// <exception cref="ArgumentException">The value is not <c>screen</c> or <c>print</c>.</exception>
    public string? Media
    {
        get => _media;
        init => _media = Keyword(value, _mediaTypes, nameof(Media));
    }

    /// <summary>
    /// <a href="https://drafts.csswg.org/mediaqueries-5/#prefers-color-scheme"><c>prefers-color-scheme</c></a>:
    /// <c>light</c>, <c>dark</c> or <c>no-preference</c>.
    /// </summary>
    /// <remarks>
    /// <c>no-preference</c> is not a Media Queries 5 value — the specification dropped it — but it is
    /// Playwright's, and it means a page that matches neither <c>(prefers-color-scheme: light)</c> nor
    /// <c>(prefers-color-scheme: dark)</c>, in <c>matchMedia</c> and in style sheets alike.
    /// </remarks>
    /// <exception cref="ArgumentException">The value is not one of the three keywords.</exception>
    public string? ColorScheme
    {
        get => _colorScheme;
        init => _colorScheme = Keyword(value, _colorSchemes, nameof(ColorScheme));
    }

    /// <summary>
    /// <a href="https://drafts.csswg.org/mediaqueries-5/#prefers-reduced-motion"><c>prefers-reduced-motion</c></a>:
    /// <c>reduce</c> or <c>no-preference</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The value is not one of the two keywords.</exception>
    public string? ReducedMotion
    {
        get => _reducedMotion;
        init => _reducedMotion = Keyword(value, _reducedMotions, nameof(ReducedMotion));
    }

    /// <summary>
    /// <a href="https://drafts.csswg.org/mediaqueries-5/#forced-colors"><c>forced-colors</c></a>:
    /// <c>active</c> or <c>none</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The value is not one of the two keywords.</exception>
    public string? ForcedColors
    {
        get => _forcedColorsValue;
        init => _forcedColorsValue = Keyword(value, _forcedColors, nameof(ForcedColors));
    }

    /// <summary>
    /// <a href="https://drafts.csswg.org/mediaqueries-5/#prefers-contrast"><c>prefers-contrast</c></a>:
    /// <c>no-preference</c>, <c>more</c>, <c>less</c> or <c>custom</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The value is not one of the four keywords.</exception>
    public string? Contrast
    {
        get => _contrast;
        init => _contrast = Keyword(value, _contrasts, nameof(Contrast));
    }

    /// <summary>
    /// The media features this sets or clears, as the CSS names a query writes them under; a feature left
    /// <see langword="null"/> is not listed.
    /// </summary>
    internal IEnumerable<KeyValuePair<string, string>> FeatureChanges()
    {
        if (_colorScheme is not null)
        {
            yield return new("prefers-color-scheme", _colorScheme);
        }

        if (_reducedMotion is not null)
        {
            yield return new("prefers-reduced-motion", _reducedMotion);
        }

        if (_forcedColorsValue is not null)
        {
            yield return new("forced-colors", _forcedColorsValue);
        }

        if (_contrast is not null)
        {
            yield return new("prefers-contrast", _contrast);
        }
    }

    /// <summary>The canonical keyword <paramref name="value"/> names, or the value itself when it is null or empty.</summary>
    private static string? Keyword(string? value, string[] allowed, string property)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        foreach (var keyword in allowed)
        {
            if (string.Equals(keyword, value, StringComparison.OrdinalIgnoreCase))
            {
                return keyword;
            }
        }

        throw new ArgumentException(
            $"'{value}' is not a valid {property}: expected {string.Join(", ", allowed)}, the empty string to clear the override, or null to leave it unchanged.",
            property);
    }
}
