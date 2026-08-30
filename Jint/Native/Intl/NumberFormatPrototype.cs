using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using Jint.Native.BigInt;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-numberformat-prototype-object
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class NumberFormatPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly NumberFormatConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString NumberFormatToStringTag = new("Intl.NumberFormat");

    public NumberFormatPrototype(
        Engine engine,
        Realm realm,
        NumberFormatConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    private JsNumberFormat ValidateNumberFormat(JsValue thisObject)
    {
        if (thisObject is JsNumberFormat numberFormat)
        {
            return numberFormat;
        }

        // UnwrapNumberFormat: a legacy-constructed wrapper keeps the real formatter under the
        // per-realm %Intl%.[[FallbackSymbol]] property.
        var unwrapped = IntlUtilities.UnwrapLegacyConstructor(_realm, _realm.Intrinsics.NumberFormat, thisObject);
        if (unwrapped is JsNumberFormat unwrappedNumberFormat)
        {
            return unwrappedNumberFormat;
        }

        Throw.TypeError(_realm, "Value is not an Intl.NumberFormat");
        return null!; // Never reached
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.numberformat.prototype.format
    /// </summary>
    [JsAccessor("format")]
    private ClrFunction GetFormat(JsValue thisObject)
    {
        var numberFormat = ValidateNumberFormat(thisObject);

        // Return a bound format function
        return new ClrFunction(Engine, "", (_, args) =>
        {
            var value = args.At(0);

            // Handle BigInt values separately to preserve precision
            if (value is JsBigInt bigInt)
            {
                return numberFormat.Format(bigInt._value);
            }

            if (value is BigIntInstance bigIntInstance)
            {
                return numberFormat.Format(bigIntInstance.BigIntData._value);
            }

            // Per ECMA-402 ToIntlMathematicalValue: when a string represents an integer that
            // exceeds Number.MAX_SAFE_INTEGER precision, route through the BigInteger path so
            // significant digits are preserved exactly.
            if (value is JsString jsStr)
            {
                var s = jsStr.ToString();
                if (TryParseLargeInteger(s, out var bigValue))
                {
                    return numberFormat.Format(bigValue);
                }
                // Fractional decimal strings whose precision exceeds what double can represent
                // (16+ significant digits): format directly from the (mantissa, fractionDigits)
                // pair to avoid losing trailing digits via TypeConverter.ToNumber.
                if (TryParseHighPrecisionDecimal(s, out var mantissa, out var fractionDigits))
                {
                    return numberFormat.FormatExactDecimal(mantissa, fractionDigits);
                }
            }

            return numberFormat.Format(TypeConverter.ToNumber(value));
        }, 1, PropertyFlag.Configurable);
    }

    /// <summary>
    /// Parses a decimal-string with a fraction part (e.g. "1.0000000000000001") into a
    /// BigInteger mantissa plus a fraction-digit count, when the total precision exceeds
    /// what an IEEE-754 double can represent (16 significant digits is the conservative
    /// boundary). Returns false otherwise so existing inputs stay on the double path.
    /// </summary>
    private static bool TryParseHighPrecisionDecimal(string s, out BigInteger mantissa, out int fractionDigits)
    {
        mantissa = default;
        fractionDigits = 0;
        if (string.IsNullOrEmpty(s))
            return false;

        var i = 0;
        var negative = false;
        if (s[0] == '+' || s[0] == '-')
        {
            negative = s[0] == '-';
            i = 1;
        }

        var dotPos = -1;
        for (var j = i; j < s.Length; j++)
        {
            if (s[j] == '.')
            {
                if (dotPos != -1) return false;
                dotPos = j;
            }
            else if (!char.IsAsciiDigit(s[j]))
            {
                return false;
            }
        }

        if (dotPos == -1) return false;
        if (dotPos == i || dotPos == s.Length - 1) return false; // need digits on both sides

        fractionDigits = s.Length - dotPos - 1;
        var totalDigits = (dotPos - i) + fractionDigits;

        // Stay on the double path when precision fits — preserves existing per-locale behavior
        // (signDisplay, percent/currency style, etc. that the BigInteger path doesn't replicate).
        if (totalDigits < 16)
            return false;

        // Build mantissa from concatenated integer + fraction digits.
        var sb = new System.Text.StringBuilder(totalDigits);
        sb.Append(s, i, dotPos - i);
        sb.Append(s, dotPos + 1, fractionDigits);
        if (!BigInteger.TryParse(sb.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out mantissa))
            return false;
        if (negative) mantissa = -mantissa;
        return true;
    }

    /// <summary>
    /// Returns true when the input string is a decimal integer literal whose magnitude
    /// requires more precision than IEEE 754 double can represent (≥17 digits).
    /// Smaller integers stay on the double path so we don't perturb existing formatting.
    /// </summary>
    private static bool TryParseLargeInteger(string s, out BigInteger value)
    {
        value = default;
        if (string.IsNullOrEmpty(s))
            return false;

        var i = 0;
        var negative = false;
        if (s[0] == '+' || s[0] == '-')
        {
            negative = s[0] == '-';
            i = 1;
        }

        // Must be all digits, at least 17 of them (the precision boundary for double).
        if (s.Length - i < 17)
            return false;

        for (var j = i; j < s.Length; j++)
        {
            if (!char.IsAsciiDigit(s[j]))
                return false;
        }

        if (!BigInteger.TryParse(s.AsSpan(i), NumberStyles.None, CultureInfo.InvariantCulture, out value))
            return false;

        if (negative)
            value = -value;
        return true;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.numberformat.prototype.resolvedoptions
    /// </summary>
    [JsFunction]
    private JsObject ResolvedOptions(JsValue thisObject)
    {
        var numberFormat = ValidateNumberFormat(thisObject);

        var result = ObjectInstance.OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        // Use CreateDataPropertyOrThrow to avoid prototype chain setters
        result.CreateDataPropertyOrThrow("locale", numberFormat.Locale);
        result.CreateDataPropertyOrThrow("numberingSystem", numberFormat.NumberingSystem);
        result.CreateDataPropertyOrThrow("style", numberFormat.Style);

        if (string.Equals(numberFormat.Style, "currency", StringComparison.Ordinal))
        {
            result.CreateDataPropertyOrThrow("currency", numberFormat.Currency ?? "");
            result.CreateDataPropertyOrThrow("currencyDisplay", numberFormat.CurrencyDisplay ?? "symbol");
            result.CreateDataPropertyOrThrow("currencySign", numberFormat.CurrencySign ?? "standard");
        }

        if (string.Equals(numberFormat.Style, "unit", StringComparison.Ordinal))
        {
            result.CreateDataPropertyOrThrow("unit", numberFormat.Unit ?? "");
            result.CreateDataPropertyOrThrow("unitDisplay", numberFormat.UnitDisplay ?? "short");
        }

        result.CreateDataPropertyOrThrow("minimumIntegerDigits", numberFormat.MinimumIntegerDigits);
        result.CreateDataPropertyOrThrow("minimumFractionDigits", numberFormat.MinimumFractionDigits);
        result.CreateDataPropertyOrThrow("maximumFractionDigits", numberFormat.MaximumFractionDigits);

        // Include significant digits options if they were specified
        if (numberFormat.MinimumSignificantDigits.HasValue)
        {
            result.CreateDataPropertyOrThrow("minimumSignificantDigits", numberFormat.MinimumSignificantDigits.Value);
        }
        if (numberFormat.MaximumSignificantDigits.HasValue)
        {
            result.CreateDataPropertyOrThrow("maximumSignificantDigits", numberFormat.MaximumSignificantDigits.Value);
        }

        // Per spec, useGrouping can be "auto", "always", "min2", or false (boolean)
        if (string.Equals(numberFormat.UseGrouping, "false", StringComparison.Ordinal))
        {
            result.CreateDataPropertyOrThrow("useGrouping", false);
        }
        else
        {
            result.CreateDataPropertyOrThrow("useGrouping", numberFormat.UseGrouping);
        }
        result.CreateDataPropertyOrThrow("notation", numberFormat.Notation);

        // compactDisplay is only included when notation is "compact"
        if (string.Equals(numberFormat.Notation, "compact", StringComparison.Ordinal))
        {
            result.CreateDataPropertyOrThrow("compactDisplay", numberFormat.CompactDisplay);
        }

        result.CreateDataPropertyOrThrow("signDisplay", numberFormat.SignDisplay);
        result.CreateDataPropertyOrThrow("roundingIncrement", numberFormat.RoundingIncrement);
        result.CreateDataPropertyOrThrow("roundingMode", numberFormat.RoundingMode);
        result.CreateDataPropertyOrThrow("roundingPriority", numberFormat.RoundingPriority);
        result.CreateDataPropertyOrThrow("trailingZeroDisplay", numberFormat.TrailingZeroDisplay);

        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.numberformat.prototype.formattoparts
    /// </summary>
    [JsFunction]
    private JsArray FormatToParts(JsValue thisObject, JsValue value)
    {
        var numberFormat = ValidateNumberFormat(thisObject);

        double number;
        if (value.IsUndefined())
        {
            number = double.NaN;
        }
        else
        {
            number = TypeConverter.ToNumber(value);
        }

        // Get parts from the number format
        var parts = numberFormat.FormatToParts(number);

        // Convert to JsArray of objects
        var result = new JsArray(Engine, (uint) parts.Count);
        for (var i = 0; i < parts.Count; i++)
        {
            var partObj = ObjectInstance.OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
            partObj.Set("type", parts[i].Type);
            partObj.Set("value", parts[i].Value);
            result.SetIndexValue((uint) i, partObj, updateLength: true);
        }

        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.numberformat.prototype.formatrange
    /// </summary>
    [JsFunction]
    private JsValue FormatRange(JsValue thisObject, JsValue start, JsValue end)
    {
        var numberFormat = ValidateNumberFormat(thisObject);

        // Validate arguments
        if (start.IsUndefined() || end.IsUndefined())
        {
            Throw.TypeError(_realm, "start and end are required");
        }

        var startFormatted = FormatRangeOperand(numberFormat, start);
        var endFormatted = FormatRangeOperand(numberFormat, end);

        // If the numbers are the same when formatted, return with approximately sign
        if (string.Equals(startFormatted, endFormatted, StringComparison.Ordinal))
        {
            // Add approximately sign prefix
            return $"~{startFormatted}";
        }

        return CollapseFormattedRange(numberFormat, startFormatted, endFormatted);
    }

    /// <summary>
    /// Formats a single end of a range, preserving precision for high-precision string inputs.
    /// </summary>
    private string FormatRangeOperand(JsNumberFormat numberFormat, JsValue value)
    {
        if (value is JsBigInt bigInt)
        {
            return numberFormat.Format(bigInt._value);
        }

        if (value is BigIntInstance bigIntInstance)
        {
            return numberFormat.Format(bigIntInstance.BigIntData._value);
        }

        if (value is JsString jsStr)
        {
            var s = jsStr.ToString();
            if (TryParseLargeInteger(s, out var bigValue))
            {
                return numberFormat.Format(bigValue);
            }
            if (TryParseHighPrecisionDecimal(s, out var mantissa, out var fractionDigits))
            {
                return numberFormat.FormatExactDecimal(mantissa, fractionDigits);
            }
        }

        var number = TypeConverter.ToNumber(value);
        if (double.IsNaN(number))
        {
            Throw.RangeError(_realm, "Invalid number value");
        }
        return numberFormat.Format(number);
    }

    /// <summary>One part of a formatted range: what https://tc39.es/ecma402/#sec-formatnumericrangetoparts reports.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct RangePart(string Type, string Value, string Source);

    /// <summary>
    /// https://tc39.es/ecma402/#sec-partitionnumberrangepattern, including its last step,
    /// https://tc39.es/ecma402/#sec-collapsenumberrange.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The collapse being implementation-defined does not make it one lane's post-processing of the other's
    /// output: it is step 8 of the partition both lanes read, so an endpoint whose sign or currency it
    /// elides is elided in the parts as well. This is where <c>formatRangeToParts</c> gets it.
    /// </para>
    /// <para>
    /// <c>formatRange</c> does not come through here, and the reason is precision rather than principle:
    /// <see cref="JsNumberFormat.FormatToParts"/> takes a <see cref="double"/>, while
    /// <see cref="FormatRangeOperand"/> keeps a BigInt and a long decimal string exact — test262's
    /// <c>formatRange/en-US.js</c> asserts a range of two 18-digit integers that differ in their last digit.
    /// So the same two shapes are stated twice, here and in <see cref="CollapseFormattedRange"/>, and
    /// <c>IntlNumberFormatPartsTests.RangePartsConcatenateToFormatRange</c> is what holds them together.
    /// </para>
    /// </remarks>
    private static List<RangePart> PartitionNumberRangePattern(JsNumberFormat numberFormat, double x, double y)
    {
        var startParts = numberFormat.FormatToParts(x);
        var endParts = numberFormat.FormatToParts(y);

        var result = new List<RangePart>(startParts.Count + endParts.Count + 1);

        // Step 4: when the two ends format alike the range is one approximate value, all of it shared.
        if (string.Equals(JoinParts(startParts), JoinParts(endParts), StringComparison.Ordinal))
        {
            result.Add(new RangePart("approximatelySign", "~", "shared"));
            foreach (var part in startParts)
            {
                result.Add(new RangePart(part.Type, part.Value, "shared"));
            }
            return result;
        }

        var plan = PlanRangeCollapse(numberFormat, startParts, endParts);

        for (var i = 0; i < startParts.Count - plan.DropFromStartTail; i++)
        {
            result.Add(new RangePart(startParts[i].Type, startParts[i].Value, "startRange"));
        }

        result.Add(new RangePart("literal", plan.Separator, "shared"));

        for (var i = plan.DropFromEndHead; i < endParts.Count; i++)
        {
            result.Add(new RangePart(endParts[i].Type, endParts[i].Value, "endRange"));
        }

        return result;
    }

    /// <summary>What <see cref="PlanRangeCollapse"/> decided: how much of each end is redundant, and which separator to write.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct RangeCollapsePlan(int DropFromStartTail, int DropFromEndHead, string Separator);

    /// <summary>
    /// https://tc39.es/ecma402/#sec-collapsenumberrange, on the parts rather than on the string: the two
    /// ICU range-pattern shapes <see cref="CollapseFormattedRange"/> implements, expressed as "these
    /// endpoint parts are redundant".
    /// </summary>
    /// <remarks>
    /// <para>
    /// A prefix-currency locale whose two ends share a sign <em>and</em> a symbol writes them once at the
    /// front and tightens the separator — test262's <c>formatRange/en-US.js</c> asserts
    /// <c>"+$2.90–3.10"</c>. A suffix-currency locale whose two ends share a trailing symbol writes it once
    /// at the back — its <c>formatRange/pt-PT.js</c> asserts <c>"3 - 5 €"</c> and
    /// <c>"+2,90 - 3,10 €"</c>. A shared symbol with no shared sign is not collapsed at all, which is
    /// the same file's <c>"$3 – $5"</c>.
    /// </para>
    /// <para>
    /// The scan stops at the first part that carries the number itself, so two ends that happen to end in
    /// the same digits do not have those digits mistaken for a shared affix.
    /// </para>
    /// </remarks>
    private static RangeCollapsePlan PlanRangeCollapse(
        JsNumberFormat numberFormat,
        List<NumberFormatPart> startParts,
        List<NumberFormatPart> endParts)
    {
        GetRangeSeparators(numberFormat.Locale, out var tight, out var loose);

        if (!string.Equals(numberFormat.Style, "currency", StringComparison.Ordinal))
        {
            return new RangeCollapsePlan(0, 0, tight);
        }

        var limit = System.Math.Min(startParts.Count, endParts.Count);

        var prefixLength = 0;
        while (prefixLength < limit && IsSharedAffix(startParts[prefixLength], endParts[prefixLength]))
        {
            prefixLength++;
        }

        var suffixLength = 0;
        while (prefixLength + suffixLength < limit
               && IsSharedAffix(startParts[startParts.Count - 1 - suffixLength], endParts[endParts.Count - 1 - suffixLength]))
        {
            suffixLength++;
        }

        var prefixHasSign = ContainsSignPart(startParts, 0, prefixLength);
        var prefixHasCurrency = ContainsCurrencyPart(startParts, 0, prefixLength);

        if (prefixHasSign && prefixHasCurrency)
        {
            return new RangeCollapsePlan(0, prefixLength, tight);
        }

        if (ContainsCurrencyPart(startParts, startParts.Count - suffixLength, suffixLength))
        {
            return new RangeCollapsePlan(suffixLength, prefixHasSign ? prefixLength : 0, loose);
        }

        return new RangeCollapsePlan(0, 0, loose);
    }

    /// <summary>
    /// True when two parts at matching positions are the same piece of pattern text — the sign, the symbol
    /// or the spacing around them. A part that carries the number is never one, however equal it looks.
    /// </summary>
    private static bool IsSharedAffix(NumberFormatPart a, NumberFormatPart b)
    {
        if (!string.Equals(a.Type, b.Type, StringComparison.Ordinal)
            || !string.Equals(a.Value, b.Value, StringComparison.Ordinal))
        {
            return false;
        }

        return a.Type switch
        {
            "integer" or "fraction" or "group" or "decimal" or "exponentInteger" or "nan" or "infinity" => false,
            _ => true
        };
    }

    private static bool ContainsSignPart(List<NumberFormatPart> parts, int start, int count)
    {
        for (var i = start; i < start + count; i++)
        {
            if (string.Equals(parts[i].Type, "plusSign", StringComparison.Ordinal)
                || string.Equals(parts[i].Type, "minusSign", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsCurrencyPart(List<NumberFormatPart> parts, int start, int count)
    {
        for (var i = start; i < start + count; i++)
        {
            if (string.Equals(parts[i].Type, "currency", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Joins two formatted range endpoints using ECMA-402 / ICU-style range patterns — the string lane's
    /// half of https://tc39.es/ecma402/#sec-collapsenumberrange.
    /// </summary>
    /// <remarks>
    /// <see cref="PlanRangeCollapse"/> states the same two shapes on parts, and
    /// <c>IntlNumberFormatPartsTests.RangePartsConcatenateToFormatRange</c> is what keeps the two honest.
    /// Every string this lane writes is exercised by test262's <c>formatRange/en-US.js</c> and
    /// <c>formatRange/pt-PT.js</c>.
    /// <para>Supports:</para>
    /// <para>— A locale-specific separator (e.g. en uses en-dash `–`, pt uses ASCII hyphen `-`).</para>
    /// <para>— Suffix collapse for locales whose currency follows the number (pt-PT etc.) —
    ///   shared trailing currency moves to the end of the range, separator gets spaces.</para>
    /// <para>— Sign+currency prefix collapse (signDisplay=always with prefix-currency locales) —
    ///   duplicate sign+currency dropped from end, tight separator.</para>
    /// </remarks>
    private static string CollapseFormattedRange(JsNumberFormat numberFormat, string startFormatted, string endFormatted)
    {
        var isCurrencyStyle = string.Equals(numberFormat.Style, "currency", StringComparison.Ordinal);
        string sepTight, sepLoose;
        GetRangeSeparators(numberFormat.Locale, out sepTight, out sepLoose);

        // Find longest common suffix. When the locale puts currency after the number,
        // the suffix typically contains the currency symbol (often preceded by NBSP).
        // Stop as soon as we hit a digit so we don't gobble part of the number itself
        // when both ends happen to share trailing digits (e.g. "+2,90 €" / "+3,10 €").
        // "A digit" is [[NumberingSystem]]'s question, not char.IsDigit's: hanidec's zero is U+3007, which
        // is not in the Nd category at all, so scanning past it turned "-$五.〇〇–一.〇〇" into "-$五–一.〇〇".
        var numberingSystem = numberFormat.ResolvedNumberingSystem;
        var suffixLen = 0;
        var maxSuffix = System.Math.Min(startFormatted.Length, endFormatted.Length);
        while (suffixLen < maxSuffix)
        {
            var ch = startFormatted[startFormatted.Length - 1 - suffixLen];
            if (ch != endFormatted[endFormatted.Length - 1 - suffixLen]) break;
            if (numberingSystem.IsDigitCharacter(ch)) break;
            suffixLen++;
        }

        // Find longest common prefix (but stop at the suffix boundary), and stop at a digit for the same
        // reason the suffix scan does: what is shared here is the sign and the symbol, never part of the
        // number. Reading two ends' digits as shared text is how "-$𝟓.𝟎𝟎–𝟏.𝟎𝟎" lost the high half of a
        // mathbold digit, both ends of which begin with the same surrogate.
        var prefixLen = 0;
        var maxPrefix = System.Math.Min(startFormatted.Length - suffixLen, endFormatted.Length - suffixLen);
        while (prefixLen < maxPrefix
               && startFormatted[prefixLen] == endFormatted[prefixLen]
               && !numberingSystem.IsDigitCharacter(startFormatted[prefixLen]))
        {
            prefixLen++;
        }

        if (isCurrencyStyle)
        {
            var prefix = startFormatted.Substring(0, prefixLen);
            var suffix = startFormatted.Substring(startFormatted.Length - suffixLen);
            var prefixHasSign = ContainsSign(prefix);
            var prefixHasCurrency = ContainsCurrencyChar(prefix);
            var suffixHasCurrency = ContainsCurrencyChar(suffix);

            // Prefix-currency locales (en-US) with shared sign+currency up front: tight separator,
            // collapse the duplicate sign+currency from the end.
            if (prefixHasSign && prefixHasCurrency)
            {
                var startTail = startFormatted.Substring(prefixLen, startFormatted.Length - prefixLen - suffixLen);
                var endTail = endFormatted.Substring(prefixLen, endFormatted.Length - prefixLen - suffixLen);
                return prefix + startTail + sepTight + endTail + suffix;
            }

            // Suffix-currency locales (pt-PT) with shared trailing currency: collapse to one
            // currency at the end, with the loose separator. If the prefix also contains a
            // shared sign, drop it from the end too.
            if (suffixHasCurrency)
            {
                var startCore = startFormatted.Substring(prefixLen, startFormatted.Length - prefixLen - suffixLen);
                var endCore = endFormatted.Substring(prefixLen, endFormatted.Length - prefixLen - suffixLen);
                if (prefixHasSign)
                {
                    return prefix + startCore + sepLoose + endCore + suffix;
                }
                // No shared sign — keep the prefix-less cores and append the shared suffix.
                var startNoSuffix = startFormatted.Substring(0, startFormatted.Length - suffixLen);
                var endNoSuffix = endFormatted.Substring(0, endFormatted.Length - suffixLen);
                return startNoSuffix + sepLoose + endNoSuffix + suffix;
            }

            // Currency without any meaningful sharing — keep both ends in full and use loose sep.
            return startFormatted + sepLoose + endFormatted;
        }

        // Decimal / percent / unit: use the locale's separator. en-US convention (and the test
        // expectation) is the tight form; pt-PT and similar use a spaced ASCII hyphen.
        return startFormatted + sepTight + endFormatted;
    }

    // TODO: read range patterns from CLDR (e.g. via Options.Intl.CldrProvider) instead of
    // hard-coding language detection here. Other locales (fr, ja, …) have their own pattern
    // shapes and will silently fall through to the en-style default until that data path
    // exists. Tracked in the precision-aware FormatRange work — see
    // Jint/Native/Intl/Data/CompactPatterns.Data.cs for an analogous CLDR-data stash.
    private static void GetRangeSeparators(string locale, out string tight, out string loose)
    {
        // Per CLDR, range patterns vary by locale. Pinned to the cases test262 exercises
        // until a richer locale-data path is wired in.
        if (locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            tight = " - ";
            loose = " - ";
            return;
        }
        // Default to en-style en-dash, with a loose variant for fallback cases.
        tight = "–";
        loose = " – ";
    }

    private static bool ContainsSign(string s)
    {
        foreach (var c in s)
        {
            if (c == '+' || c == '-' || c == '−') return true;
        }
        return false;
    }

    private static bool ContainsCurrencyChar(string s)
    {
        foreach (var c in s)
        {
            // UnicodeCategory.CurrencySymbol covers $, €, £, ¥, ₹, ﷼, ¤, etc. — every
            // single-character currency mark we care about for prefix/suffix collapse.
            if (char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.CurrencySymbol)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.numberformat.prototype.formatrangetoparts
    /// </summary>
    [JsFunction]
    private JsArray FormatRangeToParts(JsValue thisObject, JsValue start, JsValue end)
    {
        var numberFormat = ValidateNumberFormat(thisObject);

        // Validate arguments
        if (start.IsUndefined() || end.IsUndefined())
        {
            Throw.TypeError(_realm, "start and end are required");
        }

        var parts = PartitionNumberRangePattern(numberFormat, ToRangeNumber(start), ToRangeNumber(end));

        var result = new JsArray(Engine, (uint) parts.Count);
        for (var i = 0; i < parts.Count; i++)
        {
            var partObj = ObjectInstance.OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
            partObj.CreateDataPropertyOrThrow("type", parts[i].Type);
            partObj.CreateDataPropertyOrThrow("value", parts[i].Value);
            partObj.CreateDataPropertyOrThrow("source", parts[i].Source);
            result.SetIndexValue((uint) i, partObj, updateLength: true);
        }

        return result;
    }

    private double ToRangeNumber(JsValue value)
    {
        // Handle BigInt values - convert to double
        if (value is JsBigInt bigInt)
        {
            return (double) bigInt._value;
        }

        if (value is BigIntInstance bigIntInstance)
        {
            return (double) bigIntInstance.BigIntData._value;
        }

        var number = TypeConverter.ToNumber(value);
        if (double.IsNaN(number))
        {
            Throw.RangeError(_realm, "Invalid number value");
        }

        return number;
    }

    private static string JoinParts(List<NumberFormatPart> parts)
    {
        if (parts.Count == 0)
        {
            return "";
        }

        if (parts.Count == 1)
        {
            return parts[0].Value;
        }

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < parts.Count; i++)
        {
            sb.Append(parts[i].Value);
        }

        return sb.ToString();
    }
}
