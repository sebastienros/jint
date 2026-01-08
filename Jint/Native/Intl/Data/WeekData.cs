using System.Reflection;

namespace Jint.Native.Intl.Data;

/// <summary>
/// Provides CLDR week data for locale-aware week information.
/// Data is loaded from embedded WeekData.txt resource.
/// </summary>
internal static class WeekData
{
    private static readonly object _lock = new object();
    private static Dictionary<string, string>? _firstDay;
    private static Dictionary<string, int>? _minDays;
    private static Dictionary<string, string>? _weekendStart;
    private static Dictionary<string, string>? _weekendEnd;
    private static volatile bool _loaded;

    /// <summary>
    /// First day of week per region (mon, tue, wed, thu, fri, sat, sun).
    /// Key "001" is the default/world value.
    /// </summary>
    public static Dictionary<string, string> FirstDay
    {
        get
        {
            EnsureLoaded();
            return _firstDay!;
        }
    }

    /// <summary>
    /// Minimum days in first week per region (typically 1 or 4).
    /// Key "001" is the default/world value.
    /// </summary>
    public static Dictionary<string, int> MinDays
    {
        get
        {
            EnsureLoaded();
            return _minDays!;
        }
    }

    /// <summary>
    /// Weekend start day per region.
    /// Key "001" is the default/world value (typically "sat").
    /// </summary>
    public static Dictionary<string, string> WeekendStart
    {
        get
        {
            EnsureLoaded();
            return _weekendStart!;
        }
    }

    /// <summary>
    /// Weekend end day per region.
    /// Key "001" is the default/world value (typically "sun").
    /// </summary>
    public static Dictionary<string, string> WeekendEnd
    {
        get
        {
            EnsureLoaded();
            return _weekendEnd!;
        }
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
        EnsureLoaded();

        if (!string.IsNullOrEmpty(region) && _firstDay!.TryGetValue(region!, out var day))
        {
            return day;
        }

        // Default to world value
        return _firstDay!.TryGetValue("001", out var defaultDay) ? defaultDay : "mon";
    }

    /// <summary>
    /// Gets the minimum days in first week for a region.
    /// </summary>
    public static int GetMinDays(string? region)
    {
        EnsureLoaded();

        if (!string.IsNullOrEmpty(region) && _minDays!.TryGetValue(region!, out var days))
        {
            return days;
        }

        // Default to world value
        return _minDays!.TryGetValue("001", out var defaultDays) ? defaultDays : 1;
    }

    /// <summary>
    /// Gets the weekend days for a region as an array of day numbers (1=Monday, 7=Sunday).
    /// </summary>
    public static int[] GetWeekend(string? region)
    {
        EnsureLoaded();

        string startDay, endDay;

        if (!string.IsNullOrEmpty(region) && _weekendStart!.TryGetValue(region!, out var start))
        {
            startDay = start;
        }
        else
        {
            startDay = _weekendStart!.TryGetValue("001", out var defaultStart) ? defaultStart : "sat";
        }

        if (!string.IsNullOrEmpty(region) && _weekendEnd!.TryGetValue(region!, out var end))
        {
            endDay = end;
        }
        else
        {
            endDay = _weekendEnd!.TryGetValue("001", out var defaultEnd) ? defaultEnd : "sun";
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

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        lock (_lock)
        {
            if (_loaded)
            {
                return;
            }

            _firstDay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _minDays = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _weekendStart = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _weekendEnd = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var assembly = typeof(WeekData).Assembly;
            using var stream = assembly.GetManifestResourceStream("Jint.Native.Intl.Data.WeekData.txt");
            if (stream is null)
            {
                _loaded = true;
                return;
            }

            using var reader = new StreamReader(stream);
            string? currentSection = null;

            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.Length > 2 && line[0] == '[' && line[line.Length - 1] == ']')
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    continue;
                }

                var eqIndex = line.IndexOf('=');
                if (eqIndex < 0)
                {
                    continue;
                }

                var key = line.Substring(0, eqIndex);
                var value = line.Substring(eqIndex + 1);

                switch (currentSection)
                {
                    case "FIRST_DAY":
                        _firstDay[key] = value;
                        break;

                    case "MIN_DAYS":
                        if (int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var minDays))
                        {
                            _minDays[key] = minDays;
                        }
                        break;

                    case "WEEKEND_START":
                        _weekendStart[key] = value;
                        break;

                    case "WEEKEND_END":
                        _weekendEnd[key] = value;
                        break;
                }
            }

            _loaded = true;
        }
    }
}
