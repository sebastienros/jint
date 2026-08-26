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
/// Range-limited per the underlying BCL calendars. <em>Adding</em> a calendar Jint does not know is three
/// overrides, and no fewer: <see cref="GetSupportedCalendars"/>, which is what makes the identifier valid
/// anywhere in <c>Temporal</c>, and both conversions, which nobody but the host can perform for a calendar
/// the engine has never heard of. The inherited <see cref="IsSupported"/> reads the list, so the two answer
/// consistently without the host keeping them in step; override it as well only to make the membership test
/// cheaper than a scan of the list.
/// </para>
/// <para>
/// A calendar added this way reaches construction from fields and from a <c>[u-ca=…]</c> annotation, every
/// field accessor, <c>with</c>, <c>toString</c> and the <c>PlainYearMonth</c> / <c>PlainMonthDay</c>
/// conversions. It does not reach calendar arithmetic — <c>add</c>, <c>subtract</c>, <c>until</c>,
/// <c>since</c> — which is implemented per calendar inside the engine and raises a <c>RangeError</c> for a
/// calendar it does not implement; nor <c>Intl.DateTimeFormat</c>, which has its own calendar list, so
/// <c>toLocaleString</c> on such a date raises a <c>RangeError</c> too.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Correcting a calendar Jint already knows: one override.
/// sealed class AstronomicalPersian : DefaultCalendarProvider
/// {
///     public override IsoDateFields? CalendarFieldsToIso(
///         string calendar, int year, string? monthCode, int month, int day, string overflow)
///         => calendar == "persian" ? MyTables.ToIso(year, month, day) : base.CalendarFieldsToIso(calendar, year, monthCode, month, day, overflow);
/// }
///
/// options.Temporal.CalendarProvider = new AstronomicalPersian();
///
/// // Adding one it does not know: the list, plus the two conversions.
/// sealed class WithMayan : DefaultCalendarProvider
/// {
///     public override IReadOnlyCollection&lt;string&gt; GetSupportedCalendars() => [.. base.GetSupportedCalendars(), "mayan"];
///     public override CalendarFields IsoToCalendarFields(string calendar, int isoYear, int isoMonth, int isoDay) => …;
///     public override IsoDateFields? CalendarFieldsToIso(string calendar, int year, string? monthCode, int month, int day, string overflow) => …;
/// }
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
    /// <remarks>
    /// Answered from <see cref="GetSupportedCalendars"/>, so a derived provider that adds a calendar to the
    /// list does not also have to remember to claim it here. That means the list is read on a Temporal
    /// validation path: return a cached collection from <see cref="GetSupportedCalendars"/>, and override
    /// this member too if the list is long enough that scanning it matters.
    /// </remarks>
    public virtual bool IsSupported(string calendar)
    {
        var supported = GetSupportedCalendars();

        // The default is a string[], which iterates without allocating an enumerator.
        if (supported is string[] array)
        {
            foreach (var candidate in array)
            {
                if (string.Equals(candidate, calendar, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var candidate in supported)
        {
            if (string.Equals(candidate, calendar, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

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
