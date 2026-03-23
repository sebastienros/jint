namespace Jint.Native.Intl;

/// <summary>
/// List formatting patterns from CLDR.
/// </summary>
public sealed class ListPatterns
{
    /// <summary>
    /// Pattern for the first pair in a list of 3+ items. E.g., "{0}, {1}".
    /// </summary>
    public required string Start { get; init; }

    /// <summary>
    /// Pattern for middle pairs in a list of 4+ items. E.g., "{0}, {1}".
    /// </summary>
    public required string Middle { get; init; }

    /// <summary>
    /// Pattern for the last pair in a list of 3+ items. E.g., "{0}, and {1}".
    /// </summary>
    public required string End { get; init; }

    /// <summary>
    /// Pattern for exactly two items. E.g., "{0} and {1}".
    /// </summary>
    public required string Two { get; init; }
}

/// <summary>
/// Relative time formatting patterns from CLDR.
/// </summary>
public sealed class RelativeTimePatterns
{
    /// <summary>
    /// Pattern for future singular. E.g., "in {0} day".
    /// Legacy property for backwards compatibility. Use FuturePatterns for full plural form support.
    /// </summary>
    public string Future { get; init; } = "";

    /// <summary>
    /// Pattern for past singular. E.g., "{0} day ago".
    /// Legacy property for backwards compatibility. Use PastPatterns for full plural form support.
    /// </summary>
    public string Past { get; init; } = "";

    /// <summary>
    /// Pattern for future plural. E.g., "in {0} days".
    /// Legacy property for backwards compatibility. Use FuturePatterns for full plural form support.
    /// </summary>
    public string FuturePlural { get; init; } = "";

    /// <summary>
    /// Pattern for past plural. E.g., "{0} days ago".
    /// Legacy property for backwards compatibility. Use PastPatterns for full plural form support.
    /// </summary>
    public string PastPlural { get; init; } = "";

    /// <summary>
    /// Full plural form support for future patterns.
    /// Dictionary keyed by plural form (one, few, many, other).
    /// If set, this takes precedence over Future/FuturePlural.
    /// </summary>
    public Dictionary<string, string>? FuturePatterns { get; init; }

    /// <summary>
    /// Full plural form support for past patterns.
    /// Dictionary keyed by plural form (one, few, many, other).
    /// If set, this takes precedence over Past/PastPlural.
    /// </summary>
    public Dictionary<string, string>? PastPatterns { get; init; }
}

/// <summary>
/// Currency formatting data from CLDR.
/// </summary>
public sealed class CurrencyData
{
    /// <summary>
    /// Currency symbol. E.g., "$".
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Narrow currency symbol. E.g., "$" instead of "US$".
    /// </summary>
    public string? NarrowSymbol { get; init; }

    /// <summary>
    /// Display name for the currency. E.g., "US Dollar".
    /// </summary>
    public required string DisplayName { get; init; }
}

/// <summary>
/// Unit formatting patterns from CLDR.
/// Supports all CLDR plural categories: zero, one, two, few, many, other.
/// </summary>
public sealed class UnitPatterns
{
    /// <summary>
    /// Display name for the unit. E.g., "meters".
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Pattern for zero (some languages). E.g., "{0} ימים" in Hebrew for 0 days.
    /// </summary>
    public string? Zero { get; init; }

    /// <summary>
    /// Pattern for singular. E.g., "{0} meter".
    /// </summary>
    public string? One { get; init; }

    /// <summary>
    /// Pattern for dual (some languages like Arabic). E.g., "{0} يومان" for 2 days.
    /// </summary>
    public string? Two { get; init; }

    /// <summary>
    /// Pattern for few (some languages like Polish, Russian). E.g., "{0} dni" for 3-4 days.
    /// </summary>
    public string? Few { get; init; }

    /// <summary>
    /// Pattern for many (some languages like Polish, Russian). E.g., "{0} dni" for 5+ days.
    /// </summary>
    public string? Many { get; init; }

    /// <summary>
    /// Pattern for plural/other. E.g., "{0} meters". Required fallback.
    /// </summary>
    public required string Other { get; init; }
}

/// <summary>
/// Compact number formatting patterns from CLDR.
/// </summary>
public sealed class CompactPatterns
{
    /// <summary>
    /// Patterns keyed by magnitude (3 for thousands, 6 for millions, etc.).
    /// Value is the pattern, e.g., "{0}K" or "{0} thousand".
    /// </summary>
    public required Dictionary<int, string> Patterns { get; init; }
}

/// <summary>
/// Date/time formatting patterns from CLDR.
/// </summary>
public sealed class DateTimePatterns
{
    /// <summary>
    /// Date pattern component.
    /// </summary>
    public string? DatePattern { get; init; }

    /// <summary>
    /// Time pattern component.
    /// </summary>
    public string? TimePattern { get; init; }

    /// <summary>
    /// Combined date/time pattern.
    /// </summary>
    public string? DateTimePattern { get; init; }
}

/// <summary>
/// Week information from CLDR.
/// </summary>
public sealed class WeekInfo
{
    /// <summary>
    /// First day of the week.
    /// </summary>
    public required DayOfWeek FirstDay { get; init; }

    /// <summary>
    /// Minimal days in first week of year.
    /// </summary>
    public required int MinimalDays { get; init; }

    /// <summary>
    /// Weekend days.
    /// </summary>
    public DayOfWeek[]? Weekend { get; init; }
}
