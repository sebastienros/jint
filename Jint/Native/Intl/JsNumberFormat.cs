using System.Globalization;
using System.Numerics;
using Jint.Native.Object;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-numberformat-objects
/// Represents an Intl.NumberFormat instance with locale-aware number formatting.
/// </summary>
internal sealed class JsNumberFormat : ObjectInstance
{
    internal JsNumberFormat(
        Engine engine,
        NumberFormatPrototype prototype,
        string locale,
        string style,
        string? currency,
        string? currencyDisplay,
        string? currencySign,
        string? unit,
        string? unitDisplay,
        string notation,
        string compactDisplay,
        string signDisplay,
        string useGrouping,
        int minimumIntegerDigits,
        int minimumFractionDigits,
        int maximumFractionDigits,
        int? minimumSignificantDigits,
        int? maximumSignificantDigits,
        string roundingMode,
        string roundingPriority,
        int roundingIncrement,
        string trailingZeroDisplay,
        NumberFormatInfo numberFormatInfo,
        CultureInfo cultureInfo) : base(engine)
    {
        _prototype = prototype;
        Locale = locale;
        Style = style;
        Currency = currency;
        CurrencyDisplay = currencyDisplay;
        CurrencySign = currencySign;
        Unit = unit;
        UnitDisplay = unitDisplay;
        Notation = notation;
        CompactDisplay = compactDisplay;
        SignDisplay = signDisplay;
        UseGrouping = useGrouping;
        MinimumIntegerDigits = minimumIntegerDigits;
        MinimumFractionDigits = minimumFractionDigits;
        MaximumFractionDigits = maximumFractionDigits;
        MinimumSignificantDigits = minimumSignificantDigits;
        MaximumSignificantDigits = maximumSignificantDigits;
        RoundingMode = roundingMode;
        RoundingPriority = roundingPriority;
        RoundingIncrement = roundingIncrement;
        TrailingZeroDisplay = trailingZeroDisplay;
        NumberFormatInfo = numberFormatInfo;
        CultureInfo = cultureInfo;
    }

    internal string Locale { get; }
    internal string Style { get; }
    internal string? Currency { get; }
    internal string? CurrencyDisplay { get; }
    internal string? CurrencySign { get; }
    internal string? Unit { get; }
    internal string? UnitDisplay { get; }
    internal string Notation { get; }
    internal string CompactDisplay { get; }
    internal string SignDisplay { get; }
    internal string UseGrouping { get; }
    internal int MinimumIntegerDigits { get; }

    /// <summary>
    /// Returns true if grouping should be applied based on UseGrouping setting.
    /// "false" => no grouping, otherwise apply grouping.
    /// </summary>
    private bool ShouldApplyGrouping(int integerDigits = 0)
    {
        if (string.Equals(UseGrouping, "false", StringComparison.Ordinal))
        {
            return false;
        }
        if (string.Equals(UseGrouping, "min2", StringComparison.Ordinal))
        {
            // "min2" means use grouping only if there are at least 2 digits in the most significant group
            return integerDigits >= 5; // e.g., 10,000 not 1,000
        }
        // "auto", "always", "true" all enable grouping
        return true;
    }
    internal int MinimumFractionDigits { get; }
    internal int MaximumFractionDigits { get; }
    internal int? MinimumSignificantDigits { get; }
    internal int? MaximumSignificantDigits { get; }
    internal string RoundingMode { get; }
    internal string RoundingPriority { get; }
    internal int RoundingIncrement { get; }
    internal string TrailingZeroDisplay { get; }
    internal NumberFormatInfo NumberFormatInfo { get; }
    internal CultureInfo CultureInfo { get; }

    /// <summary>
    /// Formats a number according to the formatter's locale and options.
    /// </summary>
    internal string Format(double value)
    {
        // Handle notation first (scientific, engineering, compact)
        if (!string.Equals(Notation, "standard", StringComparison.Ordinal))
        {
            return FormatWithNotation(value);
        }

        return Style switch
        {
            "currency" => FormatCurrency(value),
            "percent" => FormatPercent(value),
            "unit" => FormatUnit(value),
            _ => FormatDecimal(value)
        };
    }

    private string FormatWithNotation(double value)
    {
        if (string.Equals(Notation, "scientific", StringComparison.Ordinal))
        {
            return FormatScientific(value);
        }

        if (string.Equals(Notation, "engineering", StringComparison.Ordinal))
        {
            return FormatEngineering(value);
        }

        if (string.Equals(Notation, "compact", StringComparison.Ordinal))
        {
            return FormatCompact(value);
        }

        return FormatDecimal(value);
    }

    private string FormatScientific(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value == 0)
        {
            return FormatDecimal(value);
        }

        // Get the exponent
        var exponent = (int) System.Math.Floor(System.Math.Log10(System.Math.Abs(value)));
        var mantissa = value / System.Math.Pow(10, exponent);

        // Round the mantissa
        mantissa = ApplyRounding(mantissa, MaximumFractionDigits);

        // Format mantissa
        var mantissaFormat = MaximumFractionDigits > 0
            ? "0." + new string('0', MinimumFractionDigits) + new string('#', MaximumFractionDigits - MinimumFractionDigits)
            : "0";
        var mantissaStr = mantissa.ToString(mantissaFormat, NumberFormatInfo);

        // Format exponent with sign
        var expSign = exponent >= 0 ? "+" : "";
        return $"{mantissaStr}E{expSign}{exponent}";
    }

    private string FormatEngineering(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value == 0)
        {
            return FormatDecimal(value);
        }

        // Engineering notation uses exponents that are multiples of 3
        var exponent = (int) System.Math.Floor(System.Math.Log10(System.Math.Abs(value)));
        var engineeringExponent = (int) (System.Math.Floor(exponent / 3.0) * 3);
        var mantissa = value / System.Math.Pow(10, engineeringExponent);

        // Round the mantissa
        mantissa = ApplyRounding(mantissa, MaximumFractionDigits);

        // Format mantissa
        var mantissaFormat = MaximumFractionDigits > 0
            ? "0." + new string('0', MinimumFractionDigits) + new string('#', MaximumFractionDigits - MinimumFractionDigits)
            : "0";
        var mantissaStr = mantissa.ToString(mantissaFormat, NumberFormatInfo);

        // Format exponent with sign
        var expSign = engineeringExponent >= 0 ? "+" : "";
        return $"{mantissaStr}E{expSign}{engineeringExponent}";
    }

    private string FormatCompact(double value)
    {
        // Simplified compact notation - just use K, M, B, T suffixes
        var absValue = System.Math.Abs(value);
        string suffix;
        double divisor;

        if (absValue >= 1_000_000_000_000)
        {
            suffix = "T";
            divisor = 1_000_000_000_000;
        }
        else if (absValue >= 1_000_000_000)
        {
            suffix = "B";
            divisor = 1_000_000_000;
        }
        else if (absValue >= 1_000_000)
        {
            suffix = "M";
            divisor = 1_000_000;
        }
        else if (absValue >= 1_000)
        {
            suffix = "K";
            divisor = 1_000;
        }
        else
        {
            return FormatDecimal(value);
        }

        var compactValue = value / divisor;
        compactValue = ApplyRounding(compactValue, MaximumFractionDigits);
        var formatted = compactValue.ToString("0.##", NumberFormatInfo);
        return formatted + suffix;
    }

    /// <summary>
    /// Applies rounding to a value based on the RoundingMode and RoundingIncrement settings.
    /// </summary>
    private double ApplyRounding(double value, int decimalPlaces)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return value;
        }

        var multiplier = System.Math.Pow(10, decimalPlaces);
        var scaled = value * multiplier;

        // Apply rounding increment if specified
        if (RoundingIncrement > 1)
        {
            // Round to nearest multiple of RoundingIncrement
            scaled = scaled / RoundingIncrement;
            scaled = ApplyRoundingMode(scaled, value >= 0);
            scaled = scaled * RoundingIncrement;
        }
        else
        {
            scaled = ApplyRoundingMode(scaled, value >= 0);
        }

        return scaled / multiplier;
    }

    private double ApplyRoundingMode(double scaled, bool isPositive)
    {
        return RoundingMode switch
        {
            "ceil" => System.Math.Ceiling(scaled),
            "floor" => System.Math.Floor(scaled),
            "expand" => isPositive ? System.Math.Ceiling(scaled) : System.Math.Floor(scaled),
            "trunc" => System.Math.Truncate(scaled),
            "halfCeil" => RoundHalfCeil(scaled),
            "halfFloor" => RoundHalfFloor(scaled),
            "halfExpand" => System.Math.Round(scaled, MidpointRounding.AwayFromZero),
            "halfTrunc" => RoundHalfTrunc(scaled),
            "halfEven" => System.Math.Round(scaled, MidpointRounding.ToEven),
            _ => System.Math.Round(scaled, MidpointRounding.AwayFromZero) // Default: halfExpand
        };
    }

    private static double RoundHalfCeil(double value)
    {
        var floor = System.Math.Floor(value);
        var fraction = value - floor;
        if (fraction > 0.5 || (fraction == 0.5 && value >= 0))
        {
            return floor + 1;
        }
        return floor;
    }

    private static double RoundHalfFloor(double value)
    {
        var floor = System.Math.Floor(value);
        var fraction = value - floor;
        if (fraction > 0.5 || (fraction == 0.5 && value < 0))
        {
            return floor + 1;
        }
        return floor;
    }

    private static double RoundHalfTrunc(double value)
    {
        var floor = System.Math.Floor(value);
        var fraction = value - floor;
        if (fraction > 0.5)
        {
            return floor + 1;
        }
        if (fraction < 0.5)
        {
            return floor;
        }
        // fraction == 0.5, round toward zero
        return value >= 0 ? floor : floor + 1;
    }

    /// <summary>
    /// Formats a BigInteger according to the formatter's locale and options.
    /// </summary>
    internal string Format(BigInteger value)
    {
        // If significant digits are specified, convert to double for formatting
        // This may lose precision for very large numbers but handles significantDigits correctly
        if (MinimumSignificantDigits.HasValue || MaximumSignificantDigits.HasValue)
        {
            return Format((double) value);
        }

        return Style switch
        {
            "currency" => FormatCurrencyBigInt(value),
            "percent" => FormatPercentBigInt(value),
            "unit" => FormatUnitBigInt(value),
            _ => FormatDecimalBigInt(value)
        };
    }

    private string FormatDecimalBigInt(BigInteger value)
    {
        // BigInteger formatting - use grouping if needed
        var formatted = value.ToString("R", CultureInfo.InvariantCulture);
        var digitCount = value < 0 ? formatted.Length - 1 : formatted.Length;

        if (ShouldApplyGrouping(digitCount))
        {
            // Apply grouping manually for BigInteger
            formatted = ApplyGrouping(formatted, value < 0);
        }

        // Apply minimum integer digits padding
        if (MinimumIntegerDigits > 1)
        {
            var isNegative = value < 0;
            var digits = isNegative ? formatted.Substring(1) : formatted;
            if (digits.Length < MinimumIntegerDigits)
            {
                digits = digits.PadLeft(MinimumIntegerDigits, '0');
                formatted = isNegative ? "-" + digits : digits;
            }
        }

        // Handle minimumFractionDigits for BigInt (add decimal zeros)
        if (MinimumFractionDigits > 0)
        {
            formatted += NumberFormatInfo.NumberDecimalSeparator + new string('0', MinimumFractionDigits);
        }

        return formatted;
    }

    private string ApplyGrouping(string number, bool isNegative)
    {
        var startIndex = isNegative ? 1 : 0;
        var integerPart = number.Substring(startIndex);

        if (integerPart.Length <= 3)
        {
            return number;
        }

        var groupSeparator = NumberFormatInfo.NumberGroupSeparator;
        var result = new System.Text.StringBuilder();

        var count = 0;
        for (var i = integerPart.Length - 1; i >= 0; i--)
        {
            if (count > 0 && count % 3 == 0)
            {
                result.Insert(0, groupSeparator);
            }
            result.Insert(0, integerPart[i]);
            count++;
        }

        if (isNegative)
        {
            result.Insert(0, '-');
        }

        return result.ToString();
    }

    private string FormatCurrencyBigInt(BigInteger value)
    {
        // For currency, we need to handle the formatting manually for BigInt
        var formatted = FormatDecimalBigInt(value);
        var symbol = NumberFormatInfo.CurrencySymbol;
        return symbol + formatted;
    }

    private string FormatPercentBigInt(BigInteger value)
    {
        // Percent multiplies by 100
        var percentValue = value * 100;
        return FormatDecimalBigInt(percentValue) + NumberFormatInfo.PercentSymbol;
    }

    private string FormatUnitBigInt(BigInteger value)
    {
        var formattedNumber = FormatDecimalBigInt(value);
        var unitDisplay = UnitDisplay ?? "short";
        var unitStr = Unit ?? "";
        var unitSuffix = GetUnitSuffix(unitStr, unitDisplay, (double) value);
        return $"{formattedNumber} {unitSuffix}";
    }

    private string FormatDecimal(double value)
    {
        // Handle significant digits and roundingPriority
        if (MinimumSignificantDigits.HasValue || MaximumSignificantDigits.HasValue)
        {
            return FormatWithSignificantDigits(value);
        }

        // Apply rounding based on MaximumFractionDigits
        value = ApplyRounding(value, MaximumFractionDigits);

        // Build custom format string that respects min integer digits, min/max fraction digits
        // e.g., MinimumIntegerDigits=3, MinimumFractionDigits=1, MaximumFractionDigits=3 -> "000.0##"

        // Build integer part with minimum integer digits
        // Calculate integer digits for min2 grouping check
        var integerDigits = (int) (value == 0 ? 1 : System.Math.Floor(System.Math.Log10(System.Math.Abs(value))) + 1);

        string integerPart;
        if (ShouldApplyGrouping(integerDigits))
        {
            // For grouping, we need at least 4 characters for the group separator pattern
            // e.g., "#,##0" for 1 min digit, "#,#00" for 2, "#,000" for 3, "0,000" for 4+
            if (MinimumIntegerDigits <= 1)
            {
                integerPart = "#,##0";
            }
            else if (MinimumIntegerDigits == 2)
            {
                integerPart = "#,#00";
            }
            else if (MinimumIntegerDigits == 3)
            {
                integerPart = "#,000";
            }
            else
            {
                // For 4+, we need additional leading zeros
                var extraZeros = new string('0', MinimumIntegerDigits - 3);
                integerPart = extraZeros + ",000";
            }
        }
        else
        {
            integerPart = new string('0', MinimumIntegerDigits);
        }

        string format;
        if (MaximumFractionDigits == 0)
        {
            format = integerPart;
        }
        else
        {
            var fractionPart = new string('0', MinimumFractionDigits);
            if (MaximumFractionDigits > MinimumFractionDigits)
            {
                fractionPart += new string('#', MaximumFractionDigits - MinimumFractionDigits);
            }
            format = $"{integerPart}.{fractionPart}";
        }

        return value.ToString(format, NumberFormatInfo);
    }

    private string FormatWithSignificantDigits(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return value.ToString(NumberFormatInfo);
        }

        var minSigDigits = MinimumSignificantDigits ?? 1;
        var maxSigDigits = MaximumSignificantDigits ?? 21;

        // Format based on roundingPriority
        if (string.Equals(RoundingPriority, "lessPrecision", StringComparison.Ordinal))
        {
            // Use whichever gives fewer digits
            var fracDigitsResult = FormatWithFractionDigits(value);
            var sigDigitsResult = FormatToSignificantDigits(value, minSigDigits, maxSigDigits);

            // Compare which result is "less precise" (shorter or with fewer significant figures)
            var fracDigitsCount = CountSignificantDigits(fracDigitsResult);
            var sigDigitsCount = CountSignificantDigits(sigDigitsResult);

            return sigDigitsCount <= fracDigitsCount ? sigDigitsResult : fracDigitsResult;
        }

        if (string.Equals(RoundingPriority, "morePrecision", StringComparison.Ordinal))
        {
            // Use whichever gives more digits
            var fracDigitsResult = FormatWithFractionDigits(value);
            var sigDigitsResult = FormatToSignificantDigits(value, minSigDigits, maxSigDigits);

            var fracDigitsCount = CountSignificantDigits(fracDigitsResult);
            var sigDigitsCount = CountSignificantDigits(sigDigitsResult);

            return sigDigitsCount >= fracDigitsCount ? sigDigitsResult : fracDigitsResult;
        }

        // Default: use significant digits
        return FormatToSignificantDigits(value, minSigDigits, maxSigDigits);
    }

    private string FormatWithFractionDigits(double value)
    {
        value = ApplyRounding(value, MaximumFractionDigits);
        var integerDigits = (int) (value == 0 ? 1 : System.Math.Floor(System.Math.Log10(System.Math.Abs(value))) + 1);

        string integerPart;
        if (ShouldApplyGrouping(integerDigits))
        {
            integerPart = MinimumIntegerDigits <= 1 ? "#,##0" :
                MinimumIntegerDigits == 2 ? "#,#00" :
                MinimumIntegerDigits == 3 ? "#,000" :
                new string('0', MinimumIntegerDigits - 3) + ",000";
        }
        else
        {
            integerPart = new string('0', MinimumIntegerDigits);
        }

        string format;
        if (MaximumFractionDigits == 0)
        {
            format = integerPart;
        }
        else
        {
            var fractionPart = new string('0', MinimumFractionDigits);
            if (MaximumFractionDigits > MinimumFractionDigits)
            {
                fractionPart += new string('#', MaximumFractionDigits - MinimumFractionDigits);
            }
            format = $"{integerPart}.{fractionPart}";
        }

        return value.ToString(format, NumberFormatInfo);
    }

    private string FormatToSignificantDigits(double value, int minSigDigits, int maxSigDigits)
    {
        if (value == 0)
        {
            return minSigDigits > 1 ? "0." + new string('0', minSigDigits - 1) : "0";
        }

        var isNegative = value < 0;
        var absValue = System.Math.Abs(value);

        // Get the order of magnitude
        var magnitude = (int) System.Math.Floor(System.Math.Log10(absValue));
        var decimalPlaces = maxSigDigits - magnitude - 1;

        // Round to the required number of significant digits
        double rounded;
        if (decimalPlaces >= 0)
        {
            rounded = ApplyRounding(absValue, decimalPlaces);
        }
        else
        {
            // Need to round to whole numbers or higher
            var divisor = System.Math.Pow(10, -decimalPlaces);
            rounded = ApplyRounding(absValue / divisor, 0) * divisor;
        }

        // Format the result
        string result;
        if (decimalPlaces <= 0)
        {
            result = rounded.ToString("F0", CultureInfo.InvariantCulture);
        }
        else
        {
            var formatSpec = "F" + decimalPlaces;
            result = rounded.ToString(formatSpec, CultureInfo.InvariantCulture);
        }

        // Ensure minimum significant digits (add trailing zeros if needed)
        var currentSigDigits = CountSignificantDigits(result);
        if (currentSigDigits < minSigDigits)
        {
            var zerosToAdd = minSigDigits - currentSigDigits;
            if (!result.Contains('.'))
            {
                result += "." + new string('0', zerosToAdd);
            }
            else
            {
                result += new string('0', zerosToAdd);
            }
        }

        // Apply grouping and locale-specific separators
        var decimalIdx = result.IndexOf('.');
        var intPartForGrouping = decimalIdx >= 0 ? result.Substring(0, decimalIdx) : result;
        var fracPart = decimalIdx >= 0 ? result.Substring(decimalIdx + 1) : null; // Exclude the '.'
        var intDigitCount = intPartForGrouping.Length;

        if (ShouldApplyGrouping(intDigitCount))
        {
            intPartForGrouping = ApplyGroupingToString(intPartForGrouping);
        }

        // Build result with locale-specific decimal separator
        if (fracPart != null)
        {
            result = intPartForGrouping + NumberFormatInfo.NumberDecimalSeparator + fracPart;
        }
        else
        {
            result = intPartForGrouping;
        }

        return isNegative ? "-" + result : result;
    }

    private string ApplyGroupingToString(string integerPart)
    {
        if (integerPart.Length <= 3)
        {
            return integerPart;
        }

        var groupSeparator = NumberFormatInfo.NumberGroupSeparator;
        var sb = new System.Text.StringBuilder();
        var count = 0;

        for (var i = integerPart.Length - 1; i >= 0; i--)
        {
            if (count > 0 && count % 3 == 0)
            {
                sb.Insert(0, groupSeparator);
            }
            sb.Insert(0, integerPart[i]);
            count++;
        }

        return sb.ToString();
    }

    private static int CountSignificantDigits(string formatted)
    {
        var count = 0;
        var foundNonZero = false;
        var inFraction = false;

        foreach (var c in formatted)
        {
            if (c == '.' || c == ',')
            {
                if (c == '.')
                {
                    inFraction = true;
                }
                continue;
            }

            if (c == '-')
            {
                continue;
            }

            if (char.IsDigit(c))
            {
                if (c != '0')
                {
                    foundNonZero = true;
                }

                if (foundNonZero)
                {
                    count++;
                }
                else if (inFraction && c == '0')
                {
                    // Leading zeros after decimal don't count
                    continue;
                }
            }
        }

        return count == 0 ? 1 : count; // At least 1 for "0"
    }

    private string FormatCurrency(double value)
    {
        return value.ToString("C", NumberFormatInfo);
    }

    private string FormatPercent(double value)
    {
        // Percent multiplies by 100
        var percentValue = value * 100;

        // Handle significant digits for percent formatting
        if (MinimumSignificantDigits.HasValue || MaximumSignificantDigits.HasValue)
        {
            var formatted = FormatWithSignificantDigits(percentValue);
            // Apply locale-specific percent pattern
            return ApplyPercentPattern(formatted, percentValue >= 0);
        }

        return value.ToString("P", NumberFormatInfo);
    }

    /// <summary>
    /// Applies locale-specific percent formatting pattern.
    /// Uses non-breaking space (U+00A0) for spacing as per CLDR.
    /// </summary>
    private string ApplyPercentPattern(string formattedNumber, bool isPositive)
    {
        var symbol = NumberFormatInfo.PercentSymbol;
        const string Nbsp = "\u00A0"; // Non-breaking space
        int pattern;

        if (isPositive)
        {
            pattern = NumberFormatInfo.PercentPositivePattern;
            // PercentPositivePattern values:
            // 0: n %  (space between)
            // 1: n%   (no space)
            // 2: %n   (symbol before, no space)
            // 3: % n  (symbol before, space)
            return pattern switch
            {
                0 => formattedNumber + Nbsp + symbol,
                1 => formattedNumber + symbol,
                2 => symbol + formattedNumber,
                3 => symbol + Nbsp + formattedNumber,
                _ => formattedNumber + symbol
            };
        }
        else
        {
            pattern = NumberFormatInfo.PercentNegativePattern;
            var absNumber = formattedNumber.Length > 0 && formattedNumber[0] == '-' ? formattedNumber.Substring(1) : formattedNumber;
            // PercentNegativePattern values vary by locale
            return pattern switch
            {
                0 => "-" + absNumber + Nbsp + symbol,
                1 => "-" + absNumber + symbol,
                2 => "-" + symbol + absNumber,
                3 => symbol + "-" + absNumber,
                4 => symbol + absNumber + "-",
                5 => absNumber + "-" + symbol,
                6 => absNumber + symbol + "-",
                7 => "-" + symbol + Nbsp + absNumber,
                8 => absNumber + Nbsp + symbol + "-",
                9 => symbol + Nbsp + absNumber + "-",
                10 => symbol + Nbsp + "-" + absNumber,
                11 => absNumber + "-" + Nbsp + symbol,
                _ => "-" + absNumber + symbol
            };
        }
    }

    private string FormatUnit(double value)
    {
        // Simple unit formatting - .NET doesn't have built-in unit formatting
        var formattedNumber = FormatDecimal(value);
        var unitDisplay = UnitDisplay ?? "short";
        var unitStr = Unit ?? "";

        // Get unit abbreviation based on display
        var unitSuffix = GetUnitSuffix(unitStr, unitDisplay, value);
        return $"{formattedNumber} {unitSuffix}";
    }

    private static string GetUnitSuffix(string unit, string display, double value)
    {
        // This is a simplified version - full implementation would use CLDR data
        var isPlural = System.Math.Abs(value) != 1;
        var isLong = string.Equals(display, "long", StringComparison.Ordinal);
        var isNarrow = string.Equals(display, "narrow", StringComparison.Ordinal);

        if (isLong)
        {
            return unit switch
            {
                "meter" => isPlural ? "meters" : "meter",
                "kilometer" => isPlural ? "kilometers" : "kilometer",
                "centimeter" => isPlural ? "centimeters" : "centimeter",
                "millimeter" => isPlural ? "millimeters" : "millimeter",
                "mile" => isPlural ? "miles" : "mile",
                "foot" => isPlural ? "feet" : "foot",
                "inch" => isPlural ? "inches" : "inch",
                "yard" => isPlural ? "yards" : "yard",
                "second" => isPlural ? "seconds" : "second",
                "minute" => isPlural ? "minutes" : "minute",
                "hour" => isPlural ? "hours" : "hour",
                "day" => isPlural ? "days" : "day",
                "week" => isPlural ? "weeks" : "week",
                "month" => isPlural ? "months" : "month",
                "year" => isPlural ? "years" : "year",
                "gram" => isPlural ? "grams" : "gram",
                "kilogram" => isPlural ? "kilograms" : "kilogram",
                "pound" => isPlural ? "pounds" : "pound",
                "ounce" => isPlural ? "ounces" : "ounce",
                "liter" => isPlural ? "liters" : "liter",
                "milliliter" => isPlural ? "milliliters" : "milliliter",
                "gallon" => isPlural ? "gallons" : "gallon",
                "byte" => isPlural ? "bytes" : "byte",
                "kilobyte" => isPlural ? "kilobytes" : "kilobyte",
                "megabyte" => isPlural ? "megabytes" : "megabyte",
                "gigabyte" => isPlural ? "gigabytes" : "gigabyte",
                "celsius" => "degrees Celsius",
                "fahrenheit" => "degrees Fahrenheit",
                "percent" => "percent",
                _ => unit
            };
        }

        // Short/narrow display
        return unit switch
        {
            "meter" => "m",
            "kilometer" => "km",
            "centimeter" => "cm",
            "millimeter" => "mm",
            "mile" => "mi",
            "foot" => "ft",
            "inch" => "in",
            "yard" => "yd",
            "second" => isNarrow ? "s" : "sec",
            "minute" => isNarrow ? "m" : "min",
            "hour" => isNarrow ? "h" : "hr",
            "day" => isNarrow ? "d" : "day",
            "week" => isNarrow ? "w" : "wk",
            "month" => isNarrow ? "M" : "mo",
            "year" => isNarrow ? "y" : "yr",
            "gram" => "g",
            "kilogram" => "kg",
            "pound" => "lb",
            "ounce" => "oz",
            "liter" => isNarrow ? "l" : "L",
            "milliliter" => "mL",
            "gallon" => "gal",
            "byte" => "B",
            "kilobyte" => "kB",
            "megabyte" => "MB",
            "gigabyte" => "GB",
            "celsius" => "°C",
            "fahrenheit" => "°F",
            "percent" => "%",
            _ => unit
        };
    }
}
