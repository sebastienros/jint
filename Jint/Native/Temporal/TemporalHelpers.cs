using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Temporal;

/// <summary>
/// Shared helper functions for Temporal types.
/// </summary>
internal static class TemporalHelpers
{
    /// <summary>
    /// Formats an ISO year as 4-digit (0000-9999) or 6-digit with sign prefix.
    /// </summary>
    internal static string PadIsoYear(int year)
    {
        if (year < 0 || year > 9999)
        {
            return $"{(year >= 0 ? '+' : '-')}{System.Math.Abs(year):D6}";
        }

        return $"{year:D4}";
    }

    // Polyfill for Math.Clamp which doesn't exist in netstandard2.0
    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    /// <summary>
    /// Validates that a string doesn't contain non-ASCII minus sign (U+2212).
    /// Only ASCII hyphen-minus (U+002D) is valid in ISO 8601 strings.
    /// </summary>
    public static bool ContainsInvalidMinusSign(string str)
    {
        return str.Contains('\u2212');
    }

    // Constants for nanosecond calculations
    public const long NanosecondsPerMicrosecond = 1_000L;
    public const long NanosecondsPerMillisecond = 1_000_000L;
    public const long NanosecondsPerSecond = 1_000_000_000L;
    public const long NanosecondsPerMinute = 60_000_000_000L;
    public const long NanosecondsPerHour = 3_600_000_000_000L;
    public const long NanosecondsPerDay = 86_400_000_000_000L;

    // maxTimeDuration = 2^53 × 10^9 - 1 = 9,007,199,254,740,991,999,999,999
    // This is the maximum time duration in nanoseconds
    public static readonly BigInteger MaxTimeDuration = BigInteger.Parse("9007199254740991999999999", CultureInfo.InvariantCulture);

    // Time conversion helpers
    public static long TimeToNanoseconds(IsoTime time)
    {
        return (long) time.Hour * NanosecondsPerHour +
               (long) time.Minute * NanosecondsPerMinute +
               (long) time.Second * NanosecondsPerSecond +
               (long) time.Millisecond * NanosecondsPerMillisecond +
               (long) time.Microsecond * NanosecondsPerMicrosecond +
               time.Nanosecond;
    }

    public static IsoTime NanosecondsToTime(long nanoseconds)
    {
        var hour = (int) (nanoseconds / NanosecondsPerHour);
        nanoseconds %= NanosecondsPerHour;
        var minute = (int) (nanoseconds / NanosecondsPerMinute);
        nanoseconds %= NanosecondsPerMinute;
        var second = (int) (nanoseconds / NanosecondsPerSecond);
        nanoseconds %= NanosecondsPerSecond;
        var millisecond = (int) (nanoseconds / NanosecondsPerMillisecond);
        nanoseconds %= NanosecondsPerMillisecond;
        var microsecond = (int) (nanoseconds / NanosecondsPerMicrosecond);
        var nanosecondRemainder = (int) (nanoseconds % NanosecondsPerMicrosecond);

        return new IsoTime(hour, minute, second, millisecond, microsecond, nanosecondRemainder);
    }

    public static IsoDateTime EpochNanosecondsToIsoDateTime(BigInteger epochNs)
    {
        // Days since Unix epoch
        var days = (long) (epochNs / NanosecondsPerDay);
        var remaining = (long) (epochNs % NanosecondsPerDay);
        if (remaining < 0)
        {
            days--;
            remaining += NanosecondsPerDay;
        }

        // Convert days to date
        var date = DaysToIsoDate(days);

        // Convert remaining nanoseconds to time
        var hour = (int) (remaining / NanosecondsPerHour);
        remaining %= NanosecondsPerHour;
        var minute = (int) (remaining / NanosecondsPerMinute);
        remaining %= NanosecondsPerMinute;
        var second = (int) (remaining / NanosecondsPerSecond);
        remaining %= NanosecondsPerSecond;
        var millisecond = (int) (remaining / NanosecondsPerMillisecond);
        remaining %= NanosecondsPerMillisecond;
        var microsecond = (int) (remaining / NanosecondsPerMicrosecond);
        var nanosecond = (int) (remaining % NanosecondsPerMicrosecond);

        return new IsoDateTime(date, new IsoTime(hour, minute, second, millisecond, microsecond, nanosecond));
    }

    // Regex patterns for ISO 8601 parsing
    // Note: Adding timeout for safety against ReDoS attacks
#pragma warning disable MA0023 // Use RegexOptions.ExplicitCapture - we need numbered capture groups
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    // ISO 8601 date pattern: supports both extended (YYYY-MM-DD) and basic (YYYYMMDD) formats
    private static readonly Regex DatePattern = new(
        @"^([+-]?\d{4,6})(?:-(\d{2})-(\d{2})|(\d{2})(\d{2}))$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    // Time pattern supporting multiple formats per spec TimeSpec grammar:
    // Hour | Hour TimeSeparator MinuteSecond | Hour TimeSeparator MinuteSecond TimeSeparator TimeSecond [.fraction]
    // Colon separator: HH, HH:MM, HH:MM:SS.sss (dot or comma for fractions)
    // Hyphen separator: HH, HH-MM, HH-MM-SS.sss (dot or comma for fractions)
    // Compact (no separator): HH, HHMM, HHMMSS.sss (dot or comma for fractions)
    private static readonly Regex TimePatternColon = new(
        @"^(\d{2})(?::(\d{2})(?::(\d{2})(?:[.,](\d{1,9}))?)?)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex TimePatternHyphen = new(
        @"^(\d{2})(?:-(\d{2})(?:-(\d{2})(?:[.,](\d{1,9}))?)?)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex TimePatternCompact = new(
        @"^(\d{2})(?:(\d{2})(?:(\d{2})(?:[.,](\d{1,9}))?)?)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    // Duration pattern already supports comma via [.,]
    private static readonly Regex DurationPattern = new(
        @"^([+-])?P(?:(\d+)Y)?(?:(\d+)M)?(?:(\d+)W)?(?:(\d+)D)?(?:T(?:(\d+)(?:[.,](\d{1,9}))?H)?(?:(\d+)(?:[.,](\d{1,9}))?M)?(?:(\d+)(?:[.,](\d{1,9}))?S)?)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        RegexTimeout);

    // Instant pattern already supports comma via [.,]
    private static readonly Regex InstantPattern = new(
        @"^([+-]\d{6}|\d{4})-?(\d{2})-?(\d{2})[Tt ](\d{2})(?::?(\d{2})(?::?(\d{2})(?:[.,](\d{1,9}))?)?)?([Zz]|([+-])(\d{2})(?::?(\d{2})(?::?(\d{2})(?:[.,](\d{1,9}))?)?)?)((?:\[!?[^\]]+\])*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);
#pragma warning restore MA0023

    /// <summary>
    /// Parses an offset string like "+01:00" or "-05:30" to nanoseconds.
    /// </summary>
    public static long? ParseOffsetString(string input)
    {
        if (string.IsNullOrEmpty(input) || input.Length < 3)
            return null;

        // Check for +/- prefix
        var sign = input[0];
        if (sign != '+' && sign != '-')
            return null;

        var isNegative = sign == '-';

        // Parse HH:MM, HHMM, or HH format
        int hours, minutes = 0, seconds = 0;
        long nanoseconds = 0;

        // Check for hour-only format (e.g., "-08")
        if (input.Length == 3)
        {
            if (!int.TryParse(input.AsSpan(1, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out hours))
            {
                return null;
            }
        }
        else if (input.Length >= 6 && input[3] == ':')
        {
            // +HH:MM format
            if (!int.TryParse(input.AsSpan(1, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out hours) ||
                !int.TryParse(input.AsSpan(4, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes))
            {
                return null;
            }

            // Check for optional seconds +HH:MM:SS[.fffffffff]
            if (input.Length > 6)
            {
                // If there are more characters after +HH:MM, they must be :SS or :SS.fffffffff
                if (input.Length < 9 || input[6] != ':')
                {
                    return null; // Invalid format - extra characters without proper separator
                }

                if (!int.TryParse(input.AsSpan(7, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
                {
                    return null;
                }

                // Check for fractional seconds after :SS
                if (input.Length > 9)
                {
                    // Must have a decimal separator (. or ,)
                    if (input[9] != '.' && input[9] != ',')
                    {
                        return null;
                    }

                    // Parse fractional seconds (up to 9 digits for nanoseconds)
                    var fractionStart = 10;
                    var fractionEnd = input.Length;
                    var fractionLength = fractionEnd - fractionStart;

                    if (fractionLength < 1 || fractionLength > 9)
                    {
                        return null;
                    }

                    if (!int.TryParse(input.AsSpan(fractionStart, fractionLength), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fractionValue))
                    {
                        return null;
                    }

                    // Convert fraction to nanoseconds (pad with zeros to the right to make 9 digits)
                    nanoseconds = fractionValue * (long) System.Math.Pow(10, 9 - fractionLength);
                }
            }
        }
        else if (input.Length >= 5)
        {
            // +HHMM format (no colons)
            if (!int.TryParse(input.AsSpan(1, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out hours) ||
                !int.TryParse(input.AsSpan(3, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes))
            {
                return null;
            }

            // Check for optional seconds +HHMMSS (must be exactly 7 characters total)
            if (input.Length > 5)
            {
                if (input.Length != 7)
                {
                    return null; // Invalid format - must be exactly +HHMMSS
                }

                if (!int.TryParse(input.AsSpan(5, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
                {
                    return null;
                }
            }
        }
        else
        {
            return null;
        }

        // Validate range
        if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59 || seconds < 0 || seconds > 59)
            return null;

        var totalNanoseconds = (long) hours * NanosecondsPerHour +
                               (long) minutes * NanosecondsPerMinute +
                               (long) seconds * NanosecondsPerSecond +
                               nanoseconds;

        return isNegative ? -totalNanoseconds : totalNanoseconds;
    }

    /// <summary>
    /// Formats an offset in nanoseconds as an ISO 8601 offset string.
    /// </summary>
    public static string FormatOffsetString(long offsetNanoseconds)
    {
        var sign = offsetNanoseconds >= 0 ? "+" : "-";
        offsetNanoseconds = System.Math.Abs(offsetNanoseconds);

        var hours = offsetNanoseconds / NanosecondsPerHour;
        offsetNanoseconds %= NanosecondsPerHour;
        var minutes = offsetNanoseconds / NanosecondsPerMinute;
        offsetNanoseconds %= NanosecondsPerMinute;
        var seconds = offsetNanoseconds / NanosecondsPerSecond;
        offsetNanoseconds %= NanosecondsPerSecond;

        if (seconds != 0 || offsetNanoseconds != 0)
        {
            var nanoseconds = offsetNanoseconds;
            if (nanoseconds != 0)
            {
                var fraction = nanoseconds.ToString(CultureInfo.InvariantCulture).PadLeft(9, '0').TrimEnd('0');
                return $"{sign}{hours:D2}:{minutes:D2}:{seconds:D2}.{fraction}";
            }

            return $"{sign}{hours:D2}:{minutes:D2}:{seconds:D2}";
        }

        return $"{sign}{hours:D2}:{minutes:D2}";
    }

    /// <summary>
    /// Parses an ISO 8601 duration string.
    /// </summary>
    public static DurationRecord? ParseDuration(string input)
    {
        var match = DurationPattern.Match(input);
        if (!match.Success)
            return null;

        // Group mapping with fractional hours/minutes:
        // 1=sign, 2=years, 3=months, 4=weeks, 5=days,
        // 6=hours, 7=hoursFraction, 8=minutes, 9=minutesFraction,
        // 10=seconds, 11=secondsFraction

        // Validate: at least one component must be present (reject bare "P" or "PT")
        var hasAnyComponent = false;
        for (var i = 2; i <= 11; i++)
        {
            if (match.Groups[i].Success)
            {
                hasAnyComponent = true;
                break;
            }
        }

        if (!hasAnyComponent)
            return null;

        // Validate: fractional hours cannot be followed by minutes or seconds
        if (match.Groups[7].Success && (match.Groups[8].Success || match.Groups[10].Success))
            return null;

        // Validate: fractional minutes cannot be followed by seconds
        if (match.Groups[9].Success && match.Groups[10].Success)
            return null;

        var sign = string.Equals(match.Groups[1].Value, "-", StringComparison.Ordinal) ? -1.0 : 1.0;
        var years = ParseDurationComponent(match.Groups[2].Value);
        var months = ParseDurationComponent(match.Groups[3].Value);
        var weeks = ParseDurationComponent(match.Groups[4].Value);
        var days = ParseDurationComponent(match.Groups[5].Value);
        var hours = ParseDurationComponent(match.Groups[6].Value);

        double minutes = 0;
        double seconds = 0;
        double milliseconds = 0;
        double microseconds = 0;
        double nanoseconds = 0;

        // Handle fractional hours: distribute to minutes, seconds, ms, µs, ns
        if (match.Groups[7].Success)
        {
            var frac = ParseFraction(match.Groups[7].Value);
            var totalMinutes = frac * 60;
            minutes = System.Math.Truncate(totalMinutes);
            var remainingSeconds = (totalMinutes - minutes) * 60;
            seconds = System.Math.Truncate(remainingSeconds);
            var remainingMs = (remainingSeconds - seconds) * 1000;
            milliseconds = System.Math.Truncate(remainingMs);
            var remainingUs = (remainingMs - milliseconds) * 1000;
            microseconds = System.Math.Truncate(remainingUs);
            var remainingNs = (remainingUs - microseconds) * 1000;
            nanoseconds = System.Math.Round(remainingNs);
        }
        else
        {
            minutes = ParseDurationComponent(match.Groups[8].Value);

            // Handle fractional minutes: distribute to seconds, ms, µs, ns
            if (match.Groups[9].Success)
            {
                var frac = ParseFraction(match.Groups[9].Value);
                var totalSeconds = frac * 60;
                seconds = System.Math.Truncate(totalSeconds);
                var remainingMs = (totalSeconds - seconds) * 1000;
                milliseconds = System.Math.Truncate(remainingMs);
                var remainingUs = (remainingMs - milliseconds) * 1000;
                microseconds = System.Math.Truncate(remainingUs);
                var remainingNs = (remainingUs - microseconds) * 1000;
                nanoseconds = System.Math.Round(remainingNs);
            }
            else if (match.Groups[10].Success)
            {
                seconds = double.Parse(match.Groups[10].Value, CultureInfo.InvariantCulture);
                if (match.Groups[11].Success)
                {
                    var fraction = match.Groups[11].Value.PadRight(9, '0');
                    milliseconds = double.Parse(fraction.AsSpan(0, 3), CultureInfo.InvariantCulture);
                    microseconds = double.Parse(fraction.AsSpan(3, 3), CultureInfo.InvariantCulture);
                    nanoseconds = double.Parse(fraction.AsSpan(6, 3), CultureInfo.InvariantCulture);
                }
            }
        }

        // Use NoNegativeZero to avoid -0 values (mathematical values don't have -0)
        return new DurationRecord(
            NoNegativeZero(sign * years),
            NoNegativeZero(sign * months),
            NoNegativeZero(sign * weeks),
            NoNegativeZero(sign * days),
            NoNegativeZero(sign * hours),
            NoNegativeZero(sign * minutes),
            NoNegativeZero(sign * seconds),
            NoNegativeZero(sign * milliseconds),
            NoNegativeZero(sign * microseconds),
            NoNegativeZero(sign * nanoseconds));
    }

    /// <summary>
    /// Ensures a double value of 0 is positive zero, not negative zero.
    /// Mathematical values (ℝ) in the spec don't have a concept of -0.
    /// </summary>
    internal static double NoNegativeZero(double value) => value == 0 ? 0 : value;

    private static double ParseFraction(string fractionDigits)
    {
        // Convert fractional digit string like "03125" to 0.03125
        return double.Parse("0." + fractionDigits, CultureInfo.InvariantCulture);
    }

    private static double ParseDurationComponent(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;
        // Use TryParse to handle very large numbers that would overflow double
        // (e.g., "9".repeat(1000) should become Infinity, not throw)
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;
        // If parsing fails (overflow), return Infinity so IsValidDuration rejects it
        return double.PositiveInfinity;
    }

    /// <summary>
    /// Strips annotations (calendar, timezone, etc.) from an ISO string and returns the core string and calendar.
    /// Handles: [u-ca=calendar], [timezone], [!u-ca=calendar], etc.
    /// Returns error message if annotations are invalid, null if valid.
    /// </summary>
    public static string? StripAnnotations(string input, out string coreString, out string? calendar)
    {
        // Reject non-ASCII minus sign (U+2212)
        if (ContainsInvalidMinusSign(input))
        {
            coreString = input;
            calendar = null;
            return "Invalid minus sign";
        }

        calendar = null;
        var bracketIndex = input.IndexOf('[');

        if (bracketIndex < 0)
        {
            // No annotations
            coreString = input;
            return null;
        }

        // Core string is everything before the first bracket
        coreString = input.Substring(0, bracketIndex);

        // But we might have Z or offset between time and brackets
        // e.g., "2000-05-02T15:23Z[u-ca=iso8601]" or "2000-05-02T15:23+01:00[u-ca=iso8601]"
        // So scan backwards from bracket to include Z or offset
        // Only do this if there's a time separator (T or space) in the string
        var hasTimeSeparator = input.Contains('T') || input.Contains('t') ||
                               (input.Contains(' ') && input.IndexOf(' ') < bracketIndex);

        if (hasTimeSeparator)
        {
            var scanPos = bracketIndex - 1;
            while (scanPos >= 0)
            {
                var c = input[scanPos];
                if (c == 'Z' || c == 'z')
                {
                    coreString = input.Substring(0, scanPos + 1);
                    break;
                }

                if (c == '+' || c == '-')
                {
                    // Found offset, include it and everything after until bracket
                    coreString = input.Substring(0, bracketIndex);
                    break;
                }

                if (!char.IsDigit(c) && c != ':' && c != '.')
                {
                    // Not part of offset
                    break;
                }

                scanPos--;
            }
        }

        // Parse annotations to extract calendar
        // Track calendar and timezone annotations for validation
        var calendarCount = 0;
        var hasCriticalCalendar = false;
        var timeZoneCount = 0;

        var pos = bracketIndex;
        while (pos < input.Length && input[pos] == '[')
        {
            var endBracket = input.IndexOf(']', pos);
            if (endBracket < 0)
            {
                break; // Malformed, but let the main parser handle the error
            }

            var annotation = input.Substring(pos + 1, endBracket - pos - 1);

            // Check for critical flag before stripping it
            var isCritical = annotation.Length > 0 && annotation[0] == '!';

            // Strip critical flag if present
            if (isCritical)
            {
                annotation = annotation.Substring(1);
            }

            // Check if it's a key-value annotation (contains '=')
            var equalsIndex = annotation.IndexOf('=');
            if (equalsIndex >= 0)
            {
                // Key-value annotation - validate that key is lowercase only
                var key = annotation.Substring(0, equalsIndex);
                if (!IsLowercaseAnnotationKey(key))
                {
                    coreString = input; // Set output even on error
                    return "Annotation keys must be lowercase";
                }

                // Check if it's a calendar annotation
                if (annotation.StartsWith("u-ca=", StringComparison.Ordinal))
                {
                    calendarCount++;
                    if (isCritical)
                    {
                        hasCriticalCalendar = true;
                    }

                    // Only use the first calendar annotation; subsequent ones are ignored
                    if (calendar is null)
                    {
                        var calendarId = annotation.Substring(5); // Extract calendar name after "u-ca="

                        // Validate the calendar ID
                        var canonical = CanonicalizeCalendar(calendarId);
                        if (canonical is null)
                        {
                            coreString = input;
                            return "Invalid calendar ID";
                        }

                        calendar = canonical;
                    }
                    // Subsequent calendar annotations are ignored (per spec)
                }
                else if (isCritical)
                {
                    // Unknown key-value annotation with critical flag is not allowed
                    coreString = input;
                    return "Critical unknown annotation";
                }
                // Other non-critical key-value annotations are ignored
            }
            else
            {
                // Time zone annotation (no '=')
                timeZoneCount++;
            }

            pos = endBracket + 1;
        }

        // Reject trailing text after annotations (e.g., "2020-01-01T00:00:00+00:00[UTC]junk")
        if (pos < input.Length)
        {
            coreString = input;
            return "Trailing characters after annotations";
        }

        // Validate: Multiple calendar annotations with any critical flag is invalid
        if (calendarCount > 1 && hasCriticalCalendar)
        {
            coreString = input;
            return "Multiple calendar annotations with critical flag";
        }

        // Validate: Multiple timezone annotations are invalid
        if (timeZoneCount > 1)
        {
            coreString = input;
            return "Multiple timezone annotations";
        }

        return null;
    }

    /// <summary>
    /// Checks if an annotation key is valid (lowercase letters, digits, and hyphens only).
    /// </summary>
    public static bool IsLowercaseAnnotationKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        for (var i = 0; i < key.Length; i++)
        {
            var c = key[i];
            if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Parses an ISO 8601 date string.
    /// </summary>
    public static IsoDate? ParseIsoDate(string input)
    {
        // Reject non-ASCII minus sign (U+2212)
        if (ContainsInvalidMinusSign(input))
            return null;

        var match = DatePattern.Match(input);
        if (!match.Success)
            return null;

        var yearStr = match.Groups[1].Value;

        // ISO 8601 allows:
        // - 4-digit years: YYYY (e.g., "2020")
        // - Signed 6-digit extended years: ±YYYYYY (e.g., "+002020", "-002020")
        // Reject 5-digit years or unsigned 6-digit years
        if (yearStr[0] == '+' || yearStr[0] == '-')
        {
            // Extended year must be exactly sign + 6 digits
            if (yearStr.Length != 7)
                return null;
        }
        else
        {
            // Standard year must be exactly 4 digits
            if (yearStr.Length != 4)
                return null;
        }

        var year = int.Parse(yearStr, CultureInfo.InvariantCulture);

        // Reject negative zero year (e.g., "-000000")
        // Per Temporal spec, year zero is valid but negative zero is not
        if (year == 0 && yearStr[0] == '-')
            return null;

        // Group 2 and 3 are for extended format (YYYY-MM-DD)
        // Group 4 and 5 are for basic format (YYYYMMDD)
        int month, day;
        if (match.Groups[2].Success)
        {
            // Extended format
            month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            day = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        }
        else
        {
            // Basic format
            month = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
            day = int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);
        }

        var date = new IsoDate(year, month, day);
        return date.IsValid() ? date : null;
    }

    /// <summary>
    /// Parses an ISO 8601 time string.
    /// </summary>
    public static IsoTime? ParseIsoTime(string input)
    {
        // Reject non-ASCII minus sign (U+2212)
        if (ContainsInvalidMinusSign(input))
            return null;

        // Try each format in order (prefer colon format as it's most common)
        Match? match = TimePatternColon.Match(input);
        if (!match.Success)
        {
            match = TimePatternHyphen.Match(input);
            if (!match.Success)
            {
                match = TimePatternCompact.Match(input);
                if (!match.Success)
                    return null;
            }
        }

        var hour = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var minute = match.Groups[2].Success
            ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
            : 0;
        var second = match.Groups[3].Success
            ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
            : 0;

        // Handle leap second: treat :60 as :59 (per spec)
        if (second == 60)
        {
            second = 59;
        }

        int millisecond = 0, microsecond = 0, nanosecond = 0;
        if (match.Groups[4].Success)
        {
            var fraction = match.Groups[4].Value.PadRight(9, '0');
            millisecond = int.Parse(fraction.AsSpan(0, 3), CultureInfo.InvariantCulture);
            microsecond = int.Parse(fraction.AsSpan(3, 3), CultureInfo.InvariantCulture);
            nanosecond = int.Parse(fraction.AsSpan(6, 3), CultureInfo.InvariantCulture);
        }

        var time = new IsoTime(hour, minute, second, millisecond, microsecond, nanosecond);
        return time.IsValid() ? time : null;
    }

#pragma warning disable MA0023
    private static readonly Regex AnnotationPattern = new(
        @"\[(!?)([^\]]+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    // Pattern for validating time with optional offset (used for PlainDate date-time validation)
    // Supports both extended (HH:MM:SS) and compact (HHMMSS) ISO 8601 time formats
    // Extended: HH[:MM[:SS[.fff]]]  Compact: HH[MM[SS[.fff]]]
    // Fractions are only allowed after seconds, not after hours or minutes
    // Per spec TimeSpec grammar: Hour | Hour TimeSeparator MinuteSecond | ...
    private static readonly Regex TimeWithOffsetPattern = new(
        @"^(\d{2})(?:(?::(\d{2})(?::(\d{2})(?:[.,](\d{1,9}))?)?)|(?:(\d{2})(?:(\d{2})(?:[.,](\d{1,9}))?)?))??(?:([Zz])|([+-])(\d{2}):?(\d{2})(?::?(\d{2})(?:[.,](\d{1,9}))?)?)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);
#pragma warning restore MA0023

    /// <summary>
    /// Validates that a string is a well-formed time component with optional offset.
    /// Used to validate the time portion of date-time strings for PlainDate.
    /// </summary>
    public static bool IsValidTimeWithOffset(string timeString)
    {
        if (string.IsNullOrEmpty(timeString))
            return false;

        var match = TimeWithOffsetPattern.Match(timeString);
        if (!match.Success)
            return false;

        // Validate time component ranges
        var hour = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        if (hour > 23)
            return false;

        // Extended format groups: 2=minute, 3=second
        // Compact format groups: 5=minute, 6=second
        int minute = 0, second = 0;
        if (match.Groups[2].Success)
        {
            minute = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        }
        else if (match.Groups[5].Success)
        {
            minute = int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);
        }

        if (minute > 59)
            return false;

        if (match.Groups[3].Success)
        {
            second = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        }
        else if (match.Groups[6].Success)
        {
            second = int.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture);
        }

        // Allow 60 for leap second
        if (second > 60)
            return false;

        // Validate separator consistency within offset
        // Groups: 9=sign, 10=offset hours, 11=offset minutes, 12=offset seconds
        if (match.Groups[11].Success && match.Groups[12].Success)
        {
            var hasColonBeforeMinute = match.Groups[11].Index > match.Groups[10].Index + match.Groups[10].Length;
            var hasColonBeforeSecond = match.Groups[12].Index > match.Groups[11].Index + match.Groups[11].Length;
            if (hasColonBeforeMinute != hasColonBeforeSecond)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Parses an ISO 8601 instant string and returns epoch nanoseconds.
    /// https://tc39.es/proposal-temporal/#sec-temporal-parsetemporalinstantstring
    /// </summary>
    public static BigInteger? ParseInstantString(string input)
    {
        // Reject non-ASCII minus sign (U+2212)
        if (ContainsInvalidMinusSign(input))
            return null;

        var match = InstantPattern.Match(input);
        if (!match.Success)
            return null;

        // Validate separator consistency within offset (colon usage must be uniform)
        if (match.Groups[11].Success && match.Groups[12].Success)
        {
            var hasColonBeforeMinute = match.Groups[11].Index > match.Groups[10].Index + match.Groups[10].Length;
            var hasColonBeforeSecond = match.Groups[12].Index > match.Groups[11].Index + match.Groups[11].Length;
            if (hasColonBeforeMinute != hasColonBeforeSecond)
            {
                return null;
            }
        }

        var yearStr = match.Groups[1].Value;
        var year = int.Parse(yearStr, CultureInfo.InvariantCulture);

        // Reject year zero (-000000)
        if (year == 0 && yearStr.Length > 4 && yearStr[0] == '-')
        {
            return null;
        }

        var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var day = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        var hour = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
        var minute = match.Groups[5].Success
            ? int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture)
            : 0;
        var second = match.Groups[6].Success
            ? int.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture)
            : 0;

        // Handle leap second: treat 60 as 59
        var isLeapSecond = second == 60;
        if (isLeapSecond)
        {
            second = 59;
        }

        int millisecond = 0, microsecond = 0, nanosecond = 0;
        if (match.Groups[7].Success)
        {
            var fraction = match.Groups[7].Value.PadRight(9, '0');
            millisecond = int.Parse(fraction.AsSpan(0, 3), CultureInfo.InvariantCulture);
            microsecond = int.Parse(fraction.AsSpan(3, 3), CultureInfo.InvariantCulture);
            nanosecond = int.Parse(fraction.AsSpan(6, 3), CultureInfo.InvariantCulture);
        }

        // Parse time zone offset
        var offsetString = match.Groups[8].Value;
        long offsetNanoseconds = 0;

        if (!string.Equals(offsetString, "Z", StringComparison.OrdinalIgnoreCase))
        {
            var offsetSign = string.Equals(match.Groups[9].Value, "-", StringComparison.Ordinal) ? -1 : 1;
            var offsetHour = int.Parse(match.Groups[10].Value, CultureInfo.InvariantCulture);
            var offsetMinute = match.Groups[11].Success
                ? int.Parse(match.Groups[11].Value, CultureInfo.InvariantCulture)
                : 0;

            // Validate offset range
            if (offsetHour > 23 || (offsetHour == 23 && offsetMinute > 59))
            {
                return null;
            }

            var offsetSecond = match.Groups[12].Success
                ? int.Parse(match.Groups[12].Value, CultureInfo.InvariantCulture)
                : 0;

            long offsetFractionNs = 0;
            if (match.Groups[13].Success)
            {
                var offsetFraction = match.Groups[13].Value.PadRight(9, '0');
                offsetFractionNs = long.Parse(offsetFraction.AsSpan(0, 9), CultureInfo.InvariantCulture);
            }

            offsetNanoseconds = offsetSign * (
                (long) offsetHour * 3_600_000_000_000L +
                (long) offsetMinute * 60_000_000_000L +
                (long) offsetSecond * 1_000_000_000L +
                offsetFractionNs);
        }

        // Validate annotations
        if (match.Groups[14].Success && match.Groups[14].Value.Length > 0)
        {
            if (!ValidateAnnotations(match.Groups[14].Value))
            {
                return null;
            }
        }

        // Convert to epoch nanoseconds
        var date = new IsoDate(year, month, day);
        if (!date.IsValid())
            return null;

        // Use second=59 for leap seconds
        var time = new IsoTime(hour, minute, second, millisecond, microsecond, nanosecond);
        if (!time.IsValid())
            return null;

        var epochDays = IsoDateToDays(year, month, day);
        var epochNs = (BigInteger) epochDays * 86_400_000_000_000L +
                      (BigInteger) hour * 3_600_000_000_000L +
                      (BigInteger) minute * 60_000_000_000L +
                      (BigInteger) second * 1_000_000_000L +
                      (BigInteger) millisecond * 1_000_000L +
                      (BigInteger) microsecond * 1_000L +
                      nanosecond;

        // Subtract offset to get UTC
        epochNs -= offsetNanoseconds;

        // Validate range
        if (epochNs < BigInteger.Parse("-8640000000000000000000", CultureInfo.InvariantCulture) ||
            epochNs > BigInteger.Parse("8640000000000000000000", CultureInfo.InvariantCulture))
        {
            return null;
        }

        return epochNs;
    }

    private static bool ValidateAnnotations(string annotations)
    {
        var matches = AnnotationPattern.Matches(annotations);
        var calendarCount = 0;
        var hasCalendarCritical = false;
        var timeZoneCount = 0;

        foreach (Match m in matches)
        {
            var critical = string.Equals(m.Groups[1].Value, "!", StringComparison.Ordinal);
            var content = m.Groups[2].Value;

            var eqIndex = content.IndexOf('=');
            if (eqIndex >= 0)
            {
                // Key-value annotation
                var key = content.Substring(0, eqIndex);

                // Keys must be lowercase
                foreach (var c in key)
                {
                    if (char.IsUpper(c))
                    {
                        return false;
                    }
                }

                if (content.StartsWith("u-ca=", StringComparison.Ordinal))
                {
                    calendarCount++;
                    if (critical)
                    {
                        hasCalendarCritical = true;
                    }
                }
                else
                {
                    // Unknown key-value annotation with critical flag should be rejected
                    if (critical)
                    {
                        return false;
                    }
                }
            }
            else
            {
                // Time zone annotation (no = sign)
                timeZoneCount++;

                // Reject sub-minute offsets in time zone annotations
                // Check if it looks like an offset: +/-HH:MM:SS or similar
                if (content.Length > 6 &&
                    (content[0] == '+' || content[0] == '-') &&
                    char.IsDigit(content[1]))
                {
                    // It's an offset-style time zone; check for sub-minute parts
                    // Valid: +05:30, -07:00, +0530
                    // Invalid: -07:00:01, -070001, -07:00:00.1
                    var stripped = content.Substring(1); // Remove sign
                    int colonCount = 0;
                    foreach (var c in stripped)
                    {
                        if (c == ':') colonCount++;
                    }

                    // More than 1 colon means sub-minute offset
                    if (colonCount > 1)
                    {
                        return false;
                    }

                    // Check for packed format with seconds (6+ digits)
                    var digits = stripped.Replace(":", "");
                    var dotIdx = digits.IndexOf('.');
                    var digitPart = dotIdx >= 0 ? digits.Substring(0, dotIdx) : digits;
                    if (digitPart.Length > 4)
                    {
                        return false;
                    }
                }

                // Unknown critical time zone annotation
                if (critical && timeZoneCount > 1)
                {
                    return false;
                }
            }
        }

        // Multiple time zone annotations
        if (timeZoneCount > 1)
        {
            return false;
        }

        // Multiple calendar annotations with any critical flag
        if (calendarCount > 1 && hasCalendarCritical)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that a duration has valid components.
    /// https://tc39.es/proposal-temporal/#sec-temporal-isvalidduration
    /// </summary>
    public static bool IsValidDuration(DurationRecord duration)
    {
        // Check that all components have the same sign
        var sign = 0;
        double[] components = { duration.Years, duration.Months, duration.Weeks, duration.Days, duration.Hours, duration.Minutes, duration.Seconds, duration.Milliseconds, duration.Microseconds, duration.Nanoseconds };

        foreach (var component in components)
        {
            if (double.IsNaN(component) || double.IsInfinity(component))
                return false;

            if (component > 0)
            {
                if (sign < 0)
                    return false;
                sign = 1;
            }
            else if (component < 0)
            {
                if (sign > 0)
                    return false;
                sign = -1;
            }
        }

        // Check calendar unit maximums: |years|, |months|, |weeks| < 2^32
        const double maxCalendarUnit = 4294967296.0; // 2^32
        if (System.Math.Abs(duration.Years) >= maxCalendarUnit ||
            System.Math.Abs(duration.Months) >= maxCalendarUnit ||
            System.Math.Abs(duration.Weeks) >= maxCalendarUnit)
        {
            return false;
        }

        // Check normalized seconds < 2^53
        // normalizedSeconds = |days| × 86400 + |hours| × 3600 + |minutes| × 60 + |seconds|
        //                     + |ms| × 10^-3 + |µs| × 10^-6 + |ns| × 10^-9
        // Using nanoseconds for exact comparison:
        var totalNs = (BigInteger) System.Math.Abs(duration.Days) * 86_400_000_000_000L
                      + (BigInteger) System.Math.Abs(duration.Hours) * 3_600_000_000_000L
                      + (BigInteger) System.Math.Abs(duration.Minutes) * 60_000_000_000L
                      + (BigInteger) System.Math.Abs(duration.Seconds) * 1_000_000_000L
                      + (BigInteger) System.Math.Abs(duration.Milliseconds) * 1_000_000L
                      + (BigInteger) System.Math.Abs(duration.Microseconds) * 1_000L
                      + (BigInteger) System.Math.Abs(duration.Nanoseconds);

        // 2^53 seconds in nanoseconds = 9007199254740992 * 10^9
        var maxNs = new BigInteger(9007199254740992L) * 1_000_000_000L;
        if (totalNs >= maxNs)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Converts a duration to total nanoseconds (excluding calendar units).
    /// Only valid for durations without years, months, or weeks.
    /// </summary>
    public static BigInteger TotalDurationNanoseconds(DurationRecord duration)
    {
        const long nsPerDay = 86_400_000_000_000L;
        const long nsPerHour = 3_600_000_000_000L;
        const long nsPerMinute = 60_000_000_000L;
        const long nsPerSecond = 1_000_000_000L;
        const long nsPerMs = 1_000_000L;
        const long nsPerUs = 1_000L;

        // Cast to BigInteger before multiplying to avoid double precision loss with large values
        var total = (BigInteger) duration.Days * nsPerDay;
        total += (BigInteger) duration.Hours * nsPerHour;
        total += (BigInteger) duration.Minutes * nsPerMinute;
        total += (BigInteger) duration.Seconds * nsPerSecond;
        total += (BigInteger) duration.Milliseconds * nsPerMs;
        total += (BigInteger) duration.Microseconds * nsPerUs;
        total += (BigInteger) duration.Nanoseconds;

        return total;
    }

    /// <summary>
    /// Gets the singular form of a temporal unit name.
    /// </summary>
    public static string ToSingularUnit(string unit)
    {
        return unit switch
        {
            "years" => "year",
            "months" => "month",
            "weeks" => "week",
            "days" => "day",
            "hours" => "hour",
            "minutes" => "minute",
            "seconds" => "second",
            "milliseconds" => "millisecond",
            "microseconds" => "microsecond",
            "nanoseconds" => "nanosecond",
            _ => unit
        };
    }

    /// <summary>
    /// Checks if a string is a valid temporal unit for date-time rounding.
    /// </summary>
    public static bool IsValidDateTimeUnit(string unit)
    {
        return unit is "day" or "hour" or "minute" or "second" or "millisecond" or "microsecond" or "nanosecond";
    }

    /// <summary>
    /// Checks if a unit is a time unit (hour, minute, second, millisecond, microsecond, nanosecond).
    /// Per spec: TemporalUnitCategory(unit) is ~time~
    /// </summary>
    public static bool IsTimeUnit(string unit)
    {
        return unit is "hour" or "minute" or "second" or "millisecond" or "microsecond" or "nanosecond";
    }

    /// <summary>
    /// Checks if a string is a valid temporal unit (including date units).
    /// </summary>
    public static bool IsValidTemporalUnit(string unit)
    {
        return unit is "year" or "month" or "week" or "day" or "hour" or "minute" or "second" or "millisecond" or "microsecond" or "nanosecond";
    }

    /// <summary>
    /// Checks if a string is a valid temporal unit for time rounding.
    /// </summary>
    public static bool IsValidTimeUnit(string unit)
    {
        return unit is "hour" or "minute" or "second" or "millisecond" or "microsecond" or "nanosecond";
    }

    // ---- Time Zone Helpers (shared across Temporal types) ----

    /// <summary>
    /// Checks if a string starts with a negative zero year (-000000).
    /// </summary>
    public static bool HasNegativeZeroYear(string input)
    {
        if (input.Length < 7 || input[0] != '-')
            return false;

        for (var i = 1; i <= 6; i++)
        {
            if (i >= input.Length || input[i] != '0')
                return false;
        }

        // Ensure position 7 is not a digit (to avoid matching -0000001, etc.)
        if (input.Length > 7 && char.IsDigit(input[7]))
            return false;

        return true;
    }

    /// <summary>
    /// Checks if a string looks like an offset (starts with +, -, or Unicode minus).
    /// </summary>
    public static bool IsOffsetString(string s)
    {
        return s.Length > 0 && (s[0] == '+' || s[0] == '-' || s[0] == '\u2212');
    }

    /// <summary>
    /// Checks if an offset string contains sub-minute (seconds) components.
    /// Valid offsets are ±HH, ±HHMM, or ±HH:MM only (max 6 chars including sign).
    /// </summary>
    public static bool IsSubMinuteOffset(string offset)
    {
        if (offset.Length == 0) return false;
        var start = (offset[0] == '+' || offset[0] == '-' || offset[0] == '\u2212') ? 1 : 0;
        var signlessLength = offset.Length - start;
        return signlessLength > 5;
    }

    /// <summary>
    /// Extracts the UTC offset from an ISO date-time string (the portion before any bracket).
    /// Scans backward for +/- that indicates the start of an offset.
    /// </summary>
    public static string? ExtractOffsetFromIsoDateTime(string dateTimePart)
    {
        for (var i = dateTimePart.Length - 1; i >= 0; i--)
        {
            if (dateTimePart[i] == '+' || dateTimePart[i] == '-')
            {
                var offset = dateTimePart.Substring(i);
                if (offset.Length >= 3)
                {
                    var valid = true;
                    for (var j = 1; j < offset.Length; j++)
                    {
                        var c = offset[j];
                        if (!char.IsDigit(c) && c != ':' && c != '.')
                        {
                            valid = false;
                            break;
                        }
                    }

                    if (valid)
                    {
                        return offset;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Validates a time zone ID against the provider and returns the case-corrected form.
    /// Per spec, returns [[Identifier]] (not [[PrimaryIdentifier]]).
    /// Throws RangeError if the ID is not a valid time zone.
    /// https://tc39.es/proposal-temporal/#sec-temporal-totemporaltimezoneidentifier
    /// </summary>
    public static string ValidateTimeZoneId(Engine engine, Realm realm, string timeZoneId)
    {
        var provider = engine.Options.Temporal.TimeZoneProvider;
        var canonicalized = provider.CanonicalizeTimeZone(timeZoneId);
        if (canonicalized is not null)
        {
            return canonicalized;
        }

        if (!provider.IsValidTimeZone(timeZoneId))
        {
            Throw.RangeError(realm, $"Invalid time zone: {timeZoneId}");
        }

        return timeZoneId;
    }

    /// <summary>
    /// Compares two time zone identifiers for equality per TC39 TimeZoneEquals.
    /// Resolves IANA aliases to their primary identifiers before comparing.
    /// https://tc39.es/proposal-temporal/#sec-timezoneequals
    /// </summary>
    public static bool TimeZoneEquals(Engine engine, string one, string two)
    {
        if (string.Equals(one, two, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var provider = engine.Options.Temporal.TimeZoneProvider;
        var primary1 = provider.GetPrimaryTimeZoneIdentifier(one);
        var primary2 = provider.GetPrimaryTimeZoneIdentifier(two);
        if (primary1 is not null && primary2 is not null)
        {
            return string.Equals(primary1, primary2, StringComparison.OrdinalIgnoreCase);
        }

        // Fallback: compare canonicalized forms
        var canonical1 = provider.CanonicalizeTimeZone(one);
        var canonical2 = provider.CanonicalizeTimeZone(two);
        if (canonical1 is not null && canonical2 is not null)
        {
            return string.Equals(canonical1, canonical2, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Parses a time zone string and returns the canonical time zone identifier.
    /// Handles IANA IDs, offset strings, and ISO date-time strings with embedded time zones.
    /// https://tc39.es/proposal-temporal/#sec-parsetemporaltimezonestring
    /// </summary>
    public static string ParseTemporalTimeZoneString(Engine engine, Realm realm, string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Throw.RangeError(realm, "Invalid time zone string");
            return null!;
        }

        // Check for negative zero year (-000000) before any other parsing
        if (HasNegativeZeroYear(input))
        {
            Throw.RangeError(realm, "Negative zero year is not allowed in time zone strings");
            return null!;
        }

        // Check for bracketed time zone annotation (e.g., "2016-12-31T23:59:60+00:00[UTC]")
        var bracketStart = input.IndexOf('[');
        if (bracketStart >= 0)
        {
            var bracketEnd = input.IndexOf(']', bracketStart);
            if (bracketEnd > bracketStart)
            {
                var annotation = input.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
                if (!annotation.StartsWith("u-ca=", StringComparison.Ordinal))
                {
                    if (IsOffsetString(annotation) && IsSubMinuteOffset(annotation))
                    {
                        Throw.RangeError(realm, $"Sub-minute UTC offset is not a valid time zone: {annotation}");
                        return null!;
                    }

                    return ValidateTimeZoneId(engine, realm, annotation);
                }
            }
        }

        // Check if this looks like an ISO datetime string (contains T/t separator)
        // ISO datetime strings start with digits (year portion): "2024-07-02T..."
        // IANA timezone names start with letters: "Asia/Kolkata", "America/Los_Angeles"
        // Only look for T separator if the input starts with a digit (indicating a date)
        var tPos = -1;
        if (input.Length > 4 && char.IsDigit(input[0]))
        {
            for (var i = 4; i < input.Length; i++)
            {
                if (input[i] == 'T' || input[i] == 't')
                {
                    tPos = i;
                    break;
                }
            }
        }

        if (tPos >= 0)
        {
            var dateTimePart = bracketStart >= 0 ? input.Substring(0, bracketStart) : input;

            // Check for Z at end
            if (dateTimePart.Length > 0 && (dateTimePart[dateTimePart.Length - 1] == 'Z' || dateTimePart[dateTimePart.Length - 1] == 'z'))
            {
                return "UTC";
            }

            var offset = ExtractOffsetFromIsoDateTime(dateTimePart);
            if (offset is not null)
            {
                if (IsSubMinuteOffset(offset))
                {
                    Throw.RangeError(realm, $"Sub-minute UTC offset is not a valid time zone: {offset}");
                    return null!;
                }

                return ValidateTimeZoneId(engine, realm, offset);
            }

            // Bare datetime without offset → RangeError
            Throw.RangeError(realm, $"Invalid time zone string: {input}");
            return null!;
        }

        // Check if it's an offset string
        if (input[0] == '+' || input[0] == '-' || input[0] == '\u2212')
        {
            if (IsSubMinuteOffset(input))
            {
                Throw.RangeError(realm, $"Sub-minute UTC offset is not a valid time zone: {input}");
                return null!;
            }

            return ValidateTimeZoneId(engine, realm, input);
        }

        // Plain time zone identifier (e.g., "UTC")
        return ValidateTimeZoneId(engine, realm, input);
    }

    /// <summary>
    /// Converts a JS value to a temporal time zone identifier string.
    /// Requires the value to be a string; throws TypeError for non-strings.
    /// https://tc39.es/proposal-temporal/#sec-temporal-totemporaltimezoneidentifier
    /// </summary>
    public static string ToTemporalTimeZoneIdentifier(Engine engine, Realm realm, JsValue timeZoneLike)
    {
        // If timeZoneLike is a ZonedDateTime, extract its time zone
        if (timeZoneLike is JsZonedDateTime zonedDateTime)
        {
            return zonedDateTime.TimeZone;
        }

        if (!timeZoneLike.IsString())
        {
            Throw.TypeError(realm, "Invalid time zone: expected string or ZonedDateTime");
            return null!;
        }

        var identifier = timeZoneLike.ToString();
        return ParseTemporalTimeZoneString(engine, realm, identifier);
    }

    /// <summary>
    /// Valid rounding modes per the Temporal spec.
    /// </summary>
    public static bool IsValidRoundingMode(string mode)
    {
        return mode is "ceil" or "floor" or "expand" or "trunc"
            or "halfCeil" or "halfFloor" or "halfExpand" or "halfTrunc" or "halfEven";
    }

    /// <summary>
    /// Validates that a datetime is within valid ISO limits.
    /// https://tc39.es/proposal-temporal/#sec-temporal-isvalidisodate
    /// </summary>
    public static bool IsValidIsoDateTime(int year, int month, int day)
    {
        // Check basic date validity
        if (month < 1 || month > 12 || day < 1 || day > IsoDate.IsoDateInMonth(year, month))
            return false;

        // Check that date is within Temporal's supported range
        // Minimum: -271821-04-19T00:00:00.000000000
        // Maximum: +275760-09-13T23:59:59.999999999
        if (year < -271821 || year > 275760)
            return false;

        if (year == -271821)
        {
            if (month < 4 || (month == 4 && day < 19))
                return false;
        }

        if (year == 275760)
        {
            if (month > 9 || (month == 9 && day > 13))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that a date-time is within Temporal's supported range.
    /// For PlainDateTime, the range is more restrictive than PlainDate:
    /// Minimum: -271821-04-19T00:00:00.000000001 (NOT midnight!)
    /// Maximum: +275760-09-13T23:59:59.999999999
    /// </summary>
    public static bool IsValidIsoDateTime(int year, int month, int day, int hour, int minute, int second, int millisecond, int microsecond, int nanosecond)
    {
        // Check basic date validity first
        if (!IsValidIsoDateTime(year, month, day))
            return false;

        // For boundary dates, check the time component
        if (year == -271821 && month == 4 && day == 19)
        {
            // Minimum date: must be after 00:00:00.000000000
            if (hour == 0 && minute == 0 && second == 0 && millisecond == 0 && microsecond == 0 && nanosecond == 0)
            {
                return false; // Exactly midnight is invalid
            }
        }

        // Maximum date can be any time up to 23:59:59.999999999
        // (no additional restriction needed for maximum)

        return true;
    }

    /// <summary>
    /// Validates that a year-month is within Temporal's supported range.
    /// Range: -271821-04 to +275760-09
    /// https://tc39.es/proposal-temporal/#sec-temporal-isoyearmonthwithinlimits
    /// </summary>
    public static bool ISOYearMonthWithinLimits(int year, int month)
    {
        if (year < -271821 || year > 275760)
            return false;
        if (year == -271821 && month < 4)
            return false;
        if (year == 275760 && month > 9)
            return false;
        return true;
    }

    /// <summary>
    /// Canonicalizes a calendar identifier (case-insensitive matching).
    /// https://tc39.es/proposal-temporal/#sec-temporal-canonicalizetemporalcalendaridentifier
    /// </summary>
    public static string? CanonicalizeCalendar(string calendar)
    {
        // Must use ASCII case-folding only (not Turkish İ)
        // Check if it's a valid calendar ID and return canonical form
        var lower = ToAsciiLowerCase(calendar);

        // Map of known calendar identifiers to their canonical forms
        // https://tc39.es/ecma402/#sec-availablecalendars
        return lower switch
        {
            "iso8601" => "iso8601",
            "gregory" or "gregorian" => "gregory",
            "buddhist" => "buddhist",
            "chinese" => "chinese",
            "coptic" => "coptic",
            "dangi" => "dangi",
            "ethioaa" or "ethiopic-amete-alem" => "ethioaa",
            "ethiopic" => "ethiopic",
            "hebrew" => "hebrew",
            "indian" => "indian",
            "islamic" => "islamic",
            "islamic-civil" => "islamic-civil",
            "islamic-rgsa" => "islamic-rgsa",
            "islamic-tbla" => "islamic-tbla",
            "islamic-umalqura" => "islamic-umalqura",
            "islamicc" => "islamic-civil",
            "japanese" => "japanese",
            "persian" => "persian",
            "roc" => "roc",
            _ => null // Unsupported calendar
        };
    }

    /// <summary>
    /// Returns true if the calendar uses the same Gregorian (proleptic) arithmetic as iso8601.
    /// These calendars differ only in era/epoch, not in month/day/year calculations.
    /// </summary>
    internal static bool IsGregorianBasedCalendar(string calendar)
    {
        return calendar is "iso8601" or "gregory" or "japanese" or "roc" or "buddhist";
    }

    /// <summary>
    /// Returns true if the calendar supports era/eraYear fields.
    /// </summary>
    internal static bool CalendarUsesEras(string calendar)
    {
        return calendar is "gregory" or "japanese" or "roc" or "buddhist" or "coptic" or "ethiopic" or "ethioaa" or "indian"
            or "hebrew" or "islamic-civil" or "islamic-tbla" or "islamic-umalqura" or "persian";
    }

    /// <summary>
    /// Returns the era string for a given calendar and ISO year, or null if not supported.
    /// </summary>
    internal static string? CalendarEra(string calendar, int isoYear)
    {
        switch (calendar)
        {
            case "gregory":
                return isoYear >= 1 ? "ce" : "bce";
            case "roc":
                return isoYear >= 1912 ? "minguo" : "before-roc";
            case "buddhist":
                return "be";
            case "japanese":
                // Simplified: use CE/BCE for years outside specific eras
                if (isoYear >= 2019) return "reiwa";
                if (isoYear >= 1989) return "heisei";
                if (isoYear >= 1926) return "showa";
                if (isoYear >= 1912) return "taisho";
                if (isoYear >= 1868) return "meiji";
                return isoYear >= 1 ? "ce" : "bce";
            default:
                return null;
        }
    }

    /// <summary>
    /// Returns the eraYear for a given calendar and ISO year, or null if not supported.
    /// </summary>
    internal static int? CalendarEraYear(string calendar, int isoYear)
    {
        switch (calendar)
        {
            case "gregory":
                return isoYear >= 1 ? isoYear : 1 - isoYear;
            case "roc":
                return isoYear >= 1912 ? isoYear - 1911 : 1912 - isoYear;
            case "buddhist":
                return isoYear + 543;
            case "japanese":
                if (isoYear >= 2019) return isoYear - 2018;
                if (isoYear >= 1989) return isoYear - 1988;
                if (isoYear >= 1926) return isoYear - 1925;
                if (isoYear >= 1912) return isoYear - 1911;
                if (isoYear >= 1868) return isoYear - 1867;
                return isoYear >= 1 ? isoYear : 1 - isoYear;
            default:
                return null;
        }
    }

    /// <summary>
    /// Returns the calendar-specific year for Gregorian-based calendars.
    /// For non-Gregorian-based calendars, returns the ISO year unchanged (best effort).
    /// </summary>
    internal static int CalendarYear(string calendar, int isoYear)
    {
        switch (calendar)
        {
            case "gregory":
                return isoYear;
            case "roc":
                return isoYear - 1911;
            case "buddhist":
                return isoYear + 543;
            case "japanese":
                return isoYear; // Japanese year getter returns ISO year
            default:
                return isoYear;
        }
    }

    /// <summary>
    /// Reads era and eraYear properties from a property bag for era-supporting calendars.
    /// Returns the computed year if era/eraYear are present, or null if they should be ignored.
    /// Throws TypeError if only one of era/eraYear is present.
    /// Throws RangeError if eraYear is invalid (Infinity, NaN, etc.).
    /// </summary>
    internal static int? ReadEraFields(Realm realm, ObjectInstance obj, string calendar)
    {
        if (!CalendarUsesEras(calendar))
        {
            // For calendars that don't use eras, era/eraYear are not read at all
            return null;
        }

        var eraValue = obj.Get("era");
        var eraYearValue = obj.Get("eraYear");

        var hasEra = !eraValue.IsUndefined();
        var hasEraYear = !eraYearValue.IsUndefined();

        if (hasEra && hasEraYear)
        {
            // Convert era to string
            var era = TypeConverter.ToString(eraValue);
            // Convert eraYear to integer (this throws RangeError for Infinity/NaN)
            var eraYear = ToIntegerWithTruncationAsInt(realm, eraYearValue);

            // Compute year from era + eraYear using calendar-specific era mapping
            return ComputeYearFromEra(realm, calendar, era, eraYear);
        }

        if (hasEra || hasEraYear)
        {
            // Only one of era/eraYear is present - this is an error
            Throw.TypeError(realm, "Both era and eraYear must be provided together");
        }

        // Neither era nor eraYear present
        return null;
    }

    /// <summary>
    /// Computes the ISO year from a calendar-specific era and eraYear.
    /// Throws RangeError if the era is not valid for the given calendar.
    /// </summary>
    private static int ComputeYearFromEra(Realm realm, string calendar, string era, int eraYear)
    {
        switch (calendar)
        {
            case "gregory":
                if (era is "gregory" or "ce" or "ad")
                    return eraYear;
                if (era is "gregory-inverse" or "bce" or "bc")
                    return 1 - eraYear;
                break;
            case "japanese":
                if (era is "reiwa" or "heisei" or "showa" or "taisho" or "meiji" or "ce" or "bce")
                    return eraYear; // simplified
                break;
            case "roc":
                if (era is "minguo" or "roc")
                    return eraYear;
                if (era is "before-roc" or "before-roc-inverse")
                    return 1 - eraYear;
                break;
            case "buddhist":
                if (era is "buddhist" or "be")
                    return eraYear;
                break;
            case "coptic":
                if (era is "coptic" or "era1")
                    return eraYear;
                if (era is "coptic-inverse" or "era0")
                    return 1 - eraYear;
                break;
            case "ethiopic":
                if (era is "ethiopic" or "incar" or "era1")
                    return eraYear;
                if (era is "ethiopic-inverse" or "era0")
                    return 1 - eraYear;
                if (era is "ethioaa" or "mundi")
                    return eraYear; // different epoch
                break;
            case "ethioaa":
                if (era is "ethioaa" or "mundi")
                    return eraYear;
                break;
            case "indian":
                if (era is "saka" or "indian")
                    return eraYear;
                break;
            case "hebrew":
                if (era is "am" or "hebrew")
                    return eraYear;
                break;
            case "islamic-civil":
            case "islamic-tbla":
            case "islamic-umalqura":
                if (era is "islamic" or "ah")
                    return eraYear;
                break;
            case "persian":
                if (era is "persian" or "ap")
                    return eraYear;
                break;
        }

        Throw.RangeError(realm, $"Invalid era '{era}' for calendar '{calendar}'");
        return 0;
    }

    /// <summary>
    /// Checks if a string looks like an ISO date/datetime/time string.
    /// </summary>
    internal static bool LooksLikeIsoDateString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        // Check for common ISO date patterns:
        // YYYY-MM-DD, YYYY-MM, MM-DD (month-day), or variants with time
        // Also check for time-only patterns: HH:MM, HH:MM:SS, HHMMSS, etc.
        // Also check for extended year formats: +YYYYYY-MM-DD, -YYYYYY-MM-DD

        if (input.Length < 2)
            return false;

        // Check for 'T' or 't' prefix (time designator) - e.g., "T15:23:30"
        if ((input[0] == 'T' || input[0] == 't') && input.Length > 1 && char.IsDigit(input[1]))
        {
            return true;
        }

        // Handle optional '+' or '-' prefix for extended years (e.g., "+001976-11-18")
        int offset = 0;
        if (input[0] == '+' || input[0] == '-')
        {
            offset = 1;
            if (input.Length <= offset)
                return false;
        }

        // Check for digit at start (after optional sign)
        var hasDigits = char.IsDigit(input[offset]);
        if (!hasDigits)
            return false;

        // Need at least 2 characters after any sign for meaningful patterns
        if (input.Length < offset + 2)
            return false;

        // Full year format YYYY- (need at least 5 chars after sign)
        if (input.Length >= offset + 5 && char.IsDigit(input[offset + 1]) && char.IsDigit(input[offset + 2]) && char.IsDigit(input[offset + 3]) && input[offset + 4] == '-')
        {
            return true;
        }

        // Extended year format with sign: +YYYYYY- or -YYYYYY- (at least 7 chars after sign: 6 digits + hyphen)
        if (offset > 0 && input.Length >= offset + 7)
        {
            // Check for 6 digits followed by hyphen
            if (char.IsDigit(input[offset + 1]) && char.IsDigit(input[offset + 2]) &&
                char.IsDigit(input[offset + 3]) && char.IsDigit(input[offset + 4]) &&
                char.IsDigit(input[offset + 5]) && input[offset + 6] == '-')
            {
                // Reject year -000000 (minus zero) - it's invalid per spec
                if (input[0] == '-' && input[offset] == '0' && input[offset + 1] == '0' &&
                    input[offset + 2] == '0' && input[offset + 3] == '0' &&
                    input[offset + 4] == '0' && input[offset + 5] == '0')
                {
                    return false;
                }

                return true;
            }
        }

        // Extended year without hyphen separator: +YYYYYYMM or +YYYYMMDD (at least 6 digits after sign)
        if (offset > 0 && input.Length >= offset + 6)
        {
            var allDigits = true;
            for (int i = offset; i < System.Math.Min(input.Length, offset + 8); i++)
            {
                if (!char.IsDigit(input[i]))
                {
                    allDigits = false;
                    break;
                }
            }

            if (allDigits)
            {
                return true;
            }
        }

        // Month-Day format MM-DD (need at least 5 chars, no sign prefix)
        if (offset == 0 && input.Length >= 5 && char.IsDigit(input[1]) && input[2] == '-')
        {
            return true;
        }

        // Time format HH:MM (colon separator, no sign prefix)
        if (offset == 0 && input.Length >= 4 && char.IsDigit(input[1]) && (input[2] == ':' || input.Length >= 5 && char.IsDigit(input[2]) && input[3] == ':'))
        {
            return true;
        }

        // Compact time format HHMMSS or HH (at least 2 digits, no sign prefix)
        if (offset == 0 && input.Length >= 2 && char.IsDigit(input[1]))
        {
            // Check if it looks like a time-only string (all digits initially, possibly with colons/fractions later)
            // Accept patterns like "152330", "152330.1", "15", etc.
            var hasOnlyDigitsOrTimeSeparators = true;
            for (int i = 0; i < System.Math.Min(input.Length, 6); i++)
            {
                if (!char.IsDigit(input[i]))
                {
                    hasOnlyDigitsOrTimeSeparators = false;
                    break;
                }
            }

            if (hasOnlyDigitsOrTimeSeparators)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts the calendar annotation from an ISO date/datetime string.
    /// Returns the calendar ID if found and valid, empty string if found but invalid, null if not found.
    /// </summary>
    internal static string? ExtractCalendarFromIsoString(string input)
    {
        // Look for [u-ca=...] annotation
        var bracketStart = input.IndexOf('[');
        while (bracketStart >= 0)
        {
            var bracketEnd = input.IndexOf(']', bracketStart);
            if (bracketEnd <= bracketStart)
                break;

            var annotation = input.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);

            // Check for calendar annotation (with or without !)
            var ucaIndex = annotation.IndexOf("u-ca=", StringComparison.Ordinal);

            if (ucaIndex >= 0)
            {
                var calendarStart = ucaIndex + 5;
                var calendarValue = annotation.Substring(calendarStart);
                var lower = ToAsciiLowerCase(calendarValue);

                // Return the extracted calendar value for canonicalization
                return lower;
            }

            bracketStart = input.IndexOf('[', bracketEnd);
        }

        return null; // No calendar annotation found
    }

    /// <summary>
    /// Extracts and canonicalizes the calendar from a string, defaulting to "iso8601".
    /// </summary>
    internal static string ExtractCalendarIdentifierFromString(string input)
    {
        var extracted = ExtractCalendarFromIsoString(input);
        if (extracted is not null)
        {
            var canonical = CanonicalizeCalendar(extracted);
            if (canonical is not null)
            {
                return canonical;
            }
        }

        return "iso8601";
    }

    /// <summary>
    /// Converts a string to ASCII lower case (not Turkish İ).
    /// </summary>
    private static string ToAsciiLowerCase(string input)
    {
        var result = new char[input.Length];
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            // Only convert ASCII uppercase A-Z to lowercase a-z
            if (c >= 'A' && c <= 'Z')
                result[i] = (char) (c + 32);
            else
                result[i] = c;
        }

        return new string(result);
    }

    /// <summary>
    /// Checks if a year string represents negative zero (-000000).
    /// </summary>
    public static bool IsNegativeZeroYear(string yearString)
    {
        if (string.IsNullOrEmpty(yearString))
            return false;

        // Check for -000000 pattern
        if (yearString.Length >= 2 && yearString[0] == '-')
        {
            for (var i = 1; i < yearString.Length; i++)
            {
                if (yearString[i] != '0')
                    return false;
            }

            return true; // All zeros after minus sign
        }

        return false;
    }

    /// <summary>
    /// ISO Year-Month Record for BalanceISOYearMonth return value.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct IsoYearMonthRecord(int Year, int Month);

    /// <summary>
    /// BalanceISOYearMonth ( year, month )
    /// https://tc39.es/proposal-temporal/#sec-temporal-balanceisoyearmonth
    /// Balances year and month when month is out of 1-12 range.
    /// </summary>
    public static IsoYearMonthRecord BalanceISOYearMonth(int year, int month)
    {
        // Step 1: year = year + floor((month - 1) / 12)
        year += (int) System.Math.Floor((month - 1) / 12.0);

        // Step 2: month = ((month - 1) mod 12) + 1
        month = ((month - 1) % 12 + 12) % 12 + 1; // Handle negative modulo correctly

        // Step 3: Return ISO Year-Month Record
        return new IsoYearMonthRecord(year, month);
    }

    /// <summary>
    /// CalendarDateAdd ( calendar, isoDate, duration, overflow )
    /// https://tc39.es/proposal-temporal/#sec-temporal-calendardateadd
    /// Adds a duration to a date using calendar-specific reckoning.
    /// </summary>
    public static IsoDate CalendarDateAdd(Realm? realm, string calendar, IsoDate isoDate, DurationRecord duration, string overflow)
    {
        // For ISO calendar and Gregorian-based calendars (they share the same arithmetic)
        if (IsGregorianBasedCalendar(calendar))
        {
            // Step 1: Add years and months, then balance
            var balanced = BalanceISOYearMonth(
                isoDate.Year + (int) duration.Years,
                isoDate.Month + (int) duration.Months);
            var year = balanced.Year;
            var month = balanced.Month;

            // Step 2: Regulate the date (handle day overflow)
            var intermediate = RegulateIsoDate(year, month, isoDate.Day, overflow);
            if (intermediate is null)
            {
                if (realm != null)
                    Throw.RangeError(realm, "Invalid date after adding years/months");
                else
                    throw new ArgumentException("Invalid date after adding years/months", nameof(duration));
            }

            // Step 3: Add weeks and days
            var days = duration.Days + 7 * duration.Weeks;
            // Step 4: BalanceISODate
            var result = AddDaysToISODate(intermediate.Value, days);

            // Per spec, CalendarDateAdd does NOT check ISODateWithinLimits.
            // Range validation happens at the caller (e.g., CreateTemporalDate, GetEpochNanosecondsFor).
            return result;
        }
        else
        {
            throw new NotSupportedException($"Calendar '{calendar}' not yet supported");
        }
    }

    /// <summary>
    /// ISODateWithinLimits ( isoDate )
    /// Checks if an ISO date is within the valid Temporal range.
    /// Per spec: abs(ISODateToEpochDays(year, month-1, day)) must be at most 10^8.
    /// </summary>
    private static bool IsoDateWithinLimits(IsoDate date)
    {
        // Use noon time record per spec: CombineISODateAndTimeRecord(isoDate, NoonTimeRecord())
        // then ISODateTimeWithinLimits checks abs(epochDays) <= 10^8
        var epochDays = IsoDateToDays(date.Year, date.Month, date.Day);
        return System.Math.Abs((long) epochDays) <= 100_000_000;
    }

    /// <summary>
    /// Validates and regulates ISO date fields.
    /// </summary>
    public static IsoDate? RegulateIsoDate(int year, int month, int day, string overflow)
    {
        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            month = Clamp(month, 1, 12);
            var daysInMonth = IsoDate.IsoDateInMonth(year, month);
            day = Clamp(day, 1, daysInMonth);
            var date = new IsoDate(year, month, day);
            // Even with constrain, check Temporal limits
            return IsValidIsoDateTime(year, month, day) ? date : null;
        }

        if (string.Equals(overflow, "reject", StringComparison.Ordinal))
        {
            var date = new IsoDate(year, month, day);
            // Check both basic validity and Temporal limits
            return date.IsValid() && IsValidIsoDateTime(year, month, day) ? date : null;
        }

        return null;
    }

    /// <summary>
    /// Validates and regulates ISO time fields.
    /// </summary>
    public static IsoTime? RegulateIsoTime(
        int hour, int minute, int second,
        int millisecond, int microsecond, int nanosecond,
        string overflow)
    {
        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            hour = Clamp(hour, 0, 23);
            minute = Clamp(minute, 0, 59);
            second = Clamp(second, 0, 59);
            millisecond = Clamp(millisecond, 0, 999);
            microsecond = Clamp(microsecond, 0, 999);
            nanosecond = Clamp(nanosecond, 0, 999);
            return new IsoTime(hour, minute, second, millisecond, microsecond, nanosecond);
        }

        if (string.Equals(overflow, "reject", StringComparison.Ordinal))
        {
            var time = new IsoTime(hour, minute, second, millisecond, microsecond, nanosecond);
            return time.IsValid() ? time : null;
        }

        return null;
    }

    /// <summary>
    /// Adds a duration to an ISO date.
    /// </summary>
    public static IsoDate AddDurationToDate(IsoDate date, DurationRecord duration)
    {
        // Balance time units into days first (for PlainDate)
        // Per spec: For PlainDate, time components are balanced into days before date arithmetic
        // Use BigInteger to avoid precision loss with large values
        var totalNanoseconds = TimeDurationFromComponents(duration);

        // Use truncation toward zero, not floor (important for negative values)
        // -PT24.5H should be -1 day, not -2 days
        // BigInteger division truncates toward zero by default
        var balancedDays = (long) (totalNanoseconds / NanosecondsPerDay);

        return AddDurationToDateCore(date, duration, balancedDays);
    }

    /// <summary>
    /// Core date addition logic without time balancing (for PlainDateTime which handles time separately)
    /// </summary>
    public static IsoDate AddDurationToDateCore(IsoDate date, DurationRecord duration, long additionalDays)
    {
        // Add years and months first
        var year = date.Year + (int) duration.Years;
        var month = date.Month + (int) duration.Months;

        // Normalize month
        while (month > 12)
        {
            month -= 12;
            year++;
        }

        while (month < 1)
        {
            month += 12;
            year--;
        }

        // When adding weeks/days, we need to convert to day arithmetic which requires a valid intermediate date
        // When adding only years/months (no weeks/days), preserve the day for proper overflow handling
        int day;
        if (duration.Weeks == 0 && duration.Days == 0 && additionalDays == 0)
        {
            // No day arithmetic needed - preserve original day for overflow validation
            day = date.Day;
            return new IsoDate(year, month, day);
        }
        else
        {
            // Need day arithmetic - clamp day to valid range for conversion
            day = System.Math.Min(date.Day, IsoDate.IsoDateInMonth(year, month));
        }

        // Convert to days and add weeks, days, and any additional balanced days
        var totalDays = IsoDateToDays(year, month, day);
        totalDays += (long) (duration.Weeks * 7 + duration.Days) + additionalDays;

        return DaysToIsoDate(totalDays);
    }

    /// <summary>
    /// Converts an ISO date to days since Unix epoch (1970-01-01).
    /// Based on Howard Hinnant's days_from_civil algorithm which correctly handles all years.
    /// https://howardhinnant.github.io/date_algorithms.html#days_from_civil
    /// </summary>
    public static long IsoDateToDays(int year, int month, int day)
    {
        long y = year;
        if (month <= 2)
        {
            y--;
        }

        var era = (y >= 0 ? y : y - 399) / 400;
        var yoe = (long) (y - era * 400); // [0, 399]
        var doy = (153 * (month > 2 ? month - 3 : month + 9) + 2) / 5 + day - 1; // [0, 365]
        var doe = yoe * 365 + yoe / 4 - yoe / 100 + doy; // [0, 146096]
        return era * 146097 + doe - 719468;
    }

    /// <summary>
    /// CheckISODaysRange ( isoDate )
    /// https://tc39.es/proposal-temporal/#sec-temporal-checkisodaysrange
    /// Checks that the given date is within the range of 10^8 days from the epoch.
    /// </summary>
    public static void CheckISODaysRange(Realm realm, IsoDate isoDate)
    {
        var days = IsoDateToDays(isoDate.Year, isoDate.Month, isoDate.Day);
        if (System.Math.Abs(days) > 100_000_000)
        {
            Throw.RangeError(realm, "Date is outside the valid range");
        }
    }

    /// <summary>
    /// ISODateTimeWithinLimits ( isoDateTime )
    /// https://tc39.es/proposal-temporal/#sec-temporal-isodatetimewithinlimits
    /// Checks that the given date-time is within the representable range.
    /// </summary>
    public static bool ISODateTimeWithinLimits(IsoDateTime isoDateTime)
    {
        var days = IsoDateToDays(isoDateTime.Date.Year, isoDateTime.Date.Month, isoDateTime.Date.Day);
        if (System.Math.Abs(days) > 100_000_001)
        {
            return false;
        }

        // Compute epoch nanoseconds
        var ns = (BigInteger) days * NanosecondsPerDay
                 + isoDateTime.Time.Hour * NanosecondsPerHour
                 + isoDateTime.Time.Minute * NanosecondsPerMinute
                 + isoDateTime.Time.Second * NanosecondsPerSecond
                 + isoDateTime.Time.Millisecond * NanosecondsPerMillisecond
                 + isoDateTime.Time.Microsecond * NanosecondsPerMicrosecond
                 + isoDateTime.Time.Nanosecond;

        // nsMinInstant - nsPerDay = -(10^8 + 1) × nsPerDay
        // nsMaxInstant + nsPerDay = (10^8 + 1) × nsPerDay
        var limit = (BigInteger) 100_000_001 * NanosecondsPerDay;
        return ns > -limit && ns < limit;
    }

    /// <summary>
    /// Converts days since Unix epoch (1970-01-01) to ISO date using the proleptic Gregorian calendar.
    /// Based on Howard Hinnant's civil_from_days algorithm which correctly handles all years.
    /// https://howardhinnant.github.io/date_algorithms.html#civil_from_days
    /// </summary>
    public static IsoDate DaysToIsoDate(long epochDays)
    {
        var z = epochDays + 719468; // shift to days since 0000-03-01
        var era = (z >= 0 ? z : z - 146096) / 146097;
        var doe = z - era * 146097; // day of era [0, 146096]
        var yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365; // year of era [0, 399]
        var y = yoe + era * 400;
        var doy = doe - (365 * yoe + yoe / 4 - yoe / 100); // day of year [0, 365]
        var mp = (5 * doy + 2) / 153; // [0, 11]
        var day = (int) (doy - (153 * mp + 2) / 5 + 1); // [1, 31]
        var month = (int) (mp < 10 ? mp + 3 : mp - 9); // [1, 12]
        var year = (int) (y + (month <= 2 ? 1 : 0));

        return new IsoDate(year, month, day);
    }

    /// <summary>
    /// Compares two ISO dates.
    /// </summary>
    public static int CompareIsoDates(IsoDate a, IsoDate b)
    {
        if (a.Year != b.Year) return a.Year.CompareTo(b.Year);
        if (a.Month != b.Month) return a.Month.CompareTo(b.Month);
        return a.Day.CompareTo(b.Day);
    }

    /// <summary>
    /// Compares two ISO times.
    /// </summary>
    public static int CompareIsoTimes(IsoTime a, IsoTime b)
    {
        if (a.Hour != b.Hour) return a.Hour.CompareTo(b.Hour);
        if (a.Minute != b.Minute) return a.Minute.CompareTo(b.Minute);
        if (a.Second != b.Second) return a.Second.CompareTo(b.Second);
        if (a.Millisecond != b.Millisecond) return a.Millisecond.CompareTo(b.Millisecond);
        if (a.Microsecond != b.Microsecond) return a.Microsecond.CompareTo(b.Microsecond);
        return a.Nanosecond.CompareTo(b.Nanosecond);
    }

    /// <summary>
    /// Compares two ISO date-times.
    /// </summary>
    public static int CompareIsoDateTimes(IsoDateTime a, IsoDateTime b)
    {
        var dateComparison = CompareIsoDates(a.Date, b.Date);
        if (dateComparison != 0) return dateComparison;
        return CompareIsoTimes(a.Time, b.Time);
    }

    /// <summary>
    /// Throws a RangeError for an invalid Temporal value.
    /// </summary>
    public static void ThrowRangeError(Realm realm, string message)
    {
        Throw.RangeError(realm, message);
    }

    /// <summary>
    /// Throws a TypeError for an invalid Temporal operation.
    /// </summary>
    public static void ThrowTypeError(Realm realm, string message)
    {
        Throw.TypeError(realm, message);
    }

    /// <summary>
    /// Negates a rounding mode (used for "since" operations which reverse "until").
    /// </summary>
    public static string NegateRoundingMode(string roundingMode)
    {
        return roundingMode switch
        {
            "ceil" => "floor",
            "floor" => "ceil",
            "expand" => "expand", // expand stays same (away from zero)
            "halfCeil" => "halfFloor",
            "halfFloor" => "halfCeil",
            _ => roundingMode // trunc, halfTrunc, halfExpand, halfEven stay the same
        };
    }

    /// <summary>
    /// Rounds a number to an increment using the specified rounding mode.
    /// </summary>
    public static double RoundNumberToIncrement(double value, int increment, string roundingMode)
    {
        if (increment == 0) increment = 1;
        var quotient = value / increment;

        double rounded;
        switch (roundingMode)
        {
            case "ceil":
                rounded = System.Math.Ceiling(quotient);
                break;
            case "floor":
                rounded = System.Math.Floor(quotient);
                break;
            case "trunc":
                rounded = System.Math.Truncate(quotient);
                break;
            case "expand":
                rounded = quotient >= 0 ? System.Math.Ceiling(quotient) : System.Math.Floor(quotient);
                break;
            case "halfExpand":
                rounded = System.Math.Round(quotient, MidpointRounding.AwayFromZero);
                break;
            case "halfTrunc":
                rounded = RoundHalfTowardZero(quotient);
                break;
            case "halfCeil":
                rounded = RoundHalfCeil(quotient);
                break;
            case "halfFloor":
                rounded = RoundHalfFloor(quotient);
                break;
            case "halfEven":
                rounded = System.Math.Round(quotient, MidpointRounding.ToEven);
                break;
            default:
                rounded = System.Math.Truncate(quotient);
                break;
        }

        return rounded * increment;
    }

    public static long RoundNumberToIncrement(long value, long increment, string roundingMode)
    {
        if (increment == 0) increment = 1;
        var quotient = (double) value / increment;

        double rounded;
        switch (roundingMode)
        {
            case "ceil":
                rounded = System.Math.Ceiling(quotient);
                break;
            case "floor":
                rounded = System.Math.Floor(quotient);
                break;
            case "trunc":
                rounded = System.Math.Truncate(quotient);
                break;
            case "expand":
                rounded = quotient >= 0 ? System.Math.Ceiling(quotient) : System.Math.Floor(quotient);
                break;
            case "halfExpand":
                rounded = System.Math.Round(quotient, MidpointRounding.AwayFromZero);
                break;
            case "halfTrunc":
                rounded = RoundHalfTowardZero(quotient);
                break;
            case "halfCeil":
                rounded = RoundHalfCeil(quotient);
                break;
            case "halfFloor":
                rounded = RoundHalfFloor(quotient);
                break;
            case "halfEven":
                rounded = System.Math.Round(quotient, MidpointRounding.ToEven);
                break;
            default:
                rounded = System.Math.Truncate(quotient);
                break;
        }

        return (long) rounded * increment;
    }

    public static double RoundNumberToIncrement(double value, double increment, string roundingMode)
    {
        if (increment == 0) increment = 1;
        var quotient = value / increment;

        double rounded;
        switch (roundingMode)
        {
            case "ceil":
                rounded = System.Math.Ceiling(quotient);
                break;
            case "floor":
                rounded = System.Math.Floor(quotient);
                break;
            case "trunc":
                rounded = System.Math.Truncate(quotient);
                break;
            case "expand":
                rounded = quotient >= 0 ? System.Math.Ceiling(quotient) : System.Math.Floor(quotient);
                break;
            case "halfExpand":
                rounded = System.Math.Round(quotient, MidpointRounding.AwayFromZero);
                break;
            case "halfTrunc":
                rounded = RoundHalfTowardZero(quotient);
                break;
            case "halfCeil":
                rounded = RoundHalfCeil(quotient);
                break;
            case "halfFloor":
                rounded = RoundHalfFloor(quotient);
                break;
            case "halfEven":
                rounded = System.Math.Round(quotient, MidpointRounding.ToEven);
                break;
            default:
                rounded = quotient; // default to no rounding
                break;
        }

        return rounded * increment;
    }

    private static double RoundHalfTowardZero(double value)
    {
        var truncated = System.Math.Truncate(value);
        var fraction = System.Math.Abs(value - truncated);
        if (fraction > 0.5)
        {
            return value >= 0 ? truncated + 1 : truncated - 1;
        }

        return truncated;
    }

    // halfCeil: ties toward positive infinity (at 0.5, round toward +∞)
    private static double RoundHalfCeil(double value)
    {
        var floor = System.Math.Floor(value);
        var diff = value - floor;
        // If >= 0.5, round up (toward positive infinity)
        return diff >= 0.5 ? floor + 1.0 : floor;
    }

    // halfFloor: ties toward negative infinity (at 0.5, round toward -∞)
    private static double RoundHalfFloor(double value)
    {
        var floor = System.Math.Floor(value);
        var diff = value - floor;
        // If > 0.5, round up; if exactly 0.5, round down (toward negative infinity)
        return diff > 0.5 ? floor + 1.0 : floor;
    }

    public static long GetUnitNanoseconds(string unit)
    {
        return unit switch
        {
            "day" => NanosecondsPerDay,
            "hour" => NanosecondsPerHour,
            "minute" => NanosecondsPerMinute,
            "second" => NanosecondsPerSecond,
            "millisecond" => NanosecondsPerMillisecond,
            "microsecond" => NanosecondsPerMicrosecond,
            "nanosecond" => 1,
            _ => 1
        };
    }

    public static bool IsValidCalendarNameOption(string value)
    {
        return string.Equals(value, "auto", StringComparison.Ordinal) ||
               string.Equals(value, "always", StringComparison.Ordinal) ||
               string.Equals(value, "never", StringComparison.Ordinal) ||
               string.Equals(value, "critical", StringComparison.Ordinal);
    }

    /// <summary>
    /// Parses an ISO 8601 duration string (alias for ParseDuration).
    /// </summary>
    public static DurationRecord? ParseIsoDuration(string input) => ParseDuration(input);

    /// <summary>
    /// Returns the default largest unit of a duration (the largest non-zero field).
    /// https://tc39.es/proposal-temporal/#sec-temporal-defaulttemporallargestunit
    /// </summary>
    public static string DefaultTemporalLargestUnit(DurationRecord d)
    {
        if (d.Years != 0) return "year";
        if (d.Months != 0) return "month";
        if (d.Weeks != 0) return "week";
        if (d.Days != 0) return "day";
        if (d.Hours != 0) return "hour";
        if (d.Minutes != 0) return "minute";
        if (d.Seconds != 0) return "second";
        if (d.Milliseconds != 0) return "millisecond";
        if (d.Microseconds != 0) return "microsecond";
        return "nanosecond";
    }

    /// <summary>
    /// Returns a numeric index for a temporal unit (lower = larger unit).
    /// </summary>
    public static int TemporalUnitIndex(string unit)
    {
        return unit switch
        {
            "year" => 0,
            "month" => 1,
            "week" => 2,
            "day" => 3,
            "hour" => 4,
            "minute" => 5,
            "second" => 6,
            "millisecond" => 7,
            "microsecond" => 8,
            "nanosecond" => 9,
            _ => 10
        };
    }

    /// <summary>
    /// Returns the larger of two temporal units.
    /// </summary>
    public static string LargerOfTwoTemporalUnits(string a, string b)
    {
        return TemporalUnitIndex(a) <= TemporalUnitIndex(b) ? a : b;
    }

    /// <summary>
    /// Returns true if the unit is a calendar unit (year, month, week).
    /// </summary>
    public static bool IsCalendarUnit(string unit)
    {
        return unit is "year" or "month" or "week";
    }

    /// <summary>
    /// Computes total nanoseconds from time components only (no days).
    /// </summary>
    public static BigInteger TimeDurationFromComponents(DurationRecord d)
    {
        return (BigInteger) d.Hours * NanosecondsPerHour
               + (BigInteger) d.Minutes * NanosecondsPerMinute
               + (BigInteger) d.Seconds * NanosecondsPerSecond
               + (BigInteger) d.Milliseconds * NanosecondsPerMillisecond
               + (BigInteger) d.Microseconds * NanosecondsPerMicrosecond
               + (BigInteger) d.Nanoseconds;
    }

    /// <summary>
    /// Balances total nanoseconds into a DurationRecord based on the largest unit.
    /// Calendar units (year/month/week) are always zero in the result.
    /// </summary>
    public static DurationRecord BalanceTimeDuration(BigInteger totalNanoseconds, string largestUnit)
    {
        double days = 0, hours = 0, minutes = 0, seconds = 0;
        double milliseconds = 0, microseconds = 0, nanoseconds = 0;
        var remaining = totalNanoseconds;

        if (largestUnit is "day")
        {
            days = BigIntToF64(BigInteger.DivRem(remaining, NanosecondsPerDay, out remaining));
        }

        if (largestUnit is "day" or "hour")
        {
            hours = BigIntToF64(BigInteger.DivRem(remaining, NanosecondsPerHour, out remaining));
        }

        if (largestUnit is "day" or "hour" or "minute")
        {
            minutes = BigIntToF64(BigInteger.DivRem(remaining, NanosecondsPerMinute, out remaining));
        }

        if (largestUnit is "day" or "hour" or "minute" or "second")
        {
            seconds = BigIntToF64(BigInteger.DivRem(remaining, NanosecondsPerSecond, out remaining));
        }

        if (largestUnit is "day" or "hour" or "minute" or "second" or "millisecond")
        {
            milliseconds = BigIntToF64(BigInteger.DivRem(remaining, NanosecondsPerMillisecond, out remaining));
        }

        if (largestUnit is "day" or "hour" or "minute" or "second" or "millisecond" or "microsecond")
        {
            microseconds = BigIntToF64(BigInteger.DivRem(remaining, NanosecondsPerMicrosecond, out remaining));
        }

        nanoseconds = BigIntToF64(remaining);

        return new DurationRecord(0, 0, 0, days, hours, minutes, seconds, milliseconds, microseconds, nanoseconds);
    }

    /// <summary>
    /// Converts BigInteger to double (𝔽 operation in the spec).
    /// Implements IEEE 754 round-to-nearest-even, which .NET's (double)BigInteger does NOT guarantee
    /// (it may round toward zero for values larger than long.MaxValue).
    /// </summary>
    internal static double BigIntToF64(BigInteger value)
    {
        if (value.IsZero)
            return 0.0;

        var negative = value.Sign < 0;
        var abs = BigInteger.Abs(value);

        // Fast path: fits in long range - (double)(long) is always correctly rounded
        if (abs <= long.MaxValue)
            return (double) (long) value;

        // Slow path: implement IEEE 754 round-to-nearest-even via bit manipulation
        // Double has 53-bit significand (52 explicit + 1 implicit)
        var bitLength = GetBigIntegerBitLength(abs);

        // Need rounding: extract top 54 bits (53 significand + 1 guard bit)
        var shift = bitLength - 54;
        var top54 = (long) (abs >> shift);

        // Round bit is the LSB of top54
        var roundBit = (top54 & 1) != 0;

        // Significand is the top 53 bits
        var significand = top54 >> 1;

        // Sticky bit: are there any set bits below the round bit position?
        var stickyBit = !((abs & ((BigInteger.One << shift) - 1)).IsZero);

        // IEEE 754 round-to-nearest-even
        if (roundBit && (stickyBit || (significand & 1) != 0))
        {
            significand++;
            // Check if significand overflowed to 54 bits (2^53)
            if (significand >= (1L << 53))
            {
                significand >>= 1;
                shift++;
            }
        }

        // Check for infinity (exponent too large for double)
        if (shift + 53 > 1023)
            return negative ? double.NegativeInfinity : double.PositiveInfinity;

        // Build the double: significand × 2^(shift+1)
        var result = (double) significand * Pow2(shift + 1);
        return negative ? -result : result;
    }

    /// <summary>
    /// Returns the number of bits in the absolute value of a positive BigInteger.
    /// Compatible with all .NET target frameworks (GetBitLength() is .NET 5+ only).
    /// </summary>
    private static int GetBigIntegerBitLength(BigInteger abs)
    {
        // Convert to byte array (little-endian, unsigned)
        var bytes = abs.ToByteArray();
        var length = bytes.Length;
        // Skip trailing zero bytes (sign extension)
        while (length > 0 && bytes[length - 1] == 0)
            length--;
        if (length == 0)
            return 0;
        // Count bits in the most significant byte
        var msb = bytes[length - 1];
        var bits = (length - 1) * 8;
        while (msb > 0)
        {
            bits++;
            msb >>= 1;
        }

        return bits;
    }

    /// <summary>
    /// Computes 2^exponent as a double. Handles large exponents correctly.
    /// </summary>
    private static double Pow2(int exponent)
    {
        if (exponent >= 0 && exponent <= 1023)
            return BitConverter.Int64BitsToDouble((long) (exponent + 1023) << 52);
        // For larger or negative exponents, use iterative multiplication
        var result = 1.0;
        if (exponent > 0)
        {
            var factor = 2.0;
            var exp = exponent;
            while (exp > 0)
            {
                if ((exp & 1) != 0)
                    result *= factor;
                factor *= factor;
                exp >>= 1;
            }
        }
        else
        {
            var factor = 0.5;
            var exp = -exponent;
            while (exp > 0)
            {
                if ((exp & 1) != 0)
                    result *= factor;
                factor *= factor;
                exp >>= 1;
            }
        }

        return result;
    }

    /// <summary>
    /// TemporalDurationFromInternal ( internalDuration, largestUnit )
    /// https://tc39.es/proposal-temporal/#sec-temporal-temporaldurationfrominternal
    /// Converts internal duration back to DurationRecord, balancing time up to largestUnit.
    /// </summary>
    public static DurationRecord TemporalDurationFromInternal(
        Realm realm,
        double years, double months, double weeks, double dateDays,
        BigInteger timeDuration,
        string largestUnit)
    {
        // Before balancing, check that the time duration is within range
        // maxTimeDuration = 2^53 × 10^9 - 1 nanoseconds
        if (BigInteger.Abs(timeDuration) > MaxTimeDuration)
        {
            Throw.RangeError(realm, "Time duration out of range");
        }

        // Check if the primary unit value, when converted to double, would round up
        // to exceed the duration limit. This catches edge cases where the BigInteger
        // is valid but its double representation causes overflow.
        var absTime = BigInteger.Abs(timeDuration);
        BigInteger primaryValue;
        long unitNanoseconds;

        if (string.Equals(largestUnit, "day", StringComparison.Ordinal))
        {
            primaryValue = absTime / NanosecondsPerDay;
            unitNanoseconds = NanosecondsPerDay;
        }
        else if (string.Equals(largestUnit, "hour", StringComparison.Ordinal))
        {
            primaryValue = absTime / NanosecondsPerHour;
            unitNanoseconds = NanosecondsPerHour;
        }
        else if (string.Equals(largestUnit, "minute", StringComparison.Ordinal))
        {
            primaryValue = absTime / NanosecondsPerMinute;
            unitNanoseconds = NanosecondsPerMinute;
        }
        else if (string.Equals(largestUnit, "second", StringComparison.Ordinal))
        {
            primaryValue = absTime / NanosecondsPerSecond;
            unitNanoseconds = NanosecondsPerSecond;
        }
        else if (string.Equals(largestUnit, "millisecond", StringComparison.Ordinal))
        {
            primaryValue = absTime / NanosecondsPerMillisecond;
            unitNanoseconds = NanosecondsPerMillisecond;
        }
        else if (string.Equals(largestUnit, "microsecond", StringComparison.Ordinal))
        {
            primaryValue = absTime / NanosecondsPerMicrosecond;
            unitNanoseconds = NanosecondsPerMicrosecond;
        }
        else // nanosecond
        {
            primaryValue = absTime;
            unitNanoseconds = 1;
        }

        // Convert to double and check if it rounds up to exceed limits
        var primaryDouble = (double) primaryValue;
        var primaryRoundTrip = new BigInteger(primaryDouble) * unitNanoseconds;
        if (primaryRoundTrip > MaxTimeDuration)
        {
            Throw.RangeError(realm, "Duration out of range");
        }

        // Use existing BalanceTimeDuration which handles sign and balancing correctly
        // For calendar units (year/month/week), balance time to "hour" since those units
        // can't be represented as fixed nanosecond quantities
        var balanceTarget = largestUnit switch
        {
            "year" or "month" or "week" => "hour",
            _ => largestUnit
        };
        var balanced = BalanceTimeDuration(timeDuration, balanceTarget);

        // Combine with date duration
        var result = new DurationRecord(
            years, months, weeks, dateDays + balanced.Days,
            balanced.Hours, balanced.Minutes, balanced.Seconds,
            balanced.Milliseconds, balanced.Microseconds, balanced.Nanoseconds);

        if (!IsValidDuration(result))
        {
            Throw.RangeError(realm, "Duration out of range");
        }

        return result;
    }

    /// <summary>
    /// Rounds a BigInteger value to the given increment using the specified rounding mode.
    /// </summary>
    public static BigInteger RoundBigIntegerToIncrement(
        BigInteger value, BigInteger increment, string roundingMode)
    {
        if (increment <= 1)
            return value;

        var quotient = BigInteger.DivRem(value, increment, out var remainder);

        if (remainder == 0)
            return value;

        int direction;
        switch (roundingMode)
        {
            case "ceil":
                direction = remainder > 0 ? 1 : 0;
                break;
            case "floor":
                direction = remainder < 0 ? -1 : 0;
                break;
            case "expand":
                direction = value >= 0 ? 1 : -1;
                break;
            case "trunc":
                direction = 0;
                break;
            case "halfExpand":
                direction = BigInteger.Abs(remainder) * 2 >= BigInteger.Abs(increment)
                    ? (value >= 0 ? 1 : -1)
                    : 0;
                break;
            case "halfTrunc":
                direction = BigInteger.Abs(remainder) * 2 > BigInteger.Abs(increment)
                    ? (value >= 0 ? 1 : -1)
                    : 0;
                break;
            case "halfCeil":
                direction = BigInteger.Abs(remainder) * 2 >= BigInteger.Abs(increment)
                    ? (remainder > 0 ? 1 : (remainder < 0 && BigInteger.Abs(remainder) * 2 > BigInteger.Abs(increment) ? -1 : 0))
                    : 0;
                break;
            case "halfFloor":
                direction = BigInteger.Abs(remainder) * 2 >= BigInteger.Abs(increment)
                    ? (remainder < 0 ? -1 : (remainder > 0 && BigInteger.Abs(remainder) * 2 > BigInteger.Abs(increment) ? 1 : 0))
                    : 0;
                break;
            case "halfEven":
                var absRem2 = BigInteger.Abs(remainder) * 2;
                var absInc = BigInteger.Abs(increment);
                if (absRem2 > absInc)
                    direction = value >= 0 ? 1 : -1;
                else if (absRem2 < absInc)
                    direction = 0;
                else
                    direction = quotient % 2 != 0 ? (value >= 0 ? 1 : -1) : 0;
                break;
            default:
                direction = 0;
                break;
        }

        return (quotient + direction) * increment;
    }

    /// <summary>
    /// Formats an ISO date as a string.
    /// </summary>
    public static string FormatIsoDate(IsoDate date, string? calendarId = null)
    {
        var sb = new ValueStringBuilder();

        if (date.Year < 0 || date.Year > 9999)
        {
            sb.Append(date.Year >= 0 ? '+' : '-');
            sb.Append(System.Math.Abs(date.Year).ToString("D6", CultureInfo.InvariantCulture));
        }
        else
        {
            sb.Append(date.Year.ToString("D4", CultureInfo.InvariantCulture));
        }

        sb.Append('-');
        sb.Append(date.Month.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append('-');
        sb.Append(date.Day.ToString("D2", CultureInfo.InvariantCulture));

        if (calendarId is not null && !string.Equals(calendarId, "iso8601", StringComparison.Ordinal))
        {
            sb.Append("[u-ca=");
            sb.Append(calendarId);
            sb.Append(']');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats an ISO time as a string.
    /// </summary>
    public static string FormatIsoTime(IsoTime time, string? precision = null)
    {
        var sb = new ValueStringBuilder();
        sb.Append(time.Hour.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append(':');
        sb.Append(time.Minute.ToString("D2", CultureInfo.InvariantCulture));

        // precision = "-2" means minute precision (omit seconds)
        // precision = "0" means second precision (no fractional seconds)
        // precision = "1-9" means that many fractional second digits
        // precision = null or "auto" means show all non-zero components

        if (string.Equals(precision, "-2", StringComparison.Ordinal))
        {
            // Minute precision: omit seconds entirely
            return sb.ToString();
        }

        sb.Append(':');
        sb.Append(time.Second.ToString("D2", CultureInfo.InvariantCulture));

        if (string.Equals(precision, "0", StringComparison.Ordinal))
        {
            // Second precision: no fractional seconds
            return sb.ToString();
        }

        var subSecond = time.Millisecond * 1_000_000L + time.Microsecond * 1_000L + time.Nanosecond;

        if (precision != null && int.TryParse(precision, NumberStyles.None, CultureInfo.InvariantCulture, out var digits) && digits > 0 && digits <= 9)
        {
            // Fixed precision: show exactly that many fractional digits
            if (subSecond != 0 || digits > 0)
            {
                sb.Append('.');
                var fraction = subSecond.ToString("D9", CultureInfo.InvariantCulture);
                sb.Append(fraction.AsSpan(0, digits));
            }
        }
        else
        {
            // Auto precision: show only non-zero fractional seconds, trimmed
            if (subSecond != 0)
            {
                sb.Append('.');
                var fraction = subSecond.ToString("D9", CultureInfo.InvariantCulture).TrimEnd('0');
                sb.Append(fraction);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a duration as an ISO 8601 duration string.
    /// </summary>
    /// <param name="duration">The duration record to format.</param>
    /// <param name="precision">Number of fractional digits: -1 for "auto", 0-9 for fixed digits.</param>
    public static string FormatDuration(DurationRecord duration, int precision = -1)
    {
        var sb = new ValueStringBuilder();

        if (duration.Sign() < 0)
        {
            sb.Append('-');
        }

        sb.Append('P');

        var absYears = System.Math.Abs(duration.Years);
        var absMonths = System.Math.Abs(duration.Months);
        var absWeeks = System.Math.Abs(duration.Weeks);
        var absDays = System.Math.Abs(duration.Days);
        var absHours = System.Math.Abs(duration.Hours);
        var absMinutes = System.Math.Abs(duration.Minutes);
        var absSeconds = System.Math.Abs(duration.Seconds);
        var absMilliseconds = System.Math.Abs(duration.Milliseconds);
        var absMicroseconds = System.Math.Abs(duration.Microseconds);
        var absNanoseconds = System.Math.Abs(duration.Nanoseconds);

        if (absYears != 0)
        {
            sb.Append(absYears.ToString(CultureInfo.InvariantCulture));
            sb.Append('Y');
        }

        if (absMonths != 0)
        {
            sb.Append(absMonths.ToString(CultureInfo.InvariantCulture));
            sb.Append('M');
        }

        if (absWeeks != 0)
        {
            sb.Append(absWeeks.ToString(CultureInfo.InvariantCulture));
            sb.Append('W');
        }

        if (absDays != 0)
        {
            sb.Append(absDays.ToString(CultureInfo.InvariantCulture));
            sb.Append('D');
        }

        var hasSubSeconds = absMilliseconds != 0 || absMicroseconds != 0 || absNanoseconds != 0;
        var hasTime = absHours != 0 || absMinutes != 0 || absSeconds != 0 || hasSubSeconds;
        // With non-auto precision, we always need the time section to display seconds
        var needsSeconds = precision >= 0;

        if (hasTime || needsSeconds)
        {
            sb.Append('T');
            if (absHours != 0)
            {
                sb.Append(absHours.ToString(CultureInfo.InvariantCulture));
                sb.Append('H');
            }

            if (absMinutes != 0)
            {
                sb.Append(absMinutes.ToString(CultureInfo.InvariantCulture));
                sb.Append('M');
            }

            if (absSeconds != 0 || hasSubSeconds || needsSeconds)
            {
                // Compute total nanoseconds from seconds components using BigInteger
                // to avoid precision loss with large microsecond/millisecond values.
                // The spec uses ℝ(value) which extracts the mathematical value of each Number.
                var totalNs = (BigInteger) absSeconds * 1_000_000_000
                              + (BigInteger) absMilliseconds * 1_000_000
                              + (BigInteger) absMicroseconds * 1_000
                              + (BigInteger) absNanoseconds;

                var wholeSeconds = totalNs / 1_000_000_000;
                var subsecondNs = totalNs % 1_000_000_000;

                sb.Append(wholeSeconds.ToString(CultureInfo.InvariantCulture));

                if (precision == -1)
                {
                    // Auto: show only significant digits, trim trailing zeros
                    if (subsecondNs != 0)
                    {
                        sb.Append('.');
                        var fraction = ((long) subsecondNs).ToString("D9", CultureInfo.InvariantCulture).TrimEnd('0');
                        sb.Append(fraction);
                    }
                }
                else if (precision > 0)
                {
                    // Fixed precision: show exactly N digits
                    sb.Append('.');
                    var fraction = ((long) subsecondNs).ToString("D9", CultureInfo.InvariantCulture);
                    sb.Append(fraction.AsSpan(0, precision));
                }
                // precision == 0: no decimal part

                sb.Append('S');
            }
        }

        // Handle zero duration
        var signOffset = duration.Sign() < 0 ? 2 : 1;
        if (sb.Length == signOffset)
        {
            if (precision == -1)
            {
                sb.Append("T0S");
            }
            else if (precision == 0)
            {
                sb.Append("T0S");
            }
            else
            {
                sb.Append("T0.");
                sb.Append('0', precision);
                sb.Append('S');
            }
        }

        return sb.ToString();
    }

    // ---- Shared Helper Functions for Temporal Operations ----

    /// <summary>
    /// Gets the options object from a JsValue, throwing TypeError if it's not an object or undefined.
    /// https://tc39.es/proposal-temporal/#sec-getoptionsobject
    /// </summary>
    public static ObjectInstance? GetOptionsObject(Realm realm, JsValue options)
    {
        if (options.IsUndefined())
            return null;

        if (!options.IsObject())
        {
            Throw.TypeError(realm, "Options must be an object");
        }

        return options.AsObject();
    }

    /// <summary>
    /// Reads the "overflow" option from the options argument.
    /// Returns "constrain" (default) or "reject".
    /// </summary>
    public static string GetOverflowOption(Realm realm, JsValue options)
    {
        if (options.IsUndefined())
            return "constrain";

        if (!options.IsObject())
        {
            Throw.TypeError(realm, "Options must be an object");
        }

        var obj = options.AsObject();
        var overflow = obj.Get("overflow");
        if (overflow.IsUndefined())
            return "constrain";

        var overflowStr = TypeConverter.ToString(overflow);
        if (!string.Equals(overflowStr, "constrain", StringComparison.Ordinal) &&
            !string.Equals(overflowStr, "reject", StringComparison.Ordinal))
        {
            Throw.RangeError(realm, "Invalid overflow option");
        }

        return overflowStr;
    }

    /// <summary>
    /// Converts a JsValue to an integer, ensuring it's a finite integer (no fractional part).
    /// Throws RangeError if not a finite number or has fractional part.
    /// Returns 0 (not -0) for zero values per spec (mathematical values don't have -0).
    /// https://tc39.es/proposal-temporal/#sec-tointegerifintegral
    /// </summary>
    public static double ToIntegerIfIntegral(Realm realm, JsValue value)
    {
        // For observable property access in test262, we need to use ToNumber which
        // will call ToPrimitive, which calls Get on "valueOf" and "toString"
        var number = TypeConverter.ToNumber(value);

        if (double.IsNaN(number))
        {
            Throw.RangeError(realm, "Value must be a number");
        }

        if (double.IsInfinity(number))
        {
            Throw.RangeError(realm, "Value must be finite");
        }

        if (number != System.Math.Truncate(number))
        {
            Throw.RangeError(realm, "Value must be an integer");
        }

        // Mathematical values don't have -0
        return number == 0 ? 0 : number;
    }

    /// <summary>
    /// ToIntegerWithTruncation ( value )
    /// Converts a value to integer, truncating any fractional part.
    /// https://tc39.es/proposal-temporal/#sec-tointegerwithtruncation
    /// </summary>
    public static double ToIntegerWithTruncation(Realm realm, JsValue value)
    {
        var number = TypeConverter.ToNumber(value);

        if (double.IsNaN(number))
        {
            Throw.RangeError(realm, "Value must be a number");
        }

        if (double.IsInfinity(number))
        {
            Throw.RangeError(realm, "Value must be finite");
        }

        var truncated = System.Math.Truncate(number);
        // Mathematical values don't have -0
        return truncated == 0 ? 0 : truncated;
    }

    /// <summary>
    /// ToIntegerWithTruncation returning int. Convenience overload.
    /// </summary>
    public static int ToIntegerWithTruncationAsInt(Realm realm, JsValue value)
    {
        return (int) ToIntegerWithTruncation(realm, value);
    }

    /// <summary>
    /// ToIntegerWithTruncation returning int, with a default value for undefined.
    /// </summary>
    public static int ToIntegerWithTruncationAsInt(Realm realm, JsValue value, int defaultValue)
    {
        if (value.IsUndefined())
            return defaultValue;

        return (int) ToIntegerWithTruncation(realm, value);
    }

    /// <summary>
    /// ToPositiveIntegerWithTruncation ( value )
    /// Converts a value to a positive integer, truncating any fractional part.
    /// https://tc39.es/proposal-temporal/#sec-topositiveintegerwithtruncation
    /// </summary>
    public static int ToPositiveIntegerWithTruncation(Realm realm, JsValue value)
    {
        var number = ToIntegerWithTruncation(realm, value);
        if (number < 1)
        {
            Throw.RangeError(realm, "Value must be a positive integer");
        }

        return (int) number;
    }

    /// <summary>
    /// ToTemporalCalendarIdentifier ( calendarLike )
    /// Converts a value to a calendar identifier string.
    /// https://tc39.es/proposal-temporal/#sec-temporal-totemporalcalendaridentifier (calendar.html:666-686)
    /// </summary>
    public static string ToTemporalCalendarIdentifier(Realm realm, JsValue calendarLike)
    {
        // Step 1: If temporalCalendarLike is an Object
        if (calendarLike.IsObject())
        {
            var obj = calendarLike.AsObject();

            // Step 1.a: Fast path - if it's a Temporal object, return its [[Calendar]] internal slot
            // Check for internal slots: [[InitializedTemporalDate]], [[InitializedTemporalDateTime]],
            // [[InitializedTemporalMonthDay]], [[InitializedTemporalYearMonth]], [[InitializedTemporalZonedDateTime]]
            if (obj is JsPlainDate plainDate)
            {
                return plainDate.Calendar;
            }

            if (obj is JsPlainDateTime plainDateTime)
            {
                return plainDateTime.Calendar;
            }

            if (obj is JsPlainMonthDay plainMonthDay)
            {
                return plainMonthDay.Calendar;
            }

            if (obj is JsPlainYearMonth plainYearMonth)
            {
                return plainYearMonth.Calendar;
            }

            if (obj is JsZonedDateTime zonedDateTime)
            {
                return zonedDateTime.Calendar;
            }

            // Not a Temporal object, continue to normal path
        }

        // Step 2: If temporalCalendarLike is not a String, throw a TypeError exception
        if (!calendarLike.IsString())
        {
            Throw.TypeError(realm, "calendar must be a string");
        }

        // Step 3: Let identifier be ? ParseTemporalCalendarString(temporalCalendarLike)
        // ParseTemporalCalendarString accepts either a plain calendar name or an ISO date string
        var calendar = calendarLike.ToString();

        // First try as a plain calendar identifier
        var canonical = CanonicalizeCalendar(calendar);
        if (canonical is not null)
        {
            return canonical;
        }

        // Try parsing as ISO date string to extract calendar annotation
        if (LooksLikeIsoDateString(calendar))
        {
            var extractedCalendar = ExtractCalendarFromIsoString(calendar);
            if (extractedCalendar == string.Empty)
            {
                Throw.RangeError(realm, $"Invalid calendar: {calendar}");
            }

            // If annotation found, canonicalize it; if no annotation, default to iso8601
            var calendarId = extractedCalendar ?? "iso8601";
            canonical = CanonicalizeCalendar(calendarId);
            if (canonical is not null)
            {
                return canonical;
            }
        }

        // Step 4: Return ? CanonicalizeCalendar(identifier)
        Throw.RangeError(realm, $"Invalid calendar: {calendar}");
        return null!;
    }

    /// <summary>
    /// ParseMonthCode ( monthCode )
    /// Parses a month code string and returns the month number.
    /// Month codes are in the format "M" followed by 2 digits (e.g., "M01", "M12")
    /// or with "L" suffix for leap months (e.g., "M05L")
    /// Validates well-formedness: must be uppercase 'M' + exactly 2 digits + optional 'L'
    /// </summary>
    public static int ParseMonthCode(Realm realm, string monthCode)
    {
        // Well-formedness validation (must happen before any other validation per spec)
        // Format must be: 'M' (uppercase) + 2 digits + optional 'L' (uppercase)
        // Valid: "M01", "M12", "M01L", "M13L"
        // Invalid: "m01" (lowercase), "M1" (single digit), "M001" (3 digits), "L99M" (wrong order)

        if (string.IsNullOrEmpty(monthCode))
        {
            Throw.RangeError(realm, $"Invalid monthCode: {monthCode}");
        }

        // Check first character is uppercase 'M'
        if (monthCode[0] != 'M')
        {
            Throw.RangeError(realm, $"Invalid monthCode: {monthCode}");
        }

        // Check length: minimum "M01" (3 chars), maximum "M13L" (4 chars)
        if (monthCode.Length < 3 || monthCode.Length > 4)
        {
            Throw.RangeError(realm, $"Invalid monthCode: {monthCode}");
        }

        // Extract and validate the numeric part (must be exactly 2 digits)
        var digitsPart = monthCode.Substring(1, 2);
        if (!char.IsDigit(digitsPart[0]) || !char.IsDigit(digitsPart[1]))
        {
            Throw.RangeError(realm, $"Invalid monthCode: {monthCode}");
        }

        // Check for optional 'L' suffix (must be uppercase)
        if (monthCode.Length == 4)
        {
            if (monthCode[3] != 'L')
            {
                Throw.RangeError(realm, $"Invalid monthCode: {monthCode}");
            }
            // Leap months (with 'L' suffix) are not allowed in ISO 8601 calendar
            // This will be validated later in calendar-specific code
        }

        // Parse the month number
        // Note: We do NOT validate the range here (e.g., whether 99 is too large)
        // Range validation is calendar-specific "suitability" and happens AFTER year type validation
        // Well-formedness only checks FORMAT (M + 2 digits + optional L), not VALUE
        var month = int.Parse(digitsPart, CultureInfo.InvariantCulture);

        return month;
    }

    /// <summary>
    /// InterpretISODateTimeOffset
    /// Interprets an ISO date-time with timezone offset and returns epoch nanoseconds.
    /// </summary>
    /// <summary>
    /// InterpretISODateTimeOffset ( isoDate, time, offsetBehaviour, offsetNanoseconds, timeZone, disambiguation, offsetOption, matchBehaviour )
    /// https://tc39.es/proposal-temporal/#sec-temporal-interpretisodatetimeoffset
    /// Determines the exact time in timeZone corresponding to the given calendar date and time, and the given UTC offset in nanoseconds.
    /// </summary>
    public static BigInteger InterpretISODateTimeOffset(
        Realm realm,
        ITimeZoneProvider provider,
        IsoDate isoDate,
        IsoTime time,
        string offsetBehaviour,
        long offsetNs,
        string timeZone,
        string disambiguation,
        string offsetOption,
        string matchBehaviour)
    {
        var isoDateTime = new IsoDateTime(isoDate, time);

        // Step 1: If offsetBehaviour is ~wall~, or offsetBehaviour is ~option~ and offsetOption is ~ignore~
        if (string.Equals(offsetBehaviour, "wall", StringComparison.Ordinal) || (string.Equals(offsetBehaviour, "option", StringComparison.Ordinal) && string.Equals(offsetOption, "ignore", StringComparison.Ordinal)))
        {
            return GetEpochNanosecondsFor(realm, provider, timeZone, isoDateTime, disambiguation);
        }

        // Step 2: If offsetBehaviour is ~exact~, or offsetBehaviour is ~option~ and offsetOption is ~use~
        if (string.Equals(offsetBehaviour, "exact", StringComparison.Ordinal) || (string.Equals(offsetBehaviour, "option", StringComparison.Ordinal) && string.Equals(offsetOption, "use", StringComparison.Ordinal)))
        {
            // Calculate epoch nanoseconds: UTC time minus the offset
            // (The offset tells us how much to add to UTC to get local time, so we subtract it to get UTC from local)
            var utcEpochNs = GetUTCEpochNanoseconds(isoDateTime);
            var epochNanoseconds = utcEpochNs - offsetNs;

            // Validate the result is within valid range
            if (!InstantConstructor.IsValidEpochNanoseconds(epochNanoseconds))
            {
                Throw.RangeError(realm, "Resulting instant is outside the valid range");
            }

            return epochNanoseconds;
        }

        // Step 3: Assert offsetBehaviour is ~option~ and offsetOption is ~prefer~ or ~reject~
        // Get possible epoch nanoseconds for this local time in the timezone
        var isoDate2 = isoDateTime.Date;
        CheckISODaysRange(realm, isoDate2);

        var utcEpochNanoseconds = GetUTCEpochNanoseconds(isoDateTime);
        var possibleEpochNs = GetPossibleEpochNanoseconds(realm, provider, timeZone, isoDateTime);

        // Step 4: Try to find a candidate that matches the given offset
        foreach (var candidate in possibleEpochNs)
        {
            // Calculate what offset would be needed for this candidate
            // (offset = local time - UTC time, so offset = utcEpochNanoseconds - candidate)
            var candidateOffsetBig = utcEpochNanoseconds - candidate;

            // Offsets are always within ±24 hours, so they fit in a long
            var candidateOffset = (long) candidateOffsetBig;

            // Check for exact match
            if (candidateOffset == offsetNs)
            {
                return candidate;
            }

            // If matchBehaviour is ~match-minutes~, also accept offsets rounded to nearest minute
            if (string.Equals(matchBehaviour, "match-minutes", StringComparison.Ordinal))
            {
                // Round candidate offset to nearest minute (60 * 10^9 nanoseconds)
                var roundedCandidateNs = RoundNumberToIncrement(candidateOffset, 60L * 1_000_000_000L, "halfExpand");
                if (roundedCandidateNs == offsetNs)
                {
                    return candidate;
                }
            }
        }

        // Step 5: No matching candidate found
        if (string.Equals(offsetOption, "reject", StringComparison.Ordinal))
        {
            Throw.RangeError(realm, "Offset does not match any possible instant for the given date-time in the time zone");
        }

        // Step 6: offsetOption is ~prefer~ - use disambiguation to pick an instant
        return DisambiguatePossibleEpochNanoseconds(realm, provider, possibleEpochNs, timeZone, isoDateTime, disambiguation);
    }

    /// <summary>
    /// Gets a temporal unit option from an options object.
    /// Returns null if the option is undefined, throws if invalid.
    /// https://tc39.es/proposal-temporal/#sec-temporal-totemporalroundingmode
    /// </summary>
    public static string? GetTemporalUnit(
        Realm realm,
        ObjectInstance options,
        string key,
        string[] allowedUnits,
        string? defaultValue)
    {
        var value = options.Get(key);
        if (value.IsUndefined())
            return defaultValue;

        var str = TypeConverter.ToString(value);
        var singular = ToSingularUnit(str);

        var isAllowed = false;
        foreach (var unit in allowedUnits)
        {
            if (string.Equals(unit, singular, StringComparison.Ordinal))
            {
                isAllowed = true;
                break;
            }
        }

        if (!isAllowed)
        {
            Throw.RangeError(realm, $"Invalid value for {key}: {str}");
        }

        return singular;
    }

    /// <summary>
    /// Validates a rounding increment for temporal rounding operations.
    /// https://tc39.es/proposal-temporal/#sec-temporal-validatetemporalroundingincrement
    /// </summary>
    public static void ValidateTemporalRoundingIncrement(
        Realm realm,
        double increment,
        double maximumIncrement,
        bool inclusive)
    {
        if (increment < 1)
        {
            Throw.RangeError(realm, "Rounding increment must be at least 1");
        }

        if (inclusive)
        {
            if (increment > maximumIncrement)
            {
                Throw.RangeError(realm, $"Rounding increment must be at most {maximumIncrement}");
            }
        }
        else
        {
            if (increment >= maximumIncrement)
            {
                Throw.RangeError(realm, $"Rounding increment must be less than {maximumIncrement}");
            }
        }

        // Check if increment is divisible by maximumIncrement for certain units
        if (maximumIncrement > 1)
        {
            var quotient = maximumIncrement / increment;
            if (quotient != System.Math.Floor(quotient))
            {
                Throw.RangeError(realm, "Rounding increment must divide evenly into maximum increment");
            }
        }
    }

    /// <summary>
    /// Floor division for BigInteger (rounds toward negative infinity).
    /// </summary>
    public static BigInteger FloorDivide(BigInteger dividend, long divisor)
    {
        var result = dividend / divisor;
        if (dividend % divisor != 0 && (dividend < 0) != (divisor < 0))
        {
            result -= 1;
        }

        return result;
    }

    /// <summary>
    /// MaximumTemporalDurationRoundingIncrement ( unit )
    /// https://tc39.es/proposal-temporal/#sec-temporal-maximumtemporaldurationroundingincrement
    /// </summary>
    public static int? MaximumTemporalDurationRoundingIncrement(string unit)
    {
        return unit switch
        {
            "year" or "month" or "week" or "day" => null,
            "hour" => 24,
            "minute" or "second" => 60,
            _ => 1000 // millisecond, microsecond, nanosecond
        };
    }

    /// <summary>
    /// GetTemporalRelativeToOption ( options )
    /// Converts a relativeTo option to either a PlainDate or ZonedDateTime.
    /// Returns RelativeToResult where either PlainRelativeTo or ZonedRelativeTo is non-null, or both are null.
    /// https://tc39.es/proposal-temporal/#sec-temporal-gettemporalrelativetooption
    /// </summary>
    public static RelativeToResult GetTemporalRelativeToOption(
        Engine engine,
        Realm realm,
        ObjectInstance? options)
    {
        if (options is null)
            return new RelativeToResult(null, null);

        var relativeToValue = options.Get("relativeTo");
        if (relativeToValue.IsUndefined())
            return new RelativeToResult(null, null);

        // If it's a ZonedDateTime, return it as zonedRelativeTo
        if (relativeToValue is JsZonedDateTime zonedDateTime)
        {
            return new RelativeToResult(null, zonedDateTime);
        }

        // If it's a PlainDate, return it as plainRelativeTo
        if (relativeToValue is JsPlainDate plainDate)
        {
            return new RelativeToResult(plainDate, null);
        }

        // If it's a PlainDateTime, convert to PlainDate
        if (relativeToValue is JsPlainDateTime plainDateTime)
        {
            var plainDateCtor = realm.Intrinsics.TemporalPlainDate;
            var converted = plainDateCtor.Construct(plainDateTime.IsoDateTime.Date, plainDateTime.Calendar);
            return new RelativeToResult(converted, null);
        }

        // For strings and objects, we need to parse them
        var plainDateTimeConstructor = realm.Intrinsics.TemporalPlainDateTime;
        var zonedDateTimeConstructor = realm.Intrinsics.TemporalZonedDateTime;

        // Handle string conversion
        if (relativeToValue.IsString())
        {
            var str = relativeToValue.ToString();

            // Per spec: Check if string has a timezone annotation (like [UTC] or [-07:00])
            // Only strings with timezone annotations should be parsed as ZonedDateTime
            // Strings with Z or offsets but no timezone annotation are PlainDate/PlainDateTime
            bool hasTimezoneAnnotation = false;
            if (str.Contains('['))
            {
                // Check for timezone annotation (bracket without "=" or with non-calendar key)
                var bracketStart = str.IndexOf('[');
                while (bracketStart >= 0 && bracketStart < str.Length)
                {
                    var bracketEnd = str.IndexOf(']', bracketStart);
                    if (bracketEnd < 0) break;

                    var annotation = str.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
                    // Strip critical flag if present
                    if (annotation.Length > 0 && annotation[0] == '!')
                    {
                        annotation = annotation.Substring(1);
                    }

                    // Timezone annotation is one without '=' or with key other than 'u-ca'
                    if (!annotation.Contains('=') || !annotation.StartsWith("u-ca=", StringComparison.Ordinal))
                    {
                        if (!annotation.Contains('='))
                        {
                            // No '=' means it's a timezone identifier like [UTC] or [-07:00]
                            hasTimezoneAnnotation = true;
                            break;
                        }
                    }

                    bracketStart = str.IndexOf('[', bracketEnd + 1);
                }
            }

            if (hasTimezoneAnnotation)
            {
                // Has timezone annotation - parse as ZonedDateTime
                var zdt = zonedDateTimeConstructor.ToTemporalZonedDateTime(relativeToValue, JsValue.Undefined);
                return new RelativeToResult(null, zdt);
            }
            else
            {
                // No timezone annotation - parse as PlainDate/PlainDateTime
                var plainDateCtor = realm.Intrinsics.TemporalPlainDate;
                var pd = plainDateCtor.ToTemporalDate(relativeToValue, "constrain");
                return new RelativeToResult(pd, null);
            }
        }

        // Handle object conversion
        if (relativeToValue.IsObject())
        {
            var obj = relativeToValue.AsObject();

            // Read all fields in alphabetical order using PrepareCalendarFields
            // This reads: calendar, day, hour, microsecond, millisecond, minute, month,
            // monthCode, nanosecond, offset, second, timeZone, year
            var fields = PrepareCalendarFieldsForRelativeTo(engine, realm, obj);

            // Check if timeZone is present to determine if this is PlainDate or ZonedDateTime
            if (fields.TimeZone is null)
            {
                // No timeZone - create PlainDate using existing constructor
                // which will validate the fields
                var plainDateCtor = realm.Intrinsics.TemporalPlainDate;

                // Build a simple object with just the fields PlainDate needs
                // We already read all fields in order, so this won't cause additional reads
                var resultPlainDate = CreatePlainDateFromFields(realm, fields);
                return new RelativeToResult(resultPlainDate, null);
            }
            else
            {
                // Has timeZone - create ZonedDateTime
                var zonedDateTimeCtor = realm.Intrinsics.TemporalZonedDateTime;
                var resultZonedDateTime = CreateZonedDateTimeFromFields(engine, realm, fields);
                return new RelativeToResult(null, resultZonedDateTime);
            }
        }

        Throw.TypeError(realm, "relativeTo must be a Temporal object or string");
        return new RelativeToResult(null, null);
    }

    /// <summary>
    /// Result type for GetTemporalRelativeToOption.
    /// </summary>
    internal readonly record struct RelativeToResult(JsPlainDate? PlainRelativeTo, JsZonedDateTime? ZonedRelativeTo);

    /// <summary>
    /// Simplified Calendar Fields Record for relativeTo processing.
    /// Holds fields read from an object in PrepareCalendarFields.
    /// </summary>
    private readonly record struct RelativeToFields(
        string Calendar,
        int? Day,
        int? Hour,
        int? Microsecond,
        int? Millisecond,
        int? Minute,
        int? Month,
        string? MonthCode,
        int? Nanosecond,
        string? Offset,
        int? Second,
        string? TimeZone,
        int? Year);


    /// <summary>
    /// PrepareCalendarFieldsForRelativeTo ( calendar, fields )
    /// Simplified version of PrepareCalendarFields for GetTemporalRelativeToOption use case.
    /// Reads calendar and all date/time/timezone fields in alphabetical property order.
    /// https://tc39.es/proposal-temporal/#sec-temporal-preparecalendarfields
    /// </summary>
    private static RelativeToFields PrepareCalendarFieldsForRelativeTo(
        Engine engine,
        Realm realm,
        ObjectInstance fields)
    {
        // Step 1: Get calendar via GetTemporalCalendarIdentifierWithISODefault
        // This reads the "calendar" property
        var calendarProp = fields.Get("calendar");
        string calendar = "iso8601";
        if (!calendarProp.IsUndefined())
        {
            calendar = ToTemporalCalendarIdentifier(realm, calendarProp);
        }

        // Step 2: Read all other fields in alphabetical order
        // Property keys in alphabetical order: day, era, eraYear, hour, microsecond, millisecond, minute,
        // month, monthCode, nanosecond, offset, second, timeZone, year

        // day - to-positive-integer-with-truncation
        var dayValue = fields.Get("day");
        int? day = dayValue.IsUndefined() ? null : ToPositiveIntegerWithTruncation(realm, dayValue);

        // era/eraYear - read for era-supporting calendars (alphabetically between day and hour)
        var eraYear = ReadEraFields(realm, fields, calendar);

        // hour - to-integer-with-truncation
        var hourValue = fields.Get("hour");
        int? hour = hourValue.IsUndefined()
            ? null
            : (int) ToIntegerWithTruncation(realm, hourValue);

        // microsecond - to-integer-with-truncation
        var microsecondValue = fields.Get("microsecond");
        int? microsecond = microsecondValue.IsUndefined()
            ? null
            : (int) ToIntegerWithTruncation(realm, microsecondValue);

        // millisecond - to-integer-with-truncation
        var millisecondValue = fields.Get("millisecond");
        int? millisecond = millisecondValue.IsUndefined()
            ? null
            : (int) ToIntegerWithTruncation(realm, millisecondValue);

        // minute - to-integer-with-truncation
        var minuteValue = fields.Get("minute");
        int? minute = minuteValue.IsUndefined()
            ? null
            : (int) ToIntegerWithTruncation(realm, minuteValue);

        // month - to-positive-integer-with-truncation
        var monthValue = fields.Get("month");
        int? month = monthValue.IsUndefined() ? null : ToPositiveIntegerWithTruncation(realm, monthValue);

        // monthCode - to-month-code (which calls ParseMonthCode and ToString)
        var monthCodeValue = fields.Get("monthCode");
        string? monthCode = null;
        if (!monthCodeValue.IsUndefined())
        {
            // monthCode must be a string (per spec)
            // Handle objects specially: call ToPrimitive and ensure result is a string
            if (monthCodeValue.IsObject())
            {
                var primitive = TypeConverter.ToPrimitive(monthCodeValue, Types.String);
                if (!primitive.IsString())
                {
                    Throw.TypeError(realm, "monthCode must be a string");
                }

                monthCode = primitive.ToString();
            }
            else if (monthCodeValue.IsString())
            {
                monthCode = TypeConverter.ToString(monthCodeValue);
            }
            else
            {
                // Number, BigInt, Boolean, Null - reject; Symbol throws from ToString
                if (monthCodeValue.Type != Types.Symbol)
                {
                    Throw.TypeError(realm, "monthCode must be a string");
                }

                monthCode = TypeConverter.ToString(monthCodeValue);
            }
            // Validate it's a valid month code format (M followed by digits, optionally with L suffix)
            // The ParseMonthCode validation will happen later when we use it
        }

        // nanosecond - to-integer-with-truncation
        var nanosecondValue = fields.Get("nanosecond");
        int? nanosecond = nanosecondValue.IsUndefined()
            ? null
            : (int) ToIntegerWithTruncation(realm, nanosecondValue);

        // offset - to-offset-string
        var offsetValue = fields.Get("offset");
        string? offset = null;
        if (!offsetValue.IsUndefined())
        {
            offset = ToOffsetString(realm, offsetValue);
        }

        // second - to-integer-with-truncation
        var secondValue = fields.Get("second");
        int? second = secondValue.IsUndefined()
            ? null
            : (int) ToIntegerWithTruncation(realm, secondValue);

        // timeZone - to-temporal-time-zone-identifier
        var timeZoneValue = fields.Get("timeZone");
        string? timeZone = null;
        if (!timeZoneValue.IsUndefined())
        {
            timeZone = ToTemporalTimeZoneIdentifier(
                engine, realm, timeZoneValue);
        }

        // year - use eraYear if computed, otherwise read from property
        int? year;
        if (eraYear.HasValue)
        {
            year = eraYear.Value;
            fields.Get("year");
        }
        else
        {
            var yearValue = fields.Get("year");
            year = yearValue.IsUndefined()
                ? null
                : (int) ToIntegerWithTruncation(realm, yearValue);
        }

        return new RelativeToFields(calendar, day, hour, microsecond, millisecond, minute,
            month, monthCode, nanosecond, offset, second, timeZone, year);
    }

    /// <summary>
    /// Helper to create PlainDate from RelativeToFields.
    /// </summary>
    private static JsPlainDate CreatePlainDateFromFields(Realm realm, RelativeToFields fields)
    {
        // Required fields: year, day, and (month or monthCode)
        if (!fields.Year.HasValue)
        {
            Throw.TypeError(realm, "Missing required property: year");
        }

        if (!fields.Day.HasValue)
        {
            Throw.TypeError(realm, "Missing required property: day");
        }

        // Determine month from either month or monthCode
        int month;
        if (fields.Month.HasValue && !string.IsNullOrEmpty(fields.MonthCode))
        {
            // Both provided - parse monthCode and verify they match
            var parsedMonthCode = ParseMonthCode(realm, fields.MonthCode!);
            if (parsedMonthCode != fields.Month.Value)
            {
                Throw.RangeError(realm, "month and monthCode do not match");
            }

            month = fields.Month.Value;
        }
        else if (fields.Month.HasValue)
        {
            month = fields.Month.Value;
        }
        else if (!string.IsNullOrEmpty(fields.MonthCode))
        {
            month = ParseMonthCode(realm, fields.MonthCode!);
        }
        else
        {
            Throw.TypeError(realm, "Missing required property: month or monthCode");
            return null!;
        }

        // Regulate the date
        var isoDate = RegulateIsoDate(fields.Year.Value, month, fields.Day.Value, "constrain");
        if (isoDate is null)
        {
            Throw.RangeError(realm, "Invalid date");
        }

        var plainDateCtor = realm.Intrinsics.TemporalPlainDate;
        return plainDateCtor.Construct(isoDate.Value, fields.Calendar);
    }

    /// <summary>
    /// Helper to create ZonedDateTime from RelativeToFields.
    /// </summary>
    private static JsZonedDateTime CreateZonedDateTimeFromFields(Engine engine, Realm realm, RelativeToFields fields)
    {
        // Required fields: year, day, month (or monthCode), timeZone
        if (!fields.Year.HasValue)
        {
            Throw.TypeError(realm, "Missing required property: year");
        }

        if (!fields.Day.HasValue)
        {
            Throw.TypeError(realm, "Missing required property: day");
        }

        if (fields.TimeZone is null)
        {
            Throw.TypeError(realm, "Missing required property: timeZone");
        }

        // Determine month
        int month;
        if (fields.Month.HasValue && !string.IsNullOrEmpty(fields.MonthCode))
        {
            var parsedMonthCode = ParseMonthCode(realm, fields.MonthCode!);
            if (parsedMonthCode != fields.Month.Value)
            {
                Throw.RangeError(realm, "month and monthCode do not match");
            }

            month = fields.Month.Value;
        }
        else if (fields.Month.HasValue)
        {
            month = fields.Month.Value;
        }
        else if (!string.IsNullOrEmpty(fields.MonthCode))
        {
            month = ParseMonthCode(realm, fields.MonthCode!);
        }
        else
        {
            Throw.TypeError(realm, "Missing required property: month or monthCode");
            return null!;
        }

        // Regulate date and time
        var isoDate = RegulateIsoDate(fields.Year.Value, month, fields.Day.Value, "constrain");
        if (isoDate is null)
        {
            Throw.RangeError(realm, "Invalid date");
        }

        var time = RegulateIsoTime(
            fields.Hour ?? 0,
            fields.Minute ?? 0,
            fields.Second ?? 0,
            fields.Millisecond ?? 0,
            fields.Microsecond ?? 0,
            fields.Nanosecond ?? 0,
            "constrain");

        if (time is null)
        {
            Throw.RangeError(realm, "Invalid time");
        }

        var isoDateTime = new IsoDateTime(isoDate.Value, time.Value);

        // Convert to epoch nanoseconds using InterpretISODateTimeOffset
        // For objects, offsetBehaviour is ~option~ if offset is provided, else ~wall~
        var offsetBehaviour = fields.Offset is not null ? "option" : "wall";
        long offsetNs = 0;
        if (fields.Offset is not null)
        {
            var parsedOffset = ParseOffsetString(fields.Offset);
            if (parsedOffset is null)
            {
                Throw.RangeError(realm, "Invalid offset string");
            }

            offsetNs = parsedOffset.Value;
        }

        var provider = engine.Options.Temporal.TimeZoneProvider;
        var epochNs = InterpretISODateTimeOffset(
            realm,
            provider,
            isoDate.Value,
            time.Value,
            offsetBehaviour,
            offsetNs,
            fields.TimeZone,
            "compatible", // disambiguation
            "reject", // offset option for objects
            "match-exactly"); // match behaviour

        var zonedDateTimeCtor = realm.Intrinsics.TemporalZonedDateTime;
        return zonedDateTimeCtor.Construct(epochNs, fields.TimeZone, fields.Calendar);
    }

    // Valid rounding modes as per ECMAScript Temporal specification
    private static readonly HashSet<string> ValidRoundingModes = new(StringComparer.Ordinal)
    {
        "ceil",
        "floor",
        "expand",
        "trunc",
        "halfCeil",
        "halfFloor",
        "halfExpand",
        "halfTrunc",
        "halfEven"
    };

    /// <summary>
    /// GetRoundingModeOption ( normalizedOptions, fallback )
    /// https://tc39.es/proposal-temporal/#sec-temporal-getroundingmodeoption
    /// </summary>
    public static string GetRoundingModeOption(
        Realm realm,
        JsValue normalizedOptions,
        string fallback)
    {
        // 1. If normalizedOptions is undefined, return fallback.
        if (normalizedOptions.IsUndefined())
        {
            return fallback;
        }

        // 2. Assert: Type(normalizedOptions) is Object.
        if (!normalizedOptions.IsObject())
        {
            Throw.TypeError(realm, "Options must be an object");
        }

        var options = normalizedOptions.AsObject();

        // 3. Let stringValue be ? Get(normalizedOptions, "roundingMode").
        var value = options.Get("roundingMode");

        // 4. If stringValue is undefined, return fallback.
        if (value.IsUndefined())
        {
            return fallback;
        }

        // 5. Let stringValue be ? ToString(stringValue).
        var stringValue = TypeConverter.ToString(value);

        // 6. If stringValue is not in the list of valid rounding modes, throw a RangeError exception.
        if (!ValidRoundingModes.Contains(stringValue))
        {
            Throw.RangeError(realm, $"Invalid rounding mode: {stringValue}");
        }

        // 7. Return stringValue.
        return stringValue;
    }

    /// <summary>
    /// GetRoundingIncrementOption ( normalizedOptions )
    /// https://tc39.es/proposal-temporal/#sec-temporal-getroundingincrementoption
    /// </summary>
    public static int GetRoundingIncrementOption(Realm realm, JsValue normalizedOptions)
    {
        // 1. If normalizedOptions is undefined, return 1.
        if (normalizedOptions.IsUndefined())
        {
            return 1;
        }

        // 2. Assert: Type(normalizedOptions) is Object.
        if (!normalizedOptions.IsObject())
        {
            Throw.TypeError(realm, "Options must be an object");
        }

        var options = normalizedOptions.AsObject();

        // 3. Let increment be ? Get(normalizedOptions, "roundingIncrement").
        var increment = options.Get("roundingIncrement");

        // 4. If increment is undefined, return 1.
        if (increment.IsUndefined())
        {
            return 1;
        }

        // 5. Let integerIncrement be ? ToNumber(increment).
        var number = TypeConverter.ToNumber(increment);

        // 6. If integerIncrement is NaN, +∞, or -∞, throw a RangeError exception.
        if (double.IsNaN(number) || double.IsInfinity(number))
        {
            Throw.RangeError(realm, "Rounding increment must be finite");
        }

        // 7. Let integerIncrement be truncate(ℝ(integerIncrement)).
        var integerIncrement = (int) System.Math.Truncate(number);

        // 8. If integerIncrement < 1 or integerIncrement > 10^9, throw a RangeError exception.
        if (integerIncrement < 1 || integerIncrement > 1_000_000_000)
        {
            Throw.RangeError(realm, "Rounding increment must be between 1 and 1000000000");
        }

        // 9. Return integerIncrement.
        return integerIncrement;
    }

    /// <summary>
    /// Record to hold difference operation settings.
    /// </summary>
    public record DifferenceSettings(
        string LargestUnit,
        string SmallestUnit,
        string RoundingMode,
        int RoundingIncrement);

    /// <summary>
    /// GetDifferenceSettings ( normalizedOptions, operation, largestUnitFallback, smallestUnitFallback, disallowedUnits )
    /// https://tc39.es/proposal-temporal/#sec-temporal-getdifferencesettings
    /// </summary>
    public static DifferenceSettings GetDifferenceSettings(
        Realm realm,
        JsValue normalizedOptions,
        string operation,
        string fallbackSmallestUnit,
        string fallbackLargestUnit,
        string[] allowedUnits,
        string[]? disallowedUnits = null)
    {
        var options = GetOptionsObject(realm, normalizedOptions);

        // NOTE: The following steps read options and perform independent validation in alphabetical order.
        // Spec: https://tc39.es/proposal-temporal/#sec-temporal-getdifferencesettings (abstractops.html line 1878)

        // Step 1: Read largestUnit (alphabetically first)
        string largestUnit;
        string? largestUnitInput = null;
        var largestUnitValue = options?.Get("largestUnit") ?? JsValue.Undefined;
        if (largestUnitValue.IsUndefined())
        {
            largestUnit = fallbackLargestUnit;
        }
        else
        {
            largestUnitInput = TypeConverter.ToString(largestUnitValue);
            largestUnit = ToSingularUnit(largestUnitInput);
        }

        // Step 2: Read roundingIncrement (alphabetically second)
        var roundingIncrement = GetRoundingIncrementOption(realm, normalizedOptions);

        // Step 3: Read roundingMode (alphabetically third)
        var roundingMode = GetRoundingModeOption(realm, normalizedOptions, "trunc");

        // Step 4: Read smallestUnit (alphabetically fourth)
        string smallestUnit;
        string? smallestUnitInput = null;
        var smallestUnitValue = options?.Get("smallestUnit") ?? JsValue.Undefined;
        if (smallestUnitValue.IsUndefined())
        {
            smallestUnit = fallbackSmallestUnit;
        }
        else
        {
            smallestUnitInput = TypeConverter.ToString(smallestUnitValue);
            smallestUnit = ToSingularUnit(smallestUnitInput);
        }

        // Step 5-7: Validate largestUnit (after all options have been read)
        if (largestUnitInput != null)
        {
            // "auto" is a valid value that will be resolved later
            if (!string.Equals(largestUnit, "auto", StringComparison.Ordinal))
            {
                // Validate it's in allowed units
                bool isAllowed = false;
                foreach (var unit in allowedUnits)
                {
                    if (string.Equals(largestUnit, unit, StringComparison.Ordinal))
                    {
                        isAllowed = true;
                        break;
                    }
                }

                if (!isAllowed)
                {
                    Throw.RangeError(realm, $"Invalid largestUnit: {largestUnitInput}");
                }
            }
        }

        // Check if largestUnit is disallowed
        if (disallowedUnits != null)
        {
            foreach (var disallowed in disallowedUnits)
            {
                if (string.Equals(largestUnit, disallowed, StringComparison.Ordinal))
                {
                    Throw.RangeError(realm, $"largestUnit {largestUnit} is not allowed");
                }
            }
        }

        // Step 8-10: Validate smallestUnit (after all options have been read)
        if (smallestUnitInput != null)
        {
            // Validate it's in allowed units
            bool isAllowed = false;
            foreach (var unit in allowedUnits)
            {
                if (string.Equals(smallestUnit, unit, StringComparison.Ordinal))
                {
                    isAllowed = true;
                    break;
                }
            }

            if (!isAllowed)
            {
                Throw.RangeError(realm, $"Invalid smallestUnit: {smallestUnitInput}");
            }
        }

        // Check if smallestUnit is disallowed
        if (disallowedUnits != null)
        {
            foreach (var disallowed in disallowedUnits)
            {
                if (string.Equals(smallestUnit, disallowed, StringComparison.Ordinal))
                {
                    Throw.RangeError(realm, $"smallestUnit {smallestUnit} is not allowed");
                }
            }
        }

        // Step 13: Validate that smallestUnit is not larger than largestUnit
        // Skip validation if largestUnit is "auto" (will be resolved by caller)
        if (!string.Equals(largestUnit, "auto", StringComparison.Ordinal))
        {
            if (TemporalUnitIndex(smallestUnit) < TemporalUnitIndex(largestUnit))
            {
                Throw.RangeError(realm, "smallestUnit must be smaller than largestUnit");
            }
        }

        // Step 14-15: Validate rounding increment against the maximum for the smallest unit
        var maximum = MaximumTemporalDurationRoundingIncrement(smallestUnit);
        if (maximum is not null)
        {
            ValidateTemporalRoundingIncrement(realm, roundingIncrement, maximum.Value, false);
        }

        // Step 16-17: Negate rounding mode for "since" operation
        // Spec: https://tc39.es/proposal-temporal/#sec-temporal-getdifferencesettings (abstractops.html lines 1896-1897)
        if (string.Equals(operation, "since", StringComparison.Ordinal))
        {
            roundingMode = NegateRoundingMode(roundingMode);
        }

        return new DifferenceSettings(largestUnit, smallestUnit, roundingMode, roundingIncrement);
    }

    /// <summary>
    /// Record for Nudge operation results.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct DurationNudgeResult(
        DurationRecord Duration,
        BigInteger NudgedEpochNs,
        bool DidExpandCalendarUnit);

    /// <summary>
    /// InternalDurationSign ( duration )
    /// Returns sign of an internal duration record.
    /// </summary>
    private static int InternalDurationSign(DurationRecord duration)
    {
        return duration.Sign();
    }

    /// <summary>
    /// TotalTimeDuration ( timeDuration, unit )
    /// https://tc39.es/proposal-temporal/#sec-temporal-totaltimeduration
    /// Returns total of time duration in the given unit.
    /// NOTE: The spec says this cannot be implemented directly using floating-point arithmetic
    /// when 𝔽(timeDuration) is not a safe integer. Uses exact BigInteger division.
    /// </summary>
    internal static double TotalTimeDuration(BigInteger timeDuration, string unit)
    {
        var divisor = unit switch
        {
            "hour" => NanosecondsPerHour,
            "minute" => NanosecondsPerMinute,
            "second" => NanosecondsPerSecond,
            "millisecond" => NanosecondsPerMillisecond,
            "microsecond" => NanosecondsPerMicrosecond,
            "nanosecond" => 1L,
            "day" => NanosecondsPerDay,
            _ => throw new ArgumentException($"Invalid time unit: {unit}", nameof(unit))
        };

        return DivideBigIntToF64(timeDuration, divisor);
    }

    /// <summary>
    /// Divides a BigInteger by a long divisor and returns the nearest float64 (𝔽 operation).
    /// Uses exact arithmetic to avoid precision loss from converting BigInteger to double first.
    /// </summary>
    internal static double DivideBigIntToF64(BigInteger numerator, long denominator)
    {
        return DivideBigIntToF64(numerator, new BigInteger(denominator));
    }

    internal static double DivideBigIntToF64(BigInteger numerator, BigInteger denominator)
    {
        if (numerator.IsZero)
            return 0.0;
        if (denominator == BigInteger.One)
            return BigIntToF64(numerator);

        var negative = (numerator.Sign < 0) != (denominator.Sign < 0);
        var absNum = BigInteger.Abs(numerator);
        var absDen = BigInteger.Abs(denominator);

        var q = BigInteger.DivRem(absNum, absDen, out var r);

        if (r.IsZero)
        {
            var exact = BigIntToF64(q);
            return negative ? -exact : exact;
        }

        // Compute the exact rational q + r/absDen as the nearest float64.
        // Use string-based conversion for correctness: build a decimal string with
        // enough fractional digits and use double.Parse which is correctly rounded.
        // We need ~20 significant digits. When q=0, leading zeros in the fraction
        // don't count, so we use 40 fractional digits to handle all Temporal cases.
        const int fracDigits = 40;
        var scale = BigInteger.Pow(10, fracDigits);
        var fracBig = r * scale / absDen;
        var fracStr = fracBig.ToString(CultureInfo.InvariantCulture).PadLeft(fracDigits, '0');

        var result = double.Parse(
            string.Concat(q.ToString(CultureInfo.InvariantCulture), ".", fracStr),
            System.Globalization.NumberStyles.Float,
            CultureInfo.InvariantCulture);
        return negative ? -result : result;
    }

    /// <summary>
    /// Add24HourDaysToTimeDuration ( timeDuration, days )
    /// https://tc39.es/proposal-temporal/#sec-temporal-add24hourdaystonormalizedtimeduration
    /// Adds days (as 24-hour periods) to a time duration.
    /// </summary>
    public static BigInteger Add24HourDaysToTimeDuration(BigInteger timeDuration, double days)
    {
        return timeDuration + new BigInteger(days) * NanosecondsPerDay;
    }

    /// <summary>
    /// Add24HourDaysToTimeDuration with maxTimeDuration range check.
    /// Throws RangeError if abs(result) exceeds maxTimeDuration.
    /// </summary>
    public static BigInteger Add24HourDaysToTimeDurationChecked(Realm realm, BigInteger timeDuration, double days)
    {
        var result = timeDuration + new BigInteger(days) * NanosecondsPerDay;
        if (BigInteger.Abs(result) > MaxTimeDuration)
        {
            Throw.RangeError(realm, "Time duration out of range");
        }

        return result;
    }

    /// <summary>
    /// AddTimeDurationToEpochNanoseconds ( timeDuration, epochNs )
    /// </summary>
    private static BigInteger AddTimeDurationToEpochNanoseconds(BigInteger timeDuration, BigInteger epochNs)
    {
        return epochNs + timeDuration;
    }

    /// <summary>
    /// TimeDurationFromEpochNanosecondsDifference ( one, two )
    /// </summary>
    private static BigInteger TimeDurationFromEpochNanosecondsDifference(BigInteger one, BigInteger two)
    {
        return one - two;
    }

    /// <summary>
    /// TimeDurationSign ( timeDuration )
    /// </summary>
    private static int TimeDurationSign(BigInteger timeDuration)
    {
        return timeDuration.Sign;
    }

    /// <summary>
    /// RoundTimeDurationToIncrement ( d, increment, roundingMode )
    /// https://tc39.es/proposal-temporal/#sec-temporal-roundtimedurationtoincrement
    /// It rounds the total number of nanoseconds in the time duration d to the nearest multiple of increment.
    /// </summary>
    private static BigInteger RoundTimeDurationToIncrement(Realm realm, BigInteger d, BigInteger increment, string roundingMode)
    {
        var quotient = BigInteger.DivRem(d, increment, out var remainder);
        var isNegative = d < 0;

        BigInteger rounded;
        switch (roundingMode)
        {
            case "ceil":
                rounded = remainder > 0 ? quotient + 1 : quotient;
                break;
            case "floor":
                rounded = remainder < 0 ? quotient - 1 : quotient;
                break;
            case "trunc":
                rounded = quotient;
                break;
            case "expand":
                rounded = remainder != 0 ? (isNegative ? quotient - 1 : quotient + 1) : quotient;
                break;
            case "halfExpand":
                var halfIncrement = increment / 2;
                rounded = BigInteger.Abs(remainder) >= halfIncrement ? (isNegative ? quotient - 1 : quotient + 1) : quotient;
                break;
            case "halfTrunc":
                rounded = BigInteger.Abs(remainder) > increment / 2 ? (isNegative ? quotient - 1 : quotient + 1) : quotient;
                break;
            case "halfCeil":
                rounded = remainder >= increment / 2 ? quotient + 1 : quotient;
                break;
            case "halfFloor":
                rounded = remainder > increment / 2 || (remainder == increment / 2 && isNegative) ? quotient + 1 : quotient;
                break;
            case "halfEven":
                var half = increment / 2;
                if (BigInteger.Abs(remainder) == half)
                {
                    rounded = quotient % 2 == 0 ? quotient : (isNegative ? quotient - 1 : quotient + 1);
                }
                else
                {
                    rounded = BigInteger.Abs(remainder) > half ? (isNegative ? quotient - 1 : quotient + 1) : quotient;
                }

                break;
            default:
                rounded = quotient;
                break;
        }

        var result = rounded * increment;
        if (BigInteger.Abs(result) > MaxTimeDuration)
        {
            Throw.RangeError(realm, "Time duration out of range");
        }

        return result;
    }

    /// <summary>
    /// AddTimeDuration ( one, two )
    /// https://tc39.es/proposal-temporal/#sec-temporal-addtimeduration
    /// </summary>
    public static BigInteger AddTimeDuration(Realm realm, BigInteger one, BigInteger two)
    {
        var result = one + two;
        if (BigInteger.Abs(result) > MaxTimeDuration)
        {
            Throw.RangeError(realm, "Time duration out of range");
        }

        return result;
    }

    /// <summary>
    /// CombineDateAndTimeDuration ( dateDurationRecord, timeDuration )
    /// Combines a date duration (from DurationRecord's date components) with time duration.
    /// Per spec: keeps dateDuration's days and adds time components from timeDuration.
    /// </summary>
    internal static DurationRecord CombineDateAndTimeDuration(DurationRecord dateDuration, BigInteger timeDuration, string largestUnit = "hour")
    {
        // Balance time duration based on largestUnit
        // For time units, balance only up to that unit
        string balanceUnit = largestUnit switch
        {
            "year" or "month" or "week" or "day" => "hour", // Date units -> balance to hour
            _ => largestUnit // Time units -> balance to that unit only
        };
        var balanced = BalanceTimeDuration(timeDuration, balanceUnit);

        // Preserve dateDuration's days - only use time components from balanced
        return new DurationRecord(
            dateDuration.Years,
            dateDuration.Months,
            dateDuration.Weeks,
            dateDuration.Days, // Keep the days from dateDuration, not balanced
            balanced.Hours,
            balanced.Minutes,
            balanced.Seconds,
            balanced.Milliseconds,
            balanced.Microseconds,
            balanced.Nanoseconds);
    }

    /// <summary>
    /// NudgeToDayOrTime ( duration, destEpochNs, largestUnit, increment, smallestUnit, roundingMode )
    /// https://tc39.es/proposal-temporal/#sec-temporal-nudgetodayortime
    /// </summary>
    public static DurationNudgeResult NudgeToDayOrTime(
        DurationRecord duration,
        BigInteger destEpochNs,
        string largestUnit,
        int increment,
        string smallestUnit,
        string roundingMode)
    {
        // Step 1: Convert to time duration (days as 24-hour periods)
        var timeDuration = Add24HourDaysToTimeDuration(
            TimeDurationFromComponents(duration),
            duration.Days);

        // Step 2: Get unit length in nanoseconds
        var unitLength = smallestUnit switch
        {
            "day" => NanosecondsPerDay,
            "hour" => NanosecondsPerHour,
            "minute" => NanosecondsPerMinute,
            "second" => NanosecondsPerSecond,
            "millisecond" => NanosecondsPerMillisecond,
            "microsecond" => NanosecondsPerMicrosecond,
            "nanosecond" => 1L,
            _ => throw new ArgumentException($"Invalid time unit: {smallestUnit}", nameof(smallestUnit))
        };

        // Step 3: Round to increment (use BigInteger to avoid long overflow)
        var incrementNs = (BigInteger) unitLength * increment;
        var roundedTime = RoundBigIntegerToIncrement(timeDuration, incrementNs, roundingMode);

        // Step 4: Calculate difference
        var diffTime = roundedTime - timeDuration;

        // Step 5: Calculate whole days before and after rounding
        var wholeDays = (long) (timeDuration / NanosecondsPerDay);
        var roundedWholeDays = (long) (roundedTime / NanosecondsPerDay);
        var dayDelta = roundedWholeDays - wholeDays;

        // Step 6-8: Check if days expanded
        int dayDeltaSign = dayDelta < 0 ? -1 : dayDelta > 0 ? 1 : 0;
        int timeDurationSign = timeDuration.Sign;
        bool didExpandDays = (dayDeltaSign == timeDurationSign);

        // Step 9: Calculate nudged epoch nanoseconds
        var nudgedEpochNs = destEpochNs + diffTime;

        // Step 10-12: Separate days and remainder based on largestUnit category
        double days = 0;
        BigInteger remainder = roundedTime;

        // Check if largestUnit is a date unit (year, month, week, day)
        bool largestIsDateUnit = largestUnit is "year" or "month" or "week" or "day";
        if (largestIsDateUnit)
        {
            days = roundedWholeDays;
            remainder = roundedTime - (new BigInteger(roundedWholeDays) * NanosecondsPerDay);
        }

        // Step 13-14: Create result duration
        var dateDuration = new DurationRecord(duration.Years, duration.Months, duration.Weeks, days, 0, 0, 0, 0, 0, 0);
        var resultDuration = CombineDateAndTimeDuration(dateDuration, remainder, largestUnit);

        return new DurationNudgeResult(resultDuration, nudgedEpochNs, didExpandDays);
    }

    /// <summary>
    /// RoundRelativeDuration ( duration, originEpochNs, destEpochNs, isoDateTime, largestUnit, increment, smallestUnit, roundingMode )
    /// https://tc39.es/proposal-temporal/#sec-temporal-roundrelativeduration
    /// Handles time units with NudgeToDayOrTime and calendar units with NudgeToCalendarUnit.
    /// </summary>
    /// <summary>
    /// RoundRelativeDuration ( duration, originEpochNs, destEpochNs, isoDateTime, timeZone, calendar, largestUnit, increment, smallestUnit, roundingMode )
    /// https://tc39.es/proposal-temporal/#sec-temporal-roundrelativeduration
    /// </summary>
    public static DurationRecord RoundRelativeDuration(
        Realm realm,
        ITimeZoneProvider? timeZoneProvider,
        DurationRecord duration,
        BigInteger originEpochNs,
        BigInteger destEpochNs,
        IsoDateTime isoDateTime,
        string? timeZone,
        string calendar,
        string largestUnit,
        int increment,
        string smallestUnit,
        string roundingMode)
    {
        // Step 1-2: Determine if we need calendar-aware rounding
        bool irregularLengthUnit = IsCalendarUnit(smallestUnit);

        // Step 3: If timeZone is not unset and smallestUnit is day, set irregularLengthUnit to true
        if (timeZone != null && string.Equals(smallestUnit, "day", StringComparison.Ordinal))
        {
            irregularLengthUnit = true;
        }

        // Step 4: Get sign
        int sign = InternalDurationSign(duration) < 0 ? -1 : 1;

        // Steps 5-7: Nudge operation
        DurationNudgeResult nudgeResult;

        if (irregularLengthUnit)
        {
            // Calendar units (year/month/week) or day with timezone - use NudgeToCalendarUnit
            var result = NudgeToCalendarUnit(realm, sign, duration, originEpochNs, destEpochNs, isoDateTime, timeZone, timeZoneProvider, calendar, increment, smallestUnit, roundingMode);
            nudgeResult = result.NudgeResult;
        }
        else if (timeZone != null)
        {
            // Time units with timezone - use NudgeToZonedTime
            nudgeResult = NudgeToZonedTime(realm, timeZoneProvider!, sign, duration, isoDateTime, timeZone, calendar, (long) increment, smallestUnit, roundingMode);
        }
        else
        {
            // Time units (day/hour/minute/second/ms/μs/ns) without timezone - use NudgeToDayOrTime
            nudgeResult = NudgeToDayOrTime(duration, destEpochNs, largestUnit, increment, smallestUnit, roundingMode);
        }

        // Step 8: Get rounded duration
        var resultDuration = nudgeResult.Duration;

        // Step 9-10: Bubble up if needed
        if (nudgeResult.DidExpandCalendarUnit && !string.Equals(smallestUnit, "week", StringComparison.Ordinal))
        {
            var startUnit = LargerOfTwoTemporalUnits(smallestUnit, "day");
            resultDuration = BubbleRelativeDuration(realm, sign, resultDuration, nudgeResult.NudgedEpochNs, isoDateTime, calendar, largestUnit, startUnit);
        }

        return resultDuration;
    }

    /// <summary>
    /// GetUTCEpochNanoseconds ( isoDateTime )
    /// Converts an ISO date-time to UTC epoch nanoseconds (no timezone offset).
    /// </summary>
    internal static BigInteger GetUTCEpochNanoseconds(IsoDateTime isoDateTime)
    {
        // Calculate days since Unix epoch
        var daysSinceEpoch = IsoDateToDays(isoDateTime.Date.Year, isoDateTime.Date.Month, isoDateTime.Date.Day);

        // Calculate total nanoseconds
        BigInteger totalNs = daysSinceEpoch;
        totalNs *= NanosecondsPerDay;
        totalNs += TimeToNanoseconds(isoDateTime.Time);

        return totalNs;
    }

    /// <summary>
    /// GetPossibleEpochNanoseconds ( timeZone, isoDateTime )
    /// https://tc39.es/proposal-temporal/#sec-temporal-getpossibleepochnanoseconds
    /// Determines the possible exact times that may correspond to isoDateTime.
    /// </summary>
    private static BigInteger[] GetPossibleEpochNanoseconds(
        Realm realm,
        ITimeZoneProvider timeZoneProvider,
        string timeZone,
        IsoDateTime isoDateTime)
    {
        // For UTC or fixed offset timezones, there's always exactly one epoch time
        if (string.Equals(timeZone, "UTC", StringComparison.Ordinal) || timeZone.StartsWith('+') || timeZone.StartsWith('-'))
        {
            var epochNs = GetUTCEpochNanoseconds(isoDateTime);

            // For offset timezones, adjust by the offset
            if (timeZone.StartsWith('+') || timeZone.StartsWith('-'))
            {
                // Parse offset (simplified - assumes format like +05:30 or -08:00)
                var offsetMinutes = ParseOffsetToMinutes(timeZone);
                epochNs -= offsetMinutes * 60L * NanosecondsPerSecond;
            }

            // Validate range (spec step 2)
            if (!InstantConstructor.IsValidEpochNanoseconds(epochNs))
            {
                Throw.RangeError(realm, "Epoch nanoseconds out of valid range");
            }

            return new[] { epochNs };
        }

        // Use the timezone provider to get possible instants
        var possibleInstants = timeZoneProvider.GetPossibleInstantsFor(
            timeZone,
            isoDateTime.Date.Year, isoDateTime.Date.Month, isoDateTime.Date.Day,
            isoDateTime.Time.Hour, isoDateTime.Time.Minute, isoDateTime.Time.Second,
            isoDateTime.Time.Millisecond, isoDateTime.Time.Microsecond, isoDateTime.Time.Nanosecond);

        // Validate all epoch values are within range
        foreach (var epochNs in possibleInstants)
        {
            if (!InstantConstructor.IsValidEpochNanoseconds(epochNs))
            {
                Throw.RangeError(realm, "Epoch nanoseconds out of valid range");
            }
        }

        return possibleInstants;
    }

    /// <summary>
    /// DisambiguatePossibleEpochNanoseconds ( possibleEpochNs, timeZone, isoDateTime, disambiguation )
    /// https://tc39.es/proposal-temporal/#sec-temporal-disambiguatepossibleepochnanoseconds
    /// Chooses from a List of possible exact times the one indicated by the disambiguation parameter.
    /// </summary>
    private static BigInteger DisambiguatePossibleEpochNanoseconds(
        Realm realm,
        ITimeZoneProvider timeZoneProvider,
        BigInteger[] possibleEpochNs,
        string timeZone,
        IsoDateTime isoDateTime,
        string disambiguation)
    {
        var n = possibleEpochNs.Length;

        // Step 2: If exactly one possibility, return it
        if (n == 1)
        {
            return possibleEpochNs[0];
        }

        // Step 3: If multiple possibilities (DST fall back - ambiguous time)
        if (n != 0)
        {
            if (string.Equals(disambiguation, "earlier", StringComparison.Ordinal) || string.Equals(disambiguation, "compatible", StringComparison.Ordinal))
            {
                return possibleEpochNs[0];
            }

            if (string.Equals(disambiguation, "later", StringComparison.Ordinal))
            {
                return possibleEpochNs[n - 1];
            }

            // disambiguation == "reject"
            Throw.RangeError(realm, "Ambiguous time with disambiguation=reject");
        }

        // Step 8: No possibilities (DST spring forward - gap)
        if (string.Equals(disambiguation, "reject", StringComparison.Ordinal))
        {
            Throw.RangeError(realm, "Time does not exist in timezone (DST gap) with disambiguation=reject");
        }

        // Steps 9-19: Find the epoch time during the gap
        // For "earlier": use the time before the gap
        // For "compatible" or "later": use the time after the gap

        // Simplified implementation: Try adjusting by 1 hour in each direction
        // A full implementation would use binary search to find exact gap boundaries

        // Try 1 hour earlier
        var oneHourNs = NanosecondsPerHour;
        var earlierEpochGuess = GetUTCEpochNanoseconds(isoDateTime) - oneHourNs;
        var earlierDateTimeGuess = EpochNanosecondsToIsoDateTime(earlierEpochGuess);
        var earlierPossible = GetPossibleEpochNanoseconds(realm, timeZoneProvider, timeZone, earlierDateTimeGuess);

        if (string.Equals(disambiguation, "earlier", StringComparison.Ordinal) && earlierPossible.Length > 0)
        {
            return earlierPossible[0];
        }

        // Try 1 hour later
        var laterEpochGuess = GetUTCEpochNanoseconds(isoDateTime) + oneHourNs;
        var laterDateTimeGuess = EpochNanosecondsToIsoDateTime(laterEpochGuess);
        var laterPossible = GetPossibleEpochNanoseconds(realm, timeZoneProvider, timeZone, laterDateTimeGuess);

        if (laterPossible.Length > 0)
        {
            return laterPossible[laterPossible.Length - 1];
        }

        // Fallback for "earlier" if forward search worked
        if (earlierPossible.Length > 0)
        {
            return earlierPossible[0];
        }

        // Should not reach here - throw error
        Throw.RangeError(realm, "Could not determine epoch time for ambiguous datetime");
        return BigInteger.Zero; // unreachable
    }

    /// <summary>
    /// GetEpochNanosecondsFor ( timeZone, isoDateTime, disambiguation )
    /// https://tc39.es/proposal-temporal/#sec-temporal-getepochnanosecondsfor
    /// Gets the epoch nanoseconds for a timezone-aware ISO date-time.
    /// </summary>
    public static BigInteger GetEpochNanosecondsFor(
        Realm realm,
        ITimeZoneProvider timeZoneProvider,
        string timeZone,
        IsoDateTime isoDateTime,
        string disambiguation)
    {
        var possibleEpochNs = GetPossibleEpochNanoseconds(realm, timeZoneProvider, timeZone, isoDateTime);
        return DisambiguatePossibleEpochNanoseconds(realm, timeZoneProvider, possibleEpochNs, timeZone, isoDateTime, disambiguation);
    }

    /// <summary>
    /// GetStartOfDay ( timeZone, isoDate )
    /// https://tc39.es/proposal-temporal/#sec-temporal-getstartofday
    /// Determines the exact time that corresponds to the first valid wall-clock time on the given date in the given timezone.
    /// </summary>
    public static BigInteger GetStartOfDay(
        Realm realm,
        ITimeZoneProvider provider,
        string timeZone,
        IsoDate isoDate)
    {
        // Step 1: Let isoDateTime be CombineISODateAndTimeRecord(isoDate, MidnightTimeRecord())
        var midnight = new IsoDateTime(isoDate, new IsoTime(0, 0, 0, 0, 0, 0));

        // Step 2: Let possibleEpochNs be GetPossibleEpochNanoseconds(timeZone, isoDateTime)
        var possibleEpochNs = GetPossibleEpochNanoseconds(realm, provider, timeZone, midnight);

        // Step 3: If possibleEpochNs is not empty, return possibleEpochNs[0]
        if (possibleEpochNs.Length > 0)
        {
            return possibleEpochNs[0];
        }

        // Step 5: Midnight is in a DST gap. Find the first valid local time after midnight.
        // The transition instant that creates the gap IS the start of day.
        // Search for the transition near UTC midnight for this date.
        var utcMidnightNs = (BigInteger) IsoDateToDays(isoDate.Year, isoDate.Month, isoDate.Day) * NanosecondsPerDay;

        // Search from 24 hours before UTC midnight to cover all possible timezone offsets (max ±14h)
        var searchFrom = utcMidnightNs - NanosecondsPerDay;
        var transition = provider.GetNextTransition(timeZone, searchFrom);

        // Find the transition whose new local time lands on our target date
        while (transition.HasValue && transition.Value < utcMidnightNs + NanosecondsPerDay)
        {
            // Get the offset AFTER the transition
            var offsetAfter = provider.GetOffsetNanosecondsFor(timeZone, transition.Value);
            var localNsAfter = transition.Value + offsetAfter;

            // Check if the local time after transition is on our target date and >= 00:00
            var localDays = localNsAfter / NanosecondsPerDay;
            var localTimeNs = localNsAfter - localDays * NanosecondsPerDay;
            if (localTimeNs < 0)
            {
                localDays--;
                localTimeNs += NanosecondsPerDay;
            }

            var targetDays = (BigInteger) IsoDateToDays(isoDate.Year, isoDate.Month, isoDate.Day);
            if (localDays == targetDays)
            {
                return transition.Value;
            }

            // Move to next transition
            transition = provider.GetNextTransition(timeZone, transition.Value);
        }

        // Fallback: should not reach here for valid timezones
        Throw.RangeError(realm, "Could not determine start of day");
        return BigInteger.Zero;
    }

    /// <summary>
    /// GetISODateTimeFor ( timeZone, epochNanoseconds )
    /// Converts epoch nanoseconds to an ISO date-time in the given timezone.
    /// Based on logic from JsZonedDateTime.GetIsoDateTime().
    /// </summary>
    public static IsoDateTime GetISODateTimeFor(
        ITimeZoneProvider timeZoneProvider,
        string timeZone,
        BigInteger epochNanoseconds)
    {
        // Get the timezone offset
        var offsetNs = timeZoneProvider.GetOffsetNanosecondsFor(timeZone, epochNanoseconds);
        var localNs = epochNanoseconds + offsetNs;

        // Convert nanoseconds to date/time components
        var days = (long) (localNs / NanosecondsPerDay);
        var remaining = (long) (localNs % NanosecondsPerDay);
        if (remaining < 0)
        {
            days--;
            remaining += NanosecondsPerDay;
        }

        // Convert days to date
        var date = DaysToIsoDate(days);

        // Convert remaining nanoseconds to time
        var hour = (int) (remaining / NanosecondsPerHour);
        remaining %= NanosecondsPerHour;
        var minute = (int) (remaining / NanosecondsPerMinute);
        remaining %= NanosecondsPerMinute;
        var second = (int) (remaining / NanosecondsPerSecond);
        remaining %= NanosecondsPerSecond;
        var millisecond = (int) (remaining / NanosecondsPerMillisecond);
        remaining %= NanosecondsPerMillisecond;
        var microsecond = (int) (remaining / NanosecondsPerMicrosecond);
        var nanosecond = (int) (remaining % NanosecondsPerMicrosecond);

        return new IsoDateTime(date, new IsoTime(hour, minute, second, millisecond, microsecond, nanosecond));
    }

    /// <summary>
    /// DifferenceInstant ( ns1, ns2, roundingIncrement, smallestUnit, roundingMode )
    /// https://tc39.es/proposal-temporal/#sec-temporal-differenceinstant
    /// Computes the time difference between two instants with rounding.
    /// </summary>
    public static DurationRecord DifferenceInstant(
        BigInteger ns1,
        BigInteger ns2,
        int roundingIncrement,
        string smallestUnit,
        string roundingMode,
        string largestUnit = "hour")
    {
        // Step 1: Compute the nanosecond difference
        var timeDuration = ns2 - ns1;

        // Step 2: If no rounding needed
        if (string.Equals(smallestUnit, "nanosecond", StringComparison.Ordinal) && roundingIncrement == 1)
        {
            return TemporalDurationFromInternal(null!, 0, 0, 0, 0, timeDuration, largestUnit);
        }

        // Step 3: Get nanoseconds per unit
        long nsPerUnit = smallestUnit switch
        {
            "hour" => NanosecondsPerHour,
            "minute" => NanosecondsPerMinute,
            "second" => NanosecondsPerSecond,
            "millisecond" => NanosecondsPerMillisecond,
            "microsecond" => NanosecondsPerMicrosecond,
            _ => 1 // nanosecond
        };

        // Step 4: Round the time duration (use BigInteger to avoid long overflow)
        var incrementNs = (BigInteger) nsPerUnit * roundingIncrement;
        var roundedTime = RoundBigIntegerToIncrement(timeDuration, incrementNs, roundingMode);

        // Step 5: Return as duration
        return TemporalDurationFromInternal(null!, 0, 0, 0, 0, roundedTime, largestUnit);
    }

    /// <summary>
    /// DifferenceZonedDateTime ( ns1, ns2, timeZone, calendar, largestUnit )
    /// Computes the unrounded difference between two zoned datetimes.
    /// Based on spec but simplified for initial implementation.
    /// </summary>
    private static DurationRecord DifferenceZonedDateTime(
        Realm realm,
        ITimeZoneProvider timeZoneProvider,
        BigInteger ns1,
        BigInteger ns2,
        string timeZone,
        string calendar,
        string largestUnit)
    {
        // Step 1: If ns1 = ns2, return zero
        if (ns1 == ns2)
        {
            return new DurationRecord(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        // Step 2-3: Convert both epoch times to ISO datetimes in the timezone
        var startDateTime = GetISODateTimeFor(timeZoneProvider, timeZone, ns1);
        var endDateTime = GetISODateTimeFor(timeZoneProvider, timeZone, ns2);

        // Step 4: Get initial difference - only use the date portion
        var diff = DifferenceISODateTime(startDateTime, endDateTime, calendar, largestUnit);
        var dateDifference = new DurationRecord(diff.Years, diff.Months, diff.Weeks, diff.Days, 0, 0, 0, 0, 0, 0);

        // Step 5: Create intermediate datetime by adding date portion to start
        var intermediateDate = CalendarDateAdd(realm, calendar, startDateTime.Date, dateDifference, "constrain");
        var intermediateDateTime = new IsoDateTime(intermediateDate, startDateTime.Time);

        // Step 6: Get epoch nanoseconds for intermediate
        var intermediateNs = GetEpochNanosecondsFor(realm, timeZoneProvider, timeZone, intermediateDateTime, "compatible");

        // Step 7: Compute time difference from actual epoch nanoseconds
        var timeDifference = TimeDurationFromEpochNanosecondsDifference(ns2, intermediateNs);

        // Step 8-9: Get signs
        var timeSign = TimeDurationSign(timeDifference);
        var dateSign = DateDurationSign(dateDifference);

        // Step 10: If date and time signs disagree, adjust
        if (dateSign != 0 && timeSign != 0 && dateSign == -timeSign)
        {
            // Step 10a: Adjust date duration
            dateDifference = AdjustDateDurationRecord(dateDifference, -timeSign);

            // Step 10b: Recompute intermediate
            intermediateDate = CalendarDateAdd(realm, calendar, startDateTime.Date, dateDifference, "constrain");
            intermediateDateTime = new IsoDateTime(intermediateDate, startDateTime.Time);

            // Step 10c: Recompute intermediate epoch ns
            intermediateNs = GetEpochNanosecondsFor(realm, timeZoneProvider, timeZone, intermediateDateTime, "compatible");

            // Step 10d: Recompute time difference
            timeDifference = TimeDurationFromEpochNanosecondsDifference(ns2, intermediateNs);
        }

        // Step 11: If largestUnit is a calendar unit or "day", combine date+time
        if (IsCalendarUnit(largestUnit) || string.Equals(largestUnit, "day", StringComparison.Ordinal))
        {
            return CombineDateAndTimeDuration(dateDifference, timeDifference, largestUnit);
        }

        // Step 12: Otherwise, largestUnit is a time unit — balance time only
        return TemporalDurationFromInternal(realm, 0, 0, 0, 0, timeDifference, largestUnit);
    }

    /// <summary>
    /// DateDurationSign ( dateDuration )
    /// Returns the sign of the first non-zero component, or 0 if all are zero.
    /// </summary>
    private static int DateDurationSign(DurationRecord d)
    {
        if (d.Years < 0) return -1;
        if (d.Years > 0) return 1;
        if (d.Months < 0) return -1;
        if (d.Months > 0) return 1;
        if (d.Weeks < 0) return -1;
        if (d.Weeks > 0) return 1;
        if (d.Days < 0) return -1;
        if (d.Days > 0) return 1;
        return 0;
    }

    /// <summary>
    /// AdjustDateDurationRecord ( dateDuration, increment )
    /// Adjusts the largest non-zero unit by increment, zeroing smaller units.
    /// </summary>
    private static DurationRecord AdjustDateDurationRecord(DurationRecord d, int increment)
    {
        if (d.Weeks != 0)
            return new DurationRecord(d.Years, d.Months, d.Weeks + increment, 0, 0, 0, 0, 0, 0, 0);
        if (d.Months != 0)
            return new DurationRecord(d.Years, d.Months + increment, 0, 0, 0, 0, 0, 0, 0, 0);
        if (d.Years != 0)
            return new DurationRecord(d.Years + increment, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        return new DurationRecord(0, 0, 0, d.Days + increment, 0, 0, 0, 0, 0, 0);
    }

    /// <summary>
    /// DifferenceZonedDateTimeWithRounding ( ns1, ns2, timeZone, calendar, largestUnit, roundingIncrement, smallestUnit, roundingMode )
    /// https://tc39.es/proposal-temporal/#sec-temporal-differencezoneddatetimewithrounding
    /// Computes the rounded difference between two zoned datetimes.
    /// </summary>
    public static DurationRecord DifferenceZonedDateTimeWithRounding(
        Realm realm,
        ITimeZoneProvider timeZoneProvider,
        BigInteger ns1,
        BigInteger ns2,
        string timeZone,
        string calendar,
        string largestUnit,
        int roundingIncrement,
        string smallestUnit,
        string roundingMode)
    {
        // Step 1: If largestUnit is a time unit (hour/minute/second/ms/μs/ns, NOT day), use DifferenceInstant
        // Note: "day" is not included here because days can vary in length due to DST
        if (IsTimeUnit(largestUnit))
        {
            return DifferenceInstant(ns1, ns2, roundingIncrement, smallestUnit, roundingMode, largestUnit);
        }

        // Step 2: Get unrounded difference (for calendar units: year/month/week/day)
        var difference = DifferenceZonedDateTime(realm, timeZoneProvider, ns1, ns2, timeZone, calendar, largestUnit);

        // Step 3: If no rounding needed, return
        if (string.Equals(smallestUnit, "nanosecond", StringComparison.Ordinal) && roundingIncrement == 1)
        {
            return difference;
        }

        // Step 4: Get the ISO datetime for ns1
        var dateTime = GetISODateTimeFor(timeZoneProvider, timeZone, ns1);

        // Step 5: Round the difference
        return RoundRelativeDuration(realm, timeZoneProvider, difference, ns1, ns2, dateTime, timeZone, calendar, largestUnit, roundingIncrement, smallestUnit, roundingMode);
    }

    /// <summary>
    /// TotalRelativeDuration ( duration, originEpochNs, destEpochNs, isoDateTime, timeZone, calendar, unit )
    /// https://tc39.es/proposal-temporal/#sec-temporal-totalrelativeduration (duration.html:1894-1916)
    /// Returns the total number of units in duration, relative to isoDateTime.
    /// </summary>
    public static double TotalRelativeDuration(
        Realm realm,
        DurationRecord duration,
        BigInteger originEpochNs,
        BigInteger destEpochNs,
        IsoDateTime isoDateTime,
        string? timeZone,
        ITimeZoneProvider? timeZoneProvider,
        string calendar,
        string unit)
    {
        // Step 1: If calendar unit or (timeZone is set and unit is day)
        if (IsCalendarUnit(unit) || (timeZone != null && string.Equals(unit, "day", StringComparison.Ordinal)))
        {
            // Step 1a: Get sign
            var sign = InternalDurationSign(duration) < 0 ? -1 : 1;

            // Step 1b: Call NudgeToCalendarUnit with increment=1 and roundingMode=trunc
            var record = NudgeToCalendarUnit(realm, sign, duration, originEpochNs, destEpochNs, isoDateTime, timeZone, timeZoneProvider, calendar, 1, unit, "trunc");

            // Step 1c: Return the total
            return record.Total;
        }

        // Step 2: Convert to internal duration with 24-hour days
        var timeDuration = TimeDurationFromComponents(duration);
        timeDuration = Add24HourDaysToTimeDuration(timeDuration, duration.Days);

        // Step 3: Return total time duration
        return TotalTimeDuration(timeDuration, unit);
    }

    /// <summary>
    /// DifferenceZonedDateTimeWithTotal ( ns1, ns2, timeZone, calendar, unit )
    /// https://tc39.es/proposal-temporal/#sec-temporal-differencezoneddatetimewithtotal (zoneddatetime.html:1183-1205)
    /// Computes the total difference between two zoned datetimes in the given unit.
    /// </summary>
    public static double DifferenceZonedDateTimeWithTotal(
        Realm realm,
        ITimeZoneProvider timeZoneProvider,
        BigInteger ns1,
        BigInteger ns2,
        string timeZone,
        string calendar,
        string unit)
    {
        // Step 1: If TemporalUnitCategory(unit) is time, use simple time duration calculation
        // Note: "day" has category ~date~, not ~time~, so it goes to the calendar path
        if (!IsCalendarUnit(unit) && !string.Equals(unit, "day", StringComparison.Ordinal))
        {
            // Step 1a: TimeDurationFromEpochNanosecondsDifference
            var timeDifference = ns2 - ns1;

            // Step 1b: Return total time duration
            return TotalTimeDuration(timeDifference, unit);
        }

        // Step 2: Get unrounded difference
        var difference = DifferenceZonedDateTime(realm, timeZoneProvider, ns1, ns2, timeZone, calendar, unit);

        // Step 3: Get ISO datetime for ns1
        var dateTime = GetISODateTimeFor(timeZoneProvider, timeZone, ns1);

        // Step 4: Call TotalRelativeDuration
        return TotalRelativeDuration(realm, difference, ns1, ns2, dateTime, timeZone, timeZoneProvider, calendar, unit);
    }

    /// <summary>
    /// AddInstant ( epochNanoseconds, timeDuration )
    /// https://tc39.es/proposal-temporal/#sec-temporal-addinstant (instant.html:446-460)
    /// Adds a time duration to epoch nanoseconds with range checking.
    /// </summary>
    public static BigInteger AddInstant(Realm realm, BigInteger epochNanoseconds, BigInteger timeDuration)
    {
        // Step 1: AddTimeDurationToEpochNanoseconds
        var result = AddTimeDurationToEpochNanoseconds(timeDuration, epochNanoseconds);

        // Step 2: Validate range
        if (!InstantConstructor.IsValidEpochNanoseconds(result))
        {
            Throw.RangeError(realm, "Result is outside valid Temporal range");
        }

        // Step 3: Return result
        return result;
    }

    /// <summary>
    /// AddZonedDateTime ( epochNanoseconds, timeZone, calendar, duration, overflow )
    /// https://tc39.es/proposal-temporal/#sec-temporal-addzoneddatetime (zoneddatetime.html:1084-1109)
    /// Adds a duration to zoned datetime epoch nanoseconds, accounting for timezone and calendar.
    /// </summary>
    public static BigInteger AddZonedDateTime(
        Realm realm,
        ITimeZoneProvider timeZoneProvider,
        BigInteger epochNanoseconds,
        string timeZone,
        string calendar,
        DurationRecord duration,
        string overflow)
    {
        // Extract time duration from duration components
        var timeDuration = TimeDurationFromComponents(duration);

        // Step 1: If no date portion (years, months, weeks, days all zero), just add time duration
        var hasDatePortion = duration.Years != 0 || duration.Months != 0 || duration.Weeks != 0 || duration.Days != 0;
        if (!hasDatePortion)
        {
            return AddInstant(realm, epochNanoseconds, timeDuration);
        }

        // Step 2: Get ISO datetime for starting epoch
        var isoDateTime = GetISODateTimeFor(timeZoneProvider, timeZone, epochNanoseconds);

        // Step 3: Add date portion only using CalendarDateAdd (time components are handled in step 7)
        var dateDuration = new DurationRecord(duration.Years, duration.Months, duration.Weeks, duration.Days, 0, 0, 0, 0, 0, 0);
        var addedDate = CalendarDateAdd(realm, calendar, isoDateTime.Date, dateDuration, overflow);

        // Step 4: Combine with original time
        var intermediateDateTime = new IsoDateTime(addedDate, isoDateTime.Time);

        // Step 6: Get epoch nanoseconds for intermediate datetime
        var intermediateNs = GetEpochNanosecondsFor(realm, timeZoneProvider, timeZone, intermediateDateTime, "compatible");

        // Step 7: Add time portion to intermediate nanoseconds
        return AddInstant(realm, intermediateNs, timeDuration);
    }

    /// <summary>
    /// DifferencePlainDateTimeWithTotal ( isoDateTime1, isoDateTime2, calendar, unit )
    /// https://tc39.es/proposal-temporal/#sec-temporal-differenceplaindatetimewithtotal (plaindatetime.html:1016-1037)
    /// Computes the total difference between two plain datetimes in the given unit.
    /// </summary>
    public static double DifferencePlainDateTimeWithTotal(
        Realm realm,
        IsoDateTime isoDateTime1,
        IsoDateTime isoDateTime2,
        string calendar,
        string unit)
    {
        // Step 1: If same datetime, return 0
        if (CompareIsoDateTimes(isoDateTime1, isoDateTime2) == 0)
        {
            return 0;
        }

        // Step 2: Check limits (ISODateTimeWithinLimits)
        if (!ISODateTimeWithinLimits(isoDateTime1) || !ISODateTimeWithinLimits(isoDateTime2))
        {
            Throw.RangeError(realm, "DateTime is outside valid range");
        }

        // Step 3: DifferenceISODateTime to get the diff
        var diff = DifferenceISODateTime(isoDateTime1, isoDateTime2, calendar, unit);

        // Step 4: If unit is nanosecond, return diff time duration directly
        if (string.Equals(unit, "nanosecond", StringComparison.Ordinal))
        {
            var timeDuration = TimeDurationFromComponents(diff);
            return (double) timeDuration;
        }

        // Step 5: Get origin and dest epoch nanoseconds (UTC for PlainDateTime)
        var originEpochNs = GetUTCEpochNanoseconds(isoDateTime1);
        var destEpochNs = GetUTCEpochNanoseconds(isoDateTime2);

        // Step 6: Call TotalRelativeDuration with timeZone=null (unset)
        return TotalRelativeDuration(realm, diff, originEpochNs, destEpochNs, isoDateTime1, null, null, calendar, unit);
    }

    /// <summary>
    /// ParseOffsetToMinutes ( offsetString )
    /// Parses an offset string like "+05:30" or "-08:00" to total minutes.
    /// </summary>
    private static long ParseOffsetToMinutes(string offset)
    {
        var sign = offset[0] == '-' ? -1 : 1;
        var parts = offset.Substring(1).Split(':');
        var hours = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var minutes = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
        return sign * (hours * 60 + minutes);
    }


    /// <summary>
    /// GetUnsignedRoundingMode ( roundingMode, sign )
    /// https://tc39.es/proposal-temporal/#sec-getunsignedroundingmode
    /// Returns the unsigned rounding mode for a given rounding mode and sign.
    /// </summary>
    private static string GetUnsignedRoundingMode(string roundingMode, bool isNegative)
    {
        if (string.Equals(roundingMode, "ceil", StringComparison.Ordinal))
            return isNegative ? "zero" : "infinity";
        if (string.Equals(roundingMode, "floor", StringComparison.Ordinal))
            return isNegative ? "infinity" : "zero";
        if (string.Equals(roundingMode, "expand", StringComparison.Ordinal))
            return "infinity";
        if (string.Equals(roundingMode, "trunc", StringComparison.Ordinal))
            return "zero";
        if (string.Equals(roundingMode, "halfCeil", StringComparison.Ordinal))
            return isNegative ? "half-zero" : "half-infinity";
        if (string.Equals(roundingMode, "halfFloor", StringComparison.Ordinal))
            return isNegative ? "half-infinity" : "half-zero";
        if (string.Equals(roundingMode, "halfExpand", StringComparison.Ordinal))
            return "half-infinity";
        if (string.Equals(roundingMode, "halfTrunc", StringComparison.Ordinal))
            return "half-zero";
        if (string.Equals(roundingMode, "halfEven", StringComparison.Ordinal))
            return "half-even";

        return "half-infinity"; // default
    }

    /// <summary>
    /// ApplyUnsignedRoundingMode ( x, r1, r2, unsignedRoundingMode )
    /// https://tc39.es/proposal-temporal/#sec-applyunsignedroundingmode
    /// Applies unsigned rounding mode to choose between r1 and r2.
    /// </summary>
    private static double ApplyUnsignedRoundingMode(double x, double r1, double r2, string unsignedRoundingMode)
    {
        // Step 1: If x = r1, return r1
        if (x == r1)
            return r1;

        // Step 2: Assert r1 < x < r2 (should be true by construction)

        // Steps 4-5: Simple cases
        if (string.Equals(unsignedRoundingMode, "zero", StringComparison.Ordinal))
            return r1;
        if (string.Equals(unsignedRoundingMode, "infinity", StringComparison.Ordinal))
            return r2;

        // Steps 6-7: Calculate distances
        var d1 = x - r1;
        var d2 = r2 - x;

        // Steps 8-9: Choose closer value
        if (d1 < d2)
            return r1;
        if (d2 < d1)
            return r2;

        // Step 10: Exactly halfway (d1 = d2)

        // Steps 11-12: Half-rounding modes
        if (string.Equals(unsignedRoundingMode, "half-zero", StringComparison.Ordinal))
            return r1;
        if (string.Equals(unsignedRoundingMode, "half-infinity", StringComparison.Ordinal))
            return r2;

        // Steps 13-15: Half-even (banker's rounding)
        // Check if r1 is even
        var cardinality = (long) (r1 / (r2 - r1)) % 2;
        return cardinality == 0 ? r1 : r2;
    }

    /// <summary>
    /// Record for ComputeNudgeWindow result.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct NudgeWindowResult(
        double R1,
        double R2,
        BigInteger StartEpochNs,
        BigInteger EndEpochNs,
        DurationRecord StartDuration,
        DurationRecord EndDuration);

    /// <summary>
    /// ComputeNudgeWindow ( sign, duration, originEpochNs, isoDateTime, timeZone, calendar, increment, unit, additionalShift )
    /// https://tc39.es/proposal-temporal/#sec-temporal-computenudgewindow
    /// duration.html:1569-1641
    /// </summary>
    private static NudgeWindowResult ComputeNudgeWindow(
        Realm realm,
        int sign,
        DurationRecord duration,
        BigInteger originEpochNs,
        IsoDateTime isoDateTime,
        string? timeZone,
        ITimeZoneProvider? timeZoneProvider,
        string calendar,
        int increment,
        string unit,
        bool additionalShift)
    {
        double r1, r2;
        DurationRecord startDuration, endDuration;

        if (string.Equals(unit, "year", StringComparison.Ordinal))
        {
            var years = RoundNumberToIncrement(duration.Years, increment, "trunc");
            r1 = additionalShift ? years + increment * sign : years;
            r2 = r1 + increment * sign;
            startDuration = new DurationRecord(r1, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            endDuration = new DurationRecord(r2, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }
        else if (string.Equals(unit, "month", StringComparison.Ordinal))
        {
            var months = RoundNumberToIncrement(duration.Months, increment, "trunc");
            r1 = additionalShift ? months + increment * sign : months;
            r2 = r1 + increment * sign;
            // Keep years, adjust months
            startDuration = new DurationRecord(duration.Years, r1, 0, 0, 0, 0, 0, 0, 0, 0);
            endDuration = new DurationRecord(duration.Years, r2, 0, 0, 0, 0, 0, 0, 0, 0);
        }
        else if (string.Equals(unit, "week", StringComparison.Ordinal))
        {
            // Spec lines 1607-1615: Use CalendarDateUntil to compute weeks
            // Step 1: Get yearsMonths (duration without weeks and days)
            var yearsMonths = new DurationRecord(duration.Years, duration.Months, 0, 0, 0, 0, 0, 0, 0, 0);

            // Step 2: Add yearsMonths to start date
            var weeksStart = CalendarDateAdd(realm, calendar, isoDateTime.Date, yearsMonths, "constrain");

            // Step 3: Add days to get end of the period
            var weeksEnd = AddDaysToISODate(weeksStart, (int) duration.Days);

            // Step 4: Calculate weeks between weeksStart and weeksEnd
            var untilResult = CalendarDateUntil(calendar, weeksStart, weeksEnd, "week");

            // Step 5: Round total weeks (original weeks + calculated weeks from days)
            var weeks = RoundNumberToIncrement(duration.Weeks + untilResult.Weeks, increment, "trunc");
            r1 = weeks;
            r2 = weeks + increment * sign;

            // Step 6: Create durations with adjusted weeks (AdjustDateDurationRecord pattern)
            startDuration = new DurationRecord(duration.Years, duration.Months, r1, 0, 0, 0, 0, 0, 0, 0);
            endDuration = new DurationRecord(duration.Years, duration.Months, r2, 0, 0, 0, 0, 0, 0, 0);
        }
        else // day
        {
            var days = RoundNumberToIncrement(duration.Days, increment, "trunc");
            r1 = days;
            r2 = days + increment * sign;
            // Keep years, months, weeks, adjust days
            startDuration = new DurationRecord(duration.Years, duration.Months, duration.Weeks, r1, 0, 0, 0, 0, 0, 0);
            endDuration = new DurationRecord(duration.Years, duration.Months, duration.Weeks, r2, 0, 0, 0, 0, 0, 0);
        }

        // Calculate epoch nanoseconds for start and end (spec lines 1625-1640)
        BigInteger startEpochNs, endEpochNs;

        // Only skip CalendarDateAdd if the entire startDuration is zero
        if (startDuration.Years == 0 && startDuration.Months == 0 && startDuration.Weeks == 0 && startDuration.Days == 0)
        {
            startEpochNs = originEpochNs;
        }
        else
        {
            var start = CalendarDateAdd(realm, calendar, isoDateTime.Date, startDuration, "constrain");
            var startDateTime = new IsoDateTime(start, isoDateTime.Time);

            if (timeZone != null)
            {
                startEpochNs = GetEpochNanosecondsFor(realm, (timeZoneProvider ?? throw new InvalidOperationException()), timeZone, startDateTime, "compatible");
            }
            else
            {
                startEpochNs = GetUTCEpochNanoseconds(startDateTime);
            }
        }

        var end = CalendarDateAdd(realm, calendar, isoDateTime.Date, endDuration, "constrain");
        var endDateTime = new IsoDateTime(end, isoDateTime.Time);

        if (timeZone != null)
        {
            endEpochNs = GetEpochNanosecondsFor(realm, (timeZoneProvider ?? throw new InvalidOperationException()), timeZone, endDateTime, "compatible");
        }
        else
        {
            endEpochNs = GetUTCEpochNanoseconds(endDateTime);
        }

        return new NudgeWindowResult(r1, r2, startEpochNs, endEpochNs, startDuration, endDuration);
    }

    /// <summary>
    /// Record for NudgeToCalendarUnit result.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct CalendarNudgeResult(
        DurationNudgeResult NudgeResult,
        double Total);

    /// <summary>
    /// NudgeToCalendarUnit ( sign, duration, originEpochNs, destEpochNs, isoDateTime, timeZone, calendar, increment, unit, roundingMode )
    /// https://tc39.es/proposal-temporal/#sec-temporal-nudgetocalendarunit
    /// duration.html:1644-1717
    /// </summary>
    private static CalendarNudgeResult NudgeToCalendarUnit(
        Realm realm,
        int sign,
        DurationRecord duration,
        BigInteger originEpochNs,
        BigInteger destEpochNs,
        IsoDateTime isoDateTime,
        string? timeZone,
        ITimeZoneProvider? timeZoneProvider,
        string calendar,
        int increment,
        string unit,
        string roundingMode)
    {
        bool didExpandCalendarUnit = false;

        // Step 2: Compute nudge window (without additional shift) - spec line 1667
        var nudgeWindow = ComputeNudgeWindow(realm, sign, duration, originEpochNs, isoDateTime, timeZone, timeZoneProvider, calendar, increment, unit, false);

        var startEpochNs = nudgeWindow.StartEpochNs;
        var endEpochNs = nudgeWindow.EndEpochNs;

        // Steps 5-9: Check if destEpochNs is within the window, if not, compute with additional shift
        if (sign == 1)
        {
            if (!(startEpochNs <= destEpochNs && destEpochNs <= endEpochNs))
            {
                // Spec line 1672
                nudgeWindow = ComputeNudgeWindow(realm, sign, duration, originEpochNs, isoDateTime, timeZone, timeZoneProvider, calendar, increment, unit, true);
                startEpochNs = nudgeWindow.StartEpochNs;
                endEpochNs = nudgeWindow.EndEpochNs;
                didExpandCalendarUnit = true;
            }
        }
        else
        {
            if (!(endEpochNs <= destEpochNs && destEpochNs <= startEpochNs))
            {
                // Spec line 1677
                nudgeWindow = ComputeNudgeWindow(realm, sign, duration, originEpochNs, isoDateTime, timeZone, timeZoneProvider, calendar, increment, unit, true);
                startEpochNs = nudgeWindow.StartEpochNs;
                endEpochNs = nudgeWindow.EndEpochNs;
                didExpandCalendarUnit = true;
            }
        }

        var r1 = nudgeWindow.R1;
        var r2 = nudgeWindow.R2;
        var startDuration = nudgeWindow.StartDuration;
        var endDuration = nudgeWindow.EndDuration;

        // Step 17-18: Calculate progress and total using exact BigInteger math
        // Spec: progress = (destEpochNs - startEpochNs) / (endEpochNs - startEpochNs) (exact math)
        // total = r1 + progress × increment × sign (exact math, then 𝔽())
        var progressNumerator = destEpochNs - startEpochNs;
        var progressDenominator = endEpochNs - startEpochNs;
        // total = (r1 * progressDenominator + progressNumerator * increment * sign) / progressDenominator
        var r1BigInt = new BigInteger(r1);
        var totalNumerator = r1BigInt * progressDenominator + progressNumerator * increment * sign;
        var total = DivideBigIntToF64(totalNumerator, progressDenominator);

        // Step 20-21: Determine unsigned rounding mode
        var isNegative = sign < 0;
        var unsignedRoundingMode = GetUnsignedRoundingMode(roundingMode, isNegative);

        // Step 22-25: Apply rounding
        double roundedUnit;
        if (destEpochNs == endEpochNs)
        {
            roundedUnit = System.Math.Abs(r2);
        }
        else
        {
            // Apply unsigned rounding mode to absolute values
            roundedUnit = ApplyUnsignedRoundingMode(System.Math.Abs(total), System.Math.Abs(r1), System.Math.Abs(r2), unsignedRoundingMode);
        }

        // Step 26-31: Select result duration based on rounding
        DurationRecord resultDuration;
        BigInteger nudgedEpochNs;

        if (roundedUnit == System.Math.Abs(r2))
        {
            didExpandCalendarUnit = true;
            resultDuration = endDuration;
            nudgedEpochNs = endEpochNs;
        }
        else
        {
            resultDuration = startDuration;
            nudgedEpochNs = startEpochNs;
        }

        // Step 32: Combine with zero time duration
        resultDuration = CombineDateAndTimeDuration(resultDuration, 0);

        var nudgeResult = new DurationNudgeResult(resultDuration, nudgedEpochNs, didExpandCalendarUnit);
        return new CalendarNudgeResult(nudgeResult, total);
    }

    /// <summary>
    /// NudgeToZonedTime ( sign, duration, isoDateTime, timeZone, calendar, increment, unit, roundingMode )
    /// https://tc39.es/proposal-temporal/#sec-temporal-nudgetozonedtime
    /// It implements rounding a duration to an increment of a time unit, relative to a ZonedDateTime starting point.
    /// </summary>
    private static DurationNudgeResult NudgeToZonedTime(
        Realm realm,
        ITimeZoneProvider timeZoneProvider,
        int sign,
        DurationRecord duration,
        IsoDateTime isoDateTime,
        string timeZone,
        string calendar,
        long increment,
        string unit,
        string roundingMode)
    {
        // Step 1: Let start be ? CalendarDateAdd(calendar, isoDateTime.[[ISODate]], duration.[[Date]], constrain)
        var datePart = new DurationRecord(duration.Years, duration.Months, duration.Weeks, duration.Days, 0, 0, 0, 0, 0, 0);
        var start = CalendarDateAdd(realm, calendar, isoDateTime.Date, datePart, "constrain");

        // Step 2: Let startDateTime be CombineISODateAndTimeRecord(start, isoDateTime.[[Time]])
        var startDateTime = new IsoDateTime(start, isoDateTime.Time);

        // Step 3: Let endDate be AddDaysToISODate(start, sign)
        var endDate = AddDaysToISODate(start, sign);

        // Step 4: Let endDateTime be CombineISODateAndTimeRecord(endDate, isoDateTime.[[Time]])
        var endDateTime = new IsoDateTime(endDate, isoDateTime.Time);

        // Step 5: Let startEpochNs be ? GetEpochNanosecondsFor(timeZone, startDateTime, compatible)
        var startEpochNs = GetEpochNanosecondsFor(realm, timeZoneProvider, timeZone, startDateTime, "compatible");

        // Step 6: Let endEpochNs be ? GetEpochNanosecondsFor(timeZone, endDateTime, compatible)
        var endEpochNs = GetEpochNanosecondsFor(realm, timeZoneProvider, timeZone, endDateTime, "compatible");

        // Step 7: Let daySpan be TimeDurationFromEpochNanosecondsDifference(endEpochNs, startEpochNs)
        var daySpan = TimeDurationFromEpochNanosecondsDifference(endEpochNs, startEpochNs);

        // Step 8: Assert: TimeDurationSign(daySpan) = sign
        System.Diagnostics.Debug.Assert(TimeDurationSign(daySpan) == sign);

        // Step 9: Let unitLength be the value in nanoseconds for the unit
        long unitLength = unit switch
        {
            "hour" => NanosecondsPerHour,
            "minute" => NanosecondsPerMinute,
            "second" => NanosecondsPerSecond,
            "millisecond" => NanosecondsPerMillisecond,
            "microsecond" => NanosecondsPerMicrosecond,
            "nanosecond" => 1L,
            _ => throw new InvalidOperationException($"Invalid time unit: {unit}")
        };

        // Step 10: Let roundedTimeDuration be ? RoundTimeDurationToIncrement(duration.[[Time]], increment × unitLength, roundingMode)
        var durationTime = new IsoTime((int) duration.Hours, (int) duration.Minutes, (int) duration.Seconds, (int) duration.Milliseconds, (int) duration.Microseconds, (int) duration.Nanoseconds);
        var durationTimeNs = new BigInteger(TimeToNanoseconds(durationTime));
        var roundedTimeDuration = RoundTimeDurationToIncrement(realm, durationTimeNs, (BigInteger) increment * unitLength, roundingMode);

        // Step 11: Let beyondDaySpan be ! AddTimeDuration(roundedTimeDuration, -daySpan)
        var beyondDaySpan = roundedTimeDuration - daySpan;

        bool didRoundBeyondDay;
        int dayDelta;
        BigInteger nudgedEpochNs;

        // Step 12: If TimeDurationSign(beyondDaySpan) ≠ -sign
        if (TimeDurationSign(beyondDaySpan) != -sign)
        {
            // Step 12a: Let didRoundBeyondDay be true
            didRoundBeyondDay = true;

            // Step 12b: Let dayDelta be sign
            dayDelta = sign;

            // Step 12c: Set roundedTimeDuration to ? RoundTimeDurationToIncrement(beyondDaySpan, increment × unitLength, roundingMode)
            roundedTimeDuration = RoundTimeDurationToIncrement(realm, beyondDaySpan, (BigInteger) increment * unitLength, roundingMode);

            // Step 12d: Let nudgedEpochNs be AddTimeDurationToEpochNanoseconds(roundedTimeDuration, endEpochNs)
            nudgedEpochNs = AddTimeDurationToEpochNanoseconds(roundedTimeDuration, endEpochNs);
        }
        else
        {
            // Step 13a: Let didRoundBeyondDay be false
            didRoundBeyondDay = false;

            // Step 13b: Let dayDelta be 0
            dayDelta = 0;

            // Step 13c: Let nudgedEpochNs be AddTimeDurationToEpochNanoseconds(roundedTimeDuration, startEpochNs)
            nudgedEpochNs = AddTimeDurationToEpochNanoseconds(roundedTimeDuration, startEpochNs);
        }

        // Step 14: Let dateDuration be ! AdjustDateDurationRecord(duration.[[Date]], duration.[[Date]].[[Days]] + dayDelta)
        var adjustedDateDuration = new DurationRecord(
            duration.Years,
            duration.Months,
            duration.Weeks,
            duration.Days + dayDelta,
            0, 0, 0, 0, 0, 0
        );

        // Step 15: Let resultDuration be CombineDateAndTimeDuration(dateDuration, roundedTimeDuration)
        var resultDuration = CombineDateAndTimeDuration(adjustedDateDuration, roundedTimeDuration);

        // Step 16: Return Duration Nudge Result Record
        return new DurationNudgeResult(resultDuration, nudgedEpochNs, didRoundBeyondDay);
    }

    /// <summary>
    /// BubbleRelativeDuration ( sign, duration, nudgedEpochNs, isoDateTime, timeZone, calendar, largestUnit, smallestUnit )
    /// https://tc39.es/proposal-temporal/#sec-temporal-bubblerelativeduration
    /// Simplified version for PlainDate (no timezone).
    /// </summary>
    private static DurationRecord BubbleRelativeDuration(
        Realm realm,
        int sign,
        DurationRecord duration,
        BigInteger nudgedEpochNs,
        IsoDateTime isoDateTime,
        string calendar,
        string largestUnit,
        string smallestUnit)
    {
        // Step 1: If smallestUnit == largestUnit, return unchanged
        if (string.Equals(smallestUnit, largestUnit, StringComparison.Ordinal))
            return duration;

        // Step 2-3: Get unit indices
        var largestUnitIndex = TemporalUnitIndex(largestUnit);
        var smallestUnitIndex = TemporalUnitIndex(smallestUnit);

        // Step 4: Start from unit below smallestUnit
        var unitIndex = smallestUnitIndex - 1;
        var done = false;

        // Step 5: Loop upward through units
        while (unitIndex >= largestUnitIndex && !done)
        {
            var unit = GetUnitNameByIndex(unitIndex);

            // Step 5.1: Skip week unless largestUnit is week
            if (!string.Equals(unit, "week", StringComparison.Ordinal) || string.Equals(largestUnit, "week", StringComparison.Ordinal))
            {
                DurationRecord endDuration;

                // Step 5.1.1-3: Create duration with incremented unit
                if (string.Equals(unit, "year", StringComparison.Ordinal))
                {
                    var years = duration.Years + sign;
                    endDuration = new DurationRecord(years, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                }
                else if (string.Equals(unit, "month", StringComparison.Ordinal))
                {
                    var months = duration.Months + sign;
                    endDuration = new DurationRecord(duration.Years, months, 0, 0, 0, 0, 0, 0, 0, 0);
                }
                else // week
                {
                    var weeks = duration.Weeks + sign;
                    endDuration = new DurationRecord(duration.Years, duration.Months, weeks, 0, 0, 0, 0, 0, 0, 0);
                }

                // Step 5.1.4-7: Calculate epoch for this duration
                var end = CalendarDateAdd(realm, calendar, isoDateTime.Date, endDuration, "constrain");
                var endDateTime = new IsoDateTime(end, isoDateTime.Time);
                var endEpochNs = GetUTCEpochNanoseconds(endDateTime);

                // Step 5.1.8: Check if we've gone beyond nudgedEpochNs
                var beyondEnd = nudgedEpochNs - endEpochNs;
                int beyondEndSign;
                if (beyondEnd < 0)
                    beyondEndSign = -1;
                else if (beyondEnd > 0)
                    beyondEndSign = 1;
                else
                    beyondEndSign = 0;

                // Step 5.1.9: If not beyond, use this duration
                if (beyondEndSign != -sign)
                {
                    duration = CombineDateAndTimeDuration(endDuration, 0);
                }
                else
                {
                    done = true;
                }
            }

            unitIndex--;
        }

        return duration;
    }

    /// <summary>
    /// Helper to get unit name by index.
    /// </summary>
    private static string GetUnitNameByIndex(int index)
    {
        return index switch
        {
            0 => "year",
            1 => "month",
            2 => "week",
            3 => "day",
            4 => "hour",
            5 => "minute",
            6 => "second",
            7 => "millisecond",
            8 => "microsecond",
            9 => "nanosecond",
            _ => "nanosecond"
        };
    }

    /// <summary>
    /// DifferenceTime ( time1, time2 )
    /// Computes the time difference between two IsoTime values as nanoseconds.
    /// Returns time2 - time1 in nanoseconds.
    /// </summary>
    private static BigInteger DifferenceTime(IsoTime time1, IsoTime time2)
    {
        var ns1 = (BigInteger) time1.Hour * NanosecondsPerHour
                  + (BigInteger) time1.Minute * NanosecondsPerMinute
                  + (BigInteger) time1.Second * NanosecondsPerSecond
                  + (BigInteger) time1.Millisecond * NanosecondsPerMillisecond
                  + (BigInteger) time1.Microsecond * NanosecondsPerMicrosecond
                  + (BigInteger) time1.Nanosecond;

        var ns2 = (BigInteger) time2.Hour * NanosecondsPerHour
                  + (BigInteger) time2.Minute * NanosecondsPerMinute
                  + (BigInteger) time2.Second * NanosecondsPerSecond
                  + (BigInteger) time2.Millisecond * NanosecondsPerMillisecond
                  + (BigInteger) time2.Microsecond * NanosecondsPerMicrosecond
                  + (BigInteger) time2.Nanosecond;

        return ns2 - ns1;
    }

    /// <summary>
    /// DifferenceISODateTime ( isoDateTime1, isoDateTime2, calendar, largestUnit )
    /// https://tc39.es/proposal-temporal/#sec-temporal-differenceisodatetime
    /// Computes the duration from isoDateTime1 to isoDateTime2.
    /// Simplified for ISO calendar.
    /// </summary>
    /// <summary>
    /// DifferenceISODateTime ( isoDateTime1, isoDateTime2, calendar, largestUnit )
    /// https://tc39.es/proposal-temporal/#sec-temporal-differenceisodatetime
    /// </summary>
    private static DurationRecord DifferenceISODateTime(
        IsoDateTime isoDateTime1,
        IsoDateTime isoDateTime2,
        string calendar,
        string largestUnit)
    {
        // Step 1-2: Assert ISODateTimeWithinLimits (optional)

        // Step 3: Compute time difference
        var timeDuration = DifferenceTime(isoDateTime1.Time, isoDateTime2.Time);

        // Step 4-5: Get signs
        var timeSign = TimeDurationSign(timeDuration);
        var dateSign = CompareIsoDates(isoDateTime1.Date, isoDateTime2.Date);

        // Step 6: Start with date2
        var adjustedDate = isoDateTime2.Date;

        // Step 7: Adjust if time and date have same sign (carry/borrow)
        if (timeSign == dateSign)
        {
            // Add or subtract one day using date arithmetic
            adjustedDate = AddDaysToISODate(adjustedDate, timeSign);
            timeDuration = Add24HourDaysToTimeDuration(timeDuration, -timeSign);
        }

        // Step 8: Determine date's largest unit (at least "day")
        var dateLargestUnit = LargerOfTwoTemporalUnits("day", largestUnit);

        // Step 9: Compute date difference using calendar
        var dateDifference = CalendarDateUntil(calendar, isoDateTime1.Date, adjustedDate, dateLargestUnit);

        // Step 10-11: If largestUnit is smaller than day, add days to time
        if (!string.Equals(dateLargestUnit, largestUnit, StringComparison.Ordinal))
        {
            timeDuration = Add24HourDaysToTimeDuration(timeDuration, dateDifference.Days);
            dateDifference = new DurationRecord(dateDifference.Years, dateDifference.Months, dateDifference.Weeks, 0, 0, 0, 0, 0, 0, 0);
        }

        // Step 12: Combine date and time durations (pass largestUnit for proper balancing)
        return CombineDateAndTimeDuration(dateDifference, timeDuration, largestUnit);
    }

    /// <summary>
    /// AddDaysToISODate ( isoDate, days )
    /// Adds a number of days to an ISO date.
    /// </summary>
    internal static IsoDate AddDaysToISODate(IsoDate date, double days)
    {
        var dayCount = IsoDateToDays(date.Year, date.Month, date.Day) + (long) days;
        return DaysToIsoDate(dayCount);
    }

    /// <summary>
    /// CompareSurpasses ( sign, year, monthOrCode, day, target )
    /// https://tc39.es/proposal-temporal (plaindate.html:696-723)
    /// Checks if a date surpasses target in the direction of sign.
    /// </summary>
    private static bool CompareSurpasses(int sign, int year, int month, int day, IsoDate target)
    {
        // Step 1: Compare year
        if (year != target.Year)
        {
            return sign * (year - target.Year) > 0;
        }

        // Step 2: Compare month (we use integer month, not month code)
        if (month != target.Month)
        {
            return sign * (month - target.Month) > 0;
        }

        // Step 3: Compare day
        if (day != target.Day)
        {
            return sign * (day - target.Day) > 0;
        }

        // Step 4: Equal, doesn't surpass
        return false;
    }

    /// <summary>
    /// ISODateSurpasses ( sign, baseDate, isoDate2, years, months, weeks, days )
    /// https://tc39.es/proposal-temporal (plaindate.html:726-764)
    /// Checks if baseDate + duration surpasses isoDate2 in the direction of sign.
    /// </summary>
    private static bool ISODateSurpasses(int sign, IsoDate baseDate, IsoDate isoDate2, int years, int months, int weeks, int days)
    {
        // Step 1-2: For ISO calendar, the date is just the base date
        var y0 = baseDate.Year + years;
        var m0 = baseDate.Month;
        var d0 = baseDate.Day;

        // Step 3: Check if adding just years surpasses
        if (CompareSurpasses(sign, y0, m0, d0, isoDate2))
        {
            return true;
        }

        // Step 4-7: If we have months to add, add them and check
        if (months != 0)
        {
            m0 += months;
            var balanced = BalanceISOYearMonth(y0, m0);
            y0 = balanced.Year;
            m0 = balanced.Month;

            if (CompareSurpasses(sign, y0, m0, d0, isoDate2))
            {
                return true;
            }
        }

        // Step 8: If no weeks AND no days to add, we're done
        if (weeks == 0 && days == 0)
        {
            return false;
        }

        // Step 9: Regulate the date (use constrain per spec note)
        var regulated = RegulateIsoDate(y0, m0, d0, "constrain");
        if (regulated is null)
        {
            // If regulation fails, assume it surpasses for safety
            return true;
        }

        // Step 10-11: Add weeks (as days) and days
        var totalDays = 7 * weeks + days;
        var finalDate = AddDaysToISODate(regulated.Value, totalDays);

        // Step 12: Check final date
        return CompareSurpasses(sign, finalDate.Year, finalDate.Month, finalDate.Day, isoDate2);
    }

    /// <summary>
    /// CalendarDateUntil ( calendar, one, two, largestUnit )
    /// https://tc39.es/proposal-temporal/#sec-temporal-calendardateuntil
    /// Determines the difference between dates using the calendar's reckoning.
    /// Optimized implementation that produces the same results as the spec's iterative
    /// ISODateSurpasses algorithm (calendar.html:632-661) but uses direct arithmetic
    /// for O(1) performance on years/days instead of O(n) iteration.
    /// </summary>
    internal static DurationRecord CalendarDateUntil(string calendar, IsoDate one, IsoDate two, string largestUnit)
    {
        // Step 1: Get sign
        var sign = CompareIsoDates(one, two);

        // Step 2: If equal, return zero
        if (sign == 0)
        {
            return new DurationRecord(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        // Step 3: For ISO and Gregorian-based calendars (same arithmetic)
        if (!IsGregorianBasedCalendar(calendar))
        {
            throw new NotSupportedException($"Calendar '{calendar}' not yet supported");
        }

        // Step 3.1: Negate sign per spec (line 637)
        // sign = -CompareIsoDates(one, two), so sign > 0 means one < two (moving forward)
        sign = -sign;

        // Step 3.2-3.4: Count years (if largestUnit is year)
        int years = 0;
        if (string.Equals(largestUnit, "year", StringComparison.Ordinal))
        {
            // Estimate years directly instead of iterating
            years = two.Year - one.Year;
            // Adjust: if the month/day of (one + years) surpasses two, reduce by sign
            if (years != 0)
            {
                // Check if we overshot
                if (ISODateSurpasses(sign, one, two, years, 0, 0, 0))
                {
                    years -= sign;
                }
            }
        }

        // Step 3.5-3.7: Count months (if largestUnit is year or month)
        int months = 0;
        if (string.Equals(largestUnit, "year", StringComparison.Ordinal) || string.Equals(largestUnit, "month", StringComparison.Ordinal))
        {
            if (string.Equals(largestUnit, "month", StringComparison.Ordinal))
            {
                // Estimate total months directly
                months = (two.Year - one.Year) * 12 + (two.Month - one.Month);
                // Adjust if overshot
                if (months != 0 && ISODateSurpasses(sign, one, two, 0, months, 0, 0))
                {
                    months -= sign;
                }
            }
            else
            {
                // Year already computed, count remaining months (max 11 iterations)
                int candidateMonths = sign;
                while (!ISODateSurpasses(sign, one, two, years, candidateMonths, 0, 0))
                {
                    months = candidateMonths;
                    candidateMonths += sign;
                }
            }
        }

        // Step 3.8-3.11: Count weeks (if largestUnit is week)
        int weeks = 0;
        if (string.Equals(largestUnit, "week", StringComparison.Ordinal))
        {
            // Compute remaining days using epoch day arithmetic, then divide by 7
            var intermediateDate = ComputeIntermediateDate(one, years, months, 0);
            var epochDaysIntermediate = IsoDateToDays(intermediateDate.Year, intermediateDate.Month, intermediateDate.Day);
            var epochDaysTwo = IsoDateToDays(two.Year, two.Month, two.Day);
            var remainingDays = (int) (epochDaysTwo - epochDaysIntermediate);
            // weeks = truncate toward zero (remainingDays / 7)
            weeks = remainingDays / 7;
            // Verify with ISODateSurpasses for correctness at boundaries
            if (weeks != 0 && ISODateSurpasses(sign, one, two, years, months, weeks, 0))
            {
                weeks -= sign;
            }
        }

        // Step 3.12-3.15: Count days
        // Compute the intermediate date after years+months+weeks, then use epoch day diff
        var intermediate = ComputeIntermediateDate(one, years, months, weeks);
        var epochIntermediate = IsoDateToDays(intermediate.Year, intermediate.Month, intermediate.Day);
        var epochTwo = IsoDateToDays(two.Year, two.Month, two.Day);
        var days = (int) (epochTwo - epochIntermediate);

        // Step 3.16: Return the duration
        return new DurationRecord(years, months, weeks, days, 0, 0, 0, 0, 0, 0);
    }

    /// <summary>
    /// Computes the intermediate date after adding years, months, and weeks to a base date.
    /// This mirrors the ISODateSurpasses logic: add years, add months, balance year/month,
    /// constrain day to valid range, then add weeks as days.
    /// </summary>
    private static IsoDate ComputeIntermediateDate(IsoDate baseDate, int years, int months, int weeks)
    {
        var y = baseDate.Year + years;
        var m = baseDate.Month + months;

        if (m > 12 || m < 1)
        {
            var balanced = BalanceISOYearMonth(y, m);
            y = balanced.Year;
            m = balanced.Month;
        }

        // Constrain day to valid range for this year/month
        var d = System.Math.Min(baseDate.Day, IsoDate.IsoDateInMonth(y, m));

        if (weeks != 0)
        {
            return AddDaysToISODate(new IsoDate(y, m, d), weeks * 7);
        }

        return new IsoDate(y, m, d);
    }

    /// <summary>
    /// DifferencePlainDateTimeWithRounding ( isoDateTime1, isoDateTime2, calendar, largestUnit, roundingIncrement, smallestUnit, roundingMode )
    /// https://tc39.es/proposal-temporal/#sec-temporal-differenceplaindatetimewithrounding
    /// </summary>
    /// <summary>
    /// DifferencePlainDateTimeWithRounding ( isoDateTime1, isoDateTime2, calendar, largestUnit, roundingIncrement, smallestUnit, roundingMode )
    /// https://tc39.es/proposal-temporal/#sec-temporal-differenceplaindatetimewithrounding
    /// </summary>
    public static DurationRecord DifferencePlainDateTimeWithRounding(
        Realm realm,
        IsoDateTime isoDateTime1,
        IsoDateTime isoDateTime2,
        string calendar,
        string largestUnit,
        int roundingIncrement,
        string smallestUnit,
        string roundingMode)
    {
        // Step 1: If equal, return zero
        if (CompareIsoDateTime(isoDateTime1, isoDateTime2) == 0)
        {
            return CombineDateAndTimeDuration(new DurationRecord(0, 0, 0, 0, 0, 0, 0, 0, 0, 0), 0);
        }

        // Step 2: Check ISODateTimeWithinLimits for both datetimes
        if (!ISODateTimeWithinLimits(isoDateTime1) || !ISODateTimeWithinLimits(isoDateTime2))
        {
            Throw.RangeError(realm, "DateTime value is outside the supported range");
        }

        // Step 3: Get unrounded difference
        var diff = DifferenceISODateTime(isoDateTime1, isoDateTime2, calendar, largestUnit);

        // Step 4: If no rounding needed, return
        if (string.Equals(smallestUnit, "nanosecond", StringComparison.Ordinal) && roundingIncrement == 1)
        {
            return diff;
        }

        // Step 5-7: Round the difference
        var originEpochNs = GetUTCEpochNanoseconds(isoDateTime1);
        var destEpochNs = GetUTCEpochNanoseconds(isoDateTime2);

        // timeZone is ~unset~ for PlainDateTime
        return RoundRelativeDuration(realm, null, diff, originEpochNs, destEpochNs, isoDateTime1, null, calendar, largestUnit, roundingIncrement, smallestUnit, roundingMode);
    }

    /// <summary>
    /// CompareIsoDateTime ( dt1, dt2 )
    /// Compares two ISO date-time records.
    /// </summary>
    private static int CompareIsoDateTime(IsoDateTime dt1, IsoDateTime dt2)
    {
        var dateComp = CompareIsoDates(dt1.Date, dt2.Date);
        if (dateComp != 0)
            return dateComp;

        // Compare times
        if (dt1.Time.Hour != dt2.Time.Hour)
            return dt1.Time.Hour.CompareTo(dt2.Time.Hour);
        if (dt1.Time.Minute != dt2.Time.Minute)
            return dt1.Time.Minute.CompareTo(dt2.Time.Minute);
        if (dt1.Time.Second != dt2.Time.Second)
            return dt1.Time.Second.CompareTo(dt2.Time.Second);
        if (dt1.Time.Millisecond != dt2.Time.Millisecond)
            return dt1.Time.Millisecond.CompareTo(dt2.Time.Millisecond);
        if (dt1.Time.Microsecond != dt2.Time.Microsecond)
            return dt1.Time.Microsecond.CompareTo(dt2.Time.Microsecond);
        return dt1.Time.Nanosecond.CompareTo(dt2.Time.Nanosecond);
    }

    /// <summary>
    /// ToOffsetString: converts a value to a string and validates it's an offset string.
    /// https://tc39.es/proposal-temporal/#sec-temporal-tooffsetstring
    /// Throws TypeError if value is not a string, RangeError if it's not a valid offset format.
    /// </summary>
    internal static string ToOffsetString(Realm realm, JsValue argument)
    {
        // ToPrimitive with hint "string"
        var primitive = TypeConverter.ToPrimitive(argument, Types.String);

        // If not a string, throw TypeError
        if (!primitive.IsString())
        {
            Throw.TypeError(realm, "offset must be a string");
        }

        var offsetString = primitive.ToString();

        // Validate it's a valid offset string (ParseOffsetString returns null for invalid)
        if (ParseOffsetString(offsetString) is null)
        {
            Throw.RangeError(realm, "Invalid offset string");
        }

        return offsetString;
    }
}
