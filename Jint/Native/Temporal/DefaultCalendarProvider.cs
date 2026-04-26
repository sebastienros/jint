namespace Jint.Native.Temporal;

/// <summary>
/// Default <see cref="ICalendarProvider"/> implementation backed by
/// <see cref="System.Globalization.Calendar"/> subclasses (Hebrew, Persian, UmAlQura, …)
/// plus inline epoch arithmetic for Coptic / Ethiopic / Islamic-civil / Islamic-tbla / Indian.
/// Range-limited per the underlying BCL calendars; for full historical accuracy
/// (e.g. islamic-umalqura, Persian astronomical, Chinese/Dangi at extreme years)
/// register a custom provider via <see cref="Options.TemporalOptions.CalendarProvider"/>.
/// </summary>
public sealed class DefaultCalendarProvider : ICalendarProvider
{
    /// <summary>Singleton instance.</summary>
    public static readonly DefaultCalendarProvider Instance = new();

    private static readonly string[] SupportedCalendars =
    [
        "chinese", "dangi", "hebrew", "persian",
        "coptic", "ethiopic", "ethioaa", "indian",
        "islamic-umalqura", "islamic-civil", "islamic-tbla",
    ];

    private DefaultCalendarProvider() { }

    /// <inheritdoc />
    public bool IsSupported(string calendar) => NonIsoCalendars.IsNonIsoCalendar(calendar);

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetSupportedCalendars() => SupportedCalendars;

    /// <inheritdoc />
    public CalendarFields IsoToCalendarFields(string calendar, int isoYear, int isoMonth, int isoDay)
    {
        var calDate = NonIsoCalendars.IsoToCalendarDate(calendar, new IsoDate(isoYear, isoMonth, isoDay));
        return new CalendarFields(
            calDate.Year, calDate.Month, calDate.MonthCode, calDate.Day,
            calDate.IsLeapMonth, calDate.MonthsInYear, calDate.DaysInMonth,
            calDate.DaysInYear, calDate.InLeapYear);
    }

    /// <inheritdoc />
    public IsoDateFields? CalendarFieldsToIso(string calendar, int year, string? monthCode, int month, int day, string overflow)
    {
        var iso = NonIsoCalendars.CalendarDateToIso(calendar, year, monthCode, month, day, overflow);
        return iso is null ? null : new IsoDateFields(iso.Value.Year, iso.Value.Month, iso.Value.Day);
    }
}
