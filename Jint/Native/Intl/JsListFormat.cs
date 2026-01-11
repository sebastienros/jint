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
        GetSeparators(out var separator, out var finalSeparator);

        if (list.Length == 2)
        {
            return $"{list[0]}{finalSeparator}{list[1]}";
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
    /// </summary>
    private void GetSeparators(out string separator, out string finalSeparator)
    {
        // These are simplified English patterns - a full implementation would use CLDR data
        var isEnglish = Locale.StartsWith("en", StringComparison.OrdinalIgnoreCase);

        separator = ", ";
        finalSeparator = ", ";

        if (string.Equals(ListType, "conjunction", StringComparison.Ordinal))
        {
            if (string.Equals(Style, "long", StringComparison.Ordinal))
            {
                finalSeparator = isEnglish ? ", and " : ", ";
            }
            else if (string.Equals(Style, "short", StringComparison.Ordinal))
            {
                finalSeparator = isEnglish ? ", & " : ", ";
            }
        }
        else if (string.Equals(ListType, "disjunction", StringComparison.Ordinal))
        {
            if (string.Equals(Style, "long", StringComparison.Ordinal) || string.Equals(Style, "short", StringComparison.Ordinal))
            {
                finalSeparator = isEnglish ? ", or " : ", ";
            }
        }
        else if (string.Equals(ListType, "unit", StringComparison.Ordinal))
        {
            if (string.Equals(Style, "narrow", StringComparison.Ordinal))
            {
                separator = " ";
                finalSeparator = " ";
            }
        }
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

        GetSeparators(out var separator, out var finalSeparator);

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
                literalPart.Set("value", i == list.Length - 2 ? finalSeparator : separator);
                result.SetIndexValue(index++, literalPart, updateLength: false);
            }
        }

        result.SetLength(index);
        return result;
    }
}
