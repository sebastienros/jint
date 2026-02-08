using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
#if NETSTANDARD
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
        return OperatingSystem.IsWindows();
#elif NETSTANDARD
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#else
        // net462 only runs on Windows
        return true;
#endif
    }

    private static readonly BigInteger NanosecondsPerTick = 100;
    private static readonly BigInteger NanosecondsPerMillisecond = 1_000_000;
    private static readonly BigInteger NanosecondsPerSecond = 1_000_000_000;

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
        ["CET"] = "Romance Standard Time", // IANA backward compatibility link
        ["EET"] = "FLE Standard Time", // Eastern European Time (UTC+2)
        ["MET"] = "Romance Standard Time", // Middle European Time (UTC+1)
        ["WET"] = "GMT Standard Time", // Western European Time (UTC+0)
        ["America/Ciudad_Juarez"] = "US Mountain Standard Time", // Added in TZDB 2022g
        ["Antarctica/Troll"] = "W. Europe Standard Time", // UTC+0/+2 (approximation)
        ["Antarctica/Vostok"] = "Qyzylorda Standard Time", // UTC+5 (since Dec 2023)
        ["Asia/Urumqi"] = "Central Asia Standard Time", // UTC+6
        ["Asia/Kashgar"] = "Central Asia Standard Time", // Link to Asia/Urumqi
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

    // Case-insensitive lookup mapping IANA IDs to their canonical casing.
    // Built from IanaToWindows keys at static init time.
    private static readonly Dictionary<string, string> IanaCanonicalCasing = BuildCanonicalCasingDict();

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

        // For offset-based time zones (e.g., +01:00, -05:30), return the parsed offset directly
        // This handles sub-minute precision that .NET TimeZoneInfo doesn't support
        var parsedOffset = ParseOffsetString(timeZoneId);
        if (parsedOffset.HasValue)
        {
            return (long) parsedOffset.Value.TotalMilliseconds * 1_000_000L;
        }

        var tz = ResolveTimeZone(timeZoneId);
        if (tz is null)
        {
            throw new ArgumentException($"Unknown time zone: {timeZoneId}", nameof(timeZoneId));
        }

        var bigTicks = epochNanoseconds / NanosecondsPerTick + UnixEpochTicks;

        // Clamp to valid .NET DateTime range (using BigInteger comparison to avoid overflow)
        long epochTicks;
        if (bigTicks < DateTime.MinValue.Ticks)
            epochTicks = DateTime.MinValue.Ticks;
        else if (bigTicks > DateTime.MaxValue.Ticks)
            epochTicks = DateTime.MaxValue.Ticks;
        else
            epochTicks = (long) bigTicks;

        var utcDateTime = new DateTime(epochTicks, DateTimeKind.Utc);
        var offset = tz.GetUtcOffset(utcDateTime);

        return (long) offset.TotalMilliseconds * 1_000_000L;
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

        // Handle offset-based time zones (e.g., +01:00, -05:30)
        // Per spec, GetPossibleEpochNanoseconds for offset timezones returns a single instant
        var parsedOffset = ParseOffsetString(timeZoneId);
        if (parsedOffset.HasValue)
        {
            var offsetInstant = DateTimeToEpochNanoseconds(
                year, month, day, hour, minute, second, millisecond, microsecond, nanosecond, parsedOffset.Value);
            return [offsetInstant];
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

                // Sort ascending (earliest epoch nanosecond first) per spec
                System.Array.Sort(results);
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

        var bigTicksNext = epochNanoseconds / NanosecondsPerTick + UnixEpochTicks;
        if (bigTicksNext < DateTime.MinValue.Ticks || bigTicksNext > DateTime.MaxValue.Ticks)
        {
            return null;
        }

        var currentUtc = new DateTime((long) bigTicksNext, DateTimeKind.Utc);

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
                return (BigInteger) transitionTicks * NanosecondsPerTick;
            }

            // Check DST end transition
            var dstEnd = GetTransitionDateTime(rule.DaylightTransitionEnd, year);
            if (dstEnd > currentUtc)
            {
                var transitionTicks = (dstEnd.Ticks - UnixEpochTicks);
                return (BigInteger) transitionTicks * NanosecondsPerTick;
            }

            // Try next year
            if (year < rule.DateEnd.Year)
            {
                year++;
                dstStart = GetTransitionDateTime(rule.DaylightTransitionStart, year);
                var transitionTicks = (dstStart.Ticks - UnixEpochTicks);
                return (BigInteger) transitionTicks * NanosecondsPerTick;
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

        var bigTicksPrev = epochNanoseconds / NanosecondsPerTick + UnixEpochTicks;
        if (bigTicksPrev < DateTime.MinValue.Ticks || bigTicksPrev > DateTime.MaxValue.Ticks)
        {
            return null;
        }

        var currentUtc = new DateTime((long) bigTicksPrev, DateTimeKind.Utc);
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
                    var candidate = (BigInteger) transitionTicks * NanosecondsPerTick;
                    if (lastTransition is null || candidate > lastTransition)
                        lastTransition = candidate;
                }

                var dstStart = GetTransitionDateTime(rule.DaylightTransitionStart, y);
                if (dstStart < currentUtc)
                {
                    var transitionTicks = (dstStart.Ticks - UnixEpochTicks);
                    var candidate = (BigInteger) transitionTicks * NanosecondsPerTick;
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
        if (string.IsNullOrEmpty(timeZoneId))
            return false;

        // Check for offset strings
        // Valid formats: +HH (3 chars), +HH:MM (6 chars), or +HHMM (5 chars) - no seconds allowed
        if (timeZoneId.Length >= 3 && (timeZoneId[0] == '+' || timeZoneId[0] == '-'))
        {
            // Check if it's a valid HH, HH:MM, or HHMM format (no seconds allowed)
            if (timeZoneId.Length == 3 && char.IsDigit(timeZoneId[1]) && char.IsDigit(timeZoneId[2]))
            {
                // +HH format (hour only)
                var parsedOffset = ParseOffsetString(timeZoneId);
                if (parsedOffset.HasValue)
                {
                    var totalMinutes = System.Math.Abs(parsedOffset.Value.TotalMinutes);
                    return totalMinutes <= 23 * 60 + 59;
                }
            }
            else if (timeZoneId.Length == 6 && timeZoneId[3] == ':')
            {
                // +HH:MM format
                var parsedOffset = ParseOffsetString(timeZoneId);
                if (parsedOffset.HasValue)
                {
                    var totalMinutes = System.Math.Abs(parsedOffset.Value.TotalMinutes);
                    return totalMinutes <= 23 * 60 + 59;
                }
            }
            else if (timeZoneId.Length == 5 && char.IsDigit(timeZoneId[3]))
            {
                // +HHMM format
                var parsedOffset = ParseOffsetString(timeZoneId);
                if (parsedOffset.HasValue)
                {
                    var totalMinutes = System.Math.Abs(parsedOffset.Value.TotalMinutes);
                    return totalMinutes <= 23 * 60 + 59;
                }
            }

            // Any other format starting with +/- is an offset with invalid precision
            return false;
        }

        return ResolveTimeZone(timeZoneId) is not null;
    }

    /// <inheritdoc />
    public string? CanonicalizeTimeZone(string timeZoneId)
    {
        if (string.IsNullOrEmpty(timeZoneId))
            return null;

        // Handle UTC variants - preserve the original identifier per spec
        // TimeZoneEquals handles comparing different UTC names
        if (timeZoneId.Equals("UTC", StringComparison.OrdinalIgnoreCase))
        {
            return "UTC";
        }

        if (timeZoneId.Equals("Etc/UTC", StringComparison.OrdinalIgnoreCase))
        {
            return "Etc/UTC";
        }

        if (timeZoneId.Equals("Etc/GMT", StringComparison.OrdinalIgnoreCase))
        {
            return "Etc/GMT";
        }

        if (timeZoneId.Equals("GMT", StringComparison.OrdinalIgnoreCase))
        {
            return "GMT";
        }

        // Handle Etc/GMT+N and Etc/GMT-N timezone identifiers (case-insensitive)
        // These are IANA names, not offset strings. Valid: Etc/GMT-0..Etc/GMT-14, Etc/GMT+0..Etc/GMT+12
        // Invalid: zero-padded like Etc/GMT-01, Etc/GMT+00, out of range like Etc/GMT-24
        if (timeZoneId.StartsWith("Etc/GMT", StringComparison.OrdinalIgnoreCase) && timeZoneId.Length > 7)
        {
            var suffix = timeZoneId.Substring(7);
            if ((suffix[0] == '+' || suffix[0] == '-') && suffix.Length >= 2 && suffix.Length <= 3)
            {
                // Reject zero-padded: if 2 digits and first is 0 (e.g., +01, -09, +00)
                if (suffix.Length == 3 && suffix[1] == '0')
                {
                    return null;
                }

                if (int.TryParse(suffix.AsSpan(1), System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out var offset))
                {
                    var maxOffset = suffix[0] == '-' ? 14 : 12;
                    if (offset >= 0 && offset <= maxOffset)
                    {
                        return $"Etc/GMT{suffix}";
                    }
                }
            }

            return null;
        }

        // Handle offset strings
        // Valid formats: +HH (3 chars), +HH:MM (6 chars), or +HHMM (5 chars) - no seconds allowed
        if (timeZoneId.Length >= 3 && (timeZoneId[0] == '+' || timeZoneId[0] == '-'))
        {
            // Check if it's a valid HH, HH:MM, or HHMM format (no seconds allowed)
            if (timeZoneId.Length == 3 && char.IsDigit(timeZoneId[1]) && char.IsDigit(timeZoneId[2]))
            {
                // +HH format - canonicalize to +HH:00
                var parsedOffset = ParseOffsetString(timeZoneId);
                if (parsedOffset.HasValue)
                {
                    var totalMinutes = System.Math.Abs(parsedOffset.Value.TotalMinutes);
                    if (totalMinutes <= 23 * 60 + 59)
                        return $"{timeZoneId}:00";
                }
            }
            else if (timeZoneId.Length == 6 && timeZoneId[3] == ':')
            {
                // +HH:MM format
                var parsedOffset = ParseOffsetString(timeZoneId);
                if (parsedOffset.HasValue)
                {
                    var totalMinutes = System.Math.Abs(parsedOffset.Value.TotalMinutes);
                    if (totalMinutes <= 23 * 60 + 59)
                        return timeZoneId;
                }
            }
            else if (timeZoneId.Length == 5 && char.IsDigit(timeZoneId[3]))
            {
                // +HHMM format - canonicalize to +HH:MM
                var parsedOffset = ParseOffsetString(timeZoneId);
                if (parsedOffset.HasValue)
                {
                    var totalMinutes = System.Math.Abs(parsedOffset.Value.TotalMinutes);
                    if (totalMinutes <= 23 * 60 + 59)
                    {
                        // Insert colon between hours and minutes
                        return $"{timeZoneId.Substring(0, 3)}:{timeZoneId.Substring(3)}";
                    }
                }
            }

            // Any other format starting with +/- is an offset with invalid precision
            return null;
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
        // First check if input was already IANA (case-insensitive match)
        if (IanaCanonicalCasing.TryGetValue(timeZoneId, out var canonicalIana))
        {
            return canonicalIana;
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

    /// <summary>
    /// Gets the primary IANA identifier for a timezone, resolving aliases.
    /// E.g., "Asia/Calcutta" → "Asia/Kolkata", "America/Atka" → "America/Adak".
    /// </summary>
    public string? GetPrimaryTimeZoneIdentifier(string timeZoneId)
    {
        if (string.IsNullOrEmpty(timeZoneId))
        {
            return null;
        }

        // UTC variants all map to "UTC"
        if (timeZoneId.Equals("UTC", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("Etc/UTC", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("Etc/GMT", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("Etc/UCT", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("Etc/GMT0", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("Etc/GMT+0", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("Etc/GMT-0", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("Etc/Greenwich", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("Etc/Universal", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("Etc/Zulu", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("UCT", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("GMT", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("GMT+0", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("GMT-0", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("GMT0", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("Greenwich", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("Universal", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("Zulu", StringComparison.OrdinalIgnoreCase))
        {
            return "UTC";
        }

        // Offset strings are their own primary identifier
        if (timeZoneId.Length >= 3 && (timeZoneId[0] == '+' || timeZoneId[0] == '-'))
        {
            return CanonicalizeTimeZone(timeZoneId);
        }

#if NET6_0_OR_GREATER
        // Use .NET's IANA→Windows mapping to find the primary identifier
        // Both "Asia/Calcutta" and "Asia/Kolkata" map to "India Standard Time"
        // Then convert back to get the primary IANA name
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsId))
        {
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(windowsId, out var primaryId))
            {
                return primaryId;
            }
        }
#endif

        // Fallback: resolve and return canonicalized form
        return CanonicalizeTimeZone(timeZoneId);
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

            // Handle offset strings (e.g., +01:00, -05:30, +01:30:00)
            var offset = ParseOffsetString(id);
            if (offset.HasValue)
            {
                // .NET TimeZoneInfo only supports whole minute offsets, but we need to allow
                // seconds precision for Temporal. We'll create a custom TimeZoneInfo with
                // the offset rounded to minutes, but GetOffsetNanosecondsFor will return the exact offset.
                //
                // IMPORTANT: TimeZoneInfo.CreateCustomTimeZone only accepts offsets within ±14 hours,
                // but ISO 8601/Temporal allows ±23:59. We clamp to ±14 hours for the TimeZoneInfo,
                // but GetOffsetNanosecondsFor will still return the actual parsed offset.
                var totalMinutes = (int) offset.Value.TotalMinutes;
                const int MaxOffsetMinutes = 14 * 60; // ±14 hours
                if (totalMinutes > MaxOffsetMinutes)
                    totalMinutes = MaxOffsetMinutes;
                else if (totalMinutes < -MaxOffsetMinutes)
                    totalMinutes = -MaxOffsetMinutes;

                var roundedOffset = TimeSpan.FromMinutes(totalMinutes);
                return TimeZoneInfo.CreateCustomTimeZone(id, roundedOffset, id, id);
            }

            // On Windows, .NET resolves non-IANA identifiers (Java abbreviations like ACT, BST, etc.)
            // Reject identifiers that don't follow IANA naming conventions
            if (!IsValidIanaIdentifier(id))
            {
                return null;
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

            // Try IANA to Windows mapping (case-insensitive via dictionary comparer)
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

#if NET6_0_OR_GREATER
            // Case-insensitive fallback: try all system timezones
            foreach (var systemTz in TimeZoneInfo.GetSystemTimeZones())
            {
                if (string.Equals(systemTz.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return systemTz;
                }
            }
#endif

            return null;
        });
    }

    /// <summary>
    /// Parses an offset string like "+01:00" or "-05:30" to a TimeSpan.
    /// </summary>
    private static TimeSpan? ParseOffsetString(string input)
    {
        if (string.IsNullOrEmpty(input) || input.Length < 3)
            return null;

        // Check for +/- prefix
        var sign = input[0];
        if (sign != '+' && sign != '-')
            return null;

        var isNegative = sign == '-';

        // Parse HH, HH:MM, or HHMM format
        int hours, minutes = 0, seconds = 0;

        if (input.Length == 3)
        {
            // +HH format (hour only)
            if (!int.TryParse(input.AsSpan(1, 2), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out hours))
            {
                return null;
            }
        }
        else if (input.Length >= 6 && input[3] == ':')
        {
            // +HH:MM format
            if (!int.TryParse(input.AsSpan(1, 2), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out hours) ||
                !int.TryParse(input.AsSpan(4, 2), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out minutes))
            {
                return null;
            }

            // Check for optional seconds +HH:MM:SS
            if (input.Length >= 9 && input[6] == ':')
            {
                if (!int.TryParse(input.AsSpan(7, 2), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out seconds))
                {
                    return null;
                }
            }
        }
        else if (input.Length >= 5)
        {
            // +HHMM format
            if (!int.TryParse(input.AsSpan(1, 2), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out hours) ||
                !int.TryParse(input.AsSpan(3, 2), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out minutes))
            {
                return null;
            }
        }
        else
        {
            return null;
        }

        // Validate range
        if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59 || seconds < 0 || seconds > 59)
            return null;

        var offset = new TimeSpan(hours, minutes, seconds);
        return isNegative ? -offset : offset;
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
        totalNs += (BigInteger) hour * 60 * 60 * NanosecondsPerSecond;
        totalNs += (BigInteger) minute * 60 * NanosecondsPerSecond;
        totalNs += (BigInteger) second * NanosecondsPerSecond;
        totalNs += (BigInteger) millisecond * NanosecondsPerMillisecond;
        totalNs += (BigInteger) microsecond * 1000;
        totalNs += nanosecond;

        // Subtract offset to get UTC
        totalNs -= (BigInteger) (offset.TotalMilliseconds * 1_000_000);

        return totalNs;
    }

    private static Dictionary<string, string> BuildCanonicalCasingDict()
    {
        var dict = new Dictionary<string, string>(IanaToWindows.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var key in IanaToWindows.Keys)
        {
            dict[key] = key;
        }

#if NET6_0_OR_GREATER
        // Also add system timezone IDs (on .NET 6+ these include IANA IDs)
        foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
        {
            if (!dict.ContainsKey(tz.Id))
            {
                dict[tz.Id] = tz.Id;
            }
        }
#endif

        return dict;
    }

    private static long DaysSinceEpoch(int year, int month, int day)
    {
        // Use the same algorithm as TemporalHelpers.IsoDateToDays for consistency
        return TemporalHelpers.IsoDateToDays(year, month, day);
    }

    /// <summary>
    /// Validates that a timezone identifier follows IANA naming conventions.
    /// Rejects Java/ICU-specific abbreviations like ACT, BST, JST that .NET may resolve.
    /// </summary>
    private static bool IsValidIanaIdentifier(string id)
    {
        // IDs containing '/' are Area/Location format (always valid IANA)
        if (id.Contains('/'))
        {
            return true;
        }

        // IDs containing digits with letters (like EST5EDT, CST6CDT) are IANA compound TZ names
        // Known single-word IANA identifiers (Zone and Link names from TZDB)
        return s_validSingleWordIanaIds.Contains(id);
    }

    private static readonly HashSet<string> s_validSingleWordIanaIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "CET", "CST6CDT", "EET", "EST", "EST5EDT", "HST",
        "MET", "MST", "MST7MDT", "PST8PDT", "WET",
        "GMT", "GMT0", "GMT+0", "GMT-0", "UTC", "UCT",
        "Universal", "Greenwich", "Zulu", "W-SU",
        "Cuba", "Egypt", "Eire", "GB", "GB-Eire",
        "Hongkong", "Iceland", "Iran", "Israel", "Jamaica",
        "Japan", "Kwajalein", "Libya", "NZ", "NZ-CHAT",
        "Navajo", "PRC", "Poland", "Portugal", "ROC", "ROK",
        "Singapore", "Turkey",
    };

    private static DateTime GetTransitionDateTime(TimeZoneInfo.TransitionTime transition, int year)
    {
        if (transition.IsFixedDateRule)
        {
            return new DateTime(year, transition.Month, transition.Day,
                transition.TimeOfDay.Hour, transition.TimeOfDay.Minute, transition.TimeOfDay.Second);
        }

        // Floating rule (e.g., "second Sunday of March")
        var firstDayOfMonth = new DateTime(year, transition.Month, 1);
        var firstDayOfWeek = (int) firstDayOfMonth.DayOfWeek;
        var targetDayOfWeek = (int) transition.DayOfWeek;

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
