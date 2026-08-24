namespace Jint.Native.Temporal;

/// <summary>
/// The non-ISO calendar arithmetic an engine uses unless the host replaces it, backed by BCL
/// <see cref="System.Globalization.Calendar"/> subclasses and inline epoch arithmetic.
/// </summary>
/// <remarks>
/// <para>
/// Every member is <c>virtual</c>, so correcting one calendar means deriving from this class and
/// overriding the one member that answers for it. The rest are inherited, and delegate to the BCL as before.
/// </para>
/// <para>
/// Install the derived instance on <see cref="Options.TemporalOptions.CalendarProvider"/>; leaving that
/// property alone keeps <see cref="Instance"/>, which the engine recognizes by identity and answers inline.
/// </para>
/// <para>
/// Range-limited per the underlying BCL calendars. Adding a calendar Jint does not know is not a
/// one-member job: <see cref="IsSupported"/>, <see cref="GetSupportedCalendars"/> and both conversions
/// all have to answer for it, or the inherited conversion throws on an identifier it has never seen.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// sealed class AstronomicalPersian : DefaultCalendarProvider
/// {
///     public override IsoDateFields? CalendarFieldsToIso(
///         string calendar, int year, string? monthCode, int month, int day, string overflow)
///         => calendar == "persian" ? MyTables.ToIso(year, month, day) : base.CalendarFieldsToIso(calendar, year, monthCode, month, day, overflow);
/// }
///
/// options.Temporal.CalendarProvider = new AstronomicalPersian();
/// </code>
/// </example>
public class DefaultCalendarProvider : ICalendarProvider
{
    /// <summary>Singleton instance.</summary>
    public static readonly DefaultCalendarProvider Instance = new();

    private static readonly string[] SupportedCalendars =
    [
        "chinese", "dangi", "hebrew", "persian",
        "coptic", "ethiopic", "ethioaa", "indian",
        "islamic-umalqura", "islamic-civil", "islamic-tbla",
    ];

    /// <summary>
    /// Initializes a new instance a derived provider builds on; hosts wanting the default arithmetic read <see cref="Instance"/>.
    /// </summary>
    protected DefaultCalendarProvider() { }

    /// <inheritdoc />
    public virtual bool IsSupported(string calendar) => NonIsoCalendars.IsNonIsoCalendar(calendar);

    /// <inheritdoc />
    public virtual IReadOnlyCollection<string> GetSupportedCalendars() => SupportedCalendars;

    /// <inheritdoc />
    public virtual CalendarFields IsoToCalendarFields(string calendar, int isoYear, int isoMonth, int isoDay)
    {
        var calDate = NonIsoCalendars.IsoToCalendarDate(calendar, new IsoDate(isoYear, isoMonth, isoDay));
        return new CalendarFields(
            calDate.Year, calDate.Month, calDate.MonthCode, calDate.Day,
            calDate.IsLeapMonth, calDate.MonthsInYear, calDate.DaysInMonth,
            calDate.DaysInYear, calDate.InLeapYear);
    }

    /// <inheritdoc />
    public virtual IsoDateFields? CalendarFieldsToIso(string calendar, int year, string? monthCode, int month, int day, string overflow)
    {
        var iso = NonIsoCalendars.CalendarDateToIso(calendar, year, monthCode, month, day, overflow);
        return iso is null ? null : new IsoDateFields(iso.Value.Year, iso.Value.Month, iso.Value.Day);
    }
}
