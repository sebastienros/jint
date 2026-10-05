namespace Jint.Native.Intl.Data;

/// <summary>
/// CLDR's <c>timeData</c>: the hour cycles in common use for date and time formatting in a region, or for one
/// language in a region, most preferred first.
/// </summary>
/// <remarks>
/// <para>
/// Two operations read it, for one locale in the same way: https://tc39.es/ecma402/#sec-hourcyclesoflocale,
/// which is <c>Intl.Locale.prototype.getHourCycles</c>, and the locale data
/// https://tc39.es/ecma402/#sec-createdatetimeformat takes an <c>Intl.DateTimeFormat</c>'s hour cycle from
/// when no option or keyword decides it. So a formatter's default is <c>getHourCycles()[0]</c> of its locale.
/// Both reach it through <see cref="IntlUtilities.GetLocaleHourCycles"/>, which asks a host's
/// <see cref="ICldrProvider.GetHourCycles"/> first, so a host's answer moves both readers together.
/// </para>
/// <para>
/// The data is CLDR 48.2's (see <c>TimeData.Data.cs</c>). Each entry is CLDR's preferred hour format followed
/// by its allowed ones, as hour cycle identifiers and without repeats — the same reading SpiderMonkey makes
/// of the same element — so <c>US</c> is <c>h12</c> then <c>h23</c>, and <c>JP</c> is <c>h23</c>,
/// <c>h11</c>, <c>h12</c>.
/// </para>
/// </remarks>
internal static partial class TimeData
{
    /// <summary>
    /// Step 7 of https://tc39.es/ecma402/#sec-hourcyclesoflocale: the answer for a locale no key matches.
    /// </summary>
    private static readonly string[] Fallback = ["h23"];

    /// <summary>
    /// Steps 3 to 7 of https://tc39.es/ecma402/#sec-hourcyclesoflocale: the hour cycles in common use for
    /// <paramref name="language"/> in the region <paramref name="preference"/> picks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The regions are tried in the algorithm's order — the <c>-u-rg-</c> override, then the preferred
    /// region — and for each one the language joined to it is tried before the region alone, because CLDR
    /// keys a handful of entries that way: <c>fr_CA</c> prefers <c>h23</c> where <c>CA</c> prefers
    /// <c>h12</c>, and <c>en_001</c> prefers <c>h12</c> where <c>001</c> prefers <c>h23</c>.
    /// </para>
    /// <para>
    /// "Time data … are available" is read as "the key is listed", so an override naming a region CLDR has
    /// no entry for (<c>zzzzzz</c>) falls through to the region subtag, and a region with no entry of its own
    /// answers <c>h23</c> alone rather than <c>001</c>'s two cycles: the algorithm falls back to a list,
    /// not to the world's data.
    /// </para>
    /// <para>
    /// The array returned is shared; the caller copies it into a new JavaScript array and never writes to it.
    /// </para>
    /// </remarks>
    internal static string[] GetHourCycles(string language, in RegionPreference preference)
    {
        if (preference.RegionOverride is { } regionOverride && TryGetHourCycles(language, regionOverride, out var hourCycles))
        {
            return hourCycles;
        }

        return TryGetHourCycles(language, preference.Region, out hourCycles) ? hourCycles : Fallback;
    }

    /// <summary>
    /// The hour cycles in common use for <paramref name="locale"/>: its language, in the region
    /// https://tc39.es/ecma402/#sec-regionpreference picks for it.
    /// </summary>
    /// <remarks>
    /// An <c>Intl.Locale</c>'s tag can carry <c>-u-rg-</c> and <c>-u-sd-</c>, and both are honoured. An
    /// <c>Intl.DateTimeFormat</c>'s data locale is the matched available locale of
    /// https://tc39.es/ecma402/#sec-resolvelocale, which carries no Unicode extension: neither keyword is among
    /// the formatter's relevant extension keys, so its region is the tag's own or its likely one.
    /// </remarks>
    internal static string[] GetHourCycles(string locale)
    {
        return GetHourCycles(IntlUtilities.GetLanguageSubtag(locale), RegionPreference.Of(locale));
    }

    /// <summary>
    /// <c>[[LocaleData]].[[&lt;locale&gt;]].[[hourCycle12]]</c> of
    /// https://tc39.es/ecma402/#sec-intl.datetimeformat-internal-slots, the cycle <c>hour12: true</c> resolves
    /// to: the first 12-hour cycle the locale allows, and <c>h12</c> for a locale that allows none.
    /// </summary>
    /// <remarks>
    /// Japan is the one region that allows the 0-11 clock, and it lists it ahead of the 1-12 one, so
    /// <c>ja</c> — likely <c>ja-JP</c> — is on <c>h11</c>, which is what test262's
    /// <c>intl402/DateTimeFormat/prototype/resolvedOptions/hourCycle-default.js</c> expects of it.
    /// </remarks>
    internal static string GetHourCycle12(string[] hourCycles)
    {
        foreach (var hourCycle in hourCycles)
        {
            if (hourCycle is "h11" or "h12")
            {
                return hourCycle;
            }
        }

        return "h12";
    }

    /// <summary>
    /// <c>[[LocaleData]].[[&lt;locale&gt;]].[[hourCycle24]]</c> of
    /// https://tc39.es/ecma402/#sec-intl.datetimeformat-internal-slots, the cycle <c>hour12: false</c> resolves
    /// to: the first 24-hour cycle the locale allows, and <c>h23</c> for a locale that allows none.
    /// </summary>
    /// <remarks>
    /// No region in CLDR 48.2 allows the 1-24 clock, so this answers <c>h23</c> everywhere today. It reads the
    /// table rather than returning that constant so that a later release allowing <c>h24</c> is picked up with it.
    /// </remarks>
    internal static string GetHourCycle24(string[] hourCycles)
    {
        foreach (var hourCycle in hourCycles)
        {
            if (hourCycle is "h23" or "h24")
            {
                return hourCycle;
            }
        }

        return "h23";
    }

    private static bool TryGetHourCycles(string language, string region, out string[] hourCycles)
    {
        return _hourCycles.TryGetValue(string.Concat(language, "_", region), out hourCycles!)
               || _hourCycles.TryGetValue(region, out hourCycles!);
    }
}
