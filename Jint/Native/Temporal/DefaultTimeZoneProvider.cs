using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
#if !NET5_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Jint.Native.Temporal;

/// <summary>
/// Default time zone provider using .NET TimeZoneInfo.
/// </summary>
/// <remarks>
/// This provides basic IANA time zone support via .NET's built-in TimeZoneInfo class.
/// On Windows, Windows time zone IDs are mapped to IANA identifiers where possible.
/// For full IANA support, consider using TimeZoneConverter package with a custom provider.
/// </remarks>
public sealed class DefaultTimeZoneProvider : ITimeZoneProvider
{
    private static bool IsWindowsPlatform()
    {
#if NET5_0_OR_GREATER
        return IsWindowsPlatform();
#else
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
    }

    private static readonly BigInteger NanosecondsPerTick = 100;
    private static readonly BigInteger NanosecondsPerMillisecond = 1_000_000;
    private static readonly BigInteger NanosecondsPerSecond = 1_000_000_000;
    private static readonly BigInteger TicksPerNanosecond = 1; // actually 0.01, but we divide by 100

    // Unix epoch in .NET ticks
    private static readonly long UnixEpochTicks = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

    // Cache for resolved time zones
    private readonly ConcurrentDictionary<string, TimeZoneInfo?> _timeZoneCache = new(StringComparer.OrdinalIgnoreCase);

    // Common IANA to Windows mappings (subset for common cases)
    private static readonly Dictionary<string, string> IanaToWindows = new(StringComparer.OrdinalIgnoreCase)
    {
        ["America/New_York"] = "Eastern Standard Time",
        ["America/Chicago"] = "Central Standard Time",
        ["America/Denver"] = "Mountain Standard Time",
        ["America/Los_Angeles"] = "Pacific Standard Time",
        ["America/Anchorage"] = "Alaskan Standard Time",
        ["Pacific/Honolulu"] = "Hawaiian Standard Time",
        ["America/Phoenix"] = "US Mountain Standard Time",
        ["America/Toronto"] = "Eastern Standard Time",
        ["America/Vancouver"] = "Pacific Standard Time",
        ["America/Mexico_City"] = "Central Standard Time (Mexico)",
        ["America/Sao_Paulo"] = "E. South America Standard Time",
        ["America/Buenos_Aires"] = "Argentina Standard Time",
        ["America/Argentina/Buenos_Aires"] = "Argentina Standard Time",
        ["Europe/London"] = "GMT Standard Time",
        ["Europe/Dublin"] = "GMT Standard Time",
        ["Europe/Paris"] = "Romance Standard Time",
        ["Europe/Berlin"] = "W. Europe Standard Time",
        ["Europe/Rome"] = "W. Europe Standard Time",
        ["Europe/Madrid"] = "Romance Standard Time",
        ["Europe/Amsterdam"] = "W. Europe Standard Time",
        ["Europe/Brussels"] = "Romance Standard Time",
        ["Europe/Vienna"] = "W. Europe Standard Time",
        ["Europe/Stockholm"] = "W. Europe Standard Time",
        ["Europe/Oslo"] = "W. Europe Standard Time",
        ["Europe/Copenhagen"] = "Romance Standard Time",
        ["Europe/Helsinki"] = "FLE Standard Time",
        ["Europe/Warsaw"] = "Central European Standard Time",
        ["Europe/Prague"] = "Central Europe Standard Time",
        ["Europe/Budapest"] = "Central Europe Standard Time",
        ["Europe/Athens"] = "GTB Standard Time",
        ["Europe/Bucharest"] = "GTB Standard Time",
        ["Europe/Moscow"] = "Russian Standard Time",
        ["Europe/Kiev"] = "FLE Standard Time",
        ["Europe/Kyiv"] = "FLE Standard Time",
        ["Europe/Istanbul"] = "Turkey Standard Time",
        ["Asia/Tokyo"] = "Tokyo Standard Time",
        ["Asia/Seoul"] = "Korea Standard Time",
        ["Asia/Shanghai"] = "China Standard Time",
        ["Asia/Hong_Kong"] = "China Standard Time",
        ["Asia/Taipei"] = "Taipei Standard Time",
        ["Asia/Singapore"] = "Singapore Standard Time",
        ["Asia/Kolkata"] = "India Standard Time",
        ["Asia/Calcutta"] = "India Standard Time",
        ["Asia/Mumbai"] = "India Standard Time",
        ["Asia/Dubai"] = "Arabian Standard Time",
        ["Asia/Bangkok"] = "SE Asia Standard Time",
        ["Asia/Jakarta"] = "SE Asia Standard Time",
        ["Asia/Manila"] = "Singapore Standard Time",
        ["Asia/Kuala_Lumpur"] = "Singapore Standard Time",
        ["Asia/Jerusalem"] = "Israel Standard Time",
        ["Asia/Tel_Aviv"] = "Israel Standard Time",
        ["Australia/Sydney"] = "AUS Eastern Standard Time",
        ["Australia/Melbourne"] = "AUS Eastern Standard Time",
        ["Australia/Brisbane"] = "E. Australia Standard Time",
        ["Australia/Perth"] = "W. Australia Standard Time",
        ["Australia/Adelaide"] = "Cen. Australia Standard Time",
        ["Australia/Darwin"] = "AUS Central Standard Time",
        ["Pacific/Auckland"] = "New Zealand Standard Time",
        ["Pacific/Fiji"] = "Fiji Standard Time",
        ["Africa/Cairo"] = "Egypt Standard Time",
        ["Africa/Johannesburg"] = "South Africa Standard Time",
        ["Africa/Lagos"] = "W. Central Africa Standard Time",
        ["Africa/Nairobi"] = "E. Africa Standard Time",
        ["Etc/UTC"] = "UTC",
        ["UTC"] = "UTC",
        ["Etc/GMT"] = "UTC",
        ["GMT"] = "UTC",
    };

    /// <summary>
    /// Singleton instance of the default provider.
    /// </summary>
    public static DefaultTimeZoneProvider Instance { get; } = new();

    /// <inheritdoc />
    public long GetOffsetNanosecondsFor(string timeZoneId, BigInteger epochNanoseconds)
    {
        if (string.Equals(timeZoneId, "UTC", StringComparison.Ordinal) ||
            string.Equals(timeZoneId, "Etc/UTC", StringComparison.Ordinal))
        {
            return 0;
        }

        var tz = ResolveTimeZone(timeZoneId);
        if (tz is null)
        {
            throw new ArgumentException($"Unknown time zone: {timeZoneId}", nameof(timeZoneId));
        }

        var epochTicks = (long)(epochNanoseconds / NanosecondsPerTick) + UnixEpochTicks;

        // Clamp to valid .NET DateTime range
        if (epochTicks < DateTime.MinValue.Ticks)
            epochTicks = DateTime.MinValue.Ticks;
        if (epochTicks > DateTime.MaxValue.Ticks)
            epochTicks = DateTime.MaxValue.Ticks;

        var utcDateTime = new DateTime(epochTicks, DateTimeKind.Utc);
        var offset = tz.GetUtcOffset(utcDateTime);

        return (long)offset.TotalMilliseconds * 1_000_000L;
    }

    /// <inheritdoc />
    public BigInteger[] GetPossibleInstantsFor(
        string timeZoneId,
        int year, int month, int day,
        int hour, int minute, int second,
        int millisecond, int microsecond, int nanosecond)
    {
        if (string.Equals(timeZoneId, "UTC", StringComparison.Ordinal) ||
            string.Equals(timeZoneId, "Etc/UTC", StringComparison.Ordinal))
        {
            var utcInstant = DateTimeToEpochNanoseconds(
                year, month, day, hour, minute, second, millisecond, microsecond, nanosecond, TimeSpan.Zero);
            return [utcInstant];
        }

        var tz = ResolveTimeZone(timeZoneId);
        if (tz is null)
        {
            throw new ArgumentException($"Unknown time zone: {timeZoneId}", nameof(timeZoneId));
        }

        // Handle years outside .NET DateTime range
        if (year < 1 || year > 9999)
        {
            // For extreme years, assume standard offset (no DST)
            var standardOffset = tz.BaseUtcOffset;
            var instant = DateTimeToEpochNanoseconds(
                year, month, day, hour, minute, second, millisecond, microsecond, nanosecond, standardOffset);
            return [instant];
        }

        try
        {
            var localDateTime = new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Unspecified);

            if (tz.IsInvalidTime(localDateTime))
            {
                // During spring forward - gap, no valid instants
                return [];
            }

            if (tz.IsAmbiguousTime(localDateTime))
            {
                // During fall back - ambiguous, two valid instants
                var offsets = tz.GetAmbiguousTimeOffsets(localDateTime);
                var results = new BigInteger[offsets.Length];
                for (var i = 0; i < offsets.Length; i++)
                {
                    results[i] = DateTimeToEpochNanoseconds(
                        year, month, day, hour, minute, second, millisecond, microsecond, nanosecond, offsets[i]);
                }
                // Sort by offset (earlier offset = later instant)
                System.Array.Sort(results, (a, b) => b.CompareTo(a));
                return results;
            }

            // Normal case - exactly one instant
            var offset = tz.GetUtcOffset(localDateTime);
            var epochNs = DateTimeToEpochNanoseconds(
                year, month, day, hour, minute, second, millisecond, microsecond, nanosecond, offset);
            return [epochNs];
        }
        catch (ArgumentOutOfRangeException)
        {
            // Invalid date components
            return [];
        }
    }

    /// <inheritdoc />
    public BigInteger? GetNextTransition(string timeZoneId, BigInteger epochNanoseconds)
    {
        if (string.Equals(timeZoneId, "UTC", StringComparison.Ordinal) ||
            string.Equals(timeZoneId, "Etc/UTC", StringComparison.Ordinal))
        {
            return null;
        }

        var tz = ResolveTimeZone(timeZoneId);
        if (tz is null)
        {
            return null;
        }

        // TimeZoneInfo doesn't directly expose transitions, so we approximate
        // by checking adjustment rules
        var adjustmentRules = tz.GetAdjustmentRules();
        if (adjustmentRules.Length == 0)
        {
            return null;
        }

        var epochTicks = (long)(epochNanoseconds / NanosecondsPerTick) + UnixEpochTicks;
        if (epochTicks < DateTime.MinValue.Ticks || epochTicks > DateTime.MaxValue.Ticks)
        {
            return null;
        }

        var currentUtc = new DateTime(epochTicks, DateTimeKind.Utc);

        // Find the next transition - this is an approximation
        foreach (var rule in adjustmentRules)
        {
            if (rule.DateEnd < currentUtc.Date)
                continue;

            var year = rule.DateStart.Year;
            if (currentUtc.Year > year)
                year = currentUtc.Year;

            // Check DST start transition
            var dstStart = GetTransitionDateTime(rule.DaylightTransitionStart, year);
            if (dstStart > currentUtc)
            {
                var transitionTicks = (dstStart.Ticks - UnixEpochTicks);
                return (BigInteger)transitionTicks * NanosecondsPerTick;
            }

            // Check DST end transition
            var dstEnd = GetTransitionDateTime(rule.DaylightTransitionEnd, year);
            if (dstEnd > currentUtc)
            {
                var transitionTicks = (dstEnd.Ticks - UnixEpochTicks);
                return (BigInteger)transitionTicks * NanosecondsPerTick;
            }

            // Try next year
            if (year < rule.DateEnd.Year)
            {
                year++;
                dstStart = GetTransitionDateTime(rule.DaylightTransitionStart, year);
                var transitionTicks = (dstStart.Ticks - UnixEpochTicks);
                return (BigInteger)transitionTicks * NanosecondsPerTick;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public BigInteger? GetPreviousTransition(string timeZoneId, BigInteger epochNanoseconds)
    {
        if (string.Equals(timeZoneId, "UTC", StringComparison.Ordinal) ||
            string.Equals(timeZoneId, "Etc/UTC", StringComparison.Ordinal))
        {
            return null;
        }

        var tz = ResolveTimeZone(timeZoneId);
        if (tz is null)
        {
            return null;
        }

        var adjustmentRules = tz.GetAdjustmentRules();
        if (adjustmentRules.Length == 0)
        {
            return null;
        }

        var epochTicks = (long)(epochNanoseconds / NanosecondsPerTick) + UnixEpochTicks;
        if (epochTicks < DateTime.MinValue.Ticks || epochTicks > DateTime.MaxValue.Ticks)
        {
            return null;
        }

        var currentUtc = new DateTime(epochTicks, DateTimeKind.Utc);
        BigInteger? lastTransition = null;

        foreach (var rule in adjustmentRules)
        {
            if (rule.DateStart > currentUtc.Date)
                break;

            var year = currentUtc.Year;
            if (rule.DateEnd.Year < year)
                year = rule.DateEnd.Year;

            // Check DST transitions in current and previous years
            for (var y = year; y >= rule.DateStart.Year && y >= currentUtc.Year - 1; y--)
            {
                var dstEnd = GetTransitionDateTime(rule.DaylightTransitionEnd, y);
                if (dstEnd < currentUtc)
                {
                    var transitionTicks = (dstEnd.Ticks - UnixEpochTicks);
                    var candidate = (BigInteger)transitionTicks * NanosecondsPerTick;
                    if (lastTransition is null || candidate > lastTransition)
                        lastTransition = candidate;
                }

                var dstStart = GetTransitionDateTime(rule.DaylightTransitionStart, y);
                if (dstStart < currentUtc)
                {
                    var transitionTicks = (dstStart.Ticks - UnixEpochTicks);
                    var candidate = (BigInteger)transitionTicks * NanosecondsPerTick;
                    if (lastTransition is null || candidate > lastTransition)
                        lastTransition = candidate;
                }
            }
        }

        return lastTransition;
    }

    /// <inheritdoc />
    public bool IsValidTimeZone(string timeZoneId)
    {
        return ResolveTimeZone(timeZoneId) is not null;
    }

    /// <inheritdoc />
    public string? CanonicalizeTimeZone(string timeZoneId)
    {
        if (string.IsNullOrEmpty(timeZoneId))
            return null;

        // Handle UTC variants
        if (timeZoneId.Equals("UTC", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("Etc/UTC", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("Etc/GMT", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("GMT", StringComparison.OrdinalIgnoreCase))
        {
            return "UTC";
        }

        var tz = ResolveTimeZone(timeZoneId);
        if (tz is null)
            return null;

        // If on non-Windows, TimeZoneInfo.Id is already IANA
        if (!IsWindowsPlatform())
        {
            return tz.Id;
        }

        // On Windows, try to find IANA ID
        // First check if input was already IANA
        if (IanaToWindows.ContainsKey(timeZoneId))
        {
            return timeZoneId;
        }

        // Try reverse lookup
        foreach (var kvp in IanaToWindows)
        {
            if (kvp.Value.Equals(tz.Id, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Key;
            }
        }

        // Fall back to Windows ID if no IANA mapping found
        return tz.Id;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetAvailableTimeZones()
    {
        var zones = TimeZoneInfo.GetSystemTimeZones();
        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (var zone in zones)
        {
            if (!IsWindowsPlatform())
            {
                // On Unix, IDs are already IANA
                result.Add(zone.Id);
            }
            else
            {
                // On Windows, prefer IANA names from our mapping
                var iana = IanaToWindows.FirstOrDefault(x =>
                    x.Value.Equals(zone.Id, StringComparison.OrdinalIgnoreCase)).Key;
                result.Add(iana ?? zone.Id);
            }
        }

        result.Add("UTC");
        return result.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc />
    public string GetDefaultTimeZone()
    {
        var local = TimeZoneInfo.Local;

        if (!IsWindowsPlatform())
        {
            return local.Id;
        }

        // Try to find IANA equivalent
        foreach (var kvp in IanaToWindows)
        {
            if (kvp.Value.Equals(local.Id, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Key;
            }
        }

        return local.Id;
    }

    private TimeZoneInfo? ResolveTimeZone(string timeZoneId)
    {
        return _timeZoneCache.GetOrAdd(timeZoneId, id =>
        {
            // Handle UTC
            if (id.Equals("UTC", StringComparison.OrdinalIgnoreCase) ||
                id.Equals("Etc/UTC", StringComparison.OrdinalIgnoreCase))
            {
                return TimeZoneInfo.Utc;
            }

            // Try direct lookup first
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Continue to mapping
            }

            // Try IANA to Windows mapping
            if (IsWindowsPlatform() &&
                IanaToWindows.TryGetValue(id, out var windowsId))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }
                catch (TimeZoneNotFoundException)
                {
                    // Continue
                }
            }

            return null;
        });
    }

    private static BigInteger DateTimeToEpochNanoseconds(
        int year, int month, int day,
        int hour, int minute, int second,
        int millisecond, int microsecond, int nanosecond,
        TimeSpan offset)
    {
        // Calculate days since Unix epoch
        var daysSinceEpoch = DaysSinceEpoch(year, month, day);

        // Calculate total nanoseconds
        BigInteger totalNs = daysSinceEpoch;
        totalNs *= 24L * 60 * 60 * NanosecondsPerSecond; // days to ns
        totalNs += (BigInteger)hour * 60 * 60 * NanosecondsPerSecond;
        totalNs += (BigInteger)minute * 60 * NanosecondsPerSecond;
        totalNs += (BigInteger)second * NanosecondsPerSecond;
        totalNs += (BigInteger)millisecond * NanosecondsPerMillisecond;
        totalNs += (BigInteger)microsecond * 1000;
        totalNs += nanosecond;

        // Subtract offset to get UTC
        totalNs -= (BigInteger)(offset.TotalMilliseconds * 1_000_000);

        return totalNs;
    }

    private static long DaysSinceEpoch(int year, int month, int day)
    {
        // Algorithm to calculate days since Unix epoch (1970-01-01)
        // Based on the proleptic Gregorian calendar

        // Adjust for months before March
        int a = (14 - month) / 12;
        int y = year - a;
        int m = month + 12 * a - 3;

        // Calculate Julian day number
        long jdn = day + (153 * m + 2) / 5 + 365L * y + y / 4 - y / 100 + y / 400 - 32045;

        // Unix epoch Julian day number
        const long UnixEpochJdn = 2440588; // 1970-01-01

        return jdn - UnixEpochJdn;
    }

    private static DateTime GetTransitionDateTime(TimeZoneInfo.TransitionTime transition, int year)
    {
        if (transition.IsFixedDateRule)
        {
            return new DateTime(year, transition.Month, transition.Day,
                transition.TimeOfDay.Hour, transition.TimeOfDay.Minute, transition.TimeOfDay.Second);
        }

        // Floating rule (e.g., "second Sunday of March")
        var firstDayOfMonth = new DateTime(year, transition.Month, 1);
        var firstDayOfWeek = (int)firstDayOfMonth.DayOfWeek;
        var targetDayOfWeek = (int)transition.DayOfWeek;

        var daysToAdd = (targetDayOfWeek - firstDayOfWeek + 7) % 7;
        daysToAdd += (transition.Week - 1) * 7;

        // Handle "last week" case (Week == 5)
        if (transition.Week == 5)
        {
            var lastDayOfMonth = DateTime.DaysInMonth(year, transition.Month);
            var candidate = firstDayOfMonth.AddDays(daysToAdd);
            while (candidate.Day > lastDayOfMonth || candidate.Month != transition.Month)
            {
                daysToAdd -= 7;
                candidate = firstDayOfMonth.AddDays(daysToAdd);
            }
        }

        var transitionDate = firstDayOfMonth.AddDays(daysToAdd);
        return new DateTime(transitionDate.Year, transitionDate.Month, transitionDate.Day,
            transition.TimeOfDay.Hour, transition.TimeOfDay.Minute, transition.TimeOfDay.Second);
    }
}
