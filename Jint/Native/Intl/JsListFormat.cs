using System.Globalization;
using System.Text;
using Jint.Native.Object;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-listformat-objects
/// Represents an Intl.ListFormat instance for locale-aware list formatting.
/// </summary>
internal sealed class JsListFormat : ObjectInstance
{
    internal JsListFormat(
        Engine engine,
        ObjectInstance prototype,
        string locale,
        string type,
        string style,
        CultureInfo cultureInfo) : base(engine)
    {
        _prototype = prototype;
        Locale = locale;
        ListType = type;
        Style = style;
        CultureInfo = cultureInfo;
    }

    /// <summary>
    /// The locale used for formatting.
    /// </summary>
    internal string Locale { get; }

    /// <summary>
    /// The type of list: "conjunction" (and), "disjunction" (or), or "unit".
    /// </summary>
    internal string ListType { get; }

    /// <summary>
    /// The style: "long", "short", or "narrow".
    /// </summary>
    internal string Style { get; }

    /// <summary>
    /// The .NET CultureInfo for locale-specific formatting.
    /// </summary>
    internal CultureInfo CultureInfo { get; }

    /// <summary>
    /// Gets the CLDR provider from engine options.
    /// </summary>
    private ICldrProvider CldrProvider => _engine.Options.Intl.CldrProvider;

    /// <summary>
    /// Formats a list of strings according to the locale and options.
    /// </summary>
    internal string Format(string[] list)
    {
        if (list.Length == 0)
        {
            return "";
        }

        if (list.Length == 1)
        {
            return list[0];
        }

        // Get separators based on type and style
        GetSeparators(out var separator, out var twoItemSeparator, out var finalSeparator);

        if (list.Length == 2)
        {
            return $"{list[0]}{twoItemSeparator}{list[1]}";
        }

        // For 3+ items: "A, B, and C" or "A, B, or C"
        var result = new ValueStringBuilder();
        for (var i = 0; i < list.Length; i++)
        {
            if (i > 0)
            {
                if (i == list.Length - 1)
                {
                    result.Append(finalSeparator);
                }
                else
                {
                    result.Append(separator);
                }
            }
            result.Append(list[i]);
        }

        return result.ToString();
    }

    /// <summary>
    /// Gets the separators based on type and style.
    /// CLDR list patterns use different templates based on position:
    /// - start: "{0}, {1}" (first two items)
    /// - middle: "{0}, {1}" (middle items)
    /// - end: "{0}, and {1}" (last two items)
    /// - two: "{0} and {1}" (exactly two items - no comma before "and")
    /// </summary>
    private void GetSeparators(out string separator, out string twoItemSeparator, out string finalSeparator)
    {
        // Try to get patterns from CLDR provider
        var patterns = CldrProvider.GetListPatterns(Locale, ListType, Style);
        if (patterns != null)
        {
            // Extract separators from CLDR patterns
            // Patterns are in format "{0}, {1}" or "{0} and {1}"
            separator = ExtractSeparatorFromPattern(patterns.Middle);
            twoItemSeparator = ExtractSeparatorFromPattern(patterns.Two);
            finalSeparator = ExtractSeparatorFromPattern(patterns.End);
            return;
        }

        // Fallback to hardcoded patterns
        var isEnglish = Locale.StartsWith("en", StringComparison.OrdinalIgnoreCase);

        separator = ", ";
        twoItemSeparator = ", ";
        finalSeparator = ", ";

        if (string.Equals(ListType, "conjunction", StringComparison.Ordinal))
        {
            if (string.Equals(Style, "long", StringComparison.Ordinal))
            {
                twoItemSeparator = isEnglish ? " and " : ", ";
                finalSeparator = isEnglish ? ", and " : ", ";
            }
            else if (string.Equals(Style, "short", StringComparison.Ordinal))
            {
                twoItemSeparator = isEnglish ? " & " : ", ";
                finalSeparator = isEnglish ? ", & " : ", ";
            }
            // narrow: use default ", " separators
        }
        else if (string.Equals(ListType, "disjunction", StringComparison.Ordinal))
        {
            // All disjunction styles use " or " / ", or " for English
            twoItemSeparator = isEnglish ? " or " : ", ";
            finalSeparator = isEnglish ? ", or " : ", ";
        }
        else if (string.Equals(ListType, "unit", StringComparison.Ordinal))
        {
            if (string.Equals(Style, "narrow", StringComparison.Ordinal))
            {
                separator = " ";
                twoItemSeparator = " ";
                finalSeparator = " ";
            }
            // short and long: use default ", " separators
        }
    }

    /// <summary>
    /// Extracts the separator string from a CLDR pattern like "{0}, {1}" or "{0} and {1}".
    /// </summary>
    private static string ExtractSeparatorFromPattern(string pattern)
    {
        // Pattern format: "{0}SEPARATOR{1}"
        const string placeholder0 = "{0}";
        const string placeholder1 = "{1}";

        var idx0 = pattern.IndexOf(placeholder0, StringComparison.Ordinal);
        var idx1 = pattern.IndexOf(placeholder1, StringComparison.Ordinal);

        if (idx0 >= 0 && idx1 > idx0)
        {
            // Extract the text between {0} and {1}
            var start = idx0 + placeholder0.Length;
            return pattern.Substring(start, idx1 - start);
        }

        // Fallback if pattern doesn't match expected format
        return ", ";
    }

    /// <summary>
    /// Formats a list and returns an array of parts.
    /// </summary>
    internal JsArray FormatToParts(Engine engine, string[] list)
    {
        var result = new JsArray(engine);
        uint index = 0;

        if (list.Length == 0)
        {
            return result;
        }

        GetSeparators(out var separator, out var twoItemSeparator, out var finalSeparator);

        // For 2 items, use twoItemSeparator
        // For 3+ items, use separator for middle, finalSeparator for last
        var actualSeparator = list.Length == 2 ? twoItemSeparator : finalSeparator;

        for (var i = 0; i < list.Length; i++)
        {
            // Add element part
            var elementPart = ObjectInstance.OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
            elementPart.Set("type", "element");
            elementPart.Set("value", list[i]);
            result.SetIndexValue(index++, elementPart, updateLength: false);

            // Add separator if not last element
            if (i < list.Length - 1)
            {
                var literalPart = ObjectInstance.OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
                literalPart.Set("type", "literal");
                // For 2-item lists, use twoItemSeparator; for 3+ items, use separator (middle) or finalSeparator (end)
                string sep;
                if (list.Length == 2)
                {
                    sep = twoItemSeparator;
                }
                else
                {
                    sep = i == list.Length - 2 ? finalSeparator : separator;
                }
                literalPart.Set("value", sep);
                result.SetIndexValue(index++, literalPart, updateLength: false);
            }
        }

        result.SetLength(index);
        return result;
    }
}
