namespace Jint.Native.Intl.Data;

/// <summary>
/// CLDR's <c>timeData</c>: the hour cycles in common use for date and time formatting in a region, or for one
/// language in a region, most preferred first.
/// </summary>
/// <remarks>
/// <para>
/// This is what https://tc39.es/ecma402/#sec-hourcyclesoflocale reads, and nothing else does:
/// <c>Intl.DateTimeFormat</c>'s default hour cycle is still decided by
/// <c>DateTimeFormatPrototype.GetDefaultHourCycle</c>, which does not consult this table.
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

    private static bool TryGetHourCycles(string language, string region, out string[] hourCycles)
    {
        return _hourCycles.TryGetValue(string.Concat(language, "_", region), out hourCycles!)
               || _hourCycles.TryGetValue(region, out hourCycles!);
    }
}
