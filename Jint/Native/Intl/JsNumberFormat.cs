using System.Globalization;
using System.Numerics;
using Jint.Native.Object;

namespace Jint.Native.Intl;

/// <summary>
/// Represents a part of a formatted number for formatToParts.
/// </summary>
internal readonly record struct NumberFormatPart(string Type, string Value);

/// <summary>
/// A value https://tc39.es/ecma402/#sec-tointlmathematicalvalue read, which is a mathematical value and
/// not a <see cref="double"/> — so a BigInt and a decimal string keep every digit they were written with.
/// </summary>
/// <remarks>
/// Both lanes take one of these. https://tc39.es/ecma402/#sec-formatnumber and
/// https://tc39.es/ecma402/#sec-formatnumbertoparts are the same
/// https://tc39.es/ecma402/#sec-partitionnumberpattern over the same argument, so a value one of them reads
/// exactly is a value the other reads exactly; <see cref="Number"/> is what both fall back to, together,
/// for a formatter with no exact lane for it.
/// </remarks>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct IntlMathematicalValue
{
    private IntlMathematicalValue(double number, BigInteger mantissa, int fractionDigits, bool isExact)
    {
        Number = number;
        Mantissa = mantissa;
        FractionDigits = fractionDigits;
        IsExact = isExact;
    }

    /// <summary>A value that is only ever a double — everything ToNumber was enough for.</summary>
    internal static IntlMathematicalValue Of(double number) => new(number, BigInteger.Zero, 0, isExact: false);

    /// <summary>A value read exactly, as <paramref name="mantissa"/> × 10^-<paramref name="fractionDigits"/>.</summary>
    internal static IntlMathematicalValue Exact(BigInteger mantissa, int fractionDigits)
        => new(ToDouble(mantissa, fractionDigits), mantissa, fractionDigits, isExact: true);

    internal bool IsExact { get; }

    /// <summary>The double this value rounds to, which is what a lane with no exact route for it writes.</summary>
    internal double Number { get; }

    internal BigInteger Mantissa { get; }

    internal int FractionDigits { get; }

    private static double ToDouble(BigInteger mantissa, int fractionDigits)
    {
        if (fractionDigits <= 0)
        {
            return (double) mantissa;
        }

        // Math.Pow(10, n) keeps any compounded rounding to a single division step, instead of
        // accumulating it across `fractionDigits` separate divides.
        return (double) mantissa / System.Math.Pow(10, fractionDigits);
    }
}

/// <summary>
/// https://tc39.es/ecma402/#numberformat-objects
/// Represents an Intl.NumberFormat instance with locale-aware number formatting.
/// </summary>
internal sealed class JsNumberFormat : ObjectInstance
{
    internal JsNumberFormat(
        Engine engine,
        ObjectInstance prototype,
        string locale,
        in Data.ResolvedNumberingSystem numberingSystem,
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
        _numberingSystem = numberingSystem;
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

    private readonly Data.ResolvedNumberingSystem _numberingSystem;

    internal string Locale { get; }
    internal string NumberingSystem => _numberingSystem.Name;

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
    /// <remarks>
    /// https://tc39.es/ecma402/#sec-formatnumber is the concatenation of exactly the parts
    /// https://tc39.es/ecma402/#sec-formatnumbertoparts returns, so this reads that list rather than
    /// assembling a second one. Every lane that assembled a second one disagreed with the parts somewhere:
    /// the decimal lane handed its value to <c>double.ToString</c>, which rounds a custom format string at
    /// fifteen significant digits where https://tc39.es/ecma402/#sec-tointlmathematicalvalue reads
    /// seventeen, so <c>format(12345678901234567890)</c> wrote <c>…234,600,000</c> against the parts'
    /// <c>…567,000</c>.
    /// </remarks>
    internal string Format(double value) => ConcatenateParts(FormatToParts(value));

    private static string ConcatenateParts(List<NumberFormatPart> parts)
    {
        if (parts.Count == 1)
        {
            return parts[0].Value;
        }

        var builder = new System.Text.StringBuilder();
        foreach (var part in parts)
        {
            builder.Append(part.Value);
        }

        return builder.ToString();
    }

    /// <summary>Distinguishes -0 from +0, which format differently but compare equal.</summary>
    private static bool IsNegativeZero(double value) => value == 0 && double.IsNegativeInfinity(1 / value);

    /// <summary>
    /// Applies rounding to a value based on the RoundingMode and RoundingIncrement settings.
    /// </summary>
    private double ApplyRounding(double value, int decimalPlaces)
    {
        if (!double.IsFinite(value))
        {
            return value;
        }

        // A double from 2^53 up carries no fraction at all, so rounding one is the identity — and
        // scaling it by a power of ten to round it is not, since the product is past what a double
        // holds exactly: 1e21 came back as 999999999999999900000.
        if (decimalPlaces > 0 && System.Math.Abs(value) >= 9007199254740992d)
        {
            return value;
        }

        // The same identity, at the other end: rounding at a place below the value's last significant
        // digit changes nothing, and scaling by 10^decimalPlaces to do it anyway does — 100000 multiplied
        // by 10^20 and divided back is 100000.00000000001, which maximumFractionDigits: 20 then wrote out.
        // Where the last digit sits is read from Number::toString, which is where this object's digits come
        // from everywhere else. A rounding increment is excluded because it is not the identity there:
        // it rounds to a multiple at that place whether or not the value has a digit in it.
        if (decimalPlaces > 0 && RoundingIncrement == 1 && value != 0 && IsRoundedAlready(value, decimalPlaces))
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

    /// <summary>
    /// Whether <paramref name="value"/>'s last significant digit already sits at or above the
    /// <paramref name="decimalPlaces"/> place, so rounding it there leaves it alone.
    /// </summary>
    private static bool IsRoundedAlready(double value, int decimalPlaces)
    {
        DecimalDigitsOf(System.Math.Abs(value), out var significand, out var exponent);
        return exponent - significand.Length + 1 >= -decimalPlaces;
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
    /// https://tc39.es/ecma402/#sec-formatnumber, for a value read as a mathematical value rather than a
    /// double: the concatenation of exactly the parts <see cref="FormatToParts(in IntlMathematicalValue)"/>
    /// returns.
    /// </summary>
    internal string Format(in IntlMathematicalValue value)
    {
        if (!CanPartitionExactly(in value))
        {
            return Format(value.Number);
        }

        return ConcatenateParts(FormatToParts(in value));
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-formatnumbertoparts, for a value read as a mathematical value rather
    /// than a double.
    /// </summary>
    internal List<NumberFormatPart> FormatToParts(in IntlMathematicalValue value)
    {
        if (!CanPartitionExactly(in value))
        {
            return FormatToParts(value.Number);
        }

        return TransliterateParts(PartitionExact(value.Mantissa, value.FractionDigits));
    }

    /// <summary>
    /// Whether this formatter has a lane that writes <paramref name="value"/> without rounding it to a
    /// double first. One answer, read by both lanes, so neither can format a value the other approximated.
    /// </summary>
    /// <remarks>
    /// The exact lane covers the standard notation in every style, and only there: a notation writes a
    /// scaled mantissa or an abbreviation, https://tc39.es/ecma402/#sec-partitionnotationsubpattern picks
    /// it from the value's own magnitude, and the exact carrier does not do that arithmetic — so a value
    /// this lane would otherwise keep whole takes the double instead, and gets the exponent the same
    /// formatter writes for a Number. Scaling a fraction is the other case it hands over.
    /// </remarks>
    private bool CanPartitionExactly(in IntlMathematicalValue value)
    {
        if (!value.IsExact)
        {
            return false;
        }

        if (!string.Equals(Notation, "standard", StringComparison.Ordinal))
        {
            return false;
        }

        // Scaling a fraction is the one piece of arithmetic the exact carrier does not do yet.
        return value.FractionDigits == 0 || Style is not ("currency" or "percent" or "unit");
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-partitionnumberpattern over an exact
    /// mantissa × 10^-<paramref name="fractionDigits"/>.
    /// </summary>
    /// <remarks>
    /// The value's digits are the only thing this lane owns. Everything written around them — the currency
    /// pattern https://tc39.es/ecma402/#sec-getnumberformatpattern selects, its accounting form, the unit's
    /// two affixes, the percent sign and the sign itself — is the same pattern walk a
    /// <see cref="double"/> takes, so a value read exactly cannot be dressed differently from the value a
    /// Number of the same size is dressed in.
    /// </remarks>
    private List<NumberFormatPart> PartitionExact(BigInteger mantissa, int fractionDigits)
    {
        var isNegative = mantissa.Sign < 0;
        var abs = isNegative ? -mantissa : mantissa;

        if (string.Equals(Style, "percent", StringComparison.Ordinal))
        {
            // percent scales by 100, which the exact carrier does by moving the mantissa, not the point
            abs *= 100;
        }

        // https://tc39.es/ecma402/#sec-formatnumberstring, over digits rather than over a double: which
        // of the two roundings applies is the formatter's, not the carrier's.
        var body = MinimumSignificantDigits.HasValue || MaximumSignificantDigits.HasValue
            ? ExactPrecisionBody(abs, fractionDigits)
            : ExactBody(abs, fractionDigits);
        var sign = SignFor(isNegative, body.IsZero);

        var parts = new List<NumberFormatPart>();
        switch (Style)
        {
            case "currency":
                AppendCurrencyParts(parts, in body, sign.ShowNegative, sign.ShowPositive);
                break;
            case "percent":
                AppendPercentParts(parts, in body, sign.ShowNegative, sign.ShowPositive);
                break;
            case "unit":
                AppendUnitParts(parts, in body, sign.ShowNegative, sign.ShowPositive, (double) mantissa);
                break;
            default:
                AppendSignPart(parts, sign.ShowNegative, sign.ShowPositive);
                AddNumberParts(parts, in body);
                break;
        }

        return parts;
    }

    /// <summary>
    /// The digits https://tc39.es/ecma402/#sec-torawfixed writes for a non-negative exact
    /// <paramref name="absMantissa"/> × 10^-<paramref name="fractionDigits"/>: rounded at
    /// <see cref="MaximumFractionDigits"/> with halfExpand, then trimmed of up to
    /// <c>maximumFractionDigits - minimumFractionDigits</c> trailing zeros.
    /// </summary>
    private NumberBody ExactBody(BigInteger absMantissa, int fractionDigits)
    {
        if (fractionDigits > MaximumFractionDigits)
        {
            var divisor = BigInteger.Pow(10, fractionDigits - MaximumFractionDigits);
            absMantissa = (absMantissa + divisor / 2) / divisor;
            fractionDigits = MaximumFractionDigits;
        }

        var digits = absMantissa.ToString("R", CultureInfo.InvariantCulture);

        string integerStr;
        string fractionStr;
        if (fractionDigits == 0)
        {
            integerStr = digits;
            fractionStr = string.Empty;
        }
        else if (digits.Length <= fractionDigits)
        {
            integerStr = "0";
            fractionStr = digits.PadLeft(fractionDigits, '0');
        }
        else
        {
            integerStr = digits.Substring(0, digits.Length - fractionDigits);
            fractionStr = digits.Substring(digits.Length - fractionDigits);
        }

        var minFrac = MinimumFractionDigits;
        if (fractionStr.Length > minFrac)
        {
            var keep = fractionStr.Length;
            while (keep > minFrac && fractionStr[keep - 1] == '0')
            {
                keep--;
            }

            fractionStr = fractionStr.Substring(0, keep);
        }
        else if (fractionStr.Length < minFrac)
        {
            fractionStr = fractionStr.PadRight(minFrac, '0');
        }

        return NumberBody.Finite(integerStr, fractionStr);
    }

    /// <summary>
    /// Formats a BigInteger according to the formatter's locale and options.
    /// </summary>
    internal string Format(BigInteger value) => Format(IntlMathematicalValue.Exact(value, 0));

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
    /// <remarks>
    /// <para>
    /// https://tc39.es/ecma402/#sec-formatnumber is defined as the concatenation of exactly the parts
    /// https://tc39.es/ecma402/#sec-formatnumbertoparts returns, and <see cref="Format(double)"/> is now
    /// that concatenation, so <c>[[NumberingSystem]]</c>'s digits — and every other character — reach both
    /// lanes or neither. <c>IntlNumberFormatPartsTests.PartsConcatenateToFormat</c> still walks its grid of
    /// locales, numbering systems and styles: what it asserts is no longer that two assemblies agree but
    /// that this one assembly never throws or drops a part anywhere in the grid.
    /// </para>
    /// </remarks>
    internal List<NumberFormatPart> FormatToParts(double value) => TransliterateParts(FormatToPartsCore(value));

    /// <summary>
    /// Rewrites every part that carries the number itself in <c>[[NumberingSystem]]</c>, leaving the
    /// pattern text alone.
    /// </summary>
    private List<NumberFormatPart> TransliterateParts(List<NumberFormatPart> parts)
    {
        if (!_numberingSystem.RewritesDigits)
        {
            return parts;
        }

        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            var transliterated = TransliterateNumericPart(part.Type, part.Value);
            if (!ReferenceEquals(transliterated, part.Value))
            {
                parts[i] = new NumberFormatPart(part.Type, transliterated);
            }
        }

        return parts;
    }

    /// <summary>
    /// Rewrites one part's value in this formatter's numbering system, for the part types
    /// https://tc39.es/ecma402/#sec-partitionnumberpattern fills from the number's own digits. A currency
    /// symbol, a unit name and a literal are pattern text, not a number, and keep the characters the pattern
    /// gave them.
    /// </summary>
    /// <remarks>
    /// <c>decimal</c> is a separator rather than a digit, and is here because a numbering system can carry
    /// one of its own — <c>arab</c> writes U+066B. <c>group</c> is a separator the system has no opinion
    /// about, and keeps the locale's.
    /// </remarks>
    private string TransliterateNumericPart(string type, string value)
    {
        if (string.Equals(type, "decimal", StringComparison.Ordinal))
        {
            return _numberingSystem.RewritesDecimalSeparator ? _numberingSystem.DecimalSeparator.ToString() : value;
        }

        return type switch
        {
            "integer" or "fraction" or "exponentInteger" => _numberingSystem.TransliterateDigitsOnly(value),
            _ => value
        };
    }

    /// <summary>
    /// What a pattern walk writes where https://tc39.es/ecma402/#sec-partitionnumberpattern puts its
    /// <c>number</c> part: the digits https://tc39.es/ecma402/#sec-formatnumberstring produced, or the
    /// single part https://tc39.es/ecma402/#sec-partitionnotationsubpattern makes of NaN and infinity.
    /// </summary>
    /// <remarks>
    /// The digits are carried as the two strings ToRawFixed wrote and not as a <c>long</c> plus a
    /// <c>double</c>, because the same pattern walk has to serve a value read exactly — a BigInt, a long
    /// decimal string — as well as one read as a <see cref="double"/>, and an exact value's digits are
    /// not a <c>long</c>. <see cref="FractionDigits"/> is empty when ToRawFixed's last two steps removed
    /// every one of them, which is what decides whether a decimal separator is written at all: neither
    /// lane writes a separator with nothing after it.
    /// </remarks>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct NumberBody(string IntegerDigits, string FractionDigits, NumberFormatPart? NonFinite)
    {
        internal static NumberBody Finite(string integerDigits, string fractionDigits) => new(integerDigits, fractionDigits, null);

        internal static NumberBody Of(NumberFormatPart nonFinite) => new("", "", nonFinite);

        /// <summary>Whether every digit written is a zero, which is what <c>signDisplay</c> reads.</summary>
        internal bool IsZero
        {
            get
            {
                foreach (var c in IntegerDigits)
                {
                    if (c != '0')
                    {
                        return false;
                    }
                }

                foreach (var c in FractionDigits)
                {
                    if (c != '0')
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }

    /// <summary>
    /// The digits https://tc39.es/ecma402/#sec-torawfixed writes for a non-negative value already rounded
    /// at <see cref="MaximumFractionDigits"/>.
    /// </summary>
    /// <remarks>
    /// The value's digits are read once, from <c>Number::toString</c>, and then split and trimmed by the
    /// same operation an exactly-read value takes — so a double and a decimal string of the same digits
    /// cannot be split differently. Splitting a double instead, with <c>Truncate</c> and a fraction scaled
    /// by 10^maximumFractionDigits, wrote fractions the value does not have: at
    /// <c>maximumFractionDigits: 20</c> the fraction of 1.0000000000000001 came out
    /// <c>"00000000000000022204"</c>, which is the binary expansion and not the number.
    /// </remarks>
    private NumberBody FiniteBody(double roundedAbsValue)
    {
        if (roundedAbsValue == 0)
        {
            return ExactBody(BigInteger.Zero, 0);
        }

        DecimalDigitsOf(roundedAbsValue, out var significand, out var exponent);

        // significand × 10^scale is the value, so a non-negative scale is a whole number written out and a
        // negative one is that many fraction digits
        var scale = exponent - significand.Length + 1;
        var mantissa = BigInteger.Parse(significand, CultureInfo.InvariantCulture);

        return scale >= 0
            ? ExactBody(mantissa * BigInteger.Pow(10, scale), 0)
            : ExactBody(mantissa, -scale);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-formatnumberstring over a finite value: the digits, before any pattern
    /// is written around them.
    /// </summary>
    /// <remarks>
    /// The specification computes these once, for every style — the style decides only what is written
    /// around them. Jint had four copies over a <see cref="double"/> and three of them had no
    /// significant-digit route at all, so <c>maximumSignificantDigits</c> was read under the decimal style
    /// and silently ignored under a currency, a percent and a unit. The value keeps its sign here even
    /// though only its digits come back, because <c>GetUnsignedRoundingMode</c> reads it: <c>ceil</c>
    /// rounds a positive value's magnitude up and a negative one's down.
    /// </remarks>
    private NumberBody FormatNumericToString(double value)
    {
        if (string.Equals(RoundingPriority, "auto", StringComparison.Ordinal))
        {
            return MinimumSignificantDigits.HasValue || MaximumSignificantDigits.HasValue
                ? RawPrecisionBody(value, out _)
                : RawFixedBody(value);
        }

        // more-precision and less-precision compute both and keep one, chosen by which rounded further:
        // ToRawFixed's magnitude is -maximumFractionDigits and ToRawPrecision's is e - p + 1.
        var significant = RawPrecisionBody(value, out var significantMagnitude);
        var fixedIsMorePrecise = -MaximumFractionDigits < significantMagnitude;
        var takeFixed = string.Equals(RoundingPriority, "morePrecision", StringComparison.Ordinal)
            ? fixedIsMorePrecise
            : !fixedIsMorePrecise;

        return takeFixed ? RawFixedBody(value) : significant;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-torawfixed over a finite value.
    /// </summary>
    private NumberBody RawFixedBody(double value)
        => FiniteBody(System.Math.Abs(ApplyRounding(value, MaximumFractionDigits)));

    /// <summary>
    /// https://tc39.es/ecma402/#sec-torawprecision over a finite value, reporting the
    /// <c>[[RoundingMagnitude]]</c> the priority comparison reads.
    /// </summary>
    private NumberBody RawPrecisionBody(double value, out int roundingMagnitude)
    {
        var minSig = MinimumSignificantDigits ?? 1;
        var maxSig = MaximumSignificantDigits ?? 21;
        var absValue = System.Math.Abs(value);

        if (absValue != 0)
        {
            var magnitude = (int) System.Math.Floor(System.Math.Log10(absValue));
            var decimalPlaces = maxSig - magnitude - 1;

            double rounded;
            if (decimalPlaces >= 0)
            {
                rounded = ApplyRounding(value, decimalPlaces);
            }
            else
            {
                var divisor = System.Math.Pow(10, -decimalPlaces);
                rounded = ApplyRounding(value / divisor, 0) * divisor;
            }

            absValue = System.Math.Abs(rounded);
        }

        if (absValue == 0)
        {
            roundingMagnitude = 1 - maxSig;
            return RawPrecision("0", 0, minSig, maxSig);
        }

        DecimalDigitsOf(absValue, out var significand, out var exponent);
        roundingMagnitude = exponent - maxSig + 1;
        return RawPrecision(significand, exponent, minSig, maxSig);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-torawprecision over an exact
    /// <paramref name="absMantissa"/> × 10^-<paramref name="fractionDigits"/>.
    /// </summary>
    private NumberBody ExactPrecisionBody(BigInteger absMantissa, int fractionDigits)
    {
        var minSig = MinimumSignificantDigits ?? 1;
        var maxSig = MaximumSignificantDigits ?? 21;

        if (absMantissa.IsZero)
        {
            return RawPrecision("0", 0, minSig, maxSig);
        }

        var digits = absMantissa.ToString("R", CultureInfo.InvariantCulture);
        var end = digits.Length;
        while (end > 1 && digits[end - 1] == '0')
        {
            end--;
        }

        return RawPrecision(digits.Substring(0, end), digits.Length - 1 - fractionDigits, minSig, maxSig);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-torawprecision's last steps: <paramref name="significand"/> × 10^
    /// <paramref name="exponent"/> written out to <paramref name="maxSig"/> digits, split at the decimal
    /// point, and stripped of up to <c>maxSig - minSig</c> trailing zeros.
    /// </summary>
    /// <remarks>
    /// The trim is the step the parts lane never had, which is why <c>maximumSignificantDigits: 2</c> wrote
    /// <c>"0.40"</c> where the string lane wrote <c>"0.4"</c>.
    /// </remarks>
    private static NumberBody RawPrecision(string significand, int exponent, int minSig, int maxSig)
    {
        var m = significand.Length > maxSig
            ? RoundDigits(significand, maxSig, ref exponent)
            : significand.PadRight(maxSig, '0');

        string integerStr;
        string fractionStr;
        if (exponent >= maxSig - 1)
        {
            integerStr = m + new string('0', exponent - maxSig + 1);
            fractionStr = string.Empty;
        }
        else if (exponent >= 0)
        {
            integerStr = m.Substring(0, exponent + 1);
            fractionStr = m.Substring(exponent + 1);
        }
        else
        {
            integerStr = "0";
            fractionStr = new string('0', -(exponent + 1)) + m;
        }

        var cut = maxSig - minSig;
        var keep = fractionStr.Length;
        while (cut > 0 && keep > 0 && fractionStr[keep - 1] == '0')
        {
            keep--;
            cut--;
        }

        return NumberBody.Finite(integerStr, keep == fractionStr.Length ? fractionStr : fractionStr.Substring(0, keep));
    }

    /// <summary>Keeps the leading <paramref name="keep"/> digits, rounding half away from zero.</summary>
    private static string RoundDigits(string digits, int keep, ref int exponent)
    {
        var kept = digits.Substring(0, keep).ToCharArray();
        if (digits[keep] < '5')
        {
            return new string(kept);
        }

        for (var i = keep - 1; i >= 0; i--)
        {
            if (kept[i] != '9')
            {
                kept[i]++;
                return new string(kept);
            }

            kept[i] = '0';
        }

        // every digit was a nine, so the carry adds one to the magnitude
        exponent++;
        return "1".PadRight(keep, '0');
    }

    /// <summary>
    /// A non-negative finite <see cref="double"/> as significant digits and the exponent of the first of
    /// them, which is how https://tc39.es/ecma402/#sec-tointlmathematicalvalue reads a Number: the digits
    /// of <c>Number::toString</c>, the shortest decimal that reads back as this double.
    /// </summary>
    /// <remarks>
    /// Reading the double's own binary expansion instead is what made <c>minimumSignificantDigits: 3</c>
    /// write <c>"0.400000000000000022204"</c> for <c>0.4</c>: the Intl mathematical value of the Number
    /// <c>0.4</c> is exactly four tenths, because the specification reads it through
    /// <c>Number::toString</c> before it reads it as a mathematical value at all.
    /// </remarks>
    private static void DecimalDigitsOf(double absValue, out string significand, out int exponent)
    {
        var text = Number.NumberPrototype.ToNumberString(absValue);

        var exponentIndex = text.IndexOf('e');
        var written = 0;
        if (exponentIndex >= 0)
        {
            written = int.Parse(text.AsSpan(exponentIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            text = text.Substring(0, exponentIndex);
        }

        var point = text.IndexOf('.');
        var digits = point < 0 ? text : text.Remove(point, 1);
        var integerLength = point < 0 ? text.Length : point;

        var first = 0;
        while (first < digits.Length - 1 && digits[first] == '0')
        {
            first++;
        }

        var last = digits.Length;
        while (last > first + 1 && digits[last - 1] == '0')
        {
            last--;
        }

        significand = digits.Substring(first, last - first);
        exponent = integerLength - 1 - first + written;
    }

    private List<NumberFormatPart> FormatToPartsCore(double value)
    {
        if (!double.IsFinite(value))
        {
            return FormatNonFiniteToParts(value);
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

    /// <summary>
    /// Formats NaN or an infinity, which chooses only the number's own text.
    /// </summary>
    /// <remarks>
    /// https://tc39.es/ecma402/#sec-partitionnumberpattern's non-finite branches set nothing but
    /// <c>formattedString</c>: the pattern https://tc39.es/ecma402/#sec-getnumberformatpattern selects is
    /// still selected and still walked, so the currency symbol, the unit and the percent sign are written
    /// around it exactly as they are around a finite value. That algorithm reads NaN as
    /// <c>positive-zero</c> and each infinity as the non-zero category of its own sign, which is what
    /// decides the sign below. https://tc39.es/ecma402/#sec-partitionnotationsubpattern then makes the
    /// number one part and never a notation sub-pattern, so no exponent is written for it either.
    /// </remarks>
    private List<NumberFormatPart> FormatNonFiniteToParts(double value)
    {
        var parts = new List<NumberFormatPart>();

        var isNaN = double.IsNaN(value);
        var body = NumberBody.Of(isNaN
            ? new NumberFormatPart("nan", NumberFormatInfo.NaNSymbol)
            : new NumberFormatPart("infinity", NumberFormatInfo.PositiveInfinitySymbol));

        var isNegative = double.IsNegativeInfinity(value);
        var showNegativeSign = isNegative && !string.Equals(SignDisplay, "never", StringComparison.Ordinal);
        var showPositiveSign = !isNegative && SignDisplay switch
        {
            "always" => true,
            // NaN is the zero category, so "exceptZero" leaves it unsigned while +∞ takes a plus
            "exceptZero" => !isNaN,
            _ => false
        };

        switch (Style)
        {
            case "currency":
                AppendCurrencyParts(parts, in body, showNegativeSign, showPositiveSign);
                break;
            case "percent":
                AppendPercentParts(parts, in body, showNegativeSign, showPositiveSign);
                break;
            case "unit":
                AppendUnitParts(parts, in body, showNegativeSign, showPositiveSign, value);
                break;
            default:
                AppendSignPart(parts, showNegativeSign, showPositiveSign);
                AddNumberParts(parts, in body);
                break;
        }

        return parts;
    }

    /// <summary>Which of the two signs a pattern writes around a finite value.</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct SignDecision(bool ShowNegative, bool ShowPositive);

    /// <summary>
    /// Which sign https://tc39.es/ecma402/#sec-getnumberformatpattern selects a pattern for, read from
    /// <c>[[SignDisplay]]</c> and from whether the digits that were produced are all zeros.
    /// </summary>
    /// <remarks>
    /// One decision for every style and every notation, because the algorithm makes it once: the pattern
    /// is chosen before it is walked, and what is walked around is the only thing a style changes.
    /// </remarks>
    private SignDecision SignFor(bool isNegative, bool displaysAsZero) => SignDisplay switch
    {
        "always" => new SignDecision(isNegative, !isNegative),
        "exceptZero" => new SignDecision(isNegative && !displaysAsZero, !isNegative && !displaysAsZero),
        "negative" => new SignDecision(isNegative && !displaysAsZero, false),
        "never" => new SignDecision(false, false),
        _ => new SignDecision(isNegative, false)
    };

    /// <remarks>
    /// ECMA-402 calls the sign "the ILND String representing the minus sign", so it is the locale's own
    /// datum in every lane: <c>ar</c> prefixes U+061C ARABIC LETTER MARK to it.
    /// </remarks>
    private void AppendSignPart(List<NumberFormatPart> parts, bool showNegativeSign, bool showPositiveSign)
    {
        if (showNegativeSign)
        {
            parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
        }
        else if (showPositiveSign)
        {
            parts.Add(new NumberFormatPart("plusSign", NumberFormatInfo.PositiveSign));
        }
    }

    /// <summary>
    /// Writes the number a pattern wraps, using the locale's number decimal separator.
    /// </summary>
    private void AddNumberParts(List<NumberFormatPart> parts, in NumberBody body)
    {
        if (body.NonFinite is { } nonFinite)
        {
            parts.Add(nonFinite);
            return;
        }

        FormatIntegerToParts(parts, body.IntegerDigits);

        if (body.FractionDigits.Length > 0)
        {
            parts.Add(new NumberFormatPart("decimal", NumberFormatInfo.NumberDecimalSeparator));
            parts.Add(new NumberFormatPart("fraction", body.FractionDigits));
        }
    }

    /// <summary>
    /// Writes the number a currency pattern wraps, using the locale's currency decimal separator.
    /// </summary>
    private void AddCurrencyNumberParts(List<NumberFormatPart> parts, in NumberBody body)
    {
        if (body.NonFinite is { } nonFinite)
        {
            parts.Add(nonFinite);
            return;
        }

        FormatIntegerToParts(parts, body.IntegerDigits);

        if (body.FractionDigits.Length > 0)
        {
            parts.Add(new NumberFormatPart("decimal", NumberFormatInfo.CurrencyDecimalSeparator));
            parts.Add(new NumberFormatPart("fraction", body.FractionDigits));
        }
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-partitionnumberpattern under a notation: the exponent
    /// https://tc39.es/ecma402/#sec-computeexponent picks, and the digits
    /// https://tc39.es/ecma402/#sec-formatnumberstring writes for the mantissa it scales the value down to.
    /// </summary>
    /// <remarks>
    /// The mantissa is an ordinary number to that operation, so a digit option applies to it exactly as it
    /// applies to a standard-notation number. None of the three notations read one: each rounded a mantissa
    /// of its own at <c>maximumFractionDigits</c> and never looked at <c>[[MaximumSignificantDigits]]</c>,
    /// so <c>{ notation: "scientific", maximumSignificantDigits: 2 }</c> wrote <c>"1.235E4"</c> for 12345
    /// where every other engine writes <c>"1.2E4"</c>. Compact then split its own mantissa with a cast to
    /// <c>long</c>, which .NET pins instead of throwing, so every value from 2^63 up wrote
    /// <see cref="long.MaxValue"/>'s digits and <c>1e300</c> came out <c>"9223372036854775807T"</c>.
    /// </remarks>
    private List<NumberFormatPart> FormatNotationToParts(double value)
    {
        var parts = new List<NumberFormatPart>();

        // negative zero takes the negative pattern too, per https://tc39.es/ecma402/#sec-getnumberformatpattern
        var isNegative = value < 0 || IsNegativeZero(value);
        var absValue = System.Math.Abs(value);

        var exponent = ComputeExponent(absValue, out var compactSuffix);
        var body = FormatNumericToString(ScaleDown(absValue, exponent));

        var sign = SignFor(isNegative, body.IsZero);
        AppendSignPart(parts, sign.ShowNegative, sign.ShowPositive);

        if (exponent == 0)
        {
            // nothing was scaled, so the number is written by the standard sub-pattern — the one that groups
            AddNumberParts(parts, in body);
        }
        else
        {
            AddNotationNumberParts(parts, in body);
        }

        if (string.Equals(Notation, "compact", StringComparison.Ordinal))
        {
            AppendCompactSuffix(parts, compactSuffix);
            return parts;
        }

        parts.Add(new NumberFormatPart("exponentSeparator", "E"));

        if (exponent < 0)
        {
            parts.Add(new NumberFormatPart("exponentMinusSign", NumberFormatInfo.NegativeSign));
        }

        parts.Add(new NumberFormatPart(
            "exponentInteger",
            System.Math.Abs(exponent).ToString(CultureInfo.InvariantCulture)));

        return parts;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-computeexponent: the power of ten this notation scales
    /// <paramref name="absValue"/> by, together with the abbreviation compact notation writes after it.
    /// </summary>
    /// <remarks>
    /// The last steps are the reason this is not a table lookup: rounding the mantissa can carry it into
    /// the next magnitude, and the exponent is then the one that magnitude asks for. 999999 scaled by a
    /// thousand rounds to 1000, which is one million — <c>"1M"</c>, where Jint wrote <c>"1000K"</c>.
    /// </remarks>
    private int ComputeExponent(double absValue, out string compactSuffix)
    {
        compactSuffix = string.Empty;

        if (absValue == 0 || string.Equals(Notation, "standard", StringComparison.Ordinal))
        {
            return 0;
        }

        // the magnitude of the Intl mathematical value, which https://tc39.es/ecma402/#sec-tointlmathematicalvalue
        // reads through Number::toString rather than through a logarithm
        DecimalDigitsOf(absValue, out _, out var magnitude);

        var exponent = ComputeExponentForMagnitude(magnitude, out compactSuffix);
        var body = FormatNumericToString(ScaleDown(absValue, exponent));
        if (body.IsZero || MagnitudeOf(in body) == magnitude - exponent)
        {
            return exponent;
        }

        return ComputeExponentForMagnitude(magnitude + 1, out compactSuffix);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-computeexponentformagnitude, whose compact branch is the locale's own
    /// datum and comes from <see cref="Data.CompactPatterns"/>.
    /// </summary>
    private int ComputeExponentForMagnitude(int magnitude, out string compactSuffix)
    {
        compactSuffix = string.Empty;

        switch (Notation)
        {
            case "scientific":
                return magnitude;
            case "engineering":
                return (int) (System.Math.Floor(magnitude / 3.0) * 3);
            case "compact":
                var isLong = string.Equals(CompactDisplay, "long", StringComparison.Ordinal);
                return Data.CompactPatterns.GetPatterns(Locale).CompactExponent(magnitude, isLong, out compactSuffix);
            default:
                return 0;
        }
    }

    /// <summary>
    /// <paramref name="value"/> × 10^-<paramref name="exponent"/>, in steps a <see cref="double"/> holds.
    /// </summary>
    /// <remarks>
    /// Scientific notation scales a value by its own magnitude, and the widest of those is wider than any
    /// power of ten a double carries: <c>Math.Pow(10, 324)</c> is already an infinity, so scaling 5e-324
    /// up in one step loses the number it was asked about. Every exponent inside ±300 divides exactly as it
    /// did before, which is every exponent the conformance suite reaches.
    /// </remarks>
    private static double ScaleDown(double value, int exponent)
    {
        const int Step = 300;

        while (exponent > Step)
        {
            value /= 1e300;
            exponent -= Step;
        }

        while (exponent < -Step)
        {
            value *= 1e300;
            exponent += Step;
        }

        return value / System.Math.Pow(10, exponent);
    }

    /// <summary>The power of ten of the first digit these digits carry.</summary>
    private static int MagnitudeOf(in NumberBody body)
    {
        var integerDigits = body.IntegerDigits;
        if (integerDigits.Length > 0 && integerDigits[0] != '0')
        {
            return integerDigits.Length - 1;
        }

        var fractionDigits = body.FractionDigits;
        for (var i = 0; i < fractionDigits.Length; i++)
        {
            if (fractionDigits[i] != '0')
            {
                return -(i + 1);
            }
        }

        return 0;
    }

    /// <summary>
    /// Writes a scaled mantissa, which a notation's sub-pattern does not group.
    /// </summary>
    /// <remarks>
    /// CLDR's compact patterns carry no group separator, so the mantissa of 1e21 in compact notation is
    /// <c>"1000000000"</c> and not <c>"1,000,000,000"</c> — and a mantissa is the only thing wide enough
    /// for the difference to show, since scientific and engineering keep theirs under four digits.
    /// </remarks>
    private void AddNotationNumberParts(List<NumberFormatPart> parts, in NumberBody body)
    {
        var integerDigits = body.IntegerDigits;
        if (integerDigits.Length < MinimumIntegerDigits)
        {
            integerDigits = integerDigits.PadLeft(MinimumIntegerDigits, '0');
        }

        parts.Add(new NumberFormatPart("integer", integerDigits));

        if (body.FractionDigits.Length > 0)
        {
            parts.Add(new NumberFormatPart("decimal", NumberFormatInfo.NumberDecimalSeparator));
            parts.Add(new NumberFormatPart("fraction", body.FractionDigits));
        }
    }

    /// <summary>
    /// Writes the <c>compactSymbol</c> or <c>compactName</c> https://tc39.es/ecma402/#sec-partitionnotationsubpattern
    /// puts after the number, and the literal the locale separates it with.
    /// </summary>
    private void AppendCompactSuffix(List<NumberFormatPart> parts, string compactSuffix)
    {
        if (compactSuffix.Length == 0)
        {
            return;
        }

        var patterns = Data.CompactPatterns.GetPatterns(Locale);
        var isLong = string.Equals(CompactDisplay, "long", StringComparison.Ordinal);

        if (isLong ? patterns.LongSpace : patterns.ShortSpace)
        {
            // a short abbreviation is separated by a non-breaking space, per CLDR
            parts.Add(new NumberFormatPart("literal", isLong ? " " : "\u00a0"));
        }

        parts.Add(new NumberFormatPart("compact", compactSuffix));
    }

    private List<NumberFormatPart> FormatDecimalToParts(double value)
    {
        var parts = new List<NumberFormatPart>();

        // Handle sign
        var isNegative = value < 0 || double.IsNegativeInfinity(1 / value); // Handles -0

        var body = FormatNumericToString(value);
        var sign = SignFor(isNegative, body.IsZero);

        AppendSignPart(parts, sign.ShowNegative, sign.ShowPositive);
        AddNumberParts(parts, in body);

        return parts;
    }

    private void FormatIntegerToParts(List<NumberFormatPart> parts, string intStr)
    {
        // Pad with zeros if needed
        if (intStr.Length < MinimumIntegerDigits)
        {
            intStr = intStr.PadLeft(MinimumIntegerDigits, '0');
        }

        if (!ShouldApplyGrouping(intStr.Length))
        {
            parts.Add(new NumberFormatPart("integer", intStr));
            return;
        }

        var separator = NumberFormatInfo.NumberGroupSeparator;
        var start = 0;
        foreach (var boundary in GroupBoundariesOf(intStr.Length))
        {
            parts.Add(new NumberFormatPart("integer", intStr.Substring(start, boundary - start)));
            parts.Add(new NumberFormatPart("group", separator));
            start = boundary;
        }

        parts.Add(new NumberFormatPart("integer", intStr.Substring(start)));
    }

    /// <summary>
    /// Where the locale puts a group separator in an integer of <paramref name="digitCount"/> digits, left
    /// to right.
    /// </summary>
    /// <remarks>
    /// A group is not always three digits: <c>en-IN</c> writes <c>12,34,567</c>, and
    /// <see cref="NumberFormatInfo.NumberGroupSizes"/> carries that as <c>[3, 2]</c> — the sizes apply from
    /// the right, and the last one repeats until a zero ends the grouping. Assuming three is the reason the
    /// parts lane wrote <c>1,234,567</c> where the string lane wrote <c>12,34,567</c>.
    /// </remarks>
    private List<int> GroupBoundariesOf(int digitCount)
    {
        var sizes = NumberFormatInfo.NumberGroupSizes;
        var boundaries = new List<int>();
        var position = digitCount;

        for (var i = 0; position > 0; i++)
        {
            var size = sizes.Length == 0 ? 0 : sizes[System.Math.Min(i, sizes.Length - 1)];
            if (size <= 0)
            {
                break;
            }

            position -= size;
            if (position <= 0)
            {
                break;
            }

            boundaries.Add(position);
        }

        boundaries.Reverse();
        return boundaries;
    }

    private List<NumberFormatPart> FormatCurrencyToParts(double value)
    {
        var parts = new List<NumberFormatPart>();
        var isNegative = value < 0 || double.IsNegativeInfinity(1 / value); // Handles -0

        // The digits are https://tc39.es/ecma402/#sec-formatnumberstring's, which is one operation for
        // every style: the currency pattern is written around them and never instead of them.
        var body = FormatNumericToString(value);
        var sign = SignFor(isNegative, body.IsZero);

        AppendCurrencyParts(parts, in body, sign.ShowNegative, sign.ShowPositive);

        return parts;
    }

    /// <summary>
    /// Walks the currency pattern https://tc39.es/ecma402/#sec-getnumberformatpattern selects, around
    /// whatever number stands in it.
    /// </summary>
    private void AppendCurrencyParts(List<NumberFormatPart> parts, in NumberBody body, bool showNegativeSign, bool showPositiveSign)
    {
        var currencySymbol = NumberFormatInfo.CurrencySymbol;
        var pattern = NumberFormatInfo.CurrencyPositivePattern;

        if (showNegativeSign && string.Equals(CurrencySign, "accounting", StringComparison.Ordinal))
        {
            // Use CLDR-based accounting format (parentheses for most locales)
            BuildAccountingCurrencyNegativeParts(parts, currencySymbol, in body);
            return;
        }

        AppendSignPart(parts, showNegativeSign, showPositiveSign);
        BuildCurrencyPositiveParts(parts, pattern, currencySymbol, in body);
    }

    /// <summary>
    /// Builds currency parts for accounting format (negative values with parentheses).
    /// </summary>
    private void BuildAccountingCurrencyNegativeParts(List<NumberFormatPart> parts, string symbol, in NumberBody body)
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
                    AddCurrencyNumberParts(parts, in body);
                    parts.Add(new NumberFormatPart("literal", ")"));
                    break;
                case 1: // n$ → (n$)
                    parts.Add(new NumberFormatPart("literal", "("));
                    AddCurrencyNumberParts(parts, in body);
                    parts.Add(new NumberFormatPart("currency", symbol));
                    parts.Add(new NumberFormatPart("literal", ")"));
                    break;
                case 2: // $ n → ($ n)
                    parts.Add(new NumberFormatPart("literal", "("));
                    parts.Add(new NumberFormatPart("currency", symbol));
                    parts.Add(new NumberFormatPart("literal", Nbsp));
                    AddCurrencyNumberParts(parts, in body);
                    parts.Add(new NumberFormatPart("literal", ")"));
                    break;
                case 3: // n $ → (n $)
                default:
                    parts.Add(new NumberFormatPart("literal", "("));
                    AddCurrencyNumberParts(parts, in body);
                    parts.Add(new NumberFormatPart("literal", Nbsp));
                    parts.Add(new NumberFormatPart("currency", symbol));
                    parts.Add(new NumberFormatPart("literal", ")"));
                    break;
            }
        }
        else
        {
            // Fall back to standard negative currency pattern (minus sign)
            BuildCurrencyNegativeParts(parts, symbol, in body);
        }
    }

    /// <summary>
    /// Builds currency parts for negative values using the locale's CurrencyNegativePattern.
    /// </summary>
    private void BuildCurrencyNegativeParts(List<NumberFormatPart> parts, string symbol, in NumberBody body)
    {
        var negPattern = NumberFormatInfo.CurrencyNegativePattern;
        const string Nbsp = "\u00A0"; // Non-breaking space per CLDR

        switch (negPattern)
        {
            case 0: // ($n)
                parts.Add(new NumberFormatPart("literal", "("));
                parts.Add(new NumberFormatPart("currency", symbol));
                AddCurrencyNumberParts(parts, in body);
                parts.Add(new NumberFormatPart("literal", ")"));
                break;
            case 1: // -$n
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                parts.Add(new NumberFormatPart("currency", symbol));
                AddCurrencyNumberParts(parts, in body);
                break;
            case 2: // $-n
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                AddCurrencyNumberParts(parts, in body);
                break;
            case 3: // $n-
                parts.Add(new NumberFormatPart("currency", symbol));
                AddCurrencyNumberParts(parts, in body);
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                break;
            case 4: // (n$)
                parts.Add(new NumberFormatPart("literal", "("));
                AddCurrencyNumberParts(parts, in body);
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("literal", ")"));
                break;
            case 5: // -n$
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                AddCurrencyNumberParts(parts, in body);
                parts.Add(new NumberFormatPart("currency", symbol));
                break;
            case 6: // n-$
                AddCurrencyNumberParts(parts, in body);
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                parts.Add(new NumberFormatPart("currency", symbol));
                break;
            case 7: // n$-
                AddCurrencyNumberParts(parts, in body);
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                break;
            case 8: // -n $
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                AddCurrencyNumberParts(parts, in body);
                parts.Add(new NumberFormatPart("literal", Nbsp));
                parts.Add(new NumberFormatPart("currency", symbol));
                break;
            case 9: // -$ n
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("literal", Nbsp));
                AddCurrencyNumberParts(parts, in body);
                break;
            case 10: // n $-
                AddCurrencyNumberParts(parts, in body);
                parts.Add(new NumberFormatPart("literal", Nbsp));
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                break;
            case 11: // $ n-
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("literal", Nbsp));
                AddCurrencyNumberParts(parts, in body);
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                break;
            case 12: // $ -n
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("literal", Nbsp));
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                AddCurrencyNumberParts(parts, in body);
                break;
            case 13: // n- $
                AddCurrencyNumberParts(parts, in body);
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                parts.Add(new NumberFormatPart("literal", Nbsp));
                parts.Add(new NumberFormatPart("currency", symbol));
                break;
            case 14: // ($ n)
                parts.Add(new NumberFormatPart("literal", "("));
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("literal", Nbsp));
                AddCurrencyNumberParts(parts, in body);
                parts.Add(new NumberFormatPart("literal", ")"));
                break;
            case 15: // (n $)
                parts.Add(new NumberFormatPart("literal", "("));
                AddCurrencyNumberParts(parts, in body);
                parts.Add(new NumberFormatPart("literal", Nbsp));
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("literal", ")"));
                break;
            default:
                parts.Add(new NumberFormatPart("minusSign", NumberFormatInfo.NegativeSign));
                parts.Add(new NumberFormatPart("currency", symbol));
                AddCurrencyNumberParts(parts, in body);
                break;
        }
    }

    private void BuildCurrencyPositiveParts(List<NumberFormatPart> parts, int pattern, string symbol, in NumberBody body)
    {
        const string Nbsp = "\u00A0"; // Non-breaking space per CLDR
        switch (pattern)
        {
            case 0: // $n
                parts.Add(new NumberFormatPart("currency", symbol));
                AddCurrencyNumberParts(parts, in body);
                break;
            case 1: // n$
                AddCurrencyNumberParts(parts, in body);
                parts.Add(new NumberFormatPart("currency", symbol));
                break;
            case 2: // $ n
                parts.Add(new NumberFormatPart("currency", symbol));
                parts.Add(new NumberFormatPart("literal", Nbsp));
                AddCurrencyNumberParts(parts, in body);
                break;
            case 3: // n $
                AddCurrencyNumberParts(parts, in body);
                parts.Add(new NumberFormatPart("literal", Nbsp));
                parts.Add(new NumberFormatPart("currency", symbol));
                break;
            default:
                parts.Add(new NumberFormatPart("currency", symbol));
                AddCurrencyNumberParts(parts, in body);
                break;
        }
    }

    private List<NumberFormatPart> FormatPercentToParts(double value)
    {
        var parts = new List<NumberFormatPart>();
        var isNegative = value < 0 || double.IsNegativeInfinity(1 / value); // Handles -0

        // Multiply by 100 for percent
        var body = FormatNumericToString(value * 100);
        var sign = SignFor(isNegative, body.IsZero);

        AppendPercentParts(parts, in body, sign.ShowNegative, sign.ShowPositive);

        return parts;
    }

    /// <summary>
    /// Walks the percent pattern around whatever number stands in it, so the percent sign is written for a
    /// non-finite value as it is for a finite one.
    /// </summary>
    private void AppendPercentParts(List<NumberFormatPart> parts, in NumberBody body, bool showNegativeSign, bool showPositiveSign)
    {
        // Get the percent pattern to determine symbol position and spacing
        var pattern = NumberFormatInfo.PercentPositivePattern;

        // Determine if symbol comes before or after, and if there's spacing
        // Positive patterns: 0="n %", 1="n%", 2="%n", 3="% n"
        var symbolAfter = pattern == 0 || pattern == 1;
        // CLDR's de percent pattern is "#,##0 %" with U+00A0, which is the character
        // intl402/BigInt/prototype/toLocaleString/de-DE.js asserts; .NET writes a plain space for the
        // same locale, so the pattern says where the space goes and CLDR says which space it is.
        const string Nbsp = "\u00A0";
        var hasSpace = pattern == 0 || pattern == 3;

        if (!symbolAfter)
        {
            // Symbol before number
            parts.Add(new NumberFormatPart("percentSign", NumberFormatInfo.PercentSymbol));
            if (hasSpace)
            {
                parts.Add(new NumberFormatPart("literal", Nbsp));
            }
        }

        AppendSignPart(parts, showNegativeSign, showPositiveSign);
        AddNumberParts(parts, in body);

        if (symbolAfter)
        {
            // Symbol after number
            if (hasSpace)
            {
                parts.Add(new NumberFormatPart("literal", Nbsp));
            }
            parts.Add(new NumberFormatPart("percentSign", NumberFormatInfo.PercentSymbol));
        }
    }

    private List<NumberFormatPart> FormatUnitToParts(double value)
    {
        var parts = new List<NumberFormatPart>();
        var isNegative = value < 0 || double.IsNegativeInfinity(1 / value); // Handles -0

        var body = FormatNumericToString(value);
        var sign = SignFor(isNegative, body.IsZero);

        AppendUnitParts(parts, in body, sign.ShowNegative, sign.ShowPositive, value);

        return parts;
    }

    /// <summary>
    /// Walks the CLDR unit pattern around whatever number stands in it, both of its sides included.
    /// </summary>
    /// <remarks>
    /// A pattern can put text on either side — <c>ja-JP</c>'s long kilometre-per-hour is
    /// <c>"時速 {0} キロメートル"</c> — and https://tc39.es/ecma402/#sec-partitionnumberpattern appends a
    /// <c>unit</c> part for each, the leading one before the sign. Reporting only the trailing side left
    /// <c>formatToParts</c> describing a string <c>format</c> never wrote.
    /// </remarks>
    private void AppendUnitParts(
        List<NumberFormatPart> parts,
        in NumberBody body,
        bool showNegativeSign,
        bool showPositiveSign,
        double value)
    {
        GetUnitAffixes(value, out var beforeNumber, out var afterNumber);

        AddUnitAffixParts(parts, beforeNumber, leading: true);
        AppendSignPart(parts, showNegativeSign, showPositiveSign);
        AddNumberParts(parts, in body);
        AddUnitAffixParts(parts, afterNumber, leading: false);
    }

    /// <summary>
    /// The two sides of the CLDR unit pattern this formatter writes around a number of the given size.
    /// </summary>
    private void GetUnitAffixes(double value, out string beforeNumber, out string afterNumber)
    {
        var unitDisplay = UnitDisplay ?? "short";
        var unitStr = Unit ?? "";

        // Try to get unit patterns from CLDR provider
        var unitPatterns = CldrProvider.GetUnitPatterns(Locale, unitStr, unitDisplay);
        if (unitPatterns != null)
        {
            // Extract the two sides of the pattern by removing the {0} placeholder
            // Select singular or plural pattern based on the absolute value
            var isSingular = System.Math.Abs(value) == 1;
            var pattern = isSingular ? (unitPatterns.One ?? unitPatterns.Other) : unitPatterns.Other;
            var placeholderIndex = pattern.IndexOf("{0}", StringComparison.Ordinal);

            beforeNumber = placeholderIndex >= 0 ? pattern.Substring(0, placeholderIndex) : "";
            afterNumber = placeholderIndex >= 0 ? pattern.Substring(placeholderIndex + 3) : "";
            return;
        }

        // Fallback to legacy behavior, which only ever writes a suffix
        beforeNumber = "";

        // Narrow display never has space; percent/degree units don't have space
        var needsSpace = !string.Equals(unitDisplay, "narrow", StringComparison.Ordinal) &&
                        !string.Equals(unitStr, "percent", StringComparison.Ordinal) &&
                        !string.Equals(unitStr, "celsius", StringComparison.Ordinal) &&
                        !string.Equals(unitStr, "fahrenheit", StringComparison.Ordinal);

        afterNumber = (needsSpace ? " " : "") + GetUnitSuffix(unitStr, unitDisplay, value);
    }

    /// <summary>
    /// Splits one side of a unit pattern into the parts it is made of: the unit name, and the literal
    /// separating it from the number.
    /// </summary>
    private static void AddUnitAffixParts(List<NumberFormatPart> parts, string affix, bool leading)
    {
        if (affix.Length == 0)
        {
            return;
        }

        // the separator sits between the two, so it trails a prefix and leads a suffix
        var separatorIndex = leading ? affix.Length - 1 : 0;
        var hasSeparator = affix[separatorIndex] == ' ';
        var unit = hasSeparator
            ? (leading ? affix.Substring(0, separatorIndex) : affix.Substring(1))
            : affix;

        if (leading && unit.Length > 0)
        {
            parts.Add(new NumberFormatPart("unit", unit));
        }

        if (hasSeparator)
        {
            parts.Add(new NumberFormatPart("literal", " "));
        }

        if (!leading && unit.Length > 0)
        {
            parts.Add(new NumberFormatPart("unit", unit));
        }
    }

}
