namespace Jint.Native.Intl.Data;

/// <summary>
/// CLDR's <c>calendarPreferenceData</c>, reduced to the one entry ECMA-402 reads: the calendar a region
/// prefers first.
/// </summary>
/// <remarks>
/// <para>
/// https://tc39.es/ecma402/#sec-resolvelocale step 13.c starts the <c>ca</c> key at
/// <c>keyLocaleData[0]</c>, and https://tc39.es/ecma402/#sec-internal-slots says that first element
/// "provid[es] the default value for that key in the locale". CLDR keys the ordering by territory, and its
/// world default — territory <c>001</c> — is <c>gregorian</c>, so only the territories that disagree are
/// listed here; every other region falls through to <see cref="Default"/>.
/// </para>
/// <para>
/// The identifiers are ECMA-402's rather than CLDR's, which differ for exactly one of them: CLDR writes
/// <c>gregorian</c> where https://tc39.es/ecma402/#table-datetimeformat-components and the Unicode
/// <c>-u-ca-</c> grammar write <c>gregory</c>.
/// </para>
/// <para>
/// The rest of each region's ordering — <c>SA</c>'s is
/// <c>islamic-umalqura gregorian islamic islamic-rgsa</c> — is what
/// <c>Intl.Locale.prototype.getCalendars</c> would report, and that method does not read this table yet.
/// </para>
/// </remarks>
internal static class CalendarPreferenceData
{
    /// <summary>CLDR territory <c>001</c>: what a region with no entry of its own prefers.</summary>
    internal const string Default = "gregory";

    private static readonly Dictionary<string, string> _firstPreference = new(4, StringComparer.OrdinalIgnoreCase)
    {
        ["AF"] = "persian",
        ["IR"] = "persian",
        ["SA"] = "islamic-umalqura",
        ["TH"] = "buddhist",
    };

    /// <summary>
    /// The calendar <paramref name="region"/> prefers first, or <see cref="Default"/> for a region CLDR has
    /// no separate entry for — which is most of them.
    /// </summary>
    internal static string GetFirstPreference(string? region)
    {
        if (!string.IsNullOrEmpty(region) && _firstPreference.TryGetValue(region!, out var calendar))
        {
            return calendar;
        }

        return Default;
    }
}
