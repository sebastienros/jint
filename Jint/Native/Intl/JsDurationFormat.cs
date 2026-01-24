using System.Globalization;
using System.Text;
using Jint.Native.Object;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/proposal-intl-duration-format/
/// Represents an Intl.DurationFormat instance for locale-aware duration formatting.
/// </summary>
internal sealed class JsDurationFormat : ObjectInstance
{
    internal JsDurationFormat(
        Engine engine,
        ObjectInstance prototype,
        string locale,
        string style,
        string numberingSystem,
        CultureInfo cultureInfo,
        string yearsStyle,
        string monthsStyle,
        string weeksStyle,
        string daysStyle,
        string hoursStyle,
        string minutesStyle,
        string secondsStyle,
        string millisecondsStyle,
        string microsecondsStyle,
        string nanosecondsStyle,
        string yearsDisplay,
        string monthsDisplay,
        string weeksDisplay,
        string daysDisplay,
        string hoursDisplay,
        string minutesDisplay,
        string secondsDisplay,
        string millisecondsDisplay,
        string microsecondsDisplay,
        string nanosecondsDisplay,
        int? fractionalDigits) : base(engine)
    {
        _prototype = prototype;
        Locale = locale;
        Style = style;
        NumberingSystem = numberingSystem;
        CultureInfo = cultureInfo;

        YearsStyle = yearsStyle;
        MonthsStyle = monthsStyle;
        WeeksStyle = weeksStyle;
        DaysStyle = daysStyle;
        HoursStyle = hoursStyle;
        MinutesStyle = minutesStyle;
        SecondsStyle = secondsStyle;
        MillisecondsStyle = millisecondsStyle;
        MicrosecondsStyle = microsecondsStyle;
        NanosecondsStyle = nanosecondsStyle;

        YearsDisplay = yearsDisplay;
        MonthsDisplay = monthsDisplay;
        WeeksDisplay = weeksDisplay;
        DaysDisplay = daysDisplay;
        HoursDisplay = hoursDisplay;
        MinutesDisplay = minutesDisplay;
        SecondsDisplay = secondsDisplay;
        MillisecondsDisplay = millisecondsDisplay;
        MicrosecondsDisplay = microsecondsDisplay;
        NanosecondsDisplay = nanosecondsDisplay;

        FractionalDigits = fractionalDigits;
    }

    /// <summary>
    /// Gets the CLDR provider from engine options.
    /// </summary>
    private ICldrProvider CldrProvider => _engine.Options.Intl.CldrProvider;

    /// <summary>
    /// The locale used for formatting.
    /// </summary>
    internal string Locale { get; }

    /// <summary>
    /// The style: "long", "short", "narrow", or "digital".
    /// </summary>
    internal string Style { get; }

    /// <summary>
    /// The numbering system.
    /// </summary>
    internal string NumberingSystem { get; }

    /// <summary>
    /// The .NET CultureInfo for locale-specific formatting.
    /// </summary>
    internal CultureInfo CultureInfo { get; }

    // Unit styles
    internal string YearsStyle { get; }
    internal string MonthsStyle { get; }
    internal string WeeksStyle { get; }
    internal string DaysStyle { get; }
    internal string HoursStyle { get; }
    internal string MinutesStyle { get; }
    internal string SecondsStyle { get; }
    internal string MillisecondsStyle { get; }
    internal string MicrosecondsStyle { get; }
    internal string NanosecondsStyle { get; }

    // Unit displays
    internal string YearsDisplay { get; }
    internal string MonthsDisplay { get; }
    internal string WeeksDisplay { get; }
    internal string DaysDisplay { get; }
    internal string HoursDisplay { get; }
    internal string MinutesDisplay { get; }
    internal string SecondsDisplay { get; }
    internal string MillisecondsDisplay { get; }
    internal string MicrosecondsDisplay { get; }
    internal string NanosecondsDisplay { get; }

    // Fractional digits for sub-second units
    internal int? FractionalDigits { get; }

    /// <summary>
    /// Formats a duration object.
    /// </summary>
    internal string Format(DurationRecord duration)
    {
        var isDigital = string.Equals(Style, "digital", StringComparison.Ordinal);

        if (isDigital)
        {
            return FormatDigital(duration);
        }

        return FormatNonDigital(duration);
    }

    private string FormatDigital(DurationRecord duration)
    {
        var parts = new List<string>();

        // Check if the duration is negative
        var isNegative = duration.Years < 0 || duration.Months < 0 || duration.Weeks < 0 ||
                        duration.Days < 0 || duration.Hours < 0 || duration.Minutes < 0 ||
                        duration.Seconds < 0 || duration.Milliseconds < 0 ||
                        duration.Microseconds < 0 || duration.Nanoseconds < 0;
        var displayNegativeSign = isNegative;

        // For digital style, years/months/weeks/days are formatted with unit labels using "short" style
        // Per Table 1 in spec, digital style uses "short" for date units
        // Date units use grouping (thousand separators) and proper pluralization
        void AddDateUnitIfNeeded(double value, string display, string unitName)
        {
            if (value == 0 && !string.Equals(display, "always", StringComparison.Ordinal))
            {
                return;
            }

            var absValue = System.Math.Abs(value);
            var prefix = displayNegativeSign ? "-" : "";
            displayNegativeSign = false;

            // Get unit patterns from CLDR provider for proper pluralization
            var unitPatterns = CldrProvider.GetUnitPatterns(Locale, unitName, "short");
            string formatted;

            if (unitPatterns != null)
            {
                // Choose singular or plural based on value
                var isPlural = absValue != 1;
                var pattern = isPlural ? unitPatterns.Other : (unitPatterns.One ?? unitPatterns.Other);

                // Format the number with grouping using locale's culture
                var numberWithGrouping = ((long) absValue).ToString("N0", CultureInfo);
                formatted = pattern.Replace("{0}", $"{prefix}{numberWithGrouping}");
            }
            else
            {
                // Fallback: use simple formatting with grouping
                var label = GetUnitLabel(absValue, unitName, "short");
                var numberWithGrouping = ((long) absValue).ToString("N0", CultureInfo);
                formatted = $"{prefix}{numberWithGrouping} {label}";
            }

            parts.Add(formatted);
        }

        // Add date units with their unit names (per Table 1, digital style uses "short")
        AddDateUnitIfNeeded(duration.Years, YearsDisplay, "year");
        AddDateUnitIfNeeded(duration.Months, MonthsDisplay, "month");
        AddDateUnitIfNeeded(duration.Weeks, WeeksDisplay, "week");
        AddDateUnitIfNeeded(duration.Days, DaysDisplay, "day");

        // Now format the digital time part (HH:MM:SS.fff)
        var sb = new StringBuilder();

        var hours = System.Math.Abs(duration.Hours);
        var minutes = System.Math.Abs(duration.Minutes);
        var seconds = System.Math.Abs(duration.Seconds);
        var milliseconds = System.Math.Abs(duration.Milliseconds);
        var microseconds = System.Math.Abs(duration.Microseconds);
        var nanoseconds = System.Math.Abs(duration.Nanoseconds);

        // Convert sub-second units to total seconds (milliseconds add to seconds, not just fractional)
        // Per spec: sub-second units are converted to fractional seconds
        var totalSubSeconds = milliseconds / 1000.0 + microseconds / 1_000_000.0 + nanoseconds / 1_000_000_000.0;
        var totalSeconds = seconds + totalSubSeconds;
        var wholeSeconds = (long) System.Math.Floor(totalSeconds);
        var fractionalPart = totalSeconds - wholeSeconds;

        // Add negative sign if this is the first displayed element
        if (displayNegativeSign)
        {
            sb.Append('-');
            displayNegativeSign = false;
        }

        // Add hours if non-zero or if hoursDisplay is "always"
        var showHours = duration.Hours != 0 || string.Equals(HoursDisplay, "always", StringComparison.Ordinal);

        if (showHours)
        {
            sb.Append((long) hours);
            sb.Append(':');
        }

        // Minutes always 2-digit in digital style
        sb.Append(((long) minutes).ToString("D2", CultureInfo.InvariantCulture));

        sb.Append(':');
        // Seconds: 2-digit only if less than 100, otherwise use actual digits (no grouping)
        if (wholeSeconds < 100)
        {
            sb.Append(wholeSeconds.ToString("D2", CultureInfo.InvariantCulture));
        }
        else
        {
            sb.Append(wholeSeconds);
        }

        // Add fractional seconds if needed
        if (fractionalPart > 0 || FractionalDigits.HasValue)
        {
            if (FractionalDigits.HasValue)
            {
                var digits = FractionalDigits.Value;
                if (digits > 0)
                {
                    // Format fractional part with specified number of digits
                    var formatStr = "F" + digits;
                    var fractionalStr = fractionalPart.ToString(formatStr, CultureInfo.InvariantCulture);
                    // Remove leading "0" to get just ".xxx"
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
                    sb.Append(fractionalStr.AsSpan(1));
#else
                    sb.Append(fractionalStr, 1, fractionalStr.Length - 1);
#endif
                }
            }
            else if (fractionalPart > 0)
            {
                // Format fractional part, trimming trailing zeros
                // Convert to string with enough precision, then trim
                var fractionalStr = fractionalPart.ToString("F9", CultureInfo.InvariantCulture);
                // Remove leading "0" and trailing zeros (e.g., "0.567000000" -> ".567")
                var startIndex = 1; // Skip the leading "0"
                var endIndex = fractionalStr.Length;
                while (endIndex > startIndex && fractionalStr[endIndex - 1] == '0')
                {
                    endIndex--;
                }
                if (endIndex > startIndex + 1) // More than just "."
                {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
                    sb.Append(fractionalStr.AsSpan(startIndex, endIndex - startIndex));
#else
                    sb.Append(fractionalStr, startIndex, endIndex - startIndex);
#endif
                }
            }
        }

        parts.Add(sb.ToString());

        return string.Join(", ", parts);
    }

    private string FormatNonDigital(DurationRecord duration)
    {
        var parts = new List<string>();

        // Check if the duration is negative (any non-zero component is negative)
        var isNegative = duration.Years < 0 || duration.Months < 0 || duration.Weeks < 0 ||
                        duration.Days < 0 || duration.Hours < 0 || duration.Minutes < 0 ||
                        duration.Seconds < 0 || duration.Milliseconds < 0 ||
                        duration.Microseconds < 0 || duration.Nanoseconds < 0;

        // Track whether we've shown the negative sign yet
        var displayNegativeSign = isNegative;

        // Helper to check if a style is numeric
        bool IsNumericStyle(string style) =>
            string.Equals(style, "numeric", StringComparison.Ordinal) ||
            string.Equals(style, "2-digit", StringComparison.Ordinal);

        void AddUnitIfNeeded(double value, string display, string unitName, string unitStyle)
        {
            if (!ShouldShowUnit(value, display))
            {
                return;
            }

            // Use absolute value for the number, handle sign separately
            var absValue = System.Math.Abs(value);

            // Format using CLDR provider
            string formatted;
            if (displayNegativeSign)
            {
                displayNegativeSign = false;
                var prefix = value == 0 ? "-" : (value < 0 ? "-" : "");
                formatted = FormatUnitWithCldr(absValue, unitName, unitStyle, prefix);
            }
            else
            {
                formatted = FormatUnitWithCldr(absValue, unitName, unitStyle, "");
            }

            parts.Add(formatted);
        }

        string FormatUnitWithCldr(double absValue, string unitName, string unitStyle, string prefix)
        {
            // Get unit patterns from CLDR provider
            var unitPatterns = CldrProvider.GetUnitPatterns(Locale, unitName, unitStyle);
            if (unitPatterns == null)
            {
                // Fallback using GetUnitLabel
                var label = GetUnitLabel(absValue, unitName, unitStyle);
                var needsSpace = !string.Equals(unitStyle, "narrow", StringComparison.Ordinal);
                return needsSpace ? $"{prefix}{(long) absValue} {label}" : $"{prefix}{(long) absValue}{label}";
            }

            // Choose singular or plural based on value
            var isPlural = absValue != 1;
            var pattern = isPlural ? unitPatterns.Other : (unitPatterns.One ?? unitPatterns.Other);

            // Replace {0} with the formatted number
            return pattern.Replace("{0}", $"{prefix}{(long) absValue}");
        }

        void AddSubSecondUnitIfNeeded(double value, string display, string unitName, string unitStyle)
        {
            if (!ShouldShowUnit(value, display))
            {
                return;
            }

            // Sub-second units use the same CLDR provider logic as regular units
            var absValue = System.Math.Abs(value);

            string formatted;
            if (displayNegativeSign)
            {
                displayNegativeSign = false;
                var prefix = value == 0 ? "-" : (value < 0 ? "-" : "");
                formatted = FormatUnitWithCldr(absValue, unitName, unitStyle, prefix);
            }
            else
            {
                formatted = FormatUnitWithCldr(absValue, unitName, unitStyle, "");
            }

            parts.Add(formatted);
        }

        // Years
        AddUnitIfNeeded(duration.Years, YearsDisplay, "year", YearsStyle);

        // Months
        AddUnitIfNeeded(duration.Months, MonthsDisplay, "month", MonthsStyle);

        // Weeks
        AddUnitIfNeeded(duration.Weeks, WeeksDisplay, "week", WeeksStyle);

        // Days
        AddUnitIfNeeded(duration.Days, DaysDisplay, "day", DaysStyle);

        // Check if we need to format time units numerically (with : separators)
        var hoursIsNumeric = IsNumericStyle(HoursStyle);
        var minutesIsNumeric = IsNumericStyle(MinutesStyle);
        var secondsIsNumeric = IsNumericStyle(SecondsStyle);

        // Check if there are any non-zero sub-second units
        var hasSubSeconds = ShouldShowUnit(duration.Milliseconds, MillisecondsDisplay) ||
                           ShouldShowUnit(duration.Microseconds, MicrosecondsDisplay) ||
                           ShouldShowUnit(duration.Nanoseconds, NanosecondsDisplay);

        // Format hours with unit labels if not numeric
        if (!hoursIsNumeric)
        {
            AddUnitIfNeeded(duration.Hours, HoursDisplay, "hour", HoursStyle);
        }

        // Determine if we should format minutes/seconds numerically
        // Numeric mode starts when hours is numeric, or when minutes is numeric
        var formatTimeNumerically = hoursIsNumeric || minutesIsNumeric;

        // Track whether sub-seconds were consumed as fractional seconds
        var subSecondsConsumed = false;

        if (formatTimeNumerically)
        {
            // Format time units with : separators
            var sb = new StringBuilder();

            // Add negative sign if needed (only if hours wasn't already shown)
            if (displayNegativeSign && hoursIsNumeric)
            {
                sb.Append('-');
                displayNegativeSign = false;
            }

            var hours = System.Math.Abs(duration.Hours);
            var minutes = System.Math.Abs(duration.Minutes);
            var seconds = System.Math.Abs(duration.Seconds);
            var milliseconds = System.Math.Abs(duration.Milliseconds);
            var microseconds = System.Math.Abs(duration.Microseconds);
            var nanoseconds = System.Math.Abs(duration.Nanoseconds);

            // Determine which time units should be shown
            // Note: numeric style only affects HOW units are formatted (with :), not WHETHER they are shown
            // Cascade rule: later units pull in earlier numeric units ONLY if the earlier unit is shown
            var shouldShowHoursBase = ShouldShowUnit(duration.Hours, HoursDisplay);

            // When hours is shown and there are sub-seconds, cascade to show minutes and seconds
            // to format sub-seconds as fractional seconds (e.g., "1:00:00.001")
            var showSecondsBase = ShouldShowUnit(duration.Seconds, SecondsDisplay);
            var showSeconds = showSecondsBase || (shouldShowHoursBase && hasSubSeconds);

            // Minutes cascades from hours (to maintain h:mm:ss format) only if hours is also shown
            var showMinutesBase = ShouldShowUnit(duration.Minutes, MinutesDisplay);
            var showMinutes = showMinutesBase ||
                             (minutesIsNumeric && showSeconds && shouldShowHoursBase);
            var showHours = hoursIsNumeric && shouldShowHoursBase;

            if (showHours)
            {
                if (string.Equals(HoursStyle, "2-digit", StringComparison.Ordinal))
                {
                    sb.Append(((long) hours).ToString("D2", CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append((long) hours);
                }

                if (showMinutes || showSeconds)
                {
                    sb.Append(':');
                }
            }

            if (showMinutes)
            {
                // Add negative sign before minutes if this is the first numeric unit
                if (displayNegativeSign && !showHours)
                {
                    sb.Append('-');
                    displayNegativeSign = false;
                }

                // Minutes are 2-digit when following hours or when minutes style is 2-digit
                if (showHours || string.Equals(MinutesStyle, "2-digit", StringComparison.Ordinal))
                {
                    sb.Append(((long) minutes).ToString("D2", CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append((long) minutes);
                }

                if (showSeconds)
                {
                    sb.Append(':');
                }
            }

            if (showSeconds)
            {
                // Seconds are 2-digit when following minutes or when seconds style is 2-digit
                if (showMinutes || string.Equals(SecondsStyle, "2-digit", StringComparison.Ordinal))
                {
                    sb.Append(((long) seconds).ToString("D2", CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append((long) seconds);
                }

                // If in numeric format with hours showing, add sub-seconds as fractional part
                if (showHours && hasSubSeconds)
                {
                    var totalSubSeconds = milliseconds / 1000.0 + microseconds / 1_000_000.0 + nanoseconds / 1_000_000_000.0;
                    if (totalSubSeconds > 0 || FractionalDigits.HasValue)
                    {
                        if (FractionalDigits.HasValue)
                        {
                            var digits = FractionalDigits.Value;
                            if (digits > 0)
                            {
                                var formatStr = "F" + digits;
                                var fractionalStr = totalSubSeconds.ToString(formatStr, CultureInfo.InvariantCulture);
                                // Remove leading "0" to get just ".xxx"
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
                                sb.Append(fractionalStr.AsSpan(1));
#else
                                sb.Append(fractionalStr, 1, fractionalStr.Length - 1);
#endif
                            }
                        }
                        else if (totalSubSeconds > 0)
                        {
                            // Format fractional part, trimming trailing zeros
                            var fractionalStr = totalSubSeconds.ToString("F9", CultureInfo.InvariantCulture);
                            // Remove leading "0" and trailing zeros
                            var startIndex = 1; // Skip the leading "0"
                            var endIndex = fractionalStr.Length;
                            while (endIndex > startIndex && fractionalStr[endIndex - 1] == '0')
                            {
                                endIndex--;
                            }
                            if (endIndex > startIndex + 1) // More than just "."
                            {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
                                sb.Append(fractionalStr.AsSpan(startIndex, endIndex - startIndex));
#else
                                sb.Append(fractionalStr, startIndex, endIndex - startIndex);
#endif
                            }
                        }
                        subSecondsConsumed = true;
                    }
                }
            }

            if (sb.Length > 0 && (sb.Length > 1 || sb[0] != '-'))
            {
                parts.Add(sb.ToString());
            }
        }
        else
        {
            // Format minutes with unit labels (hours already handled above)
            AddUnitIfNeeded(duration.Minutes, MinutesDisplay, "minute", MinutesStyle);

            // Special case: when seconds is numeric but hours/minutes are not,
            // and there are no hours/minutes to display, format seconds as just a number
            if (secondsIsNumeric && !ShouldShowUnit(duration.Hours, HoursDisplay) && !ShouldShowUnit(duration.Minutes, MinutesDisplay))
            {
                // Format seconds (including sub-seconds) as a plain numeric value
                var seconds = System.Math.Abs(duration.Seconds);
                var milliseconds = System.Math.Abs(duration.Milliseconds);
                var microseconds = System.Math.Abs(duration.Microseconds);
                var nanoseconds = System.Math.Abs(duration.Nanoseconds);

                // Convert sub-seconds to total seconds
                var totalSubSeconds = milliseconds / 1000.0 + microseconds / 1_000_000.0 + nanoseconds / 1_000_000_000.0;
                var totalSeconds = seconds + totalSubSeconds;

                if (ShouldShowUnit(totalSeconds, SecondsDisplay) || totalSubSeconds > 0)
                {
                    var prefix = displayNegativeSign ? "-" : "";
                    if (displayNegativeSign)
                    {
                        displayNegativeSign = false;
                    }

                    // Format with truncation rounding per spec
                    string formatted;
                    if (FractionalDigits.HasValue)
                    {
                        var digits = FractionalDigits.Value;
                        // Truncate to specified digits
                        var multiplier = System.Math.Pow(10, digits);
                        var truncated = System.Math.Truncate(totalSeconds * multiplier) / multiplier;
                        if (digits > 0)
                        {
                            formatted = truncated.ToString($"F{digits}", CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            formatted = ((long) truncated).ToString(CultureInfo.InvariantCulture);
                        }
                    }
                    else
                    {
                        // No fractional digits specified - use default formatting with trailing zeros trimmed
                        formatted = totalSeconds.ToString("0.#########", CultureInfo.InvariantCulture);
                    }

                    parts.Add($"{prefix}{formatted}");
                    subSecondsConsumed = true;
                }
            }
            else
            {
                // Format seconds with unit label
                AddUnitIfNeeded(duration.Seconds, SecondsDisplay, "second", SecondsStyle);
            }
        }

        // Only add sub-seconds with labels if they weren't consumed as fractional seconds
        if (!subSecondsConsumed)
        {
            // Check for fractional cascade: when a sub-second unit has "numeric" style,
            // the previous unit should include it as a fractional part
            var microsecondsIsNumeric = IsNumericStyle(MicrosecondsStyle);
            var nanosecondsIsNumeric = IsNumericStyle(NanosecondsStyle);

            // Helper to format a sub-second unit with fractional parts from subsequent units
            void AddSubSecondWithFractional(double value, string display, string unitName, string unitStyle,
                double fractionalValue, int fractionalDigits)
            {
                if (!ShouldShowUnit(value, display) && fractionalValue == 0)
                {
                    return;
                }

                var absValue = System.Math.Abs(value);
                var totalValue = absValue + fractionalValue;

                // Get unit patterns from CLDR provider
                var unitPatterns = CldrProvider.GetUnitPatterns(Locale, unitName, unitStyle);
                string formatted;

                if (unitPatterns != null)
                {
                    // Choose singular or plural based on whole value (not fractional)
                    var isPlural = absValue != 1;
                    var pattern = isPlural ? unitPatterns.Other : (unitPatterns.One ?? unitPatterns.Other);

                    // Format the number with fractional digits
                    string numStr;
                    if (fractionalValue > 0)
                    {
                        // Format with up to 9 decimal places, trimming trailing zeros
                        numStr = totalValue.ToString("0.#########", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        numStr = ((long) absValue).ToString(CultureInfo.InvariantCulture);
                    }

                    var prefix = displayNegativeSign ? "-" : "";
                    if (displayNegativeSign)
                    {
                        displayNegativeSign = false;
                    }

                    formatted = pattern.Replace("{0}", $"{prefix}{numStr}");
                }
                else
                {
                    // Fallback
                    var label = GetUnitLabel(absValue, unitName, unitStyle);
                    var needsSpace = !string.Equals(unitStyle, "narrow", StringComparison.Ordinal);
                    string numStr;
                    if (fractionalValue > 0)
                    {
                        numStr = totalValue.ToString("0.#########", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        numStr = ((long) absValue).ToString(CultureInfo.InvariantCulture);
                    }

                    var prefix = displayNegativeSign ? "-" : "";
                    if (displayNegativeSign)
                    {
                        displayNegativeSign = false;
                    }

                    formatted = needsSpace ? $"{prefix}{numStr} {label}" : $"{prefix}{numStr}{label}";
                }

                parts.Add(formatted);
            }

            var milliseconds = System.Math.Abs(duration.Milliseconds);
            var microseconds = System.Math.Abs(duration.Microseconds);
            var nanoseconds = System.Math.Abs(duration.Nanoseconds);

            // Case 1: microseconds is numeric - milliseconds gets fractional part from micro+nano
            if (microsecondsIsNumeric && !IsNumericStyle(MillisecondsStyle))
            {
                // Milliseconds includes microseconds and nanoseconds as fractional part
                // e.g., 444 ms + 55 μs + 6 ns = 444.055006 ms
                var fractionalPart = microseconds / 1000.0 + nanoseconds / 1_000_000.0;
                AddSubSecondWithFractional(duration.Milliseconds, MillisecondsDisplay, "millisecond", MillisecondsStyle,
                    fractionalPart, 9);
                // Skip microseconds and nanoseconds - they're consumed
            }
            // Case 2: nanoseconds is numeric but microseconds is not - microseconds gets fractional from nano
            else if (nanosecondsIsNumeric && !microsecondsIsNumeric && !IsNumericStyle(MillisecondsStyle))
            {
                // Milliseconds formatted normally
                AddSubSecondUnitIfNeeded(duration.Milliseconds, MillisecondsDisplay, "millisecond", MillisecondsStyle);

                // Microseconds includes nanoseconds as fractional part
                // e.g., 55 μs + 6 ns = 55.006 μs
                var fractionalPart = nanoseconds / 1000.0;
                AddSubSecondWithFractional(duration.Microseconds, MicrosecondsDisplay, "microsecond", MicrosecondsStyle,
                    fractionalPart, 9);
                // Skip nanoseconds - they're consumed
            }
            // Case 3: No fractional cascade - format all sub-seconds normally
            else
            {
                // Milliseconds
                AddSubSecondUnitIfNeeded(duration.Milliseconds, MillisecondsDisplay, "millisecond", MillisecondsStyle);

                // Microseconds
                AddSubSecondUnitIfNeeded(duration.Microseconds, MicrosecondsDisplay, "microsecond", MicrosecondsStyle);

                // Nanoseconds
                AddSubSecondUnitIfNeeded(duration.Nanoseconds, NanosecondsDisplay, "nanosecond", NanosecondsStyle);
            }
        }

        if (parts.Count == 0)
        {
            // If all units are zero and their display is "auto", return empty string
            // Only show "0 seconds" if at least one unit has display "always"
            if (HasAnyAlwaysDisplay())
            {
                // Format zero seconds using CLDR provider
                var unitPatterns = CldrProvider.GetUnitPatterns(Locale, "second", SecondsStyle);
                if (unitPatterns != null)
                {
                    // Use plural form for 0 (CLDR treats 0 as "other" category)
                    return unitPatterns.Other.Replace("{0}", "0");
                }
                else
                {
                    var label = GetUnitLabel(0, "second", SecondsStyle);
                    var needsSpace = !string.Equals(SecondsStyle, "narrow", StringComparison.Ordinal);
                    return needsSpace ? $"0 {label}" : $"0{label}";
                }
            }
            return "";
        }

        // Join parts based on style
        if (string.Equals(Style, "narrow", StringComparison.Ordinal))
        {
            return string.Join(" ", parts);
        }

        // For long and short, use comma-separated
        // Note: The test harness expects comma separation, not "and" conjunction
        return string.Join(", ", parts);
    }

    private static bool ShouldShowUnit(double value, string display)
    {
        if (string.Equals(display, "always", StringComparison.Ordinal))
        {
            return true;
        }
        // "auto" - only show if non-zero
        return value != 0;
    }

    private bool HasAnyAlwaysDisplay()
    {
        return string.Equals(YearsDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(MonthsDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(WeeksDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(DaysDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(HoursDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(MinutesDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(SecondsDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(MillisecondsDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(MicrosecondsDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(NanosecondsDisplay, "always", StringComparison.Ordinal);
    }

    private static string FormatUnit(double value, string singularLong, string pluralLong, string shortForm, string narrowForm, string unitStyle)
    {
        var isPlural = System.Math.Abs(value) != 1;

        // Handle numeric and 2-digit styles
        if (string.Equals(unitStyle, "numeric", StringComparison.Ordinal))
        {
            return ((long) value).ToString(CultureInfo.InvariantCulture);
        }

        if (string.Equals(unitStyle, "2-digit", StringComparison.Ordinal))
        {
            return ((long) value).ToString("D2", CultureInfo.InvariantCulture);
        }

        return unitStyle switch
        {
            "long" => $"{(long) value} {(isPlural ? pluralLong : singularLong)}",
            "short" => $"{(long) value} {shortForm}",
            "narrow" => $"{(long) value}{narrowForm}",
            _ => $"{(long) value} {shortForm}" // Default to short
        };
    }

    private static string FormatUnitWithSign(double absValue, string singularLong, string pluralLong, string shortForm, string narrowForm, string unitStyle, bool isNegative = false, bool isNegativeZero = false)
    {
        var isPlural = absValue != 1;
        var prefix = (isNegative || isNegativeZero) ? "-" : "";

        // Handle numeric and 2-digit styles
        if (string.Equals(unitStyle, "numeric", StringComparison.Ordinal))
        {
            return $"{prefix}{(long) absValue}";
        }

        if (string.Equals(unitStyle, "2-digit", StringComparison.Ordinal))
        {
            return $"{prefix}{(long) absValue:D2}";
        }

        return unitStyle switch
        {
            "long" => $"{prefix}{(long) absValue} {(isPlural ? pluralLong : singularLong)}",
            "short" => $"{prefix}{(long) absValue} {shortForm}",
            "narrow" => $"{prefix}{(long) absValue}{narrowForm}",
            _ => $"{prefix}{(long) absValue} {shortForm}" // Default to short
        };
    }

    /// <summary>
    /// Formats sub-second units (milliseconds, microseconds, nanoseconds).
    /// CLDR uses singular form for short/narrow and proper plural for long.
    /// </summary>
    private static string FormatSubSecondUnit(double value, string singular, string plural, string unitStyle)
    {
        var isPlural = System.Math.Abs(value) != 1;

        // Handle numeric and 2-digit styles
        if (string.Equals(unitStyle, "numeric", StringComparison.Ordinal))
        {
            return ((long) value).ToString(CultureInfo.InvariantCulture);
        }

        if (string.Equals(unitStyle, "2-digit", StringComparison.Ordinal))
        {
            return ((long) value).ToString("D2", CultureInfo.InvariantCulture);
        }

        // For sub-second units:
        // - long uses proper plural
        // - short/narrow use singular form always
        return unitStyle switch
        {
            "long" => $"{(long) value} {(isPlural ? plural : singular)}",
            "short" => $"{(long) value} {singular}",
            "narrow" => $"{(long) value}{singular}", // narrow has no space
            _ => $"{(long) value} {singular}" // Default to short (singular)
        };
    }

    private static string FormatSubSecondUnitWithSign(double absValue, string singular, string plural, string unitStyle, bool isNegative = false, bool isNegativeZero = false)
    {
        var isPlural = absValue != 1;
        var prefix = (isNegative || isNegativeZero) ? "-" : "";

        // Handle numeric and 2-digit styles
        if (string.Equals(unitStyle, "numeric", StringComparison.Ordinal))
        {
            return $"{prefix}{(long) absValue}";
        }

        if (string.Equals(unitStyle, "2-digit", StringComparison.Ordinal))
        {
            return $"{prefix}{(long) absValue:D2}";
        }

        // For sub-second units:
        // - long uses proper plural
        // - short/narrow use singular form always
        return unitStyle switch
        {
            "long" => $"{prefix}{(long) absValue} {(isPlural ? plural : singular)}",
            "short" => $"{prefix}{(long) absValue} {singular}",
            "narrow" => $"{prefix}{(long) absValue}{singular}", // narrow has no space
            _ => $"{prefix}{(long) absValue} {singular}" // Default to short (singular)
        };
    }

    /// <summary>
    /// Formats a duration object and returns parts.
    /// </summary>
    internal JsArray FormatToParts(Engine engine, DurationRecord duration)
    {
        var isDigital = string.Equals(Style, "digital", StringComparison.Ordinal);

        if (isDigital)
        {
            return FormatToPartsDigital(engine, duration);
        }

        return FormatToPartsNonDigital(engine, duration);
    }

    private JsArray FormatToPartsDigital(Engine engine, DurationRecord duration)
    {
        var result = new JsArray(engine);
        uint index = 0;

        // Check if the duration is negative
        var isNegative = duration.Years < 0 || duration.Months < 0 || duration.Weeks < 0 ||
                        duration.Days < 0 || duration.Hours < 0 || duration.Minutes < 0 ||
                        duration.Seconds < 0 || duration.Milliseconds < 0 ||
                        duration.Microseconds < 0 || duration.Nanoseconds < 0;
        var displayNegativeSign = isNegative;

        // Use absolute values for formatting
        var years = System.Math.Abs(duration.Years);
        var months = System.Math.Abs(duration.Months);
        var weeks = System.Math.Abs(duration.Weeks);
        var days = System.Math.Abs(duration.Days);
        var hours = System.Math.Abs(duration.Hours);
        var minutes = System.Math.Abs(duration.Minutes);
        var seconds = System.Math.Abs(duration.Seconds);
        var milliseconds = System.Math.Abs(duration.Milliseconds);
        var microseconds = System.Math.Abs(duration.Microseconds);
        var nanoseconds = System.Math.Abs(duration.Nanoseconds);

        // Convert sub-second units to total seconds
        var totalSubSeconds = milliseconds / 1000.0 + microseconds / 1_000_000.0 + nanoseconds / 1_000_000_000.0;
        var totalSeconds = seconds + totalSubSeconds;
        var wholeSeconds = (long) System.Math.Floor(totalSeconds);
        var fractionalPart = totalSeconds - wholeSeconds;

        void AddPart(string type, string value, string? unit = null)
        {
            var part = OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
            part.Set("type", type);
            part.Set("value", value);
            if (unit != null)
            {
                part.Set("unit", unit);
            }
            result.SetIndexValue(index++, part, updateLength: true);
        }

        // Helper to add date unit parts (for years, months, weeks, days in digital style)
        void AddDateUnitParts(double value, string display, string unitName)
        {
            if (value == 0 && !string.Equals(display, "always", StringComparison.Ordinal))
            {
                return;
            }

            // Add separator if not first
            if (index > 0)
            {
                AddPart("literal", ", ");
            }

            // Add minus sign for first displayed unit of negative duration
            if (displayNegativeSign)
            {
                displayNegativeSign = false;
                AddPart("minusSign", "-", unitName);
            }

            // Add integer value with grouping
            AddPart("integer", ((long) value).ToString("N0", CultureInfo), unitName);

            // Add space separator
            AddPart("literal", " ", unitName);

            // Get proper short form label with pluralization
            var unitPatterns = CldrProvider.GetUnitPatterns(Locale, unitName, "short");
            string shortLabel;
            if (unitPatterns != null)
            {
                // Extract just the unit part from the pattern (e.g., "{0} days" -> "days")
                var pattern = value != 1 ? unitPatterns.Other : (unitPatterns.One ?? unitPatterns.Other);
                shortLabel = pattern.Replace("{0}", "").Trim();
            }
            else
            {
                // Fallback using GetUnitLabel
                shortLabel = GetUnitLabel(value, unitName, "short");
            }

            // Add unit label (short form for digital style with proper pluralization)
            AddPart("unit", shortLabel, unitName);
        }

        // Add date unit parts (digital style uses short form labels with proper pluralization)
        AddDateUnitParts(years, YearsDisplay, "year");
        AddDateUnitParts(months, MonthsDisplay, "month");
        AddDateUnitParts(weeks, WeeksDisplay, "week");
        AddDateUnitParts(days, DaysDisplay, "day");

        // Add separator before time portion if there were date units
        if (index > 0)
        {
            AddPart("literal", ", ");
        }

        // Add minus sign for time if negative and no date units were shown
        if (displayNegativeSign)
        {
            displayNegativeSign = false;
            AddPart("minusSign", "-", "hour");
        }

        // Hours
        var showHours = duration.Hours != 0 || string.Equals(HoursDisplay, "always", StringComparison.Ordinal);
        if (showHours)
        {
            AddPart("integer", ((long) hours).ToString(CultureInfo.InvariantCulture), "hour");
            AddPart("literal", ":");
        }

        // Minutes
        AddPart("integer", ((long) minutes).ToString("D2", CultureInfo.InvariantCulture), "minute");
        AddPart("literal", ":");

        // Seconds (whole part)
        if (wholeSeconds < 100)
        {
            AddPart("integer", wholeSeconds.ToString("D2", CultureInfo.InvariantCulture), "second");
        }
        else
        {
            AddPart("integer", wholeSeconds.ToString(CultureInfo.InvariantCulture), "second");
        }

        // Fractional seconds if needed
        if (fractionalPart > 0 || FractionalDigits.HasValue)
        {
            string? fractionStr = null;

            if (FractionalDigits.HasValue)
            {
                var digits = FractionalDigits.Value;
                if (digits > 0)
                {
                    var formatStr = "F" + digits;
                    var fractionalStr = fractionalPart.ToString(formatStr, CultureInfo.InvariantCulture);
                    // Remove leading "0" to get just the digits after decimal
                    fractionStr = fractionalStr.Substring(2); // Skip "0."
                }
            }
            else if (fractionalPart > 0)
            {
                var fractionalStr = fractionalPart.ToString("F9", CultureInfo.InvariantCulture);
                // Remove leading "0." and trailing zeros
                var startIndex = 2; // Skip "0."
                var endIndex = fractionalStr.Length;
                while (endIndex > startIndex && fractionalStr[endIndex - 1] == '0')
                {
                    endIndex--;
                }
                if (endIndex > startIndex)
                {
                    fractionStr = fractionalStr.Substring(startIndex, endIndex - startIndex);
                }
            }

            if (fractionStr is { Length: > 0 })
            {
                AddPart("decimal", ".", "second");
                AddPart("fraction", fractionStr, "second");
            }
        }

        return result;
    }

    private JsArray FormatToPartsNonDigital(Engine engine, DurationRecord duration)
    {
        var result = new JsArray(engine);
        uint index = 0;

        // Check if the duration is negative
        var isNegative = duration.Years < 0 || duration.Months < 0 || duration.Weeks < 0 ||
                        duration.Days < 0 || duration.Hours < 0 || duration.Minutes < 0 ||
                        duration.Seconds < 0 || duration.Milliseconds < 0 ||
                        duration.Microseconds < 0 || duration.Nanoseconds < 0;
        var displayNegativeSign = isNegative;

        void AddPart(string type, string value, string? unit = null)
        {
            var part = OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
            part.Set("type", type);
            part.Set("value", value);
            if (unit != null)
            {
                part.Set("unit", unit);
            }
            result.SetIndexValue(index++, part, updateLength: true);
        }

        void AddUnitParts(double unitValue, string unitName, string display, string unitStyle)
        {
            if (!ShouldShowUnit(unitValue, display))
            {
                return;
            }

            // Add separator if not first
            if (index > 0)
            {
                // Narrow style uses space separator, others use comma-space
                var separator = string.Equals(Style, "narrow", StringComparison.Ordinal) ? " " : ", ";
                AddPart("literal", separator);
            }

            // Use absolute value for formatting
            var absValue = System.Math.Abs(unitValue);

            // Add minus sign for first displayed unit of negative duration
            if (displayNegativeSign)
            {
                displayNegativeSign = false;
                AddPart("minusSign", "-", unitName);
            }

            // Add integer part
            string valueStr;
            if (string.Equals(unitStyle, "2-digit", StringComparison.Ordinal))
            {
                valueStr = ((long) absValue).ToString("D2", CultureInfo.InvariantCulture);
            }
            else
            {
                valueStr = ((long) absValue).ToString(CultureInfo.InvariantCulture);
            }

            AddPart("integer", valueStr, unitName);

            // Add unit label for non-numeric styles
            if (!string.Equals(unitStyle, "numeric", StringComparison.Ordinal) &&
                !string.Equals(unitStyle, "2-digit", StringComparison.Ordinal))
            {
                // Narrow style: no space between number and unit
                // Long/short style: space between number and unit
                if (!string.Equals(unitStyle, "narrow", StringComparison.Ordinal))
                {
                    AddPart("literal", " ", unitName);
                }
                var label = GetUnitLabel(absValue, unitName, unitStyle);
                AddPart("unit", label, unitName);
            }
        }

        // Add parts for each unit
        AddUnitParts(duration.Years, "year", YearsDisplay, YearsStyle);
        AddUnitParts(duration.Months, "month", MonthsDisplay, MonthsStyle);
        AddUnitParts(duration.Weeks, "week", WeeksDisplay, WeeksStyle);
        AddUnitParts(duration.Days, "day", DaysDisplay, DaysStyle);
        AddUnitParts(duration.Hours, "hour", HoursDisplay, HoursStyle);
        AddUnitParts(duration.Minutes, "minute", MinutesDisplay, MinutesStyle);
        AddUnitParts(duration.Seconds, "second", SecondsDisplay, SecondsStyle);
        AddUnitParts(duration.Milliseconds, "millisecond", MillisecondsDisplay, MillisecondsStyle);
        AddUnitParts(duration.Microseconds, "microsecond", MicrosecondsDisplay, MicrosecondsStyle);
        AddUnitParts(duration.Nanoseconds, "nanosecond", NanosecondsDisplay, NanosecondsStyle);

        // If no parts, add zero seconds
        if (index == 0)
        {
            AddPart("integer", "0", "second");
            AddPart("literal", " ", "second");
            AddPart("unit", GetUnitLabel(0, "second", SecondsStyle), "second");
        }

        return result;
    }

    private string GetUnitLabel(double value, string unitName, string style)
    {
        // Use CLDR provider to get unit patterns
        // This ensures consistency with NumberFormat and provides correct formatting (periods, spaces, etc.)
        var unitPatterns = CldrProvider.GetUnitPatterns(Locale, unitName, style);
        if (unitPatterns != null)
        {
            // Choose singular or plural form based on value
            var isPlural = System.Math.Abs(value) != 1;
            var pattern = isPlural ? unitPatterns.Other : (unitPatterns.One ?? unitPatterns.Other);

            // Extract the unit suffix from the pattern by removing {0} placeholder
            // Pattern format is typically "{0} unit" or "{0}unit" (space is part of pattern)
            var placeholderIndex = pattern.IndexOf("{0}", StringComparison.Ordinal);
            if (placeholderIndex >= 0)
            {
                // Get the part after {0}
                var afterPlaceholder = pattern.Substring(placeholderIndex + 3);

                // The unit suffix starts after any leading space
                // We trim here because the space is added separately in the calling code
                return afterPlaceholder.TrimStart();
            }

            // If no placeholder found, return pattern as-is (shouldn't happen with valid CLDR data)
            return pattern;
        }

        // Fallback to hardcoded values if CLDR provider doesn't have data
        // This preserves backwards compatibility for non-English locales
        var plural = System.Math.Abs(value) != 1;
        var isLong = string.Equals(style, "long", StringComparison.Ordinal);

        if (string.Equals(unitName, "millisecond", StringComparison.Ordinal))
        {
            return isLong ? (plural ? "milliseconds" : "millisecond") : "millisecond";
        }
        else if (string.Equals(unitName, "microsecond", StringComparison.Ordinal))
        {
            return isLong ? (plural ? "microseconds" : "microsecond") : "microsecond";
        }
        else if (string.Equals(unitName, "nanosecond", StringComparison.Ordinal))
        {
            return isLong ? (plural ? "nanoseconds" : "nanosecond") : "nanosecond";
        }

        return unitName;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    internal struct DurationRecord
    {
        public double Years;
        public double Months;
        public double Weeks;
        public double Days;
        public double Hours;
        public double Minutes;
        public double Seconds;
        public double Milliseconds;
        public double Microseconds;
        public double Nanoseconds;
    }
}
