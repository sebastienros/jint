#nullable enable

using System.Numerics;
using Jint.Native.Temporal;
using NodaTime;
using NodaTime.TimeZones;

namespace Jint.Tests.Test262;

/// <summary>
/// NodaTime-based time zone provider for accurate IANA timezone support.
/// Provides sub-minute offset precision, precise historical DST transitions,
/// and comprehensive timezone alias resolution.
/// </summary>
internal sealed class NodaTimeZoneProvider : ITimeZoneProvider
{
    private static readonly IDateTimeZoneProvider TzdbProvider = DateTimeZoneProviders.Tzdb;
    private static readonly TzdbDateTimeZoneSource TzdbSource = TzdbDateTimeZoneSource.Default;

    public static NodaTimeZoneProvider Instance { get; } = new();

    private static readonly BigInteger NanosecondsPerTick = 100;
    private static readonly BigInteger NanosecondsPerSecond = 1_000_000_000;

    public long GetOffsetNanosecondsFor(string timeZoneId, BigInteger epochNanoseconds)
    {
        if (string.Equals(timeZoneId, "UTC", StringComparison.Ordinal) ||
            string.Equals(timeZoneId, "Etc/UTC", StringComparison.Ordinal))
        {
            return 0;
        }

        // Handle offset-based time zones
        var parsedOffset = ParseOffsetString(timeZoneId);
        if (parsedOffset.HasValue)
        {
            return (long)parsedOffset.Value.TotalMilliseconds * 1_000_000L;
        }

        var zone = ResolveZone(timeZoneId);
        if (zone is null)
        {
            throw new ArgumentException($"Unknown time zone: {timeZoneId}", nameof(timeZoneId));
        }

        var instant = EpochNanosecondsToInstant(epochNanoseconds);
        var offset = zone.GetUtcOffset(instant);
        return offset.Nanoseconds;
    }

    public BigInteger[] GetPossibleInstantsFor(
        string timeZoneId,
        int year, int month, int day,
        int hour, int minute, int second,
        int millisecond, int microsecond, int nanosecond)
    {
        if (string.Equals(timeZoneId, "UTC", StringComparison.Ordinal) ||
            string.Equals(timeZoneId, "Etc/UTC", StringComparison.Ordinal))
        {
            return [LocalToEpochNanoseconds(year, month, day, hour, minute, second, millisecond, microsecond, nanosecond, Offset.Zero)];
        }

        var parsedOffset = ParseOffsetString(timeZoneId);
        if (parsedOffset.HasValue)
        {
            var msOffset = (long)parsedOffset.Value.TotalMilliseconds;
            var offsetSeconds = (int)(msOffset / 1000);
            var nodaOffset = Offset.FromSeconds(offsetSeconds);
            return [LocalToEpochNanoseconds(year, month, day, hour, minute, second, millisecond, microsecond, nanosecond, nodaOffset)];
        }

        var zone = ResolveZone(timeZoneId);
        if (zone is null)
        {
            throw new ArgumentException($"Unknown time zone: {timeZoneId}", nameof(timeZoneId));
        }

        try
        {
            var localDateTime = new LocalDateTime(year, month, day, hour, minute, second)
                .PlusMilliseconds(millisecond);

            var mapping = zone.MapLocal(localDateTime);

            switch (mapping.Count)
            {
                case 0:
                    // Gap (spring forward) - no valid instants
                    return [];
                case 1:
                {
                    // Unambiguous
                    var instant = mapping.Single();
                    var epochNs = InstantToEpochNanoseconds(instant.ToInstant());
                    // Add sub-millisecond precision
                    epochNs += (BigInteger)microsecond * 1000 + nanosecond;
                    return [epochNs];
                }
                default:
                {
                    // Ambiguous (fall back) - two instants
                    var first = mapping.First();
                    var last = mapping.Last();
                    var epochNs1 = InstantToEpochNanoseconds(first.ToInstant());
                    var epochNs2 = InstantToEpochNanoseconds(last.ToInstant());
                    epochNs1 += (BigInteger)microsecond * 1000 + nanosecond;
                    epochNs2 += (BigInteger)microsecond * 1000 + nanosecond;
                    var results = new[] { epochNs1, epochNs2 };
                    Array.Sort(results);
                    return results;
                }
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            return [];
        }
    }

    public BigInteger? GetNextTransition(string timeZoneId, BigInteger epochNanoseconds)
    {
        if (string.Equals(timeZoneId, "UTC", StringComparison.Ordinal) ||
            string.Equals(timeZoneId, "Etc/UTC", StringComparison.Ordinal))
        {
            return null;
        }

        var zone = ResolveZone(timeZoneId);
        if (zone is null)
        {
            return null;
        }

        var instant = EpochNanosecondsToInstant(epochNanoseconds);
        var interval = zone.GetZoneInterval(instant);

        // If the current interval has an end, that's the next transition
        if (interval.HasEnd)
        {
            return InstantToEpochNanoseconds(interval.End);
        }

        return null;
    }

    public BigInteger? GetPreviousTransition(string timeZoneId, BigInteger epochNanoseconds)
    {
        if (string.Equals(timeZoneId, "UTC", StringComparison.Ordinal) ||
            string.Equals(timeZoneId, "Etc/UTC", StringComparison.Ordinal))
        {
            return null;
        }

        var zone = ResolveZone(timeZoneId);
        if (zone is null)
        {
            return null;
        }

        var instant = EpochNanosecondsToInstant(epochNanoseconds);
        var interval = zone.GetZoneInterval(instant);

        // If the current interval has a start, that's the previous transition
        if (interval.HasStart)
        {
            return InstantToEpochNanoseconds(interval.Start);
        }

        return null;
    }

    public bool IsValidTimeZone(string timeZoneId)
    {
        if (string.IsNullOrEmpty(timeZoneId))
            return false;

        // Check for offset strings
        if (timeZoneId.Length >= 3 && (timeZoneId[0] == '+' || timeZoneId[0] == '-'))
        {
            return IsValidOffsetString(timeZoneId);
        }

        return ResolveZone(timeZoneId) is not null;
    }

    public string? CanonicalizeTimeZone(string timeZoneId)
    {
        if (string.IsNullOrEmpty(timeZoneId))
            return null;

        // Handle UTC variants
        if (timeZoneId.Equals("UTC", StringComparison.OrdinalIgnoreCase))
            return "UTC";
        if (timeZoneId.Equals("Etc/UTC", StringComparison.OrdinalIgnoreCase))
            return "Etc/UTC";
        if (timeZoneId.Equals("Etc/GMT", StringComparison.OrdinalIgnoreCase))
            return "Etc/GMT";
        if (timeZoneId.Equals("GMT", StringComparison.OrdinalIgnoreCase))
            return "GMT";

        // Handle Etc/GMT+N and Etc/GMT-N
        if (timeZoneId.StartsWith("Etc/GMT", StringComparison.OrdinalIgnoreCase) && timeZoneId.Length > 7)
        {
            var suffix = timeZoneId.Substring(7);
            if ((suffix[0] == '+' || suffix[0] == '-') && suffix.Length >= 2 && suffix.Length <= 3)
            {
                if (suffix.Length == 3 && suffix[1] == '0')
                    return null;

                if (int.TryParse(suffix.AsSpan(1), System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out var offset))
                {
                    var maxOffset = suffix[0] == '-' ? 14 : 12;
                    if (offset >= 0 && offset <= maxOffset)
                        return $"Etc/GMT{suffix}";
                }
            }
            return null;
        }

        // Handle offset strings
        if (timeZoneId.Length >= 3 && (timeZoneId[0] == '+' || timeZoneId[0] == '-'))
        {
            return CanonicalizeOffsetString(timeZoneId);
        }

        var zone = ResolveZone(timeZoneId);
        if (zone is null)
            return null;

        // Return the ID as resolved by NodaTime (which preserves IANA casing)
        return zone.Id;
    }

    public IReadOnlyCollection<string> GetAvailableTimeZones()
    {
        return TzdbProvider.Ids.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    public string GetDefaultTimeZone()
    {
        try
        {
            var systemZone = DateTimeZoneProviders.Tzdb.GetSystemDefault();
            return systemZone.Id;
        }
        catch
        {
            return TimeZoneInfo.Local.Id;
        }
    }

    public string? GetPrimaryTimeZoneIdentifier(string timeZoneId)
    {
        if (string.IsNullOrEmpty(timeZoneId))
            return null;

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

        // Offset strings
        if (timeZoneId.Length >= 3 && (timeZoneId[0] == '+' || timeZoneId[0] == '-'))
        {
            return CanonicalizeOffsetString(timeZoneId);
        }

        // Use NodaTime's CanonicalIdMap for alias resolution
        if (TzdbSource.CanonicalIdMap.TryGetValue(timeZoneId, out var canonicalId))
        {
            return canonicalId;
        }

        // Case-insensitive fallback
        foreach (var kvp in TzdbSource.CanonicalIdMap)
        {
            if (string.Equals(kvp.Key, timeZoneId, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        return CanonicalizeTimeZone(timeZoneId);
    }

    private static DateTimeZone? ResolveZone(string timeZoneId)
    {
        try
        {
            return TzdbProvider[timeZoneId];
        }
        catch (DateTimeZoneNotFoundException)
        {
            // Try case-insensitive lookup
            foreach (var id in TzdbProvider.Ids)
            {
                if (string.Equals(id, timeZoneId, StringComparison.OrdinalIgnoreCase))
                {
                    return TzdbProvider[id];
                }
            }
            return null;
        }
    }

    private static Instant EpochNanosecondsToInstant(BigInteger epochNanoseconds)
    {
        // NodaTime Instant supports nanosecond precision
        var ticks = (long)(epochNanoseconds / NanosecondsPerTick);
        // NodaTime epoch is same as Unix epoch for Instant.FromUnixTimeTicks
        return Instant.FromUnixTimeTicks(ticks);
    }

    private static BigInteger InstantToEpochNanoseconds(Instant instant)
    {
        // Convert via ticks to preserve precision
        var ticks = instant.ToUnixTimeTicks();
        return (BigInteger)ticks * NanosecondsPerTick;
    }

    private static BigInteger LocalToEpochNanoseconds(
        int year, int month, int day,
        int hour, int minute, int second,
        int millisecond, int microsecond, int nanosecond,
        Offset offset)
    {
        // Calculate total nanoseconds from epoch, then subtract offset
        var daysSinceEpoch = TemporalHelpers.IsoDateToDays(year, month, day);
        BigInteger totalNs = daysSinceEpoch;
        totalNs *= 24L * 60 * 60 * NanosecondsPerSecond;
        totalNs += (BigInteger)hour * 60 * 60 * NanosecondsPerSecond;
        totalNs += (BigInteger)minute * 60 * NanosecondsPerSecond;
        totalNs += (BigInteger)second * NanosecondsPerSecond;
        totalNs += (BigInteger)millisecond * 1_000_000;
        totalNs += (BigInteger)microsecond * 1000;
        totalNs += nanosecond;
        totalNs -= (BigInteger)offset.Nanoseconds;
        return totalNs;
    }

    private static TimeSpan? ParseOffsetString(string input)
    {
        if (string.IsNullOrEmpty(input) || input.Length < 3)
            return null;

        var sign = input[0];
        if (sign != '+' && sign != '-')
            return null;

        var isNegative = sign == '-';
        int hours, minutes = 0, seconds = 0;

        if (input.Length == 3)
        {
            if (!int.TryParse(input.AsSpan(1, 2), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out hours))
                return null;
        }
        else if (input.Length >= 6 && input[3] == ':')
        {
            if (!int.TryParse(input.AsSpan(1, 2), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out hours) ||
                !int.TryParse(input.AsSpan(4, 2), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out minutes))
                return null;

            if (input.Length >= 9 && input[6] == ':')
            {
                if (!int.TryParse(input.AsSpan(7, 2), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out seconds))
                    return null;
            }
        }
        else if (input.Length >= 5)
        {
            if (!int.TryParse(input.AsSpan(1, 2), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out hours) ||
                !int.TryParse(input.AsSpan(3, 2), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out minutes))
                return null;
        }
        else
        {
            return null;
        }

        if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59 || seconds < 0 || seconds > 59)
            return null;

        var offset = new TimeSpan(hours, minutes, seconds);
        return isNegative ? -offset : offset;
    }

    private static bool IsValidOffsetString(string timeZoneId)
    {
        // Valid formats: +HH (3 chars), +HH:MM (6 chars), +HHMM (5 chars)
        if (timeZoneId.Length == 3 || timeZoneId.Length == 5 || (timeZoneId.Length == 6 && timeZoneId[3] == ':'))
        {
            var parsed = ParseOffsetString(timeZoneId);
            if (parsed.HasValue)
            {
                var totalMinutes = Math.Abs(parsed.Value.TotalMinutes);
                return totalMinutes <= 23 * 60 + 59;
            }
        }
        return false;
    }

    private static string? CanonicalizeOffsetString(string timeZoneId)
    {
        if (timeZoneId.Length == 3 && char.IsDigit(timeZoneId[1]) && char.IsDigit(timeZoneId[2]))
        {
            var parsed = ParseOffsetString(timeZoneId);
            if (parsed.HasValue)
            {
                var totalMinutes = Math.Abs(parsed.Value.TotalMinutes);
                if (totalMinutes <= 23 * 60 + 59)
                    return $"{timeZoneId}:00";
            }
        }
        else if (timeZoneId.Length == 6 && timeZoneId[3] == ':')
        {
            var parsed = ParseOffsetString(timeZoneId);
            if (parsed.HasValue)
            {
                var totalMinutes = Math.Abs(parsed.Value.TotalMinutes);
                if (totalMinutes <= 23 * 60 + 59)
                    return timeZoneId;
            }
        }
        else if (timeZoneId.Length == 5 && char.IsDigit(timeZoneId[3]))
        {
            var parsed = ParseOffsetString(timeZoneId);
            if (parsed.HasValue)
            {
                var totalMinutes = Math.Abs(parsed.Value.TotalMinutes);
                if (totalMinutes <= 23 * 60 + 59)
                    return $"{timeZoneId.Substring(0, 3)}:{timeZoneId.Substring(3)}";
            }
        }
        return null;
    }
}
