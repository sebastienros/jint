using System.Globalization;
using Jint.Native.Object;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#locale-objects
/// Represents an Intl.Locale instance.
/// </summary>
internal sealed class JsLocale : ObjectInstance
{
    /// <summary>
    /// The canonicalized BCP 47 language tag.
    /// </summary>
    internal string Locale { get; }

    /// <summary>
    /// The base name without extensions.
    /// </summary>
    internal string BaseName { get; }

    /// <summary>
    /// The language subtag.
    /// </summary>
    internal string Language { get; }

    /// <summary>
    /// The script subtag, if present.
    /// </summary>
    internal string? Script { get; }

    /// <summary>
    /// The region subtag, if present.
    /// </summary>
    internal string? Region { get; }

    /// <summary>
    /// The calendar type from Unicode extension, if present.
    /// </summary>
    internal string? Calendar { get; }

    /// <summary>
    /// The case first option from Unicode extension, if present.
    /// </summary>
    internal string? CaseFirst { get; }

    /// <summary>
    /// The collation type from Unicode extension, if present.
    /// </summary>
    internal string? Collation { get; }

    /// <summary>
    /// The hour cycle from Unicode extension, if present.
    /// </summary>
    internal string? HourCycle { get; }

    /// <summary>
    /// The numbering system from Unicode extension, if present.
    /// </summary>
    internal string? NumberingSystem { get; }

    /// <summary>
    /// The numeric option from Unicode extension, if present.
    /// </summary>
    internal bool? Numeric { get; }

    /// <summary>
    /// The first day of week from Unicode extension (fw), if present.
    /// </summary>
    internal string? FirstDayOfWeek { get; }

    /// <summary>
    /// The variant subtags, if present.
    /// </summary>
    internal string[] Variants { get; }

    /// <summary>
    /// The associated .NET CultureInfo.
    /// </summary>
    internal CultureInfo CultureInfo { get; }

    internal JsLocale(
        Engine engine,
        ObjectInstance prototype,
        string locale,
        string baseName,
        string language,
        string? script,
        string? region,
        string[] variants,
        string? calendar,
        string? caseFirst,
        string? collation,
        string? hourCycle,
        string? numberingSystem,
        bool? numeric,
        string? firstDayOfWeek,
        CultureInfo cultureInfo) : base(engine)
    {
        _prototype = prototype;
        Locale = locale;
        BaseName = baseName;
        Language = language;
        Script = script;
        Region = region;
        Variants = variants;
        Calendar = calendar;
        CaseFirst = caseFirst;
        Collation = collation;
        HourCycle = hourCycle;
        NumberingSystem = numberingSystem;
        Numeric = numeric;
        FirstDayOfWeek = firstDayOfWeek;
        CultureInfo = cultureInfo;
    }
}
