namespace Jint.Native.Intl.Data;

/// <summary>
/// Provides CLDR week data for locale-aware week information.
/// </summary>
internal static partial class WeekData
{
    /// <summary>
    /// First day of week per region (mon, tue, wed, thu, fri, sat, sun).
    /// Key "001" is the default/world value.
    /// </summary>
    public static Dictionary<string, string> FirstDay => _firstDay;

    /// <summary>
    /// Minimum days in first week per region (typically 1 or 4).
    /// Key "001" is the default/world value.
    /// </summary>
    public static Dictionary<string, int> MinDays => _minDays;

    /// <summary>
    /// Weekend start day per region.
    /// Key "001" is the default/world value (typically "sat").
    /// </summary>
    public static Dictionary<string, string> WeekendStart => _weekendStart;

    /// <summary>
    /// Weekend end day per region.
    /// Key "001" is the default/world value (typically "sun").
    /// </summary>
    public static Dictionary<string, string> WeekendEnd => _weekendEnd;

    /// <summary>
    /// The region https://tc39.es/ecma402/#sec-weekinfooflocale reads its week data for: the
    /// <c>-u-rg-</c> override when CLDR has week data for it, then the preferred region when CLDR has, then
    /// the world.
    /// </summary>
    /// <remarks>
    /// "Available" is read as "named in one of CLDR's <c>weekData</c> tables". The tables list only the
    /// regions that differ from <c>001</c>, so for the preferred region the answer changes nothing - a region
    /// with no entry reads <c>001</c> either way. It matters for the override: <c>en-US-u-rg-zzzzzz</c> names
    /// a region CLDR has no week data for, and so keeps <c>US</c>'s Sunday rather than taking the world's
    /// Monday.
    /// </remarks>
    internal static string GetLookupRegion(in RegionPreference preference)
    {
        if (preference.RegionOverride is { } regionOverride && HasWeekData(regionOverride))
        {
            return regionOverride;
        }

        return HasWeekData(preference.Region) ? preference.Region : RegionPreference.World;
    }

    private static bool HasWeekData(string region)
    {
        return _firstDay.ContainsKey(region)
               || _minDays.ContainsKey(region)
               || _weekendStart.ContainsKey(region)
               || _weekendEnd.ContainsKey(region);
    }

    /// <summary>
    /// Gets the first day of week for a region (1=Monday, 7=Sunday).
    /// </summary>
    public static int GetFirstDayOfWeek(string? region)
    {
        var dayName = GetFirstDayName(region);
        return DayNameToNumber(dayName);
    }

    /// <summary>
    /// Gets the first day of week name for a region.
    /// </summary>
    public static string GetFirstDayName(string? region)
    {
        if (!string.IsNullOrEmpty(region) && _firstDay.TryGetValue(region!, out var day))
        {
            return day;
        }

        // Default to world value
        return _firstDay.TryGetValue("001", out var defaultDay) ? defaultDay : "mon";
    }

    /// <summary>
    /// Gets the minimum days in first week for a region.
    /// </summary>
    public static int GetMinDays(string? region)
    {
        if (!string.IsNullOrEmpty(region) && _minDays.TryGetValue(region!, out var days))
        {
            return days;
        }

        // Default to world value
        return _minDays.TryGetValue("001", out var defaultDays) ? defaultDays : 1;
    }

    /// <summary>
    /// Gets the weekend days for a region as an array of day numbers (1=Monday, 7=Sunday).
    /// </summary>
    public static int[] GetWeekend(string? region)
    {
        string startDay, endDay;

        if (!string.IsNullOrEmpty(region) && _weekendStart.TryGetValue(region!, out var start))
        {
            startDay = start;
        }
        else
        {
            startDay = _weekendStart.TryGetValue("001", out var defaultStart) ? defaultStart : "sat";
        }

        if (!string.IsNullOrEmpty(region) && _weekendEnd.TryGetValue(region!, out var end))
        {
            endDay = end;
        }
        else
        {
            endDay = _weekendEnd.TryGetValue("001", out var defaultEnd) ? defaultEnd : "sun";
        }

        var startNum = DayNameToNumber(startDay);
        var endNum = DayNameToNumber(endDay);

        // Build weekend array
        var weekend = new List<int>();
        var current = startNum;
        while (true)
        {
            weekend.Add(current);
            if (current == endNum)
            {
                break;
            }
            current = current == 7 ? 1 : current + 1;
            if (weekend.Count > 7) // Safety check
            {
                break;
            }
        }

        return weekend.ToArray();
    }

    private static int DayNameToNumber(string dayName)
    {
        return dayName.ToLowerInvariant() switch
        {
            "mon" => 1,
            "tue" => 2,
            "wed" => 3,
            "thu" => 4,
            "fri" => 5,
            "sat" => 6,
            "sun" => 7,
            _ => 1 // Default to Monday
        };
    }

}
