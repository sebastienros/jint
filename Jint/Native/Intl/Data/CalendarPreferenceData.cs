namespace Jint.Native.Intl.Data;

/// <summary>
/// CLDR's <c>calendarPreferenceData</c>: the calendars in common use in a region, most preferred first.
/// </summary>
/// <remarks>
/// <para>
/// Two readers, one table. <c>Intl.DateTimeFormat</c> takes the first entry as the locale's default
/// calendar: https://tc39.es/ecma402/#sec-resolvelocale step 13.c starts the <c>ca</c> key at
/// <c>keyLocaleData[0]</c>, and https://tc39.es/ecma402/#sec-internal-slots says that first element
/// "provid[es] the default value for that key in the locale". <c>Intl.Locale.prototype.getCalendars</c>
/// reports the whole ordering through https://tc39.es/ecma402/#sec-calendarsoflocale. Reading one table is
/// what keeps <c>getCalendars()[0]</c> and a formatter's default calendar the same answer.
/// </para>
/// <para>
/// CLDR keys the ordering by territory and lists only the territories that differ from its world default,
/// territory <c>001</c>, which is <c>gregory</c> alone; a region with no entry prefers <see cref="Default"/>.
/// The data is CLDR 48.2's (see <c>CalendarPreferenceData.Data.cs</c>), in which three regions put something
/// other than <c>gregory</c> first: <c>AF</c> and <c>IR</c> (<c>persian</c>) and <c>TH</c>
/// (<c>buddhist</c>). <c>SA</c> did too, with <c>islamic-umalqura</c>, until CLDR 46 moved <c>gregorian</c>
/// ahead of it.
/// </para>
/// <para>
/// The orderings are kept as CLDR writes them, <c>islamic</c> and <c>islamic-rgsa</c> included, and a reader
/// filters them against https://tc39.es/ecma402/#sec-availablecalendars — the list those two are not on
/// unless a host provider claims them.
/// </para>
/// </remarks>
internal static partial class CalendarPreferenceData
{
    /// <summary>CLDR territory <c>001</c>: what a region with no entry of its own prefers.</summary>
    internal const string Default = "gregory";

    /// <summary>
    /// The calendar <paramref name="region"/> prefers first, or <see cref="Default"/> for a region CLDR has
    /// no separate entry for — which is most of them.
    /// </summary>
    internal static string GetFirstPreference(string? region)
    {
        if (!string.IsNullOrEmpty(region) && _preferences.TryGetValue(region!, out var calendars))
        {
            return calendars[0];
        }

        return Default;
    }

    /// <summary>
    /// Steps 3 to 6 of https://tc39.es/ecma402/#sec-calendarsoflocale: the calendars in common use in the
    /// region <paramref name="preference"/> picks, most preferred first, or an empty list when CLDR has no
    /// ordering for it. Filtering against the available calendars (steps 7 to 9), and the <c>gregory</c> the
    /// algorithm answers when nothing survives that (step 10), are the caller's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>-u-rg-</c> override is tried first and wins when CLDR lists it, the region otherwise. "Calendar
    /// preference data for region … are available" is read as "the region is listed", the way
    /// <see cref="WeekData.GetLookupRegion"/> reads it for week data, so <c>en-US-u-rg-zzzzzz</c> keeps
    /// <c>US</c>'s ordering. Because CLDR lists only the regions that differ from <c>001</c>, an override
    /// naming a real but unlisted region — <c>th-TH-u-rg-uszzzz</c> — keeps the region subtag's ordering too,
    /// <c>buddhist</c> first. https://github.com/tc39/ecma402/pull/1059, which is still open, would have any
    /// valid region override win instead.
    /// </para>
    /// <para>
    /// The algorithm also looks up the language joined to each region before the region alone. CLDR's
    /// <c>calendarPreferenceData</c> has no entries of that shape, so that lookup could never succeed and is
    /// not made.
    /// </para>
    /// </remarks>
    internal static string[] GetCalendarsInUse(in RegionPreference preference)
    {
        if (preference.RegionOverride is { } regionOverride && _preferences.TryGetValue(regionOverride, out var calendars))
        {
            return calendars;
        }

        return _preferences.TryGetValue(preference.Region, out calendars) ? calendars : [];
    }
}
