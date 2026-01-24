using System.Globalization;
using System.Numerics;
using Jint.Native.Object;

namespace Jint.Native.Intl;

/// <summary>
/// Represents a part of a formatted number for formatToParts.
/// </summary>
internal readonly struct NumberFormatPart
{
    public string Type { get; }
    public string Value { get; }

    public NumberFormatPart(string type, string value)
    {
        Type = type;
        Value = value;
    }
}

/// <summary>
/// https://tc39.es/ecma402/#sec-numberformat-objects
/// Represents an Intl.NumberFormat instance with locale-aware number formatting.
/// </summary>
internal sealed class JsNumberFormat : ObjectInstance
{
    internal JsNumberFormat(
        Engine engine,
        ObjectInstance prototype,
        string locale,
        string numberingSystem,
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
        bool minimumSignificantDigitsExplicit,
        bool maximumSignificantDigitsExplicit,
        string roundingMode,
        string roundingPriority,
        int roundingIncrement,
        string trailingZeroDisplay,
        NumberFormatInfo numberFormatInfo,
        CultureInfo cultureInfo) : base(engine)
    {
        _prototype = prototype;
        Locale = locale;
        NumberingSystem = numberingSystem;
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
        MinimumSignificantDigitsExplicit = minimumSignificantDigitsExplicit;
        MaximumSignificantDigitsExplicit = maximumSignificantDigitsExplicit;
        RoundingMode = roundingMode;
        RoundingPriority = roundingPriority;
        RoundingIncrement = roundingIncrement;
        TrailingZeroDisplay = trailingZeroDisplay;
        NumberFormatInfo = numberFormatInfo;
        CultureInfo = cultureInfo;
    }

    internal string Locale { get; }
    internal string NumberingSystem { get; }
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
        // Per ECMA-402 ES2023: "auto" is locale-dependent
        // Polish and similar locales use "min2" behavior, others use "always" behavior
        if (string.Equals(UseGrouping, "auto", StringComparison.Ordinal))
        {
            // Check if locale uses min2 grouping behavior
            if (UsesMin2GroupingForAuto(Locale))
            {
                return integerDigits >= 5; // min2 behavior
            }
            // Default: always use grouping
            return true;
        }
        // "always", "true" enable grouping
        return true;
    }

    /// <summary>
    /// Determines if a locale uses "min2" grouping behavior for "auto".
    /// Based on CLDR data and Test262 expectations.
    /// </summary>
    private static bool UsesMin2GroupingForAuto(string locale)
    {
        if (string.IsNullOrEmpty(locale))
        {
            return false;
        }

        // Extract language code (e.g., "pl" from "pl-PL")
        var language = locale;
        var dashIndex = locale.IndexOf('-');
        if (dashIndex > 0)
        {
            language = locale.Substring(0, dashIndex);
        }

        // Polish and certain other locales use min2 behavior for "auto"
        // This list is based on CLDR data and Test262 test expectations
        return language.Equals("pl", StringComparison.OrdinalIgnoreCase);
    }
    internal int MinimumFractionDigits { get; }
    internal int MaximumFractionDigits { get; }
    internal int? MinimumSignificantDigits { get; }
    internal int? MaximumSignificantDigits { get; }
    internal bool MinimumSignificantDigitsExplicit { get; }
    internal bool MaximumSignificantDigitsExplicit { get; }
    internal string RoundingMode { get; }
    internal string RoundingPriority { get; }
    internal int RoundingIncrement { get; }
    internal string TrailingZeroDisplay { get; }
    internal NumberFormatInfo NumberFormatInfo { get; }
    internal CultureInfo CultureInfo { get; }

    /// <summary>
    /// Gets the CLDR provider from engine options.
    /// </summary>
    private ICldrProvider CldrProvider => _engine.Options.Intl.CldrProvider;

    /// <summary>
    /// Formats a number according to the formatter's locale and options.
    /// </summary>
    internal string Format(double value)
    {
        var result = FormatCore(value);

        // Apply numbering system digit transliteration
        return Data.NumberingSystemData.TransliterateDigits(result, NumberingSystem);
    }

    private string FormatCore(double value)
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

        // Format exponent (no plus sign for positive exponents per spec)
        return $"{mantissaStr}E{exponent}";
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

        // Format exponent (no plus sign for positive exponents per spec)
        return $"{mantissaStr}E{engineeringExponent}";
    }

    private string FormatCompact(double value)
    {
        // Handle special values first
        if (double.IsNaN(value))
        {
            return NumberFormatInfo.NaNSymbol;
        }

        if (double.IsPositiveInfinity(value))
        {
            return NumberFormatInfo.PositiveInfinitySymbol;
        }

        if (double.IsNegativeInfinity(value))
        {
            return NumberFormatInfo.NegativeSign + NumberFormatInfo.PositiveInfinitySymbol;
        }

        var absValue = System.Math.Abs(value);
        var isNegative = value < 0;
        var isLong = string.Equals(CompactDisplay, "long", StringComparison.Ordinal);

        // Get locale-specific compact patterns
        var patterns = Data.CompactPatterns.GetPatterns(Locale);
        var threshold = patterns.GetThreshold(isLong);

        // For values below threshold, use regular decimal formatting
        if (absValue < threshold)
        {
            return FormatCompactSmallValue(value, absValue, isNegative);
        }

        // Determine the appropriate compact unit
        string suffix;
        string longSuffix;
        double divisor;

        // For East Asian languages (ja, ko, zh), use different divisors
        var divisorMillion = patterns.DivisorMillion;
        var divisorBillion = patterns.DivisorBillion;

        if (absValue >= 1_000_000_000_000)
        {
            suffix = patterns.ShortTrillion;
            longSuffix = patterns.LongTrillion;
            divisor = 1_000_000_000_000;
        }
        else if (absValue >= divisorBillion)
        {
            suffix = patterns.ShortBillion;
            longSuffix = patterns.LongBillion;
            divisor = divisorBillion;
        }
        else if (absValue >= divisorMillion)
        {
            suffix = patterns.ShortMillion;
            longSuffix = patterns.LongMillion;
            divisor = divisorMillion;
        }
        else if (absValue >= 1000)
        {
            var thousandSuffix = isLong ? patterns.LongThousand : patterns.ShortThousand;
            if (string.IsNullOrEmpty(thousandSuffix))
            {
                // No thousand suffix for this locale/mode, use small value formatting
                return FormatCompactSmallValue(value, absValue, isNegative);
            }
            suffix = patterns.ShortThousand;
            longSuffix = patterns.LongThousand;
            divisor = 1000;
        }
        else
        {
            // Below compact threshold but above small value handling
            return FormatCompactSmallValue(value, absValue, isNegative);
        }

        // If suffix is empty, don't use compact notation
        var actualSuffix = isLong ? longSuffix : suffix;
        if (string.IsNullOrEmpty(actualSuffix))
        {
            return FormatCompactSmallValue(value, absValue, isNegative);
        }

        var compactValue = absValue / divisor;

        // Use 2 significant figures for compact notation
        var compactMagnitude = compactValue >= 1 ? (int) System.Math.Floor(System.Math.Log10(compactValue)) : 0;
        var compactDecimalPlaces = 1 - compactMagnitude; // For 2 sig figs
        if (compactDecimalPlaces < 0)
        {
            compactDecimalPlaces = 0;
        }

        compactValue = ApplyRounding(compactValue, compactDecimalPlaces);

        // Format the number part using locale's decimal separator
        string compactFormatted;
        if (compactDecimalPlaces > 0 && compactValue != System.Math.Floor(compactValue))
        {
            compactFormatted = compactValue.ToString("F" + compactDecimalPlaces, NumberFormatInfo);
            // Trim trailing zeros after decimal
            var decSep = NumberFormatInfo.NumberDecimalSeparator;
            if (compactFormatted.Contains(decSep))
            {
                compactFormatted = compactFormatted.TrimEnd('0');
                if (compactFormatted.EndsWith(decSep, StringComparison.Ordinal))
                {
                    compactFormatted = compactFormatted.Substring(0, compactFormatted.Length - decSep.Length);
                }
            }
        }
        else
        {
            compactFormatted = ((long) compactValue).ToString(NumberFormatInfo);
        }

        // Build result with appropriate spacing
        string result;
        if (isLong)
        {
            // Long format: spacing depends on locale
            if (patterns.LongSpace)
            {
                result = compactFormatted + " " + actualSuffix;
            }
            else
            {
                result = compactFormatted + actualSuffix;
            }
        }
        else
        {
            // Short format: spacing depends on locale
            if (patterns.ShortSpace)
            {
                result = compactFormatted + "\u00a0" + actualSuffix;
            }
            else
            {
                result = compactFormatted + actualSuffix;
            }
        }

        return isNegative ? NumberFormatInfo.NegativeSign + result : result;
    }

    private string FormatCompactSmallValue(double value, double absValue, bool isNegative)
    {
        if (absValue == 0)
        {
            return "0";
        }

        // For values >= 1, format with locale's grouping/decimal separators
        if (absValue >= 1)
        {
            var magnitude = (int) System.Math.Floor(System.Math.Log10(absValue));

            // Determine decimal places based on magnitude
            int decimalPlaces;
            if (magnitude >= 4)
            {
                // 10000+ - no decimals, use grouping
                decimalPlaces = 0;
            }
            else if (magnitude >= 2)
            {
                // 100-9999 - no decimals
                decimalPlaces = 0;
            }
            else
            {
                // 1-99 - use 2 significant figures
                decimalPlaces = 1 - magnitude;
            }

            var rounded = ApplyRounding(absValue, decimalPlaces);

            string formatted;
            if (decimalPlaces <= 0)
            {
                // Use grouping only for numbers with 5+ digits (10000+)
                if (rounded >= 10000)
                {
                    formatted = rounded.ToString("#,##0", NumberFormatInfo);
                }
                else
                {
                    formatted = ((long) rounded).ToString(CultureInfo.InvariantCulture);
                }
            }
            else
            {
                formatted = rounded.ToString("F" + decimalPlaces, NumberFormatInfo);
                // Trim trailing zeros
                var decSep = NumberFormatInfo.NumberDecimalSeparator;
                if (formatted.Contains(decSep))
                {
                    formatted = formatted.TrimEnd('0');
                    if (formatted.EndsWith(decSep, StringComparison.Ordinal))
                    {
                        formatted = formatted.Substring(0, formatted.Length - decSep.Length);
                    }
                }
            }

            return isNegative ? NumberFormatInfo.NegativeSign + formatted : formatted;
        }

        // For values < 1
        var smallMagnitude = (int) System.Math.Floor(System.Math.Log10(absValue));
        var smallDecimalPlaces = 1 - smallMagnitude; // 2 sig figs

        var smallRounded = ApplyRounding(absValue, smallDecimalPlaces);
        var smallFormatted = smallRounded.ToString("F" + smallDecimalPlaces, NumberFormatInfo);

        // Trim trailing zeros but keep at least required precision
        var sep = NumberFormatInfo.NumberDecimalSeparator;
        if (smallFormatted.Contains(sep))
        {
            smallFormatted = smallFormatted.TrimEnd('0');
            if (smallFormatted.EndsWith(sep, StringComparison.Ordinal))
            {
                smallFormatted = smallFormatted.Substring(0, smallFormatted.Length - sep.Length);
            }
        }

        return isNegative ? NumberFormatInfo.NegativeSign + smallFormatted : smallFormatted;
    }

    private List<NumberFormatPart> FormatCompactToParts(double value)
    {
        var parts = new List<NumberFormatPart>();

        // Handle special values
        if (double.IsNaN(value))
        {
            parts.Add(new NumberFormatPart("nan", NumberFormatInfo.NaNSymbol));
            return parts;
        }

        if (double.IsPositiveInfinity(value))
        {
            parts.Add(new NumberFormatPart("infinity", NumberFormatInfo.PositiveInfinitySymbol));
            return parts;
        }

        if (double.IsNegativeInfinity(value))
        {
            parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
            parts.Add(new NumberFormatPart("infinity", NumberFormatInfo.PositiveInfinitySymbol));
            return parts;
        }

        var absValue = System.Math.Abs(value);
        var isNegative = value < 0;
        var isLong = string.Equals(CompactDisplay, "long", StringComparison.Ordinal);

        // Handle sign
        if (isNegative)
        {
            parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
        }

        // Get locale-specific compact patterns
        var patterns = Data.CompactPatterns.GetPatterns(Locale);
        var threshold = patterns.GetThreshold(isLong);

        // For values below threshold, format as regular decimal parts
        if (absValue < threshold || absValue == 0)
        {
            AddDecimalParts(parts, absValue);
            return parts;
        }

        // Determine the appropriate compact unit
        string? compactSuffix = null;
        double divisor = 1;

        var divisorMillion = patterns.DivisorMillion;
        var divisorBillion = patterns.DivisorBillion;

        if (absValue >= 1_000_000_000_000)
        {
            compactSuffix = isLong ? patterns.LongTrillion : patterns.ShortTrillion;
            divisor = 1_000_000_000_000;
        }
        else if (absValue >= divisorBillion)
        {
            compactSuffix = isLong ? patterns.LongBillion : patterns.ShortBillion;
            divisor = divisorBillion;
        }
        else if (absValue >= divisorMillion)
        {
            compactSuffix = isLong ? patterns.LongMillion : patterns.ShortMillion;
            divisor = divisorMillion;
        }
        else if (absValue >= 1000)
        {
            var thousandSuffix = isLong ? patterns.LongThousand : patterns.ShortThousand;
            if (!string.IsNullOrEmpty(thousandSuffix))
            {
                compactSuffix = thousandSuffix;
                divisor = 1000;
            }
        }

        // If no compact suffix, format as decimal parts
        if (string.IsNullOrEmpty(compactSuffix))
        {
            AddDecimalParts(parts, absValue);
            return parts;
        }

        var compactValue = absValue / divisor;

        // Round to 2 significant figures
        var compactMagnitude = compactValue >= 1 ? (int) System.Math.Floor(System.Math.Log10(compactValue)) : 0;
        var compactDecimalPlaces = 1 - compactMagnitude;
        if (compactDecimalPlaces < 0)
        {
            compactDecimalPlaces = 0;
        }

        compactValue = ApplyRounding(compactValue, compactDecimalPlaces);

        // Add integer part
        var integerPart = (long) System.Math.Truncate(compactValue);
        parts.Add(new NumberFormatPart("integer", integerPart.ToString(CultureInfo.InvariantCulture)));

        // Add decimal part if needed
        var fractionValue = compactValue - integerPart;
        if (compactDecimalPlaces > 0 && fractionValue > 0.00001)
        {
            parts.Add(new NumberFormatPart("decimal", NumberFormatInfo.NumberDecimalSeparator));

            var multiplier = System.Math.Pow(10, compactDecimalPlaces);
            var fractionInt = (long) System.Math.Round(fractionValue * multiplier);
            var fractionStr = fractionInt.ToString(CultureInfo.InvariantCulture);

            // Trim trailing zeros
            fractionStr = fractionStr.TrimEnd('0');
            if (fractionStr.Length > 0)
            {
                parts.Add(new NumberFormatPart("fraction", fractionStr));
            }
        }

        // Add literal space and compact suffix (only if locale uses space)
        if (isLong && patterns.LongSpace)
        {
            parts.Add(new NumberFormatPart("literal", " "));
        }
        else if (!isLong && patterns.ShortSpace)
        {
            parts.Add(new NumberFormatPart("literal", "\u00a0")); // Non-breaking space for short format
        }
        // For formats without space, we don't add a literal part

        parts.Add(new NumberFormatPart("compact", compactSuffix!));

        return parts;
    }

    private void AddDecimalParts(List<NumberFormatPart> parts, double value)
    {
        if (value == 0)
        {
            parts.Add(new NumberFormatPart("integer", "0"));
            return;
        }

        var magnitude = value >= 1 ? (int) System.Math.Floor(System.Math.Log10(value)) : 0;
        int decimalPlaces;

        if (magnitude >= 4)
        {
            decimalPlaces = 0;
        }
        else if (magnitude >= 2)
        {
            decimalPlaces = 0;
        }
        else if (value >= 1)
        {
            decimalPlaces = 1 - magnitude;
        }
        else
        {
            var smallMagnitude = (int) System.Math.Floor(System.Math.Log10(value));
            decimalPlaces = 1 - smallMagnitude;
        }

        var rounded = ApplyRounding(value, decimalPlaces);

        // Add integer part
        var integerPart = (long) System.Math.Truncate(rounded);
        var intStr = integerPart.ToString(CultureInfo.InvariantCulture);

        // Add grouping for large numbers
        if (rounded >= 10000)
        {
            intStr = integerPart.ToString("#,##0", NumberFormatInfo);
            // Parse groups and add as separate parts
            var groups = intStr.Split(new[] { NumberFormatInfo.NumberGroupSeparator }, StringSplitOptions.None);
            for (var i = 0; i < groups.Length; i++)
            {
                if (i > 0)
                {
                    parts.Add(new NumberFormatPart("group", NumberFormatInfo.NumberGroupSeparator));
                }
                parts.Add(new NumberFormatPart("integer", groups[i]));
            }
        }
        else
        {
            parts.Add(new NumberFormatPart("integer", intStr));
        }

        // Add decimal part if needed
        if (decimalPlaces > 0)
        {
            var fractionValue = rounded - integerPart;
            if (fractionValue > 0.00001)
            {
                parts.Add(new NumberFormatPart("decimal", NumberFormatInfo.NumberDecimalSeparator));

                var multiplier = System.Math.Pow(10, decimalPlaces);
                var fractionInt = (long) System.Math.Round(fractionValue * multiplier);
                var fractionStr = fractionInt.ToString(CultureInfo.InvariantCulture).PadLeft(decimalPlaces, '0');

                // Trim trailing zeros
                fractionStr = fractionStr.TrimEnd('0');
                if (fractionStr.Length > 0)
                {
                    parts.Add(new NumberFormatPart("fraction", fractionStr));
                }
            }
        }
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

        // Fix floating point precision issues by rounding to a reasonable number of decimal places
        // This handles cases like 1.15 * 100 = 114.99999999999999
        scaled = System.Math.Round(scaled, 10);

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
        // halfCeil: ties go toward positive infinity (ceiling)
        var floor = System.Math.Floor(value);
        var ceil = System.Math.Ceiling(value);
        var distToFloor = value - floor;
        var distToCeil = ceil - value;

        // Round to nearest, with ties going to ceiling
        if (distToFloor < distToCeil)
        {
            return floor;
        }
        // distToCeil <= distToFloor means we're at midpoint or closer to ceiling
        return ceil;
    }

    private static double RoundHalfFloor(double value)
    {
        // halfFloor: ties go toward negative infinity (floor)
        var floor = System.Math.Floor(value);
        var ceil = System.Math.Ceiling(value);
        var distToFloor = value - floor;
        var distToCeil = ceil - value;

        // Round to nearest, with ties going to floor
        if (distToCeil < distToFloor)
        {
            return ceil;
        }
        // distToFloor <= distToCeil means we're at midpoint or closer to floor
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

        var result = Style switch
        {
            "currency" => FormatCurrencyBigInt(value),
            "percent" => FormatPercentBigInt(value),
            "unit" => FormatUnitBigInt(value),
            _ => FormatDecimalBigInt(value)
        };

        // Apply numbering system digit transliteration
        return Data.NumberingSystemData.TransliterateDigits(result, NumberingSystem);
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

        // Try to get unit patterns from CLDR provider
        var unitPatterns = CldrProvider.GetUnitPatterns(Locale, unitStr, unitDisplay);
        if (unitPatterns != null)
        {
            // Use the CLDR pattern - it already contains spacing information
            // Select singular or plural pattern based on the absolute value
            var isSingular = BigInteger.Abs(value) == BigInteger.One;
            var pattern = isSingular ? (unitPatterns.One ?? unitPatterns.Other) : unitPatterns.Other;
            return pattern.Replace("{0}", formattedNumber);
        }

        // Fallback to legacy behavior if no CLDR pattern found
        var unitSuffix = GetUnitSuffix(unitStr, unitDisplay, (double) value);

        // Determine if we need a space before the unit
        // Narrow display never has space; percent/degree units don't have space
        var needsSpace = !string.Equals(unitDisplay, "narrow", StringComparison.Ordinal) &&
                        !string.Equals(unitStr, "percent", StringComparison.Ordinal) &&
                        !string.Equals(unitStr, "celsius", StringComparison.Ordinal) &&
                        !string.Equals(unitStr, "fahrenheit", StringComparison.Ordinal);

        if (needsSpace)
        {
            return $"{formattedNumber} {unitSuffix}";
        }

        return $"{formattedNumber}{unitSuffix}";
    }

    private string FormatDecimal(double value)
    {
        // Check if value is negative (including -0)
        var isNegative = value < 0 || double.IsNegativeInfinity(1 / value);
        var absValue = System.Math.Abs(value);

        // Handle significant digits and roundingPriority
        if (MinimumSignificantDigits.HasValue || MaximumSignificantDigits.HasValue)
        {
            return FormatWithSignificantDigits(value);
        }

        // Apply rounding based on MaximumFractionDigits
        absValue = ApplyRounding(absValue, MaximumFractionDigits);
        var displaysAsZero = absValue == 0;
        var isNaN = double.IsNaN(value);

        // Determine if we should show a sign based on signDisplay
        var showNegativeSign = isNegative;
        var showPositiveSign = false;

        switch (SignDisplay)
        {
            case "always":
                // NaN still gets a plus sign for "always"
                showPositiveSign = !isNegative;
                break;
            case "exceptZero":
                // NaN never gets a sign for exceptZero (NaN is not a number, neither zero nor non-zero)
                showPositiveSign = !isNegative && !displaysAsZero && !isNaN;
                showNegativeSign = isNegative && !displaysAsZero;
                break;
            case "negative":
                showPositiveSign = false;
                showNegativeSign = isNegative && !displaysAsZero;
                break;
            case "never":
                showNegativeSign = false;
                showPositiveSign = false;
                break;
        }

        value = absValue;

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

        var formatted = value.ToString(format, NumberFormatInfo);

        // Apply sign based on signDisplay
        if (showNegativeSign)
        {
            return NumberFormatInfo.NegativeSign + formatted;
        }

        if (showPositiveSign)
        {
            return NumberFormatInfo.PositiveSign + formatted;
        }

        return formatted;
    }

    private string FormatWithSignificantDigits(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return value.ToString(NumberFormatInfo);
        }

        var minSigDigits = MinimumSignificantDigits ?? 1;
        var maxSigDigits = MaximumSignificantDigits ?? 21;

        // Format based on roundingPriority per ECMA-402 §15.5.14 RoundingDecisionForLeadingZeros
        if (string.Equals(RoundingPriority, "lessPrecision", StringComparison.Ordinal) ||
            string.Equals(RoundingPriority, "morePrecision", StringComparison.Ordinal))
        {
            var sigDigitsResult = FormatToSignificantDigits(value, minSigDigits, maxSigDigits);
            var fracDigitsResult = FormatWithFractionDigits(value);

            // Parse both results back to numeric values to compare precision loss
            var sigValue = ParseFormattedNumber(sigDigitsResult);
            var fracValue = ParseFormattedNumber(fracDigitsResult);

            // Calculate precision loss (distance from original value)
            var sigPrecisionLoss = System.Math.Abs(value - sigValue);
            var fracPrecisionLoss = System.Math.Abs(value - fracValue);

            // Count displayed digits in each result
            var sigDigitsCount = CountSignificantDigits(sigDigitsResult);
            var fracDigitsCount = CountSignificantDigits(fracDigitsResult);

            // Determine if each result is constrained by explicit min/max
            var sdHasExplicitMin = MinimumSignificantDigitsExplicit;
            var sdHasExplicitMax = MaximumSignificantDigitsExplicit;

            // Get the actual minimum constraint values for comparison
            var sdMinConstraint = sdHasExplicitMin ? minSigDigits : 0;
            // FD minimum is the number of fraction digits required (adds to total digits)
            var fdMinConstraint = MinimumFractionDigits;

            if (string.Equals(RoundingPriority, "morePrecision", StringComparison.Ordinal))
            {
                // morePrecision: prefer the result closer to original value
                if (sigPrecisionLoss < fracPrecisionLoss - 1e-15)
                {
                    return sigDigitsResult;
                }
                if (fracPrecisionLoss < sigPrecisionLoss - 1e-15)
                {
                    return fracDigitsResult;
                }

                // When values are equal (tie):
                // If only SD max is explicit (no explicit SD min), prefer FD (minimum-constrained)
                if (sdHasExplicitMax && !sdHasExplicitMin)
                {
                    return fracDigitsResult;
                }

                // If SD has explicit max AND min, compare the constraint ranges to determine
                // which allows more precision potential.
                if (sdHasExplicitMax && sdHasExplicitMin)
                {
                    // Compare maximum constraints - larger max = allows more precision
                    // SD max is in significant digits; FD max is fraction digits + 1 (for integer digit)
                    var effectiveFdMax = MaximumFractionDigits + 1;
                    if (maxSigDigits > effectiveFdMax)
                    {
                        return sigDigitsResult;
                    }
                    if (effectiveFdMax > maxSigDigits)
                    {
                        return fracDigitsResult;
                    }
                    // Maximums are equal - compare minimums
                    // Higher minimum = requires more precision
                    var effectiveFdMin = MinimumFractionDigits + 1;
                    if (minSigDigits >= effectiveFdMin)
                    {
                        return sigDigitsResult;
                    }
                    return fracDigitsResult;
                }

                // Both have only minimums (no explicit max): prefer SD (the significant digits result)
                // This follows the spec's preference for significant digits when constraints are equivalent
                return sigDigitsResult;
            }
            else // lessPrecision
            {
                // lessPrecision: prefer the result farther from original value
                if (sigPrecisionLoss > fracPrecisionLoss + 1e-15)
                {
                    return sigDigitsResult;
                }
                if (fracPrecisionLoss > sigPrecisionLoss + 1e-15)
                {
                    return fracDigitsResult;
                }

                // When values are equal (tie):
                // If only SD max is explicit (no explicit SD min), prefer SD (no minimum constraint)
                if (sdHasExplicitMax && !sdHasExplicitMin)
                {
                    return sigDigitsResult;
                }

                // If SD has explicit max AND min, compare constraint ranges
                // For lessPrecision, prefer smaller max = less precision potential
                if (sdHasExplicitMax && sdHasExplicitMin)
                {
                    var effectiveFdMax = MaximumFractionDigits + 1;
                    if (maxSigDigits < effectiveFdMax)
                    {
                        return sigDigitsResult;
                    }
                    if (effectiveFdMax < maxSigDigits)
                    {
                        return fracDigitsResult;
                    }
                    // Maximums are equal - compare minimums
                    // For lessPrecision, smaller minimum = less precision required
                    var effectiveFdMin = MinimumFractionDigits + 1;
                    if (minSigDigits <= effectiveFdMin)
                    {
                        return sigDigitsResult;
                    }
                    return fracDigitsResult;
                }

                // Default for lessPrecision: prefer FD (fraction digits result)
                return fracDigitsResult;
            }
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
        // Check for negative zero (1.0 / -0 == -Infinity)
        var isNegativeZero = value == 0 && 1.0 / value == double.NegativeInfinity;

        if (value == 0)
        {
            var zeroResult = minSigDigits > 1 ? "0." + new string('0', minSigDigits - 1) : "0";
            return isNegativeZero ? "-" + zeroResult : zeroResult;
        }

        var isNegative = value < 0;
        var absValue = System.Math.Abs(value);

        // Get the order of magnitude
        var magnitude = (int) System.Math.Floor(System.Math.Log10(absValue));

        // Round to max significant digits first
        var decimalPlacesForRounding = maxSigDigits - magnitude - 1;
        double rounded;
        if (decimalPlacesForRounding >= 0)
        {
            rounded = ApplyRounding(value, decimalPlacesForRounding);
        }
        else
        {
            // Need to round to whole numbers or higher
            var divisor = System.Math.Pow(10, -decimalPlacesForRounding);
            rounded = ApplyRounding(value / divisor, 0) * divisor;
        }

        // After rounding, take absolute value for formatting
        rounded = System.Math.Abs(rounded);

        // For the actual formatting, format with max precision
        // then trim unnecessary trailing zeros beyond minSigDigits
        string result;
        if (decimalPlacesForRounding <= 0)
        {
            result = rounded.ToString("F0", CultureInfo.InvariantCulture);
        }
        else
        {
            // Format with max precision, then we'll trim
            var formatSpec = "F" + decimalPlacesForRounding;
            result = rounded.ToString(formatSpec, CultureInfo.InvariantCulture);

            // Trim trailing zeros beyond minSigDigits
            if (result.Contains('.'))
            {
                var currentSigDigits = CountSignificantDigits(result);
                while (currentSigDigits > minSigDigits && result[result.Length - 1] == '0' && result.Contains('.'))
                {
                    result = result.Substring(0, result.Length - 1);
                    currentSigDigits = CountSignificantDigits(result);
                }
                // Remove trailing decimal point if no fraction left
                if (result[result.Length - 1] == '.')
                {
                    result = result.Substring(0, result.Length - 1);
                }
            }
        }

        // Ensure minimum significant digits (add trailing zeros if needed)
        var finalSigDigits = CountSignificantDigits(result);
        if (finalSigDigits < minSigDigits)
        {
            var zerosToAdd = minSigDigits - finalSigDigits;
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

    /// <summary>
    /// Parses a formatted number string back to a double value.
    /// Used for comparing precision loss between different formatting approaches.
    /// Handles non-ASCII digit systems (Arabic, Thai, etc.) and locale-specific decimal separators.
    /// </summary>
    private double ParseFormattedNumber(string formatted)
    {
        // Use the locale's decimal separator to properly identify decimal vs grouping
        var decimalSeparator = NumberFormatInfo.NumberDecimalSeparator;

        // Build a clean numeric string by extracting digits and decimal point
        var sb = new System.Text.StringBuilder(formatted.Length);
        var hasDecimal = false;
        var isNegative = false;

        for (var i = 0; i < formatted.Length; i++)
        {
            var c = formatted[i];

            // Handle negative sign (various forms)
            if ((c == '-' || c == '\u2212') && sb.Length == 0) // ASCII minus or Unicode minus
            {
                isNegative = true;
                continue;
            }

            // Check if this position matches the locale's decimal separator
            if (!hasDecimal && decimalSeparator.Length > 0 && i + decimalSeparator.Length <= formatted.Length)
            {
                var match = true;
                for (var j = 0; j < decimalSeparator.Length; j++)
                {
                    if (formatted[i + j] != decimalSeparator[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    sb.Append('.');
                    hasDecimal = true;
                    i += decimalSeparator.Length - 1;
                    continue;
                }
            }

            // Handle other decimal separators (. or Arabic decimal ٫)
            if ((c == '.' || c == '٫' || c == '\u066B') && !hasDecimal)
            {
                sb.Append('.');
                hasDecimal = true;
                continue;
            }

            // Skip grouping separators (likely . or , or space depending on locale)
            if (c == ',' || c == ' ' || c == '\u00A0' || c == '\u202F') // Also handle narrow no-break space
            {
                continue;
            }

            // Handle ASCII digits 0-9
            if (c >= '0' && c <= '9')
            {
                sb.Append(c);
                continue;
            }

            // Handle Arabic-Indic digits ٠-٩ (U+0660 to U+0669)
            if (c >= '\u0660' && c <= '\u0669')
            {
                sb.Append((char) ('0' + (c - '\u0660')));
                continue;
            }

            // Handle Extended Arabic-Indic digits ۰-۹ (U+06F0 to U+06F9)
            if (c >= '\u06F0' && c <= '\u06F9')
            {
                sb.Append((char) ('0' + (c - '\u06F0')));
                continue;
            }

            // Handle other numeric systems using char.GetNumericValue
            var numericValue = char.GetNumericValue(c);
            if (numericValue >= 0 && numericValue <= 9)
            {
                sb.Append((char) ('0' + (int) numericValue));
            }
            // Skip grouping separators, currency symbols, etc.
        }

        if (sb.Length == 0)
        {
            return 0;
        }

        if (double.TryParse(sb.ToString(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var result))
        {
            return isNegative ? -result : result;
        }

        return 0;
    }

    private string FormatCurrency(double value)
    {
        // Handle significant digits if specified
        if (MinimumSignificantDigits.HasValue || MaximumSignificantDigits.HasValue)
        {
            return FormatCurrencyWithSignificantDigits(value);
        }

        // Check for negative zero (1.0 / -0 == -Infinity)
        var isNegativeZero = value == 0 && double.IsNegativeInfinity(1.0 / value);
        var isNegative = value < 0 || isNegativeZero;
        var absValue = System.Math.Abs(value);

        // Apply rounding - use MaximumFractionDigits directly (constructor sets appropriate defaults)
        var fractionDigits = MaximumFractionDigits;
        absValue = ApplyRounding(absValue, fractionDigits);

        // Check if rounded value displays as zero
        var displaysAsZero = absValue < System.Math.Pow(10, -fractionDigits) / 2;

        // Determine if we should show a sign and what kind
        var showNegativeSign = isNegative;
        var showPositiveSign = false;
        var useAccountingFormat = string.Equals(CurrencySign, "accounting", StringComparison.Ordinal);

        // Handle signDisplay
        switch (SignDisplay)
        {
            case "always":
                showPositiveSign = !isNegative;
                break;
            case "exceptZero":
                // For exceptZero, don't show sign if the displayed value is zero
                showPositiveSign = !isNegative && !displaysAsZero;
                showNegativeSign = isNegative && !displaysAsZero;
                break;
            case "negative":
                // For negative, only show negative sign for truly negative, non-zero display
                showPositiveSign = false;
                showNegativeSign = isNegative && !displaysAsZero;
                break;
            case "never":
                showNegativeSign = false;
                showPositiveSign = false;
                break;
        }

        // Format the absolute value
        var symbol = NumberFormatInfo.CurrencySymbol;

        // Build the number part
        var integerPart = (long) System.Math.Truncate(absValue);
        var fractionValue = absValue - integerPart;

        // Format integer with grouping
        var intStr = integerPart.ToString(CultureInfo.InvariantCulture);
        var digitCount = intStr.Length;
        if (ShouldApplyGrouping(digitCount))
        {
            intStr = ApplyGroupingToString(intStr);
        }

        // Format fraction
        var fractionStr = "";
        if (fractionDigits > 0)
        {
            var multiplier = System.Math.Pow(10, fractionDigits);
            var fractionInt = (long) System.Math.Round(fractionValue * multiplier);
            fractionStr = NumberFormatInfo.CurrencyDecimalSeparator +
                         fractionInt.ToString(CultureInfo.InvariantCulture).PadLeft(fractionDigits, '0');
        }

        var numberPart = intStr + fractionStr;

        // Build result based on locale's currency pattern
        // CurrencyPositivePattern: 0=$n, 1=n$, 2=$ n, 3=n $
        // Use non-breaking space (U+00A0) per CLDR conventions
        var pattern = NumberFormatInfo.CurrencyPositivePattern;
        const string Nbsp = " ";
        string formattedCurrency;

        switch (pattern)
        {
            case 0: // $n
                formattedCurrency = symbol + numberPart;
                break;
            case 1: // n$
                formattedCurrency = numberPart + symbol;
                break;
            case 2: // $ n
                formattedCurrency = symbol + Nbsp + numberPart;
                break;
            case 3: // n $
            default:
                formattedCurrency = numberPart + Nbsp + symbol;
                break;
        }

        // Apply sign based on signDisplay
        string result;
        if (showNegativeSign)
        {
            if (useAccountingFormat)
            {
                // Use CLDR-based accounting format (parentheses for most locales)
                result = FormatAccountingNegativeCurrency(numberPart, symbol);
            }
            else
            {
                // Standard format uses minus sign before everything
                result = $"-{formattedCurrency}";
            }
        }
        else if (showPositiveSign)
        {
            result = $"+{formattedCurrency}";
        }
        else
        {
            result = formattedCurrency;
        }

        return result;
    }

    /// <summary>
    /// Formats a negative currency value for accounting format using CLDR-based patterns.
    /// </summary>
    private string FormatAccountingNegativeCurrency(string numberPart, string symbol)
    {
        // CLDR accounting patterns vary by locale:
        // - English, Japanese, Korean, Chinese locales use parentheses
        // - German, French, and other European locales use minus sign (same as standard)
        // Check if locale uses parentheses for accounting
        if (UsesParenthesesForAccounting())
        {
            // Use parentheses matching the positive pattern position:
            // Positive pattern 0 ($n) → Accounting uses "($n)"
            // Positive pattern 1 (n$) → Accounting uses "(n$)"
            // Positive pattern 2 ($ n) → Accounting uses "($ n)"
            // Positive pattern 3 (n $) → Accounting uses "(n $)"
            var posPattern = NumberFormatInfo.CurrencyPositivePattern;
            const string Nbsp = "\u00A0"; // Non-breaking space

            return posPattern switch
            {
                0 => $"({symbol}{numberPart})", // ($n)
                1 => $"({numberPart}{symbol})", // (n$)
                2 => $"({symbol}{Nbsp}{numberPart})", // ($ n)
                3 => $"({numberPart}{Nbsp}{symbol})", // (n $)
                _ => $"({symbol}{numberPart})"
            };
        }

        // Fall back to standard negative currency format (minus sign)
        return FormatNegativeCurrency(numberPart, symbol);
    }

    /// <summary>
    /// Determines if the current locale uses parentheses for accounting format.
    /// Based on CLDR accounting currency patterns.
    /// </summary>
    private bool UsesParenthesesForAccounting()
    {
        // Get the base language from the locale
        var locale = Locale ?? "en";
        var dashIndex = locale.IndexOf('-');
        var lang = dashIndex > 0 ? locale.Substring(0, dashIndex) : locale;

        // Locales that use parentheses for accounting per CLDR:
        // - English (en), Japanese (ja), Korean (ko), Chinese (zh)
        // Other locales (de, fr, es, etc.) use minus sign
        return lang switch
        {
            "en" => true,
            "ja" => true,
            "ko" => true,
            "zh" => true,
            _ => false
        };
    }

    /// <summary>
    /// Formats a negative currency value according to the locale's CurrencyNegativePattern.
    /// Uses non-breaking space (U+00A0) per CLDR conventions.
    /// </summary>
    private string FormatNegativeCurrency(string numberPart, string symbol)
    {
        // CurrencyNegativePattern values:
        // 0: ($n), 1: -$n, 2: $-n, 3: $n-, 4: (n$), 5: -n$, 6: n-$, 7: n$-
        // 8: -n $, 9: -$ n, 10: n $-, 11: $ n-, 12: $ -n, 13: n- $, 14: ($ n), 15: (n $)
        var negPattern = NumberFormatInfo.CurrencyNegativePattern;
        const string Nbsp = " "; // Non-breaking space

        return negPattern switch
        {
            0 => $"({symbol}{numberPart})",
            1 => $"-{symbol}{numberPart}",
            2 => $"{symbol}-{numberPart}",
            3 => $"{symbol}{numberPart}-",
            4 => $"({numberPart}{symbol})",
            5 => $"-{numberPart}{symbol}",
            6 => $"{numberPart}-{symbol}",
            7 => $"{numberPart}{symbol}-",
            8 => $"-{numberPart}{Nbsp}{symbol}",
            9 => $"-{symbol}{Nbsp}{numberPart}",
            10 => $"{numberPart}{Nbsp}{symbol}-",
            11 => $"{symbol}{Nbsp}{numberPart}-",
            12 => $"{symbol}{Nbsp}-{numberPart}",
            13 => $"{numberPart}-{Nbsp}{symbol}",
            14 => $"({symbol}{Nbsp}{numberPart})",
            15 => $"({numberPart}{Nbsp}{symbol})",
            _ => $"-{symbol}{numberPart}"
        };
    }

    private string FormatCurrencyWithSignificantDigits(double value)
    {
        var isNegative = value < 0;
        var isZero = value == 0;
        var absValue = System.Math.Abs(value);

        // Format using significant digits
        var formatted = FormatToSignificantDigits(absValue, MinimumSignificantDigits ?? 1, MaximumSignificantDigits ?? 21);

        var symbol = NumberFormatInfo.CurrencySymbol;
        var useAccountingFormat = string.Equals(CurrencySign, "accounting", StringComparison.Ordinal);

        // Build currency string based on locale pattern
        var posPattern = NumberFormatInfo.CurrencyPositivePattern;
        string formattedCurrency;

        const string Nbsp = "\u00A0"; // Non-breaking space per CLDR
        switch (posPattern)
        {
            case 0: // $n
                formattedCurrency = symbol + formatted;
                break;
            case 1: // n$
                formattedCurrency = formatted + symbol;
                break;
            case 2: // $ n
                formattedCurrency = symbol + Nbsp + formatted;
                break;
            case 3: // n $
            default:
                formattedCurrency = formatted + Nbsp + symbol;
                break;
        }

        // Determine sign display
        var showNegativeSign = isNegative && !isZero;
        var showPositiveSign = false;

        switch (SignDisplay)
        {
            case "always":
                showPositiveSign = !isNegative;
                break;
            case "exceptZero":
                showPositiveSign = !isNegative && !isZero;
                showNegativeSign = isNegative && !isZero;
                break;
            case "never":
                showNegativeSign = false;
                break;
        }

        if (showNegativeSign)
        {
            if (useAccountingFormat)
            {
                return FormatNegativeCurrency(formatted, symbol);
            }
            return $"-{formattedCurrency}";
        }

        if (showPositiveSign)
        {
            return $"+{formattedCurrency}";
        }

        return formattedCurrency;
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
        var formattedNumber = FormatDecimal(value);
        var unitDisplay = UnitDisplay ?? "short";
        var unitStr = Unit ?? "";

        // Try to get unit patterns from CLDR provider
        var unitPatterns = CldrProvider.GetUnitPatterns(Locale, unitStr, unitDisplay);
        if (unitPatterns != null)
        {
            // Use the CLDR pattern - it already contains spacing information
            // Select singular or plural pattern based on the absolute value
            var isSingular = System.Math.Abs(value) == 1;
            var pattern = isSingular ? (unitPatterns.One ?? unitPatterns.Other) : unitPatterns.Other;
            return pattern.Replace("{0}", formattedNumber);
        }

        // Fallback to legacy behavior if no CLDR pattern found
        var unitSuffix = GetUnitSuffix(unitStr, unitDisplay, value);

        // Determine if we need a space before the unit
        // Narrow display never has space; percent/degree units don't have space
        var needsSpace = !string.Equals(unitDisplay, "narrow", StringComparison.Ordinal) &&
                        !string.Equals(unitStr, "percent", StringComparison.Ordinal) &&
                        !string.Equals(unitStr, "celsius", StringComparison.Ordinal) &&
                        !string.Equals(unitStr, "fahrenheit", StringComparison.Ordinal);

        if (needsSpace)
        {
            return $"{formattedNumber} {unitSuffix}";
        }

        return $"{formattedNumber}{unitSuffix}";
    }

    private static string GetUnitSuffix(string unit, string display, double value)
    {
        // This is a simplified version - full implementation would use CLDR data
        var isPlural = System.Math.Abs(value) != 1;
        var isLong = string.Equals(display, "long", StringComparison.Ordinal);
        var isNarrow = string.Equals(display, "narrow", StringComparison.Ordinal);

        // Handle compound units like "kilometer-per-hour"
        var perIndex = unit.IndexOf("-per-", StringComparison.Ordinal);
        if (perIndex > 0)
        {
            var numerator = unit.Substring(0, perIndex);
            var denominator = unit.Substring(perIndex + 5);

            var numSuffix = GetUnitSuffix(numerator, display, value);
            var denomSuffix = GetUnitSuffix(denominator, display, 1); // Always singular for denominator

            if (isLong)
            {
                return $"{numSuffix} per {denomSuffix}";
            }
            else
            {
                return $"{numSuffix}/{denomSuffix}";
            }
        }

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
            "second" => "s",
            "minute" => "min",
            "hour" => "h",
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

    /// <summary>
    /// Formats a number and returns an array of parts.
    /// https://tc39.es/ecma402/#sec-partitionnumberpattern
    /// </summary>
    internal List<NumberFormatPart> FormatToParts(double value)
    {
        var parts = new List<NumberFormatPart>();

        // Handle special values
        if (double.IsNaN(value))
        {
            // For signDisplay "always", NaN gets a plus sign
            if (string.Equals(SignDisplay, "always", StringComparison.Ordinal))
            {
                parts.Add(new NumberFormatPart("plusSign", NumberFormatInfo.PositiveSign));
            }
            parts.Add(new NumberFormatPart("nan", NumberFormatInfo.NaNSymbol));
            return parts;
        }

        if (double.IsPositiveInfinity(value))
        {
            // For signDisplay "always" or "exceptZero", show plus sign (infinity is never zero)
            if (string.Equals(SignDisplay, "always", StringComparison.Ordinal) ||
                string.Equals(SignDisplay, "exceptZero", StringComparison.Ordinal))
            {
                parts.Add(new NumberFormatPart("plusSign", NumberFormatInfo.PositiveSign));
            }
            parts.Add(new NumberFormatPart("infinity", NumberFormatInfo.PositiveInfinitySymbol));
            return parts;
        }

        if (double.IsNegativeInfinity(value))
        {
            // For signDisplay "negative", always show the minus sign (infinity is never zero)
            // For "never", don't show the sign
            // For "auto" (default), show the minus sign
            if (!string.Equals(SignDisplay, "never", StringComparison.Ordinal))
            {
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
            }
            parts.Add(new NumberFormatPart("infinity", NumberFormatInfo.PositiveInfinitySymbol));
            return parts;
        }

        // Handle notation first
        if (!string.Equals(Notation, "standard", StringComparison.Ordinal))
        {
            return FormatNotationToParts(value);
        }

        // Handle different styles
        return Style switch
        {
            "currency" => FormatCurrencyToParts(value),
            "percent" => FormatPercentToParts(value),
            "unit" => FormatUnitToParts(value),
            _ => FormatDecimalToParts(value)
        };
    }

    private List<NumberFormatPart> FormatNotationToParts(double value)
    {
        // Handle compact notation separately
        if (string.Equals(Notation, "compact", StringComparison.Ordinal))
        {
            return FormatCompactToParts(value);
        }

        var parts = new List<NumberFormatPart>();

        var isNegative = value < 0;
        if (isNegative)
        {
            parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
            value = System.Math.Abs(value);
        }
        else if (ShouldShowPlusSign(value))
        {
            parts.Add(new NumberFormatPart("plusSign", NumberFormatInfo.PositiveSign));
        }

        if (value == 0)
        {
            parts.Add(new NumberFormatPart("integer", "0"));
            parts.Add(new NumberFormatPart("exponentSeparator", "E"));
            parts.Add(new NumberFormatPart("exponentInteger", "0"));
            return parts;
        }

        int exponent;
        double mantissa;

        if (string.Equals(Notation, "engineering", StringComparison.Ordinal))
        {
            // Engineering notation uses exponents that are multiples of 3
            var rawExponent = (int) System.Math.Floor(System.Math.Log10(value));
            exponent = (int) (System.Math.Floor(rawExponent / 3.0) * 3);
            mantissa = value / System.Math.Pow(10, exponent);
        }
        else
        {
            // Scientific notation
            exponent = (int) System.Math.Floor(System.Math.Log10(value));
            mantissa = value / System.Math.Pow(10, exponent);
        }

        // Round the mantissa
        mantissa = ApplyRounding(mantissa, MaximumFractionDigits);

        // Split mantissa into integer and fraction
        var integerPart = (long) System.Math.Truncate(mantissa);
        var fractionValue = mantissa - integerPart;

        // Add integer part
        parts.Add(new NumberFormatPart("integer", integerPart.ToString(CultureInfo.InvariantCulture)));

        // Add fraction part if needed
        if (MinimumFractionDigits > 0 || (fractionValue > 0 && MaximumFractionDigits > 0))
        {
            parts.Add(new NumberFormatPart("decimal", NumberFormatInfo.NumberDecimalSeparator));

            var fractionDigits = MaximumFractionDigits > 0 ? MaximumFractionDigits : MinimumFractionDigits;
            var multiplier = System.Math.Pow(10, fractionDigits);
            var fractionInt = (long) System.Math.Round(fractionValue * multiplier);
            var fractionStr = fractionInt.ToString(CultureInfo.InvariantCulture).PadLeft(fractionDigits, '0');

            // Trim trailing zeros above minimum
            if (fractionStr.Length > MinimumFractionDigits)
            {
                var trimEnd = fractionStr.Length;
                while (trimEnd > MinimumFractionDigits && fractionStr[trimEnd - 1] == '0')
                {
                    trimEnd--;
                }
                fractionStr = fractionStr.Substring(0, trimEnd);
            }

            if (fractionStr.Length > 0)
            {
                parts.Add(new NumberFormatPart("fraction", fractionStr));
            }
        }

        // Add exponent separator
        parts.Add(new NumberFormatPart("exponentSeparator", "E"));

        // Add exponent sign if negative
        if (exponent < 0)
        {
            parts.Add(new NumberFormatPart("exponentMinusSign", NumberFormatInfo.NegativeSign));
            exponent = System.Math.Abs(exponent);
        }

        // Add exponent integer
        parts.Add(new NumberFormatPart("exponentInteger", exponent.ToString(CultureInfo.InvariantCulture)));

        return parts;
    }

    private List<NumberFormatPart> FormatDecimalToParts(double value)
    {
        var parts = new List<NumberFormatPart>();

        // Handle sign
        var isNegative = value < 0 || double.IsNegativeInfinity(1 / value); // Handles -0
        var absValue = System.Math.Abs(value);

        // Apply rounding first to determine if value displays as zero
        absValue = ApplyRounding(absValue, MaximumFractionDigits);
        var displaysAsZero = absValue == 0;

        // Determine if we should show a sign based on signDisplay
        var showNegativeSign = isNegative;
        var showPositiveSign = false;

        switch (SignDisplay)
        {
            case "always":
                showPositiveSign = !isNegative;
                break;
            case "exceptZero":
                showPositiveSign = !isNegative && !displaysAsZero;
                showNegativeSign = isNegative && !displaysAsZero;
                break;
            case "negative":
                showPositiveSign = false;
                // For "negative" signDisplay, only show negative sign for truly negative, non-zero display
                showNegativeSign = isNegative && !displaysAsZero;
                break;
            case "never":
                showNegativeSign = false;
                showPositiveSign = false;
                break;
        }

        if (showNegativeSign)
        {
            parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
        }
        else if (showPositiveSign)
        {
            parts.Add(new NumberFormatPart("plusSign", NumberFormatInfo.PositiveSign));
        }

        value = absValue;

        // Handle significant digits if specified
        if (MinimumSignificantDigits.HasValue || MaximumSignificantDigits.HasValue)
        {
            FormatWithSignificantDigitsToParts(parts, value);
            return parts;
        }

        // Value is already rounded

        // Split into integer and fraction parts
        var integerPart = (long) System.Math.Truncate(value);
        var fractionValue = value - integerPart;

        // Format integer part with grouping
        FormatIntegerToParts(parts, integerPart);

        // Format fraction part if needed
        if (MinimumFractionDigits > 0 || (fractionValue > 0 && MaximumFractionDigits > 0))
        {
            parts.Add(new NumberFormatPart("decimal", NumberFormatInfo.NumberDecimalSeparator));
            FormatFractionToParts(parts, fractionValue);
        }

        return parts;
    }

    /// <summary>
    /// Formats a number with significant digits and adds parts to the list.
    /// </summary>
    private void FormatWithSignificantDigitsToParts(List<NumberFormatPart> parts, double value)
    {
        if (value == 0)
        {
            var minSigDigits = MinimumSignificantDigits ?? 1;
            if (minSigDigits > 1)
            {
                parts.Add(new NumberFormatPart("integer", "0"));
                parts.Add(new NumberFormatPart("decimal", NumberFormatInfo.NumberDecimalSeparator));
                parts.Add(new NumberFormatPart("fraction", new string('0', minSigDigits - 1)));
            }
            else
            {
                parts.Add(new NumberFormatPart("integer", "0"));
            }
            return;
        }

        var minSig = MinimumSignificantDigits ?? 1;
        var maxSig = MaximumSignificantDigits ?? 21;

        // Get the order of magnitude
        var magnitude = (int) System.Math.Floor(System.Math.Log10(value));
        var decimalPlaces = maxSig - magnitude - 1;

        // Round to the required number of significant digits
        double rounded;
        if (decimalPlaces >= 0)
        {
            rounded = ApplyRounding(value, decimalPlaces);
        }
        else
        {
            // Need to round to whole numbers or higher
            var divisor = System.Math.Pow(10, -decimalPlaces);
            rounded = ApplyRounding(value / divisor, 0) * divisor;
        }

        // Get integer and fraction parts from rounded value
        var integerPart = (long) System.Math.Truncate(rounded);
        var fractionValue = rounded - integerPart;

        // Calculate actual significant digits we have
        var intStr = integerPart.ToString(CultureInfo.InvariantCulture);
        var currentSigDigits = intStr.TrimStart('0').Length;
        if (currentSigDigits == 0) currentSigDigits = 1;

        // Format integer part with grouping
        FormatIntegerToParts(parts, integerPart);

        // Calculate fraction digits needed
        var fractionDigitsNeeded = 0;
        if (decimalPlaces > 0)
        {
            fractionDigitsNeeded = decimalPlaces;
        }

        // Ensure minimum significant digits
        var zerosToAdd = minSig - currentSigDigits;
        if (zerosToAdd > fractionDigitsNeeded)
        {
            fractionDigitsNeeded = zerosToAdd;
        }

        // Add fraction if needed
        if (fractionDigitsNeeded > 0 || fractionValue > 0)
        {
            parts.Add(new NumberFormatPart("decimal", NumberFormatInfo.NumberDecimalSeparator));

            // Format fraction
            var fractionDigits = System.Math.Max(fractionDigitsNeeded, decimalPlaces > 0 ? decimalPlaces : 0);
            if (fractionDigits > 0)
            {
                var multiplier = System.Math.Pow(10, fractionDigits);
                var fractionInt = (long) System.Math.Round(fractionValue * multiplier);
                var fractionStr = fractionInt.ToString(CultureInfo.InvariantCulture).PadLeft(fractionDigits, '0');
                parts.Add(new NumberFormatPart("fraction", fractionStr));
            }
            else if (zerosToAdd > 0)
            {
                parts.Add(new NumberFormatPart("fraction", new string('0', zerosToAdd)));
            }
        }
        else if (zerosToAdd > 0)
        {
            // Need to add trailing zeros to meet minSig requirement
            parts.Add(new NumberFormatPart("decimal", NumberFormatInfo.NumberDecimalSeparator));
            parts.Add(new NumberFormatPart("fraction", new string('0', zerosToAdd)));
        }
    }

    private void FormatIntegerToParts(List<NumberFormatPart> parts, long integerValue)
    {
        var intStr = integerValue.ToString(CultureInfo.InvariantCulture);

        // Pad with zeros if needed
        if (intStr.Length < MinimumIntegerDigits)
        {
            intStr = intStr.PadLeft(MinimumIntegerDigits, '0');
        }

        var digitCount = intStr.Length;
        var applyGrouping = ShouldApplyGrouping(digitCount);

        if (!applyGrouping)
        {
            parts.Add(new NumberFormatPart("integer", intStr));
            return;
        }

        // Add grouped integer parts
        var groupSeparator = NumberFormatInfo.NumberGroupSeparator;
        var groupSize = 3;
        var position = intStr.Length % groupSize;
        if (position == 0) position = groupSize;

        var currentGroup = intStr.Substring(0, position);
        parts.Add(new NumberFormatPart("integer", currentGroup));

        while (position < intStr.Length)
        {
            parts.Add(new NumberFormatPart("group", groupSeparator));
            parts.Add(new NumberFormatPart("integer", intStr.Substring(position, groupSize)));
            position += groupSize;
        }
    }

    private void FormatFractionToParts(List<NumberFormatPart> parts, double fractionValue)
    {
        // Convert fraction to string with required precision
        var fractionDigits = MaximumFractionDigits;
        if (fractionDigits == 0 && MinimumFractionDigits > 0)
        {
            fractionDigits = MinimumFractionDigits;
        }

        var multiplier = System.Math.Pow(10, fractionDigits);
        var fractionInt = (long) System.Math.Round(fractionValue * multiplier);
        var fractionStr = fractionInt.ToString(CultureInfo.InvariantCulture).PadLeft(fractionDigits, '0');

        // Trim trailing zeros if above minimum
        if (fractionStr.Length > MinimumFractionDigits)
        {
            var trimEnd = fractionStr.Length;
            while (trimEnd > MinimumFractionDigits && fractionStr[trimEnd - 1] == '0')
            {
                trimEnd--;
            }
            fractionStr = fractionStr.Substring(0, trimEnd);
        }

        // Ensure minimum fraction digits
        if (fractionStr.Length < MinimumFractionDigits)
        {
            fractionStr = fractionStr.PadRight(MinimumFractionDigits, '0');
        }

        if (fractionStr.Length > 0)
        {
            parts.Add(new NumberFormatPart("fraction", fractionStr));
        }
    }

    private List<NumberFormatPart> FormatCurrencyToParts(double value)
    {
        var parts = new List<NumberFormatPart>();
        var isNegative = value < 0 || double.IsNegativeInfinity(1 / value); // Handles -0
        var absValue = System.Math.Abs(value);

        // Get currency symbol
        var currencySymbol = NumberFormatInfo.CurrencySymbol;

        // Apply rounding first to determine if value displays as zero
        var fractionDigits = MaximumFractionDigits > 0 ? MaximumFractionDigits : 2;
        absValue = ApplyRounding(absValue, fractionDigits);
        var displaysAsZero = absValue == 0;

        // Determine if we should show a negative sign based on signDisplay
        var showNegativeSign = isNegative;
        var showPositiveSign = false;
        var useAccountingFormat = string.Equals(CurrencySign, "accounting", StringComparison.Ordinal);

        switch (SignDisplay)
        {
            case "always":
                showPositiveSign = !isNegative;
                break;
            case "exceptZero":
                showPositiveSign = !isNegative && !displaysAsZero;
                showNegativeSign = isNegative && !displaysAsZero;
                break;
            case "negative":
                showPositiveSign = false;
                // For "negative" signDisplay, only show negative sign for truly negative, non-zero display
                showNegativeSign = isNegative && !displaysAsZero;
                break;
            case "never":
                showNegativeSign = false;
                showPositiveSign = false;
                break;
        }

        // Split value
        var integerPart = (long) System.Math.Truncate(absValue);
        var fractionValue = absValue - integerPart;

        // Determine pattern based on locale (use positive pattern as base)
        var pattern = NumberFormatInfo.CurrencyPositivePattern;

        // Build parts based on sign display and format
        if (showNegativeSign)
        {
            if (useAccountingFormat)
            {
                // Use CLDR-based accounting format (parentheses for most locales)
                BuildAccountingCurrencyNegativeParts(parts, currencySymbol, integerPart, fractionValue);
            }
            else
            {
                // Standard format uses minus sign
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                BuildCurrencyPositiveParts(parts, pattern, currencySymbol, integerPart, fractionValue);
            }
        }
        else if (showPositiveSign)
        {
            parts.Add(new NumberFormatPart("plusSign", NumberFormatInfo.PositiveSign));
            BuildCurrencyPositiveParts(parts, pattern, currencySymbol, integerPart, fractionValue);
        }
        else
        {
            BuildCurrencyPositiveParts(parts, pattern, currencySymbol, integerPart, fractionValue);
        }

        return parts;
    }

    /// <summary>
    /// Builds currency parts for accounting format (negative values with parentheses).
    /// </summary>
    private void BuildAccountingCurrencyNegativeParts(List<NumberFormatPart> parts, string symbol, long integerPart, double fractionValue)
    {
        // Check if locale uses parentheses for accounting
        if (UsesParenthesesForAccounting())
        {
            // Use parentheses matching the positive pattern position
            var posPattern = NumberFormatInfo.CurrencyPositivePattern;
            const string Nbsp = "\u00A0"; // Non-breaking space

            switch (posPattern)
            {
                case 0: // $n → ($n)
                    parts.Add(new NumberFormatPart("literal", "("));
                    parts.Add(new NumberFormatPart("currency", symbol));
                    FormatIntegerToParts(parts, integerPart);
                    AddFractionPartsIfNeeded(parts, fractionValue);
                    parts.Add(new NumberFormatPart("literal", ")"));
                    break;
                case 1: // n$ → (n$)
                    parts.Add(new NumberFormatPart("literal", "("));
                    FormatIntegerToParts(parts, integerPart);
                    AddFractionPartsIfNeeded(parts, fractionValue);
                    parts.Add(new NumberFormatPart("currency", symbol));
                    parts.Add(new NumberFormatPart("literal", ")"));
                    break;
                case 2: // $ n → ($ n)
                    parts.Add(new NumberFormatPart("literal", "("));
                    parts.Add(new NumberFormatPart("currency", symbol));
                    parts.Add(new NumberFormatPart("literal", Nbsp));
                    FormatIntegerToParts(parts, integerPart);
                    AddFractionPartsIfNeeded(parts, fractionValue);
                    parts.Add(new NumberFormatPart("literal", ")"));
                    break;
                case 3: // n $ → (n $)
                default:
                    parts.Add(new NumberFormatPart("literal", "("));
                    FormatIntegerToParts(parts, integerPart);
                    AddFractionPartsIfNeeded(parts, fractionValue);
                    parts.Add(new NumberFormatPart("literal", Nbsp));
                    parts.Add(new NumberFormatPart("currency", symbol));
                    parts.Add(new NumberFormatPart("literal", ")"));
                    break;
            }
        }
        else
        {
            // Fall back to standard negative currency pattern (minus sign)
            BuildCurrencyNegativeParts(parts, symbol, integerPart, fractionValue);
        }
    }

    /// <summary>
    /// Builds currency parts for negative values using the locale's CurrencyNegativePattern.
    /// </summary>
    private void BuildCurrencyNegativeParts(List<NumberFormatPart> parts, string symbol, long integerPart, double fractionValue)
    {
        var negPattern = NumberFormatInfo.CurrencyNegativePattern;
        const string Nbsp = "\u00A0"; // Non-breaking space per CLDR

        switch (negPattern)
        {
            case 0: // ($n)
                parts.Add(new NumberFormatPart("literal", "("));
                parts.Add(new NumberFormatPart("currency", symbol));
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                parts.Add(new NumberFormatPart("literal", ")"));
                break;
            case 1: // -$n
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                parts.Add(new NumberFormatPart("currency", symbol));
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                break;
            case 2: // $-n
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                break;
            case 3: // $n-
                parts.Add(new NumberFormatPart("currency", symbol));
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                break;
            case 4: // (n$)
                parts.Add(new NumberFormatPart("literal", "("));
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("literal", ")"));
                break;
            case 5: // -n$
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                parts.Add(new NumberFormatPart("currency", symbol));
                break;
            case 6: // n-$
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                parts.Add(new NumberFormatPart("currency", symbol));
                break;
            case 7: // n$-
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                break;
            case 8: // -n $
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                parts.Add(new NumberFormatPart("literal", Nbsp));
                parts.Add(new NumberFormatPart("currency", symbol));
                break;
            case 9: // -$ n
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("literal", Nbsp));
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                break;
            case 10: // n $-
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                parts.Add(new NumberFormatPart("literal", Nbsp));
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                break;
            case 11: // $ n-
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("literal", Nbsp));
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                break;
            case 12: // $ -n
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("literal", Nbsp));
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                break;
            case 13: // n- $
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                parts.Add(new NumberFormatPart("literal", Nbsp));
                parts.Add(new NumberFormatPart("currency", symbol));
                break;
            case 14: // ($ n)
                parts.Add(new NumberFormatPart("literal", "("));
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("literal", Nbsp));
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                parts.Add(new NumberFormatPart("literal", ")"));
                break;
            case 15: // (n $)
                parts.Add(new NumberFormatPart("literal", "("));
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                parts.Add(new NumberFormatPart("literal", Nbsp));
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("literal", ")"));
                break;
            default:
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                parts.Add(new NumberFormatPart("currency", symbol));
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                break;
        }
    }

    private void BuildCurrencyPositiveParts(List<NumberFormatPart> parts, int pattern, string symbol, long integerPart, double fractionValue)
    {
        const string Nbsp = "\u00A0"; // Non-breaking space per CLDR
        switch (pattern)
        {
            case 0: // $n
                parts.Add(new NumberFormatPart("currency", symbol));
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                break;
            case 1: // n$
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                parts.Add(new NumberFormatPart("currency", symbol));
                break;
            case 2: // $ n
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("literal", Nbsp));
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                break;
            case 3: // n $
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                parts.Add(new NumberFormatPart("literal", Nbsp));
                parts.Add(new NumberFormatPart("currency", symbol));
                break;
            default:
                parts.Add(new NumberFormatPart("currency", symbol));
                FormatIntegerToParts(parts, integerPart);
                AddFractionPartsIfNeeded(parts, fractionValue);
                break;
        }
    }

    private void AddFractionPartsIfNeeded(List<NumberFormatPart> parts, double fractionValue)
    {
        var minFrac = MinimumFractionDigits > 0 ? MinimumFractionDigits : 2;
        if (minFrac > 0 || fractionValue > 0)
        {
            parts.Add(new NumberFormatPart("decimal", NumberFormatInfo.CurrencyDecimalSeparator));
            FormatFractionToParts(parts, fractionValue);
        }
    }

    private List<NumberFormatPart> FormatPercentToParts(double value)
    {
        var parts = new List<NumberFormatPart>();
        var isNegative = value < 0 || double.IsNegativeInfinity(1 / value); // Handles -0

        // Multiply by 100 for percent
        var percentValue = System.Math.Abs(value) * 100;
        percentValue = ApplyRounding(percentValue, MaximumFractionDigits);
        var displaysAsZero = percentValue == 0;

        // Determine if we should show a sign based on signDisplay
        var showNegativeSign = isNegative;
        var showPositiveSign = false;

        switch (SignDisplay)
        {
            case "always":
                showPositiveSign = !isNegative;
                break;
            case "exceptZero":
                showPositiveSign = !isNegative && !displaysAsZero;
                showNegativeSign = isNegative && !displaysAsZero;
                break;
            case "negative":
                showPositiveSign = false;
                showNegativeSign = isNegative && !displaysAsZero;
                break;
            case "never":
                showNegativeSign = false;
                showPositiveSign = false;
                break;
        }

        // Get the percent pattern to determine symbol position and spacing
        var pattern = NumberFormatInfo.PercentPositivePattern;

        // Determine if symbol comes before or after, and if there's spacing
        // Positive patterns: 0="n %", 1="n%", 2="%n", 3="% n"
        var symbolAfter = pattern == 0 || pattern == 1;
        var hasSpace = pattern == 0 || pattern == 3;

        if (!symbolAfter)
        {
            // Symbol before number
            parts.Add(new NumberFormatPart("percentSign", NumberFormatInfo.PercentSymbol));
            if (hasSpace)
            {
                parts.Add(new NumberFormatPart("literal", " "));
            }
        }

        if (showNegativeSign)
        {
            parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
        }
        else if (showPositiveSign)
        {
            parts.Add(new NumberFormatPart("plusSign", NumberFormatInfo.PositiveSign));
        }

        var integerPart = (long) System.Math.Truncate(percentValue);
        var fractionValue = percentValue - integerPart;

        FormatIntegerToParts(parts, integerPart);

        if (MinimumFractionDigits > 0 || (fractionValue > 0 && MaximumFractionDigits > 0))
        {
            parts.Add(new NumberFormatPart("decimal", NumberFormatInfo.NumberDecimalSeparator));
            FormatFractionToParts(parts, fractionValue);
        }

        if (symbolAfter)
        {
            // Symbol after number
            if (hasSpace)
            {
                parts.Add(new NumberFormatPart("literal", " "));
            }
            parts.Add(new NumberFormatPart("percentSign", NumberFormatInfo.PercentSymbol));
        }

        return parts;
    }

    private List<NumberFormatPart> FormatUnitToParts(double value)
    {
        var parts = new List<NumberFormatPart>();
        var isNegative = value < 0 || double.IsNegativeInfinity(1 / value); // Handles -0
        var absValue = System.Math.Abs(value);

        // Apply rounding first to determine if value displays as zero
        absValue = ApplyRounding(absValue, MaximumFractionDigits);
        var displaysAsZero = absValue == 0;

        // Determine if we should show a sign based on signDisplay
        var showNegativeSign = isNegative;
        var showPositiveSign = false;

        switch (SignDisplay)
        {
            case "always":
                showPositiveSign = !isNegative;
                break;
            case "exceptZero":
                showPositiveSign = !isNegative && !displaysAsZero;
                showNegativeSign = isNegative && !displaysAsZero;
                break;
            case "negative":
                showPositiveSign = false;
                showNegativeSign = isNegative && !displaysAsZero;
                break;
            case "never":
                showNegativeSign = false;
                showPositiveSign = false;
                break;
        }

        if (showNegativeSign)
        {
            parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
        }
        else if (showPositiveSign)
        {
            parts.Add(new NumberFormatPart("plusSign", NumberFormatInfo.PositiveSign));
        }

        var integerPart = (long) System.Math.Truncate(absValue);
        var fractionValue = absValue - integerPart;

        FormatIntegerToParts(parts, integerPart);

        if (MinimumFractionDigits > 0 || (fractionValue > 0 && MaximumFractionDigits > 0))
        {
            parts.Add(new NumberFormatPart("decimal", NumberFormatInfo.NumberDecimalSeparator));
            FormatFractionToParts(parts, fractionValue);
        }

        // Add space and unit using CLDR pattern
        var unitDisplay = UnitDisplay ?? "short";
        var unitStr = Unit ?? "";

        // Try to get unit patterns from CLDR provider
        var unitPatterns = CldrProvider.GetUnitPatterns(Locale, unitStr, unitDisplay);
        if (unitPatterns != null)
        {
            // Extract unit portion from pattern by removing {0} placeholder
            // Select singular or plural pattern based on the absolute value
            var isSingular = System.Math.Abs(value) == 1;
            var pattern = isSingular ? (unitPatterns.One ?? unitPatterns.Other) : unitPatterns.Other;
            var placeholderIndex = pattern.IndexOf("{0}", StringComparison.Ordinal);

            if (placeholderIndex >= 0)
            {
                // Pattern has format like "{0} km/h" or "時速 {0} キロメートル"
                var beforeNumber = pattern.Substring(0, placeholderIndex);
                var afterNumber = pattern.Substring(placeholderIndex + 3);

                // The afterNumber part contains the unit (possibly with space)
                if (!string.IsNullOrEmpty(afterNumber))
                {
                    // Check if it starts with a space
                    if (afterNumber[0] == ' ')
                    {
                        parts.Add(new NumberFormatPart("literal", " "));
                        parts.Add(new NumberFormatPart("unit", afterNumber.Substring(1)));
                    }
                    else
                    {
                        parts.Add(new NumberFormatPart("unit", afterNumber));
                    }
                }
            }
        }
        else
        {
            // Fallback to legacy behavior
            var unitSuffix = GetUnitSuffix(unitStr, unitDisplay, absValue);

            // Determine if we need a space before the unit
            var needsSpace = !string.Equals(unitDisplay, "narrow", StringComparison.Ordinal) &&
                            !string.Equals(unitStr, "percent", StringComparison.Ordinal) &&
                            !string.Equals(unitStr, "celsius", StringComparison.Ordinal) &&
                            !string.Equals(unitStr, "fahrenheit", StringComparison.Ordinal);

            if (needsSpace)
            {
                parts.Add(new NumberFormatPart("literal", " "));
            }
            parts.Add(new NumberFormatPart("unit", unitSuffix));
        }

        return parts;
    }

    private bool ShouldShowPlusSign(double value)
    {
        if (value <= 0) return false;

        return SignDisplay switch
        {
            "always" => true,
            "exceptZero" => value != 0,
            _ => false
        };
    }
}
