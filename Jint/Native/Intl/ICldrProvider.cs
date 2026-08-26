namespace Jint.Native.Intl;

/// <summary>
/// Provider interface for CLDR (Common Locale Data Repository) data.
/// Implementations can supply locale-specific data for Intl formatters.
/// </summary>
public interface ICldrProvider
{
    // === List Patterns (ListFormat) ===

    /// <summary>
    /// Gets list formatting patterns for a locale.
    /// </summary>
    /// <param name="locale">The locale identifier (e.g., "en-US").</param>
    /// <param name="type">List type: "conjunction", "disjunction", or "unit".</param>
    /// <param name="style">Style: "long", "short", or "narrow".</param>
    /// <returns>List patterns or null if not available.</returns>
    ListPatterns? GetListPatterns(string locale, string type, string style);

    // === Relative Time Patterns (RelativeTimeFormat) ===

    /// <summary>
    /// Gets relative time formatting patterns for a locale.
    /// </summary>
    /// <param name="locale">The locale identifier.</param>
    /// <param name="unit">Time unit: "second", "minute", "hour", "day", "week", "month", "quarter", "year".</param>
    /// <param name="style">Style: "long", "short", or "narrow".</param>
    /// <returns>Relative time patterns or null if not available.</returns>
    RelativeTimePatterns? GetRelativeTimePatterns(string locale, string unit, string style);

    /// <summary>
    /// Gets a special relative time phrase (e.g., "yesterday", "tomorrow", "last week").
    /// </summary>
    /// <param name="locale">The locale identifier.</param>
    /// <param name="unit">Time unit.</param>
    /// <param name="value">The relative value (-1, 0, or 1).</param>
    /// <param name="past">Whether the value represents past time.</param>
    /// <param name="style">Style: "long", "short", or "narrow".</param>
    /// <returns>Special phrase or null if not available (fallback to numeric format).</returns>
    string? GetRelativeTimeSpecialPhrase(string locale, string unit, int value, bool past, string style);

    // === Number Formatting (NumberFormat) ===

    /// <summary>
    /// Gets the digit characters for a numbering system.
    /// </summary>
    /// <param name="numberingSystem">The numbering system identifier (e.g., "latn", "arab").</param>
    /// <returns>A string of 10 digit characters (0-9) or null if not supported.</returns>
    string? GetNumberingSystemDigits(string numberingSystem);

    /// <summary>
    /// Gets the default numbering system for a locale (e.g., "ar-EG" → "arab", "en-US" → "latn").
    /// Used when a formatter is constructed without an explicit "nu" option or Unicode extension.
    /// </summary>
    /// <param name="locale">The locale identifier.</param>
    /// <returns>The default numbering system name, or null if the provider has no opinion (caller falls back to "latn").</returns>
    string? GetDefaultNumberingSystem(string locale);

    /// <summary>
    /// Gets the symbols and name <c>Intl.NumberFormat</c> writes beside an amount in this currency.
    /// </summary>
    /// <param name="locale">The locale identifier.</param>
    /// <param name="currencyCode">ISO 4217 currency code (e.g., "USD").</param>
    /// <returns>Currency data, or null to fall back to the currency code itself.</returns>
    /// <remarks>
    /// Read once per <c>Intl.NumberFormat</c> construction, not per <c>format()</c> call, and only when
    /// <c>style</c> is <c>"currency"</c>. <c>currencyDisplay: "code"</c> never reads it: the specification
    /// fixes that display to the currency code.
    /// </remarks>
    CurrencyData? GetCurrencyData(string locale, string currencyCode);

    /// <summary>
    /// Gets unit formatting patterns for a locale.
    /// </summary>
    /// <param name="locale">The locale identifier.</param>
    /// <param name="unit">The unit identifier (e.g., "meter", "kilogram").</param>
    /// <param name="style">Style: "long", "short", or "narrow".</param>
    /// <returns>Unit patterns or null if not available.</returns>
    UnitPatterns? GetUnitPatterns(string locale, string unit, string style);

    // === Date/Time Formatting (DateTimeFormat) ===

    /// <summary>
    /// Gets month names for a locale.
    /// </summary>
    /// <param name="locale">The locale identifier.</param>
    /// <param name="style">Style: "long", "short", "narrow", or "numeric".</param>
    /// <param name="calendar">Calendar identifier (e.g., "gregory", "buddhist"), or null for default.</param>
    /// <returns>Array of 12 month names (January-December) or null if not available.</returns>
    string[]? GetMonthNames(string locale, string style, string? calendar);

    /// <summary>
    /// Gets weekday names for a locale.
    /// </summary>
    /// <param name="locale">The locale identifier.</param>
    /// <param name="style">Style: "long", "short", or "narrow".</param>
    /// <returns>Array of 7 weekday names (Sunday-Saturday) or null if not available.</returns>
    string[]? GetWeekdayNames(string locale, string style);

    /// <summary>
    /// Gets day period names (AM/PM) for a locale.
    /// </summary>
    /// <param name="locale">The locale identifier.</param>
    /// <param name="style">Style: "long", "short", or "narrow".</param>
    /// <param name="calendar">Calendar identifier (e.g., "gregory", "buddhist"), or null for default.</param>
    /// <returns>Array of day period names or null if not available.</returns>
    string[]? GetDayPeriods(string locale, string style, string? calendar);

    /// <summary>
    /// Gets era names for a locale.
    /// </summary>
    /// <param name="locale">The locale identifier.</param>
    /// <param name="style">Style: "long", "short", or "narrow".</param>
    /// <param name="calendar">Calendar identifier (e.g., "gregory", "japanese"), or null for default.</param>
    /// <returns>Array of era names or null if not available.</returns>
    string[]? GetEraNames(string locale, string style, string? calendar);

    // === Display Names ===

    /// <summary>
    /// Gets the display name for a currency code.
    /// </summary>
    /// <param name="locale">The locale for localization.</param>
    /// <param name="code">The currency code to get the name for.</param>
    /// <returns>Display name or null if not available.</returns>
    string? GetCurrencyDisplayName(string locale, string code);

    // === Locale Data ===

    /// <summary>
    /// Gets the day a week starts on and the days that are weekend, for a locale.
    /// </summary>
    /// <param name="locale">The locale identifier.</param>
    /// <returns>Week info, or null to fall back to the embedded CLDR week data.</returns>
    /// <remarks>
    /// Read by <c>Intl.Locale.prototype.getWeekInfo</c>, once per call. A <c>-u-fw-</c> extension on the
    /// locale takes precedence over <see cref="WeekInfo.FirstDay"/>, as the specification requires.
    /// </remarks>
    WeekInfo? GetWeekInfo(string locale);

    // === Supported Values ===

    /// <summary>
    /// Gets all supported calendar identifiers.
    /// </summary>
    IReadOnlyCollection<string> GetSupportedCalendars();

    /// <summary>
    /// Gets all supported collation identifiers.
    /// </summary>
    IReadOnlyCollection<string> GetSupportedCollations();

    /// <summary>
    /// Gets all supported currency codes.
    /// </summary>
    IReadOnlyCollection<string> GetSupportedCurrencies();

    /// <summary>
    /// Gets all supported numbering system identifiers.
    /// </summary>
    IReadOnlyCollection<string> GetSupportedNumberingSystems();

    /// <summary>
    /// Gets all supported IANA timezone names.
    /// </summary>
    IReadOnlyCollection<string> GetSupportedTimeZones();

    /// <summary>
    /// Gets all supported unit identifiers.
    /// </summary>
    IReadOnlyCollection<string> GetSupportedUnits();
}
