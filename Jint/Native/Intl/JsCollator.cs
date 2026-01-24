using System.Globalization;
using Jint.Native.Object;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-collator-objects
/// Represents an Intl.Collator instance with locale-aware string comparison.
/// </summary>
internal sealed class JsCollator : ObjectInstance
{
    internal JsCollator(
        Engine engine,
        ObjectInstance prototype,
        string locale,
        string usage,
        string sensitivity,
        bool ignorePunctuation,
        string collation,
        bool numeric,
        string caseFirst,
        CompareInfo compareInfo,
        CompareOptions compareOptions) : base(engine)
    {
        _prototype = prototype;
        Locale = locale;
        Usage = usage;
        Sensitivity = sensitivity;
        IgnorePunctuation = ignorePunctuation;
        Collation = collation;
        Numeric = numeric;
        CaseFirst = caseFirst;
        CompareInfo = compareInfo;
        CompareOptions = compareOptions;
    }

    /// <summary>
    /// The locale used for collation.
    /// </summary>
    internal string Locale { get; }

    /// <summary>
    /// "sort" or "search" - the intended usage.
    /// </summary>
    internal string Usage { get; }

    /// <summary>
    /// "base", "accent", "case", or "variant" - the sensitivity level.
    /// </summary>
    internal string Sensitivity { get; }

    /// <summary>
    /// Whether punctuation should be ignored.
    /// </summary>
    internal bool IgnorePunctuation { get; }

    /// <summary>
    /// The collation type (e.g., "default", "phonebook").
    /// </summary>
    internal string Collation { get; }

    /// <summary>
    /// Whether numeric sorting is enabled.
    /// </summary>
    internal bool Numeric { get; }

    /// <summary>
    /// "upper", "lower", or "false" - case ordering.
    /// </summary>
    internal string CaseFirst { get; }

    /// <summary>
    /// The .NET CompareInfo for string comparison.
    /// </summary>
    internal CompareInfo CompareInfo { get; }

    /// <summary>
    /// The .NET CompareOptions derived from collator settings.
    /// </summary>
    internal CompareOptions CompareOptions { get; }

    /// <summary>
    /// Compares two strings according to the collator's locale and options.
    /// </summary>
    internal int Compare(string x, string y)
    {
        // Normalize strings to NFC form so that canonically equivalent strings compare as equal
        // For example: "ö" (U+00F6) should equal "o\u0308" (o + combining umlaut)
        x = x.Normalize(System.Text.NormalizationForm.FormC);
        y = y.Normalize(System.Text.NormalizationForm.FormC);

        if (Numeric)
        {
            // Use natural sort comparison for numeric strings
            return NaturalStringCompare(x, y);
        }

        var result = CompareInfo.Compare(x, y, CompareOptions);

        // Some locales (like Thai) inherently ignore punctuation/symbols in comparison.
        // If ignorePunctuation is false and the result is 0 but strings differ, check if the
        // difference is only in punctuation/symbols (by removing them and comparing).
        if (result == 0 && !IgnorePunctuation && !string.Equals(x, y, System.StringComparison.Ordinal))
        {
            // Strip all punctuation/symbols and see if what remains is equal
            var xStripped = StripPunctuation(x);
            var yStripped = StripPunctuation(y);
            if (string.Equals(xStripped, yStripped, System.StringComparison.Ordinal))
            {
                // The difference is only in punctuation/symbols which should not be ignored
                result = string.CompareOrdinal(x, y);
            }
        }

        // Normalize result to -1, 0, or 1 (ECMA-402 only guarantees sign, but tests expect these values)
        return result < 0 ? -1 : result > 0 ? 1 : 0;
    }

    /// <summary>
    /// Natural string comparison that handles embedded numbers.
    /// For example: "a2" comes before "a10" instead of "a10" before "a2".
    /// </summary>
    private int NaturalStringCompare(string x, string y)
    {
        int xi = 0, yi = 0;
        while (xi < x.Length && yi < y.Length)
        {
            bool xIsDigit = char.IsDigit(x[xi]);
            bool yIsDigit = char.IsDigit(y[yi]);

            if (xIsDigit && yIsDigit)
            {
                // Extract numeric portions
                int xStart = xi;
                while (xi < x.Length && char.IsDigit(x[xi])) xi++;
                int yStart = yi;
                while (yi < y.Length && char.IsDigit(y[yi])) yi++;

                // Compare numeric values
#if NET6_0_OR_GREATER
                var xNum = long.Parse(x.AsSpan(xStart, xi - xStart), System.Globalization.CultureInfo.InvariantCulture);
                var yNum = long.Parse(y.AsSpan(yStart, yi - yStart), System.Globalization.CultureInfo.InvariantCulture);
#else
                var xNum = long.Parse(x.Substring(xStart, xi - xStart), System.Globalization.CultureInfo.InvariantCulture);
                var yNum = long.Parse(y.Substring(yStart, yi - yStart), System.Globalization.CultureInfo.InvariantCulture);
#endif

                int numCompare = xNum.CompareTo(yNum);
                if (numCompare != 0) return numCompare < 0 ? -1 : 1;
            }
            else
            {
                // Compare single characters using locale settings
                int charCompare = CompareInfo.Compare(x, xi, 1, y, yi, 1, CompareOptions);
                if (charCompare != 0) return charCompare < 0 ? -1 : 1;
                xi++;
                yi++;
            }
        }

        // Handle remaining characters
        int lengthDiff = (x.Length - xi).CompareTo(y.Length - yi);
        return lengthDiff < 0 ? -1 : lengthDiff > 0 ? 1 : 0;
    }

    /// <summary>
    /// Strips punctuation and symbol characters from a string.
    /// </summary>
    private static string StripPunctuation(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return s;
        }

        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (!char.IsPunctuation(c) && !char.IsSymbol(c) && !char.IsWhiteSpace(c))
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
